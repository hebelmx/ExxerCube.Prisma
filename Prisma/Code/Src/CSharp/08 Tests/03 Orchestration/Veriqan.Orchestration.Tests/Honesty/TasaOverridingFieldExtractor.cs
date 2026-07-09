using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Honesty;

/// <summary>
/// Decorator for the S3.1 verdict-flip guard (deliverable 3): runs the real, unmodified
/// production <see cref="IStatementFieldExtractor"/> and then overwrites
/// <see cref="PeriodSummary.Tasa"/> with a plausible-but-wrong value, leaving every other field
/// (including <see cref="ExtractionStatus"/>, which stays <see cref="ExtractionStatus.Extracted"/>
/// — a confidently-wrong value, not an abstention) untouched. This is the cleanest controllable
/// seam named in the S3.1 brief: it corrupts data <em>after</em> real extraction/escalation, so
/// the injected error reaches validation exactly as a genuine mis-read digit would, without
/// needing to fabricate a whole synthetic PDF.
/// </summary>
internal sealed class TasaOverridingFieldExtractor : IStatementFieldExtractor
{
    private readonly IStatementFieldExtractor _inner;
    private readonly decimal _wrongTasa;

    public TasaOverridingFieldExtractor(IStatementFieldExtractor inner, decimal wrongTasa)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _wrongTasa = wrongTasa;
    }

    public Task<Result<StatementModel>> ExtractHeaderAsync(byte[] pdf, CancellationToken cancellationToken = default) =>
        _inner.ExtractHeaderAsync(pdf, cancellationToken);

    public async Task<Result<StatementModel>> ExtractFullAsync(
        byte[] pdf,
        CancellationToken cancellationToken = default,
        VecReferenceBundle? referenceBundle = null)
    {
        var result = await _inner.ExtractFullAsync(pdf, cancellationToken, referenceBundle).ConfigureAwait(false);
        if (result.IsCancelled() || result.IsFailure)
            return result;

        var model = result.Value!;
        if (model.PeriodSummary is not { } ps)
            return result;

        var corruptedTasa = new ExtractedField<decimal>(
            _wrongTasa,
            ps.Tasa.Confidence,
            ps.Tasa.Locator,
            ExtractionStatus.Extracted,
            ps.Tasa.Provenance);

        var corruptedPeriodSummary = new PeriodSummary(
            ps.Product,
            ps.PeriodStart,
            ps.PeriodCutDate,
            ps.PaymentDueDate,
            ps.DayCountPrinted,
            ps.DayCount,
            ps.PagoParaNoGenerarIntereses,
            ps.PagoMinimo,
            ps.PagoMinimoMasMeses,
            corruptedTasa,
            ps.Cat,
            ps.SaldoDeudorTotal,
            ps.CreditoDisponible,
            ps.AdeudoPeriodoAnterior,
            ps.CargosRegularesNoMeses,
            ps.CargosComprasAMesesCapital,
            ps.MontoIntereses,
            ps.MontoComisiones,
            ps.IvaInteresesYComisiones,
            ps.PagosYAbonos,
            ps.SaldoCargosRegulares,
            ps.SaldoCargosAMeses,
            ps.TotalCargos,
            ps.TotalAbonos);

        var corruptedModel = new StatementModel(
            model.ClientName, model.Address, model.BranchNumber, model.CardNumber,
            model.Clabe, model.ClientNumber, model.Rfc)
        {
            PeriodSummary = corruptedPeriodSummary,
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

        return Result<StatementModel>.WithSuccess(corruptedModel);
    }
}
