using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.AddOnPublishing.UseCases;

/// <summary>
/// Thrown when a manifest tag value is not present in the controlled category vocabulary
/// for its dimension. The message names the invalid value(s) and allowed vocabulary so the
/// CLI caller can correct the manifest.
/// HTTP 400 Bad Request.
/// </summary>
public sealed class UnknownCategoryTagException : ProblemDetailsException
{
    public UnknownCategoryTagException(string dimension, IReadOnlyList<string> invalidValues)
        : base(BuildMessage(dimension, invalidValues)) { }

    private static string BuildMessage(string dimension, IReadOnlyList<string> invalidValues)
    {
        string joined = string.Join(", ", invalidValues.Select(v => $"'{v}'"));
        return $"Unknown {dimension} tag value(s): {joined}. Use the /api/v1/categories endpoint to list valid values.";
    }
}
