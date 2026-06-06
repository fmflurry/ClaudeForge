namespace ClaudeForge.Application.Modules.PluginDistribution.UseCases;

/// <summary>
/// The resolved download artifact returned by <see cref="DownloadPluginUseCase"/>.
/// </summary>
public sealed record DownloadResult(
    Stream Stream,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256);
