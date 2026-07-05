using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;
using IndQuestResults;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Behavior-neutral regression harness for the progressive fallback-extraction chain (E1).
/// </summary>
/// <remarks>
/// <para>
/// E1.A added extraction vocabulary (<see cref="ExtractionStatus.ExtractedByInference"/>,
/// <see cref="ExtractionProvenance"/>, <see cref="FieldKind"/>/<see cref="StageId"/>); E1.B added
/// <see cref="EscalatingStatementFieldExtractor"/>, a decorator over the positional
/// <see cref="PdfPigStatementFieldExtractor"/>. As of E1, every <see cref="FieldKind"/> resolves to
/// <see cref="FieldEscalationLadder.PositionalOnly"/> (see <see cref="FieldEscalationLadderRegistry"/>),
/// so the decorator is REQUIRED to return byte-identical field statuses and values to the raw
/// positional extractor.
/// </para>
/// <para>
/// This suite runs BOTH extractors — a raw <see cref="PdfPigStatementFieldExtractor"/> and a
/// second, freshly-constructed instance wrapped by <see cref="EscalatingStatementFieldExtractor"/> —
/// over all five demo fixtures, for both <c>ExtractHeaderAsync</c> and <c>ExtractFullAsync</c>, and
/// asserts every <see cref="ExtractedField{T}"/>'s <see cref="ExtractionStatus"/> and
/// <see cref="ExtractedField{T}.Value"/> agree, and that every escalated field still carries
/// <see cref="StageId.Positional"/> provenance. A future epic that registers a non-empty ladder for
/// any <see cref="FieldKind"/> is expected to break this suite deliberately — until then, any
/// divergence here is a real E1.B bug.
/// </para>
/// <para>
/// <b>E2.2 update:</b> <see cref="CreateEscalating"/> now wires the real production
/// <see cref="DefaultFieldStageProvider"/> (not <see cref="EmptyFieldStageProvider"/>) so this
/// harness actually exercises the fuzzy label-anchor stage registered for
/// <see cref="FieldKind.PaymentDueDate"/> — the only field with a non-empty ladder as of this
/// chunk. Every field OTHER than PaymentDueDate is still asserted byte-identical via
/// <see cref="Compare{T}"/>. PaymentDueDate is compared by <see cref="ComparePaymentDueDate"/>
/// instead: when positional already found a value, StatusGate cannot fire and identity is still
/// required; when positional found nothing, the escalated result may honestly stay NotExtracted
/// (fuzzy stage also abstained) OR recover a plausible value via <see cref="StageId.FuzzyLabel"/> —
/// both are acceptable, and only those two outcomes are acceptable (a value that doesn't clear
/// <see cref="PaymentDueDatePlausibilityValidator.IsPlausible"/>, or provenance from any other
/// stage, is still flagged as a mismatch).
/// </para>
/// </remarks>
public sealed class EscalationSeamBehaviorNeutralTests
{
    // -----------------------------------------------------------------------
    // Fixtures — the five demo-corpus PDFs (PRP2/demo)
    // -----------------------------------------------------------------------

    private static readonly string[] FixtureNames =
    [
        "good.pdf",
        "compliant-master.pdf",
        "bad-math-cl21.pdf",
        "bad-font-cl35.pdf",
        "scanned.pdf",
    ];

