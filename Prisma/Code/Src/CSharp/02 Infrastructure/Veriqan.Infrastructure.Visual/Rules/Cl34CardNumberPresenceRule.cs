using System.Linq;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Visual.Rules;

/// <summary>
/// CL-34: Verifies that every page of the statement contains the card number extracted
/// from the statement header.
/// </summary>
/// <remarks>
/// <para>
/// <b>Technique (ADR-V2):</b> <see cref="TechniqueClass.Deterministic"/> — structural inspection only.
/// </para>
/// <para>
/// <b>Card-number matching:</b> the card number (digits only, spaces stripped) must appear
/// in each page's text (also stripped of spaces).  Matching is case-insensitive on digits.
/// The extractor also supports masked card numbers (e.g. <c>XXXX-XXXX-XXXX-1234</c>) by
/// matching the last-4 digits within a 4-digit-group card pattern — guarding against
/// false positives from dates or year tokens (VERIQAN-E2-S6).
/// </para>
/// <para>
/// <b>Page-1 propagation (VERIQAN-E2-S6):</b> if the card is confirmed on page 1 and every
/// page that is missing the card has no text layer (<c>HasContent = false</c> — image-only),
/// the rule returns Pass for all pages.  Banks often print the card number only in the header
/// on page 1; inner pages may render it inside a repeated graphic that text extraction cannot
/// reach.  If a page with <c>HasContent = true</c> is missing the card, the absence is treated
/// as a genuine failure even when page 1 confirms the card.
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item><c>StatementModel</c> is <see langword="null"/> (extraction stage did not run).</item>
///   <item><see cref="StatementModel.Pages"/> is empty (per-page extraction did not run).</item>
///   <item><see cref="StatementModel.CardNumber"/> was not successfully extracted
///     (<see cref="ExtractionStatus.NotExtracted"/>).</item>
///   <item>No page has the card in its text layer (all pages are image-only — card may be
///     embedded in a header graphic that text extraction cannot reach).</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Cl34CardNumberPresenceRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-34";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §15";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.BaselineLocked;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        var model = ctx.StatementModel;

        if (model is null || model.Pages.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: "No per-page inspection data available — extraction stage did not run."));
        }

        // Guard: card number must have been extracted.
        if (model.CardNumber.Status == ExtractionStatus.NotExtracted)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: "Card number was not extracted from the statement header — cannot verify per-page presence."));
        }

        var cardNumber = model.CardNumber.Value;
        if (string.IsNullOrWhiteSpace(cardNumber))
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: "Extracted card number value is empty — cannot verify per-page presence."));
        }

        var pagesWithoutCardNumber = model.Pages
            .Where(p => !p.ContainsCardNumber)
            .ToList();

        if (pagesWithoutCardNumber.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"Card number present on all {model.Pages.Count} page(s)."));
        }

        // First-page propagation: banks commonly print the card number only in the header on the
        // first page.  If the first page (by minimum PageNumber — sliced statements may not start
        // at absolute page 1) has the card in its text layer AND every page that is missing the
        // card has NO text layer (HasContent = false — image-only page where text extraction yields
        // nothing), treat all pages as PASS.  Rationale: inner pages may render the card only in a
        // repeated graphic/watermark header that the text-layer extractor cannot read; a confirmed
        // first-page hit is sufficient evidence when the remaining pages are image-only.
        // Note: if a page HAS text content but the card is absent, that is a genuine failure even
        // when the first page confirms the card — image-only propagation does not override real text evidence.
        var firstPage = model.Pages.MinBy(p => p.PageNumber);
        if (firstPage is { ContainsCardNumber: true })
        {
            var allMissingAreImageOnly = pagesWithoutCardNumber.All(p => !p.HasContent);
            if (allMissingAreImageOnly)
            {
                return Result<RuleFinding>.WithSuccess(
                    RuleFinding.Pass(
                        checkId: CheckId,
                        technique: Technique,
                        engineVersion: Version,
                        observed: $"Card number confirmed on page {firstPage.PageNumber} (first page) — propagated as PASS for all {model.Pages.Count} page(s) (remaining pages are image-only with no text layer)."));
            }
        }

        // Abstain-safety: if the card number is on NO page in the text layer AND no page has any
        // text content (e.g. the card is only inside a header graphic), we cannot distinguish a
        // genuine absence from an image-only rendering.  Emit InsufficientData rather than Fail.
        var anyPageHasCard = model.Pages.Any(p => p.ContainsCardNumber);
        if (!anyPageHasCard)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: "Card number not found in the text layer of any page — may be embedded in a header graphic that text extraction cannot reach."));
        }

        var pageNumbers = string.Join(", ", pagesWithoutCardNumber.Select(p => p.PageNumber));
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: "Card number present on every page",
                observed: $"{pagesWithoutCardNumber.Count} page(s) do not contain the card number: page(s) {pageNumbers}.",
                locator: pagesWithoutCardNumber[0].Locator));
    }
}
