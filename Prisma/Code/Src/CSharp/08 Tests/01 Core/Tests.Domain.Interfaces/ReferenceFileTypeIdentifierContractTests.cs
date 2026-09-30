using ExxerCube.Prisma.Domain.Enum;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Implementation instance of <see cref="FileTypeIdentifierContract"/> (ADR-005 worked
/// example): demonstrates how a concrete implementation inherits the contract suite.
/// </summary>
/// <remarks>
/// The SUT here is a hand-written reference implementation rather than the production
/// <c>FileTypeIdentifierService</c>, so the N + 1 mechanics are proven without touching
/// Infrastructure test projects before their refactor phase. The production service
/// gains its own <c>FileTypeIdentifierServiceContractTests</c> in the Phase 6 sweep.
/// </remarks>
public sealed class ReferenceFileTypeIdentifierContractTests : FileTypeIdentifierContract
{
    /// <summary>
    /// Initializes the implementation instance with the reference implementation.
    /// </summary>
    public ReferenceFileTypeIdentifierContractTests()
        : base(new ReferenceFileTypeIdentifier())
    {
    }
}

/// <summary>
/// Minimal real (non-mock) <see cref="IFileTypeIdentifier"/> used by the worked example —
/// honest logic, no NSubstitute, implementing exactly the contracted behavior.
/// </summary>
internal sealed class ReferenceFileTypeIdentifier : IFileTypeIdentifier
{
    /// <inheritdoc />
    public Task<Result<FileFormat>> IdentifyFileTypeAsync(
        byte[] fileContent,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<FileFormat>());
        }

        if (fileContent is null || fileContent.Length == 0)
        {
            return Task.FromResult(Result<FileFormat>.WithFailure("File content is null or empty"));
        }

        if (fileContent.Length >= 4 && fileContent[0] == 0x25 && fileContent[1] == 0x50 && fileContent[2] == 0x44 && fileContent[3] == 0x46)
        {
            return Task.FromResult(Result<FileFormat>.Success(FileFormat.Pdf));
        }

        var extension = string.IsNullOrEmpty(fileName) ? null : System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        var fallback = extension switch
        {
            ".pdf" => Result<FileFormat>.Success(FileFormat.Pdf),
            ".xml" => Result<FileFormat>.Success(FileFormat.Xml),
            ".docx" => Result<FileFormat>.Success(FileFormat.Docx),
            _ => Result<FileFormat>.WithFailure("Unable to identify file type"),
        };
        return Task.FromResult(fallback);
    }
}
