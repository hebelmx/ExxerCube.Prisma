namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Mutation-killing tests for <see cref="SemanticAnalyzerService"/> (§2.7). Drives the analyzer through a
/// fully mocked <see cref="ITextComparer"/> so each directive detector, the best-match selection, the
/// confidence propagation, and the <c>&gt;= DefaultThreshold</c> boundary can be isolated deterministically.
/// (The existing <c>LegalDirectiveClassifierDictionaryTests</c> exercise the real Levenshtein comparer; these
/// pin the exact branches the integration tests cannot force.)
/// </summary>
public class SemanticAnalyzerServiceMutationTests
{
    private readonly ITextComparer _comparer;
    private readonly SemanticAnalyzerService _service;

    public SemanticAnalyzerServiceMutationTests()
    {
        _comparer = Substitute.For<ITextComparer>();
        _service = new SemanticAnalyzerService(_comparer, Substitute.For<ILogger<SemanticAnalyzerService>>());
    }

    private static TextMatchResult Match(double similarity) => new()
    {
        MatchedText = "matched",
        Similarity = similarity,
        StartIndex = 0,
        Length = 1,
    };

    private void MatchOnly(string phrase, double similarity) =>
        _comparer.FindBestMatch(phrase, Arg.Any<string>(), Arg.Any<double>()).Returns(Match(similarity));

    private const string Doc = "some legal document text";

    // ---------- Guards ----------

