using ExxerCube.Prisma.Infrastructure.Extraction.Ocr;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Mutation-killing tests for <see cref="DocumentComparisonService"/>. Pins the CompareField status
/// ladder (both-empty=Match, one-empty=Missing, Ordinal exact=Match, fuzzy &gt;= 0.80=Partial else
/// Different) with exact similarity values, the Trim normalization, OcrConfidence passthrough, and the
/// CompareExpedientes aggregation (16 fields, MatchCount, exact Average, numeric/date/bool formatting).
/// </summary>
public class DocumentComparisonServiceMutationTests
{
    private readonly DocumentComparisonService _svc =
        new(Substitute.For<ILogger<DocumentComparisonService>>());

    // ---- CompareField status ladder ----

    [Fact]
    public void CompareField_ExactOrdinalMatch_MatchWith1()
    {
        var r = _svc.CompareField("F", "ABC-123", "ABC-123");

        r.Status.ShouldBe("Match");
        r.Similarity.ShouldBe(1.0f);
    }

    [Fact]
    public void CompareField_BothEmpty_MatchWith1()
    {
        var r = _svc.CompareField("F", "", "   ");

        r.Status.ShouldBe("Match");
        r.Similarity.ShouldBe(1.0f);
    }

    [Fact]
    public void CompareField_OneEmpty_MissingWith0()
    {
        var r = _svc.CompareField("F", "value", "");

        r.Status.ShouldBe("Missing");
        r.Similarity.ShouldBe(0.0f);
    }

    [Fact]
    public void CompareField_OtherEmpty_MissingWith0()
    {
        var r = _svc.CompareField("F", "", "value");

        r.Status.ShouldBe("Missing");
        r.Similarity.ShouldBe(0.0f);
    }

    [Fact]
    public void CompareField_DiffersOnlyByCase_NotExactUsesFuzzy()
    {
        // Exact match is Ordinal (case-sensitive); "ABC" vs "abc" is NOT exact. FuzzySharp is
        // case-sensitive too, so the ratio is 0 -> "Different". Kills Ordinal -> OrdinalIgnoreCase
        // (which would short-circuit to "Match").
        var r = _svc.CompareField("F", "ABC", "abc");

        r.Status.ShouldBe("Different");
        r.Similarity.ShouldBe(0.0f);
    }

    [Fact]
    public void CompareField_FuzzyExactly80Percent_PartialAt0_8()
    {
        // 5-char strings differing by exactly one char -> Fuzz.Ratio == 80 -> 0.8 >= 0.80 -> "Partial".
        // Kills `>=` -> `>` (80 > 80 is false) and the 0.8 threshold constant.
        var r = _svc.CompareField("F", "abcde", "abcdX");

        r.Similarity.ShouldBe(0.8f);
        r.Status.ShouldBe("Partial");
    }

    [Fact]
    public void CompareField_FuzzyBelow80Percent_Different()
    {
        // 5-char strings differing by two chars -> Fuzz.Ratio == 60 -> 0.6 < 0.80 -> "Different".
        var r = _svc.CompareField("F", "abcde", "abXYe");

        r.Similarity.ShouldBe(0.6f);
        r.Status.ShouldBe("Different");
    }

    [Fact]
    public void CompareField_TrimsBeforeComparing_Match()
    {
        // Both trim to "abc" -> exact match. Kills the .Trim() in normalization.
        var r = _svc.CompareField("F", "  abc  ", "abc");

        r.Status.ShouldBe("Match");
        r.Similarity.ShouldBe(1.0f);
    }

    [Fact]
    public void CompareField_PreservesOcrConfidence()
    {
        var r = _svc.CompareField("F", "v", "v", 0.73f);

        r.OcrConfidence.ShouldBe(0.73f);
    }

    [Fact]
    public void CompareField_SetsFieldNameAndValues()
    {
        var r = _svc.CompareField("MyField", " x ", "y");

        r.FieldName.ShouldBe("MyField");
        r.XmlValue.ShouldBe("x");   // normalized (trimmed)
        r.OcrValue.ShouldBe("y");
    }

    // ---- CompareExpedientes aggregation ----

