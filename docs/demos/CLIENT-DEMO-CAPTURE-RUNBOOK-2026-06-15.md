# Client Demo — Capture Runbook (Atención a Autoridades)

> **Date:** 2026-06-15 · **Branch:** Kt2 · **Audience:** compliance / business owner (her
> "Atención a Autoridades" team at Banamex). **Format:** several short screen-capture clips
> (2–4 min each), **live UI in a curated order**, narration in **Spanish** mirroring her
> exact field names. Source checklist: `docs/legal/samples/Checklist+2+Demo+AA.xlsx`
> (4 etapas / 7 pasos).

This runbook is the shot list. Each capture maps 1:1 to a paso in her Excel, names the
**faithful on-screen source** (verified against the code, not aspirational), gives the exact
setup, and a Spanish narration line. Where a paso has **no faithful product UI today**, that is
called out explicitly with the recommended workaround — do **not** improvise on camera.

---

## ⚠️ EXECUTION ADDENDUM — 2026-06-25 (Linux box + §2 gate) — READ BEFORE FILMING

> **Why this exists:** the shot list below (§0–Appendix) was authored 2026-06-15 on branch `Kt2`
> for a **Windows** box (PowerShell, `E:/`, LocalDB, per-stage launches). Development has since
> moved to the **Linux (Ubuntu 26.04) dev box** on branch `Liv`, and the canonical full-pipeline
> proof is now the **§2 max-fidelity gate** (`MaxFidelityGateFullPipelineE2ETests`), which runs the
> real **3-process split** end-to-end. This addendum is the **current** execution truth; the
> Windows commands in §0–§5 remain valid *only* if you film on the old Windows box. On the Linux
> box, translate every `powershell`/`$env:`/`E:/` command to the bash recipe in **A2** below.

### A0. GO / NO-GO gate — do NOT film the live full pipeline until BOTH are green

Two blockers currently stop a clean live full-pipeline run on Linux. **Dedicated agents are fixing
both right now** (branch `Liv`); this runbook is staged to execute *the moment they land*.

| # | Blocker | Brief | Owner agent | Blocks which captures |
|---|---------|-------|-------------|-----------------------|
| 1 | Native OCR **SIGSEGV** (Tesseract/Leptonica ⟂ SkiaSharp/Emgu coexistence) — gate host dies exit 139 in Stage 2 | `docs/planning-artifacts/remediation/TASK-OCR-SEGFAULT-LINUX.md` | `ocr-segfault-troubleshooter` | Capture 2 (extraction), Capture 3 (layout), and the §2 gate "money shot" |
| 2 | **Corpus inconsistency** — improvised PRP1 companions disagree → fusion `ManualReviewRequired` → export gate BLOCKS | `docs/planning-artifacts/remediation/TASK-GATE-CORPUS-SYNTHETIC-WORKLOAD.md` | `siara-corpus-generator` | Capture 3 (clean SIRO/xlsx export), Capture 5 close (green gate) |

**Validation command — "is it green now?"** (run this; a clean pass = GO for the live full-pipeline captures):
```bash
TESSDATA_PREFIX=/usr/share/tesseract-ocr/5/tessdata \
dotnet test "Prisma/Code/Src/CSharp/08 Tests/06 E2E/Tests.AllRealWireE2E/ExxerCube.Prisma.Tests.AllRealWireE2E.csproj" \
  --filter-query "/*/*/MaxFidelityGateFullPipelineE2ETests/RealSiaraCase_FlowsAcrossAllThreeProcesses_WithRealPipeline_AndPersistsAudit"
```
- Until **Blocker 1** is fixed: this dies with exit 139 (SIGSEGV) right after `Executing Tesseract OCR` → **NO-GO** for any OCR-bearing live capture.
- After Blocker 1, until **Blocker 2** is fixed: the run survives OCR but export **BLOCKS** at the gate (fusion `ManualReviewRequired`) → the green-export captures (3, 5) are **NO-GO**; OCR-extraction captures are filmable.
- Both green → **GO**. The captures that don't touch the live pipeline (Capture 0 simulator, Capture 4 `/oficio-summary`, the SLA/dashboard pages in Capture 5) are filmable **regardless** — record those first.

