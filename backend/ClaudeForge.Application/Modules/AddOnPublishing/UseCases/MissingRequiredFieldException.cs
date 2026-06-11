using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.AddOnPublishing.UseCases;

/// <summary>
/// Thrown when a required manifest field is missing.
/// Message format: "Required field missing: {fieldName}"
/// </summary>
public sealed class MissingRequiredFieldException : ProblemDetailsException
{
    public MissingRequiredFieldException(string fieldName)
        : base($"Required field missing: {fieldName}") { }
}
