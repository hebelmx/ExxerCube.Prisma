using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using IndQuestResults;
using IndQuestResults.Operations;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Export;

/// <summary>
/// Implements SIRO-compliant XML export generation from unified metadata records.
/// </summary>
public class SiroXmlExporter : IResponseExporter
{
    private readonly ILogger<SiroXmlExporter> _logger;
    private readonly XmlSchemaSet? _siroSchemaSet;

    /// <summary>
    /// Initializes a new instance of the <see cref="SiroXmlExporter"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public SiroXmlExporter(ILogger<SiroXmlExporter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SiroXmlExporter"/> class with schema validation.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="siroSchemaSet">The SIRO XML schema set for validation (optional).</param>
    public SiroXmlExporter(ILogger<SiroXmlExporter> logger, XmlSchemaSet? siroSchemaSet = null)
    {
        _logger = logger;
        _siroSchemaSet = siroSchemaSet;
    }

    /// <inheritdoc />
    public async Task<Result> ExportSiroXmlAsync(
        UnifiedMetadataRecord metadata,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("SIRO XML export cancelled before starting");
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
            _logger.LogInformation("Starting SIRO XML export for expediente: {Expediente}", metadata.Expediente?.NumeroExpediente ?? "Unknown");

            // Validate required fields
            var validationResult = ValidateMetadata(metadata);
            if (validationResult.IsFailure)
            {
                return validationResult;
            }

            // Generate XML content
            var xmlContent = GenerateSiroXml(metadata);

            // Validate against schema if provided
            if (_siroSchemaSet != null)
            {
                var schemaValidationResult = ValidateXmlSchema(xmlContent);
                if (schemaValidationResult.IsFailure)
                {
                    return schemaValidationResult;
                }
            }

            // Write to output stream
            var bytes = Encoding.UTF8.GetBytes(xmlContent);
            await outputStream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
            await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully exported SIRO XML for expediente: {Expediente}", metadata.Expediente?.NumeroExpediente ?? "Unknown");
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("SIRO XML export cancelled");
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SIRO XML export");
            return Result.WithFailure($"Error generating SIRO XML: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result> ExportSignedPdfAsync(
        UnifiedMetadataRecord metadata,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Signed PDF export cancelled before starting");
            return ResultExtensions.Cancelled();
        }

        // This will be implemented in Story 1.8 (PDF signing)
        _logger.LogWarning("PDF signing not yet implemented - will be added in Story 1.8");
        return Result.WithFailure("PDF signing functionality will be implemented in Story 1.8");
    }

    /// <summary>
    /// Validates that metadata contains all required fields for SIRO export.
    /// </summary>
    /// <param name="metadata">The metadata to validate.</param>
    /// <returns>A result indicating validation success or failure.</returns>
    private Result ValidateMetadata(UnifiedMetadataRecord metadata)
    {
        if (metadata.Expediente == null)
        {
            return Result.WithFailure("Expediente is required for SIRO export");
        }

        if (string.IsNullOrWhiteSpace(metadata.Expediente.NumeroExpediente))
        {
            return Result.WithFailure("Expediente number is required for SIRO export");
        }

        if (string.IsNullOrWhiteSpace(metadata.Expediente.NumeroOficio))
        {
            return Result.WithFailure("Oficio number is required for SIRO export");
        }

        return Result.Success();
    }

    /// <summary>
    /// Generates SIRO-compliant XML content from unified metadata record.
    /// </summary>
    /// <param name="metadata">The unified metadata record.</param>
    /// <returns>The SIRO XML content as a string.</returns>
    private string GenerateSiroXml(UnifiedMetadataRecord metadata)
    {
        var expediente = metadata.Expediente!;
        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false
        };

        using var stringWriter = new StringWriter();
        using var xmlWriter = XmlWriter.Create(stringWriter, settings);

        xmlWriter.WriteStartDocument();
        xmlWriter.WriteStartElement("SiroResponse", "http://siro.regulatory.namespace");

        // Expediente information
        xmlWriter.WriteElementString("NumeroExpediente", expediente.NumeroExpediente);
        xmlWriter.WriteElementString("NumeroOficio", expediente.NumeroOficio);
        xmlWriter.WriteElementString("SolicitudSiara", expediente.SolicitudSiara);
        xmlWriter.WriteElementString("Folio", expediente.Folio.ToString());
        xmlWriter.WriteElementString("OficioYear", expediente.OficioYear.ToString());
        xmlWriter.WriteElementString("AreaClave", expediente.AreaClave.ToString());
        xmlWriter.WriteElementString("AreaDescripcion", expediente.AreaDescripcion);
        xmlWriter.WriteElementString("FechaPublicacion", expediente.FechaPublicacion.ToString("yyyy-MM-dd"));
        xmlWriter.WriteElementString("DiasPlazo", expediente.DiasPlazo.ToString());
        xmlWriter.WriteElementString("AutoridadNombre", expediente.AutoridadNombre);

