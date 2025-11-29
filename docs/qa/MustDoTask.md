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
- Interfaces/Services to update: `IResponseExporter`, `ILayoutGenerator`, `ExportService`, any UI trigger (e.g., `DocumentProcessing.razor`, export buttons).
- Contract changes (sample):
  ```csharp
  public interface IResponseExporter
  {
      Task<Result<ExportSnapshot>> GenerateAsync(
          Expediente expediente,
          ComplianceAction action,
          ValidationState validation,
          CancellationToken ct);
  }
  ```
  - Require: `FundamentoLegal`, `MedioEnvio`, `MeasureKind`, `Cuenta/Monto` (if applicable), `RFC variants/CURP`, `SLA dates`, `ValidationState` not Failed/Unknown.
  - Block/flag export when any required field is missing or `ValidationState` is Failed/Unknown; surface reasons in `ValidationIssues`.
- Affected downstream:
  - Domain: `Expediente` (canonical fields), `ComplianceAction` (MeasureKind/Cuenta/Monto), `PersonaSolicitud` (RFC variants/CURP), `SLAStatus` (dates).
  - Application: `ExportService`, `ILayoutGenerator` implementations.
  - UI: export buttons should show validation failures instead of silent no-ops.
- Tests to add:
  - Export snapshot fails when required fields are missing; includes `ValidationIssues`.
  - Export succeeds when all required fields present; snapshot contains canonical fields (fundamento, medio/envío, measure, cuenta/monto, RFC variants/CURP, SLA).
  - Unknown/Other in enums triggers review/failed export unless explicitly allowed.

Implementation sketch:
- Add `ValidationIssues` DTO to `ExportSnapshot`; require `ValidationState.IsValid` (or similar).
- In `ExportService`, gate execution:
  ```csharp
  if (!validation.IsValid) return Result<ExportSnapshot>.WithFailure("Missing required fields", snapshot);
  ```
- In layout generator, include canonical fields and format them per regulator template; return failure if mandatory segments are empty.


## 6) Parsing/Derivation Layer — TODO
- Files/Services: `IFieldExtractor`, `IPdfRequirementSummarizer`, `IPersonIdentityResolver`, `ISLAEnforcer`, `LegalDirectiveClassifierService`.
- Done (in code): `NameMatchingPolicy` + DI; `FieldMatcherService` routes name fields; OCR sanitizer (`TextSanitizer/ITextSanitizer`) with fixtures and pipeline tests (clean/noisy/missing/no-XML/gibberish) proving best-effort behavior.
- Missing (to implement now):
  - Field origin tagging: add `FieldOrigin/OriginTrace` (XML, PDF/OCR, Derived, Manual) on parsed fields and propagate into `MatchedFields/UnifiedMetadataRecord`.
  - Subdivision mapping: map `AreaClave/AreaDescripcion` → `LegalSubdivision`; flag Unknown in `ValidationState`.
  - Measure/account parsing: infer `MeasureKind` and `Cuenta` (numero/banco/sucursal/producto/moneda/monto) from XML + OCR text; apply sanitizer output; set origins + validation flags.
  - Identity enrichment: extract RFC variants/CURP from XML + OCR, store in `PersonaSolicitud`, flag missing/ambiguous in `ValidationState`.
  - SLA derivation: compute `FechaEstimadaConclusion = FechaRecepcion + DiasPlazo` via `ISLAEnforcer`; mark origin Derived.
  - Best-effort reconciliation: never short-circuit on divergence (missing XML, value drift); raise manual-review flags when uncertain.
