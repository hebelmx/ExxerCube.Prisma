# Design — MVP-PATH #8: Full SIRO-XML export in Reconciliator.Worker

**Date:** 2026-06-13 · **Branch:** `Kt2`
**Owner decisions already settled (pre-design):**
1. Template DB uses a **dedicated** `ConnectionStrings:TemplateConnection` (separate from the audit `DefaultConnection`).
2. Scope is **full SIRO-XML** — the Reconciliator's terminal export must emit SIRO-conformant XML, not Excel.
3. Output must be **schema-validated** (XSD if available; structural assertions otherwise).

This is the intended-solution doc. Adversarial review checks the diff against THIS, not subagent prose.

---

## 1. Decisive finding: which exporter produces SIRO XML?

**Use `SiroXmlExporter` directly — not the `IAdaptiveExporter` "XML" path.**

The reason is a concrete code discrepancy:

| Path | Root element emitted | SIRO-conformant? |
|---|---|---|
| `SiroXmlExporter.GenerateSiroXml()` (`Infrastructure.Export/SiroXmlExporter.cs:181`) | `<SiroResponse xmlns="http://siro.regulatory.namespace">` + full structured payload | **YES** |
| `AdaptiveExporter.GenerateXmlExport()` (`Infrastructure.Export.Adaptive/AdaptiveExporter.cs:396-422`) | `<Export>` (generic flat key/value dump via `XElement(mapping.TargetField, value)`) | **NO** |

`AdaptiveResponseExporterAdapter.ExportSiroXmlAsync()` (`AdaptiveResponseExporterAdapter.cs:56-59`) delegates to
`_adaptiveExporter.ExportAsync(metadata, "XML", ct)` — which routes to `GenerateXmlExport()` above.
The "XML" seeded template maps 15 SIRO field names but the generator still emits `<Export><NumeroExpediente>…</Export>`,
not `<SiroResponse xmlns="…">`. That is NOT what the bank receives.

`SiroXmlExporter` already implements the correct XML structure, namespace, optional-element gates,
`SolicitudPartes`/`SolicitudEspecificas` collections, ISO date formatting, and an optional
`XmlSchemaSet`-backed validator — all verified green by `SiroXmlExporterTests.cs` (50+ assertions).

**The Reconciliator should inject `IResponseExporter` (resolved to `SiroXmlExporter` or
`CompositeResponseExporter`) and call `ExportSiroXmlAsync(unifiedMetadataRecord, stream, ct)`.**

This is consistent with how Web.UI registers the export stack:
`AddExportServices()` (`Infrastructure.Export/DependencyInjection/`) registers `SiroXmlExporter` as scoped
and `CompositeResponseExporter` as `IResponseExporter`. `AddAdaptiveExportServices()` then *overrides*
`IResponseExporter` with `AdaptiveResponseExporterAdapter`. In the Reconciliator we will register
`AddExportServices()` only — no `AddAdaptiveExportServices()` — so `IResponseExporter` resolves to
`CompositeResponseExporter` → `SiroXmlExporter`. This matches the Web.UI SIRO path exactly.

---

## 2. Architecture: what changes

### 2.1 `ReconciliationOrchestrator` Stage 5 — replace `IAdaptiveExporter` with `IResponseExporter`

**Current** (`ReconciliationOrchestrator.cs:28,44,67,187,204-205`):
- Constructor parameter: `IAdaptiveExporter? exporter`
- Stage 5 call: `_exporter.ExportAsync(sourceObject, "Excel", cancellationToken)`
- Event fields: `Destination = $"exports/{fileId}.xlsx"`, `Format = "Excel"`

**After**:
- Constructor parameter: `IResponseExporter? exporter`
- Stage 5 call: `_exporter.ExportSiroXmlAsync(unifiedMetadata, stream, cancellationToken)`
  where `unifiedMetadata` is built from `fusionResult.FusedExpediente`
  and `stream` is a `MemoryStream` (bytes captured for the `ExportCompletedEvent.ExportedSizeBytes`)
