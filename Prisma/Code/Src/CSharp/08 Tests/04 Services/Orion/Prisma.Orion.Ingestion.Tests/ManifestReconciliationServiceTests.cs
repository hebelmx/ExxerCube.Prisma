using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Services.Manifest;

namespace ExxerCube.Prisma.Orion.Ingestion.Tests;

/// <summary>
/// Unit tests for <see cref="ManifestReconciliationService"/> — the pure domain reconciler (Item B #8 + F #12).
/// Covers every reconciliation bucket: Complete / Partial / Missing / Extra / empty-expected / empty-actual,
/// plus per-file format gaps and the flat file list (F).
/// </summary>
public sealed class ManifestReconciliationServiceTests
{
    private static IManifestReconciler CreateSut() => new ManifestReconciliationService();

    // ------------------------------------------------------------------------------------------------
    // Helper factories
    // ------------------------------------------------------------------------------------------------

    private static ExpectedManifest MakeExpected(params (string caseId, FileFormat[] formats)[] oficios)
    {
        var list = oficios
            .Select(o => new ExpectedOficio(o.caseId, o.formats))
            .ToList();
        return new ExpectedManifest { Oficios = list };
    }

    private static DiscoveredOficio MakeDiscovered(string caseId, bool isComplete, params FileFormat[] formats)
    {
        var files = formats
            .Select(f => new DownloadedFileEntry(
                FileName: $"{caseId}.{f.Name.ToLowerInvariant()}",
                Extension: f.Name.ToLowerInvariant(),
                Format: f))
            .ToList();
        return new DiscoveredOficio(caseId, files, isComplete);
    }

    // ================================================================================================
    // Bucket: Complete
    // ================================================================================================

