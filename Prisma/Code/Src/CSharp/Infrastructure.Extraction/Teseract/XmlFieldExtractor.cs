using System.Text.RegularExpressions;
using System.Xml.Linq;
using ExxerCube.Prisma.Domain.Enums;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Teseract;

/// <summary>
/// XML field extractor implementation for CNBV/PRP1 fixtures.
/// Extracts canonical fields and maps subdivision, measure hints, identity, SLA hints, and accounts.
/// </summary>
public class XmlFieldExtractor : IFieldExtractor<XmlSource>
{
    private static readonly XNamespace Ns = "http://www.cnbv.gob.mx";
    private static readonly Regex AccountRegex = new(@"\b\d{6,}\b", RegexOptions.Compiled);
    private static readonly Regex CurpRegex = new(@"\b[A-Z]{4}\d{6}[A-Z0-9]{8}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
                additional["Curp"] = curp;
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
        return (areaClave ?? string.Empty).Trim() switch
        {
            "1" => "Hacendario",
            "2" => "Judicial",
            "3" => "Aseguramiento",
            "4" => "OperacionesIlicitas",
            _ => string.IsNullOrWhiteSpace(areaDescripcion) ? "Unknown" : areaDescripcion!.Trim()
        };
    }

    private static string InferMeasure(string? tieneAseguramiento, string? instrucciones)
    {
        if (bool.TryParse(tieneAseguramiento, out var isAseguramiento) && isAseguramiento)
        {
            return "Aseguramiento";
        }

        if (!string.IsNullOrWhiteSpace(instrucciones))
        {
            var text = instrucciones!.ToUpperInvariant();
            if (text.Contains("DEJAR SIN EFECTOS") || text.Contains("ELIMINA") || text.Contains("REANUD"))
            {
                return "Desbloqueo";
            }
            if (text.Contains("COPIA CERTIFICADA") || text.Contains("DOCUMENT"))
            {
                return "Documentacion";
            }
            if (text.Contains("TRANSFER"))
            {
                return "Transferencia";
            }
        }

        return "Informacion";
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

            var match = CurpRegex.Match(complementarios!);
            if (match.Success)
            {
                return match.Value.ToUpperInvariant();
            }
        }

        return null;
    }
}