- Event fields: `Destination = $"exports/{fileId}.siro.xml"`, `Format = "SiroXml"`

**Source object construction**: `fusionResult?.FusedExpediente` is an `Expediente`. `SiroXmlExporter`
expects a `UnifiedMetadataRecord` (wraps `Expediente`). Stage 5 must construct:
```csharp
var metadata = new UnifiedMetadataRecord { Expediente = fusionResult?.FusedExpediente };
```
If `fusionResult` is null or `FusedExpediente` is null, skip with a Warning (same null-guard pattern as today).

**Guard on missing required fields**: `SiroXmlExporter.ValidateMetadata()` (line 142-159) already returns
`Result.WithFailure` when `NumeroExpediente` or `NumeroOficio` is blank. Stage 5 propagates via
`EmitProcessingError` as today. No new validation logic needed in the orchestrator.

### 2.2 `ReconciliationOrchestrator` constructor — `IAdaptiveExporter` dependency gone

`Program.cs` line 67 today: `exporter: sp.GetService<IAdaptiveExporter>()`

After: `exporter: sp.GetService<IResponseExporter>()`

The optional-null contract is preserved — if `IResponseExporter` is not registered, Stage 5 still skips
with a Warning.

### 2.3 `Prisma.Reconciliator.Worker/Program.cs` — wiring additions

Three additions after the `AddProcessIdentity` call (line 36) and before `AddSingleton<ReconciliationOrchestrator>`:

```csharp
// (A) Export services: SiroXmlExporter + CompositeResponseExporter (IResponseExporter)
// Guarded: if TemplateConnection is blank/placeholder the Reconciliator still boots but Stage 5 skips.
var templateConnectionString = builder.Configuration.GetConnectionString("TemplateConnection");
if (!string.IsNullOrWhiteSpace(templateConnectionString)
    && !templateConnectionString.StartsWith("DEV-PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddExportServices(builder.Configuration);  // registers SiroXmlExporter + CompositeResponseExporter
}
else
{
    startupLogger.LogWarning(
        "SIRO export DISABLED for Reconciliator Worker: ConnectionStrings:TemplateConnection is blank or placeholder. " +
        "Set ConnectionStrings__TemplateConnection for production.");
}
```

`AddExportServices` does NOT need a connection string — the export stack (`SiroXmlExporter`,
`CompositeResponseExporter`) has no DB dependency. `TemplateConnection` is checked here only as the
owner-designated sentinel meaning "production export is configured."

Then in the `ReconciliationOrchestrator` factory (currently line 63-67):
```csharp
builder.Services.AddSingleton<ReconciliationOrchestrator>(sp => new ReconciliationOrchestrator(
    sp.GetRequiredService<IEventPublisher>(),
    sp.GetRequiredService<ILogger<ReconciliationOrchestrator>>(),
    classifier: sp.GetRequiredService<IFileClassifier>(),
    exporter: sp.GetService<IResponseExporter>()));   // <-- changed from IAdaptiveExporter
```

### 2.4 Output file storage

The Reconciliator currently only emits the terminal `ExportCompletedEvent` with a `Destination` path string
— it does not actually write to disk. **For MVP this is acceptable**: the event payload includes
`ExportedSizeBytes` and `Format = "SiroXml"` as proof-of-export. Writing the `.siro.xml` to shared storage
is a post-MVP step (track as follow-up in the session handoff).

If the owner wants the XML file persisted in this session, Stage 5 should:
```csharp
var outputPath = StoragePathResolution.Combine(fileId, "siro.xml"); // conceptual
var fullPath = _storagePathResolver.Resolve(outputPath);
using var fs = File.OpenWrite(fullPath);
await _exporter.ExportSiroXmlAsync(metadata, fs, cancellationToken);
```
Defer unless owner explicitly requests file persistence. **Owner decision #1 below.**