    [Fact]
    public async Task Cancelled_ReturnsExactFailure()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await _service.AnalyzeDirectivesAsync(Doc, null, cts.Token);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Operation was cancelled.");
        _comparer.DidNotReceive().FindBestMatch(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<double>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankText_ReturnsExactFailure(string? text)
    {
        var result = await _service.AnalyzeDirectivesAsync(text!, null, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Document text cannot be null or empty.");
    }

    // ---------- Match / no-match across all detectors ----------

    [Fact]
    public async Task AllDictionariesMatch_AllRequirementsSetWithConfidence()
    {
        _comparer.FindBestMatch(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<double>()).Returns(Match(0.92));

        var result = await _service.AnalyzeDirectivesAsync(Doc, null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var a = result.Value!;
        a.RequiereBloqueo.ShouldNotBeNull();
        a.RequiereBloqueo!.EsRequerido.ShouldBeTrue();
        a.RequiereBloqueo.Confidence.ShouldBe(0.92, 0.00001);
        a.RequiereDesbloqueo.ShouldNotBeNull();
        a.RequiereDocumentacion.ShouldNotBeNull();
        a.RequiereTransferencia.ShouldNotBeNull();
        a.RequiereInformacionGeneral.ShouldNotBeNull();
    }

    [Fact]
    public async Task NoMatch_AllRequirementsNull()
    {
        _comparer.FindBestMatch(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<double>()).Returns((TextMatchResult?)null);

        var result = await _service.AnalyzeDirectivesAsync(Doc, null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var a = result.Value!;
        a.RequiereBloqueo.ShouldBeNull();
        a.RequiereDesbloqueo.ShouldBeNull();
        a.RequiereDocumentacion.ShouldBeNull();
        a.RequiereTransferencia.ShouldBeNull();
        a.RequiereInformacionGeneral.ShouldBeNull();
    }

    // ---------- Per-detector isolation (each maps to its own requirement) ----------

    [Fact]
    public async Task BlockPhraseOnly_SetsBloqueoOnly()
    {
        MatchOnly("aseguramiento de fondos", 0.9);
        var a = (await _service.AnalyzeDirectivesAsync(Doc, null, TestContext.Current.CancellationToken)).Value!;
        a.RequiereBloqueo.ShouldNotBeNull();
        a.RequiereDesbloqueo.ShouldBeNull();
        a.RequiereDocumentacion.ShouldBeNull();
        a.RequiereTransferencia.ShouldBeNull();
        a.RequiereInformacionGeneral.ShouldBeNull();
    }

    [Fact]
    public async Task UnblockPhraseOnly_SetsDesbloqueoOnly()
    {
        MatchOnly("desbloqueo de cuenta", 0.9);
        var a = (await _service.AnalyzeDirectivesAsync(Doc, null, TestContext.Current.CancellationToken)).Value!;
        a.RequiereDesbloqueo.ShouldNotBeNull();
        a.RequiereBloqueo.ShouldBeNull();
        a.RequiereDocumentacion.ShouldBeNull();
        a.RequiereTransferencia.ShouldBeNull();
        a.RequiereInformacionGeneral.ShouldBeNull();
    }

    [Fact]
    public async Task DocumentPhraseOnly_SetsDocumentacionOnly()
    {
        MatchOnly("solicitud de documentación", 0.9);
        var a = (await _service.AnalyzeDirectivesAsync(Doc, null, TestContext.Current.CancellationToken)).Value!;
        a.RequiereDocumentacion.ShouldNotBeNull();
        a.RequiereBloqueo.ShouldBeNull();
        a.RequiereDesbloqueo.ShouldBeNull();
        a.RequiereTransferencia.ShouldBeNull();
        a.RequiereInformacionGeneral.ShouldBeNull();
    }

    [Fact]
    public async Task TransferPhraseOnly_SetsTransferenciaOnly()
    {
        MatchOnly("transferencia de fondos", 0.9);
        var a = (await _service.AnalyzeDirectivesAsync(Doc, null, TestContext.Current.CancellationToken)).Value!;
        a.RequiereTransferencia.ShouldNotBeNull();
        a.RequiereBloqueo.ShouldBeNull();
        a.RequiereDesbloqueo.ShouldBeNull();
        a.RequiereDocumentacion.ShouldBeNull();
        a.RequiereInformacionGeneral.ShouldBeNull();
    }

    [Fact]
    public async Task InformationPhraseOnly_SetsInformacionGeneralOnly()
    {
        MatchOnly("solicitud de información", 0.9);
        var a = (await _service.AnalyzeDirectivesAsync(Doc, null, TestContext.Current.CancellationToken)).Value!;
        a.RequiereInformacionGeneral.ShouldNotBeNull();
        a.RequiereBloqueo.ShouldBeNull();
        a.RequiereDesbloqueo.ShouldBeNull();
        a.RequiereDocumentacion.ShouldBeNull();
        a.RequiereTransferencia.ShouldBeNull();
    }

    // ---------- Threshold boundary (>= DefaultThreshold 0.85) ----------

    [Fact]
    public async Task SimilarityExactlyThreshold_IsDetected()
    {
        // 0.85 >= 0.85 -> matched. Kills `>=` -> `>` on the threshold compare.
        MatchOnly("aseguramiento de fondos", 0.85);
        var a = (await _service.AnalyzeDirectivesAsync(Doc, null, TestContext.Current.CancellationToken)).Value!;
        a.RequiereBloqueo.ShouldNotBeNull();
        a.RequiereBloqueo!.Confidence.ShouldBe(0.85, 0.00001);
    }

    [Fact]
    public async Task SimilarityJustBelowThreshold_IsNotDetected()
    {
        MatchOnly("aseguramiento de fondos", 0.84);
        var a = (await _service.AnalyzeDirectivesAsync(Doc, null, TestContext.Current.CancellationToken)).Value!;
        a.RequiereBloqueo.ShouldBeNull();
    }

    // ---------- Best-match selection + confidence propagation ----------

    [Fact]
    public async Task BestMatch_UsesHighestSimilarityAcrossPhrases()
    {
        // Two phrases in the Block dictionary match at different similarities; the highest must win.
        _comparer.FindBestMatch("aseguramiento de fondos", Arg.Any<string>(), Arg.Any<double>()).Returns(Match(0.86));
        _comparer.FindBestMatch("aseguramiento de la cuenta", Arg.Any<string>(), Arg.Any<double>()).Returns(Match(0.95));

        var a = (await _service.AnalyzeDirectivesAsync(Doc, null, TestContext.Current.CancellationToken)).Value!;

        a.RequiereBloqueo.ShouldNotBeNull();
        a.RequiereBloqueo!.Confidence.ShouldBe(0.95, 0.00001); // max, not first-seen 0.86
    }

    [Fact]
    public async Task Confidence_IsPropagatedExactly()
    {
        MatchOnly("aseguramiento de fondos", 0.9123);
        var a = (await _service.AnalyzeDirectivesAsync(Doc, null, TestContext.Current.CancellationToken)).Value!;
        a.RequiereBloqueo!.Confidence.ShouldBe(0.9123, 0.00001);
    }

    // ---------- Exception path ----------

    [Fact]
    public async Task ComparerThrows_ReturnsWrappedError()
    {
        _comparer.FindBestMatch(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<double>())
            .Returns<TextMatchResult?>(_ => throw new InvalidOperationException("boom"));

        var result = await _service.AnalyzeDirectivesAsync(Doc, null, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Error analyzing legal directives: boom");
    }
}
