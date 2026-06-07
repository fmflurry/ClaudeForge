using Amazon.Runtime;
using Amazon.S3;
using ClaudeForge.Api.Module;
using ClaudeForge.Application.Modules.PluginPublishing.Ports;
using ClaudeForge.Application.Modules.PluginPublishing.UseCases;
using ClaudeForge.Core.Ports;
using ClaudeForge.Core.Shared.Exceptions;
using ClaudeForge.Core.Shared.Model;
using ClaudeForge.Infrastructure.Packaging;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.PluginPublishing;
using ClaudeForge.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;


namespace ClaudeForge.Api.Modules.PluginPublishing;

/// <summary>
/// Feature module for Plugin Publishing endpoints:
///   POST   /api/v1/plugins/upload
///   POST   /api/v1/plugins/{pluginId:Guid}/versions
///   GET    /api/v1/plugins/{pluginId:Guid}/versions
///   GET    /api/v1/plugins/{pluginId:Guid}/versions/{version}
///   PATCH  /api/v1/plugins/{pluginId:Guid}/versions/{version}  → 405
/// </summary>
public sealed class PluginPublishingModule : IModule
{
    private const string UploadRateLimitPolicy = "plugin-upload-limit";

    public IServiceCollection RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        // Package storage — adapter selected at startup by StorageOptions.Type.
        services.AddSingleton<IPackageStoragePort>(sp =>
        {
            StorageOptions opts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;

            if (opts.Type == "OVHObjectStorage")
            {
                OvhStorageOptions ovh = opts.Ovh!;
                AmazonS3Config s3Config = new()
                {
                    ServiceURL = ovh.Endpoint,
                    ForcePathStyle = true,
                    AuthenticationRegion = "us-east-1"
                };
                IAmazonS3 s3Client = new AmazonS3Client(
                    new BasicAWSCredentials(ovh.AccessKey, ovh.SecretKey),
                    s3Config);
                return new OvhObjectStorageAdapter(s3Client, ovh.BucketName);
            }

            // Default: LocalFileSystem
            string configuredPath = opts.LocalPath
                ?? Path.Combine(Path.GetTempPath(), "claudeforge-packages");

            string localPath = configuredPath;
            try
            {
                Directory.CreateDirectory(localPath);
            }
            catch
            {
                // Fallback to a writable temp directory (e.g., in tests or non-Docker environments)
                localPath = Path.Combine(Path.GetTempPath(), "claudeforge-packages-" + Guid.NewGuid().ToString("N")[..8]);
                Directory.CreateDirectory(localPath);
            }

            return new LocalFileSystemPackageStorageAdapter(localPath);
        });

        // Package reader (stateless)
        services.AddSingleton<IPackageReader, PackageReader>();

        // Repository adapter
        services.AddScoped<IPluginPublishingRepositoryPort>(sp =>
            new PluginPublishingRepositoryAdapter(sp.GetRequiredService<MarketplaceDbContext>()));

        // Use cases
        services.AddScoped<UploadPluginUseCase>();
        services.AddScoped<PublishVersionUseCase>();