---

## 3. Schema validation approach

**No XSD file exists in the repo.** `SiroXmlExporter._siroSchemaSet` is `null` by default
(the no-`XmlSchemaSet` constructor is used at registration). Validation is therefore structural only:
`ValidateMetadata()` (lines 142-159) checks `NumeroExpediente` and `NumeroOficio` are non-blank.

Two options:

| Option | What it does | Owner effort |
|---|---|---|
| **A (recommended MVP)** | Keep `_siroSchemaSet = null`; rely on `ValidateMetadata()` guards + structural XML assertions in tests | Zero — already implemented |
| B | Author and register a real SIRO XSD; inject `XmlSchemaSet` via DI | Requires the bank's schema definition |

**Recommendation: Option A for MVP.** The regulatory SIRO schema is a bank-provided artifact. Until Banamex
supplies the `.xsd`, Option A gives complete structural confidence through test assertions (namespace,
required elements, date format, collections). The validation hook in `SiroXmlExporter` is already wired
and can accept a schema the moment one is provided without any code change.

If the bank XSD is available, register it:
```csharp
// In AddExportServices():
var schemaSet = new XmlSchemaSet();
schemaSet.Add("http://siro.regulatory.namespace", "path/to/siro.xsd");
services.AddSingleton(schemaSet);
// SiroXmlExporter second constructor picks it up via DI.
```

---

## 4. Test plan

### 4.1 Where the test lives

New file: `08 Tests/04 Services/Athena/Prisma.Athena.Processing.Tests/ReconciliationOrchestratorSiroExportTests.cs`

The existing `ReconciliationPipelineServiceTests.cs` (same project) already has a real
`ReconciliationOrchestrator` + mock `IAdaptiveExporter` wired at lines 43-44 — the new test class
follows the same pattern but uses a **real `SiroXmlExporter`** instead of a mock.

### 4.2 Test cases

```
ReconciliationOrchestrator_Stage5_EmitsSiroXmlEvent
  Given: real SiroXmlExporter (no XmlSchemaSet), minimal-valid Expediente
  When: ReconcileAsync runs
  Then: ExportCompletedEvent.Format == "SiroXml"
        ExportCompletedEvent.Destination ends with ".siro.xml"
        ExportCompletedEvent.ExportedSizeBytes > 0

ReconciliationOrchestrator_Stage5_SiroXml_HasCorrectStructure
  Given: real SiroXmlExporter, fully-populated Expediente
  When: ReconcileAsync runs (capture the MemoryStream bytes from the event or a test hook)
  Then: parsed XML root local name == "SiroResponse"
        root namespace == "http://siro.regulatory.namespace"
        <NumeroExpediente> == Expediente.NumeroExpediente
        <NumeroOficio> == Expediente.NumeroOficio
        <FechaPublicacion> matches yyyy-MM-dd
        (reuse the assertion helpers from SiroXmlExporterTests)

ReconciliationOrchestrator_Stage5_SkipsExport_WhenExporterNull
  Given: exporter = null
  When: ReconcileAsync runs
  Then: stagesCompleted == 1 (Stage 4 only), no ExportCompletedEvent published

ReconciliationOrchestrator_Stage5_SkipsExport_WhenFusionResultNull
  Given: real exporter, fusionResult = null
  When: ReconcileAsync runs
  Then: Stage 5 warns + skips (no ExportCompletedEvent)

ReconciliationOrchestrator_Stage5_EmitsError_WhenValidationFails
  Given: real SiroXmlExporter, Expediente with blank NumeroExpediente
  When: ReconcileAsync runs
  Then: ProcessingErrorEvent emitted, Component == "Export"
```

