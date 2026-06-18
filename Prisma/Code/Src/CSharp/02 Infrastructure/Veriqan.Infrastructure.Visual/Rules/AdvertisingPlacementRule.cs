using System;
using System.Collections.Generic;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Visual.Rules;

/// <summary>
/// LAW-ADS-PLACEMENT: Verifies that advertising content is restricted to the legally
/// permitted free sections (§21 and §28 — the "sección opcional libre"), and that
/// §12 "Mensajes importantes" is not over-length and does not contain promotional content.
/// </summary>
/// <remarks>
/// <para>
/// <b>Legal basis:</b> the CONDUSEF <i>Acuerdo</i> §12 specifies the purpose of the
/// "Mensajes importantes" section as a mandatory informational area restricted to legal
/// and product notices.  Advertising is not permitted in mandatory sections; only the
/// optional free sections §21 and §28 are available for promotional material.
/// </para>
/// <para>
/// <b>Technique:</b> <see cref="TechniqueClass.Deterministic"/> — structural check
/// (§12 byte-length) plus conservative heuristic marker matching.
/// </para>
/// <para>
/// <b>PREVENTIVE GATE — CARDINAL RULE: NEVER false-Fail.</b>
/// Advertising detection is heuristic and corpus-starved.  The promotional-marker list
/// is intentionally small and restricted to phrases that would NOT normally appear in
/// mandated legal or financial statement content.  If a section text is ambiguous, the
/// rule abstains rather than risks flagging a false positive.  The marker list MUST be
/// calibrated against a real CONDUSEF-statement corpus before tightening
/// (carry-forward for a future story).
/// </para>
/// <para>
/// <b>InsufficientData paths (abstain — never false-Fail):</b>
/// <list type="bullet">
///   <item><c>StatementModel</c> is <see langword="null"/> (extraction stage did not run).</item>
///   <item><c>StatementModel.Sections</c> is <see langword="null"/> or empty (section-detection
///     pass did not run or the PDF has no text layer).</item>
/// </list>
/// </para>
/// <para>
/// <b>§12 length sub-check:</b> objective and deterministic.  If §12 is Present with
/// non-empty text exceeding <see cref="Section12MaxChars"/> characters, that is a
/// confident finding.  If §12 is Absent, Indeterminate, or has empty text, this
/// sub-check is skipped (not Fail).
/// </para>
/// <para>
/// <b>Advertising heuristic sub-check:</b> scans section text — after
/// <see cref="VecTextNormalizer.Normalize"/> — for a conservative allowlist of strong
/// promotional phrases.  Generic terms that are legitimate statement content
/// ("meses sin intereses", "CAT", "pago", "tasa", "credito") are excluded from the
/// marker list and will never trigger a Fail.
/// </para>
/// </remarks>
internal sealed class AdvertisingPlacementRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    // §12 "Mensajes importantes" character ceiling.
    // An over-length §12 is a high-confidence finding (the section has a fixed purpose).
    private const int Section12MaxChars = 700;

    // Only §21 and §28 are the "sección opcional libre" — advertising is lawful there.
    private static readonly int[] PermittedAdvertisingSections = [21, 28];

    /// <summary>
    /// Conservative promotional-marker list (post-normalization: upper-case, accent-stripped).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each entry must be a phrase that would NOT normally appear as mandated legal
    /// or financial content in a CONDUSEF-compliant statement.  This list is intentionally
    /// biased toward false-negatives (missing advertising) rather than false-positives
    /// (wrongly flagging legitimate content), per the preventive-gate cardinal rule.
    /// </para>
    /// <para>
    /// DO NOT add generic terms — "meses sin intereses", "CAT", "pago", "tasa",
    /// "credito", "saldo", "interes" — even if they sometimes appear in advertising.
    /// Explicitly excluded terms ensure the rule never false-Fails on normal statement
    /// language.
    /// </para>
    /// <para>
    /// Calibration carry-forward: this list must be validated against a real
    /// CONDUSEF-statement corpus.  Add entries only after confirmed false-negatives;
    /// remove entries that trigger false-positives in the wild.
    /// </para>
    /// </remarks>
    private static readonly string[] PromotionalMarkers =
    [
        "CONTRATA YA",
        "CONTRATA HOY",
        "ADQUIERE TU",
        "SOLICITA TU",
        "PROMOCION ESPECIAL",
        "OFERTA EXCLUSIVA",
        "TASA PREFERENCIAL",
        "FELICIDADES HAS SIDO PRESELECCIONADO",
        "PREAPROBADO",
        "VISITA NUESTRA PAGINA PARA CONTRATAR",
    ];

    /// <inheritdoc />
    public string CheckId => "LAW-ADS-PLACEMENT";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §12";

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

        // InsufficientData: section-detection pass did not run.
        if (model is null || model.Sections is null || model.Sections.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: "Section boundaries unavailable (Epic 10 detection pass did not run or PDF has no text layer)."));
        }

        var sections = model.Sections;

        // Collect findings across sub-checks.
        var findings = new List<(string Detail, FieldLocator? Locator, bool IsOverLength)>();

        // -----------------------------------------------------------------------
        // Sub-check 1: §12 length (objective / deterministic)
        // -----------------------------------------------------------------------
        var section12 = FindSection(sections, sectionNumber: 12);
        if (section12 is not null
            && section12.DetectionStatus == SectionDetectionStatus.Present
            && !string.IsNullOrEmpty(section12.SectionText))
        {
            var len = section12.SectionText.Length;
            if (len > Section12MaxChars)
            {
                findings.Add((
                    Detail: $"§12 SectionText is {len} chars, exceeding the {Section12MaxChars}-char ceiling by {len - Section12MaxChars} chars.",
                    Locator: section12.Locator,
                    IsOverLength: true));
            }
        }

        // -----------------------------------------------------------------------
        // Sub-check 2: §12 advertising heuristic
        // -----------------------------------------------------------------------
        if (section12 is not null
            && section12.DetectionStatus == SectionDetectionStatus.Present
            && !string.IsNullOrEmpty(section12.SectionText))
        {
            var markerFound = FindMarker(section12.SectionText);
            if (markerFound is not null)
            {
                findings.Add((
                    Detail: $"§12 contains promotional marker \"{markerFound}\" — advertising not permitted in a mandatory section.",
                    Locator: section12.Locator,
                    IsOverLength: false));
            }
        }

        // -----------------------------------------------------------------------
        // Sub-check 3: advertising in non-permitted sections (heuristic)
        // -----------------------------------------------------------------------
        foreach (var section in sections)
        {
            if (!IsPermitted(section.SectionNumber)
                && section.DetectionStatus == SectionDetectionStatus.Present
                && !string.IsNullOrEmpty(section.SectionText))
            {
                var markerFound = FindMarker(section.SectionText);
                if (markerFound is not null)
                {
                    findings.Add((
                        Detail: $"§{section.SectionNumber} ({section.Name}) contains promotional marker \"{markerFound}\" — advertising is only permitted in §21 and §28.",
                        Locator: section.Locator,
                        IsOverLength: false));
                }
            }
        }

        // -----------------------------------------------------------------------
        // Verdict
        // -----------------------------------------------------------------------
        if (findings.Count == 0)
        {
            var s12Len = (section12 is not null
                && section12.DetectionStatus == SectionDetectionStatus.Present
                && !string.IsNullOrEmpty(section12.SectionText))
                ? section12.SectionText.Length
                : 0;

            var passNote = s12Len > 0
                ? $"§12 length OK ({s12Len} chars ≤ {Section12MaxChars}); no advertising markers in non-permitted sections."
                : $"§12 length sub-check skipped (§12 absent/indeterminate or empty text); no advertising markers in non-permitted sections.";

            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: passNote));
        }

        // At least one finding: pick Critical if any are over-length, else Warning.
        var hasOverLength = findings.Exists(f => f.IsOverLength);
        var severity = hasOverLength ? FindingSeverity.Critical : FindingSeverity.Warning;

        // Use the locator of the first finding (most anchored detail first).
        var primaryLocator = findings[0].Locator;
        var observed = string.Join(" | ", findings.ConvertAll(f => f.Detail));

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: severity,
                engineVersion: Version,
                expected: $"§12 ≤ {Section12MaxChars} chars; no promotional markers outside §21/§28.",
                observed: observed,
                locator: primaryLocator));
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static DetectedSection? FindSection(
        IReadOnlyList<DetectedSection> sections,
        int sectionNumber)
    {
        foreach (var s in sections)
        {
            if (s.SectionNumber == sectionNumber)
                return s;
        }

        return null;
    }

    private static bool IsPermitted(int sectionNumber)
    {
        foreach (var n in PermittedAdvertisingSections)
        {
            if (n == sectionNumber)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the first matching promotional marker found in <paramref name="sectionText"/>
    /// (after normalization), or <see langword="null"/> if none found.
    /// </summary>
    private static string? FindMarker(string sectionText)
    {
        // Normalize the section text once, then check each marker.
        var normalized = VecTextNormalizer.Normalize(sectionText);

        foreach (var marker in PromotionalMarkers)
        {
            // Markers are already upper-case, accent-stripped (defined as normalized literals).
            if (normalized.Contains(marker, StringComparison.Ordinal))
                return marker;
        }

        return null;
    }
}