### A1. The §2 gate is the new headline capture ("money shot")

The strongest single piece of demo evidence is the **§2 gate running green end-to-end** — it proves
the real **3-process split** with no mocks:
```
SIARA Simulator  ──(real browser download)──▶  Orion (Downloader)
   ──(SignalR/Ember over real TCP)──▶  Athena (Extractor: Quality→OCR→Fusion→Classify)
   ──(SignalR/Ember over real TCP)──▶  Reconciliator (Export: SIRO XML + DatosCarga xlsx)
   ──▶  SQL audit ledger (rows from ≥2 distinct ProcessIds)
```
Evidence: `Prisma/Code/Src/CSharp/08 Tests/06 E2E/Tests.AllRealWireE2E/MaxFidelityGateFullPipelineE2ETests.cs`
(test `RealSiaraCase_FlowsAcrossAllThreeProcesses_WithRealPipeline_AndPersistsAudit`). It asserts a
populated SIRO XML (`SiroResponse` root, `NumeroExpediente`/`NumeroOficio`), a 24-column DatosCarga
xlsx, and ≥2-process audit rows. **Recommended new capture order:** film the green gate run first as
"prueba de sistema completo", then drill into the per-etapa UI captures (§1–§5) for the narrative.
> Note vs. §0.4: the gate runs OCR + the 3-process pipeline across **separate processes**, which is
> exactly why the single-process Tesseract re-init deadlock doesn't apply to it — but the *new*
> Linux SIGSEGV (Blocker 1) is a different, native-coexistence crash. Both are handled by A0.

### A2. Linux box launch recipe (translate §0–§5 PowerShell to this)

