# Must-Do Tasks for Compliance MVP
Priority tasks to close critical gaps (ready for developer assignment). Based on `docs/qa/Domain_Legal_CodeReview.md`, `docs/qa/Canonical_XML_Gap_Assessment.md`, and pipeline requirements.

## 1) Canonical Case Data (Expediente) — DONE (structural)
- File: `Prisma/Code/Src/CSharp/Domain/Entities/Expediente.cs`
- Add fields: `FundamentoLegal`, `MedioEnvio` (SIARA/Fisico), `EvidenciaFirma` (hash/ticket), `OficioOrigen`, `AcuerdoReferencia`, `FechaRegistro`, `FechaEstimadaConclusion`, `Subdivision` (enum `LegalSubdivision`), `ValidationState`.
- Enum: `Prisma/Code/Src/CSharp/Domain/Enums/LegalSubdivision.cs` (new) with Unknown/Other + A/AS…E/IN.
- Action: derive `FechaEstimadaConclusion` from `FechaRecepcion + DiasPlazo` (hábiles via `ISLAEnforcer`); map `AreaClave/AreaDescripcion` to `LegalSubdivision`.
- Tests: mapping from fixtures, SLA calc, required fields validation.

## 2) Measure Intent & Assets — DONE (structural)
- Files: `Prisma/Code/Src/CSharp/Domain/Entities/SolicitudEspecifica.cs`, `ComplianceAction.cs`
- Add `MeasureType` enum (new file) with Unknown/Other, Bloqueo, Desbloqueo, TransferenciaFondos, Documentacion, Informacion.
- Add value objects (new): `Cuenta` (numero, banco, sucursal, producto, moneda, monto), `DocumentItem` (tipo, periodo, certificada), `ValidationState`.
- Bind `ComplianceAction` to `Cuenta?`, `Monto`, `Producto`, `LegalBasis`, `DueDate`, `ValidationState`; remove reliance on loose strings.
- Tests: measure inference, account parsing, action validation.

## 3) Identity Fidelity — DONE (structural)
- Files: `Prisma/Code/Src/CSharp/Domain/Entities/SolicitudParte.cs`, `PersonaSolicitud.cs`
- Add: `List<RfcVariant>` (value object), `Curp`, `DateOnly? FechaNacimiento`, `ValidationState`; keep `Nombre/Paterno/Materno/Domicilio`.
- Action: allow multiple RFCs and mark missing identity fields; parse variants/CURP from XML/OCR.
- Tests: RFC variant capture from fixtures; validation flags when missing.

## 4) Evidence & Traceability — DONE (structural)
- File: `Prisma/Code/Src/CSharp/Domain/Entities/FileMetadata.cs`
- Add: `Channel` (SIARA/Fisico), `SignatureType` (FELAVA/N/A), `EvidenceHash`, `LinkedExpediente/Oficio`.
- Action: associate evidence with case/officio; log in `IAuditLogger`.
- Tests: evidence linkage present/absent; validation flags missing evidence.

## 5) Export & Layout Compliance — TODO
- Interfaces: `IResponseExporter`, `ILayoutGenerator`
- Update contracts to require canonical fields (fundamento, medio/envío, measures, accounts, RFC variants, SLA) and block export when required fields are missing (`ValidationState`).
- Tests: export snapshot fails on missing required fields; passes when populated.

## 6) Parsing/Derivation Layer — TODO
- Files/Services: `IFieldExtractor`, `IPdfRequirementSummarizer`, `IPersonIdentityResolver`, `ISLAEnforcer`
- Action: implement parsers to populate subdivision, measure, RFC variants/CURP, accounts/montos from XML + PDF/Word/OCR; compute SLA dates; annotate field origins.
- Tests: fixture-based extraction/inference; SLA calc; origin tagging.

## 7) UI/Workflow Gaps (stakeholder confidence) — TODO
- Build/extend pages: download reconciliation (expected vs downloaded), canonical case view (legal backbone, SLA, subdivision), measures/assets, identity with variants, evidence chain, 5-part summary (bloqueo, desbloqueo, documentación, transferencia, información).
- Artifacts: screenshots/GIFs for stakeholder demo (see `docs/qa/Stakeholder_Presentation_Plan.md`).

## 8) Enum/Schema Future-Proofing — PARTIAL (enums added; consumer handling pending)
- Ensure enums (`MeasureType`, `LegalSubdivision`, `DocumentItemType`, `AuthorityType`) include Unknown/Other and that consumers treat them as review-needed states.
- Allow extension elements/unknown nodes in canonical XML; ignore gracefully but flag for review.
- Evaluate reusing the existing SmartEnum pattern (`Domain/Enum/RequirementType.cs`, `EnumModel`) for high-variance domains (authority, measure/document types) to support dynamic discovery and dictionary-driven resolution when PDF/XML disagree.

## 9) Validation & Tests (cross-cutting) — PARTIAL (ValidationState added + unit test; broader coverage pending)
- Add `ValidationState` to core entities; require checks for required legal fields; surface missing/derived/manual-needed status.
- Tests to add: fixture mapping/completeness, SLA calc, export snapshot, evidence linkage, validation failure on missing required fields, Unknown/Other handling.
- Add reconciliation tests for diverging sources (XML vs PDF/Word/OCR) to ensure Unknown/Other paths and dynamic enum resolution work without breaking flows.

## Annex: SmartEnum Migration Plan & Consumers (not started)

