namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Adaptive;

using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;
using Meziantou.Extensions.Logging.Xunit.v3;

/// <summary>
/// Mutation-killing tests for <see cref="AdaptiveDocxExtractor"/> (the orchestrator over the five
/// <see cref="IAdaptiveDocxStrategy"/> implementations).
/// </summary>
/// <remarks>
/// <para>
/// Written against the Stryker.NET survivor map (baseline 80.77%: Survived 0, NoCoverage 25, Timeout 77).
/// The existing <c>AdaptiveDocxExtractorLiskovTests</c> exercise the orchestrator through the <em>real</em>
/// strategies on a couple of happy-path documents, so the merge/complement internals, the strategy-selection
/// ladder, the confidence ordering, and the two <c>catch</c> blocks were never pinned to exact values.
/// </para>
/// <para>
/// These tests drive the orchestrator with <strong>NSubstitute mocks of <see cref="IAdaptiveDocxStrategy"/></strong>
/// so the per-strategy confidence and extraction results are controlled precisely. That makes the orchestrator's
/// own logic observable in isolation: the <see cref="ExtractionMode"/> dispatch, the
/// <c>OrderByDescending</c> confidence sort, the <c>Confidence &gt; 0</c> / <c>== 0</c> guards, the first-non-null
/// <c>MergeExtractedFields</c> rules, the <c>ComplementFields</c> preserve-then-fill rules, and the unique-by
/// <c>(Currency,Value)</c> Montos dedup.
/// </para>
/// <para>
/// <strong>Cancellation coverage note:</strong> the Liskov cancellation tests pass an already-cancelled token,
/// which trips <c>ThrowIfCancellationRequested()</c> <em>before</em> the <c>try</c> — so neither
/// <c>catch (OperationCanceledException)</c> block is ever entered. Here a mock strategy throws the
/// <see cref="OperationCanceledException"/> from <em>inside</em> the try (with a live token), which is the only
/// way to reach and pin the rethrows.
/// </para>
/// <para>
/// <strong>Residual / equivalent floor:</strong> the remaining survivors after this pass are pure Serilog
/// <c>LogDebug</c>/<c>LogError</c> statement/string mutants and the discarded
/// <see cref="ArgumentOutOfRangeException"/> message (the invalid-mode throw is swallowed by the generic
/// <c>catch</c> and converted to <c>null</c>, so the message is never observable). None are killable through
/// the public API.
/// </para>
/// </remarks>
public sealed class AdaptiveDocxExtractorMutationKillingTests
{
    private readonly ILogger<AdaptiveDocxExtractor> _logger;

    public AdaptiveDocxExtractorMutationKillingTests(ITestOutputHelper output)
    {
        _logger = XUnitLogger.CreateLogger<AdaptiveDocxExtractor>(output);
    }

    private const string AnyDoc = "any non-empty document text";

    /// <summary>Builds a mock strategy with a fixed name, confidence, and extraction result.</summary>
    private static IAdaptiveDocxStrategy MockStrategy(string name, int confidence, ExtractedFields? extract)
    {
        var s = Substitute.For<IAdaptiveDocxStrategy>();
        s.StrategyName.Returns(name);
        s.GetConfidenceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(confidence);
        s.ExtractAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(extract);
        return s;
    }

    private static ExtractedFields Fields(
        string? exp = null,
        string? causa = null,
        string? accion = null,
        Dictionary<string, string?>? additional = null,
        List<AmountData>? montos = null,
        List<string>? fechas = null)
        => new()
        {
            Expediente = exp,
            Causa = causa,
            AccionSolicitada = accion,
            AdditionalFields = additional ?? new(),
            Montos = montos ?? new(),
            Fechas = fechas ?? new(),
        };

    private AdaptiveDocxExtractor Build(params IAdaptiveDocxStrategy[] strategies)
        => new(strategies, _logger);

    // =====================================================================
    // Constructor guards (L31-37).
    // =====================================================================

    [Fact]
    public void Ctor_NullStrategies_ThrowsArgumentNullException()
        => Should.Throw<ArgumentNullException>(() => new AdaptiveDocxExtractor(null!, _logger));

    [Fact]
    public void Ctor_NullLogger_ThrowsArgumentNullException()
        => Should.Throw<ArgumentNullException>(
            () => new AdaptiveDocxExtractor(new[] { MockStrategy("a", 50, null) }, null!));

    [Fact]
    public void Ctor_EmptyStrategies_ThrowsArgumentException_WithMessage()
    {
        // Pins the "At least one strategy" guard (L34-36): kills the statement-removal and the message string.
        var ex = Should.Throw<ArgumentException>(
            () => new AdaptiveDocxExtractor(Array.Empty<IAdaptiveDocxStrategy>(), _logger));
        ex.Message.ShouldContain("At least one strategy must be provided");
    }

