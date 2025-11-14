using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using IndQuestResults;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Extraction;

/// <summary>
/// XML parser implementation for extracting Expediente entities from XML documents.
/// </summary>
public class XmlExpedienteParser : IXmlNullableParser<Expediente>
{
    private readonly ILogger<XmlExpedienteParser> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlExpedienteParser"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public XmlExpedienteParser(ILogger<XmlExpedienteParser> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<Expediente>> ParseAsync(
        byte[] xmlContent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var xmlString = System.Text.Encoding.UTF8.GetString(xmlContent);
            var doc = XDocument.Parse(xmlString);
            var root = doc.Root;

            if (root == null)
            {
                return Task.FromResult(Result<Expediente>.WithFailure("XML document has no root element"));
            }

            var expediente = new Expediente
            {
                NumeroExpediente = GetElementValue(root, "NumeroExpediente") ?? string.Empty,
                NumeroOficio = GetElementValue(root, "NumeroOficio") ?? string.Empty,
                SolicitudSiara = GetElementValue(root, "SolicitudSiara") ?? string.Empty,
                Folio = int.TryParse(GetElementValue(root, "Folio"), out var folio) ? folio : 0,
                OficioYear = int.TryParse(GetElementValue(root, "OficioYear"), out var year) ? year : DateTime.Now.Year,
                AreaClave = int.TryParse(GetElementValue(root, "AreaClave"), out var areaClave) ? areaClave : 0,
                AreaDescripcion = GetElementValue(root, "AreaDescripcion") ?? string.Empty,
                FechaPublicacion = DateTime.TryParse(GetElementValue(root, "FechaPublicacion"), out var fecha) ? fecha : DateTime.MinValue,
                DiasPlazo = int.TryParse(GetElementValue(root, "DiasPlazo"), out var diasPlazo) ? diasPlazo : 0,
                AutoridadNombre = GetElementValue(root, "AutoridadNombre") ?? string.Empty,
                AutoridadEspecificaNombre = GetElementValue(root, "AutoridadEspecificaNombre"),
                NombreSolicitante = GetElementValue(root, "NombreSolicitante"),
                Referencia = GetElementValue(root, "Referencia") ?? string.Empty,
                Referencia1 = GetElementValue(root, "Referencia1") ?? string.Empty,
                Referencia2 = GetElementValue(root, "Referencia2") ?? string.Empty,
                TieneAseguramiento = bool.TryParse(GetElementValue(root, "TieneAseguramiento"), out var tieneAseg) && tieneAseg
            };

            // Parse SolicitudPartes
            var partesElements = root.Elements("SolicitudPartes").Elements("Parte");
            foreach (var parteElement in partesElements)
            {
                var parte = new SolicitudParte
                {
                    ParteId = int.TryParse(GetElementValue(parteElement, "ParteId"), out var parteId) ? parteId : 0,
                    Caracter = GetElementValue(parteElement, "Caracter") ?? string.Empty,
                    PersonaTipo = GetElementValue(parteElement, "PersonaTipo") ?? string.Empty,
                    Paterno = GetElementValue(parteElement, "Paterno"),
                    Materno = GetElementValue(parteElement, "Materno"),
                    Nombre = GetElementValue(parteElement, "Nombre") ?? string.Empty,
                    Rfc = GetElementValue(parteElement, "Rfc"),
                    Relacion = GetElementValue(parteElement, "Relacion"),
                    Domicilio = GetElementValue(parteElement, "Domicilio"),
                    Complementarios = GetElementValue(parteElement, "Complementarios")
                };
                expediente.SolicitudPartes.Add(parte);
            }

            // Parse SolicitudEspecificas
            var especificasElements = root.Elements("SolicitudEspecificas").Elements("Especifica");
            foreach (var especificaElement in especificasElements)
            {
                var especifica = new SolicitudEspecifica
                {
                    RequerimientoId = GetElementValue(especificaElement, "RequerimientoId") ?? string.Empty,
                    Descripcion = GetElementValue(especificaElement, "Descripcion") ?? string.Empty,
                    Tipo = GetElementValue(especificaElement, "Tipo") ?? string.Empty
                };
                expediente.SolicitudEspecificas.Add(especifica);
            }

            _logger.LogDebug("Successfully parsed Expediente: {NumeroExpediente}", expediente.NumeroExpediente);
            return Task.FromResult(Result<Expediente>.Success(expediente));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing XML to Expediente");
            return Task.FromResult(Result<Expediente>.WithFailure($"Error parsing XML: {ex.Message}", default(Expediente), ex));
        }
    }

    private static string? GetElementValue(XElement? parent, string elementName)
    {
        return parent?.Element(elementName)?.Value;
    }
}

