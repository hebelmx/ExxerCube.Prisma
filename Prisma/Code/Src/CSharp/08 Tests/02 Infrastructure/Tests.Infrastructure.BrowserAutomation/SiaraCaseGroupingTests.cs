using System.Collections.Generic;

namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Pure unit tests for the <see cref="SiaraCaseGrouping"/> static helper (MVP-PATH 2.1).
/// No browser, no DI — just URL-parsing + grouping logic.
/// </summary>
public sealed class SiaraCaseGroupingTests
{
    // ---------------------------------------------------------------------------
    // GroupByCase — empty / single-file edge cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void GroupByCase_EmptyInput_ReturnsEmptyList()
    {
        var result = SiaraCaseGrouping.GroupByCase(new List<DownloadableFile>());

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    // ---------------------------------------------------------------------------
    // GroupByCase — three files of ONE case → ONE SiaraCase with 3 files
    // ---------------------------------------------------------------------------

    [Fact]
    public void GroupByCase_ThreeFilesOfSameCase_ReturnsOneCaseWithThreeFiles()
    {
        var files = new List<DownloadableFile>
        {
            new() { Url = "https://siara.local/document_store/CASE1/a.pdf",  FileName = "a",  Format = FileFormat.Pdf },
            new() { Url = "https://siara.local/document_store/CASE1/b.xml",  FileName = "b",  Format = FileFormat.Xml },
            new() { Url = "https://siara.local/document_store/CASE1/c.docx", FileName = "c",  Format = FileFormat.Docx },
        };

        var cases = SiaraCaseGrouping.GroupByCase(files);

        cases.Count.ShouldBe(1);
        var siaraCase = cases[0];
        siaraCase.CaseId.ShouldBe("CASE1");
        siaraCase.Files.Count.ShouldBe(3);
    }

    [Fact]
    public void GroupByCase_ThreeFilesOfSameCase_PreservesFormats()
    {
        var files = new List<DownloadableFile>
        {
            new() { Url = "https://siara.local/document_store/CASE1/a.pdf",  FileName = "a",  Format = FileFormat.Pdf },
            new() { Url = "https://siara.local/document_store/CASE1/b.xml",  FileName = "b",  Format = FileFormat.Xml },
            new() { Url = "https://siara.local/document_store/CASE1/c.docx", FileName = "c",  Format = FileFormat.Docx },
        };

        var cases = SiaraCaseGrouping.GroupByCase(files);

        var resultFiles = cases[0].Files;
        resultFiles.ShouldContain(f => f.Format == FileFormat.Pdf);
        resultFiles.ShouldContain(f => f.Format == FileFormat.Xml);
        resultFiles.ShouldContain(f => f.Format == FileFormat.Docx);
    }

    // ---------------------------------------------------------------------------
    // GroupByCase — files across TWO cases → TWO SiaraCase entries
    // ---------------------------------------------------------------------------

    [Fact]
    public void GroupByCase_FilesAcrossTwoCases_ReturnsTwoCases()
    {
        var files = new List<DownloadableFile>
        {
            new() { Url = "https://siara.local/document_store/CASE_A/doc1.pdf",  FileName = "doc1", Format = FileFormat.Pdf },
            new() { Url = "https://siara.local/document_store/CASE_A/doc1.xml",  FileName = "doc1", Format = FileFormat.Xml },
            new() { Url = "https://siara.local/document_store/CASE_B/report.pdf", FileName = "report", Format = FileFormat.Pdf },
        };

        var cases = SiaraCaseGrouping.GroupByCase(files);

        cases.Count.ShouldBe(2);
        cases.ShouldContain(c => c.CaseId == "CASE_A" && c.Files.Count == 2);
        cases.ShouldContain(c => c.CaseId == "CASE_B" && c.Files.Count == 1);
    }

    [Fact]
    public void GroupByCase_FilesAcrossTwoCases_PreservesInputOrder()
    {
        // CASE_A appears first in the input → should be first in the result.
        var files = new List<DownloadableFile>
        {
            new() { Url = "https://siara.local/document_store/CASE_A/doc1.pdf", FileName = "doc1", Format = FileFormat.Pdf },
            new() { Url = "https://siara.local/document_store/CASE_B/report.pdf", FileName = "report", Format = FileFormat.Pdf },
        };

        var cases = SiaraCaseGrouping.GroupByCase(files);

        cases[0].CaseId.ShouldBe("CASE_A");
        cases[1].CaseId.ShouldBe("CASE_B");
    }

    // ---------------------------------------------------------------------------
    // GroupByCase — case-id comparison is OrdinalIgnoreCase
    // ---------------------------------------------------------------------------

    [Fact]
    public void GroupByCase_CaseIdComparison_IsOrdinalIgnoreCase()
    {
        var files = new List<DownloadableFile>
        {
            new() { Url = "https://siara.local/document_store/case1/a.pdf",  FileName = "a", Format = FileFormat.Pdf },
            new() { Url = "https://siara.local/document_store/CASE1/b.xml",  FileName = "b", Format = FileFormat.Xml },
        };

        var cases = SiaraCaseGrouping.GroupByCase(files);

        // Despite different casing in the URL, they map to the same case.
        cases.Count.ShouldBe(1);
        cases[0].Files.Count.ShouldBe(2);
    }

    // ---------------------------------------------------------------------------
    // GroupByCase — malformed / short-path URL → fallback to FileName
    // ---------------------------------------------------------------------------

    [Fact]
    public void GroupByCase_MalformedUrl_FallsBackToFileName()
    {
        var file = new DownloadableFile
        {
            Url = "not-a-url-with-segments",
            FileName = "my-document",
            Format = FileFormat.Pdf,
        };

        var cases = SiaraCaseGrouping.GroupByCase(new List<DownloadableFile> { file });

        cases.Count.ShouldBe(1);
        cases[0].CaseId.ShouldBe("my-document");
        cases[0].Files.Count.ShouldBe(1);
        cases[0].Files[0].Format.ShouldBe(FileFormat.Pdf);
    }

    [Fact]
    public void GroupByCase_SingleSegmentUrl_FallsBackToFileName()
    {
        // A URL with only one path segment (no parent = no caseId segment).
        var file = new DownloadableFile
        {
            Url = "https://siara.local/only-one-segment",
            FileName = "only-one-segment",
            Format = FileFormat.Xml,
        };

        var cases = SiaraCaseGrouping.GroupByCase(new List<DownloadableFile> { file });

        cases.Count.ShouldBe(1);
        // With a single usable segment the fallback kicks in → FileName is used as the case key.
        cases[0].CaseId.ShouldBe("only-one-segment");
    }

    [Fact]
    public void GroupByCase_BlankUrlAndBlankFileName_FallsBackToSentinel()
    {
        var file = new DownloadableFile
        {
            Url = "   ",
            FileName = "  ",
            Format = FileFormat.Pdf,
        };

        var cases = SiaraCaseGrouping.GroupByCase(new List<DownloadableFile> { file });

        cases.Count.ShouldBe(1);
        // Should not throw; case id is the "__unknown__" sentinel.
        cases[0].CaseId.ShouldBe("__unknown__");
    }

    // ---------------------------------------------------------------------------
    // GroupByCase — relative URL (no scheme) still extracts caseId
    // ---------------------------------------------------------------------------

    [Fact]
    public void GroupByCase_RelativeUrl_ExtractsCaseIdFromPathSegments()
    {
        var files = new List<DownloadableFile>
        {
            new() { Url = "/document_store/RELCASE/file.pdf", FileName = "file", Format = FileFormat.Pdf },
        };

        var cases = SiaraCaseGrouping.GroupByCase(files);

        cases.Count.ShouldBe(1);
        cases[0].CaseId.ShouldBe("RELCASE");
    }
}
