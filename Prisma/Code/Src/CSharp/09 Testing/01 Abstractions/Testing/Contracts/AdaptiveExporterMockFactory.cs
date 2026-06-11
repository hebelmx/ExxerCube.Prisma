using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using IndQuestResults;
using NSubstitute;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Builds the contract-conforming NSubstitute mock that backs the blueprint instance
/// of <see cref="AdaptiveExporterContract"/>.
/// </summary>
/// <remarks>
/// <para>
/// The contract asserts real orchestration outcomes (active-template selection, field mapping,
/// non-empty export bytes, validation/preview semantics, exact failure messages), which canned
/// stubs cannot satisfy. The factory therefore evolved into a <em>reference fake</em> that
/// composes the <see cref="TemplateFieldMapperMockFactory"/> reference fake with an in-memory
/// template store seeded by the blueprint, mirroring the production <c>AdaptiveExporter</c>
/// orchestration. This is exactly the evolution ADR-005 §6 expects.
/// </para>
/// </remarks>
public static class AdaptiveExporterMockFactory
{
    /// <summary>
    /// Creates an <see cref="IAdaptiveExporter"/> mock that satisfies every test in
    /// <see cref="AdaptiveExporterContract"/>, backed by the supplied template store.
    /// </summary>
    /// <param name="templateStore">The shared store the blueprint instance seeds.</param>
    /// <returns>The configured mock.</returns>
    public static IAdaptiveExporter CreateContractConformingMock(List<TemplateDefinition> templateStore)
    {
        var fieldMapper = TemplateFieldMapperMockFactory.CreateContractConformingMock();
        var mock = Substitute.For<IAdaptiveExporter>();

        mock.ExportAsync(Arg.Any<object>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => ExportAsync(
                templateStore, fieldMapper, call.ArgAt<object>(0), call.ArgAt<string>(1), call.ArgAt<CancellationToken>(2)));

        mock.ExportWithVersionAsync(Arg.Any<object>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => ExportWithVersionAsync(
                templateStore, fieldMapper, call.ArgAt<object>(0), call.ArgAt<string>(1), call.ArgAt<string>(2), call.ArgAt<CancellationToken>(3)));

        mock.GetActiveTemplateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(GetActiveTemplate(templateStore, call.ArgAt<string>(0))));

        mock.ValidateExportAsync(Arg.Any<object>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => ValidateExportAsync(
                templateStore, fieldMapper, call.ArgAt<object>(0), call.ArgAt<string>(1), call.ArgAt<CancellationToken>(2)));

        mock.PreviewMappingAsync(Arg.Any<object>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => PreviewMappingAsync(
                templateStore, fieldMapper, call.ArgAt<object>(0), call.ArgAt<string>(1), call.ArgAt<CancellationToken>(2)));

