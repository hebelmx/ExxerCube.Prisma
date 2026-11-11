// <copyright file="OcrTextValidator.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Validators.Ocr;

/// <summary>
/// Describes an expected field/value pair that the OCR output is checked against.
/// </summary>
/// <param name="FieldName">A label identifying the field (used in finding descriptions).</param>
/// <param name="ExpectedSubstring">
/// A substring that must be present (case-insensitive) in the OCR text for the field to be
/// considered present.
/// </param>
public sealed record OcrExpectedField(string FieldName, string ExpectedSubstring);

/// <summary>
/// The subject supplied to <see cref="OcrTextValidator"/>.
/// Pairs the raw OCR text with the set of fields expected to appear in it.
/// </summary>
/// <param name="OcrText">The raw text produced by the OCR engine.</param>
/// <param name="ExpectedFields">
/// The fields (each with an expected substring) that must be present in <paramref name="OcrText"/>.
/// </param>
public sealed record OcrValidationSubject(
    string OcrText,
    IReadOnlyList<OcrExpectedField> ExpectedFields);

/// <summary>
/// Validates that OCR-extracted text contains every field specified in the ground-truth expectations.
/// The comparison is case-insensitive substring matching, reflecting the imprecision inherent in OCR
/// output from scanned documents.
/// </summary>
/// <remarks>
/// Conformance means every expected field substring was found in the OCR text.  A missing field is
/// reported as a <see cref="FindingSeverity.Major"/> finding.  An empty OCR text when fields are
/// expected is a <see cref="FindingSeverity.Critical"/> finding.
/// </remarks>
public sealed class OcrTextValidator : IDomainValidator<OcrValidationSubject>
{
    /// <inheritdoc/>
    public string ValidatorId => "OCR-TEXT";

    /// <inheritdoc/>
    public string Description =>
        "Observes whether OCR-extracted text contains every expected field substring. " +
        "Missing field substrings are reported as Major findings; empty OCR output when " +
        "fields are expected is a Critical finding.";

    /// <inheritdoc/>
    public Task<ValidationResult> ValidateAsync(
        OcrValidationSubject subject,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(NonConformant("OCR-TEXT-00", FindingSeverity.Critical,
                "Validation cancelled before it started.", Observed: null, Expected: null));
        }

        if (subject is null)
        {
            return Task.FromResult(NonConformant("OCR-TEXT-01", FindingSeverity.Critical,
                "Validation subject is null.", Observed: "(null)", Expected: "OcrValidationSubject"));
        }

        return Task.FromResult(ValidateInternal(subject));
    }

    private ValidationResult ValidateInternal(OcrValidationSubject subject)
    {
        var findings = new List<ValidationFinding>();

        var ocrText = subject.OcrText ?? string.Empty;
        var expectedFields = subject.ExpectedFields ?? Array.Empty<OcrExpectedField>();

        // ── 1. Empty text guard ─────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(ocrText) && expectedFields.Count > 0)
        {
            findings.Add(new ValidationFinding(
                "OCR-TEXT-02", FindingSeverity.Critical,
                "OCR text is empty but expected fields were specified.",
                Observed: "(empty)", Expected: $"{expectedFields.Count} field(s) present"));
            return new ValidationResult(ValidatorId, IsConformant: false, findings);
        }

        // ── 2. No expected fields — informational ───────────────────────────
        if (expectedFields.Count == 0)
        {
            findings.Add(new ValidationFinding(
                "OCR-TEXT-INFO", FindingSeverity.Info,
                "No expected fields were specified; nothing to check.",
                Observed: $"OCR text length: {ocrText.Length}", Expected: "At least one expected field"));
            return new ValidationResult(ValidatorId, IsConformant: true, findings);
        }

        // ── 3. Per-field presence check ─────────────────────────────────────
        foreach (var field in expectedFields)
        {
            if (string.IsNullOrEmpty(field.ExpectedSubstring))
            {
                findings.Add(new ValidationFinding(
                    $"OCR-TEXT-03-{field.FieldName}", FindingSeverity.Info,
                    $"Expected substring for field '{field.FieldName}' is empty; skipping check.",
                    Observed: "(empty)", Expected: "Non-empty substring"));
                continue;
            }

            var present = ocrText.Contains(
                field.ExpectedSubstring,
                StringComparison.OrdinalIgnoreCase);

            if (!present)
            {
                findings.Add(new ValidationFinding(
                    $"OCR-TEXT-04-{field.FieldName}", FindingSeverity.Major,
                    $"Expected field '{field.FieldName}' was not found in OCR output.",
                    Observed: "(absent)",
                    Expected: $"Substring: '{field.ExpectedSubstring}'"));
            }
        }

        var isConformant = !findings.Any(f =>
            f.Severity == FindingSeverity.Critical || f.Severity == FindingSeverity.Major);

        return new ValidationResult(ValidatorId, isConformant, findings);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ValidationResult NonConformant(
        string ruleId, FindingSeverity severity, string description,
        string? Observed, string? Expected) =>
        new(ValidatorId, IsConformant: false,
            [new ValidationFinding(ruleId, severity, description, Observed, Expected)]);
}