One-time box setup (full recipe: `docs/planning-artifacts/remediation/EXECUTION-TRACKER.md`, 2026-06-25 handoff):
```bash
# Native Tesseract 5 data (eng/spa/osd present on this box)
export TESSDATA_PREFIX=/usr/share/tesseract-ocr/5/tessdata
# Playwright chromium for Ubuntu 26.04 (PW 1.60 refuses the 26.04 download → symlink)
ln -sfn ~/.cache/ms-playwright/chromium-1228 ~/.cache/ms-playwright/chromium-1223
# Chromium sandbox under Ubuntu 26.04 (reversible; restore to 1 or reboot after)
sudo sysctl -w kernel.apparmor_restrict_unprivileged_userns=0
# Docker must be up for Testcontainers SQL 2025 (the gate provisions its own DB)
```
SIARA simulator — run **http-only** so the gate/worker plain `HttpClient` probe works (sim on
`http://localhost:5001`), from `Deployments/Siara.Simulator/app/`:
```bash
ASPNETCORE_ENVIRONMENT=Development \
Kestrel__Endpoints__Https__Url=http://localhost:5002 \
dotnet Siara.Simulator.dll
# login BANAMEX / password123 ; corpus served from ../bulk_generated_documents_all_formats (gitignored)
```
PowerShell→bash command map for the per-stage captures:
- `$env:NAME = "v"` → `export NAME=v` (or inline `NAME=v dotnet …`).
- `E:/Dynamic/_demo/storage` → a local shared dir, e.g. `export Storage__BasePath="$HOME/_demo/storage"` (all 3 workers must point at the **same** path).
- LocalDB conn strings (§0.1/§1) → either let the gate use Testcontainers SQL, or point at a real SQL 2025 via `ConnectionStrings__DefaultConnection` / `__ApplicationConnection` (the Web.UI appsettings now ship **empty** conn strings by design — supply them via env, so §0.1's "delete the hardcoded file" step is obsolete; just export the two vars).
- Orion (Capture 1): `cd "Prisma/Code/Src/CSharp/04 Services/Orion/Prisma.Orion.Worker" && ASPNETCORE_ENVIRONMENT=Development NavigationTargets__SiaraUrl=http://localhost:5001 Storage__BasePath="$HOME/_demo/storage" dotnet run`.
- Athena (extraction) and **Reconciliator** (export) are now **separate worker processes** — the old runbook folded export into "Athena + Reconciliator"; on Linux launch all three (Orion, Athena, Reconciliator) as distinct `dotnet run`s pointing at the same `Storage__BasePath`, mirroring the gate.

### A3. Live-status hook — UPDATED 2026-06-25 after the first combined gate run

A full §2 gate run was executed on the Linux box with **both** fixes resident (commits `ea90aa17`
+ `3c7532a2`) against the tier-1 corpus. Result: the pipeline advanced from "segfault at Stage 2"
all the way through fusion to **Stage 4**, then a **new, third** gate condition blocked export.

- [x] **Blocker 1 (OCR SIGSEGV) — FIXED & PROVEN.** Gate Stage 2 ran Tesseract clean: `Text length: 1284, Confidence avg: 95.71%` — no exit 139. _Owner: `ocr-segfault-troubleshooter` (done)._
- [x] **Blocker 2 (consistent corpus) — FIXED & PROVEN.** Gate fusion: `Confidence: 0.83, Conflicts: 0, NextAction: Revisar recomendado` (NOT ManualReviewRequired) → fusion no longer blocks export. _Owner: `siara-corpus-generator` (done)._
- [ ] **Blocker 3 (NEW) — classification confidence.** `Stage 5 BLOCKED by export gate: Classification confidence 10% is below the required threshold of 70% (BlockOnLowConfidence)`. Stage 4 classified the tier-1 case as `Aseguramiento` at **10%**. **Deep inspection done (2026-06-25):** the root cause is a **wiring defect** — Stage 4 is fed only `AreaDescripcion + NumeroExpediente`; the 1284-char OCR body text (which DOES contain "aseguramiento" 4+ times) never reaches the classifier, so all categories tie at the no-match floor and `(Aseguramiento, 10)` is a tie-break artifact. NOT a corpus gap. Tier-1 fix is a one-line wiring change (turns the gate green); deeper finding is that the whole confidence pipeline lacks a unified model. Full evidence + tiered hardening/re-architecture plan: **`docs/planning-artifacts/remediation/CONFIDENCE-PIPELINE-DEEP-INSPECTION-2026-06-25.md`**.
- [ ] §2 gate green end-to-end on Linux (A0 validation command passes) → **STILL NO-GO** for the live full-pipeline captures (blocked on #3 only). Captures that don't touch the live pipeline (Capture 0 sim, Capture 4 `/oficio-summary`, SLA/dashboard) remain filmable now.

---

## 0. Pre-flight (do this once, before any recording)

### 0.1 Fix the hardcoded DB server (blocks Web.UI boot off-box)
`07 UI/UI/ExxerCube.Prisma.Web.UI/appsettings.Development.json` hardcodes
`Server=DESKTOP-FB2ES22\SQL2022`. Before launching the UI either delete/rename that file or
override:
```powershell
$env:ConnectionStrings__DefaultConnection   = "Server=(localdb)\MSSQLLocalDB;Database=PrismaID;Trusted_Connection=True;TrustServerCertificate=True"
$env:ConnectionStrings__ApplicationConnection= "Server=(localdb)\MSSQLLocalDB;Database=Prisma;Trusted_Connection=True;TrustServerCertificate=True"
```

### 0.2 Avoid the port clash
The **simulator** listens on `http://localhost:5001` + `https://localhost:5002`.
The **Web.UI** defaults to `http://localhost:5000` + `https://localhost:5001` — its HTTPS port
(5001) **collides** with the simulator's HTTP port. Launch the UI on its own ports:
```powershell
$env:ASPNETCORE_URLS = "https://localhost:7443;http://localhost:7080"
```

### 0.3 Demo data — already aligned, use it
Real corpus: `docs/legal/samples/` → `222AAA-…`, `333BBB-…`, `333ccc-…`, `555CCC-…`
(each PDF + XML + DOCX). Her **"Listado"** tab lists `777XXX-…` (which we do **not** have)
and does **not** list `555CCC-…` (which we **do** have). That gives a real, honest
**falta = 777XXX / sobra = 555CCC** pair for Paso 3 — no fabrication needed. Use it.

### 0.4 Tesseract caveat (hard rule)
Tesseract cannot re-initialize twice in one process (documented deadlock). **Never** try to run
live OCR + the rest of the pipeline in a single process on camera. Capture the download/extract
path and any OCR-bearing path as **separate launches** (this is exactly what the E2E test does).

### 0.5 Recommended recording order
Record the **stable, high-impact** segments first (Etapa 3 Excel, Etapa 2 extraction), then the
live download (Etapa 1) last. Re-recording one clip is cheap; that's the point of clip-per-etapa.

### 0.6 ⚠️ Apply the latest schema — the review dashboard depends on it (REQUIRED)
**Captures 2 (Paso 5 alertamiento) and 5 (review/audit) read the manual-review dashboard, which
shows cases the *real pipeline* flags.** Before the `DropReviewCaseFileMetadataFk` fix (2026-06-15),
every worker `INSERT` into `ReviewCases` failed on `FK_ReviewCases_FileMetadata_FileId` (the worker
persists by `FileId` with no `FileMetadata` parent) and was silently swallowed — so the dashboard
showed **nothing** from the live pipeline. Ensure the demo database has the FK dropped:
- If the DB is created fresh from the current EF model (`EnsureCreated`) or by applying migrations
  (`DropReviewCaseFileMetadataFk`), it is already correct — nothing to do.
- If you reuse an **older** demo DB, either re-create it or run `dotnet ef database update` so the FK
  is dropped; otherwise Paso 5 and the close will look empty even though the pipeline ran.
Proven by `ReviewCaseFkDropIntegrationTests` (real-SQL). The audit trail is unaffected (its FK was
already dropped).

### 0.7 DOCX field extraction is fixed (issue #2)
Paso 4 over the real Word doc now surfaces the **CNBV "Oficio Núm."** (`222/AAA/-4444444444/2025`,
not the embedded source oficio) and the **"Folio Núm."** expediente (whitespace normalised), and
the worker's DOCX→fusion path now actually receives `NumeroOficio`. So the Word companion genuinely
contributes to cross-source fusion. Pinned by `DocxFieldExtractorTests` + the hardened
`DemoChecklistSevenStepsTests` Step 4.

---

## 1. Capture sequence (the shot list)

| # | Clip | Pasos | Faithful on-screen source | Confidence |
|---|------|-------|---------------------------|------------|
| 0 | Overview / el problema | framing | Simulator dashboard, cases arriving | ✅ live |
| 1 | Etapa 1 — Descarga + Listado + Cotejo | 1, 2, 3 | Simulator + Orion worker logs/cycle report | ✅ live |
| 2 | Etapa 2 — Extracción + Validación cruzada | 4, 5 | Web.UI `/document-processing` + `/manual-review/{id}` | ✅ live |
| 3 | Etapa 3 — Layout SIRO "Datos Carga de Oficio" | 6 | **Pipeline artifact** `exports/{id}.datos-carga-oficio.xlsx` opened in Excel | ⚠️ artifact, not in-UI |
| 4 | Etapa 4 — Resumen IA (5 apartados) | 7 | Web.UI `/oficio-summary` (live page) | ✅ live |
| 5 | Cierre — Confianza / Auditoría | all | `/sla-dashboard` + `/dashboard` + green E2E test | ✅ live |

---

## Capture 0 — Overview / "el problema" (~1.5 min)

**Run:**
```powershell
cd "tools/Siara.Simulator"; dotnet run
# → https://localhost:5002 · login BANAMEX / password123
```
**Show:** the simulator dashboard with oficios arriving in real time; click a case to reveal its
**PDF / XML / Word** trio.

**Narración (ES):**
> "La CNBV publica oficios de autoridad en SIARA. Cada oficio llega como **tres archivos** —
> PDF, XML y Word. Hoy se descargan y capturan a mano: lento, propenso a error, y con plazos
> regulatorios encima. Esto es lo que vamos a automatizar."

> Nota legal en cámara: aclarar que es un **simulador** de SIARA (no el portal real), con
> documentos sintéticos. (Ver `tools/Siara.Simulator/DEMO_MANUAL.md`.)

---

## Capture 1 — Etapa 1: Descarga automática + Listado + Cotejo (Pasos 1–3, ~3 min)

**What the product does:** Orion's `SiaraWatchLoop` polls the simulator, `SiaraDocumentDownloader`
auto-downloads the 3 companions per case, the watch loop emits a per-cycle **downloaded-file
list** (name + extension), and `ManifestReconciliationService` cotejos that list against the
expected "Listado" into **Missing / Extra / Partial / Complete** buckets.

**Run** (separate terminal from the simulator):
```powershell
cd "Prisma/Code/Src/CSharp/04 Services/Orion/Prisma.Orion.Worker"
$env:ASPNETCORE_ENVIRONMENT          = "Development"
$env:NavigationTargets__SiaraUrl     = "https://localhost:5002"   # the simulator
$env:Storage__BasePath               = "E:/Dynamic/_demo/storage" # shared download dir
$env:ConnectionStrings__DefaultConnection = "Server=(localdb)\MSSQLLocalDB;Database=PrismaID;Trusted_Connection=True;TrustServerCertificate=True"
dotnet run
```
**Show, in order:**
1. **Paso 1** — worker log lines as the 3 companions download for a case; then the files
   appearing under `Storage:BasePath`.
2. **Paso 2** — the per-cycle **listado** (file name + extensión) the watch loop reports.
3. **Paso 3** — the reconciliation buckets: **`777XXX` → FALTA**, **`555CCC` → SOBRA**.

**Narración (ES):**
> "El sistema se conecta al sitio de publicación y **descarga automáticamente** los tres
> archivos de cada oficio. Genera un **listado** con el nombre y la extensión de cada archivo, y
> lo **coteja contra el listado esperado**: aquí detecta que **falta el 777XXX** y que **sobra el
> 555CCC** — exactamente el control de la Etapa 1."

> **If a fully-live worker run is fragile on the day**, fall back to the deterministic proof:
> `dotnet test --filter "FullyQualifiedName~DemoChecklistSevenStepsTests" ...` Steps 1–3 assert
> the 3-companion enumeration and the Missing/Extra buckets over the real corpus.

---

## Capture 2 — Etapa 2: Extracción de campos + Validación cruzada (Pasos 4–5, ~4 min)

**What the product does:** field extraction from XML + PDF + Word (the "Datos descargados" set),
then cross-source fusion that raises an **alertamiento** when two documents disagree, routing the
case to manual review with reason **"Cross-validation field mismatch (alertamiento)"**.

**Run the Web.UI** (separate terminal; env from §0.1–0.2 set):
```powershell
cd "Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI"; dotnet run
# → https://localhost:7443
```

**Paso 4 — Extracción.** Navigate to **`/document-processing`**.
- Load the sample **XML** and **PDF** for `222AAA-…` (the page has XML load, PDF/OCR load, and a
  bulk sample loader).
- Show the extracted SIRO fields and name them in her vocabulary: **Número de expediente**,
  **Oficio**, **Días**, **Subdivisión** (the field the docs call *AreaDescripción*),
  **Descripción** (Paterno + Materno + Nombre), **RFC**, **Dirección/Domicilio**, **Nombre del
  remitente** (the funcionario in the Word signature image), **Número identificador del
  requerimiento**.

**Paso 5 — Validación cruzada → alertamiento.** Navigate to **`/manual-review`**, filter by
**Review Reason = field mismatch**, open a flagged case at **`/manual-review/{CaseId}`**.
- Show the **"Conflicting values detected across sources"** warning banner on the conflicting
  field, with the per-source values side by side.

**Narración (ES):**
> "De los tres documentos extraemos los datos que pide la pestaña *Datos descargados*: expediente,
> oficio, días, subdivisión, descripción, RFC, domicilio, y el **nombre del remitente** —que viene
> dentro de la **imagen de la firma** del Word. Luego **comparamos la información de dos
> documentos**; cuando no coinciden, el sistema levanta un **alertamiento** y manda el caso a
> revisión, con el conflicto resaltado campo por campo."

> To guarantee an alert on camera, pick (or stage) a case where XML and PDF disagree on
> `NumeroExpediente`. The E2E test Step 5 does exactly this if you need the deterministic version.

---

## Capture 3 — Etapa 3: Layout SIRO "Datos Carga de Oficio" (Paso 6, ~3 min)

> ✅ **Fixed (GH #14):** `/export-management` → "Datos Carga de Oficio (Excel)" now calls the real
> 24-column `DatosCargaOficioLayoutGenerator`, hydrating the **real** consolidated metadata from
> `IUnifiedMetadataStore` (keyed by FileId). It no longer fabricates placeholder values — if a case
> hasn't been processed through the pipeline yet, it fails with a clear message instead of producing
> a wrong file. **For the demo, prefer the pipeline artifact** (below) for guaranteed reliability;
> the in-UI download is now a correct secondary path once the case has been processed.

**Faithful source — pick one:**

- **(A, recommended) Pipeline artifact.** Run the Athena + Reconciliator workers over `222AAA-…`
  so Stage 5b writes the real xlsx, then **open it in Excel** on camera. (The pipeline run is the
  same 3-process path exercised by the MaxFidelity gate.)
- **(B, most reliable) Pre-generate before filming.** Run the demo E2E test, which builds the real
  24-column workbook via `DatosCargaOficioLayoutGenerator` over the real `222AAA` XML and verifies
  all 24 headers + fixed values + dates. Save its xlsx and open in Excel:
  ```powershell
  dotnet test "Prisma/Code/Src/CSharp/08 Tests/06 E2E/Tests.AllRealWireE2E" `
    --filter "FullyQualifiedName~DemoChecklistSevenStepsTests"
  ```

**Show in Excel:** the **24 columns** in row 1; the **fixed values** ("C.N.B.V. JUZGADOS",
"registrado", "Banco", etc.); the **calculated dates** (recepción / registro / **conclusión** =
días hábiles + recepción, holiday-aware via the Mexican-holiday calendar); **Subdivisión** and
**Descripción** populated from the extracted data.

**Narración (ES):**
> "Con la información de la Etapa 2, el sistema **genera el layout de carga en Excel** con los
> campos de *Datos Carga de Oficio*: los datos fijos (Procedencia, Estatus *registrado*, Entidad),
> los datos del oficio (expediente, oficio, subdivisión, descripción), y las **fechas calculadas**
> —incluida la **fecha estimada de conclusión** contando **días hábiles** y días festivos de
> México. Es el archivo listo para registrar en SIRO."

---

## Capture 4 — Etapa 4: Resumen IA del oficio en 5 apartados (Paso 7, ~3 min)

> ✅ **Now a live in-product page** (built 2026-06-15): **`/oficio-summary`** ("Resumen del Oficio",
> under the *Document Processing* nav section). It runs the real `ISemanticAnalyzer`
> (`SemanticAnalyzerService`) over the oficio text and renders the **5 apartados** as cards —
> each with **Sí/No** + **confianza** and the sub-answers (cuentas, productos, monto, parcial/total,
> expediente original, información solicitada). Behavior is pinned green by
> `OficioSummaryDemoSamplesTests` (5/5) in `Tests.AllRealWireE2E`.

**Run** (Web.UI already up from Capture 2):
1. Navigate to **`/oficio-summary`**. The page pre-loads the **Bloqueo** sample (proven to populate
   all sub-answers), so it's demo-ready on first render.
2. Click **"Analizar Oficio"** → the 5 cards render. Show **1. Requiere Bloqueo = Sí**, with
   **Tipo = Parcial**, **Monto = $500,000.00**, **Cuentas** (12345678, 87654321), **Productos**
   (TARJETA DE CRÉDITO).
3. Use the **quick-fill buttons** (Bloqueo / Desbloqueo / Documentación / Transferencia /
   Información) to demonstrate each of the 5 apartados in turn → click **Analizar** after each.
4. *(Optional, most faithful)* paste the **real oficio text** for `222AAA-…` into the textarea and
   analyze — shows it reading an actual document, not just samples.

**Narración (ES):**
> "Finalmente, el sistema **lee e interpreta el oficio** y lo resume en los **cinco apartados**
> que ustedes manejan: si **requiere bloqueo**, **desbloqueo**, **documentación**, **transferencia
> de fondos** o **información** —y extrae el detalle: **cuentas**, **montos**, **productos**, y si
> el bloqueo es **parcial o total**."

> Note: the classifier uses **fuzzy** phrase matching, so a strong *bloqueo* text can also show a
> secondary *desbloqueo = Sí* (the words are near-identical). This is real production behavior —
> just narrate the primary apartado. The confidence % is shown per card.

---

## Capture 5 — Cierre: Confianza y Auditoría (~2 min)

**Show, live in the Web.UI:**
- **`/sla-dashboard`** — countdown timers, escalation levels (Warning / Critical / Breached) —
  ties back to the regulatory deadline pain in Capture 0.
- **`/dashboard`** — documents processed, success rate, average confidence, system health.
- Then the green run of the **7-step demo E2E** over the real corpus:
  ```powershell
  dotnet test "Prisma/Code/Src/CSharp/08 Tests/06 E2E/Tests.AllRealWireE2E" `
    --filter "FullyQualifiedName~DemoChecklistSevenStepsTests"
  ```

**Narración (ES):**
> "Todo el proceso tiene **tablero de SLA** con los plazos en cuenta regresiva, **métricas** y
> **trazabilidad**. Y cada uno de los siete pasos que vieron está **probado de forma automática
> sobre documentos reales** — no es una maqueta, es el sistema."

---

## Appendix — Faithfulness ledger (what's live vs. artifact vs. gap)

| Paso | Capability | Faithful demo source | Status |
|------|-----------|----------------------|--------|
| 1 Descarga 3 archivos | `SiaraWatchLoop` + `SiaraDocumentDownloader` | Orion worker live vs. simulator | ✅ live |
| 2 Listado | per-cycle downloaded-file report | Orion worker log/report | ✅ live |
| 3 Cotejo listado | `ManifestReconciliationService` (Missing/Extra/Partial) | Orion worker; 777XXX/555CCC pair | ✅ live |
| 4 Extracción | XML/PDF/DOCX extractors (incl. Word signature-image OCR) | `/document-processing` | ✅ live |
| 5 Validación cruzada → alertamiento | fusion conflict → `FieldConflictAlert` / `ReviewReason.FieldMismatch` | `/manual-review/{id}` warning banner | ✅ live |
| 6 Layout Datos Carga de Oficio (24 col) | `DatosCargaOficioLayoutGenerator`, pipeline Stage 5b + `/export-management` (GH #14) | xlsx artifact opened in Excel (UI download is a correct secondary path post-processing) | ✅ pipeline; UI fixed |
| 7 Resumen 5 apartados | `SemanticAnalyzerService` | `/oficio-summary` live page (built 2026-06-15) | ✅ live |

**Integrity note for whoever films:**
1. `/export-management` "Datos Carga de Oficio (Excel)" now produces the real 24-column layout
   (GH #14 fixed) — but it needs the case to have been **processed** (metadata in
   `IUnifiedMetadataStore`); otherwise it fails cleanly. For a guaranteed take, use the **pipeline
   xlsx** `exports/{id}.datos-carga-oficio.xlsx` (Capture 3).
</content>
</invoke>