- Implementation steps (concrete next edits):
  1) Extend `XmlFieldExtractor`/`FieldMatcherService` to emit `OriginTrace` alongside `FieldValue` for subdivision, measure hint, account, identity fields.
  2) Add `LegalSubdivisionMapper` helper (AreaClave/Descripcion → `LegalSubdivisionKind`), plug into extractor, set validation warn when Unknown.
  3) Add `MeasureParser` and `AccountParser` (reuse sanitizer) to infer `MeasureKind` + `Cuenta`; populate `ComplianceActions` in `UnifiedMetadataRecord`; flag missing account for block/unblock/transfer.
  4) Extend `IdentityValidator` to collect RFC variants/CURP from both XML/OCR, store in `PersonaSolicitud.RfcVariantes/Curp`, add validation warnings when absent or divergent.
 5) Implement `ISLAEnforcer.CalculateDeadline` use in extraction pipeline to set `Expediente.FechaEstimadaConclusion`; origin = Derived.
 6) Wire validation accumulation: after parsing, aggregate `ValidationState` onto `UnifiedMetadataRecord.Validation` (expediente, personas, actions).
 7) Tests: add fixture-driven system tests (XML + synthetic OCR text) covering subdivision mapping, measure/account parsing (happy/missing/conflict), identity capture, SLA derivation, validation flags (missing vs warning).
 8) Plug `ISLAEnforcer` into the extraction/matching flow (not just SLA tracking service) so `FechaEstimadaConclusion` uses the same business-day calculation already defined in infra. Reuse `SLAEnforcerService` via DI in extraction pipeline.

### Task 6 Implementation Checklist (execution order)
- [ ] Wire `LegalSubdivisionMapper` into XML extraction (set `Expediente.Subdivision`; warn if Unknown).
- [ ] Add `MeasureParser`/`AccountParser` (reuse sanitizer) to fill `ComplianceActions` with `MeasureKind`/`Cuenta`; set origins/validation.
- [ ] Extend `IdentityValidator` to merge RFC variants/CURP from XML + OCR; populate `PersonaSolicitud` fields; warn on missing/ambiguous.
- [ ] Use `ISLAEnforcer` in extraction to compute `FechaEstimadaConclusion` from `FechaRecepcion + DiasPlazo`; mark origin Derived.
- [ ] Aggregate per-entity `ValidationState` into `UnifiedMetadataRecord.Validation` (expediente, personas, actions, additional conflicts).
- [ ] System tests: fixtures for subdivision, measure/account (happy/missing/conflict), identity (match/conflict/missing), SLA derivation; assert origins + validation flags; no pipeline short-circuit.
- [ ] Delete / quarantine out-of-scope system OCR enhanced tests (cleanup done).
- [ ] Wire aggregation in `FieldMatchingService` after parsers to include subdivision/measure/account/identity validations.
- [ ] Resolve DOCX strategy stubs (DocxExtractionStrategy/IDocxExtractionStrategy/DocxStructure) and DI wiring to unblock system tests (XmlExtractorFixtureTests) without altering pipeline behavior.
- Affected downstream:
  - Domain: `Expediente`, `ComplianceAction`, `PersonaSolicitud`, `SLAStatus` populated with origin + validation.
  - Application: parsers (`MetadataExtractionService`, `LegalDirectiveClassifierService`) need to fill origins/validation; exporters consume them.
  - UI: show origins/validation to drive review when Origin = OCR/Manual or ValidationState = Failed/Unknown.
- Tests to add:
  - Fixture-based extraction covering subdivision, measure, cuenta, RFC variants/CURP, SLA with correct origins/validation (including divergence cases).
  - SLA calc (business days) via `ISLAEnforcer`.
  - Identity resolver: variants + CURP captured; missing fields flagged.
  - Origin tagging surfaces in snapshots/validation.
  - Wire sanitizer: parsed accounts/SWIFT keep raw + cleaned + warnings; manual-review flag when still invalid.

Implementation sketch (code snippets):
- Subdivision mapping:
  ```csharp
  expediente.Subdivision = LegalSubdivisionMapper.FromArea(expediente.AreaClave, expediente.AreaDescripcion);
  expediente.ValidationState.AddIfMissing(expediente.Subdivision != LegalSubdivision.Unknown, "Subdivision");
  ```
- Measure/account parsing:
  ```csharp
  var measure = MeasureKindResolver.FromText(rawMeasure);
  var cuenta = AccountParser.Parse(xmlNode, ocrText);
  action.Measure = measure;
  action.Cuenta = cuenta;
  action.ValidationState.AddIfMissing(measure != MeasureKind.Unknown, "Measure");
  action.ValidationState.AddIfMissing(cuenta?.IsComplete == true, "Cuenta/Monto");
  ```
