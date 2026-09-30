using ExxerCube.Prisma.Domain.Enums;

namespace ExxerCube.Prisma.Application.Services
{
    /// <summary>
    /// Application service for orchestrating field extraction and matching across XML, DOCX, and PDF sources.
    /// Coordinates field extraction from multiple sources, matches field values, and generates unified metadata records.
    /// </summary>
    public class FieldMatchingService : IFieldMatchingService
    {
        private readonly IFieldExtractor<DocxSource> _docxFieldExtractor;
        private readonly IFieldExtractor<PdfSource> _pdfFieldExtractor;
        private readonly IFieldExtractor<XmlSource>? _xmlFieldExtractor;
        private readonly IMatchingPolicy _matchingPolicy;
        private readonly IMatchingPolicy _nameMatchingPolicy;
        private readonly ILogger<FieldMatchingService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FieldMatchingService"/> class.
        /// </summary>
        /// <param name="docxFieldExtractor">The DOCX field extractor.</param>
        /// <param name="pdfFieldExtractor">The PDF field extractor.</param>
        /// <param name="xmlFieldExtractor">The XML field extractor (optional).</param>
        /// <param name="matchingPolicy">The general matching policy service.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="nameMatchingPolicy">The name-aware matching policy used for person/authority NAME
        /// fields (homonym disambiguation). Optional; falls back to <paramref name="matchingPolicy"/> when not
        /// supplied so existing callers/tests keep working.</param>
        public FieldMatchingService(
            IFieldExtractor<DocxSource> docxFieldExtractor,
            IFieldExtractor<PdfSource> pdfFieldExtractor,
            IFieldExtractor<XmlSource>? xmlFieldExtractor,
            IMatchingPolicy matchingPolicy,
            ILogger<FieldMatchingService> logger,
            INameMatchingPolicy? nameMatchingPolicy = null)
        {
            _docxFieldExtractor = docxFieldExtractor;
            _pdfFieldExtractor = pdfFieldExtractor;
            _xmlFieldExtractor = xmlFieldExtractor;
            _matchingPolicy = matchingPolicy;
            _nameMatchingPolicy = nameMatchingPolicy ?? matchingPolicy;
            _logger = logger;
        }

        /// <summary>Selects the matching policy for a field: name-aware for NAME fields, otherwise general.</summary>
        private IMatchingPolicy PolicyFor(string fieldName) =>
            fieldName.Contains("NOMBRE", StringComparison.OrdinalIgnoreCase) ? _nameMatchingPolicy : _matchingPolicy;

        /// <summary>
        /// Orchestrates field extraction and matching across XML, DOCX, and PDF sources, generating a unified metadata record.
        /// </summary>
        /// <param name="docxSource">The DOCX document source (optional).</param>
        /// <param name="pdfSource">The PDF document source (optional).</param>
        /// <param name="xmlSource">The XML document source (optional).</param>
        /// <param name="fieldDefinitions">The field definitions specifying which fields to extract and match.</param>
        /// <param name="expediente">The expediente information (optional, may be extracted from XML).</param>
        /// <param name="classification">The classification result (optional).</param>
        /// <param name="requiredFields">The list of required field names for validation (optional).</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A result containing the unified metadata record or an error.</returns>
        public async Task<Result<UnifiedMetadataRecord>> MatchFieldsAndGenerateUnifiedRecordAsync(
            DocxSource? docxSource,
            PdfSource? pdfSource,
            XmlSource? xmlSource,
            FieldDefinition[] fieldDefinitions,
            Expediente? expediente = null,
            ClassificationResult? classification = null,
            List<string>? requiredFields = null,
            CancellationToken cancellationToken = default)
        {
            // Check for cancellation before starting work
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Field matching workflow cancelled before starting");
                return ResultExtensions.Cancelled<UnifiedMetadataRecord>();
            }