**Note on test observability**: `ReconciliationOrchestrator` publishes events via `IEventPublisher`.
The existing tests use `Substitute.For<IEventPublisher>()` and assert with `Received()` calls.
The XML bytes are not directly observable without a test hook. For the structural-assertion test,
two options:
- (A) Add an `internal` `TestExportedBytes` property to `ReconciliationOrchestrator` gated by `[Conditional("DEBUG")]`
- (B) Pass a `Func<MemoryStream>` factory to Stage 5 (testability seam) — over-engineering for MVP
- **(C, recommended)**: assert via `ExportCompletedEvent.ExportedSizeBytes > 200` (SIRO XML for a
  fully-populated record is always > 200 bytes) + test `SiroXmlExporter` directly for structural
  assertions (already done in `SiroXmlExporterTests.cs`). The orchestrator test only proves the
  plumbing is wired — the XML correctness proof is in the unit test.

### 4.3 Existing tests affected

| File | Impact |
|---|---|
| `ReconciliationPipelineServiceTests.cs` (line 39-44) | Mock is `IAdaptiveExporter`; change to `IResponseExporter` mock + `ExportSiroXmlAsync` stub |
| Any test asserting `Format = "Excel"` or `.xlsx` destination | Update to `"SiroXml"` / `.siro.xml` |
| `Program.cs` smoke / DI resolution tests (if any) | Add `TemplateConnection` config value to test `appsettings` |

Search: `grep -r "\"Excel\"" 08\ Tests/04\ Services/Athena` — confirms exactly which test lines need changing.

---

## 5. ITDD impact (ADR-005)

`ReconciliationOrchestrator` is not currently covered by an ITDD contract base (it is a concrete
class, not behind an interface, and its Stage 5 behavior is tested via
`ReconciliationPipelineServiceTests`). No new contract base is needed for this change.

The changed dependency (`IResponseExporter` replaces `IAdaptiveExporter`) already has a contract base
(`IResponseExporterContractTests` or equivalent under `Tests.Domain.Interfaces`) from the ITDD refactor
Phase 5. The new `SiroXmlExporter`-as-exporter path is covered by the existing
`SiroXmlExporterTests.cs` suite — no new contract inheritor is required.

If a future session adds an `IReconciliationOrchestrator` interface (to enable in-process substitution),
ITDD Phase 7 (`IFieldExtractor<T>` family) would be the natural companion scope.

---

## 6. Risks and honest gaps

### R1 — Fused `Expediente` may lack SIRO-required fields (HIGH)

`SiroXmlExporter.ValidateMetadata()` requires non-blank `NumeroExpediente` and `NumeroOficio`. The
fused `Expediente` from `FusionExpedienteService` is populated from OCR text via field extractors.
If OCR quality is low or the document is not a CNBV requerimiento, these fields may be empty.

**Consequence:** Stage 5 returns `Result.WithFailure`, `ProcessingErrorEvent` is emitted, but the
pipeline does not crash. The Reconciliator still emits its terminal event.

**Mitigation for MVP:** The Classification stage (Stage 4) already flags low-confidence docs for
manual review. An export failure on an unrecognised document is expected behavior. No code change needed.

### R2 — `AdaptiveExporter` "XML" path is NOT SIRO-conformant (design rationale captured here)

If a future developer calls `AddAdaptiveExportServices()` in the Reconciliator and the orchestrator
resolves `IAdaptiveExporter`, the produced XML will be `<Export>…</Export>` — not SIRO. This design
note is the audit trail. The comment in Program.cs (lines 58-61) should be updated to explicitly say
"do NOT call AddAdaptiveExportServices here — use AddExportServices for SIRO XML."

### R3 — No XSD (schema validation is structural only)

The bank SIRO XSD is not in the repo. The `XmlSchemaSet?` hook in `SiroXmlExporter` is ready but
dormant. Risk: the produced XML is structurally plausible but may fail bank-side schema validation
on a field we do not know about. **Mitigation:** request the `.xsd` from Banamex before go-live.

### R4 — XML bytes not persisted to shared storage (MVP scope)

