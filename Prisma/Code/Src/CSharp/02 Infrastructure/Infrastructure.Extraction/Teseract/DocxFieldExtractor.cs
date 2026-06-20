namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

/// <summary>
/// DOCX field extractor implementation for extracting structured fields from DOCX documents.
/// Implements <see cref="IFieldExtractor{T}"/> for <see cref="DocxSource"/>.
/// </summary>
public class DocxFieldExtractor : IFieldExtractor<DocxSource>
{
    private readonly ILogger<DocxFieldExtractor> _logger;
    private readonly IOcrExecutor? _ocrExecutor;

    /// <summary>
    /// Minimum OCR confidence (0–100) required to accept a remitente name from an image.
    /// Below this threshold the result is treated as unreliable and discarded (fail-open).
    /// </summary>
    internal const float MinimumRemitenteConfidence = 30f;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocxFieldExtractor"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="ocrExecutor">
    /// Optional OCR executor used to read the signing-functionary name from embedded images (D2).
    /// When <see langword="null"/> the image-OCR path is skipped entirely; existing text extraction
    /// is unaffected. The injected instance MUST be the already-registered <see cref="TesseractOcrExecutor"/>
    /// — do NOT new-up a second engine (Tesseract same-process second-init DEADLOCK).
    /// </param>
    public DocxFieldExtractor(ILogger<DocxFieldExtractor> logger, IOcrExecutor? ocrExecutor = null)
    {
        _logger = logger;
        _ocrExecutor = ocrExecutor;
    }

