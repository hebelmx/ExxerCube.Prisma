using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using ExxerCube.Prisma.Veriqan.Web.UI.Models;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Services;

/// <summary>
/// <see cref="IVerificationOutcomeMapper"/> implementation (VLD-S4a): copies the pipeline's real
/// <see cref="VerificationOutcome"/> onto the demo-shaped <see cref="DemoStatementCase"/> view
/// model, enriching each finding from the real check ledger (<see cref="RealCheckLedger"/>) —
/// honest tiers, human-readable labels, and law-vs-brand DOF-numeral citations — instead of the
/// prior hardcoded <c>Tier = Condusef</c> / <c>Label = CheckId</c> placeholder behavior.
/// </summary>
public sealed class VerificationOutcomeMapper : IVerificationOutcomeMapper
{
    private readonly RealCheckLedger _ledger;

    /// <summary>Initializes a new <see cref="VerificationOutcomeMapper"/>.</summary>
    /// <param name="ledger">The real check ledger used to enrich each finding.</param>
    public VerificationOutcomeMapper(RealCheckLedger ledger)
    {
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
    }

    /// <inheritdoc />
    public Result<DemoStatementCase> Map(
        VerificationOutcome outcome,
        IReadOnlyDictionary<int, byte[]> markedPagePngs,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<DemoStatementCase>();

        if (outcome is null)
            return Result<DemoStatementCase>.WithFailure("Verification outcome cannot be null.");

        if (string.IsNullOrWhiteSpace(fileName))
            return Result<DemoStatementCase>.WithFailure("File name cannot be null or empty.");

        var pagePngs = markedPagePngs ?? new Dictionary<int, byte[]>();

        var summary = outcome.Summary;
        var findings = outcome.Findings
            .Select(MapFinding)
            .ToList()
            .AsReadOnly();

        var demoCase = new DemoStatementCase
        {
            JobId = outcome.Job.Id,
            CaseName = $"Resultado en vivo — {fileName}",
            FileName = fileName,
            Signal = summary.Signal,
            CondusefTierVerdict = summary.CondusefTierVerdict,
            BankTierVerdict = summary.BankTierVerdict,
            TotalChecks = summary.Total,
            PassCount = summary.PassCount,
            FailCount = summary.FailCount,
            InsufficientDataCount = summary.InsufficientDataCount,
            FailCheckIds = summary.FailCheckIds,
            BlockReason = summary.BlockedOutcome?.Reason,
            BlockDetail = summary.BlockedOutcome?.Detail,
            Findings = findings,
            ExtractedFields = new DemoExtractedFields(),
            ProcessingDuration = outcome.ProcessingDuration,
            ReceivedAtUtc = outcome.Job.ReceivedAtUtc,
            MarkedPdfPath = null,
            MarkedPagePngs = pagePngs,
            AuditRows = Array.Empty<DemoAuditRow>(),
            Dispositions = [],
        };

        return Result<DemoStatementCase>.WithSuccess(demoCase);
    }

    private DemoFinding MapFinding(RuleFinding finding)
    {
        if (_ledger.TryGet(finding.CheckId, out var entry) && entry is not null)
        {
            return new DemoFinding
            {
                CheckId = finding.CheckId,
                Verdict = finding.Verdict,
                Technique = finding.Technique,
                Severity = finding.Severity,
                Label = entry.Label,
                Expected = finding.Expected,
                Observed = finding.Observed,
                DofNumeral = entry.DofNumeralDisplay,
                Tier = entry.Tier,
                Confidence = finding.Confidence,
                IsVisual = entry.IsVisual,
                Locator = finding.Locator,
            };
        }

        // Uncatalogued CheckId: conservative Condusef tier (never silently dropped from a RED
        // outcome), and an honest engineering-gap label — never the raw CheckId as label.
        return new DemoFinding
        {
            CheckId = finding.CheckId,
            Verdict = finding.Verdict,
            Technique = finding.Technique,
            Severity = finding.Severity,
            Label = RealCheckLedger.UncataloguedLabel,
            Expected = finding.Expected,
            Observed = finding.Observed,
            DofNumeral = finding.DofNumeral,
            Tier = ChecklistTier.Condusef,
            Confidence = finding.Confidence,
            IsVisual = false,
            Locator = finding.Locator,
        };
    }
}