### Candidates to convert to SmartEnum (EnumModel-derived)
- AuthorityType → SmartEnum (e.g., AuthorityKind): CNBV, UIF, Juzgado, Hacienda, Other, Unknown; add aliases/keywords for noisy PDF/OCR.
- MeasureType → SmartEnum (e.g., MeasureKind): Bloqueo, Desbloqueo, Transferencia, Documentacion, Informacion, Other, Unknown; allow dynamic entries for new legal asks.
- DocumentItemType → SmartEnum (e.g., DocumentItemKind): known doc types + Other/Unknown; runtime additions if authorities request new artifacts.
- LegalSubdivision: keep as plain enum unless dynamic extensions are required (fixed CNBV codes).

### Affected entities (with current file/lines)
- Expediente (`Prisma/Code/Src/CSharp/Domain/Entities/Expediente.cs`:49 Subdivision, 79 FundamentoLegal, 149 Validation) — Subdivision uses `LegalSubdivision`; would switch to SmartEnum if converted.
- SolicitudEspecifica (`.../Entities/SolicitudEspecifica.cs`:27 Measure, 51 Cuentas, 56 Documentos, 61 Validation) — Measure now uses `MeasureKind` (SmartEnum) instead of `MeasureType` enum.
- ComplianceAction (`.../Entities/ComplianceAction.cs`:23 Cuenta, 63 LegalBasis, 73 Validation) — Account ties to measure intent; action type stays as-is but may consume SmartEnum outputs.
- DocumentItem (`.../ValueObjects/DocumentItem.cs`: Tipo) — Tipo uses `DocumentItemType`; would switch to SmartEnum.
- PersonaSolicitud (`.../Entities/PersonaSolicitud.cs`:57 RfcVariantes, 87 Validation) / SolicitudParte (`.../Entities/SolicitudParte.cs`:48 RfcVariantes, 78 Validation) — could store AuthorityKind if added; currently unaffected.

### Affected interfaces
- Parsers/extractors: `IFieldExtractor`, `IPdfRequirementSummarizer`, `IPersonIdentityResolver` — need mapping from text → SmartEnum with alias/keyword metadata.
- Exporters: `IResponseExporter`, `ILayoutGenerator` — emit SmartEnum value/display name; treat Unknown/Other as review-needed.
- Validation/logging: `IAuditLogger` — record source and final SmartEnum resolution; Unknown/Other should trigger review.
- SLA/Derivation unaffected directly, but classification steps must produce SmartEnum outputs.

### Refactor sketch (SmartEnum shape)
```csharp
public sealed class AuthorityKind : EnumModel
{
    public static readonly AuthorityKind Unknown = new(0, "Unknown", "Desconocido");
    public static readonly AuthorityKind CNBV = new(1, "CNBV", "Comisión Nacional Bancaria y de Valores", aliases: new[] { "CNBV" });
    public static readonly AuthorityKind UIF = new(2, "UIF", "Unidad de Inteligencia Financiera", aliases: new[] { "UIF" });
    public static readonly AuthorityKind Juzgado = new(3, "Juzgado", "Autoridad Judicial", aliases: new[] { "Juzgado", "Tribunal" });
    public static readonly AuthorityKind Hacienda = new(4, "Hacienda", "Autoridad Fiscal", aliases: new[] { "Hacienda", "SAT" });
    public static readonly AuthorityKind Other = new(999, "Other", "Otro");

    public IReadOnlyCollection<string> Aliases { get; }

    private AuthorityKind(int value, string name, string displayName, IEnumerable<string>? aliases = null)
        : base(value, name, displayName)
    {
        Aliases = aliases?.ToArray() ?? Array.Empty<string>();
    }

    public static AuthorityKind FromText(string text) =>
        FromName<AuthorityKind>(text) ?? FromDisplayName<AuthorityKind>(text) ??
        GetAll<AuthorityKind>().FirstOrDefault(k => k.Aliases.Contains(text, StringComparer.OrdinalIgnoreCase)) ?? Unknown;
}
```

### Converter/consumer changes
- Entities: replace enum properties with SmartEnum types; keep int Value semantics for persistence; add explicit ToInt/FromInt converters if needed (EF).
- Parsers: use `FromText`/aliases to map PDF/OCR strings; fall back to Unknown/Other.
- Exporters: emit `Value` and `DisplayName`; if Unknown/Other, flag validation.
- Validation: treat Unknown/Other as review-needed; use `ValidationState` to surface.
- Interfaces: update contracts to return SmartEnum types where applicable, e.g.:
  ```csharp
  public interface IFieldExtractor
  {
      AuthorityKind ResolveAuthority(string raw);
      MeasureKind ResolveMeasure(string raw);
      DocumentItemKind ResolveDocumentItem(string raw);
  }
   ```
  Ensure `IResponseExporter`/`ILayoutGenerator` accept SmartEnum fields and block/flag Unknown/Other on required outputs.
- Persistence (EF Core): add ValueConverters for SmartEnums (int ↔ SmartEnum) in DbContext configuration to keep storage invariant and avoid runtime reflection cost in EF. Example:
  ```csharp
  builder.Property(e => e.Subdivision)
         .HasConversion(
             v => v.Value,
             v => LegalSubdivision.FromValue(v));
  ```
- Caching/serialization (FusionCache/JSON): add custom JSON converters for SmartEnums so cached payloads serialize as int/name and deserialize via FromValue/FromName without reflection surprises. Ensure read-only repositories using FusionCache register these converters in the serializer options.

### Tests to add
- SmartEnum resolution: FromValue/FromName/FromDisplayName/FromText with aliases; Unknown/Other fallbacks.
- Entity wiring: ensure SmartEnum properties serialize/deserialize to int (if persisted) and stay backward compatible.
- Parser mapping: text samples from XML/PDF/OCR map to expected SmartEnum; Unknown on ambiguous input.
- Exporter: outputs display name/value correctly; blocks or flags Unknown/Other when required.
