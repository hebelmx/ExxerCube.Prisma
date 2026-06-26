namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Mutation-killing tests for <see cref="FileClassifierService"/>.
/// </summary>
/// <remarks>
/// The existing <c>FileClassifierServiceTests</c> (8 tests) assert only <c>Level1</c>/<c>Level2</c> with loose
/// score checks (<c>ShouldBeGreaterThan(70)</c>) and never exercise the Informacion / Transferencia /
/// OperacionesIlicitas categories, the middle (70) or default (10) score rungs, the confidence ladder, the
/// <c>/AS</c> shortcut, the <c>string.Join</c> separator, or the Level-2 priority order. This left Stryker at
/// 38.79% (56 survivors / 15 NoCoverage).
///
/// Every test pins an <b>exact</b> score / confidence / category so a keyword/equality/arithmetic/Linq/block
/// mutation is observable. The classifier is fully deterministic (pure string rules — no OCR/DB/network).
///
/// Confidence is a strong sentinel: for a single category at 90 (rest 10) the score difference is 80 ≥ 60 so
/// confidence = Min(100, 90) = 90; any stray keyword mutation that lights up a second category collapses the
/// difference and drops confidence well below 90, so asserting <c>Confidence == 90</c> catches phantom matches.
/// </remarks>
public class FileClassifierServiceMutationKillingTests
{
    private readonly FileClassifierService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileClassifierServiceMutationKillingTests"/> class.
    /// </summary>
    public FileClassifierServiceMutationKillingTests()
    {
        var logger = Substitute.For<ILogger<FileClassifierService>>();
        _service = new FileClassifierService(logger);
    }

