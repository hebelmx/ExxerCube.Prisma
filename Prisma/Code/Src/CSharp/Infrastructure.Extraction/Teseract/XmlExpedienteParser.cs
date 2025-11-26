namespace ExxerCube.Prisma.Infrastructure.Extraction.Teseract;

/// <summary>
/// XML parser implementation for extracting Expediente entities from XML documents.
/// </summary>
/// <remarks>
/// ╔════════════════════════════════════════════════════════════════════════════════╗
/// ║                            🗺️ ROADMAP / FUTURE ENHANCEMENTS                    ║
/// ╠════════════════════════════════════════════════════════════════════════════════╣
/// ║ TODO: FUZZY SEARCH FOR MISSING FIELDS                                          ║
/// ║ ─────────────────────────────────────────────────────────────────────────────  ║
/// ║ After all exact field matching is exhausted (including Cnbv_ prefixes),       ║
/// ║ implement fuzzy search to match XML elements to domain properties when:        ║
/// ║   • Element name doesn't match exactly                                         ║
/// ║   • Typos or variations in XML field names                                     ║
/// ║   • Different naming conventions (camelCase vs PascalCase vs snake_case)       ║
/// ║                                                                                 ║
/// ║ Implementation approach:                                                        ║
/// ║   1. Track all XML elements found vs domain properties required                ║
/// ║   2. For unmatched domain properties, use fuzzy matching (Levenshtein)         ║
/// ║   3. Log warnings for fuzzy-matched fields (compliance audit trail)            ║
/// ║   4. Require minimum confidence threshold for fuzzy matches                    ║
/// ║                                                                                 ║
/// ║ See: MinimumFieldsProvidedBySamples for baseline field expectations            ║
/// ╚════════════════════════════════════════════════════════════════════════════════╝
/// </remarks>
public class XmlExpedienteParser : IXmlNullableParser<Expediente>
{
    private readonly ILogger<XmlExpedienteParser> _logger;

    /// <summary>
    /// Minimum number of fields that should be extractable based on real PRP1 sample fixtures.
    /// Used as baseline for validation and future fuzzy matching implementation.
    /// </summary>
    /// <remarks>
    /// Based on 4 PRP1 fixtures (222AAA, 333BBB, 333ccc, 555CCC):
    /// - Root fields: ~15 (NumeroExpediente, NumeroOficio, FechaPublicacion, etc.)
    /// - SolicitudPartes: 1+ with 10 fields each
    /// - SolicitudEspecifica: 1+ (currently 3 fields, should be 11+ when domain fixed)
    /// Total expected: ~30+ fields per document minimum.
    /// </remarks>
    private const int MinimumFieldsProvidedBySamples = 30;

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
            // Use StreamReader to automatically handle UTF-8 BOM (Byte Order Mark)
            // Real CNBV XML files often have BOM (EF BB BF) which causes parsing errors
            using var stream = new MemoryStream(xmlContent);
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var doc = XDocument.Load(reader);
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

            // Parse SolicitudPartes (XML structure: <SolicitudPartes> contains fields directly, not a collection)
            // Note: XML uses singular "SolicitudPartes" element (not a collection wrapper)
            var partesElements = root.Elements().Where(e => e.Name.LocalName == "SolicitudPartes");
            foreach (var parteElement in partesElements)
            {
                var parte = new SolicitudParte
                {
                    ParteId = int.TryParse(GetElementValue(parteElement, "ParteId"), out var parteId) ? parteId : 0,
                    Caracter = GetElementValue(parteElement, "Caracter") ?? string.Empty,
                    PersonaTipo = GetElementValue(parteElement, "Persona") ?? string.Empty, // XML uses <Persona> not <PersonaTipo>
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

            // Parse SolicitudEspecifica (XML structure: singular element, not "SolicitudEspecificas" collection)
            var especificasElements = root.Elements().Where(e => e.Name.LocalName == "SolicitudEspecifica");
            foreach (var especificaElement in especificasElements)
            {
                var especifica = new SolicitudEspecifica
                {
                    SolicitudEspecificaId = int.TryParse(GetElementValue(especificaElement, "SolicitudEspecificaId"), out var especificaId) ? especificaId : 0,
                    InstruccionesCuentasPorConocer = GetElementValue(especificaElement, "InstruccionesCuentasPorConocer") ?? string.Empty
                };

                // Parse nested PersonasSolicitud collection
                var personasSolicitudElements = especificaElement.Elements().Where(e => e.Name.LocalName == "PersonasSolicitud");
                foreach (var personaElement in personasSolicitudElements)
                {
                    var persona = new PersonaSolicitud
                    {
                        PersonaId = int.TryParse(GetElementValue(personaElement, "PersonaId"), out var personaId) ? personaId : 0,
                        Caracter = GetElementValue(personaElement, "Caracter") ?? string.Empty,
                        Persona = GetElementValue(personaElement, "Persona") ?? string.Empty, // XML uses <Persona> not <PersonaTipo>
                        Paterno = GetElementValue(personaElement, "Paterno"),
                        Materno = GetElementValue(personaElement, "Materno"),
                        Nombre = GetElementValue(personaElement, "Nombre") ?? string.Empty,
                        Rfc = GetElementValue(personaElement, "Rfc"),
                        Relacion = GetElementValue(personaElement, "Relacion"),
                        Domicilio = GetElementValue(personaElement, "Domicilio"),
                        Complementarios = GetElementValue(personaElement, "Complementarios")
                    };
                    especifica.PersonasSolicitud.Add(persona);
                }

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
        if (parent == null)
        {
            return null;
        }

        // Try to get element without namespace first (handles local names correctly)
        var element = parent.Elements().FirstOrDefault(e => e.Name.LocalName == elementName);
        if (element == null)
        {
            // Try with Cnbv_ prefix (CNBV standard format)
            element = parent.Elements().FirstOrDefault(e => e.Name.LocalName == $"Cnbv_{elementName}");
        }

        if (element == null)
        {
            return null;
        }

        // Check for xsi:nil="true" attribute (XML null representation)
        var nilAttribute = element.Attributes().FirstOrDefault(a => a.Name.LocalName == "nil");
        if (nilAttribute != null && nilAttribute.Value.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Return value, or null if empty (empty XML elements should be treated as null for optional fields)
        var value = element.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

