using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.PluginPublishing.UseCases;

/// <summary>
/// Thrown when no package file is provided in the upload request.
/// </summary>
public sealed class MissingPackageFileException : ProblemDetailsException
{
    public MissingPackageFileException()
        : base("Package file is required") { }
}
