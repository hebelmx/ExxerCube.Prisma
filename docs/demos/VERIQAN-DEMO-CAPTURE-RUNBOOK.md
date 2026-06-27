# Veriqan VEC — Demo Capture Runbook

> **Date:** 2026-06-27 · **Branch:** `Liv` · **Audience:** banking client (mixed technical /
> legal / accounting / financial). **Format:** 6 screen-capture clips (Captures 0–5), narration
> in **Spanish**, each segment mapped to the 55-item VEC checklist (CL-1…CL-55). Source
> checklist: `Prisma/Fixtures/PRP2/Check+list+demo+v2+Iqubica.xlsx`.

**VEC = Verificación de Estado de Cuenta** automates the quality gate for bank credit-card
statements: 13–20 million statements per month are today verified manually against a
55-item regulatory checklist; Veriqan does this in seconds, degrades gracefully (abstains
or routes to a human; never silently auto-rejects), and captures every finding for audit
and calibration. The demo is **hybrid**: a deterministic `VecChecklistDemoE2ETests` suite
(W4) drives the pipeline over a 4-fixture anonymized corpus and writes artifacts to a
known `exports/` directory; a visual Blazor demo UI (W-UI) reads those artifacts and
presents the pipeline story. The test proves the result is real; the UI makes it legible
to a mixed audience. Each capture below names the exact on-screen source, the checklist
items it exercises, a Spanish narration script, and the GO/NO-GO gate that must hold
before the camera rolls. Where a workstream is **not yet complete** (W4, W-UI), that
dependency is called out explicitly — do not improvise on camera.

---

## 0. Pre-flight setup

### 0.1 Environment — Veriqan Worker appsettings

The Worker (`Prisma/Code/Src/CSharp/04 Services/Veriqan.Worker/`) reads configuration
from `appsettings.json` and environment-variable overrides. Three sections must be
configured before any test or live run. Provide all values via environment variables
rather than editing `appsettings.json`; the startup validator logs a structured WARNING
for every key that has no safe default.

**ConnectionStrings:VeriqanDb** (SQL Server for verdict, finding, and audit persistence):
```bash
export ConnectionStrings__VeriqanDb="Server=localhost,1433;Database=VeriqanDemo;User Id=sa;Password=Demo_2026!;TrustServerCertificate=True"
```
Quick local container:
```bash
docker run -d --name veriqan-sql \
  -e ACCEPT_EULA=Y -e SA_PASSWORD=Demo_2026! \
  -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
```
EF migrations run automatically at Worker startup (`VeriqanLegalBaselineStartupService`);
the database is created and migrated on first launch. No manual `dotnet ef` step needed.

> When `ConnectionStrings__VeriqanDb` is absent, the Worker falls back to **in-memory
> persistence** and logs a WARNING. In-memory state is lost on restart — use the SQL
> path for any capture involving the disposition or audit trail (Capture 5).

**Veriqan:LegalBaseline:EncryptionKey** (required when SQL persistence is active):
```bash
# Generate a fresh demo key — never commit a real production key:
export Veriqan__LegalBaseline__EncryptionKey="$(openssl rand -base64 32)"
```

**Veriqan:CsvReferenceData:RootDirectory** (institution-specific reference bundle —
products, rates, legends, tolerances, client accounts, prior-period closing values):
```bash
export Veriqan__CsvReferenceData__RootDirectory="$HOME/_demo/veriqan-bundle"
```
The adapter looks for a sub-directory whose name matches the institution identifier
(case-insensitive, whitespace-normalized). For the "Iqubica" demo institution the full
path is `$HOME/_demo/veriqan-bundle/Iqubica/`. The directory must contain the CSV files
listed below; all are optional except `bundle-metadata.csv` (missing sections yield
`INSUFFICIENT_REFERENCE_DATA` findings, never crashes):

```
Iqubica/
├── bundle-metadata.csv           # required: schemaVersion, institution, period
├── products.csv                  # card product catalog (CL-1, CL-34, CL-35)
├── interest-rates.csv            # annual ordinary fixed rates by product+period (CL-19/20)
├── tolerance-config.csv          # currency/points tolerance bands
├── validation-constants.csv      # requiredFontFamily (default Aptos), bankingYearDays
├── mandatory-legends.csv         # legends that must appear (CL-46)
├── sequential-images.csv         # ordered images between sections (CL-47)
├── promotions.csv                # promotional inserts with validity windows (CL-49)
├── client-accounts.csv           # cardholder master data (CL-2…CL-8)
├── client-accounts-entries.csv   # per-account entries joined by clientId
├── prior-statements.csv          # prior-month closing balances (CL-17, CL-36, CL-40)
├── prior-statements-installments.csv  # open MSI installments from prior period (CL-40/41)
└── expected-transactions.csv     # ground-truth transaction detail (CL-45/58)
```

