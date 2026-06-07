using System.Security.Cryptography;
using ClaudeForge.Core.Identity.Ports;

namespace ClaudeForge.Infrastructure.Identity;

/// <summary>
/// Builds a JWKS document from a list of RSA public key PEMs.
/// Supports multiple keys during rotation (current + prior key).
/// Implements <see cref="IJwksProvider"/>.
/// </summary>
public sealed class RsaJwksProvider : IJwksProvider
{
    private readonly IReadOnlyList<(string PublicPem, string Kid)> _activeKeys;

    /// <param name="activeKeys">
    /// Each entry is a PEM-encoded RSA public key paired with a key identifier string.
    /// During key rotation, supply both the current and prior public keys.
    /// </param>
    public RsaJwksProvider(IReadOnlyList<(string PublicPem, string Kid)> activeKeys)
    {
        _activeKeys = activeKeys;
    }

    /// <inheritdoc />
    public JwksDocument GetCurrentKeys()
    {
        List<JwksKey> keys = new(_activeKeys.Count);

        foreach ((string publicPem, string kid) in _activeKeys)
        {
            using RSA rsa = RSA.Create();
            rsa.ImportFromPem(publicPem);

            RSAParameters parameters = rsa.ExportParameters(includePrivateParameters: false);

            string n = Base64UrlEncode(parameters.Modulus!);
            string e = Base64UrlEncode(parameters.Exponent!);

            keys.Add(new JwksKey(
                Kty: "RSA",
                Use: "sig",
                Alg: "RS256",
                Kid: kid,
                N: n,
                E: e));
        }

        return new JwksDocument(keys);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