    private async Task<ClassificationResult> ClassifyAsync(
        string area = "GENERICO", string numero = "X-001", string[]? refs = null)
    {
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente { AreaDescripcion = area, NumeroExpediente = numero },
            LegalReferences = refs ?? Array.Empty<string>()
        };
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        return result.Value!;
    }

    // ── Level-1 high rung (score 90): each keyword independently triggers its category ──────────────
    // The keyword lives in AreaDescripcion, so each row also pins the L41 `?? string.Empty` source
    // (a remove-left mutation would blank AreaDescripcion and drop the keyword).

    /// <summary>Each Level-1 high-confidence keyword maps to its category at score 90 / confidence 90.</summary>
    [Theory]
    [InlineData("ASEGURAMIENTO", 0)]
    [InlineData("EMBARGO", 0)]
    [InlineData("LIBERAR", 1)]
    [InlineData("DOCUMENTACION", 2)]
    [InlineData("DOCUMENTO", 2)]
    [InlineData("SOLICITUD DOCUMENTAL", 2)]
    [InlineData("INFORMACION", 3)]
    [InlineData("INFORMAR", 3)]
    [InlineData("REPORTE", 3)]
    [InlineData("TRANSFERENCIA", 4)]
    [InlineData("TRANSFERIR", 4)]
    [InlineData("LAVADO", 5)]
    [InlineData("OPERACIONES ILICITAS", 5)]
    [InlineData("FINANCIAMIENTO TERRORISMO", 5)]
    public async Task Classify_HighRungKeyword_SetsCategoryTo90(string keyword, int expectedLevel1)
    {
        var result = await ClassifyAsync(area: keyword);
        result.Level1.Value.ShouldBe(expectedLevel1);
        result.Confidence.ShouldBe(90);
    }

    /// <summary>The score for each high-rung category is exactly 90 (not just &gt; 70).</summary>
    [Fact]
    public async Task Classify_Aseguramiento_ScoreIsExactly90()
    {
        var result = await ClassifyAsync(area: "ASEGURAMIENTO");
        result.Scores.AseguramientoScore.ShouldBe(90);
        result.Scores.DesembargoScore.ShouldBe(10);
        result.Scores.DocumentacionScore.ShouldBe(10);
        result.Scores.InformacionScore.ShouldBe(10);
        result.Scores.TransferenciaScore.ShouldBe(10);
        result.Scores.OperacionesIlicitasScore.ShouldBe(10);
    }

    // ── Level-1 middle rung (score 70): the secondary keyword + its else-if/else blocks ─────────────

    /// <summary>Each Level-1 secondary keyword maps to its category at score 70 / confidence 70.</summary>
    [Theory]
    [InlineData("ASEGURAR", 0)]
    [InlineData("DESEMBARGAR", 1)]
    [InlineData("DOCUMENTAR", 2)]
    [InlineData("INFORMATIVO", 3)]
    [InlineData("TRANSFER", 4)]
    [InlineData("ILICITO", 5)]
    public async Task Classify_MiddleRungKeyword_SetsCategoryTo70(string keyword, int expectedLevel1)
    {
        var result = await ClassifyAsync(area: keyword);
        result.Level1.Value.ShouldBe(expectedLevel1);
        result.Confidence.ShouldBe(70);
    }

    // ── Level-1 default rung (score 10): no keyword present ─────────────────────────────────────────

    /// <summary>
    /// With no category keyword, every score defaults to 10 (not 0). Because all six categories sit at the
    /// no-match floor simultaneously the no-signal guards fire: confidence is 0 (not 10) and Level1 is
    /// Unknown (not Aseguramiento by insertion-order). Pins all six <c>else</c> blocks: a removed else leaves
    /// the score at its 0 default.
    /// </summary>
    [Fact]
    public async Task Classify_NoKeyword_AllScoresDefaultTo10()
    {
        var result = await ClassifyAsync(area: "GENERICO", numero: "NEUTRO-001");
        result.Scores.AseguramientoScore.ShouldBe(10);
        result.Scores.DesembargoScore.ShouldBe(10);
        result.Scores.DocumentacionScore.ShouldBe(10);
        result.Scores.InformacionScore.ShouldBe(10);
        result.Scores.TransferenciaScore.ShouldBe(10);
        result.Scores.OperacionesIlicitasScore.ShouldBe(10);
        result.Confidence.ShouldBe(0);
        result.Level1.ShouldBe(ClassificationLevel1.Unknown);
        result.Level2.ShouldBeNull();
    }

    // ── /AS shortcut + numeroExpediente source + string.Join separator ──────────────────────────────

    /// <summary>
    /// The <c>/AS</c> pattern in the expediente number triggers Aseguramiento (L83) and the Especial Level-2
    /// (L181), independent of the area text. Also pins the L42 numeroExpediente source.
    /// </summary>
    [Fact]
    public async Task Classify_AsPatternInExpediente_TriggersAseguramientoAndEspecial()
    {
        var result = await ClassifyAsync(area: "GENERICO", numero: "EXP/AS1-2024");
        result.Scores.AseguramientoScore.ShouldBe(90);
        result.Level1.Value.ShouldBe(0);
        result.Level2.ShouldBe(ClassificationLevel2.Especial);
        result.Confidence.ShouldBe(90);
    }

    /// <summary>
    /// Legal references are joined with a single space, so two adjacent tokens form the multi-word keyword
    /// "OPERACIONES ILICITAS". Pins the L44 separator (a <c>""</c> join would concatenate to
    /// "OPERACIONESILICITAS" and miss the keyword, leaving the category at 10).
    /// </summary>
    [Fact]
    public async Task Classify_MultiWordKeywordAcrossReferences_RequiresSpaceJoin()
    {
        var result = await ClassifyAsync(area: "GENERICO", refs: new[] { "OPERACIONES", "ILICITAS" });
        result.Scores.OperacionesIlicitasScore.ShouldBe(90);
        result.Level1.Value.ShouldBe(5);
    }

    // ── Level-2 classification: each keyword + priority order + null ────────────────────────────────

    /// <summary>Each Level-2 keyword maps to its subcategory.</summary>
    [Theory]
    [InlineData("ESPECIAL", 0)]
    [InlineData("JUDICIAL", 1)]
    [InlineData("JUEZ", 1)]
    [InlineData("TRIBUNAL", 1)]
    [InlineData("HACENDARIO", 2)]
    [InlineData("SAT", 2)]
    [InlineData("SHCP", 2)]
    public async Task Classify_Level2Keyword_SetsSubcategory(string keyword, int expectedLevel2)
    {
        var result = await ClassifyAsync(area: keyword);
        result.Level2.ShouldNotBeNull();
        result.Level2!.Value.ShouldBe(expectedLevel2);
    }

    /// <summary>Especial is checked before Judicial (priority order).</summary>
    [Fact]
    public async Task Classify_EspecialAndJudicial_PrefersEspecial()
    {
        var result = await ClassifyAsync(area: "ASUNTO ESPECIAL JUDICIAL");
        result.Level2.ShouldBe(ClassificationLevel2.Especial);
    }

    /// <summary>Judicial is checked before Hacendario (priority order).</summary>
    [Fact]
    public async Task Classify_JudicialAndHacendario_PrefersJudicial()
    {
        var result = await ClassifyAsync(area: "ASUNTO JUDICIAL HACENDARIO");
        result.Level2.ShouldBe(ClassificationLevel2.Judicial);
    }

    // ── CalculateConfidence ladder ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// A single 70-category (rest 10) gives a score difference of exactly 60 → Min(100, 70) = 70. Confirms
    /// the high-clarity branch caps at the max score.
    /// </summary>
    [Fact]
    public async Task Confidence_SingleSeventyCategory_Is70()
    {
        var result = await ClassifyAsync(area: "ASEGURAR");
        result.Scores.AseguramientoScore.ShouldBe(70);
        result.Confidence.ShouldBe(70);
    }

    /// <summary>
    /// When the top two categories are 90 and 70 (difference 20 &lt; 40), confidence falls to the
    /// Min(70, average) branch: average of {90,70,10,10,10,10} = 33. Pins the L218 <c>Average()</c>
    /// (a <c>Min()</c> mutation gives 10), the L234 <c>Math.Min(70, avg)</c> (a <c>Max</c> mutation gives 70),
    /// and the L222 difference arithmetic (a <c>+</c> mutation would land in the ≥60 branch → 90).
    /// </summary>
    [Fact]
    public async Task Confidence_TopTwoCloseScores_UsesAverageBranch()
    {
        var result = await ClassifyAsync(area: "ASEGURAMIENTO", refs: new[] { "DOCUMENTAR" });
        result.Scores.AseguramientoScore.ShouldBe(90);
        result.Scores.DocumentacionScore.ShouldBe(70);
        result.Level1.Value.ShouldBe(0);
        result.Confidence.ShouldBe(33);
    }
}