Source CSV files for the Iqubica bundle are seeded from
`Prisma/Fixtures/PRP2/Check+list+demo+v2+Iqubica_*.csv`; they need to be renamed to
the adapter's expected file names (above) and placed under the `Iqubica/` sub-directory.
This is W3 work; the interim Dummie VEC PDFs in PRP2 are the synthetic placeholder until
the owner supplies anonymized real statements.

**Veriqan:Smtp** (email notification — stage 8 of the pipeline):
```bash
export Veriqan__Smtp__Host=localhost
export Veriqan__Smtp__Port=1025
export Veriqan__Smtp__EnableSsl=false
export Veriqan__Smtp__From=noreply@veriqan.local
# For a local MailHog stub (no real relay needed during recording):
docker run -d --name mailhog -p 1025:1025 -p 8025:8025 mailhog/mailhog
```
The pipeline does not block if email delivery fails — a WARNING is logged and the run
continues. Set at least one recipient so stage 8 dispatches:
```bash
export Veriqan__Alerts__Recipients__0="reviewer@iqubica.demo"
```

### 0.2 Corpus placement

Place the 4 demo fixtures where the E2E test and the Worker can reach them. Anticipated
fixture path: `Prisma/Fixtures/PRP2/vec-demo/` (set in `VecChecklistDemoE2ETests`
once W4 is authored).

| Fixture | Expected verdict | Key checklist items | Source |
|---|---|---|---|
| `good.pdf` | **GREEN** | all CL-1…CL-55 pass | Anonymized intermediate statement (Aug–Sep 2025 period); bank name + logo changed |
| `bad-math-cl21.pdf` | **RED** (CL-21) | CL-21 arithmetic reconciliation | Movement total does not match reported balance; rendering preserved |
| `bad-font-cl35.pdf` | **RED** (CL-35) | CL-35 font compliance | Aptos font absent or not embedded; font metadata preserved |
| `scanned.pdf` | **BLOCKED** | density guard (§1, commit `60fe635d`) | Image-only PDF; zero text layer; authored synthetically |

The corpus is built from a series of anonymized real statements: prior (Jul–Aug 2025),
**intermediate** (Aug–Sep 2025 — the analysis subject), and next (Sep–Oct 2025). The
prior period feeds `prior-statements.csv` in the reference bundle so CL-17/CL-36/CL-40
have valid ground truth. Interim synthetic PDFs are at
`Prisma/Fixtures/PRP2/01+Dummie+VEC+jul_ago+20252.pdf`,
`02+Dummie+VEC+ago_sep+2025.pdf`, and `03+Dummie+VEC+sep_oct+2025.pdf` until the
owner-supplied anonymized set lands (W3).

> **Anonymization fidelity rule:** for `bad-math-cl21.pdf` and `bad-font-cl35.pdf`, mask PAN,
> account numbers, and cardholder names — but **preserve layout, fonts, and geometry**.
> CL-31 pagination, CL-34 card-in-image, and CL-35 font embed all fire on rendering;
> destroying geometry breaks the very gaps the demo must prove fixed.

### 0.3 Run the deterministic E2E proof (W4 — gate before any capture)

> **Status as of 2026-06-27:** `VecChecklistDemoE2ETests` is not yet authored (W4 is
> pending). The command below is the anticipated invocation once W4 is complete. Before
> the first recording session, verify this command passes clean. If any test is RED, do
> not proceed to the camera.

```bash
# From repo root. Requires: Worker built, DB running, reference bundle present.
dotnet test \
  "Prisma/Code/Src/CSharp/08 Tests/06 E2E/Tests.Veriqan.E2E/ExxerCube.Prisma.Tests.Veriqan.E2E.csproj" \
  --filter-query "/*/*/VecChecklistDemoE2ETests"
```