The `ExportCompletedEvent.Destination` field names a path but Stage 5 does not write the file.
Downstream consumers (future audit or delivery steps) cannot read the XML from disk. This is
acceptable for MVP (event-driven proof) but must be resolved before production hand-off.

---

## 7. Owner decisions needed

| # | Decision | Recommendation |
|---|---|---|
| **D1** | Should Stage 5 **write the `.siro.xml` file to shared storage** in this session, or only emit the `ExportCompletedEvent` with bytes-in-memory? | **Defer to post-MVP.** Event-driven proof is sufficient for the A4/F1 DoD. File write adds `IStoragePathResolver` coupling to Stage 5 and a new test surface. |
| **D2** | Should the **`TemplateConnection` guard** be the sentinel for enabling SIRO export, or should export always be registered (SiroXmlExporter has no DB dependency)? | **Always register** `AddExportServices()` unconditionally — it has no DB dep. Drop the `TemplateConnection` guard entirely. Only the Adaptive/template-DB path needs a connection string sentinel, and we are NOT using the Adaptive path here. |
| **D3** | Is the bank SIRO **XSD available**? Should it be added to the repo under `Prisma/Fixtures/schemas/siro.xsd` and loaded via `XmlSchemaSet` in `AddExportServices()`? | If yes, add it now and enable schema validation in the Reconciliator. If not yet obtained, proceed with structural assertions (Option A, §3). |

---

## 8. Wiring delta summary (all changes)

```
Prisma/Code/Src/CSharp/
├── 04 Services/Athena/Prisma.Athena.Processing/
│   └── ReconciliationOrchestrator.cs
│       · field:  IAdaptiveExporter? → IResponseExporter?
│       · ctor:   IAdaptiveExporter? → IResponseExporter?
│       · Stage5: ExportAsync(obj,"Excel",ct) → ExportSiroXmlAsync(UnifiedMetadataRecord,stream,ct)
│       · Stage5: build UnifiedMetadataRecord { Expediente = fusionResult?.FusedExpediente }
│       · Stage5: null-guard on FusedExpediente (warn+skip)
│       · Event:  Format="Excel"→"SiroXml", Destination=".xlsx"→".siro.xml"
│
├── 04 Services/Reconciliator/Prisma.Reconciliator.Worker/
│   └── Program.cs
│       · add using for Infrastructure.Export.DependencyInjection
│       · add builder.Services.AddExportServices(builder.Configuration)  [unconditional per D2]
│       · change GetService<IAdaptiveExporter>() → GetService<IResponseExporter>()
│       · update comment block (lines 58-61) to warn against AddAdaptiveExportServices
│
└── 08 Tests/04 Services/Athena/Prisma.Athena.Processing.Tests/
    ├── ReconciliationOrchestratorSiroExportTests.cs  [NEW — 5 test cases, §4.2]
    └── ReconciliationPipelineServiceTests.cs
        · mock: IAdaptiveExporter → IResponseExporter
        · stub: ExportAsync(…) → ExportSiroXmlAsync(…)
        · assert: Format=="SiroXml" where currently "Excel"
```

No new projects. No new NuGet packages. `Infrastructure.Export` is already a project reference in
`Prisma.Athena.Processing` (via the existing `ExportService` path) — verify with
`dotnet list reference` before implementation.

---

## 9. Build verification steps (implementer checklist)

1. `dotnet build Prisma.Athena.Processing.csproj` — 0 errors (IResponseExporter swap)
2. `dotnet build Prisma.Reconciliator.Worker.csproj` — 0 errors (AddExportServices wiring)
3. `dotnet test Prisma.Athena.Processing.Tests.csproj` — all existing tests pass
4. New `ReconciliationOrchestratorSiroExportTests` — 5/5 green
5. Structural assertion: parsed XML root local name `ShouldBe("SiroResponse")`,
   namespace `ShouldBe("http://siro.regulatory.namespace")`
6. `dotnet test ExxerCube.Prisma.sln` — 0 regressions
