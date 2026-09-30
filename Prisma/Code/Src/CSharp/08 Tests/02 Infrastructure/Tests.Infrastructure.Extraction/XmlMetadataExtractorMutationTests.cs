using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Mutation-killing tests for <see cref="XmlMetadataExtractor"/> (the <see cref="IMetadataExtractor"/>
/// adapter over <see cref="IXmlNullableParser{Expediente}"/>). Drives a substituted parser to pin the
/// RFC/name/date/legal-reference projections, the <c>Length &gt; 0 ? x : null</c> empty-to-null ternaries,
/// the name concatenation + <c>Trim</c>, the null-expediente guard, and the text/docx/pdf paths.
/// </summary>
public class XmlMetadataExtractorMutationTests
{
    private readonly IXmlNullableParser<Expediente> _parser = Substitute.For<IXmlNullableParser<Expediente>>();
    private readonly XmlMetadataExtractor _extractor;

    public XmlMetadataExtractorMutationTests()
    {
        _extractor = new XmlMetadataExtractor(_parser, Substitute.For<ILogger<XmlMetadataExtractor>>());
    }

    private void ParserReturns(Result<Expediente> result) =>
        _parser.ParseAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(result);

    private Task<Result<ExtractedMetadata>> Extract() =>
        _extractor.ExtractFromXmlAsync(new byte[] { 1 }, TestContext.Current.CancellationToken);

    // ---- projections from a fully-populated expediente ----

    [Fact]
    public async Task ExtractFromXmlAsync_FullExpediente_ProjectsAllCollections()
    {
        var expediente = new Expediente
        {
            FechaPublicacion = new DateTime(2025, 1, 15),
            Referencia = "REF-0",
            Referencia1 = "REF-1",
            Referencia2 = "REF-2",
            SolicitudPartes =
            {
                new SolicitudParte { Nombre = "Juan", Paterno = "Perez", Materno = "Lopez", Rfc = "PERJ800101ABC" },
                new SolicitudParte { Nombre = "Ana", Paterno = null, Materno = null, Rfc = "GOMA900202XYZ" },
            },
        };
        ParserReturns(Result<Expediente>.Success(expediente));

        var result = await Extract();

        result.IsSuccess.ShouldBeTrue();
        var m = result.Value!;
        m.RfcValues.ShouldBe(new[] { "PERJ800101ABC", "GOMA900202XYZ" });
        // Name is "{Nombre} {Paterno} {Materno}".Trim(): second party trims trailing spaces to just "Ana".
        m.Names.ShouldBe(new[] { "Juan Perez Lopez", "Ana" });
        m.Dates.ShouldBe(new[] { new DateTime(2025, 1, 15) });
        m.LegalReferences.ShouldBe(new[] { "REF-0", "REF-1", "REF-2" });
        m.Expediente.ShouldBeSameAs(expediente);
    }

    [Fact]
    public async Task ExtractFromXmlAsync_EmptyExpediente_AllCollectionsNull()
    {
        // No parties, MinValue date, blank references -> every "Length > 0 ? x : null" yields null.
        ParserReturns(Result<Expediente>.Success(new Expediente
        {
            FechaPublicacion = DateTime.MinValue,
            Referencia = string.Empty,
            Referencia1 = string.Empty,
            Referencia2 = string.Empty,
        }));

        var result = await Extract();

        result.IsSuccess.ShouldBeTrue();
        var m = result.Value!;
        m.RfcValues.ShouldBeNull();
        m.Names.ShouldBeNull();
        m.Dates.ShouldBeNull();
        m.LegalReferences.ShouldBeNull();
    }

    [Fact]
    public async Task ExtractFromXmlAsync_PartyWithBlankRfc_ExcludedFromRfcValues()
    {
        ParserReturns(Result<Expediente>.Success(new Expediente
        {
            SolicitudPartes =
            {
                new SolicitudParte { Nombre = "Juan", Rfc = "" },     // filtered out (blank Rfc)
                new SolicitudParte { Nombre = "Ana", Rfc = "GOMA900202XYZ" },
            },
        }));

        var result = await Extract();

        result.Value!.RfcValues.ShouldBe(new[] { "GOMA900202XYZ" });
    }

    [Fact]
    public async Task ExtractFromXmlAsync_NameTrimmedToContentOnly()
    {
        // Nombre only -> "Ana  ".Trim() == "Ana" (no trailing spaces). Kills the .Trim() removal.
        ParserReturns(Result<Expediente>.Success(new Expediente
        {
            SolicitudPartes = { new SolicitudParte { Nombre = "Ana", Paterno = null, Materno = null } },
        }));

        var result = await Extract();

        result.Value!.Names.ShouldBe(new[] { "Ana" });
    }

    [Fact]
    public async Task ExtractFromXmlAsync_ValidDate_IncludedInDates()
    {
        ParserReturns(Result<Expediente>.Success(new Expediente { FechaPublicacion = new DateTime(2024, 6, 1) }));

        var result = await Extract();

        result.Value!.Dates.ShouldBe(new[] { new DateTime(2024, 6, 1) });
    }

    [Fact]
    public async Task ExtractFromXmlAsync_OnlyFirstReference_OthersBlankFilteredOut()
    {
        ParserReturns(Result<Expediente>.Success(new Expediente
        {
            Referencia = "REF-ONLY",
            Referencia1 = string.Empty,
            Referencia2 = string.Empty,
        }));

        var result = await Extract();

        result.Value!.LegalReferences.ShouldBe(new[] { "REF-ONLY" });
    }

    // ---- guards / failure propagation ----

    [Fact]
    public async Task ExtractFromXmlAsync_ParserSucceedsWithNull_ReturnsFailure()
    {
        ParserReturns(Result<Expediente>.Success(null!));

        var result = await Extract();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("null");
    }

    [Fact]
    public async Task ExtractFromXmlAsync_ParserFails_PropagatesError()
    {
        ParserReturns(Result<Expediente>.WithFailure("boom-parse"));

        var result = await Extract();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("boom-parse");
    }

    [Fact]
    public async Task ExtractFromXmlAsync_ParserThrows_CaughtAndWrappedWithMessage()
    {
        // Drives the catch block: the wrapped failure message must include the exception text.
        _parser.ParseAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<Expediente>>>(_ => throw new InvalidOperationException("kaboom-xml"));

        var result = await Extract();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("kaboom-xml");
    }

    // ---- ExtractTextAsync ----

    [Fact]
    public async Task ExtractTextAsync_ValidContent_ReturnsDecodedUtf8()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("<Hola>Ñ</Hola>");

        var result = await _extractor.ExtractTextAsync(bytes, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("<Hola>Ñ</Hola>");
    }

    [Fact]
    public async Task ExtractTextAsync_PreCancelled_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _extractor.ExtractTextAsync(new byte[] { 1 }, cts.Token);

        result.IsSuccess.ShouldBeFalse();
    }

    // ---- unsupported formats ----

    [Fact]
    public async Task ExtractFromDocxAsync_ReturnsFailureMentioningDocxExtractor()
    {
        var result = await _extractor.ExtractFromDocxAsync(new byte[] { 1 }, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("DocxMetadataExtractor");
    }

    [Fact]
    public async Task ExtractFromPdfAsync_ReturnsFailureMentioningPdfExtractor()
    {
        var result = await _extractor.ExtractFromPdfAsync(new byte[] { 1 }, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("PdfMetadataExtractor");
    }
}
