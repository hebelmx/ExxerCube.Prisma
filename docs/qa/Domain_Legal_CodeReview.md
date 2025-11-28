# Domain vs. Legal Requirements Review
Scope: compare current domain entities (`Prisma/Code/Src/CSharp/Domain/Entities` and `Domain/Enums`) against CNBV/SIARA requirements (see `docs/qa/Requirements.md`) and legal PDFs in `Prisma/Fixtures/PRP1/` (e.g., R29 A-2911, Disposiciones SIARA 2018, Instructivo DGAAAC 2019, SIARA manuals). No code changes made; this is a gap analysis and refactor plan.

## Key Gaps with Rationale and Outcomes (ordered by impact)
- Legal backbone fields missing  
  - What: `Expediente` lacks fundamento legal, medio/envío evidence, oficio/acuerdo origen, and SLA dates (`FechaRegistro/FechaEstimadaConclusion`).  
  - Why: model mirrored fixture XML; those fields aren’t explicit there.  
  - Solution: add fields and derive where needed (compute SLA, capture fundamentos, medio/envío, oficio/acuerdo refs).  
  - Why implement: legal completeness and auditability; required by Disposiciones SIARA 2018/R29.  
  - Benefit: strengthens defensibility in audits, reduces rework, clearer story for customers and sales.

- Measure intent not explicit  
  - What: only `TieneAseguramiento` + free text; no structured `MeasureType` or binding to cuentas/montos/productos.  
  - Why: intent inferred informally from text; not modeled.  
  - Solution: introduce `MeasureType` enum and structured measures tied to accounts/products/montos.  
  - Why implement: makes actions machine-verifiable and testable; aligns with “identify account/product/monto” requirements.  
  - Benefit: faster, safer automation; clearer outcomes for compliance teams and buyers.

- Subdivisión/Área taxonomy is loose  
  - What: `AreaClave` int + free text; no controlled A/AS…E/IN set.  
  - Why: raw XML copied as-is.  
  - Solution: enforce `LegalSubdivision` enum and map from XML values.  
  - Why implement: prevents drift and misclassification.  
  - Benefit: consistent reporting, lowers support issues, boosts trust.

- Identity fidelity gaps  
  - What: single RFC, no CURP/birthdate separation, RFC variants buried in `Complementarios`.  
  - Why: flat strings without variant/value objects.  
  - Solution: add RFC variant collection, CURP/birthdate fields/value objects.  
  - Why implement: avoids false positives/negatives in matching; meets legal identification expectations.  
  - Benefit: higher match accuracy, fewer manual fixes, better client confidence.

- Traceability of references  
  - What: references stored as free strings; no typed links to oficio/expediente/instrument origin.  
  - Why: no typed relationships or validation.  
  - Solution: add typed reference fields and validations; align `ComplianceAction` with core entities.  
  - Why implement: required for showing lineage to regulators and internal audit.  
  - Benefit: rapid proof of compliance, lower audit friction.

- Evidence/channel not modeled  
  - What: no fields for channel (SIARA/físico), signature (FELAVA), or evidence linkages; `FileMetadata` not tied to cases.  
  - Why: metadata kept generic and detached.  
  - Solution: link evidence to Expediente/Oficio, capture format/signature/hash.  
  - Why implement: supports non-repudiation and regulator expectations on submission proof.  
  - Benefit: stronger compliance posture and sales narrative on audit readiness.

- Product/account structure missing  
  - What: cuentas/productos/montos only in free text.  
  - Why: no `Cuenta`/`Producto` value objects.  
  - Solution: add structured account/product objects and parsers to populate them.  
  - Why implement: enables validation of required questions and safe automation.  
  - Benefit: reduces errors in executions (block/unblock/transfer), builds client trust.

- SLA posture partial  
  - What: `Expediente` lacks SLA dates though `Oficio` has them; parsed data and SLA tracking diverge.  
  - Why: XML parsing stops at publication/plazo.  
  - Solution: store `FechaRecepcion`, compute/store `FechaEstimadaConclusion`, align with `ComplianceAction.DueDate`.  
  - Why implement: enforce deadlines and escalations correctly.  
  - Benefit: fewer SLA breaches, clearer status for ops and customers.