Expected result:
```
[PASS] VecChecklist_GoodPdf_ReturnsGreenVerdict
[PASS] VecChecklist_BadMathPdf_ReturnsRedVerdict_Cl21
[PASS] VecChecklist_BadFontPdf_ReturnsRedVerdict_Cl35
[PASS] VecChecklist_ScannedPdf_ReturnsBlockedVerdict
```

Artifacts written to `exports/` (consumed by the demo UI and Capture 3):
```
exports/
├── good.verdict.json
├── bad-math-cl21.verdict.json        # findings: [{ rule: "CL-21", locator: ... }]
├── bad-math-cl21.marked.pdf          # red annotations at the failing location
├── bad-font-cl35.verdict.json        # findings: [{ rule: "CL-35", locator: ... }]
├── bad-font-cl35.marked.pdf          # red annotations at the font-non-compliant region
└── scanned.verdict.json         # verdict: BLOCKED, reason: InsufficientData
```

**GO/NO-GO — all captures:** all 4 tests in `VecChecklistDemoE2ETests` must be PASS before
any recording session begins. Re-run after each reset between takes.

### 0.4 Launch the demo UI (W-UI — required for captures 1–5)

> **Status as of 2026-06-27:** the Veriqan visual demo UI (W-UI) is greenfield — no
> Razor/Blazor surface exists for Veriqan. The command below is the anticipated launch
> once W-UI is authored. The UI reads from the `exports/` directory produced by W4.

```bash
# Anticipated project path (set when W-UI is scaffolded):
cd "Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Veriqan.Demo.UI"
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="http://localhost:7090"
export Veriqan__Demo__ExportsDirectory="$PWD/../../../../../exports"
dotnet run
# → http://localhost:7090
```

Port note: keep the Veriqan demo UI on port 7090 to avoid collision with any Prisma
Web.UI instance (defaults to 7080 / 7443).

### 0.5 Reset between takes

```bash
# 1. Drop and recreate the demo database (EF migrates on next Worker start):
docker exec veriqan-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P Demo_2026! -No \
  -Q "DROP DATABASE IF EXISTS VeriqanDemo; CREATE DATABASE VeriqanDemo;"

# 2. Clear generated artifacts:
rm -f exports/*.json exports/*.pdf exports/*.xlsx

# 3. Re-run VecChecklistDemoE2ETests to regenerate artifacts (step 0.3).
#    The demo UI reads from exports/ — artifacts must be fresh before filming.
```

---

## Capture 0 — Panorama: el problema regulatorio (~2 min)

**Story beat:** Context setting. Banks process 13–20 million credit-card statements per
month against a 55-item regulatory checklist; the gate is manual today — slow,
inconsistent, and exposed to deadline risk. Veriqan automates it, with graceful
degradation: it never silently rejects what it cannot read.

**Checklist tie:** CL-1…CL-55 overview — frame the scope of the whole checklist and the
9-stage pipeline (Ingestion → Extraction → Binding → Validation → Verdict → Persist →
Report → Notify → Disposition), not any individual item.

**On-screen:**
1. Open `Prisma/Fixtures/PRP2/Check+list+demo+v2+Iqubica.xlsx` in Excel. Scroll
   through the 55 CL rows to make the scope of the checklist visible to the audience.
   (Alternatively: open the checklist overview page on the demo UI if W-UI is built.)
2. Switch to the demo UI home / pipeline diagram page showing the 9 stages end-to-end.

**Narración (ES):**
> "Los bancos verifican entre 13 y 20 millones de estados de cuenta al mes contra una
> lista de 55 criterios regulatorios — desde la ortografía del encabezado hasta la
> aritmética de los movimientos y la tipografía legal del documento. Hoy ese proceso es
> manual: lento, inconsistente y con plazos regulatorios encima. Veriqan VEC automatiza
> esa verificación: recibe el estado de cuenta, lo analiza contra los 55 criterios y
> emite un dictamen en segundos. Si el documento no contiene suficiente información para
> verificarse, el sistema abstiene y lo manda a revisión humana — nunca rechaza en
> silencio un documento que no puede leer."

**GO/NO-GO gate:** `Prisma/Fixtures/PRP2/Check+list+demo+v2+Iqubica.xlsx` opens and
shows 55 rows (CL-1…CL-55). No pipeline or W-UI dependency. This capture is filmable
**regardless of W4 or W-UI status** — record it first.

---

## Capture 1 — Ingesta + Extracción: campos del estado de cuenta (~3 min)

