using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using IndQuestResults;
using IndQuestResults.Operations;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Export;

/// <summary>
/// Implements Excel layout generation for SIRO registration systems (FR18) from unified metadata records.
/// </summary>
public class ExcelLayoutGenerator : ILayoutGenerator
{
    private readonly ILogger<ExcelLayoutGenerator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExcelLayoutGenerator"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public ExcelLayoutGenerator(ILogger<ExcelLayoutGenerator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> GenerateExcelLayoutAsync(
        UnifiedMetadataRecord metadata,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Excel layout generation cancelled before starting");
            return ResultExtensions.Cancelled();
        }

        // Input validation
        if (metadata == null)
        {
            return Result.WithFailure("Metadata cannot be null");
        }

        if (outputStream == null)
        {
            return Result.WithFailure("Output stream cannot be null");
        }

        if (!outputStream.CanWrite)
        {
            return Result.WithFailure("Output stream is not writable");
        }

        try
        {
            _logger.LogInformation("Starting Excel layout generation for expediente: {Expediente}", metadata.Expediente?.NumeroExpediente ?? "Unknown");

            // Validate required fields
            if (metadata.Expediente == null)
            {
                return Result.WithFailure("Expediente is required for Excel layout generation");
            }

            // Generate Excel workbook
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("SIRO Registration");

            // Header row
            var headerRow = worksheet.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

            worksheet.Cell(1, 1).Value = "NumeroExpediente";
            worksheet.Cell(1, 2).Value = "NumeroOficio";
            worksheet.Cell(1, 3).Value = "SolicitudSiara";
            worksheet.Cell(1, 4).Value = "Folio";
            worksheet.Cell(1, 5).Value = "OficioYear";
            worksheet.Cell(1, 6).Value = "AreaClave";
            worksheet.Cell(1, 7).Value = "AreaDescripcion";
            worksheet.Cell(1, 8).Value = "FechaPublicacion";
            worksheet.Cell(1, 9).Value = "DiasPlazo";
            worksheet.Cell(1, 10).Value = "AutoridadNombre";
            worksheet.Cell(1, 11).Value = "RFC";
            worksheet.Cell(1, 12).Value = "NombreCompleto";

            // Data row
            var dataRow = worksheet.Row(2);
            var expediente = metadata.Expediente;

            dataRow.Cell(1).Value = expediente.NumeroExpediente;
            dataRow.Cell(2).Value = expediente.NumeroOficio;
            dataRow.Cell(3).Value = expediente.SolicitudSiara;
            dataRow.Cell(4).Value = expediente.Folio;
            dataRow.Cell(5).Value = expediente.OficioYear;
            dataRow.Cell(6).Value = expediente.AreaClave;
            dataRow.Cell(7).Value = expediente.AreaDescripcion;
            dataRow.Cell(8).Value = expediente.FechaPublicacion.ToString("yyyy-MM-dd");
            dataRow.Cell(9).Value = expediente.DiasPlazo;
            dataRow.Cell(10).Value = expediente.AutoridadNombre;

            // Extract RFC and name from first party if available
            if (expediente.SolicitudPartes != null && expediente.SolicitudPartes.Count > 0)
            {
                var firstParte = expediente.SolicitudPartes[0];
                dataRow.Cell(11).Value = firstParte.Rfc ?? string.Empty;
                dataRow.Cell(12).Value = $"{firstParte.Nombre} {firstParte.Paterno ?? string.Empty} {firstParte.Materno ?? string.Empty}".Trim();
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            // Save to stream
            await Task.Run(() => workbook.SaveAs(outputStream), cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully generated Excel layout for expediente: {Expediente}", expediente.NumeroExpediente);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Excel layout generation cancelled");
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Excel layout");
            return Result.WithFailure($"Error generating Excel layout: {ex.Message}", ex);
        }
    }
}