    public static IEnumerable<object[]> Fixtures() =>
        FixtureNames.Select(name => new object[] { name });

    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "demo", fileName);

    // -----------------------------------------------------------------------
    // Factory helpers — mirrors VeriqanExtractionExtensions.AddVeriqanExtraction wiring
    // -----------------------------------------------------------------------

    private static PdfPigStatementFieldExtractor CreateInner() =>
        new(XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>(),
            Microsoft.Extensions.Options.Options.Create(new PdfExtractionOptions()),
            new NullPasswordProvider());

    private static EscalatingStatementFieldExtractor CreateEscalating()
    {
        // A SECOND, independent inner instance — the decorator must be transparent regardless
        // of which concrete inner instance produced the positional result.
        var inner = CreateInner();
        var registry = new FieldEscalationLadderRegistry();
        // E2.2: DefaultFieldStageProvider (production wiring), not EmptyFieldStageProvider — see
        // the class remarks above for why the empty provider would make this harness stale.
        var orchestrator = new FieldResolutionOrchestrator(
            registry, new DefaultFieldStageProvider(), XUnitLogger.CreateLogger<FieldResolutionOrchestrator>());
        return new EscalatingStatementFieldExtractor(
            inner, orchestrator, XUnitLogger.CreateLogger<EscalatingStatementFieldExtractor>());
    }

    // -----------------------------------------------------------------------
    // Theories — ExtractFullAsync and ExtractHeaderAsync over all 5 demo fixtures
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task ExtractFullAsync_EscalatingSeam_MatchesPositionalBaseline(string fixtureName)
    {
        var ct = TestContext.Current.CancellationToken;
        var pdf = await LoadFixtureOrSkipAsync(fixtureName, ct);

        var baselineResult = await CreateInner().ExtractFullAsync(pdf, ct);
        var escalatedResult = await CreateEscalating().ExtractFullAsync(pdf, ct);

        AssertAgree(baselineResult, escalatedResult);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task ExtractHeaderAsync_EscalatingSeam_MatchesPositionalBaseline(string fixtureName)
    {
        var ct = TestContext.Current.CancellationToken;
        var pdf = await LoadFixtureOrSkipAsync(fixtureName, ct);

        var baselineResult = await CreateInner().ExtractHeaderAsync(pdf, ct);
        var escalatedResult = await CreateEscalating().ExtractHeaderAsync(pdf, ct);

        AssertAgree(baselineResult, escalatedResult);
    }

    private static async Task<byte[]> LoadFixtureOrSkipAsync(string fixtureName, CancellationToken ct)
    {
        var path = FixturePath(fixtureName);
        if (!File.Exists(path))
            Assert.Skip($"Demo corpus fixture not found at: {path}");

        return await File.ReadAllBytesAsync(path, ct);
    }

    // -----------------------------------------------------------------------
    // Agreement assertion
    // -----------------------------------------------------------------------

    private static void AssertAgree(Result<StatementModel> baselineResult, Result<StatementModel> escalatedResult)
    {
        escalatedResult.IsSuccess.ShouldBe(
            baselineResult.IsSuccess,
            "Escalating extractor must agree with the positional baseline on overall IsSuccess.");

        if (!baselineResult.IsSuccess)
        {
            // Both paths agree the document could not be extracted (e.g. scanned.pdf with no
            // text layer) — there is no StatementModel to compare fields on.
            return;
        }

        var baseline = baselineResult.Value!;
        var escalated = escalatedResult.Value!;
        var mismatches = new List<string>();

        // Header identity fields — always present on both ExtractHeaderAsync and ExtractFullAsync.
        Compare("ClientName", baseline.ClientName, escalated.ClientName, mismatches);
        Compare("Address", baseline.Address, escalated.Address, mismatches);
        Compare("BranchNumber", baseline.BranchNumber, escalated.BranchNumber, mismatches);
        Compare("CardNumber", baseline.CardNumber, escalated.CardNumber, mismatches);
        Compare("Clabe", baseline.Clabe, escalated.Clabe, mismatches);
        Compare("ClientNumber", baseline.ClientNumber, escalated.ClientNumber, mismatches);
        Compare("Rfc", baseline.Rfc, escalated.Rfc, mismatches);

        // Period/summary scalar fields — only present after ExtractFullAsync. Presence must agree
        // between the two paths (the decorator neither adds nor drops PeriodSummary).
        (baseline.PeriodSummary is null).ShouldBe(
            escalated.PeriodSummary is null,
            "Baseline and escalated PeriodSummary presence must agree.");

        if (baseline.PeriodSummary is not null && escalated.PeriodSummary is not null)
        {
            var bps = baseline.PeriodSummary;
            var eps = escalated.PeriodSummary;

            Compare("Product", bps.Product, eps.Product, mismatches);
            Compare("PeriodStart", bps.PeriodStart, eps.PeriodStart, mismatches);
            Compare("PeriodCutDate", bps.PeriodCutDate, eps.PeriodCutDate, mismatches);
            ComparePaymentDueDate(bps.PaymentDueDate, eps.PaymentDueDate, mismatches);
            Compare("DayCountPrinted", bps.DayCountPrinted, eps.DayCountPrinted, mismatches);
            Compare("PagoParaNoGenerarIntereses", bps.PagoParaNoGenerarIntereses, eps.PagoParaNoGenerarIntereses, mismatches);
            Compare("PagoMinimo", bps.PagoMinimo, eps.PagoMinimo, mismatches);
            Compare("PagoMinimoMasMeses", bps.PagoMinimoMasMeses, eps.PagoMinimoMasMeses, mismatches);
            Compare("Tasa", bps.Tasa, eps.Tasa, mismatches);
            Compare("Cat", bps.Cat, eps.Cat, mismatches);
            Compare("SaldoDeudorTotal", bps.SaldoDeudorTotal, eps.SaldoDeudorTotal, mismatches);
            Compare("CreditoDisponible", bps.CreditoDisponible, eps.CreditoDisponible, mismatches);
            Compare("AdeudoPeriodoAnterior", bps.AdeudoPeriodoAnterior, eps.AdeudoPeriodoAnterior, mismatches);
            Compare("CargosRegularesNoMeses", bps.CargosRegularesNoMeses, eps.CargosRegularesNoMeses, mismatches);
            Compare("CargosComprasAMesesCapital", bps.CargosComprasAMesesCapital, eps.CargosComprasAMesesCapital, mismatches);
            Compare("MontoIntereses", bps.MontoIntereses, eps.MontoIntereses, mismatches);
            Compare("MontoComisiones", bps.MontoComisiones, eps.MontoComisiones, mismatches);
            Compare("IvaInteresesYComisiones", bps.IvaInteresesYComisiones, eps.IvaInteresesYComisiones, mismatches);
            Compare("PagosYAbonos", bps.PagosYAbonos, eps.PagosYAbonos, mismatches);
            Compare("SaldoCargosRegulares", bps.SaldoCargosRegulares, eps.SaldoCargosRegulares, mismatches);
            Compare("SaldoCargosAMeses", bps.SaldoCargosAMeses, eps.SaldoCargosAMeses, mismatches);
            Compare("TotalCargos", bps.TotalCargos, eps.TotalCargos, mismatches);
            Compare("TotalAbonos", bps.TotalAbonos, eps.TotalAbonos, mismatches);
        }

        mismatches.ShouldBeEmpty(
            $"Escalating extractor diverged from the positional baseline:{Environment.NewLine}{string.Join(Environment.NewLine, mismatches)}");

        // Movements is a structural/table field passed through unchanged by the decorator (E1
        // does not thread it through the per-field orchestrator) — counts and status must match.
        escalated.Movements.Count.ShouldBe(baseline.Movements.Count, "Movements row count must be unchanged.");
        escalated.MovementsStatus.ShouldBe(baseline.MovementsStatus, "MovementsStatus must be unchanged.");
    }

    /// <summary>
    /// Compares one scalar field between the baseline and escalated <see cref="StatementModel"/>,
    /// appending a human-readable mismatch description (naming the field) to
    /// <paramref name="mismatches"/> rather than asserting immediately, so a single failing theory
    /// case reports every diverging field at once.
    /// </summary>
    private static void Compare<T>(
        string fieldName,
        ExtractedField<T> baseline,
        ExtractedField<T> escalated,
        List<string> mismatches)
    {
        if (baseline.Status != escalated.Status)
        {
            mismatches.Add($"{fieldName}: Status baseline={baseline.Status} escalated={escalated.Status}");
        }
        else if (!Equals(baseline.Value, escalated.Value))
        {
            mismatches.Add($"{fieldName}: Value baseline={baseline.Value} escalated={escalated.Value}");
        }

        // Every field on the E1 escalated path must still carry positional provenance — the
        // per-field orchestrator's empty ladders mean stage 1 is always final in this epic.
        if (escalated.Provenance.Stage != StageId.Positional)
        {
            mismatches.Add(
                $"{fieldName}: escalated Provenance.Stage={escalated.Provenance.Stage} (expected {StageId.Positional})");
        }
    }

    /// <summary>
    /// PaymentDueDate-specific comparison (E2.2): unlike every other field in this harness, this
    /// one has a non-empty ladder, so the escalated path is allowed to genuinely recover a value
    /// the positional extractor missed. See the class remarks for the full acceptance matrix.
    /// </summary>
    private static void ComparePaymentDueDate(
        ExtractedField<DateOnly> baseline,
        ExtractedField<DateOnly> escalated,
        List<string> mismatches)
    {
        if (baseline.Status != ExtractionStatus.NotExtracted)
        {
            // Positional already found something — StatusGate (PaymentDueDate's only trigger)
            // cannot fire, so the orchestrator cannot have escalated. Must be byte-identical, same
            // as every other field.
            Compare("PaymentDueDate", baseline, escalated, mismatches);
            return;
        }

        // Positional found nothing: either the fuzzy stage also honestly abstained (unchanged —
        // required for every other field, still the default expectation here) or it recovered a
        // plausible value. Anything else is a real divergence.
        if (escalated.Status == ExtractionStatus.NotExtracted)
            return;

        var isHonestRecovery =
            escalated.Status == ExtractionStatus.Extracted
            && escalated.Provenance.Stage == StageId.FuzzyLabel
            && PaymentDueDatePlausibilityValidator.IsPlausible(escalated.Value);

        if (!isHonestRecovery)
        {
            mismatches.Add(
                $"PaymentDueDate: escalated result is neither 'still NotExtracted' nor a plausible "
                + $"FuzzyLabel recovery. Status={escalated.Status} Value={escalated.Value} "
                + $"Provenance={escalated.Provenance.Stage}");
        }

        // A genuine recovery is not itself a mismatch — it is the point of E2.2 — but it changes
        // the demo verdict-preservation gate's scope, so it must be visible in test output rather
        // than silently swallowed.
        TestContext.Current.SendDiagnosticMessage(
            isHonestRecovery
                ? $"[E2.2] PaymentDueDate recovered {escalated.Value:yyyy-MM-dd} via FuzzyLabel "
                  + $"(confidence {escalated.Confidence:0.00})."
                : "[E2.2] PaymentDueDate escalated result failed the honesty check above.");
    }
}
