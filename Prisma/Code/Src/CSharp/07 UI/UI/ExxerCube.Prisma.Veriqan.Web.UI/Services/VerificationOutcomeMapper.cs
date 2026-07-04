using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Services;

// MINIMAL mapping — VLD-S4 enriches with the LAW-vs-BRAND ledger citations, tier tags, and
// bbox rail data.
/// <summary>
/// Minimal <see cref="IVerificationOutcomeMapper"/> implementation: copies the pipeline's real
/// <see cref="VerificationOutcome"/> onto the demo-shaped <see cref="DemoStatementCase"/> view
/// model without any reference-data-driven enrichment (labels, DOF numerals, tiers).
/// </summary>
public sealed class VerificationOutcomeMapper : IVerificationOutcomeMapper
{
    /// <inheritdoc />
    public DemoStatementCase Map(VerificationOutcome outcome, string fileName)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(fileName);

        var summary = outcome.Summary;
        var findings = outcome.Findings
            .Select(MapFinding)
            .ToList()
            .AsReadOnly();

        return new DemoStatementCase
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
            AuditRows = Array.Empty<DemoAuditRow>(),
            Dispositions = [],
        };
    }

    private static DemoFinding MapFinding(RuleFinding finding) => new()
    {
        CheckId = finding.CheckId,
        Verdict = finding.Verdict,
        Technique = finding.Technique,
        Severity = finding.Severity,
        Label = finding.CheckId,
        Expected = finding.Expected,
        Observed = finding.Observed,
        DofNumeral = finding.DofNumeral,
        Tier = ChecklistTier.Condusef,
        Confidence = finding.Confidence,
    };
}
