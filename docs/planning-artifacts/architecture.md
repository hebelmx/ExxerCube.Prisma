---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
inputDocuments:
  - docs/planning-artifacts/prds/prd-veriqan-vec-2026-06-16/prd.md
  - docs/planning-artifacts/prds/prd-veriqan-vec-2026-06-16/addendum.md
  - docs/planning-artifacts/prds/prd-veriqan-vec-2026-06-16/.decision-log.md
  - Prisma/Fixtures/PRP2/REUSE-VS-STANDALONE-RECOMMENDATION.md
  - Prisma/Fixtures/PRP2/reference-data/ (JSON contract)
  - Prisma/Fixtures/PRP2/architecture.md (UNTRUSTED prior draft — reference only)
workflowType: 'architecture'
authoringMode: 'fast-one-pass'
project_name: 'veriqan-vec'
user_name: 'hebelmx'
date: '2026-06-16'
---

# Architecture Decision Document — Veriqan VEC

_Authored in fast one-pass mode from the finalized PRD and reuse analysis, for review. Companion to
`prd.md` (capabilities/FRs) — this document is the technical "how"._

## 1. Context & Constraints

Veriqan VEC is an **additive module of the existing ExxerCube.Prisma platform** (which today hosts
Solution 1, the oficio / "Atención a Autoridades" product). It verifies bank credit-card statements
against the 55-item checklist (source of truth: `Check+list+demo+v2+Iqubica.xlsx`).

Binding constraints (from the PRD / decision log):
- **Brownfield, additive, one-directional.** Dependency `Veriqan → Prisma` only. No breaking change
  to Solution 1 (FR-23). Shared-abstraction changes are additive and gated by Solution 1's
  regression suite (SM-5).
- **Reuse the Shared Core** (extraction, imaging, export, storage, events, EF Core, CSnakes Python
  interop, `Result<T>`) — do not duplicate (FR-24).
- **Deterministic-first.** v1 = arithmetic + embedded-font/geometry + lightweight-CV image presence.
  ML (CLIP/LayoutLMv3) deferred to v2 behind a stable boundary (FR-9…FR-12).
