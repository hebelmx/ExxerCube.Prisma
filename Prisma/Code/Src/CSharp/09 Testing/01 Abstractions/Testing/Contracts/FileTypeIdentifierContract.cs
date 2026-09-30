using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using IndQuestResults.Operations;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="IFileTypeIdentifier"/> — every implementation
/// (and the mock blueprint) must pass these tests unchanged.
/// </summary>
/// <remarks>
/// <para>
/// <strong>This is the canonical ADR-005 worked example.</strong> It demonstrates the
/// default SUT mechanism: a constructor-injected, interface-typed <see cref="Sut"/>.
/// The class is <c>abstract</c>, so xUnit does not discover it; each inherited
/// <c>[Fact]</c> runs once per deriving class (N + 1 inheritors: the mock blueprint
/// in <c>Tests.Domain.Interfaces</c> plus one instance per implementation).
/// </para>
/// <para>
/// Scope rule (ADR-005 §5): only behavior ANY correct implementation must exhibit —
/// Result semantics, null/empty handling, cancellation, and behaviors promised by the
/// interface's XML documentation (extension fallback). Signature-detection specifics
/// beyond the universal PDF magic-number case stay in implementation test projects.
/// </para>
/// </remarks>
public abstract class FileTypeIdentifierContract
{
    /// <summary>
    /// Initializes the contract with the implementation under test.
    /// </summary>
    /// <param name="sut">The <see cref="IFileTypeIdentifier"/> implementation to verify.</param>
    protected FileTypeIdentifierContract(IFileTypeIdentifier sut)
    {
        ArgumentNullException.ThrowIfNull(sut);
        Sut = sut;
    }

    /// <summary>Gets the implementation under test.</summary>
    protected IFileTypeIdentifier Sut { get; }

    /// <summary>
    /// Contract: null content yields a failure Result — never a thrown exception, never a null Result.
    /// </summary>
    [Fact]
    public async Task IdentifyFileTypeAsync_NullContent_ReturnsFailure()
    {
        // Act
        var result = await Sut.IdentifyFileTypeAsync(null!, null, TestContext.Current.CancellationToken);

        // Assert - Contract: failures are Results, not exceptions
        result.ShouldNotBeNull();
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>
    /// Contract: empty content yields a failure Result.
    /// </summary>
    [Fact]
    public async Task IdentifyFileTypeAsync_EmptyContent_ReturnsFailure()
    {
        // Arrange
        var emptyContent = Array.Empty<byte>();

        // Act
        var result = await Sut.IdentifyFileTypeAsync(emptyContent, null, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>
    /// Contract: content starting with the PDF magic number (%PDF) is identified as
    /// <see cref="FileFormat.Pdf"/> by content alone — no filename required.
    /// </summary>
    [Fact]
    public async Task IdentifyFileTypeAsync_PdfMagicBytes_ReturnsPdf()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // %PDF-1.4

        // Act
        var result = await Sut.IdentifyFileTypeAsync(pdfContent, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(FileFormat.Pdf);
    }

    /// <summary>
    /// Contract (promised by the interface documentation): when content analysis cannot
    /// identify the type, the optional filename is used as a fallback.
    /// </summary>
    [Fact]
    public async Task IdentifyFileTypeAsync_UnknownContentWithKnownExtension_FallsBackToExtension()
    {
        // Arrange
        var unknownContent = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        // Act
        var result = await Sut.IdentifyFileTypeAsync(unknownContent, "test.pdf", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(FileFormat.Pdf);
    }

    /// <summary>
    /// Contract: unidentifiable content with no filename yields a failure Result, not a throw.
    /// </summary>
    [Fact]
    public async Task IdentifyFileTypeAsync_UnknownContentWithoutFileName_ReturnsFailure()
    {
        // Arrange
        var unknownContent = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        // Act
        var result = await Sut.IdentifyFileTypeAsync(unknownContent, null, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>
    /// Contract: a pre-cancelled token yields a Cancelled Result (repository-wide
    /// CancellationToken mandate) — never an <see cref="OperationCanceledException"/>.
    /// </summary>
    [Fact]
    public async Task IdentifyFileTypeAsync_PreCancelledToken_ReturnsCancelled()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // valid input, so only the token can stop it
        var cancelledToken = new CancellationToken(canceled: true);

        // Act
        var result = await Sut.IdentifyFileTypeAsync(pdfContent, null, cancelledToken);

        // Assert
        result.ShouldNotBeNull();
        result.IsCancelled().ShouldBeTrue();
    }
}