        mock.IsTemplateAvailableAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(Result<bool>.Success(GetLatest(templateStore, call.ArgAt<string>(0)) != null)));

        return mock;
    }

    private static async Task<Result<byte[]>> ExportAsync(
        List<TemplateDefinition> store, ITemplateFieldMapper fieldMapper, object sourceObject, string templateType, CancellationToken ct)
    {
        if (sourceObject == null)
        {
            return Result<byte[]>.Failure("Source object cannot be null");
        }

        var templateResult = GetActiveTemplate(store, templateType);
        if (templateResult.IsFailure)
        {
            return Result<byte[]>.Failure(templateResult.Error ?? "Failed to retrieve active template");
        }

        var template = templateResult.Value!;
        var mappingResult = await fieldMapper.MapAllFieldsAsync(sourceObject, template, ct);
        if (mappingResult.IsFailure)
        {
            return Result<byte[]>.Failure($"Field mapping failed: {mappingResult.Error}");
        }

        return Result<byte[]>.Success(GenerateBytes(template, mappingResult.Value!));
    }

    private static async Task<Result<byte[]>> ExportWithVersionAsync(
        List<TemplateDefinition> store, ITemplateFieldMapper fieldMapper, object sourceObject, string templateType, string version, CancellationToken ct)
    {
        if (sourceObject == null)
        {
            return Result<byte[]>.Failure("Source object cannot be null");
        }

        var template = store.FirstOrDefault(t => t.TemplateType == templateType && t.Version == version);
        if (template == null)
        {
            return Result<byte[]>.Failure($"Template version '{version}' not found for type '{templateType}'");
        }

        var mappingResult = await fieldMapper.MapAllFieldsAsync(sourceObject, template, ct);
        if (mappingResult.IsFailure)
        {
            return Result<byte[]>.Failure($"Field mapping failed: {mappingResult.Error}");
        }

        return Result<byte[]>.Success(GenerateBytes(template, mappingResult.Value!));
    }

    private static Result<TemplateDefinition> GetActiveTemplate(List<TemplateDefinition> store, string templateType)
    {
        var template = GetLatest(store, templateType);

        if (template == null)
        {
            return Result<TemplateDefinition>.Failure($"No active template found for type '{templateType}'");
        }

        return Result<TemplateDefinition>.Success(template);
    }

    private static async Task<Result> ValidateExportAsync(
        List<TemplateDefinition> store, ITemplateFieldMapper fieldMapper, object sourceObject, string templateType, CancellationToken ct)
    {
        if (sourceObject == null)
        {
            return Result.Failure("Source object cannot be null");
        }

        var templateResult = GetActiveTemplate(store, templateType);
        if (templateResult.IsFailure)
        {
            return Result.Failure($"Template '{templateType}' not found");
        }

        var template = templateResult.Value!;

        foreach (var mapping in template.FieldMappings)
        {
            var mapResult = await fieldMapper.MapFieldAsync(sourceObject, mapping, ct);

            if (mapResult.IsFailure && mapping.IsRequired)
            {
                return Result.Failure(mapResult.Error ?? $"Required field '{mapping.SourceFieldPath}' validation failed");
            }
        }

        return Result.Success();
    }

    private static async Task<Result<Dictionary<string, string>>> PreviewMappingAsync(
        List<TemplateDefinition> store, ITemplateFieldMapper fieldMapper, object sourceObject, string templateType, CancellationToken ct)
    {
        if (sourceObject == null)
        {
            return Result<Dictionary<string, string>>.Failure("Source object cannot be null");
        }

        var templateResult = GetActiveTemplate(store, templateType);
        if (templateResult.IsFailure)
        {
            return Result<Dictionary<string, string>>.Failure($"Template '{templateType}' not found");
        }

        var template = templateResult.Value!;
        var mappingResult = await fieldMapper.MapAllFieldsAsync(sourceObject, template, ct);
        if (mappingResult.IsFailure)
        {
            return Result<Dictionary<string, string>>.Failure(mappingResult.Error ?? "Mapping preview failed");
        }

        return Result<Dictionary<string, string>>.Success(mappingResult.Value!);
    }

    private static TemplateDefinition? GetLatest(List<TemplateDefinition> store, string templateType)
    {
        var now = DateTime.UtcNow;
        return store
            .Where(t =>
                t.TemplateType == templateType &&
                t.IsActive &&
                t.EffectiveDate <= now &&
                (t.ExpirationDate == null || t.ExpirationDate > now))
            .OrderByDescending(t => t.EffectiveDate)
            .FirstOrDefault();
    }

    private static byte[] GenerateBytes(TemplateDefinition template, Dictionary<string, string> mappedFields)
    {
        // Reference fake produces a deterministic, non-empty payload (the contract only
        // asserts Length > 0). Binary-format fidelity (xlsx/xml/docx) is implementation
        // detail pinned by AdaptiveExporterMutationTests, not the contract.
        var ordered = template.FieldMappings
            .OrderBy(fm => fm.DisplayOrder)
            .Select(fm => mappedFields.TryGetValue(fm.TargetField, out var v) ? $"{fm.TargetField}={v}" : fm.TargetField);

        var payload = $"{template.TemplateType}:{string.Join(";", ordered)}";
        return Encoding.UTF8.GetBytes(payload);
    }
}
