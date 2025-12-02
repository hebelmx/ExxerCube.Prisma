namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

/// <summary>
/// XML field extractor implementation for CNBV/PRP1 fixtures.
/// Extracts canonical fields and maps subdivision, measure hints, identity, SLA hints, and accounts.
/// </summary>
public class XmlFieldExtractor : IFieldExtractor<XmlSource>
{
    private static readonly XNamespace Ns = "http://www.cnbv.gob.mx";
    private static readonly Regex AccountRegex = new(@"\b\d{6,}\b", RegexOptions.Compiled);
    private static readonly Regex StrictCurpRegex = new(@"\b[A-Z][AEIOUX][A-Z]{2}\d{2}(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])[HM][A-Z]{2}[B-DF-HJ-NP-TV-Z]{3}[A-Z0-9]\d\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    // CURP can appear truncated or with/without the verification digits; capture the whole payload if present.
    private static readonly Regex LooseCurpRegex = new(@"\b(?<curp>[A-Z]{4}\d{6}[A-Z0-9]{6}(?:\d{2})?)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <inheritdoc />
    public Task<Result<ExtractedFields>> ExtractFieldsAsync(XmlSource source, FieldDefinition[] fieldDefinitions)
    {
        try
        {
            var doc = LoadXml(source);
            if (doc == null)
            {
                return Task.FromResult(Result<ExtractedFields>.WithFailure("XML content is empty or invalid"));
            }

            var root = doc.Root;
            if (root == null)
            {
                return Task.FromResult(Result<ExtractedFields>.WithFailure("XML has no root element"));
            }

            var additional = new Dictionary<string, string?>();

            // Core identifiers
            var expediente = Value(root, "Cnbv_NumeroExpediente");
            var instrucciones = Value(root.Element(Ns + "SolicitudEspecifica"), "InstruccionesCuentasPorConocer");

            // Subdivision (AreaClave/AreaDescripcion)
            var areaClave = Value(root, "Cnbv_AreaClave");
            var areaDescripcion = Value(root, "Cnbv_AreaDescripcion");
            additional["AreaClave"] = areaClave;
            additional["AreaDescripcion"] = areaDescripcion;
            additional["Subdivision"] = MapSubdivision(areaClave, areaDescripcion);

            // SLA inputs
            additional["FechaPublicacion"] = Value(root, "Cnbv_FechaPublicacion");
            additional["DiasPlazo"] = Value(root, "Cnbv_DiasPlazo");

            // Authority
            additional["AutoridadNombre"] = Value(root, "AutoridadNombre");
            additional["AutoridadEspecificaNombre"] = Value(root, "AutoridadEspecificaNombre");

            // Measure hint
            var tieneAseguramiento = Value(root, "TieneAseguramiento");
            additional["TieneAseguramiento"] = tieneAseguramiento;
            additional["MeasureHint"] = InferMeasure(tieneAseguramiento, instrucciones);

            // Accounts from instructions
            if (!string.IsNullOrWhiteSpace(instrucciones))
            {
                var accounts = AccountRegex.Matches(instrucciones!)
                    .Select(m => m.Value)
                    .Distinct()
                    .ToArray();
                if (accounts.Length > 0)
                {
                    additional["CuentasRaw"] = string.Join(",", accounts);
                }
            }

            // Identity: RFC variants + CURP (from complementarios)
            var rfcs = CollectRfcVariants(root);
            if (rfcs.Count > 0)
            {
                additional["RfcList"] = string.Join(",", rfcs);
            }
            var curp = ExtractCurp(root);
            if (!string.IsNullOrWhiteSpace(curp))
            {
                additional["Curp"] = NormalizeCurp(curp);
            }

            var extractedFields = new ExtractedFields
            {
                Expediente = expediente,
                AccionSolicitada = instrucciones,
                AdditionalFields = additional
            };

            return Task.FromResult(Result<ExtractedFields>.Success(extractedFields));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<ExtractedFields>.WithFailure($"XML extraction failed: {ex.Message}", default(ExtractedFields), ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<FieldValue>> ExtractFieldAsync(XmlSource source, string fieldName)
    {
        var fieldsResult = await ExtractFieldsAsync(source, Array.Empty<FieldDefinition>()).ConfigureAwait(false);
        if (fieldsResult.IsFailure || fieldsResult.Value == null)
        {
            return Result<FieldValue>.WithFailure(fieldsResult.Error ?? "Extraction failed");
        }

        var fields = fieldsResult.Value;
        var lower = fieldName.ToLowerInvariant();
        var value = lower switch
        {
            "expediente" => fields.Expediente,
            "causa" => fields.Causa,
            "accionsolicitada" or "accion_solicitada" => fields.AccionSolicitada,
            _ => fields.AdditionalFields.TryGetValue(fieldName, out var v) ? v : null
        };

        if (value == null)
        {
            return Result<FieldValue>.WithFailure($"Field '{fieldName}' not found in XML");
        }

        return Result<FieldValue>.Success(new FieldValue(fieldName, value, 1.0f, "XML", FieldOrigin.Xml));
    }

    private static XDocument? LoadXml(XmlSource source)
    {
        if (!string.IsNullOrWhiteSpace(source.XmlContent))
        {
            return XDocument.Parse(source.XmlContent);
        }

        if (!string.IsNullOrWhiteSpace(source.FilePath) && File.Exists(source.FilePath))
        {
            var content = File.ReadAllText(source.FilePath);
            return XDocument.Parse(content);
        }

        return null;
    }

    private static string? Value(XElement? root, string localName)
    {
        return root?.Element(Ns + localName)?.Value?.Trim();
    }

    private static string MapSubdivision(string? areaClave, string? areaDescripcion)
    {
        // Use the AreaDescripcion directly - it's the human-readable subdivision name
        // Normalize to PascalCase without spaces for consistency
        if (string.IsNullOrWhiteSpace(areaDescripcion))
        {
            return LegalSubdivisionKind.Unknown.Name;
        }

        // Convert to title case and remove spaces/special characters
        var normalized = System.Globalization.CultureInfo.CurrentCulture.TextInfo
            .ToTitleCase(areaDescripcion.ToLowerInvariant())
            .Replace(" ", "")           // Remove spaces: "Operaciones Ilícitas" → "OperacionesIlícitas"
            .Replace("í", "i")          // Normalize accents: "Ilícitas" → "Ilicitas"
            .Replace("á", "a")
            .Replace("é", "e")
            .Replace("ó", "o")
            .Replace("ú", "u");

        return normalized;
    }

    private static string InferMeasure(string? tieneAseguramiento, string? instrucciones)
    {
        if (bool.TryParse(tieneAseguramiento, out var isAseguramiento) && isAseguramiento)
        {
            return ToSpanishMeasureName(ComplianceActionKind.Block);
        }

        if (!string.IsNullOrWhiteSpace(instrucciones))
        {
            var text = instrucciones!.ToUpperInvariant();
            if (text.Contains("DEJAR SIN EFECTOS") || text.Contains("ELIMINA") || text.Contains("REANUD"))
            {
                return ToSpanishMeasureName(ComplianceActionKind.Unblock);
            }
            if (text.Contains("COPIA CERTIFICADA") || text.Contains("DOCUMENT"))
            {
                return ToSpanishMeasureName(ComplianceActionKind.Document);
            }
            if (text.Contains("TRANSFER"))
            {
                return ToSpanishMeasureName(ComplianceActionKind.Transfer);
            }
        }

        var parsed = ParseActionKind(instrucciones);
        return parsed == ComplianceActionKind.Unknown
            ? ToSpanishMeasureName(ComplianceActionKind.Information)
            : ToSpanishMeasureName(parsed);
    }

    private static string ToSpanishMeasureName(ComplianceActionKind kind)
    {
        return kind.Name switch
        {
            "Block" => "Aseguramiento",
            "Unblock" => "Desbloqueo",
            "Document" => "Documentacion",
            "Transfer" => "Transferencia",
            "Information" => "Informacion",
            "Ignore" => "Ignorar",
            "Other" => "Otro",
            _ => "Desconocido"
        };
    }

    private static ComplianceActionKind ParseActionKind(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ComplianceActionKind.Unknown;
        }

        var text = raw.ToUpperInvariant();
        if (text.Contains("BLOQ")) return ComplianceActionKind.Block;
        if (text.Contains("DESBLO") || text.Contains("LIBER")) return ComplianceActionKind.Unblock;
        if (text.Contains("TRANS") || text.Contains("TRASP")) return ComplianceActionKind.Transfer;
        if (text.Contains("INFO")) return ComplianceActionKind.Information;
        if (text.Contains("DOC")) return ComplianceActionKind.Document;
        if (text.Contains("IGNOR")) return ComplianceActionKind.Ignore;
        return ComplianceActionKind.Other;
    }

    private static List<string> CollectRfcVariants(XElement root)
    {
        var rfcs = new List<string>();
        foreach (var node in root.Elements(Ns + "SolicitudPartes"))
        {
            var rfc = Value(node, "Rfc");
            if (!string.IsNullOrWhiteSpace(rfc))
            {
                rfcs.Add(rfc.Trim());
            }
        }

        var solicitudEspecifica = root.Element(Ns + "SolicitudEspecifica");
        if (solicitudEspecifica != null)
        {
            foreach (var persona in solicitudEspecifica.Elements(Ns + "PersonasSolicitud"))
            {
                var rfc = Value(persona, "Rfc");
                if (!string.IsNullOrWhiteSpace(rfc))
                {
                    rfcs.Add(rfc.Trim());
                }
            }
        }

        return rfcs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? ExtractCurp(XElement root)
    {
        var solicitudEspecifica = root.Element(Ns + "SolicitudEspecifica");
        if (solicitudEspecifica == null)
        {
            return null;
        }

        foreach (var persona in solicitudEspecifica.Elements(Ns + "PersonasSolicitud"))
        {
            var complementarios = Value(persona, "Complementarios");
            if (string.IsNullOrWhiteSpace(complementarios))
            {
                continue;
            }

            var strict = StrictCurpRegex.Match(complementarios!);
            if (strict.Success)
            {
                return NormalizeCurp(strict.Value);
            }

            var loose = LooseCurpRegex.Match(complementarios!);
            if (loose.Success)
            {
                return NormalizeCurp(loose.Groups["curp"].Value);
            }
        }

        return null;
    }

    private static string NormalizeCurp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var curp = value.ToUpperInvariant().Trim();
        // Cap at standard CURP length (18) to avoid noise, but keep full verifier when present.
        if (curp.Length > 18)
        {
            curp = curp[..18];
        }
        return curp;
    }
}
