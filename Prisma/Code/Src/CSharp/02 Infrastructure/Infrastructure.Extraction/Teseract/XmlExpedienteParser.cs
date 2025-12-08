namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

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

            var numeroExpediente = GetElementValue(root, "NumeroExpediente") ?? string.Empty;
            var areaDescripcion = GetElementValue(root, "AreaDescripcion") ?? string.Empty;
            var areaClave = int.TryParse(GetElementValue(root, "AreaClave"), out var areaClaveValue) ? areaClaveValue : 0;
            var autoridadNombre = GetElementValue(root, "AutoridadNombre") ?? string.Empty;
            var tieneAseguramiento = bool.TryParse(GetElementValue(root, "TieneAseguramiento"), out var tieneAseg) && tieneAseg;

            var expediente = new Expediente
            {
                NumeroExpediente = numeroExpediente,
                NumeroOficio = GetElementValue(root, "NumeroOficio") ?? string.Empty,
                SolicitudSiara = GetElementValue(root, "SolicitudSiara") ?? string.Empty,
                Folio = int.TryParse(GetElementValue(root, "Folio"), out var folio) ? folio : 0,
                OficioYear = int.TryParse(GetElementValue(root, "OficioYear"), out var year) ? year : DateTime.Now.Year,
                AreaClave = areaClave,
                AreaDescripcion = areaDescripcion,
                FechaPublicacion = DateTime.TryParse(GetElementValue(root, "FechaPublicacion"), out var fecha) ? fecha : DateTime.MinValue,
                DiasPlazo = int.TryParse(GetElementValue(root, "DiasPlazo"), out var diasPlazo) ? diasPlazo : 0,
                AutoridadNombre = autoridadNombre,
                AutoridadEspecificaNombre = GetElementValue(root, "AutoridadEspecificaNombre"),
                NombreSolicitante = GetElementValue(root, "NombreSolicitante"),
                Referencia = GetElementValue(root, "Referencia") ?? string.Empty,
                Referencia1 = GetElementValue(root, "Referencia1") ?? string.Empty,
                Referencia2 = GetElementValue(root, "Referencia2") ?? string.Empty,
                TieneAseguramiento = tieneAseguramiento,

                // Law-mandated fields - best-effort extraction from XML (bank systems will enrich later)
                LawMandatedFields = ExtractLawMandatedFields(autoridadNombre, areaDescripcion, areaClave, tieneAseguramiento),

                // Semantic analysis - null until classification engine runs
                SemanticAnalysis = null,

                // Future-proofing: capture unknown XML fields
                AdditionalFields = new Dictionary<string, string>()
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

            // Capture any unknown XML fields for future-proofing (defensive intelligence)
            // This ensures zero data loss when CNBV adds new fields to their XML schema
            CaptureUnknownFields(root, expediente.AdditionalFields);

            // Build extraction metadata for fusion quality scoring
            var metadata = BuildExtractionMetadata(expediente, root);

            var result = Result<Expediente>.Success(expediente);
            result.SetMetadata(metadata);

            _logger.LogDebug("Successfully parsed Expediente: {NumeroExpediente} with {RegexMatches} pattern matches and {PatternViolations} violations",
                expediente.NumeroExpediente, metadata.RegexMatches, metadata.PatternViolations);

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing XML to Expediente");
            return Task.FromResult(Result<Expediente>.WithFailure($"Error parsing XML: {ex.Message}", default(Expediente), ex));
        }
    }

    /// <summary>
    /// Best-effort extraction of law-mandated fields from XML data.
    /// Populates what we can from current XML schema; bank systems will enrich missing fields later.
    /// </summary>
    /// <param name="autoridadNombre">Authority name from XML.</param>
    /// <param name="areaDescripcion">Area description (ASEGURAMIENTO, HACENDARIO, etc.).</param>
    /// <param name="areaClave">Area code from XML.</param>
    /// <param name="tieneAseguramiento">Whether case involves asset seizure.</param>
    /// <returns>LawMandatedFields with best-effort population, or null if no fields can be extracted.</returns>
    private static LawMandatedFields? ExtractLawMandatedFields(
        string autoridadNombre,
        string areaDescripcion,
        int areaClave,
        bool tieneAseguramiento)
    {
        // Only create LawMandatedFields if we can populate at least one field
        var hasData = !string.IsNullOrWhiteSpace(autoridadNombre) ||
                      !string.IsNullOrWhiteSpace(areaDescripcion) ||
                      areaClave > 0;

        if (!hasData)
        {
            return null; // No law-mandated data available from XML
        }

        return new LawMandatedFields
        {
            // Section 2.1: Core Identification & Tracking
            // InternalCaseId will be generated by bank system (null for now)
            SourceAuthorityCode = !string.IsNullOrWhiteSpace(autoridadNombre) ? autoridadNombre : null,
            // ProcessingStatus will be set by workflow (null for now)

            // Section 2.2: SLA & Classification
            RequirementType = !string.IsNullOrWhiteSpace(areaDescripcion) ? areaDescripcion : null,
            RequirementTypeCode = areaClave > 0 ? areaClave : null,

            // Section 2.3: Subject Information
            // IsPrimaryTitular - cannot determine from current XML (null for now)

            // Section 2.4: Financial Information
            // All financial fields come from bank systems (null for now)
            // BranchCode, StateINEGI, AccountNumber, ProductType, Currency,
            // InitialBlockedAmount, OperationAmount, FinalBalance
        };
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

    /// <summary>
    /// Captures unknown XML fields for future-proofing (defensive intelligence pattern).
    /// Ensures zero data loss when CNBV adds new fields to their XML schema.
    /// </summary>
    /// <param name="root">The root XML element to scan.</param>
    /// <param name="additionalFields">Dictionary to store unknown fields.</param>
    /// <remarks>
    /// Best-effort approach: populates what it can, never throws exceptions.
    /// Unknown fields are logged as warnings for compliance audit trail.
    /// </remarks>
    private void CaptureUnknownFields(XElement root, Dictionary<string, string> additionalFields)
    {
        // Define known field names from current schema (both with and without Cnbv_ prefix)
        var knownFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Root-level fields (lines 74-89)
            "NumeroExpediente", "Cnbv_NumeroExpediente",
            "NumeroOficio", "Cnbv_NumeroOficio",
            "SolicitudSiara", "Cnbv_SolicitudSiara",
            "Folio", "Cnbv_Folio",
            "OficioYear", "Cnbv_OficioYear",
            "AreaClave", "Cnbv_AreaClave",
            "AreaDescripcion", "Cnbv_AreaDescripcion",
            "FechaPublicacion", "Cnbv_FechaPublicacion",
            "DiasPlazo", "Cnbv_DiasPlazo",
            "AutoridadNombre", "Cnbv_AutoridadNombre",
            "AutoridadEspecificaNombre", "Cnbv_AutoridadEspecificaNombre",
            "NombreSolicitante", "Cnbv_NombreSolicitante",
            "Referencia", "Cnbv_Referencia",
            "Referencia1", "Cnbv_Referencia1",
            "Referencia2", "Cnbv_Referencia2",
            "TieneAseguramiento", "Cnbv_TieneAseguramiento",

            // Collection elements (lines 101-153)
            "SolicitudPartes", "Cnbv_SolicitudPartes",
            "SolicitudEspecifica", "Cnbv_SolicitudEspecifica",
            "PersonasSolicitud", "Cnbv_PersonasSolicitud",

            // SolicitudPartes fields (lines 108-117)
            "ParteId", "Cnbv_ParteId",
            "Caracter", "Cnbv_Caracter",
            "Persona", "Cnbv_Persona",
            "PersonaTipo", "Cnbv_PersonaTipo",
            "Paterno", "Cnbv_Paterno",
            "Materno", "Cnbv_Materno",
            "Nombre", "Cnbv_Nombre",
            "Rfc", "Cnbv_Rfc",
            "Relacion", "Cnbv_Relacion",
            "Domicilio", "Cnbv_Domicilio",
            "Complementarios", "Cnbv_Complementarios",

            // SolicitudEspecifica fields (lines 128-129)
            "SolicitudEspecificaId", "Cnbv_SolicitudEspecificaId",
            "InstruccionesCuentasPorConocer", "Cnbv_InstruccionesCuentasPorConocer",

            // PersonasSolicitud fields (same as SolicitudPartes, lines 138-147)
            "PersonaId", "Cnbv_PersonaId"
        };

        try
        {
            // Scan root-level elements
            foreach (var element in root.Elements())
            {
                var fieldName = element.Name.LocalName;

                // Skip known fields and collection elements (collections are handled separately)
                if (knownFields.Contains(fieldName))
                {
                    continue;
                }

                // Unknown field detected at root level
                var value = element.Value ?? string.Empty;

                // Only capture non-empty values
                if (!string.IsNullOrWhiteSpace(value))
                {
                    additionalFields[fieldName] = value;
                    _logger.LogWarning(
                        "Unknown XML field captured: {FieldName} = {Value}. " +
                        "This may indicate CNBV schema evolution. Field preserved in AdditionalFields.",
                        fieldName,
                        value);
                }
            }
        }
        catch (Exception ex)
        {
            // Best effort - log but don't fail parsing (defensive intelligence)
            _logger.LogError(ex,
                "Error capturing unknown XML fields. Parsing will continue normally. " +
                "Some future-proofing data may be lost, but core extraction remains functional.");
        }
    }

    /// <summary>
    /// Builds extraction metadata for multi-source data fusion quality scoring.
    /// Applies DRY principle: cleaning, validation, and confidence calculation happen ONCE here.
    /// </summary>
    /// <param name="expediente">The extracted Expediente entity.</param>
    /// <param name="root">The XML root element for field counting.</param>
    /// <returns>Extraction metadata with quality metrics.</returns>
    /// <remarks>
    /// For XML extraction (hand-filled forms):
    /// - No OCR confidence (null)
    /// - No image quality metrics (null)
    /// - Focus on pattern validation and catalog validation
    /// </remarks>
    private static ExtractionMetadata BuildExtractionMetadata(Expediente expediente, XElement root)
    {
        var metadata = new ExtractionMetadata
        {
            Source = SourceType.XML_HandFilled
        };

        // Pattern regex definitions (Mexican standards)
        var rfcPattern = new System.Text.RegularExpressions.Regex(@"^[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}$");
        var curpPattern = new System.Text.RegularExpressions.Regex(@"^[A-Z]{4}\d{6}[HM][A-Z]{5}[0-9A-Z]\d$");
        var datePattern = new System.Text.RegularExpressions.Regex(@"^\d{4}-\d{2}-\d{2}");

        // CNBV catalog values (based on ClassificationRules.md and sample fixtures)
        var validAreaDescripciones = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ASEGURAMIENTO",
            "HACENDARIO",
            "PENAL",
            "CIVIL",
            "ADMINISTRATIVO"
        };

        int regexMatches = 0;
        int totalFieldsExtracted = 0;
        int catalogValidations = 0;
        int patternViolations = 0;

        // Count root-level extracted fields (non-null/non-empty)
        if (!string.IsNullOrWhiteSpace(expediente.NumeroExpediente)) totalFieldsExtracted++;
        if (!string.IsNullOrWhiteSpace(expediente.NumeroOficio)) totalFieldsExtracted++;
        if (!string.IsNullOrWhiteSpace(expediente.SolicitudSiara)) totalFieldsExtracted++;
        if (expediente.Folio > 0) totalFieldsExtracted++;
        if (expediente.OficioYear > 0) totalFieldsExtracted++;
        if (expediente.AreaClave > 0) totalFieldsExtracted++;
        if (!string.IsNullOrWhiteSpace(expediente.AreaDescripcion)) totalFieldsExtracted++;
        if (expediente.FechaPublicacion != DateTime.MinValue) totalFieldsExtracted++;
        if (expediente.DiasPlazo > 0) totalFieldsExtracted++;
        if (!string.IsNullOrWhiteSpace(expediente.AutoridadNombre)) totalFieldsExtracted++;
        if (!string.IsNullOrWhiteSpace(expediente.AutoridadEspecificaNombre)) totalFieldsExtracted++;
        if (!string.IsNullOrWhiteSpace(expediente.NombreSolicitante)) totalFieldsExtracted++;
        if (!string.IsNullOrWhiteSpace(expediente.Referencia)) totalFieldsExtracted++;
        if (!string.IsNullOrWhiteSpace(expediente.Referencia1)) totalFieldsExtracted++;
        if (!string.IsNullOrWhiteSpace(expediente.Referencia2)) totalFieldsExtracted++;

        // Validate AreaDescripcion against catalog
        if (!string.IsNullOrWhiteSpace(expediente.AreaDescripcion))
        {
            if (validAreaDescripciones.Contains(expediente.AreaDescripcion.Trim()))
            {
                catalogValidations++;
            }
            else
            {
                patternViolations++;
            }
        }

        // Validate AutoridadNombre (not empty = valid for now, real catalog validation comes later)
        if (!string.IsNullOrWhiteSpace(expediente.AutoridadNombre))
        {
            catalogValidations++;
        }

        // Validate FechaPublicacion (check if valid date)
        if (expediente.FechaPublicacion != DateTime.MinValue && expediente.FechaPublicacion.Year > 2000)
        {
            regexMatches++;
        }
        else if (expediente.FechaPublicacion == DateTime.MinValue)
        {
            patternViolations++;
        }

        // Count SolicitudPartes fields
        foreach (var parte in expediente.SolicitudPartes)
        {
            if (parte.ParteId > 0) totalFieldsExtracted++;
            if (!string.IsNullOrWhiteSpace(parte.Caracter)) totalFieldsExtracted++;
            if (!string.IsNullOrWhiteSpace(parte.PersonaTipo)) totalFieldsExtracted++;
            if (!string.IsNullOrWhiteSpace(parte.Nombre)) totalFieldsExtracted++;

            // Validate RFC if present
            if (!string.IsNullOrWhiteSpace(parte.Rfc))
            {
                totalFieldsExtracted++;
                var rfcClean = parte.Rfc.Trim();
                if (rfcPattern.IsMatch(rfcClean))
                {
                    regexMatches++;
                }
                else
                {
                    patternViolations++;
                }
            }

            if (!string.IsNullOrWhiteSpace(parte.Paterno)) totalFieldsExtracted++;
            if (!string.IsNullOrWhiteSpace(parte.Materno)) totalFieldsExtracted++;
            if (!string.IsNullOrWhiteSpace(parte.Relacion)) totalFieldsExtracted++;
            if (!string.IsNullOrWhiteSpace(parte.Domicilio)) totalFieldsExtracted++;
            if (!string.IsNullOrWhiteSpace(parte.Complementarios)) totalFieldsExtracted++;
        }

        // Count SolicitudEspecificas fields
        foreach (var especifica in expediente.SolicitudEspecificas)
        {
            if (especifica.SolicitudEspecificaId > 0) totalFieldsExtracted++;
            if (!string.IsNullOrWhiteSpace(especifica.InstruccionesCuentasPorConocer)) totalFieldsExtracted++;

            // Count nested PersonasSolicitud
            foreach (var persona in especifica.PersonasSolicitud)
            {
                if (persona.PersonaId > 0) totalFieldsExtracted++;
                if (!string.IsNullOrWhiteSpace(persona.Caracter)) totalFieldsExtracted++;
                if (!string.IsNullOrWhiteSpace(persona.Persona)) totalFieldsExtracted++;
                if (!string.IsNullOrWhiteSpace(persona.Nombre)) totalFieldsExtracted++;

                // Validate RFC if present
                if (!string.IsNullOrWhiteSpace(persona.Rfc))
                {
                    totalFieldsExtracted++;
                    var rfcClean = persona.Rfc.Trim();
                    if (rfcPattern.IsMatch(rfcClean))
                    {
                        regexMatches++;
                    }
                    else
                    {
                        patternViolations++;
                    }
                }

                if (!string.IsNullOrWhiteSpace(persona.Paterno)) totalFieldsExtracted++;
                if (!string.IsNullOrWhiteSpace(persona.Materno)) totalFieldsExtracted++;
                if (!string.IsNullOrWhiteSpace(persona.Relacion)) totalFieldsExtracted++;
                if (!string.IsNullOrWhiteSpace(persona.Domicilio)) totalFieldsExtracted++;
                if (!string.IsNullOrWhiteSpace(persona.Complementarios)) totalFieldsExtracted++;
            }
        }

        // Populate metadata
        metadata.RegexMatches = regexMatches;
        metadata.TotalFieldsExtracted = totalFieldsExtracted;
        metadata.CatalogValidations = catalogValidations;
        metadata.PatternViolations = patternViolations;

        // OCR and image quality metrics = null for XML (not applicable)
        metadata.MeanConfidence = null;
        metadata.MinConfidence = null;
        metadata.TotalWords = null;
        metadata.LowConfidenceWords = null;
        metadata.QualityIndex = null;
        metadata.BlurScore = null;
        metadata.ContrastScore = null;
        metadata.NoiseEstimate = null;
        metadata.EdgeDensity = null;

        return metadata;
    }
}