**Story beat:** Submit the intermediate statement (`good.pdf`, Aug–Sep period). The
pipeline hashes the document for integrity, ingests it, and extracts all header fields,
billing period, balances, and movement detail — the raw material the 55 checks will run
against.

**Checklist tie:** CL-1 (document present and hashed), CL-2 (billing period), CL-3
(cut-off date), CL-4 (payment due date), CL-5…CL-9 (header fields: bank name, account
holder, card product, card number, credit limit).

**On-screen (W-UI upload + extracted-fields panel):**
- Drag `good.pdf` onto the demo UI upload panel. Submit via `POST /verify`.
- After ingestion, the extracted-fields panel displays: document hash, billing period
  (Aug–Sep 2025), cut-off date, payment due date, account holder, last-4 digits of card,
  credit limit, prior balance, new purchases total, payments, interest charged, balance
  due, minimum payment due.
- Name each field aloud, tying it to its CL item.

**Narración (ES):**
> "Cargamos el estado de cuenta del período agosto–septiembre. El sistema genera un
> **hash del documento** para garantizar su integridad y registra el ingreso en la
> bitácora de auditoría. De inmediato extrae los campos del encabezado: el período de
> facturación, la fecha de corte, la fecha límite de pago, el nombre del titular, los
> últimos cuatro dígitos de la tarjeta, el límite de crédito y todos los saldos. Estos
> son los datos que los 55 criterios van a verificar."

> **Fallback if live submission is fragile on the day:** open `exports/good.verdict.json`
> in the UI's verdict detail view — it contains the same extracted fields. Do not
> improvise field values on camera.

**GO/NO-GO gate:** W-UI upload panel is functional; the extracted-fields panel renders
billing period, balance, and document hash for `good.pdf` with no 500-class error.
Requires W-UI complete and reference bundle present (Binding stage must not fail on a
missing `bundle-metadata.csv`).

---

## Capture 2 — Verificación GREEN: el estado de cuenta conforme (~3 min)

**Story beat:** A fully compliant statement clears all 55 checks and receives a GREEN
verdict. Show the full 55-check grid in its passing state and the GREEN verdict banner.
This establishes the "happy path" baseline the audience will contrast against Capture 3.

**Checklist tie:** All CL-1…CL-55 pass (every check green or safely abstained with no
RED finding).

**On-screen (W-UI 55-check grid + verdict banner):**
- Demo UI: 55-check grid for `good.pdf` — all rows green.
- GREEN verdict banner at top: "Conforme — 0 hallazgos."
- Summary line: verdict GREEN, timestamp, document hash, processing duration.

**Narración (ES):**
> "Este estado de cuenta supera los 55 criterios: encabezado completo y correcto,
> aritmética de movimientos cuadrada, fuentes legales presentes, número de tarjeta en
> portada y paginación consistente. El sistema emite el dictamen **VERDE** — el
> documento está conforme. Ningún criterio genera hallazgo; el resultado queda
> persistido con su hash, su sello de tiempo y la traza de auditoría completa."

**GO/NO-GO gate:** `VecChecklist_GoodPdf_ReturnsGreenVerdict` PASS; W-UI 55-check grid
renders all items green for `good.pdf` with zero RED rows. This gate also confirms the
reference bundle is loaded (Binding stage must succeed for `good.pdf` to reach GREEN).
If any CL item is RED on `good.pdf`, stop — a cardinal false-RED has regressed.

---

## Capture 3 — Hallazgos RED: CL-21 y CL-35 con PDF marcado (~4 min)

**Story beat:** Two defective statements expose two distinct failure modes. (a) CL-21:
the movement total does not reconcile with the reported balance — arithmetic mismatch.
(b) CL-35: the required legal font (Aptos) is absent or not embedded. In both cases the
system emits RED, lists the exact finding, and produces a **marked PDF** with red
annotations pinned to the offending location on the page. This is the most visually
compelling segment — the marked PDF is the demo centrepiece.

**Checklist tie:** CL-21 (arithmetic reconciliation of movements vs. balance); CL-35
(Aptos font embedded and used on statement body). Wave-1 fixes `8ba45202`+`99382e4a`
(CL-35 prefix match + `IsEmbedded`) and `d0ab2485` (CL-21 sign-convention guard) are
closed.

**On-screen (W-UI findings panel + marked PDF viewer):**