    private static Expediente Sample() => new()
    {
        NumeroExpediente = "A/AS1-1111-222222-AAA",
        NumeroOficio = "222/AAA/-4444444444/2025",
        SolicitudSiara = "AGAFADAFSON2/2025/000084",
        Folio = 6789,
        OficioYear = 2025,
        AreaClave = 3,
        AreaDescripcion = "ASEGURAMIENTO",
        FechaPublicacion = new DateTime(2025, 6, 5),
        DiasPlazo = 7,
        AutoridadNombre = "SUBDELEGACION 8 SAN ANGEL",
        NombreSolicitante = null,
        Referencia = "",
        Referencia1 = "",
        Referencia2 = "IMSSCOB/40/01/001283/2025",
        TieneAseguramiento = true,
    };

    [Fact]
    public async Task CompareExpedientes_Identical_All16FieldsMatch()
    {
        var r = await _svc.CompareExpedientesAsync(Sample(), Sample(), TestContext.Current.CancellationToken);

        r.TotalFields.ShouldBe(16);     // 10 string + 4 numeric + 1 date + 1 bool
        r.MatchCount.ShouldBe(16);
        r.OverallSimilarity.ShouldBe(1.0f);
        r.FieldComparisons.Count.ShouldBe(16);
    }

    [Fact]
    public async Task CompareExpedientes_OneFieldMissing_ExactAverageAndMatchCount()
    {
        var xml = Sample();
        var ocr = Sample();
        ocr.Referencia2 = ""; // xml non-empty, ocr empty -> that ONE field is "Missing" (sim 0.0)

        var r = await _svc.CompareExpedientesAsync(xml, ocr, TestContext.Current.CancellationToken);

        r.MatchCount.ShouldBe(15);
        r.TotalFields.ShouldBe(16);
        // 15 fields at 1.0 + 1 at 0.0 -> 15/16 = 0.9375 exactly.
        r.OverallSimilarity.ShouldBe(0.9375f);
    }

    [Fact]
    public async Task CompareExpedientes_NumericFieldDiffers_NotCountedAsMatch()
    {
        var xml = Sample();
        var ocr = Sample();
        ocr.Folio = 1; // "6789" vs "1" -> not a match

        var r = await _svc.CompareExpedientesAsync(xml, ocr, TestContext.Current.CancellationToken);

        r.MatchCount.ShouldBe(15);
        r.FieldComparisons.Single(c => c.FieldName == "Folio").Status.ShouldNotBe("Match");
    }

    [Fact]
    public async Task CompareExpedientes_SameDayDifferentTime_DateFieldStillMatches()
    {
        // FechaPublicacion is compared via ToString("yyyy-MM-dd"): same calendar day with different
        // time-of-day must still be a Match. Kills mutating the "yyyy-MM-dd" format (default ToString
        // would include the differing time and break the match).
        var xml = Sample();
        var ocr = Sample();
        xml.FechaPublicacion = new DateTime(2025, 6, 5, 8, 0, 0);
        ocr.FechaPublicacion = new DateTime(2025, 6, 5, 17, 30, 0);

        var r = await _svc.CompareExpedientesAsync(xml, ocr, TestContext.Current.CancellationToken);

        r.FieldComparisons.Single(c => c.FieldName == "FechaPublicacion").Status.ShouldBe("Match");
        r.MatchCount.ShouldBe(16);
    }

    [Fact]
    public async Task CompareExpedientes_BooleanFieldFormatted_DiffersWhenValuesDiffer()
    {
        var xml = Sample();
        var ocr = Sample();
        ocr.TieneAseguramiento = false; // "True" vs "False"

        var r = await _svc.CompareExpedientesAsync(xml, ocr, TestContext.Current.CancellationToken);

        r.FieldComparisons.Single(c => c.FieldName == "TieneAseguramiento").Status.ShouldNotBe("Match");
        r.MatchCount.ShouldBe(15);
    }

    [Fact]
    public async Task CompareExpedientes_NullArguments_Throw()
    {
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _svc.CompareExpedientesAsync(null!, Sample(), TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _svc.CompareExpedientesAsync(Sample(), null!, TestContext.Current.CancellationToken));
    }
}