- RFC/CURP:
  ```csharp
  persona.RfcVariantes = RfcParser.ParseAll(xmlText, ocrText);
  persona.Curp = CurpParser.Parse(xmlText, ocrText);
  persona.ValidationState.AddIfMissing(!string.IsNullOrEmpty(persona.Curp), "CURP");
  ```
- SLA:
  ```csharp
  expediente.FechaEstimadaConclusion = slaEnforcer.CalculateDeadline(expediente.FechaRecepcion, expediente.DiasPlazo);
  ```

Parallelizing 5 and 6:
- 5 (Export contracts/validation) can proceed independently while 6 implements the parsers; both converge when export consumes the populated canonical fields + ValidationState.
- Coordinate DTO shape (`ExportSnapshot`, `ValidationIssues`) so parsers feed export validation cleanly.

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
- ComplianceActionType (high risk/high value): conversion would touch Domain (ComplianceAction), Infrastructure.Classification (LegalDirectiveClassifierService), UI (LegalDirectiveClassificationView.razor), and tests across Infra/App/UI. Plan a dedicated pass with:
  - SmartEnum class (ComplianceActionKind) maintaining existing values/names for backward compatibility.
  - EF/JSON converters and UI mapper updates (color/icon switches).
  - Regression tests in Domain/App/Infra.Classification/UI to verify mappings and display behavior.

### Static analysis loop — ComplianceActionType → SmartEnum (no code run yet)
- Direct usages (tracked via `rg "ComplianceActionType"`): Domain entity `Domain/Entities/ComplianceAction.cs`; Infra classifier service `Infrastructure.Classification/LegalDirectiveClassifierService.cs` (action builders + string mapping); UI component `UI/ExxerCube.Prisma.Web.UI/Components/Shared/LegalDirectiveClassificationView.razor` (color/icon switch expressions); App tests (`Tests.Application/Services/DecisionLogic*`, `ManualReviewIntegrationTests`, `ExportServicePerformanceTests`); Infra tests (`Tests.Infrastructure.Classification/LegalDirectiveClassifierServiceTests.cs`); UI story docs; PRP scripts.
- Compile-risk hot spots:
  - Blazor switch expressions require constants; SmartEnum values are not constants. Rewrite to switch statements or helper methods that compare by value/name to avoid CS0150.
  - EF/JSON serialization: add ValueConverters/JsonConverter to keep int storage and cache payloads stable.
  - Tests asserting enum equality must switch to `ComplianceActionKind.Block` etc.; where comparing ints, add `.Value`.
- Migration plan (ready for implementation):
  - **New enum model:** Add `Domain/Enum/ComplianceActionKind.cs` deriving `EnumModel`, mirror existing numeric values, add `Unknown/Other`, include alias list (`Bloqueo`, `Desbloqueo`, `Transferencia`, `Documentacion`, `Información`, `Ignorar`).
  - **Entity wiring:** In `Domain/Entities/ComplianceAction.cs` change `ComplianceActionType` → `ComplianceActionKind`; ensure DTOs/handlers map to `.Value` when crossing boundaries that still expect ints.
  - **Infra classifier:** `Infrastructure.Classification/LegalDirectiveClassifierService.cs` map strings to `ComplianceActionKind` via `.FromName/.FromDisplayName/aliases`; default Unknown triggers `ValidationState` review flag. Builders emit `ComplianceActionKind.Block` etc.
  - **UI (Blazor):** Replace switch expressions with Value/Name-based switch helpers (compile-safe):
    ```csharp
    private Color GetActionColor(ComplianceActionKind actionType) => actionType.Value switch
    {
        1 => Color.Error,      // Block
        2 => Color.Success,    // Unblock
        3 => Color.Info,       // Document
        4 => Color.Warning,    // Transfer
        5 => Color.Primary,    // Information
        6 => Color.Default,    // Ignore
        _ => Color.Default
    };

    private string GetActionIcon(ComplianceActionKind actionType) => actionType.Name switch
    {
        "Block" => Icons.Material.Filled.Block,
        "Unblock" => Icons.Material.Filled.LockOpen,
        "Document" => Icons.Material.Filled.Description,
        "Transfer" => Icons.Material.Filled.SwapHoriz,
        "Information" => Icons.Material.Filled.Info,
        "Ignore" => Icons.Material.Filled.SkipNext,
        _ => Icons.Material.Filled.Help
    };
    ```
  - **EF/JSON converters:** Add SmartEnum int converters for ComplianceActionKind in DbContext and register JSON converter in FusionCache serializer to keep storage/caching stable.
  - **Tests to add (must before merge):**
    - Domain: SmartEnum resolution (FromValue/FromName/aliases) with Unknown/Other fallbacks.
    - Infra.Classification: classifier returns expected kinds for each fixture; Unknown path covered.
    - Application: DecisionLogic/ManualReview/Export tests updated to new kind; add assertions on `.Value` where needed.
    - UI: helper tests for color/icon mapping (Value/Name switch) and Unknown fallback (see switch-expression safety tests).
  - **Docs:** Update story/ADR references mentioning `ComplianceActionType` to `ComplianceActionKind`; note alias coverage.