            try
            {
                _logger.LogDebug("Starting field matching workflow across multiple sources");

                // Input validation
                if (docxSource == null && pdfSource == null && xmlSource == null)
                {
                    return Result<UnifiedMetadataRecord>.WithFailure("At least one source (DOCX, PDF, or XML) must be provided");
                }

                if (fieldDefinitions == null || fieldDefinitions.Length == 0)
                {
                    return Result<UnifiedMetadataRecord>.WithFailure("Field definitions cannot be null or empty");
                }

                // Extract fields from each source type and collect all field values
                var allFieldValues = new Dictionary<string, List<FieldValue>>();
                // Per-source extracted fields (with their origin) for non-core additional-field reconciliation.
                var perSource = new List<(ExtractedFields Fields, string SourceType)>();

                // Extract from DOCX source
                if (docxSource != null)
                {
                    // Check for cancellation before extraction
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Field matching workflow cancelled before DOCX extraction");
                        return ResultExtensions.Cancelled<UnifiedMetadataRecord>();
                    }

                    var docxExtractResult = await _docxFieldExtractor.ExtractFieldsAsync(docxSource, fieldDefinitions).ConfigureAwait(false);

                    // Propagate cancellation from dependencies
                    if (docxExtractResult.IsCancelled())
                    {
                        _logger.LogWarning("Field matching workflow cancelled by DOCX extractor");
                        return ResultExtensions.Cancelled<UnifiedMetadataRecord>();
                    }

                    if (docxExtractResult.IsSuccess && docxExtractResult.Value != null)
                    {
                        CollectFieldValues(allFieldValues, docxExtractResult.Value, "DOCX");
                        perSource.Add((docxExtractResult.Value, "DOCX"));
                    }
                    else
                    {
                        _logger.LogWarning("Failed to extract fields from DOCX: {Error}", docxExtractResult.Error);
                    }
                }

                // Extract from PDF source
                if (pdfSource != null)
                {
                    // Check for cancellation before extraction
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Field matching workflow cancelled before PDF extraction");
                        return ResultExtensions.Cancelled<UnifiedMetadataRecord>();
                    }

                    var pdfExtractResult = await _pdfFieldExtractor.ExtractFieldsAsync(pdfSource, fieldDefinitions).ConfigureAwait(false);

                    // Propagate cancellation from dependencies
                    if (pdfExtractResult.IsCancelled())
                    {
                        _logger.LogWarning("Field matching workflow cancelled by PDF extractor");
                        return ResultExtensions.Cancelled<UnifiedMetadataRecord>();
                    }

                    if (pdfExtractResult.IsSuccess && pdfExtractResult.Value != null)
                    {
                        CollectFieldValues(allFieldValues, pdfExtractResult.Value, "PDF");
                        perSource.Add((pdfExtractResult.Value, "PDF"));
                    }
                    else
                    {
                        _logger.LogWarning("Failed to extract fields from PDF: {Error}", pdfExtractResult.Error);
                    }
                }

                // Extract from XML source (if extractor available)
                if (xmlSource != null && _xmlFieldExtractor != null)
                {
                    // Check for cancellation before extraction
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Field matching workflow cancelled before XML extraction");
                        return ResultExtensions.Cancelled<UnifiedMetadataRecord>();
                    }

                    var xmlExtractResult = await _xmlFieldExtractor.ExtractFieldsAsync(xmlSource, fieldDefinitions).ConfigureAwait(false);

                    // Propagate cancellation from dependencies
                    if (xmlExtractResult.IsCancelled())
                    {
                        _logger.LogWarning("Field matching workflow cancelled by XML extractor");
                        return ResultExtensions.Cancelled<UnifiedMetadataRecord>();
                    }