    [Fact]
    [Trait("Category", "Unit")]
    public void Reconcile_WhenAllExpectedFormatsDownloaded_ReturnsComplete()
    {
        var sut = CreateSut();
        var expected = MakeExpected(("case-1", [FileFormat.Pdf, FileFormat.Xml, FileFormat.Docx]));
        var actual = new[]
        {
            MakeDiscovered("case-1", isComplete: true, FileFormat.Pdf, FileFormat.Xml, FileFormat.Docx),
        };

        var report = sut.Reconcile(expected, actual);

        report.Complete.Count.ShouldBe(1);
        report.Complete[0].CaseId.ShouldBe("case-1");
        report.Partial.ShouldBeEmpty();
        report.Missing.ShouldBeEmpty();
        report.Extra.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Reconcile_CompleteOficio_HasNoMissingOrExtraFormats()
    {
        var sut = CreateSut();
        var expected = MakeExpected(("case-A", [FileFormat.Pdf, FileFormat.Xml]));
        var actual = new[] { MakeDiscovered("case-A", isComplete: true, FileFormat.Pdf, FileFormat.Xml) };

        var report = sut.Reconcile(expected, actual);

        report.Complete[0].MissingFormats.ShouldBeEmpty();
        report.Complete[0].ExtraFormats.ShouldBeEmpty();
    }

    // ================================================================================================
    // Bucket: Partial (missing ≥1 expected format)
    // ================================================================================================

    [Fact]
    [Trait("Category", "Unit")]
    public void Reconcile_WhenOneExpectedFormatMissing_ReturnsPartial()
    {
        var sut = CreateSut();
        var expected = MakeExpected(("case-1", [FileFormat.Pdf, FileFormat.Xml, FileFormat.Docx]));
        var actual = new[]
        {
            // Only PDF and XML downloaded — DOCX is missing.
            MakeDiscovered("case-1", isComplete: false, FileFormat.Pdf, FileFormat.Xml),
        };

        var report = sut.Reconcile(expected, actual);

        report.Partial.Count.ShouldBe(1);
        report.Partial[0].CaseId.ShouldBe("case-1");
        report.Partial[0].MissingFormats.ShouldContain(FileFormat.Docx);
        report.Partial[0].MissingFormats.Count.ShouldBe(1);
        report.Complete.ShouldBeEmpty();
        report.Missing.ShouldBeEmpty();
        report.Extra.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Reconcile_WhenTwoExpectedFormatsMissing_PartialListsBoth()
    {
        var sut = CreateSut();
        var expected = MakeExpected(("case-X", [FileFormat.Pdf, FileFormat.Xml, FileFormat.Docx]));
        var actual = new[]
        {
            // Only PDF downloaded — both XML and DOCX are missing.
            MakeDiscovered("case-X", isComplete: false, FileFormat.Pdf),
        };

        var report = sut.Reconcile(expected, actual);

        report.Partial.Count.ShouldBe(1);
        report.Partial[0].MissingFormats.Count.ShouldBe(2);
        report.Partial[0].MissingFormats.ShouldContain(FileFormat.Xml);
        report.Partial[0].MissingFormats.ShouldContain(FileFormat.Docx);
    }

    // ================================================================================================
    // Bucket: Missing (expected oficio not discovered at all)
    // ================================================================================================

    [Fact]
    [Trait("Category", "Unit")]
    public void Reconcile_WhenExpectedOfficioNotDiscovered_ReturnsMissing()
    {
        var sut = CreateSut();
        var expected = MakeExpected(
            ("case-present", [FileFormat.Pdf]),
            ("case-absent", [FileFormat.Pdf, FileFormat.Xml]));
        var actual = new[]
        {
            MakeDiscovered("case-present", isComplete: true, FileFormat.Pdf),
        };

        var report = sut.Reconcile(expected, actual);

        report.Missing.Count.ShouldBe(1);
        report.Missing[0].CaseId.ShouldBe("case-absent");
        report.Complete.Count.ShouldBe(1);
        report.Extra.ShouldBeEmpty();
        report.Partial.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Reconcile_WhenNoCasesDiscovered_AllExpectedAreMissing()
    {
        var sut = CreateSut();
        var expected = MakeExpected(
            ("case-1", [FileFormat.Pdf]),
            ("case-2", [FileFormat.Xml]));

        var report = sut.Reconcile(expected, actual: []);

        report.Missing.Count.ShouldBe(2);
        report.Missing.Select(e => e.CaseId).ShouldContain("case-1");
        report.Missing.Select(e => e.CaseId).ShouldContain("case-2");
        report.Complete.ShouldBeEmpty();
        report.Partial.ShouldBeEmpty();
        report.Extra.ShouldBeEmpty();
    }

    // ================================================================================================
    // Bucket: Extra / sobra (discovered but not in expected manifest)
    // ================================================================================================

    [Fact]
    [Trait("Category", "Unit")]
    public void Reconcile_WhenDiscoveredCaseNotInExpected_ReturnsExtra()
    {
        var sut = CreateSut();
        var expected = MakeExpected(("case-known", [FileFormat.Pdf]));
        var actual = new[]
        {
            MakeDiscovered("case-known", isComplete: true, FileFormat.Pdf),
            MakeDiscovered("case-unknown", isComplete: true, FileFormat.Pdf),  // extra / sobra
        };

        var report = sut.Reconcile(expected, actual);

        report.Extra.Count.ShouldBe(1);
        report.Extra[0].CaseId.ShouldBe("case-unknown");
        report.Complete.Count.ShouldBe(1);
        report.Missing.ShouldBeEmpty();
        report.Partial.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Reconcile_WhenAllDiscoveredAreExtra_AllLandInExtraBucket()
    {
        var sut = CreateSut();
        // Expected manifest is empty — every discovered case is a sobra.
        var expected = ExpectedManifest.Empty;
        var actual = new[]
        {
            MakeDiscovered("case-a", isComplete: true, FileFormat.Pdf),
            MakeDiscovered("case-b", isComplete: true, FileFormat.Xml),
        };

        var report = sut.Reconcile(expected, actual);

        report.Extra.Count.ShouldBe(2);
        report.Complete.ShouldBeEmpty();
        report.Partial.ShouldBeEmpty();
        report.Missing.ShouldBeEmpty();
    }

    // ================================================================================================
    // Extra files within an otherwise-expected oficio
    // ================================================================================================

    [Fact]
    [Trait("Category", "Unit")]
    public void Reconcile_WhenCaseHasExtraFile_ExtraFormatsPopulated()
    {
        var sut = CreateSut();
        // Expected: only PDF; discovered: PDF + XML (XML is extra).
        var expected = MakeExpected(("case-1", [FileFormat.Pdf]));
        var actual = new[]
        {
            MakeDiscovered("case-1", isComplete: true, FileFormat.Pdf, FileFormat.Xml),
        };

        var report = sut.Reconcile(expected, actual);

        // Complete because all EXPECTED formats are present.
        report.Complete.Count.ShouldBe(1);
        report.Complete[0].ExtraFormats.ShouldContain(FileFormat.Xml);
        report.Complete[0].MissingFormats.ShouldBeEmpty();
    }

    // ================================================================================================
    // Edge: empty expected manifest
    // ================================================================================================

    [Fact]
    [Trait("Category", "Unit")]
    public void Reconcile_EmptyExpectedManifest_AllDiscoveredAreExtra()
    {
        var sut = CreateSut();
        var actual = new[]
        {
            MakeDiscovered("case-1", isComplete: true, FileFormat.Pdf),
        };

        var report = sut.Reconcile(ExpectedManifest.Empty, actual);

        report.Extra.Count.ShouldBe(1);
        report.Complete.ShouldBeEmpty();
        report.Partial.ShouldBeEmpty();
        report.Missing.ShouldBeEmpty();
    }

    // ================================================================================================
    // Edge: empty actual (no cases discovered)
    // ================================================================================================

    [Fact]
    [Trait("Category", "Unit")]
    public void Reconcile_EmptyActual_AllExpectedAreMissing()
    {
        var sut = CreateSut();
        var expected = MakeExpected(("case-1", [FileFormat.Pdf, FileFormat.Xml]));

        var report = sut.Reconcile(expected, actual: []);

        report.Missing.Count.ShouldBe(1);
        report.Complete.ShouldBeEmpty();
        report.Partial.ShouldBeEmpty();
        report.Extra.ShouldBeEmpty();
        report.DownloadedFiles.ShouldBeEmpty();
    }

    // ================================================================================================
    // Edge: both expected and actual are empty
    // ================================================================================================

    [Fact]
    [Trait("Category", "Unit")]
    public void Reconcile_BothEmpty_ProducesEmptyReport()
    {
        var sut = CreateSut();
        var report = sut.Reconcile(ExpectedManifest.Empty, actual: []);

        report.Complete.ShouldBeEmpty();
        report.Partial.ShouldBeEmpty();
        report.Missing.ShouldBeEmpty();
        report.Extra.ShouldBeEmpty();
        report.DownloadedFiles.ShouldBeEmpty();
    }

    // ================================================================================================
    // Item F: flat per-cycle downloaded-file list
    // ================================================================================================

    [Fact]
    [Trait("Category", "Unit")]
    public void Reconcile_DownloadedFilesContainsEveryFile_AcrossAllCases()
    {
        var sut = CreateSut();
        var expected = MakeExpected(
            ("case-A", [FileFormat.Pdf, FileFormat.Xml]),
            ("case-B", [FileFormat.Pdf]));
        var actual = new[]
        {
            MakeDiscovered("case-A", isComplete: true, FileFormat.Pdf, FileFormat.Xml),
            MakeDiscovered("case-B", isComplete: false, FileFormat.Pdf),
        };

        var report = sut.Reconcile(expected, actual);

        report.DownloadedFiles.Count.ShouldBe(3);
        report.DownloadedFiles.Select(f => f.CaseId).ShouldContain("case-A");
        report.DownloadedFiles.Select(f => f.CaseId).ShouldContain("case-B");
        report.DownloadedFiles.Select(f => f.Format).ShouldContain(FileFormat.Pdf);
        report.DownloadedFiles.Select(f => f.Format).ShouldContain(FileFormat.Xml);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Reconcile_DownloadedFileSummary_CopiesIsCompleteFromCase()
    {
        var sut = CreateSut();
        var expected = MakeExpected(
            ("complete-case", [FileFormat.Pdf]),
            ("partial-case", [FileFormat.Pdf, FileFormat.Xml]));
        var actual = new[]
        {
            MakeDiscovered("complete-case", isComplete: true, FileFormat.Pdf),
            MakeDiscovered("partial-case", isComplete: false, FileFormat.Pdf),
        };

        var report = sut.Reconcile(expected, actual);

        var completeFile = report.DownloadedFiles.First(f => f.CaseId == "complete-case");
        completeFile.IsComplete.ShouldBeTrue();

        var partialFile = report.DownloadedFiles.First(f => f.CaseId == "partial-case");
        partialFile.IsComplete.ShouldBeFalse();
    }

    // ================================================================================================
    // Mixed scenario: Complete + Partial + Missing + Extra simultaneously
    // ================================================================================================

    [Fact]
    [Trait("Category", "Unit")]
    public void Reconcile_MixedScenario_AllBucketsPopulatedCorrectly()
    {
        var sut = CreateSut();
        var expected = MakeExpected(
            ("complete", [FileFormat.Pdf, FileFormat.Xml]),
            ("partial", [FileFormat.Pdf, FileFormat.Xml, FileFormat.Docx]),
            ("missing", [FileFormat.Pdf]));
        var actual = new[]
        {
            MakeDiscovered("complete", isComplete: true, FileFormat.Pdf, FileFormat.Xml),
            MakeDiscovered("partial", isComplete: false, FileFormat.Pdf),          // DOCX + XML missing
            MakeDiscovered("extra-sobra", isComplete: true, FileFormat.Pdf),       // not in expected
        };

        var report = sut.Reconcile(expected, actual);

        report.Complete.Count.ShouldBe(1);
        report.Complete[0].CaseId.ShouldBe("complete");

        report.Partial.Count.ShouldBe(1);
        report.Partial[0].CaseId.ShouldBe("partial");
        report.Partial[0].MissingFormats.Count.ShouldBe(2);

        report.Missing.Count.ShouldBe(1);
        report.Missing[0].CaseId.ShouldBe("missing");

        report.Extra.Count.ShouldBe(1);
        report.Extra[0].CaseId.ShouldBe("extra-sobra");

        // F: 2 (complete) + 1 (partial-pdf) + 1 (extra) = 4 files total.
        report.DownloadedFiles.Count.ShouldBe(4);
    }
}