- Proposed edits (sketches):
  - New `Domain/Enums/ComplianceActionKind.cs` (SmartEnum) with Unknown/Other, matching existing numeric values for backward compatibility; `ComplianceAction.ActionType` → `ComplianceActionKind ActionType`.
  - Infra classifier mapping: return `ComplianceActionKind.Block` etc.; string mapper uses `.FromName`/`.FromDisplayName`/aliases; default to Unknown, flag via ValidationState.
  - UI component: replace switch expressions with methods:
    ```csharp
    private Color GetActionColor(ComplianceActionKind actionType)
    {
        if (actionType == ComplianceActionKind.Block) return Color.Error;
        if (actionType == ComplianceActionKind.Unblock) return Color.Success;
        if (actionType == ComplianceActionKind.Document) return Color.Info;
        if (actionType == ComplianceActionKind.Transfer) return Color.Warning;
        if (actionType == ComplianceActionKind.Information) return Color.Primary;
        if (actionType == ComplianceActionKind.Ignore) return Color.Default;
        return Color.Default;
    }
    ```
  - Tests: update fixture builders to use new kind; add SmartEnum resolution tests for classifier; keep existing expected values.
- Exit condition for the loop: `dotnet build Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln` clean + targeted suites green (`Tests.Domain`, `Tests.Application --filter ComplianceAction`, `Tests.Infrastructure.Classification`, `Tests.UI` component test). Iterate plan if any compilation gaps surface.