## Where to model (entities/interfaces) and null-handling stance
- `Expediente` (Entity): add legal backbone (fundamento, medio/envío, evidencia firma, oficio/acuerdo origen), SLA fields (`FechaRegistro`, `FechaEstimadaConclusion`), subdivision enum mapping; allow missing inputs but require explicit validation flags rather than nullable annotations.
- `SolicitudEspecifica` (Entity): host structured `MeasureType`, requested documentation items, and structured cuentas/productos/montos; fields may be empty but must be explicitly checked and marked when unresolved.
- `SolicitudParte` / `PersonaSolicitud` (Entities): support multiple RFC variants and structured identity (CURP, birthdate, domicilio) as value objects; avoid nullable annotations—use explicit “unknown/empty” handling with validation results.
- `ComplianceAction` (Entity): bind to structured account/product/monto and legal basis; align `DueDate` to SLA; keep free strings minimal and guard access with explicit null/empty checks.
- `FileMetadata` (Entity) + linkage: associate evidence (channel, signature, hash) to `Expediente`/`Oficio`; tolerate missing evidence but surface it via validation, not silent nulls.
- `RequirementTypeDictionary` (Entity): ensure mappings reference `MeasureType`/`LegalSubdivision`.
- Interfaces:
  - `IResponseExporter`: emit canonical XML/PDF including new fields.
  - `ISLAEnforcer`: compute `FechaEstimadaConclusion` from `FechaRecepcion + DiasPlazo` (hábiles).
  - `IPersonIdentityResolver`: handle RFC variants/CURP parsing and matching.
  - `IFieldExtractor` / `IPdfRequirementSummarizer`: extract accounts/products/montos, measure intent, identity details from PDF/Word/OCR to populate nullable slots.
  - `IAuditLogger`: log source (XML/OCR/manual) for each field so gaps are visible and not silently ignored.
- Null-handling principle: prefer explicit presence checks and validation results over nullable reference annotations; real-world gaps are expected, but every required field must be marked as filled/derived/missing so reviewers see the state and runtime null exceptions are avoided.

## Suggested Refactors (planning only)
- Add structured fields to `Expediente`: `FundamentoLegal`, `MedioEnvio` (SIARA/Físico), `EvidenciaFirma` (FELAVA/TIFF hash), `FechaRegistro`, `FechaEstimadaConclusion`, `OficioInicial`, `AcuerdoReferencia`, and `PlazoHabil` calculation hook.
- Introduce enums/value objects:
  - `LegalSubdivision` enum for the A/AS…E/IN codes; replace `AreaClave`/`AreaDescripcion` free text or map from them.
  - `MeasureType` enum to tag bloqueo/desbloqueo/transferencia/documentación/información at `SolicitudEspecifica` level (and roll up to `ComplianceAction`).
  - `DocumentItemType` enum for requested docs (estado de cuenta, contrato, identificación, comprobante domicilio, firma, cheque, expediente apertura).
  - Value objects: `RfcVariant` (RFC + variant tag), `IdentidadPersona` (names, CURP, birthdate), `Cuenta` (numero, banco, sucursal, producto, moneda, monto).
- Strengthen `ComplianceAction`: include `LegalBasis` and `DueDate` (already present) sourced from `Expediente` SLA; add optional `Cuenta`/`Monto`/`Producto` references instead of loose strings.
- Link evidence: associate `FileMetadata` with `Expediente`/`Oficio` and mark channel/signature; store hash for signed TIFF/PDF as per SIARA Gestión guide.
- Validation layer: enforce required set per Disposiciones and Requirements.md (expediente, oficio, plazo, autoridad, subdivisión, sujeto(s), medida). Emit warnings when derived from OCR vs. XML.
- Multi-source merge: support multiple RFCs and personas per solicitud (as seen in `555CCC-66666662025.xml`); add a collection for RFC variants rather than duplicating personas.

## Test Coverage to Add
- Fixture-based mapping tests: parse each `Prisma/Fixtures/PRP1/*.xml` into the canonical model and assert: subdivision enum maps, measure type inferred, RFC variants captured, SLA dates calculated.
- Completeness checks: unit tests that fail when mandatory legal fields are missing or empty after mapping.
- Snapshot of canonical XML regeneration: ensure stable schema and detect accidental drift.
- SLA tests: verify `FechaRecepcion + DiasPlazo (hábiles)` produces `FechaEstimadaConclusion`; ensure DueDate on `ComplianceAction` matches.

## Enum strategy (coverage + future-proof)
- Every enum should include `Unknown` and `Other` to tolerate incomplete/mismatched inputs without breaking flows.
- `MeasureType`: Bloqueo, Desbloqueo, TransferenciaFondos, Documentacion, Informacion, Other (future legal asks).
- `DocumentItemType`: EstadoCuenta, Contrato, Identificacion, ComprobanteDomicilio, Firma, Cheque, ExpedienteApertura, Other.
- `LegalSubdivision`: add `Other` alongside `Unknown` to accommodate unforeseen CNBV codes.
- Consumer code must treat `Unknown`/`Other` as explicit review-needed states, not silent defaults.

