using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using IndQuestResults;
using IndQuestResults.Operations;
using NSubstitute;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Builds the contract-conforming NSubstitute mock that backs the blueprint instance
/// of <see cref="FileTypeIdentifierContract"/> (ADR-005 worked example).
/// </summary>
/// <remarks>
/// The configuration below is the <strong>executable design specification</strong>:
/// it encodes, in one place, the behavior any <see cref="IFileTypeIdentifier"/>
/// implementation must exhibit. It is argument-sensitive by necessity — the contract
/// asserts different outcomes for different inputs, which a blanket
/// <c>ReturnsForAnyArgs</c> stub could never satisfy. Growing such factories toward a
/// reference fake is expected and desirable (ADR-005 §6).
/// </remarks>
public static class FileTypeIdentifierMockFactory
{
    /// <summary>
    /// Creates an <see cref="IFileTypeIdentifier"/> mock that satisfies every test in
    /// <see cref="FileTypeIdentifierContract"/>.
    /// </summary>
    /// <returns>The configured mock.</returns>
    public static IFileTypeIdentifier CreateContractConformingMock()
    {
        var mock = Substitute.For<IFileTypeIdentifier>();

        mock.IdentifyFileTypeAsync(Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => Identify(
                call.ArgAt<byte[]?>(0),
                call.ArgAt<string?>(1),
                call.ArgAt<CancellationToken>(2)));

        return mock;
    }

    private static Result<FileFormat> Identify(byte[]? content, string? fileName, CancellationToken cancellationToken)
    {
        // Contract: a pre-cancelled token short-circuits to Cancelled, never throws.
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<FileFormat>();
        }

        // Contract: null/empty content is a failure Result, not an exception.
        if (content is null || content.Length == 0)
        {
            return Result<FileFormat>.WithFailure("File content is null or empty");
        }

        // Contract: the PDF magic number (%PDF) is identifiable from content alone.
        if (content.Length >= 4 && content[0] == 0x25 && content[1] == 0x50 && content[2] == 0x44 && content[3] == 0x46)
        {
            return Result<FileFormat>.Success(FileFormat.Pdf);
        }

        // Contract: unidentified content falls back to the optional filename.
        var extension = string.IsNullOrEmpty(fileName) ? null : Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => Result<FileFormat>.Success(FileFormat.Pdf),
            ".xml" => Result<FileFormat>.Success(FileFormat.Xml),
            ".docx" => Result<FileFormat>.Success(FileFormat.Docx),
            _ => Result<FileFormat>.WithFailure("Unable to identify file type"),
        };
    }
}