        if (!string.IsNullOrWhiteSpace(expediente.AutoridadEspecificaNombre))
        {
            xmlWriter.WriteElementString("AutoridadEspecificaNombre", expediente.AutoridadEspecificaNombre);
        }

        if (!string.IsNullOrWhiteSpace(expediente.NombreSolicitante))
        {
            xmlWriter.WriteElementString("NombreSolicitante", expediente.NombreSolicitante);
        }

        // Legal references
        if (!string.IsNullOrWhiteSpace(expediente.Referencia))
        {
            xmlWriter.WriteElementString("Referencia", expediente.Referencia);
        }

        if (!string.IsNullOrWhiteSpace(expediente.Referencia1))
        {
            xmlWriter.WriteElementString("Referencia1", expediente.Referencia1);
        }

        if (!string.IsNullOrWhiteSpace(expediente.Referencia2))
        {
            xmlWriter.WriteElementString("Referencia2", expediente.Referencia2);
        }

        // SolicitudPartes
        if (expediente.SolicitudPartes != null && expediente.SolicitudPartes.Count > 0)
        {
            xmlWriter.WriteStartElement("SolicitudPartes");
            foreach (var parte in expediente.SolicitudPartes)
            {
                xmlWriter.WriteStartElement("Parte");
                xmlWriter.WriteElementString("ParteId", parte.ParteId.ToString());
                xmlWriter.WriteElementString("Caracter", parte.Caracter);
                xmlWriter.WriteElementString("PersonaTipo", parte.PersonaTipo);
                if (!string.IsNullOrWhiteSpace(parte.Paterno))
                {
                    xmlWriter.WriteElementString("Paterno", parte.Paterno);
                }

                if (!string.IsNullOrWhiteSpace(parte.Materno))
                {
                    xmlWriter.WriteElementString("Materno", parte.Materno);
                }

                xmlWriter.WriteElementString("Nombre", parte.Nombre);
                if (!string.IsNullOrWhiteSpace(parte.Rfc))
                {
                    xmlWriter.WriteElementString("Rfc", parte.Rfc);
                }

                if (!string.IsNullOrWhiteSpace(parte.Relacion))
                {
                    xmlWriter.WriteElementString("Relacion", parte.Relacion);
                }

                if (!string.IsNullOrWhiteSpace(parte.Domicilio))
                {
                    xmlWriter.WriteElementString("Domicilio", parte.Domicilio);
                }

                if (!string.IsNullOrWhiteSpace(parte.Complementarios))
                {
                    xmlWriter.WriteElementString("Complementarios", parte.Complementarios);
                }

                xmlWriter.WriteEndElement(); // Parte
            }

            xmlWriter.WriteEndElement(); // SolicitudPartes
        }

        // SolicitudEspecificas
        if (expediente.SolicitudEspecificas != null && expediente.SolicitudEspecificas.Count > 0)
        {
            xmlWriter.WriteStartElement("SolicitudEspecificas");
            foreach (var especifica in expediente.SolicitudEspecificas)
            {
                xmlWriter.WriteStartElement("Especifica");
                xmlWriter.WriteElementString("Id", especifica.SolicitudEspecificaId.ToString());
                xmlWriter.WriteElementString("Instrucciones", especifica.InstruccionesCuentasPorConocer);
                xmlWriter.WriteEndElement(); // Especifica
            }

            xmlWriter.WriteEndElement(); // SolicitudEspecificas
        }

        xmlWriter.WriteEndElement(); // SiroResponse
        xmlWriter.WriteEndDocument();
        xmlWriter.Flush();

        return stringWriter.ToString();
    }

    /// <summary>
    /// Validates XML content against SIRO schema.
    /// </summary>
    /// <param name="xmlContent">The XML content to validate.</param>
    /// <returns>A result indicating validation success or failure.</returns>
    private Result ValidateXmlSchema(string xmlContent)
    {
        if (_siroSchemaSet == null)
        {
            return Result.Success();
        }

        try
        {
            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas = _siroSchemaSet
            };

            var errors = new List<string>();
            settings.ValidationEventHandler += (sender, e) =>
            {
                if (e.Severity == XmlSeverityType.Error)
                {
                    errors.Add($"Line {e.Exception?.LineNumber}, Position {e.Exception?.LinePosition}: {e.Message}");
                }
            };

            using var reader = XmlReader.Create(new StringReader(xmlContent), settings);
            while (reader.Read())
            {
                // Read through document to trigger validation
            }

            if (errors.Count > 0)
            {
                return Result.WithFailure($"SIRO schema validation failed: {string.Join("; ", errors)}");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.WithFailure($"Error validating SIRO schema: {ex.Message}", ex);
        }
    }
}

