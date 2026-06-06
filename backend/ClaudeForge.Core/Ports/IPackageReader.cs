using ClaudeForge.Core.Domain.Packaging;

namespace ClaudeForge.Core.Ports;

/// <summary>
/// Reads a plugin archive stream and extracts the manifest and optional README.
/// Format is detected from <c>fileName</c>: ".tar.gz" or ".zip".
/// </summary>
public interface IPackageReader
{
    /// <summary>
    /// Reads <paramref name="archiveStream"/> and extracts its manifest and README.
    /// </summary>
    /// <param name="archiveStream">Seekable or non-seekable archive bytes.</param>
    /// <param name="fileName">
    /// Original file name used to detect format (e.g. "my-plugin-1.0.0.tar.gz" or "package.zip").
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Extracted <see cref="PackageContents"/>.</returns>
    /// <exception cref="UnsupportedPackageFormatException">fileName extension is not tar.gz or zip.</exception>
    /// <exception cref="CorruptedArchiveException">Archive bytes cannot be read/decompressed.</exception>
    /// <exception cref="MissingManifestException">Archive contains no manifest at root level.</exception>
    Task<PackageContents> ReadAsync(Stream archiveStream, string fileName, CancellationToken ct = default);
}