*Part A — `bad-math-cl21.pdf`:*
- 55-check grid: CL-21 row is RED; all other rows green or abstained.
- RED verdict banner.
- Finding card: "CL-21 — Aritmética de movimientos: saldo reportado vs. suma de
  movimientos discrepan en $X.XX."
- Side panel or tab: `bad-math-cl21.marked.pdf` — red highlight over the balance-due cell
  and the movement-total column on the offending page.

*Part B — `bad-font-cl35.pdf`:*
- Switch fixture. 55-check grid: CL-35 row RED.
- Finding: "CL-35 — Fuente Aptos no encontrada o no incrustada en el documento."
- `bad-font-cl35.marked.pdf` — red highlight over the font-non-compliant body-text region.

**Narración (ES):**
> "Ahora dos estados de cuenta con defectos. El primer criterio, **CL-21**, detecta que
> la suma de los movimientos no cuadra con el saldo reportado — el sistema señala
> exactamente en qué celda del documento está la discrepancia. En el segundo caso,
> el criterio **CL-35** detecta que la fuente **Aptos** — requerida por el estándar
> regulatorio — no está incrustada en el documento. En ambos casos el dictamen es
> **ROJO**, con el hallazgo descrito con precisión y el PDF marcado listo para el
> analista. El sistema no rechaza genéricamente; especifica qué falló y dónde."

> **Pre-flight CL-35 check:** if `bad-font-cl35.pdf` returns GREEN or BLOCKED instead of RED
> on CL-35, the font rule has regressed — stop and diagnose before filming. The GO/NO-GO
> gate below covers this explicitly.

**GO/NO-GO gate:**
- `VecChecklist_BadMathPdf_ReturnsRedVerdict_Cl21` PASS and `exports/bad-math-cl21.marked.pdf`
  exists with a visible red annotation on the correct page.
- `VecChecklist_BadFontPdf_ReturnsRedVerdict_Cl35` PASS and `exports/bad-font-cl35.marked.pdf`
  exists with a visible annotation. If either marked PDF has no visible annotation or the
  annotation is on the wrong page, do not film — diagnose the marked-PDF rotation/Y-flip
  fix (commits `f9d5e8e1`/`1adfcbc9`) first.

---

## Capture 4 — Degradación segura: estado de cuenta BLOQUEADO (~2 min)

**Story beat:** An image-only scanned statement contains no analyzable text layer. The
pipeline's text-density guard fires before any of the 55 checks run. The system returns
BLOCKED with disposition `InsufficientData` and routes to a human reviewer — it never
emits a false RED on a document it cannot read. This is the abstain-safety guarantee.

