// <copyright file="ExcelExportValidatorTests.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.IO;
using ClosedXML.Excel;
using ExxerCube.Prisma.QaHarness.Validators;
using ExxerCube.Prisma.QaHarness.Validators.Export;

namespace ExxerCube.Prisma.QaHarness.Tests.Validators;

/// <summary>
/// Fast unit tests for <see cref="ExcelExportValidator"/>.
/// Uses in-memory ClosedXML workbooks — no Docker or network access required.
/// </summary>
public sealed class ExcelExportValidatorTests
{
    private static readonly ExcelExportValidator Sut = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Creates a temp .xlsx file from the supplied workbook and returns the path.</summary>
    private static string SaveWorkbook(XLWorkbook wb)
    {
        var path = Path.Combine(Path.GetTempPath(), $"excel-validator-{Guid.NewGuid():N}.xlsx");
        wb.SaveAs(path);
        return path;
    }

    /// <summary>Builds a workbook with the correct 24 Datos-Carga-Oficio headers.</summary>
    private static XLWorkbook BuildConformantWorkbook()
    {
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("DatosCarga");
        for (var i = 0; i < ExcelExportValidator.ExpectedHeaders.Count; i++)
        {
            ws.Cell(1, i + 1).Value = ExcelExportValidator.ExpectedHeaders[i];
        }
        return wb;
    }

    // ── Conformant cases ─────────────────────────────────────────────────────

    /// <summary>A workbook with all 24 correct headers should be conformant with no Major/Critical findings.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_AllCorrectHeaders_IsConformant()
    {
        using var wb = BuildConformantWorkbook();
        var path = SaveWorkbook(wb);
        try
        {
            var result = await Sut.ValidateAsync(path, TestContext.Current.CancellationToken);

            result.ValidatorId.ShouldBe("EXCEL-EXPORT");
            result.IsConformant.ShouldBeTrue();
            result.Findings.ShouldNotContain(f =>
                f.Severity == FindingSeverity.Critical || f.Severity == FindingSeverity.Major);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── Non-conformant: missing file ─────────────────────────────────────────

    /// <summary>A missing file path should produce a Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_MissingFile_IsNotConformant_WithCriticalFinding()
    {
        var path = Path.Combine(Path.GetTempPath(), "does-not-exist.xlsx");

        var result = await Sut.ValidateAsync(path, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Critical);
    }

    // ── Non-conformant: wrong headers ────────────────────────────────────────

    /// <summary>A workbook with a wrong header at column 1 should produce a Major finding for that column.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_WrongHeader_IsNotConformant_WithMajorFinding()
    {
        using var wb = BuildConformantWorkbook();
        // Corrupt column 1
        wb.Worksheet(1).Cell(1, 1).Value = "WrongHeader";
        var path = SaveWorkbook(wb);
        try
        {
            var result = await Sut.ValidateAsync(path, TestContext.Current.CancellationToken);

            result.IsConformant.ShouldBeFalse();
            var col1Finding = result.Findings.FirstOrDefault(f => f.RuleId == "EXCEL-EXPORT-04-C01");
            col1Finding.ShouldNotBeNull();
            col1Finding!.Severity.ShouldBe(FindingSeverity.Major);
            col1Finding.Observed.ShouldBe("WrongHeader");
            col1Finding.Expected.ShouldBe(ExcelExportValidator.ExpectedHeaders[0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>A workbook with only 5 columns (too few) should produce Major findings for the missing headers.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_TooFewColumns_IsNotConformant_WithMajorFindings()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        // Write only 5 headers
        for (var i = 0; i < 5; i++)
        {
            ws.Cell(1, i + 1).Value = ExcelExportValidator.ExpectedHeaders[i];
        }
        var path = SaveWorkbook(wb);
        try
        {
            var result = await Sut.ValidateAsync(path, TestContext.Current.CancellationToken);

            result.IsConformant.ShouldBeFalse();
            // At minimum the column-count finding or per-column findings must be Major
            result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Major);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>A workbook with correct first 23 headers but wrong 24th should produce one Major finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_LastHeaderWrong_IsNotConformant_SingleMajorFinding()
    {
        using var wb = BuildConformantWorkbook();
        // Corrupt last column (24th)
        wb.Worksheet(1).Cell(1, 24).Value = "WrongLastHeader";
        var path = SaveWorkbook(wb);
        try
        {
            var result = await Sut.ValidateAsync(path, TestContext.Current.CancellationToken);

            result.IsConformant.ShouldBeFalse();
            var majorFindings = result.Findings.Where(f => f.Severity == FindingSeverity.Major).ToList();
            majorFindings.Count.ShouldBe(1);
            majorFindings[0].RuleId.ShouldBe("EXCEL-EXPORT-04-C24");
            majorFindings[0].Observed.ShouldBe("WrongLastHeader");
            majorFindings[0].Expected.ShouldBe(ExcelExportValidator.ExpectedHeaders[23]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── Non-conformant: empty path ────────────────────────────────────────────

    /// <summary>A null/empty path should produce a Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_EmptyPath_IsNotConformant_WithCriticalFinding()
    {
        var result = await Sut.ValidateAsync(string.Empty, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Critical);
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    /// <summary>A pre-cancelled token should yield IsConformant=false with a Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_CancelledToken_IsNotConformant()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await Sut.ValidateAsync("/any.xlsx", cts.Token);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Critical);
    }

    // ── Expected headers list ─────────────────────────────────────────────────

    /// <summary>The expected-headers list must contain exactly 24 entries.</summary>
    [Fact]
    [Trait("category", "fast")]
    public void ExpectedHeaders_HasExactly24Entries()
    {
        ExcelExportValidator.ExpectedHeaders.Count.ShouldBe(24);
    }
}
