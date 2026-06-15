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

> ⚠️ **Do NOT use the `/export-management` page for this.** Its "Excel FR18" button calls the
> legacy 12-column `ExcelLayoutGenerator` with **placeholder** metadata (`FileId` as expediente,
> `"SYSTEM"` as authority) — it would show empty/wrong values on camera. The correct **24-column
> "Datos Carga de Oficio"** layout is produced only by the **Reconciliator/Athena pipeline
> (Stage 5b)** and written to `exports/{fileId}.datos-carga-oficio.xlsx` under `Storage:BasePath`.

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
| 6 Layout Datos Carga de Oficio (24 col) | `DatosCargaOficioLayoutGenerator`, pipeline Stage 5b | xlsx artifact opened in Excel | ⚠️ artifact (NOT the `/export-management` button) |
| 7 Resumen 5 apartados | `SemanticAnalyzerService` | `/oficio-summary` live page (built 2026-06-15) | ✅ live |

**Integrity note for whoever films:**
1. `/export-management` "Excel FR18" ≠ the client's layout (legacy 12-col generator + placeholder
   data). Use the **pipeline xlsx** `exports/{id}.datos-carga-oficio.xlsx` (Capture 3).
</content>
</invoke>