- **Human-in-the-loop.** VEC assists; it never auto-rejects in v1 (FR-18).
- **Untrusted prototype.** The existing PRP2 scaffolding is salvaged selectively, rebuilt otherwise
  (decision #5).

## 2. Architecture Style & Module Placement

The platform is **hexagonal / clean architecture** organized in numbered layers under
`Prisma/Code/Src/CSharp/`: `01 Core` (Domain, Application) → `02 Infrastructure` → `03 Orchestration`
→ `04 Services` → `07 UI`, with `08 Tests` and `09 Testing`. Veriqan mirrors this layering inside a
`Veriqan` sub-namespace so it composes with, but is isolated from, oficio code.

**ADR-V1 — Veriqan is a set of new projects in the existing solution, namespaced `ExxerCube.Prisma.Veriqan.*`.**
Rationale: maximizes Shared-Core reuse, keeps one build/test/deploy toolchain, and the dependency
rule is mechanically enforceable. Alternative (separate repo/solution) rejected: duplicates infra,
splits the toolchain, and the products share a real core competency. Independently *deployable*
(separate worker/host) ≠ separate codebase.

New projects (additive):
```
01 Core/
  Veriqan.Domain            // BankStatement, Transaction, Finding, Verdict, value objects, enums
  Veriqan.Application       // VerificationOrchestrator, rule engine contracts, ports
02 Infrastructure/
  Veriqan.Infrastructure.Extraction      // statement field extraction (reuses IFieldExtractor)
  Veriqan.Infrastructure.Validation      // IVecValidationRule implementations (the engine)
  Veriqan.Infrastructure.Visual          // font/overlap/pagination/image-presence (det + light-CV)
  Veriqan.Infrastructure.ReferenceData   // IVecReferenceDataProvider + CSV/DB/API adapters
  Veriqan.Infrastructure.Reporting       // Marked PDF, email alerts
  Veriqan.Infrastructure.Persistence     // additive EF Core entities + DbContext extension
  (v2) Veriqan.Infrastructure.Python.Vec // CSnakes ML boundary (LayoutLMv3/CLIP) — salvage-based
03 Orchestration/
  Veriqan.Orchestration     // DI composition root for Veriqan services
04 Services/
  Veriqan.Worker            // batch worker host (independently deployable)
07 UI/
  Veriqan.QaConsole         // human-in-the-loop disposition UI (or module in existing Web.UI)
08 Tests/  09 Architecture/  // mirror tests incl. the dependency-direction NetArchTest rule
```

## 3. Shared-Core Reuse Map (what Veriqan consumes vs. builds)

| Concern | Shared Core (reuse) | Veriqan builds |
|---|---|---|
| Result/error handling | `Result<T>` (IndQuestResults) | — |
| Field extraction | `IFieldExtractor<T>`, `Infrastructure.Extraction.Txt/.Adaptive`, `TextSanitizer` | VEC field maps & normalizers |
| Image quality | `EmguCvImageQualityAnalyzer` | font/overlap/pagination/pHash checks |
| Export/PDF | `Infrastructure.Export` (PdfSharp, signer), `.Adaptive` | Marked-PDF annotator |
| Persistence | `Infrastructure.Database` EF Core | additive VEC tables/DbContext |
| Eventing | `InMemoryEventBus` | VEC domain events |
| Python interop (v2) | CSnakes pattern from `Infrastructure.Python.GotOcr2` | VEC ML wrapper |
| Reconciliation pattern | `FusionExpedienteService` → generalize | `IDataFuser<T>` consumer |
| Pipeline pattern | `Athena.ExtractionOrchestrator` | `VerificationOrchestrator` |
| Worker health | `Sentinel` | reuse directly |

## 4. Component Architecture (the verification pipeline)

```
[Intake] → [Context Binding] → [Extraction] → [Validation Engine] ─┬→ [Verdict] → [Reporting] → [QA Console]
   FR-1        FR-2/3            FR-4/5         FR-6/7/8/25         │   FR-15      FR-16/17      FR-18
                  │                              [Visual Inspection]┤
            [Reference Bundle]                   FR-9..12          │
            via IVecReferenceDataProvider        [Regulatory/Fiscal]┘
                                                  FR-13/14/26
```

- **Intake / Job** (`Veriqan.Application`): accepts a Statement PDF, creates an idempotent (by content
  hash) `VerificationJob`. Realizes FR-1.
- **Context Binding**: resolves the `ReferenceBundle` (FR-2) and product (FR-3) via
  `IVecReferenceDataProvider`. Missing sections flagged → downstream `INSUFFICIENT_DATA`.
- **Extraction** (`Veriqan.Infrastructure.Extraction`): PDF text-layer first (PdfPig/pdfplumber),
  reusing `IFieldExtractor` + `TextSanitizer`; OCR fallback only for image-only PDFs (out of v1
  primary scope). Produces a typed `StatementModel` with per-field confidence + locators.
- **Validation Engine** (§5): the deterministic core.
- **Visual Inspection** (`Veriqan.Infrastructure.Visual`): font (embedded-name), overlap (geometry),
  pagination/blank-page, image presence (pHash). Each Check records its **technique class**.
- **Reporting** (`Veriqan.Infrastructure.Reporting`): aggregates Findings → `Verdict`, renders the
  Marked PDF, sends email alerts.
- **QA Console** (`07 UI`): human dispositions Findings with immutable audit (FR-18).

## 5. Validation Rule Engine

**ADR-V2 — Each checklist item is an independent `IVecValidationRule` aggregated by a `VecValidationEngine`.**
```csharp
public interface IVecValidationRule
{
    string CheckId { get; }                 // e.g. "CL-21"
    TechniqueClass Technique { get; }        // Deterministic | LightweightCv | Ml
    Result<Finding> Evaluate(VerificationContext ctx, CancellationToken ct);
}
```
- `VerificationContext` carries the `StatementModel`, the `ReferenceBundle`, the `PriorStatement`,
  and `toleranceConfig`.
- A rule returns `Finding { CheckId, Verdict (Pass|Fail|InsufficientData), Expected, Observed,
  ToleranceApplied, Severity, Locator, Technique }`.
- Rules are **pure and deterministic** (NFR-5): same inputs → same Finding. No rule throws for
  control flow (`Result<T>`, NFR-6).
- Discovery via DI: registering a new rule adds a Check with zero engine changes — supports growth
  to v2 and beyond. Rule→CL mapping is 1:1 or 1:many per the PRD §17 coverage map.

**ADR-V3 — Tolerance and thresholds are data, not code.** All numeric tolerances and visual
thresholds come from `ReferenceBundle.toleranceConfig` / config (FR-8, §4.4 note). Findings record
the applied value. No magic numbers.

## 6. Reference-Data Architecture

**ADR-V4 — One canonical JSON contract + pluggable adapters.** `IVecReferenceDataProvider` returns a
schema-valid `ReferenceBundle` (contract at `Prisma/Fixtures/PRP2/reference-data/`). Adapters:
`CsvReferenceDataAdapter`, `DatabaseReferenceDataAdapter`, `ApiReferenceDataAdapter` — all emit the
same bundle. New delivery mechanism = new adapter, no core/engine change (FR-19, realizes UJ-3).
**Graceful degradation** (FR-20): absent section → dependent Checks emit `INSUFFICIENT_DATA`, never
`FAIL`. Bundles are validated against the JSON schema at the boundary; invalid bundle ⇒ `BLOCKED`.

## 7. Data Model (additive persistence)

**ADR-V5 — Separate `VeriqanDbContext` in its own `veriqan` schema; never touch `PrismaDbContext`.**
The existing `PrismaDbContext` is a single concrete (non-partial) context with hardcoded `DbSet`s, so
"extend it additively" is not achievable without editing Solution 1. Instead Veriqan owns a
**separate `VeriqanDbContext`** with its **own EF migrations** and a dedicated **`veriqan` SQL
schema** — truly additive, zero Solution 1 schema change (FR-23, §13). Migration ownership is
disjoint; the two contexts may share a database but not tables.
Core aggregates:
- `VerificationJob` (1) → `Finding` (N)
- `StatementSnapshot` (extracted header fields + locators) per job
- `StatementMovement` (N) — extracted DESGLOSE rows, for FR-25 reconciliation
- `ExpectedTransactionSnapshot` (N) — the reference `expectedTransactions` as evaluated (FR-25)
- `PriorStatementLink` — reference to the prior Period's closing values used (FR-7)
- `Verdict` (rollup) per job
- `Disposition` (N) — **append-only** audit rows (actor, timestamp, before/after) in a dedicated
  immutable table (NOT the existing mutable `AuditRecord`) (FR-18, §14)
- `ReferenceBundleVersion` — versioned by Period for reproducibility
- `EngineVersion` stamped on every Finding (provenance §14)
Retention per policy (~7 yr, §13). Card numbers masked at rest unless a Check provably needs full
PAN (§11 PCI note → open decision).

## 8. Brownfield Integration & Phase-0

**ADR-V6 (revised) — VEC v1 does NOT genericize `FusionExpedienteService`; Phase-0 is reduced to an
isolation gate.** The adversarial review confirmed `FusionExpedienteService` is ~2,811 lines of
oficio-typed methods (only `FuseFieldAsync` ~100 lines is generic) AND — more importantly — **VEC v1
is single-source verification (one statement PDF), not multi-document fusion.** It therefore does not
need `IDataFuser<T>` at all. So the biggest brownfield risk is **removed from the v1 path**, not
mitigated.

Phase-0 for v1 is now just the **non-breaking isolation setup**:
1. Stand up the `Veriqan.*` projects and `VeriqanDbContext` (ADR-V5).
2. Author a **NetArchTest** rule (the existing stack is **NetArchTest**, not ArchUnitNET) asserting
   `Veriqan → Prisma` one-way; it fails the build on any `Prisma → Veriqan` reference (FR-23). This
   test **does not exist today and is created here**; the existing cross-infra-isolation guardrails
   must be curated to allow Veriqan's multi-infra composition.
3. Solution 1's full regression suite passes unchanged (SM-5) — no Solution 1 code is modified in v1.

**Deferred (not v1):** generalizing `FusionExpedienteService`/the orchestrator into shared seams is
done **only if a real shared need emerges later**, as its own additive, gated change. FR-27 is
re-scoped accordingly (see decision log / PRD update note).

## 9. Compute Boundary (v1 = pure C#, no Python; v2 = Python ML)

**ADR-V7 — v1 is C#-only, in-process, no Python and no GPU.** This corrects the prototype, which did
fonts/geometry in Python (pdfplumber/Shapely). In v1 we use the **C# stack that already exists or is
a small, named add**:
- Font family per text run → **PdfPig** (already a dependency) exposes embedded font names from the
  PDF — no Python.
- Text-overlap & layout geometry → **PdfPig** glyph/word bounding boxes + C# geometry — no Shapely.
- Page render for visual checks → **PDFtoImage / EmguCV** (already in stack).
- Image presence (pHash) → a C# perceptual-hash nuget (**net-new**, see §9a).
- QR / fiscal block → **ZXing.NET** (**net-new**, see §9a).
- All arithmetic / text / legend checks → pure C#.

So v1 has **no Python on the hot path** and ships without the ML environment.

**ADR-V8 — v2 ML runs behind a stable port as a separately-deployable (GPU) service.** The ports
`IStatementMlExtractor` / `IVisualComplianceMl` are defined in v1 so v1 ships without them and v2
slots in. The heavy Python env (CUDA/torch/transformers, LayoutLMv3/CLIP) is a **v2-only** artifact —
it is NOT salvaged into v1. From the prototype, salvage *as v2 starting points only*:
`image_quality.py`, `font_detector.py` (logic reference), `model_cache.py`, the CSnakes wrapper;
rebuild the missing `models/` Pydantic package and all C# domain/mapping/tests. The prototype's
`requirements.txt` (pinned CUDA/torch) belongs to v2, not v1.

### 9a. Net-new dependencies (outside the ~50–60% reuse headline)

These are honestly NOT reuse — they are new to the codebase and must be added to
`Directory.Packages.props`:
- **ZXing.NET** — QR/barcode decode (FR-14).
- A **perceptual-hash** nuget (e.g. Shipwreck.Phash or CoenM.ImageHash) — image presence (FR-12).
- (v2) the Python ML stack — deferred.
Everything else (PdfPig, EmguCV, PDFtoImage, PdfSharp, ClosedXML, EF Core) is already present.

## 10. Cross-Cutting

- **Orchestration/DI**: `Veriqan.Orchestration` composition root mirrors `PrismaServiceCollectionExtensions`;
  registers rules, adapters, pipeline. Prevents inter-infrastructure coupling.
- **Batch/throughput** (FR-21/22, NFR-1): `Veriqan.Worker` pulls from a queue with bounded
  concurrency + backpressure; failures → exception queue (no batch halt, NFR-3); resumable, idempotent
  by content hash. Design point ≈1 statement/s/worker, scale horizontally. **Throughput caveat:** the
  ≥1/s target must be validated against the real per-statement cost — PDF parse + page render +
  50+ rules. The dominant cost is page rendering for visual checks (PDFtoImage), not the arithmetic;
  cold-start is negligible in v1 (no model load). Budget = parse + render(once, cached per page) +
  rules; measure before committing the number (ties to PRD OQ-1). Queue transport is an open decision
  (§14): for horizontally-scaled workers, `InMemoryEventBus` is intra-process only — cross-worker
  dispatch needs a real transport (e.g. the existing Ember/SignalR hub or a durable queue).
- **Observability** (NFR-4): reuse Shared-Core Serilog + metrics; correlation id per job; emit
  throughput/latency/verdict-distribution/exception counters.
- **Security/PII** (§11/§13): masked card numbers by default; access-controlled QA console; data
  isolated from Solution 1.

## 11. Deployment Topology

- v1: `Veriqan.Worker` (batch) + `Veriqan.QaConsole` (UI) + SQL (additive schema), all on existing
  infra; no GPU.
- v2: add a GPU-backed ML inference service behind ADR-V8 ports.
- Independently deployable from Solution 1 (separate hosts), shared codebase/build.

## 12. Key Risks (architecture-level)

- **Phase-0 refactor touches Solution 1** → mitigated by ADR-V6 gates.
- **Reference data undefined** → mitigated by ADR-V4 graceful degradation; value scales with data.
- **Scan-only PDFs** → v1 scopes to text-layer; OCR is a sequenced add (OQ-4).
- **pHash false positives** (image presence) → tracked against SM-C1; full match is v2.

## 13. Component → FR / ADR Index

| Component | FRs | ADRs |
|---|---|---|
| Application/Orchestrator | FR-1, FR-15, FR-21, FR-22 | ADR-V1 |
| Extraction | FR-4, FR-5 | ADR-V1, ADR-V8 |
| Validation Engine | FR-6, FR-7, FR-8, FR-25 | ADR-V2, ADR-V3 |
| Visual Inspection | FR-9, FR-10, FR-11, FR-12 | ADR-V2, ADR-V7 |
| Regulatory/Fiscal | FR-13, FR-14, FR-26 | ADR-V2, ADR-V7 |
| Reference Data | FR-2, FR-3, FR-19, FR-20 | ADR-V4 |
| Reporting | FR-16, FR-17 | ADR-V1 |
| QA Console | FR-18 | ADR-V1 |
| Persistence | (all, provenance) | ADR-V5 |
| Brownfield/Phase-0 | FR-23, FR-24, FR-27 | ADR-V6 |
| ML boundary (v2) | FR-9..12 (full) | ADR-V8 |

## 14. Open Architecture Decisions

1. QR/barcode decode library (ZXing.NET vs alternative).
2. Queue/transport for batch (in-proc vs Service Bus vs the existing SignalR/Ember hub).
3. QA Console: extend existing `Web.UI` vs separate `Veriqan.QaConsole` app.
4. PAN-at-rest policy (PCI scope) — masked vs tokenized vs full.
5. Reference-data first delivery mechanism (drives which adapter ships first) — tied to PRD OQ-2.
