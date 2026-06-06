namespace ClaudeForge.Core.Ports;

/// <summary>
/// Immutable metadata record for a stored plugin package artifact.
/// </summary>
/// <param name="Sha256">Lowercase hexadecimal SHA-256 hash (exactly 64 characters).</param>
/// <param name="SizeBytes">Exact byte count of the stored content.</param>
public sealed record PackageMetadata(string Sha256, long SizeBytes);
