using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive;
using IndQuestResults;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExxerCube.Prisma.Tests.Infrastructure.Export.Adaptive;

/// <summary>
/// Mutation-killing tests for <see cref="AdaptiveExporter"/>'s failure paths, driving
/// substituted collaborators to return failures or throw. Pins the propagated/caught
/// error messages on the ExportWithVersion mapping-failure path and the per-method
/// exception catches (GetActiveTemplate, ExportWithVersion, ValidateExport,
/// PreviewMapping, IsTemplateAvailable).
/// </summary>
public sealed class AdaptiveExporterErrorPathTests
{
    private readonly ITemplateRepository _repository = Substitute.For<ITemplateRepository>();
    private readonly ITemplateFieldMapper _mapper = Substitute.For<ITemplateFieldMapper>();
    private readonly AdaptiveExporter _exporter;

    public AdaptiveExporterErrorPathTests()
    {
        _exporter = new AdaptiveExporter(_repository, _mapper, NullLogger<AdaptiveExporter>.Instance);
    }

    private static TemplateDefinition ExcelTemplate() => new()
    {
        TemplateId = "id",
        TemplateType = "Excel",
        Version = "1.0.0",
        FieldMappings = { new FieldMapping("Name", "TargetName", isRequired: true) }
    };

    [Fact]
    public async Task GetActiveTemplateAsync_WhenRepositoryThrows_ReturnsRetrievingError()
    {
        _repository.GetLatestTemplateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<TemplateDefinition?>(_ => throw new InvalidOperationException("repo down"));

        var result = await _exporter.GetActiveTemplateAsync("Excel", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Error retrieving template");
    }

    [Fact]
    public async Task ExportWithVersionAsync_WhenMappingFails_PrefixesFieldMappingFailed()
    {
        _repository.GetTemplateAsync("Excel", "1.0.0", Arg.Any<CancellationToken>())
            .Returns(ExcelTemplate());
        _mapper.MapAllFieldsAsync(Arg.Any<object>(), Arg.Any<TemplateDefinition>(), Arg.Any<CancellationToken>())
            .Returns(Result<Dictionary<string, string>>.Failure("inner mapping error"));

        var result = await _exporter.ExportWithVersionAsync(
            new { Name = "x" }, "Excel", "1.0.0", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Field mapping failed");
    }

    [Fact]
    public async Task ExportWithVersionAsync_WhenMapperThrows_ReturnsExportError()
    {
        _repository.GetTemplateAsync("Excel", "1.0.0", Arg.Any<CancellationToken>())
            .Returns(ExcelTemplate());
        _mapper.MapAllFieldsAsync(Arg.Any<object>(), Arg.Any<TemplateDefinition>(), Arg.Any<CancellationToken>())
            .Returns<Result<Dictionary<string, string>>>(_ => throw new InvalidOperationException("kaboom"));

        var result = await _exporter.ExportWithVersionAsync(
            new { Name = "x" }, "Excel", "1.0.0", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Export error");
    }

    [Fact]
    public async Task ValidateExportAsync_WhenMapperThrows_ReturnsValidationError()
    {
        _repository.GetLatestTemplateAsync("Excel", Arg.Any<CancellationToken>())
            .Returns(ExcelTemplate());
        _mapper.MapFieldAsync(Arg.Any<object>(), Arg.Any<FieldMapping>(), Arg.Any<CancellationToken>())
            .Returns<Result<string>>(_ => throw new InvalidOperationException("kaboom"));

        var result = await _exporter.ValidateExportAsync(
            new { Name = "x" }, "Excel", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Validation error");
    }

    [Fact]
    public async Task PreviewMappingAsync_WhenMapperThrows_ReturnsPreviewError()
    {
        _repository.GetLatestTemplateAsync("Excel", Arg.Any<CancellationToken>())
            .Returns(ExcelTemplate());
        _mapper.MapAllFieldsAsync(Arg.Any<object>(), Arg.Any<TemplateDefinition>(), Arg.Any<CancellationToken>())
            .Returns<Result<Dictionary<string, string>>>(_ => throw new InvalidOperationException("kaboom"));

        var result = await _exporter.PreviewMappingAsync(
            new { Name = "x" }, "Excel", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Preview error");
    }

    [Fact]
    public async Task IsTemplateAvailableAsync_WhenRepositoryThrows_ReturnsError()
    {
        _repository.GetLatestTemplateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<TemplateDefinition?>(_ => throw new InvalidOperationException("repo down"));

        var result = await _exporter.IsTemplateAvailableAsync("Excel", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Error checking template availability");
    }
}