## Code-shape proposals (reference only; not applied)
- `Prisma/Code/Src/CSharp/Domain/Entities/Expediente.cs` — add legal backbone and validation guards:
```csharp
public sealed class Expediente
{
    // existing fields...
    public string FundamentoLegal { get; set; } = string.Empty;
    public string MedioEnvio { get; set; } = string.Empty;           // SIARA | Fisico
    public string EvidenciaFirma { get; set; } = string.Empty;       // hash/ticket
    public string OficioOrigen { get; set; } = string.Empty;
    public string AcuerdoReferencia { get; set; } = string.Empty;    // e.g., 105/2021
    public DateTime FechaRegistro { get; set; }
    public DateTime FechaEstimadaConclusion { get; set; }
    public LegalSubdivision Subdivision { get; set; } = LegalSubdivision.Unknown;
    public ValidationState Validation { get; } = new();
}
```

- `Prisma/Code/Src/CSharp/Domain/Enums/LegalSubdivision.cs` — constrain area codes:
```csharp
public enum LegalSubdivision
{
    Unknown = 0,
    A_AS, A_DE, A_TF, A_IN,
    J_AS, J_DE, J_IN,
    H_IN,
    E_AS, E_DE, E_IN
}
```

- `Prisma/Code/Src/CSharp/Domain/Entities/SolicitudEspecifica.cs` — explicit measure and structured asks:
```csharp
public sealed class SolicitudEspecifica
{
    // existing fields...
    public MeasureType Measure { get; set; } = MeasureType.Unknown;
    public List<Cuenta> Cuentas { get; } = new();           // numero, banco, sucursal, producto, moneda, monto
    public List<DocumentItem> Documentos { get; } = new();  // estado de cuenta, contrato, etc.
    public ValidationState Validation { get; } = new();
}
```

- `Prisma/Code/Src/CSharp/Domain/Entities/PersonaSolicitud.cs` (and `SolicitudParte`) — RFC variants/CURP with explicit checks:
```csharp
public sealed class PersonaSolicitud
{
    // existing fields...
    public List<RfcVariant> RfcVariantes { get; } = new();
    public string Curp { get; set; } = string.Empty;
    public DateOnly? FechaNacimiento { get; set; }          // check before use
    public ValidationState Validation { get; } = new();
}
public readonly record struct RfcVariant(string Value, string SourceTag);
```

- `Prisma/Code/Src/CSharp/Domain/Entities/ComplianceAction.cs` — tie to structured account/product and SLA:
```csharp
public sealed class ComplianceAction
{
    public ComplianceActionType ActionType { get; set; }
    public Cuenta? Cuenta { get; set; }      // optional; check for null
    public decimal? Monto { get; set; }
    public string Producto { get; set; } = string.Empty;
    public string LegalBasis { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public ValidationState Validation { get; } = new();
}
```

- `Prisma/Code/Src/CSharp/Domain/Entities/FileMetadata.cs` — evidence linkage:
```csharp
public sealed class FileMetadata
{
    // existing fields...
    public string Channel { get; set; } = string.Empty;       // SIARA | Fisico
    public string SignatureType { get; set; } = string.Empty; // FELAVA | N/A
    public string EvidenceHash { get; set; } = string.Empty;
    public string LinkedExpediente { get; set; } = string.Empty;
}
```

- `Prisma/Code/Src/CSharp/Domain/ValueObjects/ValidationState.cs` — explicit presence checks:
```csharp
public sealed class ValidationState
{
    private readonly HashSet<string> _missing = new();
    public void Require(bool condition, string field)
    {
        if (!condition) _missing.Add(field);
    }
    public bool IsValid => _missing.Count == 0;
    public IReadOnlyCollection<string> Missing => _missing;
}
```
Mapping guard example (XML → domain):
```csharp
expediente.Validation.Require(!string.IsNullOrWhiteSpace(expediente.NumeroExpediente), "NumeroExpediente");
expediente.Validation.Require(expediente.Subdivision != LegalSubdivision.Unknown, "Subdivision");
expediente.FechaEstimadaConclusion = slaEnforcer.Calculate(expediente.FechaRecepcion, expediente.DiasPlazo);
```