### Switch-expression safety tests to add (Value/Name pattern)
- Goal: ensure the modern switch expressions compile and behave with SmartEnum by switching on `Value` (int) or `Name` (string), keeping case labels as literals.
- Suggested test targets:
  - `Tests.UI` (or a small helper test project) add a unit test for classification view mapping helpers (extract them into an internal static helper if needed). Example:
    ```csharp
    [Fact]
    public void Color_mapping_uses_expected_palette()
    {
        GetActionColor(ComplianceActionKind.Block).ShouldBe(Color.Error);
        GetActionColor(ComplianceActionKind.Information).ShouldBe(Color.Primary);
        GetActionColor(ComplianceActionKind.Unknown).ShouldBe(Color.Default);
    }
    ```
  - Companion test for icons with `Name` switch expression:
    ```csharp
    [Fact]
    public void Icon_mapping_handles_unknown()
    {
        GetActionIcon(ComplianceActionKind.Ignore).ShouldBe(Icons.Material.Filled.SkipNext);
        GetActionIcon(ComplianceActionKind.Unknown).ShouldBe(Icons.Material.Filled.Help);
    }
    ```
  - Classifier mapping test: in `Tests.Infrastructure.Classification/LegalDirectiveClassifierServiceTests.cs`, assert `ActionType.Value` and `ActionType.Name` switch expressions map to the same outputs (Block → 1/\"Block\", etc.).
- Add these in the same branch that introduces `ComplianceActionKind` to get compile-time coverage of the new switch style.

### Lessons learned from ComplianceActionKind migration (reuse for remaining enums)
- Blazor switch expressions require literal case labels; SmartEnum instances aren’t const. Switch on `Value` or `Name` to keep switch expressions terse and compiler-safe.
- Keep SmartEnums persistence-agnostic. Apply conversions in EF configs only for persisted entities (DbSets). A reusable helper keeps it DRY:
  ```csharp
  public static PropertyBuilder<TEnum> HasEnumModelConversion<TEnum>(this PropertyBuilder<TEnum> builder)
      where TEnum : EnumModel, new() =>
      builder.HasConversion(v => v.Value, v => EnumModel.FromValue<TEnum>(v));
  ```
- Caching/JSON: if a SmartEnum flows through FusionCache/JSON, register a JsonConverter that roundtrips via `.Value`/`.Name` to avoid reflection surprises.
- Preserve numeric values and add Unknown/Other to protect flows when inputs diverge (PDF/OCR vs XML).
- Tests first: add SmartEnum resolution tests (FromValue/Name/aliases), update existing asserts to the new kind (`.Value` when comparing ints), and add UI helper tests for mapping + Unknown fallback.
- Scoped suites are enough to stay green (Domain + targeted Application/Infra UI) without running long system tests.

### Batch SmartEnum plan for remaining domain enums (workflow-oriented)
- Scope: workflow/ops enums still plain: `ClassificationLevel1`, `ClassificationLevel2`, `EscalationLevel`, `ReviewStatus`, `ReviewReason`, `DecisionType`, `ProcessingStage`, `AuditActionType`, `ImageQualityLevel`, `ImageFilterType`, `BulkProcessingStatus`, `FileFormat`.
- Classification rationale: Level1 is the primary legal bucket (Aseguramiento, Desembargo, Documentacion, Informacion, Transferencia, OperacionesIlicitas); Level2 refines by authority flavor (Especial, Judicial, Hacendario). Both are used by classifiers (`FileClassifierService`) and naming/export flows.
- Conversion approach:
  - For Level1/Level2 and SLA escalations, add SmartEnums with Unknown/Other; keep numeric values aligned to current ordering to avoid breaking persisted SLAStatus data. Add aliases (e.g., “Especial/Judicial/Hacendario”).
  - For ReviewStatus/Reason/DecisionType/ProcessingStage/AuditActionType, consider staying as plain enums unless we need aliases/Unknown; if converted, preserve values and add Unknown for safety.
  - Imaging enums (ImageQualityLevel/ImageFilterType) are technical; keep as-is unless we need runtime aliasing; if converted, do it last with minimal blast radius.
  - BulkProcessingStatus/FileFormat likely remain plain; convert only if we need extensibility.
- Persistence/caching touchpoints:
  - SLAStatus already persists `EscalationLevel`; if converted, add EF ValueConverter to `SLAStatusConfiguration` using the `HasEnumModelConversion` helper; migrate DB only if values change (avoid changing value order).
  - Classification results travel through Infra.Classification/FileStorage tests and exports; if persisted later, add converters when adding DbSets.
  - No caching impacts unless we serialize these enums; if so, register a JsonConverter.
- Tests to plan:
  - SmartEnum resolution for Level1/Level2/Escalation if converted.
  - Classifier tests (`FileClassifierServiceTests`) updated to new types; ensure Unknown/Other fallback.
  - SLAStatus tests verifying EF converter roundtrip if we persist EscalationLevel as SmartEnum.
  - UI models (e.g., `SLACaseViewModel`) switch helpers updated to Value/Name switch; add helper unit tests similar to ComplianceActionKind.

### Static analysis loop — remaining enums (plan before execution)
- EF helper to add (once): `HasEnumModelConversion<TEnum>` for EnumModel ⇄ int, applied only in EF configurations of persisted entities. No change to enums themselves.
- Conversion targets and primary consumers (grep map):
  - ClassificationLevel1/ClassificationLevel2: used in `Domain/ValueObjects/ClassificationResult.cs`, `Infrastructure.Classification/FileClassifierService.cs`, file naming/export tests (`Tests.Infrastructure.FileStorage/*`, `Tests.Application/MetadataExtraction*`, `Tests.EndToEnd/MetadataExtractionIntegrationTests.cs`), manual review flows. UI impact minimal.
  - EscalationLevel: persisted in `Domain/Entities/SLAStatus.cs`, configured in `Infrastructure.Database/EntityFramework/Configurations/SLAStatusConfiguration.cs`, used in SLA services (`Infrastructure.Database/SLAEnforcerService.cs`, `Resilience/ResilientSLAEnforcerService.cs`), UI view models (`UI/.../SLACaseViewModel.cs`), hub notifications, metrics. Migrations already store int—preserve values to avoid schema changes.
  - ReviewStatus/ReviewReason/DecisionType/ProcessingStage/AuditActionType: workflow enums used in Review services/tests; not currently persisted beyond logs/tests.
  - ImageQualityLevel/ImageFilterType, BulkProcessingStatus, FileFormat: technical enums used in imaging/batch; low risk; keep last.
- Proposed refactor shape (per SmartEnum):
  ```csharp
  public sealed class ClassificationLevel1 : EnumModel
  {
      public static readonly ClassificationLevel1 Unknown = new(-1, "Unknown", "Desconocido");
      public static readonly ClassificationLevel1 Aseguramiento = new(0, "Aseguramiento", "Aseguramiento");
      public static readonly ClassificationLevel1 Desembargo = new(1, "Desembargo", "Desembargo");
      public static readonly ClassificationLevel1 Documentacion = new(2, "Documentacion", "Documentación");
      public static readonly ClassificationLevel1 Informacion = new(3, "Informacion", "Información");
      public static readonly ClassificationLevel1 Transferencia = new(4, "Transferencia", "Transferencia");
      public static readonly ClassificationLevel1 OperacionesIlicitas = new(5, "OperacionesIlicitas", "Operaciones ilícitas");
      public static readonly ClassificationLevel1 Other = new(999, "Other", "Otro");
      public ClassificationLevel1() {}
      private ClassificationLevel1(int value, string name, string displayName) : base(value, name, displayName) {}
      public static ClassificationLevel1 FromValue(int value) => FromValue<ClassificationLevel1>(value);
      public static ClassificationLevel1 FromName(string name) => FromName<ClassificationLevel1>(name);
      public static implicit operator int(ClassificationLevel1 value) => value.Value;
      public static implicit operator ClassificationLevel1(int value) => FromValue(value);
  }
  ```
  (Repeat pattern for Level2, EscalationLevel, etc.; keep existing numeric order to avoid data churn.)
- Switch expression adjustments: replace `enum` switches with Value/Name switches to satisfy Blazor/CS0150 where applicable (e.g., `SLACaseViewModel`).
- EF: apply `HasEnumModelConversion` only to persisted entities (e.g., `SLAStatusConfiguration` for EscalationLevel). No DB schema change if values are preserved.
- JSON/cache: add a JsonConverter for EnumModel only if these types are serialized via FusionCache/JSON; otherwise defer.
- Static compile simulation: ensure all `using ExxerCube.Prisma.Domain.Enums;` are replaced with `...Domain.Enum;` where conversions occur; adjust equality assertions to SmartEnum (`ShouldBe(ClassificationLevel1.Aseguramiento)`; if comparing ints, use `.Value`).
- Exit condition before execution: plan covers all usages with refactor snippets; Value/Name switches identified; EF touchpoints isolated to persisted entities; no migrations required when numeric order is kept.

## Hard directives (user-specified, one-time)
- Do not run git commands (add/commit/reset/revert/etc.) without explicit user approval.
- Do not add/remove projects or edit solution files (`*.sln`) without explicit user approval.
- Do not delete projects or packages; stay strictly within the assigned scope unless explicitly instructed.

## Journal / Progress Tracker
- 2025-02-14: Read real XML fixtures under `Prisma/Fixtures/PRP1/*2025.xml`; confirmed shape: `Cnbv_*` metadata (AreaClave/AreaDescripcion, FechaPublicacion, DiasPlazo), parties in `SolicitudPartes`, detailed persons in `SolicitudEspecifica/PersonasSolicitud`, instructions text carrying account numbers/bank names, RFC variants, CURP/date hints in `Complementarios`. SLA inputs present (`Cnbv_FechaPublicacion`, `Cnbv_DiasPlazo`); no explicit FechaRecepcion. ~5% XML may be missing; PDF/OCR often diverge.
- Current state: solution builds clean; origin tracking is wired (FieldOrigin on FieldValue/FieldMatchResult) and OCR sanitizer + pipeline fixtures exist. XML extractor remains stub; subdivision/measure/account/identity/SLA parsing not yet wired.
- Next steps (respecting DRY/SOLID, fill gaps vs. rebuild): extend existing extractors (no new pipelines) to parse subdivision, measure, account (with sanitizer), RFC variants/CURP, SLA derivation; propagate into MatchedFields/UnifiedMetadataRecord with origins/validation; add focused integration/system tests using existing fixtures to prove reconciliation (XML vs. OCR vs. PDF) without short-circuiting. Update this journal as milestones close.
- 2025-02-15: Added EF Core converters/comparers for SmartEnums used in review workflow (ReviewReason, ReviewStatus, DecisionType) to keep InMemory/relational providers aligned. Application test suite passes (165/165). Pending: export contract/validation expansion (Task 5) and parsing/derivation wiring (Task 6).
- 2025-02-16: Hardened export validation (requires fundamento, medio de envío, subdivision, fechas, well-formed compliance actions with accounts for block/unblock/transfer). Updated export unit/perf tests to feed required fields and added negative coverage for missing account. Application tests (Export suites) now passing (166/166).
## Name/Identity Matching Policy (to implement)
- Normalize names before comparison: uppercase, remove accents, collapse whitespace; keep raw for audit.
- Use multi-signal fuzzy comparison (TokenSortRatio + Jaro-Winkler). Thresholds (tunable via `IOptionsMonitor<NameMatchingOptions>`):
  - Auto-accept: score >= 0.95.
  - Review-needed: 0.80–0.95.
  - Conflict: < 0.80.
- Maintain a small alias/variant list for common Spanish variants (e.g., PEREZ/PERES, GONZALEZ/GONZALES, CHRISTIAN/CRISTIAN), but only auto-accept if scores exceed the accept threshold; otherwise mark review.
- Never overwrite a valid value with a “more popular” variant; on disagreement store both in `MatchedFields.AllValues`, set `HasConflict`, and surface validation/warnings for manual review.
- Make thresholds and alias list tuneable at runtime via `IOptionsMonitor<NameMatchingOptions>` to adjust in production without rebuilds.
## OCR/Extraction Fixture Plan (for system tests)
- Add structured dummy fixtures under `Prisma/Code/Src/CSharp/Tests.Infrastructure.Extraction.Teseract/Fixtures/` with subfolders per scenario:
  - `Accounts/Clean` – clear account + SWIFT, happy path (PDF/PNG).
  - `Accounts/Noisy` – spaced digits, noisy SWIFT (PDF/PNG); OCR + sanitizer should normalize and warn.
  - `Identity/Match` – XML/PDF with matching RFC/CURP to assert winner selection.
  - `Identity/Conflict` – XML/PDF with differing RFC/CURP to trigger conflict/manual review.
  - `Accounts/MissingInOne` – account present only in one source; best-effort merge.
  - `Names/DuplicateSame` – duplicate but same/similar names; no false conflict.
  - `Names/DuplicateDifferent` – two valid but different names; must flag manual review.
  - `Edge/NoXml` – PDF-only (simulate 5% missing XML); pipeline continues with warnings.
  - `Edge/GibberishAccount` – OCR gibberish for account/SWIFT to assert warnings/no stop.
- Generation approach: use `Prisma/Code/Src/CSharp/Python/generate_corpus.py` (or targeted scripts) to emit PDF/PNG with controlled text and matching XML per scenario; add noise where applicable.
- Tests to add: system-level checks that run OCR + sanitizer and matching, asserting winner vs conflict/manual-review flags in `MatchedFields`/validation state without short-circuiting the pipeline.
