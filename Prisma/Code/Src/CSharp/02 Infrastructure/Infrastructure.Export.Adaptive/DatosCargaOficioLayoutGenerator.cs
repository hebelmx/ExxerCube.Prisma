using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Export.Adaptive;

/// <summary>
/// Generates the "Datos Carga de Oficio" Excel workbook (24 columns, FR-A requirement)
/// from a <see cref="UnifiedMetadataRecord"/> using the template-field-mapper pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Template resolution order:
/// <list type="number">
///   <item><description>
///     If <c>ITemplateRepository</c> is wired and returns a non-null <c>TemplateDefinition</c>
///     for type <c>"DatosCargaOficio"</c>, that definition is used (DB-override).
///   </description></item>
///   <item><description>
///     Otherwise falls back to <see cref="DatosCargaOficioTemplate.Default"/> (built-in code default,
///     demo-safe without DB seeding).
///   </description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class DatosCargaOficioLayoutGenerator : IDatosCargaOficioLayoutGenerator
{
    private readonly ITemplateFieldMapper _fieldMapper;
    private readonly ITemplateRepository? _templateRepository;
    private readonly ILogger<DatosCargaOficioLayoutGenerator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatosCargaOficioLayoutGenerator"/> class.
    /// </summary>
    /// <param name="fieldMapper">Required field mapper (reflection-based).</param>
    /// <param name="logger">Logger.</param>
    /// <param name="templateRepository">
    /// Optional template repository. When <see langword="null"/> the built-in
    /// <see cref="DatosCargaOficioTemplate.Default"/> is always used.
    /// </param>
    public DatosCargaOficioLayoutGenerator(
        ITemplateFieldMapper fieldMapper,
        ILogger<DatosCargaOficioLayoutGenerator> logger,
        ITemplateRepository? templateRepository = null)
    {
        _fieldMapper = fieldMapper ?? throw new ArgumentNullException(nameof(fieldMapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _templateRepository = templateRepository;
    }

    /// <inheritdoc />
    public async Task<Result> GenerateAsync(
        UnifiedMetadataRecord metadata,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("DatosCargaOficio layout generation cancelled before starting");
            return ResultExtensions.Cancelled();
        }

        if (metadata is null)
        {
            return Result.WithFailure("Metadata cannot be null");
        }

        if (outputStream is null)
        {
            return Result.WithFailure("Output stream cannot be null");
        }

        if (!outputStream.CanWrite)
        {
            return Result.WithFailure("Output stream is not writable");
        }

        if (metadata.Expediente is null)
        {
            return Result.WithFailure("Expediente is required for DatosCargaOficio layout generation");
        }

        try
        {
            _logger.LogInformation(
                "Starting DatosCargaOficio layout generation for expediente: {Expediente}",
                metadata.Expediente.NumeroExpediente);

            // Resolve template (repo-override → built-in fallback)
            var template = await ResolveTemplateAsync(cancellationToken).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return ResultExtensions.Cancelled();
            }

            // Build the flat projection that the field-mapper will reflect over
            var projection = DatosCargaOficioProjection.From(metadata, metadata.ComplianceActions);

            // Map all 24 fields via template
            var mappingResult = await _fieldMapper
                .MapAllFieldsAsync(projection, template, cancellationToken)
                .ConfigureAwait(false);

            if (mappingResult.IsFailure)
            {
                _logger.LogWarning(
                    "DatosCargaOficio field mapping failed: {Error}", mappingResult.Error);
                return Result.WithFailure(mappingResult.Error ?? "Field mapping failed");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return ResultExtensions.Cancelled();
            }

            var fieldValues = mappingResult.Value!;

            // Build the Excel workbook
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Datos Carga de Oficio");

            // Header row — bold + light-gray (mirrors ExcelLayoutGenerator style)
            var headerRow = worksheet.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

            // Write headers in display order
            var sortedMappings = template.FieldMappings
                .OrderBy(m => m.DisplayOrder)
                .ToList();

            for (int i = 0; i < sortedMappings.Count; i++)
            {
                var mapping = sortedMappings[i];
                worksheet.Cell(1, i + 1).Value = mapping.TargetField;

                // Data value
                var value = fieldValues.TryGetValue(mapping.TargetField, out var v) ? v : string.Empty;
                worksheet.Cell(2, i + 1).Value = value;
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            // Persist to stream
            await Task.Run(() => workbook.SaveAs(outputStream), cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Successfully generated DatosCargaOficio layout for expediente: {Expediente}",
                metadata.Expediente.NumeroExpediente);

            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("DatosCargaOficio layout generation cancelled");
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating DatosCargaOficio layout");
            return Result.WithFailure($"Error generating DatosCargaOficio layout: {ex.Message}", ex);
        }
    }

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    private async Task<TemplateDefinition> ResolveTemplateAsync(CancellationToken cancellationToken)
    {
        if (_templateRepository is not null)
        {
            try
            {
                var dbTemplate = await _templateRepository
                    .GetLatestTemplateAsync(DatosCargaOficioTemplate.TemplateType, cancellationToken)
                    .ConfigureAwait(false);

                if (dbTemplate is not null)
                {
                    _logger.LogDebug(
                        "DatosCargaOficio template loaded from repository (version {Version})",
                        dbTemplate.Version);
                    return dbTemplate;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to load DatosCargaOficio template from repository — using built-in fallback");
            }
        }

        _logger.LogDebug("DatosCargaOficio template: using built-in default");
        return DatosCargaOficioTemplate.Default;
    }
}
