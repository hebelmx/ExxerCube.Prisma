# Item A (#7) — "Datos Carga de Oficio" Excel layout + Stage-5 wiring — Design

**Owner decision:** Template-repo driven (the 24 columns are defined as a `TemplateDefinition`/`FieldMapping`
data structure and mapped via `ITemplateFieldMapper`, NOT a hardcoded `cell = record.X` sequence).
**Orchestrator sub-decisions** (resolving the DB-seeding fork the owner answer left open):
- Provide the layout as a **built-in code-defined default** `TemplateDefinition` (`DatosCargaOficioTemplate.Default`).
  The generator resolves via `ITemplateRepository?.GetLatestTemplateAsync("DatosCargaOficio", ct)` and **falls
  back to the built-in default** when the repo is absent/empty. This is demo-safe (no DB seed required in the
  Reconciliator worker) AND repo-overridable (Web.UI with `TemplateDbContext` can override). `ITemplateRepository`
  is an **optional** dependency of the generator.
- **Fixed values live in the template** as `FieldMapping.DefaultValue` (data/config-editable), satisfying the
  "fixed values config-driven" sub-question.
- **Do NOT modify the existing `ExcelLayoutGenerator`** (12-col "SIRO Registration", 17 pinned tests). This is a
  NEW generator. Leave the old one untouched.

## The 24 columns (authoritative: `docs/product/requirements/Requirements.md:253-294`)

| # | Column header | Kind | Source / fixed value |
|---|---------------|------|----------------------|
| 1 | Procedencia | Fixed | `C.N.B.V. JUZGADOS` |
| 2 | Numero de expediente | Extracted | `Expediente.NumeroExpediente` |
| 3 | Oficio | Extracted | `Expediente.NumeroOficio` |
| 4 | Fecha de registro | Calculated | registration date (= recepción date for demo) |
| 5 | Fecha de recepción | Calculated | `Expediente.FechaRecepcion` (today / 00:00 t+1 rule) |
| 6 | Días | Extracted | `Expediente.DiasPlazo` |
| 7 | Fecha estimada de conclusión | Calculated | `FechaRecepcion` + `DiasPlazo` business days (`Expediente.FechaEstimadaConclusion` if populated) |
| 8 | Estatus | Fixed | `registrado` |
| 9 | Tipo de asunto | Derived | one of EMBARGO / DESEMBARGO / DOCUMENTACIÓN / INFORMACIÓN / TRANSFERENCIAS — from classification / `ComplianceActionKind` / `TieneAseguramiento`+`AreaDescripcion` |
| 10 | Grupo | Fixed | `CNBV - Filiales` |
| 11 | Área remitente | Fixed | `COMISION NACIONAL BANCARIA Y DE VALORES` |
| 12 | Subdivisión | Extracted | `Expediente.Subdivision` (LegalSubdivisionKind → "A/AS" etc. code) |
| 13 | Entidad Financiera | Fixed | `Banco` |
| 14 | Descripción | Composed | `Paterno + " " + Materno + " " + Nombre` from `SolicitudPartes[0]` |
| 15 | Nombre del remitente | Extracted | signing functionary (`Expediente.AutoridadNombre` for now; Word-image OCR is item D stretch) |
| 16 | Origen | Fixed | `Oficio` |
| 17 | Tipo de documento | Fixed | `Oficio` |
| 18 | Medio de seguimiento | Fixed | `Carta` |
| 19 | Nombre Abogado Interno | Fixed | `Airam Zepol Zepol` |
| 20 | Nombre abogado responsable | Fixed | `Nauj Zerep Zerep` |
| 21 | Despacho | Fixed | `El abogado justo` |
| 22 | Estado | Fixed | `CIUDAD DE MEXICO` |
| 23 | Ciudad | Fixed | `MEXICO` |
| 24 | Zona | Fixed | `METROPOLITANO` |

> The fixed-value strings (abogados, despacho, estado, etc.) are **demo placeholders** baked into the built-in
> template's `DefaultValue`s — editable by overriding the template (DB) or the `DatosCargaOficioTemplate` factory.

