namespace ClaudeForge.Application.Modules.AddOnDistribution.UseCases;

/// <summary>
/// The resolved download artifact returned by <see cref="DownloadAddOnUseCase"/>.
/// </summary>
public sealed record DownloadResult(
    Stream Stream,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256);