    // =====================================================================
    // ExtractAsync — empty / whitespace input (L47-51).
    // =====================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task ExtractAsync_BlankInput_ReturnsNull(string blank)
    {
        var extractor = Build(MockStrategy("a", 90, Fields(exp: "X")));

        var result = await extractor.ExtractAsync(blank, ExtractionMode.BestStrategy, null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // =====================================================================
    // ExtractAsync — mode dispatch (L60-66). Same doc, different mode => different behavior.
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_InvalidMode_ReturnsNull()
    {
        // Default switch arm throws ArgumentOutOfRangeException, which the generic catch converts to null.
        var extractor = Build(MockStrategy("a", 90, Fields(exp: "X")));

        var result = await extractor.ExtractAsync(AnyDoc, (ExtractionMode)999, null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExtractAsync_ModeDispatch_BestStrategyVsComplement_DiffersOnExisting()
    {
        // BestStrategy ignores existingFields; Complement preserves them. Same strategy + doc, different mode.
        var strategy = MockStrategy("a", 90, Fields(exp: "FROM-STRATEGY"));
        var extractor = Build(strategy);
        var existing = Fields(exp: "FROM-EXISTING");

        var best = await extractor.ExtractAsync(AnyDoc, ExtractionMode.BestStrategy, existing, TestContext.Current.CancellationToken);
        var complement = await extractor.ExtractAsync(AnyDoc, ExtractionMode.Complement, existing, TestContext.Current.CancellationToken);

        best.ShouldNotBeNull();
        best.Expediente.ShouldBe("FROM-STRATEGY");      // BestStrategy uses strategy output, ignores existing
        complement.ShouldNotBeNull();
        complement.Expediente.ShouldBe("FROM-EXISTING"); // Complement preserves existing
    }

    // =====================================================================
    // ExtractAsync / GetStrategyConfidencesAsync — cancellation rethrow from INSIDE the try (L68-71, L115-118).
    // A live token + a strategy that throws OCE during the operation is the only way to enter the catch(OCE).
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_StrategyThrowsOperationCanceled_Rethrows()
    {
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();
        strategy.StrategyName.Returns("a");
        strategy.GetConfidenceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new OperationCanceledException());
        var extractor = Build(strategy);

        // Token is NOT cancelled — the OCE comes from the strategy, inside the try.
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await extractor.ExtractAsync(AnyDoc, ExtractionMode.BestStrategy, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetStrategyConfidencesAsync_StrategyThrowsOperationCanceled_Rethrows()
    {
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();
        strategy.StrategyName.Returns("a");
        strategy.GetConfidenceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new OperationCanceledException());
        var extractor = Build(strategy);

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await extractor.GetStrategyConfidencesAsync(AnyDoc, TestContext.Current.CancellationToken));
    }

    // =====================================================================
    // ExtractAsync / GetStrategyConfidencesAsync — generic exception swallowed (L73-77 => null, L120-124 => all-zero).
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_BestStrategyExtractThrows_ReturnsNull()
    {
        // Confidence is high (strategy selected), but its ExtractAsync throws a non-OCE exception:
        // it propagates to the orchestrator's catch(Exception) which returns null.
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();
        strategy.StrategyName.Returns("a");
        strategy.GetConfidenceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(90);
        strategy.ExtractAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ExtractedFields?>(_ => throw new InvalidOperationException("boom"));
        var extractor = Build(strategy);

        var result = await extractor.ExtractAsync(AnyDoc, ExtractionMode.BestStrategy, null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetStrategyConfidencesAsync_StrategyThrows_ReturnsAllZeroForEveryStrategy()
    {
        var throwing = Substitute.For<IAdaptiveDocxStrategy>();
        throwing.StrategyName.Returns("boom");
        throwing.GetConfidenceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new InvalidOperationException());
        var healthy = MockStrategy("healthy", 90, Fields(exp: "X"));
        var extractor = Build(throwing, healthy);

        var confidences = await extractor.GetStrategyConfidencesAsync(AnyDoc, TestContext.Current.CancellationToken);

        confidences.Count.ShouldBe(2);                       // one per strategy
        confidences.ShouldAllBe(c => c.Confidence == 0);      // fallback is all-zero
        confidences.Select(c => c.StrategyName).ShouldBe(new[] { "boom", "healthy" }, ignoreOrder: true);
    }

    // =====================================================================
    // GetStrategyConfidencesAsync — empty input => all-zero, names preserved (L85-88).
    // =====================================================================

    [Fact]
    public async Task GetStrategyConfidencesAsync_EmptyInput_ReturnsZeroForEachStrategyByName()
    {
        var extractor = Build(MockStrategy("alpha", 90, null), MockStrategy("beta", 80, null));

        var confidences = await extractor.GetStrategyConfidencesAsync("", TestContext.Current.CancellationToken);

        confidences.Count.ShouldBe(2);
        confidences.ShouldAllBe(c => c.Confidence == 0);
        confidences.Select(c => c.StrategyName).ShouldBe(new[] { "alpha", "beta" }, ignoreOrder: true);
    }

    // =====================================================================
    // GetStrategyConfidencesAsync — ordering + exact values + names (L97-113).
    // =====================================================================

    [Fact]
    public async Task GetStrategyConfidencesAsync_OrdersByConfidenceDescending_WithExactValues()
    {
        var extractor = Build(
            MockStrategy("low", 30, null),
            MockStrategy("high", 90, null),
            MockStrategy("mid", 60, null));

        var confidences = await extractor.GetStrategyConfidencesAsync(AnyDoc, TestContext.Current.CancellationToken);

        confidences.Select(c => c.StrategyName).ShouldBe(new[] { "high", "mid", "low" });
        confidences.Select(c => c.Confidence).ShouldBe(new[] { 90, 60, 30 });
    }

    // =====================================================================
    // BestStrategy — selects the HIGHEST-confidence strategy's output (L131-153).
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_BestStrategy_UsesHighestConfidenceStrategyOutput()
    {
        var winner = MockStrategy("winner", 90, Fields(exp: "FROM-WINNER"));
        var loser = MockStrategy("loser", 50, Fields(exp: "FROM-LOSER"));
        var extractor = Build(loser, winner); // order intentionally not confidence-sorted

        var result = await extractor.ExtractAsync(AnyDoc, ExtractionMode.BestStrategy, null, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("FROM-WINNER");
        await winner.Received(1).ExtractAsync(AnyDoc, Arg.Any<CancellationToken>());
        await loser.DidNotReceive().ExtractAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExtractAsync_BestStrategy_AllZeroConfidence_ReturnsNull()
    {
        var extractor = Build(MockStrategy("a", 0, Fields(exp: "X")), MockStrategy("b", 0, Fields(exp: "Y")));

        var result = await extractor.ExtractAsync(AnyDoc, ExtractionMode.BestStrategy, null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // =====================================================================
    // MergeAll — capable filter (Confidence > 0), all-null guard, and merge rules (L155-285).
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_MergeAll_ExcludesZeroConfidenceStrategies()
    {
        // Kills `Confidence > 0` -> `>= 0`: the zero-confidence strategy carries Causa, which must NOT appear.
        var capable = MockStrategy("capable", 90, Fields(exp: "E"));
        var zero = MockStrategy("zero", 0, Fields(causa: "SHOULD-NOT-APPEAR"));
        var extractor = Build(capable, zero);

        var result = await extractor.ExtractAsync(AnyDoc, ExtractionMode.MergeAll, null, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("E");
        result.Causa.ShouldBeNull();
        await zero.DidNotReceive().ExtractAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExtractAsync_MergeAll_AllCapableReturnNull_ReturnsNull()
    {
        var extractor = Build(MockStrategy("a", 90, null), MockStrategy("b", 80, null));

        var result = await extractor.ExtractAsync(AnyDoc, ExtractionMode.MergeAll, null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExtractAsync_MergeAll_NoCapableStrategies_ReturnsNull()
    {
        var extractor = Build(MockStrategy("a", 0, Fields(exp: "X")));

        var result = await extractor.ExtractAsync(AnyDoc, ExtractionMode.MergeAll, null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExtractAsync_MergeAll_CoreFields_FirstNonNullWins_NotOverwritten()
    {
        // BOTH strategies supply ALL three core fields (different values). Confidence order (desc): A then B.
        // The first non-null value (A's) must win for every field. This kills the `&&` -> `||` mutation in each
        // merge guard: with `||`, an already-set field would be overwritten by the later strategy's value.
        var a = MockStrategy("a", 90, Fields(exp: "EXP-A", causa: "CAUSA-A", accion: "ACCION-A"));
        var b = MockStrategy("b", 80, Fields(exp: "EXP-B", causa: "CAUSA-B", accion: "ACCION-B"));
        var extractor = Build(a, b);

        var result = await extractor.ExtractAsync(AnyDoc, ExtractionMode.MergeAll, null, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("EXP-A");
        result.Causa.ShouldBe("CAUSA-A");
        result.AccionSolicitada.ShouldBe("ACCION-A");
    }

    [Fact]
    public async Task ExtractAsync_MergeAll_CoreFields_FillsGapsFromLaterStrategy()
    {
        // A supplies only Expediente; B fills Causa + Accion. Kills the `!string.IsNullOrEmpty(fieldSet.X)`
        // removal (the gap must actually be filled from B).
        var a = MockStrategy("a", 90, Fields(exp: "EXP-A"));
        var b = MockStrategy("b", 80, Fields(causa: "CAUSA-B", accion: "ACCION-B"));
        var extractor = Build(a, b);

        var result = await extractor.ExtractAsync(AnyDoc, ExtractionMode.MergeAll, null, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("EXP-A");
        result.Causa.ShouldBe("CAUSA-B");
        result.AccionSolicitada.ShouldBe("ACCION-B");
    }

    [Fact]
    public async Task ExtractAsync_MergeAll_AdditionalFields_FirstKeyWins()
    {
        var a = MockStrategy("a", 90, Fields(exp: "E", additional: new() { ["K"] = "VAL-A" }));
        var b = MockStrategy("b", 80, Fields(additional: new() { ["K"] = "VAL-B", ["K2"] = "VAL-2" }));
        var extractor = Build(a, b);

        var result = await extractor.ExtractAsync(AnyDoc, ExtractionMode.MergeAll, null, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.AdditionalFields["K"].ShouldBe("VAL-A");  // first key wins
        result.AdditionalFields["K2"].ShouldBe("VAL-2");
    }

    [Fact]
    public async Task ExtractAsync_MergeAll_Montos_UniqueByCurrencyAndValue()
    {
        var a = MockStrategy("a", 90, Fields(exp: "E", montos: new() { new AmountData("MXN", 100m, "a") }));
        var b = MockStrategy("b", 80, Fields(montos: new()
        {
            new AmountData("MXN", 100m, "dup"),   // exact duplicate -> skipped
            new AmountData("USD", 100m, "diffcur"), // same value, different currency -> added
            new AmountData("MXN", 200m, "diffval"), // same currency, different value -> added
        }));
        var extractor = Build(a, b);

        var result = await extractor.ExtractAsync(AnyDoc, ExtractionMode.MergeAll, null, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Montos.Count.ShouldBe(3);
        result.Montos.ShouldContain(m => m.Currency == "MXN" && m.Value == 100m);
        result.Montos.ShouldContain(m => m.Currency == "USD" && m.Value == 100m);
        result.Montos.ShouldContain(m => m.Currency == "MXN" && m.Value == 200m);
    }

    [Fact]
    public async Task ExtractAsync_MergeAll_Fechas_Deduplicated()
    {
        var a = MockStrategy("a", 90, Fields(exp: "E", fechas: new() { "2025-01-01" }));
        var b = MockStrategy("b", 80, Fields(fechas: new() { "2025-01-01", "2025-02-02" }));
        var extractor = Build(a, b);

        var result = await extractor.ExtractAsync(AnyDoc, ExtractionMode.MergeAll, null, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Fechas.ShouldBe(new[] { "2025-01-01", "2025-02-02" }, ignoreOrder: true);
    }

    // =====================================================================
    // Complement — existing==null delegates to BestStrategy; new==null returns existing (L199-228).
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_Complement_NoExisting_UsesBestStrategy()
    {
        var extractor = Build(MockStrategy("a", 90, Fields(exp: "FROM-STRATEGY")));

        var result = await extractor.ExtractAsync(AnyDoc, ExtractionMode.Complement, null, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("FROM-STRATEGY");
    }

    [Fact]
    public async Task ExtractAsync_Complement_NewExtractionNull_ReturnsExistingUnchanged()
    {
        var extractor = Build(MockStrategy("a", 90, null)); // best strategy returns null
        var existing = Fields(exp: "EXP-E", causa: "CAUSA-E");

        var result = await extractor.ExtractAsync(AnyDoc, ExtractionMode.Complement, existing, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("EXP-E");
        result.Causa.ShouldBe("CAUSA-E");
    }

    // =====================================================================
    // ComplementFields — preserve-then-fill core fields + collection copy/dedup (L287-343).
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_Complement_CoreFields_PreserveExpediente_FillCausaAccion()
    {
        // existing has Expediente only; new extraction supplies Causa + Accion (and a different Expediente).
        // Kills the `existing.Expediente ?? new` LEFT-removal (existing wins) and the Causa/Accion
        // RIGHT-removal (the null existing values get filled from new).
        var newExtraction = Fields(exp: "NEW-EXP", causa: "NEW-CAUSA", accion: "NEW-ACCION");
        var extractor = Build(MockStrategy("a", 90, newExtraction));
        var existing = Fields(exp: "KEEP-EXP");

        var result = await extractor.ExtractAsync(AnyDoc, ExtractionMode.Complement, existing, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("KEEP-EXP");      // preserved (existing wins)
        result.Causa.ShouldBe("NEW-CAUSA");           // filled (existing was null)
        result.AccionSolicitada.ShouldBe("NEW-ACCION"); // filled
    }

    [Fact]
    public async Task ExtractAsync_Complement_CoreFields_PreserveCausaAccion_FillExpediente()
    {
        // Mirror of the above: existing has Causa + Accion (Expediente null); new supplies all three.
        // Kills the Causa/Accion `?? new` LEFT-removal (existing wins) and the Expediente RIGHT-removal
        // (the null existing Expediente gets filled from new).
        var newExtraction = Fields(exp: "NEW-EXP", causa: "NEW-CAUSA", accion: "NEW-ACCION");
        var extractor = Build(MockStrategy("a", 90, newExtraction));
        var existing = Fields(causa: "KEEP-CAUSA", accion: "KEEP-ACCION");

        var result = await extractor.ExtractAsync(AnyDoc, ExtractionMode.Complement, existing, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("NEW-EXP");           // filled (existing was null)
        result.Causa.ShouldBe("KEEP-CAUSA");              // preserved (existing wins)
        result.AccionSolicitada.ShouldBe("KEEP-ACCION");  // preserved
    }

    [Fact]
    public async Task ExtractAsync_Complement_AdditionalFields_ExistingKeyWins_NewKeysAdded()
    {
        var newExtraction = Fields(exp: "N", additional: new() { ["K"] = "NEW-VAL", ["NK"] = "NEW-ONLY" });
        var extractor = Build(MockStrategy("a", 90, newExtraction));
        var existing = Fields(exp: "E", additional: new() { ["K"] = "EXISTING-VAL", ["EK"] = "EXISTING-ONLY" });

        var result = await extractor.ExtractAsync(AnyDoc, ExtractionMode.Complement, existing, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.AdditionalFields["K"].ShouldBe("EXISTING-VAL"); // existing wins over new
        result.AdditionalFields["EK"].ShouldBe("EXISTING-ONLY"); // existing copied
        result.AdditionalFields["NK"].ShouldBe("NEW-ONLY");    // new-only added
    }

    [Fact]
    public async Task ExtractAsync_Complement_Montos_CopyExistingAndAddUnique()
    {
        var newExtraction = Fields(exp: "N", montos: new()
        {
            new AmountData("MXN", 50m, "dup"),     // duplicate of existing -> skipped
            new AmountData("EUR", 50m, "diffcur"),  // different currency -> added
            new AmountData("MXN", 99m, "diffval"),   // different value -> added
        });
        var extractor = Build(MockStrategy("a", 90, newExtraction));
        // GBP 77 is existing-only: it pins that existing montos are COPIED (not just refilled by the new dup).
        var existing = Fields(exp: "E", montos: new()
        {
            new AmountData("MXN", 50m, "existing"),
            new AmountData("GBP", 77m, "existing-only"),
        });

        var result = await extractor.ExtractAsync(AnyDoc, ExtractionMode.Complement, existing, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Montos.Count.ShouldBe(4);
        result.Montos.ShouldContain(m => m.Currency == "MXN" && m.Value == 50m);
        result.Montos.ShouldContain(m => m.Currency == "GBP" && m.Value == 77m); // existing-only, copied
        result.Montos.ShouldContain(m => m.Currency == "EUR" && m.Value == 50m);
        result.Montos.ShouldContain(m => m.Currency == "MXN" && m.Value == 99m);
    }

    [Fact]
    public async Task ExtractAsync_Complement_Fechas_CopyExistingAndAddUnique()
    {
        var newExtraction = Fields(exp: "N", fechas: new() { "2025-03-03", "2025-04-04" });
        var extractor = Build(MockStrategy("a", 90, newExtraction));
        // 2025-05-05 is existing-only: it pins that existing fechas are COPIED (not just refilled by the new dup).
        var existing = Fields(exp: "E", fechas: new() { "2025-03-03", "2025-05-05" });

        var result = await extractor.ExtractAsync(AnyDoc, ExtractionMode.Complement, existing, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Fechas.Count.ShouldBe(3);
        result.Fechas.ShouldBe(new[] { "2025-03-03", "2025-05-05", "2025-04-04" }, ignoreOrder: true);
    }
}