## Tipo de asunto derivation (column 9)
Map from the classification / compliance signal to the 5-value combo:
- Aseguramiento / Block action / `TieneAseguramiento==true` + `AreaDescripcion` ASEGURAMIENTO → **EMBARGO**
- Desembargo / Unblock action → **DESEMBARGO**
- Documentación / Document action → **DOCUMENTACIÓN**
- Transferencia / Transfer action → **TRANSFERENCIAS**
- Información / Information action → **INFORMACIÓN**
Default/unknown → best-available (EMBARGO if `TieneAseguramiento`, else INFORMACIÓN). Keep the mapping in one
small pure method so it is unit-testable and mutation-killable.

## Components
1. **`DatosCargaOficioTemplate`** (static factory) → `TemplateDefinition` with 24 `FieldMapping`s
   (TargetField=header, DisplayOrder=1..24, DefaultValue for fixed, SourceFieldPath for projection-mapped, Format
   "yyyy-MM-dd" for dates). Place in `Infrastructure.Export.Adaptive`.
2. **`DatosCargaOficioProjection`** (flat POCO) computed from `UnifiedMetadataRecord` (+ `ClassificationResult`/
   `ComplianceActions` if available): holds the 10 extracted/calculated/derived values (NumeroExpediente, Oficio,
   FechaRegistro, FechaRecepcion, Dias, FechaEstimadaConclusion, TipoAsunto, Subdivision, Descripcion, Remitente).
   A pure builder method `DatosCargaOficioProjection.From(record, ...)`. Fixed columns are NOT on the projection —
   they come from the template `DefaultValue`.
3. **`DatosCargaOficioLayoutGenerator : IDatosCargaOficioLayoutGenerator`** —
   `Task<Result> GenerateAsync(UnifiedMetadataRecord metadata, Stream outputStream, CancellationToken ct)`.
   Deps: `ITemplateFieldMapper` (required), `ITemplateRepository?` (optional), `ILogger<>`. Resolves template
   (repo→fallback built-in), builds projection, `MapAllFieldsAsync(projection, template)` → ordered dict, writes
   one worksheet "Datos Carga de Oficio" via ClosedXML (header row bold+gray like the existing generator,
   autofit), saves to stream. Full `Result<T>` + cancellation + null-guard discipline.
4. **DI** — new `AddDatosCargaOficioExportServices(this IServiceCollection, lifetime)` in
   `Infrastructure.Export.Adaptive` (or extend export DI): registers `ITemplateFieldMapper`→`TemplateFieldMapper`
   and `IDatosCargaOficioLayoutGenerator`→`DatosCargaOficioLayoutGenerator`. Do NOT force `TemplateDbContext`.
5. **Stage 5 wiring** — inject `IDatosCargaOficioLayoutGenerator?` + `IStoragePathResolver?` into
   `ReconciliationOrchestrator` (optional ctor params, matching the existing optional-dep style). In
   `ExecuteStage5ExportAsync`, AFTER the SIRO XML export, if the generator is present: generate the xlsx; if
   `IStoragePathResolver` resolves `exports/{fileId}.datos-carga-oficio.xlsx` to an absolute path, write the bytes
   there; emit `ExportCompletedEvent { Format = "DatosCargaOficioXlsx", Destination = "exports/{fileId}.datos-carga-oficio.xlsx" }`.
   If resolver is null/fails, still emit the event with the in-memory size (preserve current no-disk behavior).
   Do NOT touch the SIRO XML path. Register the generator (+ resolver already wired) in the Reconciliator worker host.

## Tests (ITDD per ADR-005)
- **Unit** `DatosCargaOficioLayoutGeneratorTests` (new, in `Tests.Infrastructure.Export.Adaptive`): pins all 24
  headers in order; fixed values present; extracted values mapped from a sample `UnifiedMetadataRecord`; Tipo de
  asunto theory over the 5 cases; date format ISO; cancellation/null-guard/non-writable-stream guards (mirror the
  existing `ExcelLayoutGeneratorTests`).
- **Integration** extend `ReconciliationOrchestratorSiroExportTests` (or a sibling): a fused expediente →
  Stage 5 emits BOTH the SIRO XML event AND a `Format=="DatosCargaOficioXlsx"` event; the xlsx parses and has the
  24 headers. Resolver-null path still emits the event.

## Definition of done
Build 0/0 for touched projects; `Tests.Infrastructure.Export.Adaptive` + `Prisma.Athena.Processing.Tests` green;
the existing `ExcelLayoutGeneratorTests` (12-col) still green (untouched). `git diff` confined to the new
generator/template/projection, the DI extension, the orchestrator Stage 5 region, and the worker host registration.
