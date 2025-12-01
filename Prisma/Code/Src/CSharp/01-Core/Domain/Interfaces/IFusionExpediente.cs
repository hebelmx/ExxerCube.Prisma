// <copyright file="IFusionExpediente.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Interfaces;

using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Service interface for fusing Expediente data from multiple unreliable sources (XML, PDF, DOCX).
/// Uses dynamic source reliability weighting based on OCR confidence, image quality, and extraction success metrics.
/// </summary>
/// <remarks>
/// <para><strong>Problem Statement:</strong></para>
/// <para>
/// The CNBV Prisma system receives Expediente data from 3 sources:
/// 1. XML - Manually filled by bank staff (prone to typos, catalog errors)
/// 2. PDF - CNBV-generated with OCR (official but OCR quality varies)
/// 3. DOCX - Authority-generated with OCR (variable quality by issuing authority)
/// </para>
/// <para>
/// All 3 sources are unreliable and frequently disagree. The fusion algorithm reconciles these
/// sources using a multi-phase approach:
/// </para>
/// <list type="number">
/// <item>Pre-processing: Sanitize text, remove HTML entities, trim whitespace</item>
/// <item>Pattern validation: Validate RFC, CURP, dates, expediente numbers against regex</item>
/// <item>Catalog validation: Match AreaDescripcion, AutoridadNombre against CNBV catalogs</item>
/// <item>Field-level fusion: Exact match → Fuzzy match → Weighted voting → Conflict flagging</item>
/// <item>Confidence scoring: Calculate per-field confidence (0.0-1.0)</item>
/// <item>Overall confidence: Weighted combination (70% required fields, 30% optional fields)</item>
/// <item>Decision: AutoProcess (>0.85) | ReviewRecommended (0.70-0.85) | ManualReviewRequired (&lt;0.70)</item>
/// </list>
/// <para><strong>Legal Compliance:</strong></para>
/// <para>
/// R29 A-2911 regulation requires 42 mandatory fields for monthly CNBV reporting with NO NULLS PERMITTED.
/// Any Expediente missing required fields MUST be flagged for manual review.
/// </para>
/// <para><strong>Optimization:</strong></para>
/// <para>
/// Fusion coefficients (source reliability weights, thresholds, boosts) are optimized using
/// Genetic Algorithm + Polynomial Regression methodology (adapted from OCR filter optimization).
/// Target metrics: >95% field accuracy, >90% Expediente accuracy, &lt;2% false negatives.
/// </para>
/// </remarks>
public interface IFusionExpediente
{
    /// <summary>
    /// Fuses data from up to 3 sources (XML, PDF, DOCX) into a single reconciled Expediente.
    /// Uses dynamic source reliability weighting based on OCR confidence, image quality,
    /// and extraction success metrics.
    /// </summary>
    /// <param name="xmlExpediente">Expediente extracted from manually-filled XML. May be null.</param>
    /// <param name="pdfExpediente">Expediente extracted from CNBV-generated PDF via OCR. May be null.</param>
    /// <param name="docxExpediente">Expediente extracted from authority-generated DOCX via OCR. May be null.</param>
    /// <param name="xmlMetadata">Extraction metadata for XML source (pattern violations, catalog validations).</param>
    /// <param name="pdfMetadata">Extraction metadata for PDF source (OCR confidence, image quality, extraction success).</param>
    /// <param name="docxMetadata">Extraction metadata for DOCX source (OCR confidence, image quality, extraction success).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// FusionResult containing:
    /// - FusedExpediente with reconciled data
    /// - OverallConfidence (0.0-1.0)
    /// - NextAction (AutoProcess | ReviewRecommended | ManualReviewRequired)
    /// - Field-level fusion results with decision rationale
    /// - Source reliability scores
    /// - List of conflicting and missing required fields
    /// </returns>
    /// <remarks>
    /// At least one source must be non-null. If all sources are null, returns failure result.
    /// If only one source is provided, confidence is based solely on that source's metadata quality.
    /// </remarks>
    Task<Result<FusionResult>> FuseAsync(
        Expediente? xmlExpediente,
        Expediente? pdfExpediente,
        Expediente? docxExpediente,
        ExtractionMetadata xmlMetadata,
        ExtractionMetadata pdfMetadata,
        ExtractionMetadata docxMetadata,
        CancellationToken cancellationToken);

    /// <summary>
    /// Fuses a single field from multiple candidate values using the fusion algorithm.
    /// </summary>
    /// <param name="fieldName">The name of the field being fused (e.g., "NumeroExpediente", "RFC").</param>
    /// <param name="candidates">List of candidate values from different sources with metadata.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// FieldFusionResult containing:
    /// - Selected value
    /// - Confidence score (0.0-1.0)
    /// - FusionDecision (AllAgree | FuzzyAgreement | WeightedVoting | Conflict | etc.)
    /// - List of contributing sources
    /// - Winning source (for weighted voting)
    /// - Conflict details (if applicable)
    /// - Manual review flags
    /// </returns>
    /// <remarks>
    /// Fusion logic:
    /// 1. Remove null/invalid candidates
    /// 2. Check for exact agreement → Return with high confidence (0.85-0.95)
    /// 3. Try fuzzy matching (for name fields) → Return if similarity > 0.85
    /// 4. Weighted voting using source reliability + pattern match + catalog validation
    /// 5. If winner margin &lt; 0.15 → Flag as Conflict or BestEffort
    /// 6. Critical fields with conflicts → Mandatory manual review
    /// </remarks>
    Task<Result<FieldFusionResult>> FuseFieldAsync(
        string fieldName,
        List<FieldCandidate> candidates,
        CancellationToken cancellationToken);
}
