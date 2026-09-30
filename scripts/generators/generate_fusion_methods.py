#!/usr/bin/env python3
"""Generate fusion methods for remaining R29 fields"""

FIELDS = [
    # Field name, max length, field type (string/int/datetime)
    ("OficioOrigen", 100, "string"),
    ("Referencia", 50, "string"),
    ("AreaClave", 100, "string"),
    ("AcuerdoReferencia", 200, "string"),
    ("OficioSerie", 50, "string"),
    ("OficioNumero", 50, "string"),
]

def generate_string_method(field_name, max_len):
    return f"""
    private async Task Fuse{field_name}Async(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {{
        var candidates = new List<FieldCandidate>();

        if (xml != null)
        {{
            var sanitized = FieldSanitizer.Sanitize(xml.{field_name});
            if (sanitized != null)
            {{
                candidates.Add(new FieldCandidate
                {{
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, {max_len})
                }});
            }}
        }}

        if (pdf != null)
        {{
            var sanitized = FieldSanitizer.Sanitize(pdf.{field_name});
            if (sanitized != null)
            {{
                candidates.Add(new FieldCandidate
                {{
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, {max_len})
                }});
            }}
        }}

        if (docx != null)
        {{
            var sanitized = FieldSanitizer.Sanitize(docx.{field_name});
            if (sanitized != null)
            {{
                candidates.Add(new FieldCandidate
                {{
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, {max_len})
                }});
            }}
        }}

        var result = await FuseFieldAsync("{field_name}", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {{
            fused.{field_name} = result.Value.Value ?? string.Empty;
            results["{field_name}"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {{
                conflicts.Add("{field_name}");
            }}
        }}
    }}"""

def generate_integration_call(field_name):
    return f"            await Fuse{field_name}Async(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);"

# Generate integration calls
print("=== INTEGRATION CALLS ===")
for field_name, max_len, field_type in FIELDS:
    if field_type == "string":
        print(generate_integration_call(field_name))

# Generate methods
print("\n\n=== METHODS ===")
for field_name, max_len, field_type in FIELDS:
    if field_type == "string":
        print(generate_string_method(field_name, max_len))
