#!/usr/bin/env python3
"""Add all remaining R29 fields one at a time"""
import subprocess
import sys

# Remaining string fields from Expediente
STRING_FIELDS = [
    ("Referencia", 100),
    ("AcuerdoReferencia", 200),
    ("EvidenciaFirma", 100),
    ("Referencia1", 100),
    ("Referencia2", 100),
]

# Int fields need different template
INT_FIELDS = [
    ("AreaClave", "AreaClave"),
    ("DiasPlazo", "DiasPlazo"),
]

def add_string_field(field_name, max_len, line_num):
    """Add one string field"""
    method = f"""
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
    }}
"""

    # Write method to temp file
    with open("/tmp/temp_method.txt", "w") as f:
        f.write(method)

    # Add integration call
    integration_call = f'            await Fuse{field_name}Async(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);'

    subprocess.run(f"cd Prisma/Code/Src/CSharp && sed -i '{line_num}a\\{integration_call}' 02-Infrastructure/Infrastructure.Classification/FusionExpedienteService.cs", shell=True, check=True)

    # Find method insertion point (after last method)
    result = subprocess.run("cd Prisma/Code/Src/CSharp && grep -n 'private async Task Fuse.*Async(' 02-Infrastructure/Infrastructure.Classification/FusionExpedienteService.cs | tail -1 | cut -d: -f1", shell=True, capture_output=True, text=True)
    last_method_line = int(result.stdout.strip())

    # Insert method after last method's closing brace (approximately +65 lines for method body)
    method_insert_line = last_method_line + 65

    subprocess.run(f"cd Prisma/Code/Src/CSharp && sed -i '{method_insert_line}r /tmp/temp_method.txt' 02-Infrastructure/Infrastructure.Classification/FusionExpedienteService.cs", shell=True, check=True)

    # Build and test
    build_result = subprocess.run("cd Prisma/Code/Src/CSharp && dotnet build 02-Infrastructure/Infrastructure.Classification/ExxerCube.Prisma.Infrastructure.Classification.csproj --no-incremental 2>&1 | tail -3", shell=True, capture_output=True, text=True)

    if "Build succeeded" in build_result.stdout:
        print(f"✅ {field_name} added successfully!")
        return True
    else:
        print(f"❌ {field_name} FAILED:")
        print(build_result.stdout)
        return False

# Start adding fields
print("Adding remaining R29 fields...")
current_line = 99  # After OficioOrigen

for field_name, max_len in STRING_FIELDS:
    if not add_string_field(field_name, max_len, current_line):
        print(f"Stopping at {field_name}")
        sys.exit(1)
    current_line += 1

print(f"\n🎉 Successfully added {len(STRING_FIELDS)} fields!")
print("Total fields: 23 + {} = {}".format(len(STRING_FIELDS), 23 + len(STRING_FIELDS)))
