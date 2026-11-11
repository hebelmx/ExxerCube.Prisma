using System.IO;
using System.Text;
using System.Xml.Linq;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExxerCube.Prisma.Tests.Infrastructure.Export.Adaptive;

/// <summary>
/// Mutation-killing tests for <see cref="AdaptiveExporter"/> driven by REAL collaborators
/// (in-memory repository + real <see cref="TemplateFieldMapper"/>). Reads the generated
/// artifacts back (ClosedXML / XDocument / OpenXml) to pin export-generation mutants
/// (ordering, column/element/paragraph placement, root names, format strings) and asserts
/// exact guard/propagated error messages.
/// </summary>
public sealed class AdaptiveExporterMutationTests : IDisposable
{
    private readonly TemplateDbContext _dbContext;
    private readonly ITemplateRepository _repository;
    private readonly AdaptiveExporter _exporter;

    public AdaptiveExporterMutationTests()
    {
        var options = new DbContextOptionsBuilder<TemplateDbContext>()
            .UseInMemoryDatabase($"AdaptiveExporterMut_{Guid.NewGuid()}")
            .Options;
        _dbContext = new TemplateDbContext(options);
        _repository = new TemplateRepository(_dbContext, NullLogger<TemplateRepository>.Instance);
        var mapper = new TemplateFieldMapper(NullLogger<TemplateFieldMapper>.Instance);
        _exporter = new AdaptiveExporter(_repository, mapper, NullLogger<AdaptiveExporter>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    /// <summary>
    /// Two fields whose DisplayOrder is the REVERSE of their declaration, so ascending
    /// ordering is observable: "Second" (order 1) precedes "First" (order 2).
    /// </summary>
    private async Task<TemplateDefinition> SeedTwoFieldTemplateAsync(string templateType)
    {
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = templateType,
            Version = "1.0.0",
            Name = "T",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test",
            FieldMappings =
            {
                new FieldMapping("First", "ColA", isRequired: false) { DisplayOrder = 2 },
                new FieldMapping("Second", "ColB", isRequired: false) { DisplayOrder = 1 }
            }
        };
        await _repository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);
        return template;
    }

    // ── Excel generation round-trip ───────────────────────────────────────────

