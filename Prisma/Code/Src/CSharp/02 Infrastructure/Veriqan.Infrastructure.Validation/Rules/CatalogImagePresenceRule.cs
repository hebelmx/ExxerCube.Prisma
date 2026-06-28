using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CLIENT-IMG-CATALOG (FR-12): Client/brand catalog-image presence rule — verifies that
/// every catalog image referenced in the bundle's <c>products[].cardImage</c>,
/// <c>products[].importantMessageImage</c>, and <c>sequentialImages[].image</c>
/// sections with a <c>perceptualHash</c> is visually present on at least one page of
/// the submitted statement PDF.  Anchored to Acuerdo §1 (SIPRES logo required;
/// product/card imagery optional per tenant brand catalog).
/// </summary>
/// <remarks>
/// <para>
/// <b>How hashes are compared:</b>
/// The extraction stage (<c>ExtractFullAsync</c>) renders each PDF page to a raster image
/// and stores a CoenM <see cref="CoenM.ImageHash.HashAlgorithms.PerceptualHash"/> value
/// (64-bit ulong) in <see cref="StatementModel.PagePerceptualHashes"/>, one entry per page
/// ordered by 1-based page number.
/// </para>
/// <para>
/// The rule parses each bundle <c>ImageRef.PerceptualHash</c> string as a 16-character
/// hex <c>ulong</c> and computes the Hamming distance between the bundle hash and every
/// page hash via <c>BitOperations.PopCount(bundleHash ^ pageHash)</c>.
/// A bundle image is considered <b>present</b> if any page's Hamming distance is at most
/// <see cref="DefaultHammingThreshold"/> bits (default ≤ 5).
/// </para>
/// <para>
/// <b>InsufficientData paths (abstain-safety — never RED on missing reference data):</b>
/// <list type="bullet">
///   <item>The bundle contains no <c>ImageRef</c> entries with a <c>perceptualHash</c>.</item>
///   <item><see cref="StatementModel"/> is null or <see cref="StatementModel.PagePerceptualHashes"/>
///         is empty (the rendering/hashing pass has not run).</item>
/// </list>
/// </para>
/// <para>
/// <b>Hamming threshold configurability:</b>
/// The threshold is a rule-level constant (<see cref="DefaultHammingThreshold"/> = 5 bits)
/// with a <c>// configurable</c> note.  A future story can expose this via
/// <c>TenantProfile</c> or <c>ValidationConstants</c> once a corpus is available to
/// calibrate the optimal value.
/// </para>
/// </remarks>
internal sealed class CatalogImagePresenceRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <summary>
    /// Maximum Hamming distance (inclusive) at which two 64-bit perceptual hashes are
    /// considered a visual match.  5 bits out of 64 = ≈92% similarity threshold.
    /// // configurable — expose via TenantProfile or ValidationConstants once corpus-calibrated.
    /// </summary>
    internal const int DefaultHammingThreshold = 5;

    /// <inheritdoc />
    public string CheckId => "CLIENT-IMG-CATALOG";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §1 (logo SIPRES) + tenant brand catalog";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.TenantTightenableOnly;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // Gate 1: StatementModel and its page hashes must be present.
        var model = ctx.StatementModel;
        if (model is null || model.PagePerceptualHashes.Count == 0)
            return InsufficientData(
                "StatementModel.PagePerceptualHashes is empty — the PDF rendering/hashing pass has not run.");

        // Gate 2: bundle must have at least one ImageRef with a perceptual hash.
        var allImageRefs = CollectImageRefs(ctx.Bundle);
        if (allImageRefs.Count == 0)
            return InsufficientData(
                "Bundle contains no ImageRef entries with a perceptualHash — no catalog images to verify.");

        // Compare each catalog image hash against all page hashes.
        var missingLabels = new List<string>();

        foreach (var (label, imageRef) in allImageRefs)
        {
            if (!TryParseHash(imageRef.PerceptualHash, out var referenceHash))
            {
                // Non-parseable hash — skip silently (treat as absent reference data, not a Fail).
                continue;
            }

            var found = false;
            foreach (var pageHash in model.PagePerceptualHashes)
            {
                var distance = BitOperations.PopCount(referenceHash ^ pageHash);
                if (distance <= DefaultHammingThreshold)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                missingLabels.Add(label);
        }

        if (missingLabels.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"All {allImageRefs.Count} catalog image(s) matched within Hamming ≤ {DefaultHammingThreshold} bits.",
                    toleranceApplied: null,
                    locator: FieldLocator.PageHint(1)));
        }

        var idList = string.Join(", ", missingLabels.Count <= 5 ? missingLabels : missingLabels.GetRange(0, 5));
        var suffix = missingLabels.Count > 5 ? $" … (+{missingLabels.Count - 5} more)" : string.Empty;

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: $"All catalog images present (Hamming ≤ {DefaultHammingThreshold} bits)",
                observed: $"{missingLabels.Count} catalog image(s) absent: {idList}{suffix}",
                toleranceApplied: null,
                locator: FieldLocator.PageHint(1)));
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Collects all <see cref="ImageRef"/> entries from the bundle that carry a
    /// <c>perceptualHash</c> string, together with a human-readable label for
    /// diagnostic messages.
    /// </summary>
    private static IReadOnlyList<(string Label, ImageRef Ref)> CollectImageRefs(VecReferenceBundle bundle)
    {
        var result = new List<(string, ImageRef)>();

        // Products: cardImage + importantMessageImage.
        if (bundle.Products is not null)
        {
            foreach (var product in bundle.Products)
            {
                if (product.CardImage is { PerceptualHash: not null } cardImg)
                    result.Add(($"{product.ProductId}/cardImage", cardImg));

                if (product.ImportantMessageImage is { PerceptualHash: not null } msgImg)
                    result.Add(($"{product.ProductId}/importantMessageImage", msgImg));
            }
        }

        // SequentialImages.
        if (bundle.SequentialImages is not null)
        {
            foreach (var seq in bundle.SequentialImages)
            {
                if (seq.Image is { PerceptualHash: not null } seqImg)
                    result.Add(($"seqImage[{seq.Order}]", seqImg));
            }
        }

        // Promotions.
        if (bundle.Promotions is not null)
        {
            foreach (var promo in bundle.Promotions)
            {
                if (promo.Image is { PerceptualHash: not null } promoImg)
                    result.Add(($"promotion/{promo.PromotionId}", promoImg));
            }
        }

        return result;
    }

    /// <summary>
    /// Attempts to parse a perceptual-hash string as a hex <see cref="ulong"/>.
    /// Accepts upper- and lower-case hex with or without a leading <c>0x</c> prefix.
    /// </summary>
    private static bool TryParseHash(string? raw, out ulong hash)
    {
        hash = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var span = raw.AsSpan().Trim();
        if (span.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            span = span[2..];

        return ulong.TryParse(span, System.Globalization.NumberStyles.HexNumber, null, out hash);
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}