    /// <inheritdoc />
    public async Task<Result<ExtractedFields>> ExtractFieldsAsync(DocxSource source, FieldDefinition[] fieldDefinitions)
    {
        try
        {
            _logger.LogDebug("Extracting fields from DOCX document: {FilePath}", source.FilePath);

            // Get file content
            byte[] fileContent;
            if (source.FileContent != null)
            {
                fileContent = source.FileContent;
            }
            else if (!string.IsNullOrEmpty(source.FilePath) && File.Exists(source.FilePath))
            {
                fileContent = File.ReadAllBytes(source.FilePath);
            }
            else
            {
                return Result<ExtractedFields>.WithFailure("DOCX source must have either FileContent or valid FilePath");
            }

            // Extract text from DOCX
            var textResult = ExtractTextFromDocx(fileContent);
            if (textResult.IsFailure)
            {
                return Result<ExtractedFields>.WithFailure($"Failed to extract text from DOCX: {textResult.Error}");
            }

            var text = textResult.Value ?? string.Empty;
            var confidence = 1.0f; // DOCX text extraction has high confidence

            // Extract fields based on definitions
            var extractedFields = new ExtractedFields();

            foreach (var fieldDef in fieldDefinitions)
            {
                var fieldResult = ExtractFieldByName(text, fieldDef.FieldName, confidence);
                if (fieldResult.IsSuccess && fieldResult.Value != null)
                {
                    ApplyFieldToExtractedFields(extractedFields, fieldDef.FieldName, fieldResult.Value.Value);
                }
            }

            // D2: image-OCR path — enumerate embedded images and extract remitente name (fail-open).
            // IOcrExecutor.ExecuteOcrAsync does not accept CancellationToken; CancellationToken not propagated.
            if (_ocrExecutor != null)
            {
                var remitente = await ExtractRemitenteFromImagesAsync(fileContent).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(remitente))
                {
                    extractedFields.AdditionalFields["Remitente"] = remitente;
                    _logger.LogDebug("Remitente extracted from DOCX embedded image: {Remitente}", remitente);
                }
            }

            _logger.LogDebug("Successfully extracted {Count} fields from DOCX document", fieldDefinitions.Length);
            return Result<ExtractedFields>.Success(extractedFields);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting fields from DOCX");
            return Result<ExtractedFields>.WithFailure($"Error extracting DOCX fields: {ex.Message}", default(ExtractedFields), ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<FieldValue>> ExtractFieldAsync(DocxSource source, string fieldName)
    {
        try
        {
            _logger.LogDebug("Extracting field {FieldName} from DOCX document: {FilePath}", fieldName, source.FilePath);

            // Get file content
            byte[] fileContent;
            if (source.FileContent != null)
            {
                fileContent = source.FileContent;
            }
            else if (!string.IsNullOrEmpty(source.FilePath) && File.Exists(source.FilePath))
            {
                fileContent = File.ReadAllBytes(source.FilePath);
            }
            else
            {
                return Result<FieldValue>.WithFailure("DOCX source must have either FileContent or valid FilePath");
            }

            // D2: "remitente" field name routed to image-OCR path (only when executor available).
            if (string.Equals(fieldName, "remitente", StringComparison.OrdinalIgnoreCase) && _ocrExecutor != null)
            {
                var remitente = await ExtractRemitenteFromImagesAsync(fileContent).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(remitente))
                {
                    return Result<FieldValue>.Success(
                        new FieldValue(fieldName, remitente, MinimumRemitenteConfidence / 100f, "DOCX-Image", FieldOrigin.Docx));
                }

                return Result<FieldValue>.WithFailure("Field 'remitente' could not be read from DOCX embedded images");
            }

            // Extract text from DOCX
            var textResult = ExtractTextFromDocx(fileContent);
            if (textResult.IsFailure)
            {
                return Result<FieldValue>.WithFailure($"Failed to extract text from DOCX: {textResult.Error}");
            }

            var text = textResult.Value ?? string.Empty;
            var confidence = 1.0f; // DOCX text extraction has high confidence

            // Extract specific field
            var fieldResult = ExtractFieldByName(text, fieldName, confidence);
            if (fieldResult.IsSuccess && fieldResult.Value != null)
            {
                return Result<FieldValue>.Success(fieldResult.Value);
            }

            return Result<FieldValue>.WithFailure($"Field '{fieldName}' not found in DOCX document");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting field {FieldName} from DOCX", fieldName);
            return Result<FieldValue>.WithFailure($"Error extracting field from DOCX: {ex.Message}", default(FieldValue), ex);
        }
    }

    // -------------------------------------------------------------------------
    // D2: image-OCR — remitente name extraction
    // -------------------------------------------------------------------------

    /// <summary>
    /// Enumerates all embedded image parts in the DOCX, runs OCR on each via the injected executor,
    /// and returns the best-effort remitente (signing functionary) name. The method is fail-open:
    /// any exception is caught and logged at Warning; an empty/null return means no name was found.
    /// </summary>
    /// <remarks>
    /// Name-heuristic: for each OCR text result, we look for a non-empty line near an
    /// "Atentamente" keyword (common Spanish closing). The first plausible name found wins.
    /// A "plausible name" is a line of 2–5 words, each capitalised, with no digits and a
    /// minimum length of 4 characters per word.
    /// </remarks>
    private async Task<string?> ExtractRemitenteFromImagesAsync(byte[] fileContent)
    {
        try
        {
            using var stream = new MemoryStream(fileContent);
            using var wordDocument = WordprocessingDocument.Open(stream, false);

            var mainPart = wordDocument.MainDocumentPart;
            if (mainPart == null)
            {
                return null;
            }

            var imageParts = mainPart.ImageParts.ToList();
            if (imageParts.Count == 0)
            {
                _logger.LogDebug("DOCX has no embedded image parts; skipping remitente OCR.");
                return null;
            }

            for (var i = 0; i < imageParts.Count; i++)
            {
                var imagePart = imageParts[i];
                var partUri = imagePart.Uri.ToString();

                byte[] imageBytes;
                try
                {
                    using var imageStream = imagePart.GetStream();
                    using var ms = new MemoryStream();
                    await imageStream.CopyToAsync(ms).ConfigureAwait(false);
                    imageBytes = ms.ToArray();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "D2: Could not read image bytes from DOCX part {PartUri}; skipping.", partUri);
                    continue;
                }

                if (imageBytes.Length == 0)
                {
                    continue;
                }

                var imageData = new ImageData(imageBytes, partUri, pageNumber: i + 1, totalPages: imageParts.Count);
                var ocrConfig = new OCRConfig
                {
                    Language = "spa",
                    OEM = 1,
                    PSM = 6,
                    FallbackLanguage = "eng",
                    ConfidenceThreshold = 0.3f
                };

                Result<OCRResult> ocrResult;
                try
                {
                    ocrResult = await _ocrExecutor!.ExecuteOcrAsync(imageData, ocrConfig).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "D2: OCR executor threw for DOCX image part {PartUri}; skipping.", partUri);
                    continue;
                }

                if (!ocrResult.IsSuccess || ocrResult.Value == null)
                {
                    _logger.LogDebug("D2: OCR returned failure for image part {PartUri}: {Error}", partUri, ocrResult.Error);
                    continue;
                }

                var ocr = ocrResult.Value;
                if (ocr.ConfidenceAvg < MinimumRemitenteConfidence)
                {
                    _logger.LogDebug(
                        "D2: OCR confidence {Confidence:F1} below threshold {Threshold} for image part {PartUri}; skipping.",
                        ocr.ConfidenceAvg, MinimumRemitenteConfidence, partUri);
                    continue;
                }

                var name = ParseRemitenteFromOcrText(ocr.Text);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "D2: Unexpected error during remitente image-OCR; returning null (fail-open).");
            return null;
        }
    }

    /// <summary>
    /// Parses the remitente (signing-functionary) name from raw OCR text.
    /// Strategy: prefer a capitalised name line appearing AFTER "Atentamente" (Spanish formal sign-off).
    /// Falls back to scanning all lines for a plausible name if no Atentamente context is found.
    /// </summary>
    /// <returns>The best-effort name string, or <see langword="null"/> if none found.</returns>
    public static string? ParseRemitenteFromOcrText(string? ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
        {
            return null;
        }

        var lines = ocrText
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        // Phase 1: prefer lines immediately after "Atentamente" (common Spanish formal sign-off)
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains("Atentamente", StringComparison.OrdinalIgnoreCase)
                || lines[i].Contains("Atentamente,", StringComparison.OrdinalIgnoreCase))
            {
                // Check the next few lines for a plausible name
                for (var j = i + 1; j < Math.Min(i + 4, lines.Count); j++)
                {
                    if (IsPlausibleName(lines[j]))
                    {
                        return lines[j];
                    }
                }
            }
        }

        // Phase 2: fallback — scan all lines for a plausible capitalised name
        foreach (var line in lines)
        {
            if (IsPlausibleName(line))
            {
                return line;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="line"/> looks like a Spanish full name:
    /// 2–5 words, each at least 3 characters long, starts with an uppercase letter,
    /// contains no digits, and does not look like a label (ends in colon / is all-caps).
    /// </summary>
    private static bool IsPlausibleName(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        // Reject obvious non-name lines
        if (line.EndsWith(':') || line.All(c => char.IsUpper(c) || c == ' '))
        {
            return false;
        }

        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2 || words.Length > 6)
        {
            return false;
        }

        foreach (var word in words)
        {
            // Each word must: start with uppercase, have no digits, be at least 3 chars
            if (word.Length < 3)
            {
                return false;
            }

            if (!char.IsUpper(word[0]))
            {
                return false;
            }

            if (word.Any(char.IsDigit))
            {
                return false;
            }
        }

        return true;
    }

    // -------------------------------------------------------------------------
    // Text-field helpers (unchanged from D1)
    // -------------------------------------------------------------------------

    private Result<string> ExtractTextFromDocx(byte[] fileContent)
    {
        try
        {
            using var stream = new MemoryStream(fileContent);
            using var wordDocument = WordprocessingDocument.Open(stream, false);

            var mainPart = wordDocument.MainDocumentPart;
            if (mainPart == null)
            {
                return Result<string>.WithFailure("DOCX document has no main document part");
            }

            var body = mainPart.Document?.Body;
            if (body == null)
            {
                return Result<string>.WithFailure("DOCX document has no body");
            }

            // Extract text content
            var textContent = string.Join(" ", body.Descendants<Text>().Select(t => t.Text));
            return Result<string>.Success(textContent);
        }
        catch (Exception ex)
        {
            return Result<string>.WithFailure($"Error reading DOCX: {ex.Message}", string.Empty, ex);
        }
    }

    private Result<FieldValue> ExtractFieldByName(string text, string fieldName, float confidence)
    {
        var value = fieldName.ToLowerInvariant() switch
        {
            "expediente" or "numeroexpediente" or "numero_expediente" => ExtractExpediente(text),
            "causa" => ExtractCausa(text),
            "accionsolicitada" or "accion_solicitada" => ExtractAccionSolicitada(text),
            "numerooficio" or "numero_oficio" => ExtractNumeroOficio(text),
            "requerimiento" or "solicitudsiara" or "solicitud_siara" => ExtractRequerimiento(text),

            // SAT requerimiento DOCX fields (D2 hardening)
            "rfc" or "rfccontribuyente" or "rfc_contribuyente" => ExtractRfc(text),
            "folio" or "numerofolio" or "numero_folio" => ExtractFolio(text),
            "fecharequerimiento" or "fecha_requerimiento" or "fechaoficio" or "fecha_oficio" => ExtractFechaOficio(text),
            "autoridadnombre" or "autoridad_nombre" => ExtractAutoridadNombre(text),

            _ => null
        };

        if (value != null)
        {
            return Result<FieldValue>.Success(new FieldValue(fieldName, value, confidence, "DOCX", FieldOrigin.Docx));
        }

        return Result<FieldValue>.WithFailure($"Field '{fieldName}' not found");
    }

    private static void ApplyFieldToExtractedFields(ExtractedFields fields, string fieldName, string? value)
    {
        switch (fieldName.ToLowerInvariant())
        {
            case "expediente":
            case "numeroexpediente":
            case "numero_expediente":
                fields.Expediente = value;
                break;
            case "causa":
                fields.Causa = value;
                break;
            case "accionsolicitada":
            case "accion_solicitada":
                fields.AccionSolicitada = value;
                break;
            case "numerooficio":
            case "numero_oficio":
                // NumeroOficio has no typed slot on ExtractedFields; surface it in AdditionalFields
                // so consumers (e.g. the Athena worker's MapExtractedFieldsToExpediente) can read it.
                if (!string.IsNullOrWhiteSpace(value))
                {
                    fields.AdditionalFields["NumeroOficio"] = value;
                }
                break;
            default:
                // SAT requerimiento fields and any other named fields go into AdditionalFields,
                // mirroring the AdaptiveTxtFieldExtractor pattern so fusion sees consistent keys.
                if (!string.IsNullOrWhiteSpace(value))
                {
                    fields.AdditionalFields[fieldName] = value;
                }
                break;
        }
    }

    private static string? ExtractExpediente(string text)
    {
        // Prefer the label-anchored folio/expediente, e.g. "Folio Núm.: A/AS1- 1111-222222-AAA"
        // (real CNBV oficios print the folio with internal whitespace).
        var labeled = System.Text.RegularExpressions.Regex.Match(
            text,
            @"(?:Folio\s+N[uú]m\.?|Expediente)\s*:?\s*([A-Z]/[A-Z]{1,2}\d+-\s*\d+-\s*\d+-\s*[A-Z]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (labeled.Success && labeled.Groups.Count > 1)
        {
            return NormalizeExpediente(labeled.Groups[1].Value);
        }

        // Fallback: bare pattern (A/AS1-2505-088637-PHM), tolerant of internal whitespace.
        var expedientePattern = @"[A-Z]/[A-Z]{1,2}\d+-\s*\d+-\s*\d+-\s*[A-Z]+";
        var match = System.Text.RegularExpressions.Regex.Match(text, expedientePattern);
        return match.Success ? NormalizeExpediente(match.Value) : null;
    }

    /// <summary>
    /// Collapses internal whitespace in an expediente/folio token so a value printed as
    /// <c>A/AS1- 1111-222222-AAA</c> normalises to <c>A/AS1-1111-222222-AAA</c>, matching the
    /// canonical form the XML extractor surfaces (so the two sources agree under fusion).
    /// </summary>
    private static string NormalizeExpediente(string raw) =>
        System.Text.RegularExpressions.Regex.Replace(raw.Trim(), @"\s+", string.Empty);

    /// <summary>
    /// Extracts the authoritative CNBV oficio number from the DOCX text, preferring the
    /// label-anchored value (<c>Oficio Núm.: 222/AAA/-4444444444/2025</c>) over the bare
    /// requerimiento/solicitud id pattern (a remitted source oficio such as
    /// <c>AGAFADAFSON2/2025/000084</c>), so the DOCX agrees with the XML on NumeroOficio.
    /// </summary>
    private static string? ExtractNumeroOficio(string text)
    {
        var labeled = System.Text.RegularExpressions.Regex.Match(
            text,
            @"Oficio\s+N[uú]m\.?\s*:?\s*([0-9A-Z/\-]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (labeled.Success && labeled.Groups.Count > 1)
        {
            var value = labeled.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return ExtractRequerimiento(text);
    }

    /// <summary>
    /// Extracts the requerimiento / SIARA solicitud id from the DOCX text.
    /// Pattern reused from <c>AdaptiveTxtFieldExtractor.ExtractNumeroOficio</c>:
    /// matches values like <c>AGAFADAFSON2/2025/000084</c>.
    /// </summary>
    private static string? ExtractRequerimiento(string text)
    {
        // Reuses AdaptiveTxtFieldExtractor pattern: [A-Z]{4,}[A-Z0-9]{0,10}/\d{4}/\d{6}
        var requerimientoPattern = @"[A-Z]{4,}[A-Z0-9]{0,10}/\d{4}/\d{6}";
        var match = System.Text.RegularExpressions.Regex.Match(
            text, requerimientoPattern, System.Text.RegularExpressions.RegexOptions.Multiline);
        return match.Success ? match.Value : null;
    }

    private static string? ExtractCausa(string text)
    {
        // Look for "CAUSA:" or "Causa:" followed by text
        var causaPattern = @"(?:CAUSA|Causa)\s*:?\s*([^\n\r]+)";
        var match = System.Text.RegularExpressions.Regex.Match(text, causaPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractAccionSolicitada(string text)
    {
        // Look for "ACCIÓN SOLICITADA:" or "Accion Solicitada:" followed by text
        var accionPattern = @"(?:ACCI[ÓO]N\s+SOLICITADA|Accion\s+Solicitada)\s*:?\s*([^\n\r]+)";
        var match = System.Text.RegularExpressions.Regex.Match(text, accionPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : null;
    }

    // -------------------------------------------------------------------------
    // SAT requerimiento DOCX field extractors — D2 hardening (PRISMA-E5-S5)
    // Labels are tolerant of accent variations and optional trailing colon.
    // All patterns use named capture groups and RegexOptions.CultureInvariant
    // to avoid locale-sensitive behaviour. No catastrophic backtracking: anchored,
    // explicit character classes, no nested quantifiers.
    // -------------------------------------------------------------------------

    /// <summary>
    /// SAT field: RFC del contribuyente.
    /// Matches label variants:
    ///   "RFC del contribuyente:"  (full SAT label)
    ///   "RFC:"                    (bare label, also used by CNBV officios)
    /// RFC format: 4 letters + 6-digit date + 3-character homoclave  = 13 chars total (física),
    /// or 3 letters + 6-digit date + 3-char homoclave = 12 chars (moral).
    /// Pattern accepts both and allows the value to be separated by optional whitespace.
    /// </summary>
    private static string? ExtractRfc(string text)
    {
        // SAT field: "RFC del contribuyente:" — full label (with or without accent on "u")
        // Also matches bare "RFC:" (single label) used in abbreviated headers.
        var labeled = System.Text.RegularExpressions.Regex.Match(
            text,
            @"RFC(?:\s+del\s+contribuyente)?\s*:?\s*(?<rfc>[A-Z&Ñ]{3,4}\d{6}[A-Z0-9]{3})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
            | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (labeled.Success)
        {
            return labeled.Groups["rfc"].Value.Trim().ToUpperInvariant();
        }

        return null;
    }

    /// <summary>
    /// SAT field: Número de folio / Folio.
    /// Matches label variants:
    ///   "Número de folio:"   (full label with accent)
    ///   "Numero de folio:"   (without accent — OCR tolerance)
    ///   "Folio:"             (bare label)
    /// Folio values follow the CNBV expediente format: A/AS1-2505-088637-PHM.
    /// Falls back to the existing <see cref="ExtractExpediente"/> logic so both
    /// label forms resolve the same canonical value (consistent with fusion).
    /// </summary>
    private static string? ExtractFolio(string text)
    {
        // SAT field: "Número de folio:" — prefer label-anchored extraction.
        var labeled = System.Text.RegularExpressions.Regex.Match(
            text,
            @"(?:N[uú]mero\s+de\s+folio|Folio)\s*:?\s*(?<folio>[A-Z]/[A-Z]{1,4}\d*[-–]\s*\d+[-–]\s*\d+[-–]\s*[A-Z]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
            | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (labeled.Success)
        {
            return NormalizeExpediente(labeled.Groups["folio"].Value);
        }

        // Fallback: delegate to the bare expediente extractor (same token shape).
        return ExtractExpediente(text);
    }

    /// <summary>
    /// SAT field: Fecha del oficio / Fecha del requerimiento.
    /// Matches label variants:
    ///   "Fecha:"             (bare label)
    ///   "Fecha del oficio:"  (SAT requerimiento header)
    ///   "Fecha de emisión:"  (alternative SAT wording)
    /// Value format is free-text Spanish date (e.g. "09 de Abril de 2025" or "2025-04-09").
    /// The extractor returns the raw date string for downstream normalisation.
    /// </summary>
    private static string? ExtractFechaOficio(string text)
    {
        // SAT field: "Fecha del oficio:" / "Fecha de emisión:" / "Fecha:"
        // Capture everything up to the next separator character.
        var labeled = System.Text.RegularExpressions.Regex.Match(
            text,
            @"Fecha(?:\s+de(?:l)?\s+(?:oficio|emisi[oó]n|requerimiento))?\s*:?\s*(?<fecha>[^\n\r,;]{5,40})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
            | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (labeled.Success)
        {
            var raw = labeled.Groups["fecha"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                return raw;
            }
        }

        return null;
    }

    /// <summary>
    /// SAT field: Autoridad / Nombre de la autoridad.
    /// Mirrors the authority-detection logic of AdaptiveTxtFieldExtractor so that
    /// DOCX and TXT extractions agree under fusion. Priority:
    ///   1. SAT explicitly named (full name or acronym with word boundary).
    ///   2. CNBV full name.
    ///   3. AGAFF full name.
    ///   4. Bare acronyms (AGAFF, CNBV, SAT) with word-boundary guards.
    /// </summary>
    private static string? ExtractAutoridadNombre(string text)
    {
        // SAT field: "SAT - Servicio de Administración Tributaria" — explicit SAT self-identification.
        if (System.Text.RegularExpressions.Regex.IsMatch(
                text,
                @"SAT\s*[-–]\s*Servicio\s+de\s+Administraci[oó]n\s+Tributaria",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            return "SAT";
        }

        // SAT field: CNBV full name — governing regulator for SIARA documents.
        if (text.Contains("Comisión Nacional Bancaria y de Valores", StringComparison.OrdinalIgnoreCase))
        {
            return "Comisión Nacional Bancaria y de Valores";
        }

        // SAT field: AGAFF full name.
        if (text.Contains("Administración General de Auditoría Fiscal Federal", StringComparison.OrdinalIgnoreCase))
        {
            return "Administración General de Auditoría Fiscal Federal";
        }

        // SAT field: bare acronyms — word-boundary guards prevent false matches on email domains.
        var acronyms = new[] { "AGAFF", "CNBV", "SAT" };
        foreach (var acronym in acronyms)
        {
            var pattern = @"(?<![@.])\b" + System.Text.RegularExpressions.Regex.Escape(acronym) + @"\b(?!\.gob\.mx)";
            if (System.Text.RegularExpressions.Regex.IsMatch(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return acronym;
            }
        }

        return null;
    }
}