**Checklist tie:** Abstain discipline — the density guard (gap #1, commit `60fe635d`)
prevents any CL check from running on an unreadable document. No individual CL item
fires; all 55 are gray / abstained.

**On-screen (W-UI BLOCKED banner + routing action):**
- Demo UI: BLOCKED verdict banner for `scanned.pdf`:
  "BLOQUEADO — Datos insuficientes para verificación automática."
- Reason text: "El documento no contiene capa de texto analizable — se requiere revisión
  humana."
- 55-check grid: all 55 items in abstain state (gray — no RED, no GREEN).
- Routing button or disposition: `RequiresHumanReview`.

**Narración (ES):**
> "¿Qué sucede si el estado de cuenta llega como imagen escaneada, sin capa de texto?
> El sistema detecta que no tiene suficiente información para verificar ninguno de los
> 55 criterios. En lugar de emitir un dictamen incorrecto, lo marca como **BLOQUEADO**
> — datos insuficientes — y lo envía a un analista humano para su revisión. El sistema
> nunca rechaza automáticamente un documento que no puede leer."

**GO/NO-GO gate:** `VecChecklist_ScannedPdf_ReturnsBlockedVerdict` PASS; W-UI shows
the BLOCKED banner; the 55-check grid has **zero RED rows** for `scanned.pdf`. Any RED
row on a scanned fixture = regression in the density guard — stop immediately. The guard
is closed (commit `60fe635d`); use this capture as the ongoing regression proof.

---

## Capture 5 — Disposición + Auditoría: trazabilidad completa (~3 min)

**Story beat:** An analyst reviews the RED finding from Capture 3, accepts or escalates
it, and the action is written to an append-only audit row — an immutable record of who
acted, on which verdict, and when. Close the demo with the `VecChecklistDemoE2ETests`
green run in the terminal as system-level proof: not a mock-up, not staged data — the
real pipeline on real documents.

**Checklist tie:** FR-16 (audit trail — every verification event persisted); FR-18
(disposition — analyst workflow closes the finding loop).

**On-screen:**

*Step 1 — Disposition screen (W-UI):*
- Open the `bad-math-cl21.pdf` case from the verdict list. Show the verdict detail: RED,
  CL-21 finding, document hash, timestamp, processing path.
- Analyst clicks "Aceptar hallazgo" (or "Escalar" — either action writes the audit row).
- Audit trail panel updates immediately: `{ verdictId, action, actor, timestamp, finding }`
  — append-only; no edit or delete control is present.

*Step 2 — E2E terminal close:*
- Switch to terminal. Run `VecChecklistDemoE2ETests` live on camera:
  ```bash
  dotnet test \
    "Prisma/Code/Src/CSharp/08 Tests/06 E2E/Tests.Veriqan.E2E/ExxerCube.Prisma.Tests.Veriqan.E2E.csproj" \
    --filter-query "/*/*/VecChecklistDemoE2ETests"
  ```
- Show all 4 tests going green in real time. End on the pass summary.

**Narración (ES):**
> "El analista revisa el hallazgo, lo acepta o lo escala, y el sistema registra esa
> acción en la **bitácora de auditoría**: quién actuó, cuándo y sobre qué dictamen. El
> registro es de solo adición — no se puede modificar ni eliminar. Y para cerrar la
> demostración: aquí están los cuatro casos corriendo de forma automática sobre
> documentos reales. Verde, rojo por aritmética, rojo por tipografía, bloqueado por
> imagen. No es una maqueta — es el sistema."

**GO/NO-GO gate:**
- Disposition action writes an audit row visible in the W-UI audit trail panel (requires
  `ConnectionStrings__VeriqanDb` pointing at a running SQL instance — not in-memory mode).
- All 4 `VecChecklistDemoE2ETests` pass green in the terminal **during the recording**.
  If any test is RED on camera, stop — do not continue.

> **Practical note (gap #15):** in-memory result/reprocess stores are open. If the
> Worker restarts between the E2E pre-run (step 0.3) and this capture, the verdict
> list may be empty. Complete the E2E run, then immediately film Capture 5 without
> restarting the Worker or the demo UI.

---

## Recommended Record Order

Record **deterministic and pipeline-independent** segments first; reserve live DB
interaction and the E2E terminal close for last. Rationale: each clip is independent,
re-recording one clip is cheap, and the most state-sensitive segments (disposition +
live E2E) are filmed once the pipeline proof is already safely in the can. This mirrors
the Prisma demo lesson (§0.5 in `CLIENT-DEMO-CAPTURE-RUNBOOK-2026-06-15.md`): "record
the stable, high-impact segments first."

| Order | Capture | Rationale |
|---|---|---|
| 1st | **0 — Panorama** | Zero pipeline dependency; just the Excel checklist + flow diagram. Film immediately regardless of W4 / W-UI status. |
| 2nd | **4 — BLOCKED** | Single fixture, simplest verdict path. The density guard is closed (commit `60fe635d`). Proves abstain-safety; sets up the safety narrative before showing failures. |
| 3rd | **2 — GREEN** | All-pass path on `good.pdf`. Exercises the reference bundle end-to-end. Film once W3 bundle + W4 + W-UI 55-check grid are complete. |
| 4th | **3 — RED + marked PDF** | Two-fixture sequence; the marked PDFs are the visual centrepiece. Film after GREEN is in the can so the contrast is available in editing. |
| 5th | **1 — Ingesta + Extracción** | Live upload interaction — most dependent on the UI being stable and the reference bundle loaded. Film after all verdict captures are done. |
| 6th | **5 — Disposición + Auditoría + E2E close** | Requires live DB state (disposition writes an audit row) plus the terminal green run. Film last; the E2E run is your closing shot. |

---

## GO/NO-GO Gate Table

Aligned with `VERIQAN-MVP-PATH-2026-06-27.md` §5 and extended to cover all 6 captures.

| Capture | Depends on | Gate — condition that must hold to record |
|---|---|---|
| **0 — Panorama** | None | `Prisma/Fixtures/PRP2/Check+list+demo+v2+Iqubica.xlsx` opens; 55 rows (CL-1…CL-55) visible. No pipeline required. |
| **1 — Ingesta** | W-UI upload panel + W3 bundle | Extracted-fields panel renders billing period, document hash, and balance fields for `good.pdf`; no 500-class error; `bundle-metadata.csv` present so Binding does not abort. |
| **2 — GREEN** | W4 E2E test + W-UI 55-check grid + W3 bundle | `VecChecklist_GoodPdf_ReturnsGreenVerdict` PASS; UI 55-check grid shows zero RED rows for `good.pdf`; GREEN verdict banner renders. |
| **3 — RED + marked PDF** | W4 E2E test + W-UI findings panel + W-UI marked-PDF viewer | `VecChecklist_BadMathPdf_ReturnsRedVerdict_Cl21` PASS + `exports/bad-math-cl21.marked.pdf` has visible annotation; `VecChecklist_BadFontPdf_ReturnsRedVerdict_Cl35` PASS + `exports/bad-font-cl35.marked.pdf` has visible annotation. Both marked PDFs must show annotations on the correct page (not rotated / Y-flipped). |
| **4 — BLOCKED** | W4 E2E test + W-UI BLOCKED banner + density guard `60fe635d` | `VecChecklist_ScannedPdf_ReturnsBlockedVerdict` PASS; UI shows BLOCKED banner; 55-check grid has zero RED items for `scanned.pdf`. |
| **5 — Disposición + Auditoría** | W-UI disposition screen + SQL persistence (`ConnectionStrings__VeriqanDb` set) + all W4 tests | Disposition action writes a visible audit row in the UI; all 4 `VecChecklistDemoE2ETests` pass green in the terminal during the recording; Worker has not been restarted since the pre-run (gap #15). |

---

## Known Limitations / Honesty Notes

1. **Anonymized corpus — not live bank data.** The 4 demo fixtures are derived from real
   credit-card statements supplied by the owner. PAN, account numbers, cardholder names,
   and the bank name + logo have been changed. Layout, fonts, and geometry are preserved
   so structural CL checks (CL-31 pagination, CL-34 card-in-image, CL-35 font) fire on
   real rendering. State this on camera during Capture 0: *"los documentos son
   anonimizados — los datos personales y el nombre del banco han sido sustituidos, pero
   la estructura y la tipografía son reales."*

2. **Synthetic `scanned.pdf`.** The BLOCKED fixture is an authored image-only PDF, not
   a scanned real statement. Narrate it honestly: *"un estado de cuenta escaneado como
   imagen."* Do not claim it is an owner-supplied document.

3. **No CONDUSEF calibration yet.** Numeric tolerances (CL-19/CL-20 rate checks, CL-21
   arithmetic band, `toleranceConfig.currencyToleranceMxn`) are set to reasonable
   defaults in the reference bundle. A real CONDUSEF corpus for threshold calibration is
   business-gated (E13 buyer gate). Do not claim the thresholds are CONDUSEF-certified;
   narrate as: *"criterios configurables, calibrables con datos reales de CONDUSEF."*

4. **Production hardening is open — this demo does not demonstrate those items.** Open
   gaps as of 2026-06-27: no auth/authz on `POST /verify` + `POST /batch` (#17), no
   TLS/HSTS (#20), SMTP `EnableSsl=false` in default config, in-memory result/reprocess
   stores on restart (#15), audit immutability is app-convention only — no DB
   trigger/ledger (#19), plaintext AES key in config with no Key Vault (#21), no
   LFPDPPP retention regime (#22). Do not claim production-readiness in those
   dimensions during the recording.

5. **W4 (`VecChecklistDemoE2ETests`) and W-UI are not yet authored** as of 2026-06-27.
   This runbook pre-describes their anticipated commands and screenshots. Before the
   first recording session, verify W4 passes clean (step 0.3) and W-UI renders the
   correct views (step 0.4) using the GO/NO-GO gates above.

6. **Reference bundle is owner-gated (W3).** The `Veriqan__CsvReferenceData__RootDirectory`
   data for the Iqubica demo institution is on the critical path. Without a valid
   `bundle-metadata.csv` in the `Iqubica/` sub-directory, the Binding stage logs
   `INSUFFICIENT_REFERENCE_DATA` findings and `good.pdf` cannot reach GREEN. Captures
   2–4 are blocked until W3 is complete. The interim synthetic PDFs in PRP2 are a
   partial placeholder; the full anonymized real bundle is the true requirement.
