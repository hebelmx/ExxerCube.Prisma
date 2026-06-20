// <copyright file="OcrTextValidatorTests.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using ExxerCube.Prisma.QaHarness.Validators;
using ExxerCube.Prisma.QaHarness.Validators.Ocr;

namespace ExxerCube.Prisma.QaHarness.Tests.Validators;

/// <summary>
/// Fast unit tests for <see cref="OcrTextValidator"/>.
/// Uses planted OCR text inline — no Docker, file I/O, or network access required.
/// </summary>
public sealed class OcrTextValidatorTests
{
    private static readonly OcrTextValidator Sut = new();

    // ── Conformant: all fields present ──────────────────────────────────────

    /// <summary>When all expected substrings appear in the OCR text, the result is conformant.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_AllFieldsPresent_IsConformant()
    {
        var subject = new OcrValidationSubject(
            OcrText: "EXPEDIENTE: A/AS1-2505-088637-PHM  OFICIO: 214-1-18714972/2025  CNBV",
            ExpectedFields:
            [
                new OcrExpectedField("NumeroExpediente", "A/AS1-2505-088637-PHM"),
                new OcrExpectedField("NumeroOficio", "214-1-18714972/2025"),
                new OcrExpectedField("AutoridadNombre", "CNBV"),
            ]);

        var result = await Sut.ValidateAsync(subject, TestContext.Current.CancellationToken);

        result.ValidatorId.ShouldBe("OCR-TEXT");
        result.IsConformant.ShouldBeTrue();
        result.Findings.ShouldNotContain(f =>
            f.Severity == FindingSeverity.Critical || f.Severity == FindingSeverity.Major);
    }

    /// <summary>Matching should be case-insensitive.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_CaseInsensitiveMatch_IsConformant()
    {
        var subject = new OcrValidationSubject(
            OcrText: "expediente: a/as1-2505-088637-phm",
            ExpectedFields: [new OcrExpectedField("Expediente", "A/AS1-2505-088637-PHM")]);

        var result = await Sut.ValidateAsync(subject, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeTrue();
    }

    // ── Non-conformant: missing field ────────────────────────────────────────

    /// <summary>A missing field produces a Major finding with the expected substring in Expected.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_MissingField_IsNotConformant_WithMajorFinding()
    {
        var subject = new OcrValidationSubject(
            OcrText: "CNBV oficio presente pero no el expediente",
            ExpectedFields:
            [
                new OcrExpectedField("NumeroExpediente", "A/AS1-2505-088637-PHM"),
                new OcrExpectedField("AutoridadNombre", "CNBV"),
            ]);

        var result = await Sut.ValidateAsync(subject, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        var missing = result.Findings.FirstOrDefault(f =>
            f.RuleId.Contains("NumeroExpediente") && f.Severity == FindingSeverity.Major);
        missing.ShouldNotBeNull();
        missing!.Observed.ShouldBe("(absent)");
        missing.Expected!.ShouldContain("A/AS1-2505-088637-PHM");
    }

    // ── Non-conformant: empty OCR text with expected fields ─────────────────

    /// <summary>Empty OCR text with expected fields produces a Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_EmptyOcrText_WithExpectedFields_IsNotConformant_Critical()
    {
        var subject = new OcrValidationSubject(
            OcrText: string.Empty,
            ExpectedFields: [new OcrExpectedField("Expediente", "A/AS1")]);

        var result = await Sut.ValidateAsync(subject, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Critical);
    }

    // ── Conformant: no expected fields ───────────────────────────────────────

    /// <summary>When no expected fields are specified, result is conformant with an Info finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_NoExpectedFields_IsConformant_WithInfoFinding()
    {
        var subject = new OcrValidationSubject(
            OcrText: "Any text",
            ExpectedFields: []);

        var result = await Sut.ValidateAsync(subject, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeTrue();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Info);
    }

    // ── Planted text from PRP1_Degraded_Q2 fixture ───────────────────────────
    // The Q2 .psm fixtures contain partial OCR output extracted from PRP1 Degraded images.
    // We use a substring known to appear in PRP1 documents (fiscal-authority names + expediente
    // patterns) as the ground-truth expectation.

    /// <summary>
    /// Simulates validating OCR output against ground-truth fields from the PRP1 Degraded Q2 fixture.
    /// Uses a text fragment representative of what Tesseract extracts from those images.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_PRP1DegradedQ2GroundTruth_IsConformant_WhenFieldsPresent()
    {
        // Planted ground-truth text representative of PRP1 Degraded Q2 OCR output.
        // The actual Q2 images contain CNBV authority references and expediente patterns.
        const string simulatedOcrText =
            "COMISION NACIONAL BANCARIA Y DE VALORES\n" +
            "EXPEDIENTE: A/AS1-2505-088637-PHM\n" +
            "OFICIO: 214-1-18714972/2025\n" +
            "REQUERIMIENTO: BLOQUEO DE CUENTAS";

        var subject = new OcrValidationSubject(
            OcrText: simulatedOcrText,
            ExpectedFields:
            [
                new OcrExpectedField("AutoridadNombre", "COMISION NACIONAL BANCARIA"),
                new OcrExpectedField("NumeroExpediente", "A/AS1-2505-088637-PHM"),
                new OcrExpectedField("NumeroOficio", "214-1-18714972/2025"),
            ]);

        var result = await Sut.ValidateAsync(subject, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeTrue();
        result.Findings.ShouldNotContain(f =>
            f.Severity == FindingSeverity.Critical || f.Severity == FindingSeverity.Major);
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    /// <summary>A pre-cancelled token yields IsConformant=false with Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_CancelledToken_IsNotConformant()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var subject = new OcrValidationSubject("any", []);
        var result = await Sut.ValidateAsync(subject, cts.Token);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Critical);
    }

    // ── Null subject guard ────────────────────────────────────────────────────

    /// <summary>A null subject should produce a Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_NullSubject_IsNotConformant_WithCriticalFinding()
    {
        var result = await Sut.ValidateAsync(null!, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Critical);
    }

    // ── Findings carry RuleId / Severity / Observed / Expected ───────────────

    /// <summary>Every Major finding must carry non-null Observed and Expected fields.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_MissingField_FindingCarriesObservedAndExpected()
    {
        var subject = new OcrValidationSubject(
            OcrText: "some unrelated text",
            ExpectedFields: [new OcrExpectedField("TestField", "ExpectedValue123")]);

        var result = await Sut.ValidateAsync(subject, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        var finding = result.Findings.First(f => f.Severity == FindingSeverity.Major);
        finding.RuleId.ShouldNotBeNullOrWhiteSpace();
        finding.Observed.ShouldBe("(absent)");
        finding.Expected!.ShouldContain("ExpectedValue123");
    }
}