    [Fact]
    public async Task ExportAsync_Excel_WritesHeadersAndDataInDisplayOrder()
    {
        await SeedTwoFieldTemplateAsync("Excel");
        var source = new { First = "v1", Second = "v2" };

        var result = await _exporter.ExportAsync(source, "Excel", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        using var wb = new XLWorkbook(new MemoryStream(result.Value!));
        var ws = wb.Worksheet(1);
        // Ascending DisplayOrder → ColB first, ColA second.
        ws.Cell(1, 1).GetString().ShouldBe("ColB");
        ws.Cell(1, 2).GetString().ShouldBe("ColA");
        ws.Cell(2, 1).GetString().ShouldBe("v2");
        ws.Cell(2, 2).GetString().ShouldBe("v1");
    }

    // ── XML generation round-trip ─────────────────────────────────────────────

    [Fact]
    public async Task ExportAsync_Xml_WritesRootAndElementsInDisplayOrder()
    {
        await SeedTwoFieldTemplateAsync("xml");
        var source = new { First = "v1", Second = "v2" };

        var result = await _exporter.ExportAsync(source, "xml", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var doc = XDocument.Parse(Encoding.UTF8.GetString(result.Value!));
        doc.Root!.Name.LocalName.ShouldBe("Export");
        var children = doc.Root.Elements().ToList();
        children.Count.ShouldBe(2);
        children[0].Name.LocalName.ShouldBe("ColB");
        children[0].Value.ShouldBe("v2");
        children[1].Name.LocalName.ShouldBe("ColA");
        children[1].Value.ShouldBe("v1");
    }

    // ── DOCX generation round-trip ────────────────────────────────────────────

    [Fact]
    public async Task ExportAsync_Docx_WritesParagraphsInDisplayOrderWithLabelAndValue()
    {
        await SeedTwoFieldTemplateAsync("docx");
        var source = new { First = "v1", Second = "v2" };

        var result = await _exporter.ExportAsync(source, "docx", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        using var doc = WordprocessingDocument.Open(new MemoryStream(result.Value!), false);
        var paragraphs = doc.MainDocumentPart!.Document!.Body!
            .Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
            .Select(p => p.InnerText)
            .ToList();
        paragraphs.Count.ShouldBe(2);
        paragraphs[0].ShouldBe("ColB: v2");
        paragraphs[1].ShouldBe("ColA: v1");
    }

    [Fact]
    public async Task ExportAsync_UnsupportedTemplateType_ReturnsExportError()
    {
        // Template persisted with an unsupported TemplateType reaches the switch default.
        await SeedTwoFieldTemplateAsync("pdf");
        var source = new { First = "v1", Second = "v2" };

        var result = await _exporter.ExportAsync(source, "pdf", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("not supported");
    }

    // ── Propagated error messages ─────────────────────────────────────────────

    [Fact]
    public async Task ExportAsync_WhenTemplateMissing_PropagatesNoActiveTemplate()
    {
        var result = await _exporter.ExportAsync(new { X = "1" }, "Excel", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("No active template found");
    }

    [Fact]
    public async Task ExportAsync_WhenRequiredFieldMissing_PrefixesFieldMappingFailed()
    {
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test",
            FieldMappings = { new FieldMapping("Age", "TargetAge", isRequired: true) }
        };
        await _repository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        var result = await _exporter.ExportAsync(new { Name = "x" }, "Excel", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Field mapping failed");
    }

    [Fact]
    public async Task PreviewMappingAsync_WhenRequiredFieldMissing_PropagatesMapError()
    {
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test",
            FieldMappings = { new FieldMapping("Age", "TargetAge", isRequired: true) }
        };
        await _repository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        var result = await _exporter.PreviewMappingAsync(new { Name = "x" }, "Excel", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("not found");
    }

    // ── Exact guard messages ──────────────────────────────────────────────────

    [Fact]
    public async Task ExportAsync_WhenTemplateTypeEmpty_ReturnsExactError()
    {
        var result = await _exporter.ExportAsync(new { X = "1" }, "   ", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Template type cannot be null or empty");
    }

    [Fact]
    public async Task ExportWithVersionAsync_WhenSourceNull_ReturnsExactError()
    {
        var result = await _exporter.ExportWithVersionAsync(null!, "Excel", "1.0.0", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Source object cannot be null");
    }

    [Fact]
    public async Task ExportWithVersionAsync_WhenTemplateTypeEmpty_ReturnsExactError()
    {
        var result = await _exporter.ExportWithVersionAsync(new { X = "1" }, "  ", "1.0.0", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Template type cannot be null or empty");
    }

    [Fact]
    public async Task ExportWithVersionAsync_WhenVersionEmpty_ReturnsExactError()
    {
        var result = await _exporter.ExportWithVersionAsync(new { X = "1" }, "Excel", "  ", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Version cannot be null or empty");
    }

    [Fact]
    public async Task GetActiveTemplateAsync_WhenTemplateTypeEmpty_ReturnsExactError()
    {
        var result = await _exporter.GetActiveTemplateAsync("  ", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Template type cannot be null or empty");
    }

    [Fact]
    public async Task ValidateExportAsync_WhenSourceNull_ReturnsExactError()
    {
        var result = await _exporter.ValidateExportAsync(null!, "Excel", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Source object cannot be null");
    }

    [Fact]
    public async Task ValidateExportAsync_WhenTemplateTypeEmpty_ReturnsExactError()
    {
        var result = await _exporter.ValidateExportAsync(new { X = "1" }, "  ", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Template type cannot be null or empty");
    }

    [Fact]
    public async Task PreviewMappingAsync_WhenTemplateTypeEmpty_ReturnsExactError()
    {
        var result = await _exporter.PreviewMappingAsync(new { X = "1" }, "  ", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Template type cannot be null or empty");
    }

    [Fact]
    public async Task IsTemplateAvailableAsync_WhenTemplateTypeEmpty_ReturnsFalse()
    {
        // Empty type short-circuits to Success(false) without touching the repository.
        var result = await _exporter.IsTemplateAvailableAsync("  ", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }
}
