using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Strangler-fig decorator that threads every scalar field of a <see cref="StatementModel"/>
/// produced by the inner (positional) extractor through the
/// <see cref="FieldResolutionOrchestrator"/>, so that higher stages (E2+) can be added purely by
/// registering ladders/stages — with no change to <c>PdfPigStatementFieldExtractor</c> or any
/// caller of <see cref="IStatementFieldExtractor"/>.
/// </summary>
/// <remarks>
/// <para>
/// As of E1, <see cref="FieldEscalationLadderRegistry"/> returns an empty (positional-only)
/// ladder for every <see cref="FieldKind"/>, so <see cref="FieldResolutionOrchestrator.ResolveAsync{TValue}"/>
/// returns each field's original <see cref="ExtractedField{T}"/> instance <em>by reference,
/// unchanged</em>. This decorator detects that with a reference-equality check per field
/// (<c>anyEscalated</c>) and, in the E1 case, returns the inner <see cref="StatementModel"/>
/// instance completely unchanged instead of rebuilding it — zero allocation, zero risk of a
/// rebuild bug affecting the demo verdicts. The rebuild path only executes once a later epic
/// actually registers a non-empty ladder for at least one field.
/// </para>
/// <para>
/// <see cref="StatementModel.Movements"/> is a structural/table field (a
/// <see cref="System.Collections.Generic.IReadOnlyList{T}"/> of rows, not an
/// <see cref="ExtractedField{T}"/>) — it is passed through unchanged by this decorator. Per the
/// design doc, the movements table needs a fuzzy-heading/table-shape resolution strategy that is
/// structurally different from the scalar per-field ladder used here; it is out of scope for E1
/// and is not threaded through <see cref="FieldResolutionOrchestrator"/>.
/// </para>
/// </remarks>
public sealed class EscalatingStatementFieldExtractor : IStatementFieldExtractor
{
    private readonly IStatementFieldExtractor _inner;
    private readonly FieldResolutionOrchestrator _orchestrator;
    private readonly ILogger<EscalatingStatementFieldExtractor> _logger;