        // Per-IP rate limiting for upload and version-publish endpoints
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(UploadRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    }));
        });

        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/plugins/upload", UploadPluginHandler)
            .WithName("UploadPlugin")
            .WithTags("PluginPublishing")
            .RequireRateLimiting(UploadRateLimitPolicy)
            .DisableAntiforgery();

        endpoints.MapPost("/api/v1/plugins/{pluginId:guid}/versions", PublishVersionHandler)
            .WithName("PublishPluginVersion")
            .WithTags("PluginPublishing")
            .RequireRateLimiting(UploadRateLimitPolicy)
            .DisableAntiforgery();

        endpoints.MapGet("/api/v1/plugins/{pluginId:guid}/versions", GetVersionHistoryHandler)
            .WithName("GetVersionHistory")
            .WithTags("PluginPublishing");

        endpoints.MapGet("/api/v1/plugins/{pluginId:guid}/versions/{version}", GetVersionHandler)
            .WithName("GetVersion")
            .WithTags("PluginPublishing");

        endpoints.MapMethods(
            "/api/v1/plugins/{pluginId:guid}/versions/{version}",
            ["PATCH"],
            PatchVersionHandler)
            .WithName("PatchVersion")
            .WithTags("PluginPublishing");

        return endpoints;
    }

    // =========================================================================
    // Handlers
    // =========================================================================

    private static async Task<IResult> UploadPluginHandler(
        HttpRequest request,
        [FromServices] UploadPluginUseCase useCase)
    {
        IFormCollection form = await request.ReadFormAsync();

        if (!form.Files.Any() || form.Files["package"] is null)
            throw new MissingPackageFileException();

        IFormFile packageFile = form.Files["package"]!;
        string name = form["name"].FirstOrDefault() ?? string.Empty;
        string description = form["description"].FirstOrDefault() ?? string.Empty;
        string author = form["author"].FirstOrDefault() ?? string.Empty;
        string initialVersion = form["initialVersion"].FirstOrDefault() ?? string.Empty;
        string releaseNotes = form["releaseNotes"].FirstOrDefault() ?? string.Empty;

        UploadPluginCommand command = new(
            PackageStream: packageFile.OpenReadStream(),
            FileName: packageFile.FileName,
            Name: name,
            Description: description,
            Author: author,
            InitialVersion: initialVersion,
            ReleaseNotes: releaseNotes);

        PluginPublishResult result = await useCase.ExecuteAsync(command);

        return Results.Created(
            $"/api/v1/plugins/{result.PluginId}",
            new { pluginId = result.PluginId, version = result.Version });
    }

    private static async Task<IResult> PublishVersionHandler(
        Guid pluginId,
        HttpRequest request,
        [FromServices] PublishVersionUseCase useCase)
    {
        IFormCollection form = await request.ReadFormAsync();

        if (!form.Files.Any() || form.Files["package"] is null)
            throw new MissingPackageFileException();

        IFormFile packageFile = form.Files["package"]!;
        string version = form["versionNumber"].FirstOrDefault() ?? string.Empty;
        string releaseNotes = form["releaseNotes"].FirstOrDefault() ?? string.Empty;

        PublishVersionCommand command = new(
            PluginId: pluginId,
            PackageStream: packageFile.OpenReadStream(),
            FileName: packageFile.FileName,
            Version: version,
            ReleaseNotes: releaseNotes);

        PluginVersionPublishResult result = await useCase.ExecuteAsync(command);

        return Results.Created(
            $"/api/v1/plugins/{result.PluginId}/versions/{result.Version}",
            new { pluginId = result.PluginId, versionId = result.VersionId, version = result.Version });
    }

    private static async Task<IResult> GetVersionHistoryHandler(
        Guid pluginId,
        [FromServices] IPluginPublishingRepositoryPort repo,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        PaginationRequest pagination = new() { Page = page, Limit = limit };
        if (!pagination.IsValid(out string? error))
        {
            return Results.Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);
        }

        (IReadOnlyList<VersionHistoryDto> items, int totalCount) =
            await repo.GetVersionHistoryAsync(pluginId, pagination);

        PaginatedEnvelope<VersionHistoryDto> envelope = new()
        {
            Data = items,
            TotalCount = totalCount,
            Page = page,
            Limit = limit,
        };

        return Results.Ok(envelope);
    }

    private static async Task<IResult> GetVersionHandler(
        Guid pluginId,
        string version,
        [FromServices] IPluginPublishingRepositoryPort repo)
    {
        VersionDetailDto? detail = await repo.GetVersionAsync(pluginId, version);

        if (detail is null)
            throw new VersionNotFoundException();

        return Results.Ok(detail);
    }

    private static Task<IResult> PatchVersionHandler()
    {
        return Task.FromResult(Results.StatusCode(StatusCodes.Status405MethodNotAllowed));
    }
}

/// <summary>
/// Thrown when a specific version is not found.
/// </summary>
internal sealed class VersionNotFoundException : ProblemDetailsException
{
    public override int StatusCode => 404;

    public VersionNotFoundException()
        : base("Version not found") { }
}