                    if (xmlExtractResult.IsSuccess && xmlExtractResult.Value != null)
                    {
                        CollectFieldValues(allFieldValues, xmlExtractResult.Value, "XML");
                        perSource.Add((xmlExtractResult.Value, "XML"));
                    }
                    else
                    {
                        _logger.LogWarning("Failed to extract fields from XML: {Error}", xmlExtractResult.Error);
                    }
                }

                // Match fields across all sources using matching policy
                var matchedFields = new MatchedFields();

                foreach (var fieldDef in fieldDefinitions)
                {
                    // Check for cancellation between iterations
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Field matching workflow cancelled during matching");
                        return ResultExtensions.Cancelled<UnifiedMetadataRecord>();
                    }

                    if (allFieldValues.TryGetValue(fieldDef.FieldName, out var values) && values.Count > 0)
                    {
                        var matchResult = await PolicyFor(fieldDef.FieldName).SelectBestValueAsync(fieldDef.FieldName, values).ConfigureAwait(false);

                        // Propagate cancellation from dependencies
                        if (matchResult.IsCancelled())
                        {
                            _logger.LogWarning("Field matching workflow cancelled by matching policy");
                            return ResultExtensions.Cancelled<UnifiedMetadataRecord>();
                        }

                        if (matchResult.IsSuccess && matchResult.Value != null)
                        {
                            matchedFields.FieldMatches[fieldDef.FieldName] = matchResult.Value;

                            if (matchResult.Value.HasConflict)
                            {
                                matchedFields.ConflictingFields.Add(fieldDef.FieldName);
                            }
                        }
                    }
                    else
                    {
                        matchedFields.MissingFields.Add(fieldDef.FieldName);
                    }
                }

                // Calculate overall agreement
                if (matchedFields.FieldMatches.Count > 0)
                {
                    var agreementLevels = matchedFields.FieldMatches.Values.Select(m => m.AgreementLevel).ToList();
                    matchedFields.OverallAgreement = agreementLevels.Average();
                }

                // Reconcile NON-CORE (additional) fields across sources: select the best value per key and FLAG
                // cross-source disagreements as conflicts. These additional fields (RFC/CURP/contact/authority
                // names, etc.) are the metadata used UPSTREAM to disambiguate homonyms, so an XML-vs-OCR-vs-DOCX
                // disagreement must be surfaced, not silently swallowed. Name keys route to the name-aware policy.
                var definedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Expediente", "Causa", "AccionSolicitada", "accion_solicitada"
                };
                foreach (var fieldDef in fieldDefinitions)
                {
                    definedNames.Add(fieldDef.FieldName);
                }

                foreach (var additional in CollectAdditionalFieldValues(perSource, definedNames))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Field matching workflow cancelled during additional-field reconciliation");
                        return ResultExtensions.Cancelled<UnifiedMetadataRecord>();
                    }

                    var additionalMatch = await PolicyFor(additional.Key).SelectBestValueAsync(additional.Key, additional.Value).ConfigureAwait(false);

                    if (additionalMatch.IsCancelled())
                    {
                        _logger.LogWarning("Field matching workflow cancelled by matching policy (additional fields)");
                        return ResultExtensions.Cancelled<UnifiedMetadataRecord>();
                    }

                    if (additionalMatch.IsSuccess && additionalMatch.Value != null)
                    {
                        matchedFields.AdditionalMerged[additional.Key] = additionalMatch.Value.MatchedValue;
                        if (additionalMatch.Value.HasConflict)
                        {
                            matchedFields.AdditionalConflicts.Add(additional.Key);
                        }
                    }
                }

                // Validate completeness if required fields specified
                if (requiredFields != null && requiredFields.Count > 0)
                {
                    var missingRequired = requiredFields.Where(f => !matchedFields.FieldMatches.ContainsKey(f) || string.IsNullOrWhiteSpace(matchedFields.FieldMatches[f].MatchedValue)).ToList();
                    if (missingRequired.Count > 0)
                    {
                        _logger.LogWarning("Required fields missing or empty: {MissingFields}", string.Join(", ", missingRequired));
                    }
                }

                // Generate unified record
                var unifiedRecord = new UnifiedMetadataRecord
                {
                    Expediente = expediente,
                    ExtractedFields = CreateExtractedFieldsFromMatchedFields(matchedFields),
                    Classification = classification,
                    MatchedFields = matchedFields,
                    AdditionalFields = matchedFields.AdditionalMerged,
                    AdditionalFieldConflicts = matchedFields.AdditionalConflicts
                };

                //PopulateComplianceActions(unifiedRecord);
                DeriveSlaFromAdditional(unifiedRecord);
                AggregateValidation(unifiedRecord);

                _logger.LogDebug("Successfully completed field matching workflow. Matched: {MatchedCount}, Conflicts: {ConflictCount}, Missing: {MissingCount}, Overall Agreement: {Agreement}",
                    matchedFields.FieldMatches.Count, matchedFields.ConflictingFields.Count, matchedFields.MissingFields.Count, matchedFields.OverallAgreement);

                return Result<UnifiedMetadataRecord>.Success(unifiedRecord);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Field matching workflow cancelled");
                return ResultExtensions.Cancelled<UnifiedMetadataRecord>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in field matching workflow");
                return Result<UnifiedMetadataRecord>.WithFailure($"Error in field matching workflow: {ex.Message}", default(UnifiedMetadataRecord), ex);
            }
        }

        /// <summary>
        /// Collects NON-CORE (additional) field values across sources, keyed by field name, each value tagged
        /// with its source origin. Skips blanks, the magic "Origin" key, and any field already reconciled as a
        /// defined/core field. The resulting per-key value lists carry distinct-origin values so the matching
        /// policy can flag cross-source disagreements (homonym-revealing conflicts).
        /// </summary>
        private static Dictionary<string, List<FieldValue>> CollectAdditionalFieldValues(
            List<(ExtractedFields Fields, string SourceType)> perSource,
            HashSet<string> definedFieldNames)
        {
            var result = new Dictionary<string, List<FieldValue>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (fields, sourceType) in perSource)
            {
                if (fields.AdditionalFields == null)
                {
                    continue;
                }

                foreach (var kvp in fields.AdditionalFields)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Value))
                    {
                        continue;
                    }
                    if (kvp.Key.Equals("Origin", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (definedFieldNames.Contains(kvp.Key))
                    {
                        continue;
                    }

                    if (!result.TryGetValue(kvp.Key, out var list))
                    {
                        list = new List<FieldValue>();
                        result[kvp.Key] = list;
                    }
                    list.Add(new FieldValue(kvp.Key, kvp.Value, 1.0f, sourceType, ToOrigin(sourceType)));
                }
            }
            return result;
        }

        private static void CollectFieldValues(Dictionary<string, List<FieldValue>> allFieldValues, ExtractedFields fields, string sourceType)
        {
            if (!string.IsNullOrWhiteSpace(fields.Expediente))
            {
                AddFieldValue(allFieldValues, "Expediente", fields.Expediente, sourceType);
            }
            if (!string.IsNullOrWhiteSpace(fields.Causa))
            {
                AddFieldValue(allFieldValues, "Causa", fields.Causa, sourceType);
            }
            if (!string.IsNullOrWhiteSpace(fields.AccionSolicitada))
            {
                AddFieldValue(allFieldValues, "AccionSolicitada", fields.AccionSolicitada, sourceType);
            }

            if (fields.AdditionalFields != null && fields.AdditionalFields.Count > 0)
            {
                foreach (var kvp in fields.AdditionalFields)
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Value))
                    {
                        AddFieldValue(allFieldValues, kvp.Key, kvp.Value, sourceType);
                    }
                }
            }
        }

        private static void AddFieldValue(Dictionary<string, List<FieldValue>> allFieldValues, string fieldName, string? value, string sourceType)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!allFieldValues.ContainsKey(fieldName))
            {
                allFieldValues[fieldName] = new List<FieldValue>();
            }

            allFieldValues[fieldName].Add(new FieldValue(fieldName, value, 1.0f, sourceType, ToOrigin(sourceType)));
        }

        private static ExtractedFields CreateExtractedFieldsFromMatchedFields(MatchedFields matchedFields)
        {
            var extractedFields = new ExtractedFields();

            foreach (var match in matchedFields.FieldMatches)
            {
                switch (match.Key.ToLowerInvariant())
                {
                    case "expediente":
                        extractedFields.Expediente = match.Value.MatchedValue;
                        break;

                    case "causa":
                        extractedFields.Causa = match.Value.MatchedValue;
                        break;

                    case "accionsolicitada":
                    case "accion_solicitada":
                        extractedFields.AccionSolicitada = match.Value.MatchedValue;
                        break;
                }
            }

            return extractedFields;
        }

        private static FieldOrigin ToOrigin(string sourceType) =>
            sourceType.ToUpperInvariant() switch
            {
                "XML" => FieldOrigin.Xml,
                "PDF" => FieldOrigin.PdfOcr,
                "DOCX" => FieldOrigin.Docx,
                _ => FieldOrigin.Unknown
            };

        private static void DeriveSlaFromAdditional(UnifiedMetadataRecord record)
        {
            if (record.Expediente == null)
            {
                return;
            }

            if (record.Expediente.FechaRecepcion == default &&
                record.AdditionalFields.TryGetValue("FechaPublicacion", out var fechaPubRaw) &&
                DateTime.TryParse(fechaPubRaw, out var fechaPub))
            {
                record.Expediente.FechaRecepcion = fechaPub;
            }

            if (record.Expediente.FechaEstimadaConclusion == default &&
                record.Expediente.FechaRecepcion != default &&
                record.AdditionalFields.TryGetValue("DiasPlazo", out var diasRaw) &&
                int.TryParse(diasRaw, out var dias))
            {
                record.Expediente.FechaEstimadaConclusion = record.Expediente.FechaRecepcion.AddDays(dias);
            }
        }

        private static void AggregateValidation(UnifiedMetadataRecord record)
        {
            var validation = record.Validation ?? new ValidationState();

            if (record.Expediente != null)
            {
                validation.Require(!string.IsNullOrWhiteSpace(record.Expediente.NumeroExpediente), "Expediente");
                validation.Require(!string.IsNullOrWhiteSpace(record.Expediente.NumeroOficio), "NumeroOficio");
                validation.Require(record.Expediente.Subdivision != LegalSubdivisionKind.Unknown, "Subdivision");
                validation.Require(record.Expediente.FechaRecepcion != default, "FechaRecepcion");
                // WarnIf warns when the condition is false; pass the presence condition so the warning
                // fires when FechaEstimadaConclusion is MISSING (matching the Require(...) fields above).
                validation.WarnIf(record.Expediente.FechaEstimadaConclusion != default, "FechaEstimadaConclusion");
            }
            else
            {
                validation.Require(false, "Expediente");
            }

            foreach (var persona in record.Personas)
            {
                if (!persona.Validation.IsValid)
                {
                    validation.Warn("PersonaValidation");
                }
            }

            foreach (var action in record.ComplianceActions)
            {
                validation.Require(action.ActionType != ComplianceActionKind.Unknown, "ComplianceAction.ActionType");
                if (action.ActionType == ComplianceActionKind.Block ||
                    action.ActionType == ComplianceActionKind.Unblock ||
                    action.ActionType == ComplianceActionKind.Transfer)
                {
                    var hasAccount = !string.IsNullOrWhiteSpace(action.AccountNumber) ||
                                     !string.IsNullOrWhiteSpace(action.Cuenta?.Numero);
                    validation.Require(hasAccount, "ComplianceAction.Account");
                }
            }

            foreach (var conflict in record.AdditionalFieldConflicts)
            {
                validation.Warn($"Conflict:{conflict}");
            }

            record.Validation = validation;
        }
    }
}