    /// <summary>
    /// Initializes an <see cref="EscalatingStatementFieldExtractor"/>.
    /// </summary>
    /// <param name="inner">
    /// The stage-1 (positional) extractor, run exactly once per call. In production this is the
    /// concrete <c>PdfPigStatementFieldExtractor</c>.
    /// </param>
    /// <param name="orchestrator">Per-field resolution pipeline walking each field's ladder.</param>
    /// <param name="logger">Logger for escalation diagnostics.</param>
    public EscalatingStatementFieldExtractor(
        IStatementFieldExtractor inner,
        FieldResolutionOrchestrator orchestrator,
        ILogger<EscalatingStatementFieldExtractor> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<Result<StatementModel>> ExtractHeaderAsync(
        byte[] pdf,
        CancellationToken cancellationToken = default)
    {
        var innerResult = await _inner.ExtractHeaderAsync(pdf, cancellationToken).ConfigureAwait(false);
        return await EscalateAsync(innerResult, pdf, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Result<StatementModel>> ExtractFullAsync(
        byte[] pdf,
        CancellationToken cancellationToken = default)
    {
        var innerResult = await _inner.ExtractFullAsync(pdf, cancellationToken).ConfigureAwait(false);
        return await EscalateAsync(innerResult, pdf, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<StatementModel>> EscalateAsync(
        Result<StatementModel> innerResult,
        byte[] pdf,
        CancellationToken cancellationToken)
    {
        // The inner extractor's own failure/cancellation is passed straight through — escalation
        // never runs on a document the positional extractor could not even open.
        if (innerResult.IsCancelled() || innerResult.IsFailure)
            return innerResult;

        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<StatementModel>();

        var model = innerResult.Value!;
        using var corpus = new LazyPdfCorpus(pdf);
        var budget = new StageBudget();

        Result<StatementModel>? abort = null;
        var anyEscalated = false;

        async Task<ExtractedField<TValue>> ResolveAsync<TValue>(
            FieldKind fieldKind,
            ExtractedField<TValue> positional,
            IFieldValidator? validatorOverride = null)
        {
            // Once aborted (a stage failed/was cancelled), stop calling the orchestrator for the
            // remaining fields — the result is discarded by the caller regardless.
            if (abort is not null)
                return positional;

            var result = await _orchestrator
                .ResolveAsync(fieldKind, positional, pdf, corpus, budget, higherStages: null, validatorOverride, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsCancelled())
            {
                abort = ResultExtensions.Cancelled<StatementModel>();
                return positional;
            }

            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "Field resolution failed for {FieldKind}: {Error}",
                    fieldKind,
                    result.Errors?.FirstOrDefault() ?? "unknown error");
                abort = Result<StatementModel>.WithFailure(result.Errors);
                return positional;
            }

            var resolved = result.Value!;
            if (!ReferenceEquals(resolved, positional))
                anyEscalated = true;

            return resolved;
        }

        // -----------------------------------------------------------------
        // Header — identity fields (always present).
        // -----------------------------------------------------------------
        var clientName = await ResolveAsync(FieldKind.ClientName, model.ClientName).ConfigureAwait(false);
        var address = await ResolveAsync(FieldKind.Address, model.Address).ConfigureAwait(false);
        var branchNumber = await ResolveAsync(FieldKind.BranchNumber, model.BranchNumber).ConfigureAwait(false);
        var cardNumber = await ResolveAsync(FieldKind.CardNumber, model.CardNumber).ConfigureAwait(false);
        var clabe = await ResolveAsync(FieldKind.Clabe, model.Clabe).ConfigureAwait(false);
        var clientNumber = await ResolveAsync(FieldKind.ClientNumber, model.ClientNumber).ConfigureAwait(false);
        var rfc = await ResolveAsync(FieldKind.Rfc, model.Rfc).ConfigureAwait(false);

        if (abort is not null)
            return abort;

        // -----------------------------------------------------------------
        // Period / summary fields — only present after ExtractFullAsync.
        // -----------------------------------------------------------------
        var periodSummary = model.PeriodSummary;
        if (periodSummary is not null)
        {
            var product = await ResolveAsync(FieldKind.Product, periodSummary.Product).ConfigureAwait(false);
            var periodStart = await ResolveAsync(FieldKind.PeriodStart, periodSummary.PeriodStart).ConfigureAwait(false);
            var periodCutDate = await ResolveAsync(FieldKind.PeriodCutDate, periodSummary.PeriodCutDate).ConfigureAwait(false);

            // Once the statement's own period cut date is resolved, PaymentDueDate's ladder
            // validator is superseded — for this call only — by a period-relative instance
            // (tighter than the static [2020, 2035] sanity window). When the cut date is missing
            // or not a real extracted value, no override is passed and the ladder falls back to
            // its own static-window PaymentDueDatePlausibilityValidator instance unchanged.
            var paymentDueDateValidator = periodCutDate.Status == ExtractionStatus.Extracted
                ? new PaymentDueDatePlausibilityValidator(periodCutDate.Value)
                : null;
            var paymentDueDate = await ResolveAsync(FieldKind.PaymentDueDate, periodSummary.PaymentDueDate, paymentDueDateValidator).ConfigureAwait(false);
            var dayCountPrinted = await ResolveAsync(FieldKind.DayCountPrinted, periodSummary.DayCountPrinted).ConfigureAwait(false);
            var pagoParaNoGenerarIntereses = await ResolveAsync(FieldKind.PagoParaNoGenerarIntereses, periodSummary.PagoParaNoGenerarIntereses).ConfigureAwait(false);
            var pagoMinimo = await ResolveAsync(FieldKind.PagoMinimo, periodSummary.PagoMinimo).ConfigureAwait(false);
            var pagoMinimoMasMeses = await ResolveAsync(FieldKind.PagoMinimoMasMeses, periodSummary.PagoMinimoMasMeses).ConfigureAwait(false);
            var tasa = await ResolveAsync(FieldKind.Tasa, periodSummary.Tasa).ConfigureAwait(false);
            var cat = await ResolveAsync(FieldKind.Cat, periodSummary.Cat).ConfigureAwait(false);
            var saldoDeudorTotal = await ResolveAsync(FieldKind.SaldoDeudorTotal, periodSummary.SaldoDeudorTotal).ConfigureAwait(false);
            var creditoDisponible = await ResolveAsync(FieldKind.CreditoDisponible, periodSummary.CreditoDisponible).ConfigureAwait(false);
            var adeudoPeriodoAnterior = await ResolveAsync(FieldKind.AdeudoPeriodoAnterior, periodSummary.AdeudoPeriodoAnterior).ConfigureAwait(false);
            var cargosRegularesNoMeses = await ResolveAsync(FieldKind.CargosRegularesNoMeses, periodSummary.CargosRegularesNoMeses).ConfigureAwait(false);
            var cargosComprasAMesesCapital = await ResolveAsync(FieldKind.CargosComprasAMesesCapital, periodSummary.CargosComprasAMesesCapital).ConfigureAwait(false);
            var montoIntereses = await ResolveAsync(FieldKind.MontoIntereses, periodSummary.MontoIntereses).ConfigureAwait(false);
            var montoComisiones = await ResolveAsync(FieldKind.MontoComisiones, periodSummary.MontoComisiones).ConfigureAwait(false);
            var ivaInteresesYComisiones = await ResolveAsync(FieldKind.IvaInteresesYComisiones, periodSummary.IvaInteresesYComisiones).ConfigureAwait(false);
            var pagosYAbonos = await ResolveAsync(FieldKind.PagosYAbonos, periodSummary.PagosYAbonos).ConfigureAwait(false);
            var saldoCargosRegulares = await ResolveAsync(FieldKind.SaldoCargosRegulares, periodSummary.SaldoCargosRegulares).ConfigureAwait(false);
            var saldoCargosAMeses = await ResolveAsync(FieldKind.SaldoCargosAMeses, periodSummary.SaldoCargosAMeses).ConfigureAwait(false);
            var totalCargos = await ResolveAsync(FieldKind.TotalCargos, periodSummary.TotalCargos).ConfigureAwait(false);
            var totalAbonos = await ResolveAsync(FieldKind.TotalAbonos, periodSummary.TotalAbonos).ConfigureAwait(false);

            if (abort is not null)
                return abort;

            if (anyEscalated)
            {
                // Dormant rebuild path: only reached once a later epic registers a non-empty
                // ladder for at least one PeriodSummary field. DayCount is not an ExtractedField
                // and is carried over verbatim — no stage resolves it.
                periodSummary = new PeriodSummary(
                    product,
                    periodStart,
                    periodCutDate,
                    paymentDueDate,
                    dayCountPrinted,
                    periodSummary.DayCount,
                    pagoParaNoGenerarIntereses,
                    pagoMinimo,
                    pagoMinimoMasMeses,
                    tasa,
                    cat,
                    saldoDeudorTotal,
                    creditoDisponible,
                    adeudoPeriodoAnterior,
                    cargosRegularesNoMeses,
                    cargosComprasAMesesCapital,
                    montoIntereses,
                    montoComisiones,
                    ivaInteresesYComisiones,
                    pagosYAbonos,
                    saldoCargosRegulares,
                    saldoCargosAMeses,
                    totalCargos,
                    totalAbonos);
            }
        }

        if (!anyEscalated)
        {
            // E1 always takes this path: every ladder is empty, so every field above came back
            // as the exact same instance already on `model`. Returning `model` itself (rather
            // than a field-by-field-identical rebuild) is the behavior-neutral guarantee E1 is
            // required to prove.
            return Result<StatementModel>.WithSuccess(model);
        }

        var rebuilt = new StatementModel(clientName, address, branchNumber, cardNumber, clabe, clientNumber, rfc)
        {
            PeriodSummary = periodSummary,
            Movements = model.Movements,
            MovementsStatus = model.MovementsStatus,
            TextOverlapIncidents = model.TextOverlapIncidents,
            SectionHeaderStyles = model.SectionHeaderStyles,
            FontRuns = model.FontRuns,
            FontExtractionStatus = model.FontExtractionStatus,
            NormalizedFullText = model.NormalizedFullText,
            FiscalBlock = model.FiscalBlock,
            PageCount = model.PageCount,
            Pages = model.Pages,
            Sections = model.Sections,
            SectionGaps = model.SectionGaps,
            TypographySamples = model.TypographySamples,
            TypographyExtractionStatus = model.TypographyExtractionStatus,
            FinancialTables = model.FinancialTables,
            DisputeRows = model.DisputeRows,
            DisputeRowsStatus = model.DisputeRowsStatus,
            PagePerceptualHashes = model.PagePerceptualHashes,
        };

        return Result<StatementModel>.WithSuccess(rebuilt);
    }
}
