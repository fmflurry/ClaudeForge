using Microsoft.Extensions.Options;

namespace ClaudeForge.Infrastructure.Identity.Validation;

/// <summary>
/// Validates <see cref="OidcOptions"/> at startup.
/// Reports ALL validation errors (no short-circuit) so operators see every missing secret.
/// </summary>
public sealed class OidcConfigValidator : IValidateOptions<OidcOptions>
{
    private readonly bool _isProduction;

    public OidcConfigValidator(bool isProduction)
    {
        _isProduction = isProduction;
    }

    public ValidateOptionsResult Validate(string? name, OidcOptions options)
    {
        List<string> errors = new();

        if (options.EnabledProviders is null || options.EnabledProviders.Length == 0)
        {
            // No enabled providers — valid for development.
            return ValidateOptionsResult.Success;
        }

        foreach (string providerName in options.EnabledProviders)
        {
            switch (providerName.ToLowerInvariant())
            {
                case "google":
                    ValidateProvider("google", options.Google, errors);
                    break;
                case "microsoft":
                    ValidateProvider("microsoft", options.Microsoft, errors);
                    break;
                default:
                    errors.Add(
                        $"Unknown provider '{providerName}' in OIDC__ENABLEDPROVIDERS. " +
                        "Only 'google' and 'microsoft' are supported.");
                    break;
            }
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(string.Join("; ", errors))
            : ValidateOptionsResult.Success;
    }

    private void ValidateProvider(string providerName, ProviderConfig? config, List<string> errors)
    {
        if (config is null)
        {
            errors.Add(
                $"Provider '{providerName}' is enabled but its configuration section is missing.");
            return;
        }

        string prefix = $"OIDC__{providerName.ToUpperInvariant()}__";

        if (string.IsNullOrWhiteSpace(config.ClientId))
        {
            errors.Add($"{prefix}CLIENTID is required for provider '{providerName}'.");
        }

        if (string.IsNullOrWhiteSpace(config.ClientSecret))
        {
            errors.Add($"{prefix}CLIENTSECRET is required for provider '{providerName}'.");
        }

        if (string.IsNullOrWhiteSpace(config.RedirectUri))
        {
            errors.Add($"{prefix}REDIRECTURI is required for provider '{providerName}'.");
        }
        else if (_isProduction)
        {
            // Production requires absolute HTTPS redirect URIs.
            bool isAbsoluteHttps =
                Uri.TryCreate(config.RedirectUri, UriKind.Absolute, out Uri? uri)
                && uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);

            if (!isAbsoluteHttps)
            {
                errors.Add(
                    $"{prefix}REDIRECTURI must be an absolute HTTPS URL in Production " +
                    $"(current value: '{config.RedirectUri}'). " +
                    "Non-HTTPS RedirectUri values are not allowed in Production environments.");
            }
        }
    }
}
