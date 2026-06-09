using System.IO;
using System.Text;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive;
using IndQuestResults;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExxerCube.Prisma.Tests.Infrastructure.Export.Adaptive;

/// <summary>
/// Mutation-killing tests for <see cref="AdaptiveResponseExporterAdapter"/>, the
/// IResponseExporter → IAdaptiveExporter adapter. Drives a substituted
/// <see cref="IAdaptiveExporter"/> to pin: constructor null-guards, the "XML" template
/// type passed on delegation, error propagation, the null-bytes guard, the exact
/// stream write (offset/length), and the PDF stub message.
/// </summary>
public sealed class AdaptiveResponseExporterAdapterTests
{
    private readonly IAdaptiveExporter _adaptive = Substitute.For<IAdaptiveExporter>();
    private readonly AdaptiveResponseExporterAdapter _adapter;

    public AdaptiveResponseExporterAdapterTests()
    {
        _adapter = new AdaptiveResponseExporterAdapter(_adaptive, NullLogger<AdaptiveResponseExporterAdapter>.Instance);
    }

    [Fact]
    public void Constructor_WhenAdaptiveExporterNull_Throws()
    {
        var ex = Record.Exception(() =>
            new AdaptiveResponseExporterAdapter(null!, NullLogger<AdaptiveResponseExporterAdapter>.Instance));

        ex.ShouldBeOfType<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WhenLoggerNull_Throws()
    {
        var ex = Record.Exception(() =>
            new AdaptiveResponseExporterAdapter(_adaptive, null!));

        ex.ShouldBeOfType<ArgumentNullException>();
    }

    [Fact]
    public async Task ExportSiroXmlAsync_OnSuccess_WritesExactBytesAndDelegatesWithXmlType()
    {
        var payload = Encoding.UTF8.GetBytes("<SiroResponse/>");
        _adaptive.ExportAsync(Arg.Any<object>(), "XML", Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Success(payload));
        using var stream = new MemoryStream();

        var result = await _adapter.ExportSiroXmlAsync(
            new UnifiedMetadataRecord(), stream, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        // Exact bytes (offset 0, full length) round-trip to the stream.
        stream.ToArray().ShouldBe(payload);
        // Delegation uses the "XML" template type.
        await _adaptive.Received(1).ExportAsync(Arg.Any<object>(), "XML", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportSiroXmlAsync_WhenDelegateFails_PropagatesErrorAndWritesNothing()
    {
        _adaptive.ExportAsync(Arg.Any<object>(), "XML", Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Failure("delegate boom"));
        using var stream = new MemoryStream();

        var result = await _adapter.ExportSiroXmlAsync(
            new UnifiedMetadataRecord(), stream, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("delegate boom");
        stream.Length.ShouldBe(0);
    }

    [Fact]
    public async Task ExportSiroXmlAsync_WhenDelegateReturnsNullBytes_ReturnsNullBytesFailure()
    {
        _adaptive.ExportAsync(Arg.Any<object>(), "XML", Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Success(null!));
        using var stream = new MemoryStream();

        var result = await _adapter.ExportSiroXmlAsync(
            new UnifiedMetadataRecord(), stream, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("null bytes");
        stream.Length.ShouldBe(0);
    }

    [Fact]
    public async Task ExportSignedPdfAsync_ReturnsNotYetImplementedFailure()
    {
        using var stream = new MemoryStream();

        var result = await _adapter.ExportSignedPdfAsync(
            new UnifiedMetadataRecord(), stream, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("PDF signing functionality will be implemented in Story 1.8");
    }
}