## File/line callouts for implementation
- `Prisma/Code/Src/CSharp/Domain/Entities/Expediente.cs` (lines ~6-108): insert legal backbone fields (`FundamentoLegal`, `MedioEnvio`, `EvidenciaFirma`, `OficioOrigen`, `AcuerdoReferencia`, `FechaRegistro`, `FechaEstimadaConclusion`, `Subdivision: LegalSubdivision`) after existing fields; add `ValidationState`.
- `Prisma/Code/Src/CSharp/Domain/Enums` (new file `LegalSubdivision.cs`): add enum with `Unknown`, `Other`, and A/AS…E/IN codes; update `Expediente` to use it.
- `Prisma/Code/Src/CSharp/Domain/Entities/SolicitudEspecifica.cs` (lines ~11-46): add `MeasureType` property, `List<Cuenta> Cuentas`, `List<DocumentItem> Documentos`, and `ValidationState`.
- `Prisma/Code/Src/CSharp/Domain/Entities/SolicitudParte.cs` (lines ~8-63) and `PersonaSolicitud.cs` (lines ~13-72): add RFC variants (`List<RfcVariant>`), CURP, birthdate (`DateOnly?`), and `ValidationState`; consider replacing singular `Rfc` with structured variants.
- `Prisma/Code/Src/CSharp/Domain/Entities/ComplianceAction.cs` (lines ~6-69): replace loose account/product fields with `Cuenta?`, keep `Monto` as decimal?, align `LegalBasis`, `DueDate`, and add `ValidationState`.
- `Prisma/Code/Src/CSharp/Domain/Entities/FileMetadata.cs` (lines ~6-47): add channel (`SIARA/Fisico`), signature type (`FELAVA/N/A`), evidence hash, and linkage to `Expediente`/`Oficio`.
- `Prisma/Code/Src/CSharp/Domain/Entities/Oficio.cs` (lines ~6-69): align subdivision to `LegalSubdivision` and ensure SLA fields match `Expediente` usage.
- `Prisma/Code/Src/CSharp/Domain/Enums` (new files): add `MeasureType`, `DocumentItemType`, `AuthorityType` with `Unknown`/`Other`.
- `Prisma/Code/Src/CSharp/Domain/ValueObjects` (new): add `RfcVariant`, `Cuenta`, `DocumentItem`, `ValidationState`.
- Interfaces to adjust: `IResponseExporter`, `ISLAEnforcer`, `IPersonIdentityResolver`, `IFieldExtractor`, `IPdfRequirementSummarizer`, `IAuditLogger` to require/populate the new structures and validation status.

## Test checklist (before/with implementation)
- Mapping tests: for each fixture XML in `Prisma/Fixtures/PRP1/*.xml`, assert subdivision enum mapping, measure inference, RFC variants capture, accounts/products parsed when present, SLA dates computed.
- Completeness tests: required legal fields flagged when missing (e.g., FundamentoLegal, MedioEnvio, Subdivision, Measure, identity essentials).
- Canonical XML snapshot: round-trip generation matches schema and remains stable across changes.
- SLA tests: `FechaRecepcion + DiasPlazo (hábiles)` → `FechaEstimadaConclusion` and `ComplianceAction.DueDate` alignment.
- Evidence linkage tests: when channel/signature/hash absent, validation marks missing; when present, links to `Expediente`/`Oficio` are set.

## Why current mapping yields nulls (what/why/how)
- What is missing: fundamento legal, medio/envío evidence, oficio/acuerdo origen, structured medidas/cuentas/montos, RFC variants/CURP, SLA dates, subdivision enum, and document-type evidence.
- Why: the domain mirrors fixture XML tags; those legal-grade fields are absent or appear only as free text, so a richer model would get nulls by default.
- How to solve: enrich ingestion and validation:
  - Compute SLA dates (recepción + días hábiles) and fill `FechaRegistro/FechaEstimadaConclusion`.
  - Map `AreaClave/AreaDescripcion` to a `LegalSubdivision` enum.
  - Infer `MeasureType` from `TieneAseguramiento` + instruction text; extract cuentas/productos/montos from `InstruccionesCuentasPorConocer` and PDF/Word/OCR.
  - Parse RFC variants/CURP/birthdate from `Complementarios` and allow multiple RFCs per persona.
  - Link `FileMetadata` as evidence (channel/signature) to cases.
  - Add a validation layer to flag unresolved required fields for manual completion instead of silent nulls.

## References (APA)
- Comisión Nacional Bancaria y de Valores. (2016). *Instructivo de la Serie R29 A-2911 Aseguramientos, Transferencias y Desbloqueo de Cuentas*. `Prisma/Fixtures/PRP1/R29 A-2911 Aseguramientos, Transferencias y Desbloqueos de Cuentas_03032016.pdf`.
- Comisión Nacional Bancaria y de Valores. (2018). *Disposiciones de carácter general aplicables a los requerimientos de información...* (4 de septiembre). `Prisma/Fixtures/PRP1/Disposiciones SIARA 4 de septiembre de 2018.pdf`.
- Comisión Nacional Bancaria y de Valores. (2019). *Instructivo para el llenado de los formatos para requerir información y documentación por parte de las autoridades hacendarias federales*. `Prisma/Fixtures/PRP1/Instructivo_requerimiento_fisico_DGAAAC_v2019.pdf`.
- Dirección General Adjunta Atención a Autoridades. (2019). *Manual de usuario del SIARA (Civiles v01)*. `Prisma/Fixtures/PRP1/SIARA_Manual_Civiles_v01.pdf`.
