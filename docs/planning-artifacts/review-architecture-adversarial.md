---
title: Adversarial Architecture Review — Veriqan VEC
reviewer: architecture (cynical/adversarial pass)
date: 2026-06-16
target: docs/planning-artifacts/architecture.md
prd: docs/planning-artifacts/prds/prd-veriqan-vec-2026-06-16/prd.md
ground-truth: Prisma/Fixtures/PRP2/REUSE-VS-STANDALONE-RECOMMENDATION.md + Prisma/Code/Src/CSharp/
---

# Adversarial Architecture Review — Veriqan VEC

**Verdict:** The architecture is structurally sound and the module-not-standalone call is correct, but it
ships at least one self-contradiction (ADR-V7 "no GPU/no ML" rests on a salvaged Python env that pins
the full CUDA/torch/transformers stack), two unbudgeted net-new C# capabilities (QR decode + perceptual
hash have **no** library in the solution today), an under-specified persistence story that cannot be
"additive only" against the single concrete `PrismaDbContext` without touching Solution 1, and a Phase-0
refactor (ADR-V6) whose blast radius the document materially understates.

Findings are ranked Critical → High → Medium → Low. Every finding cites a verified location.

---

## CRITICAL

### C-1. ADR-V7 "v1 needs no GPU and no learned models" is contradicted by the only Python env it ships on
- **Where:** architecture.md §9 ADR-V7 / ADR-V8; NFR in PRD §10/§12 (`[ASSUMPTION: deterministic v1 needs no GPU]`); ground truth `Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.Python.VecExtraction/python/requirements.txt`.
- **Problem:** ADR-V7 says font (pdfplumber) and overlap (Shapely) are pure-Python utilities run "via the existing CSnakes pattern." pdfplumber and Shapely **are** Python — so v1 is **not** C#-only and **does** require the Python runtime to be bootstrapped, started, and kept warm per worker. Worse, the requirements.txt that the doc proposes to "salvage" (ADR-V8: "pinned `requirements.txt`") pins `--index-url .../whl/cu130`, `torch==2.5.1`, `torchvision`, `transformers==4.46.3`, `accelerate`, `safetensors`, `pdf2image` (needs Poppler native), `pymupdf`. That is a multi-GB CUDA deep-learning environment. If v1 installs *that* file, the "no GPU, no ML, ships in weeks" claim is false on first `pip install`. If v1 installs a *trimmed* file (pdfplumber + shapely + pillow only), then ADR-V8's "salvage the pinned requirements.txt" is wrong and a new pinned env must be authored — unbudgeted.
- **Bite:** Cold-start, image size, and ops footprint are sized as if v1 is lightweight C#; reality is a Python interop boundary (CSnakes) on the hot path of *every* statement for font + geometry. This invalidates the throughput model (see C-4) and the "no GPU" deployment topology (§11).
- **Fix:** Author a dedicated `requirements-v1.txt` (pdfplumber, shapely, pillow, pypdfium2 only — no torch/transformers/cuda); state explicitly that v1 **does** require the Python/CSnakes runtime; re-tag the PRD assumption from "no GPU" to "no GPU **but Python-on-hot-path**."

### C-2. QR decode and perceptual-hash (pHash) have NO implementing library anywhere in the solution
- **Where:** architecture.md §9 ADR-V7 (QR `[DECISION PENDING: ZXing.NET vs alternative]`; "image presence from perceptual hashing"); FR-12 (pHash presence), FR-14 (QR decode, CL-50). Ground truth: `Directory.Packages.props` contains `PdfPig`, `Emgu.CV*` — **no** `ZXing.Net`, **no** `Magick`/`CoreCompat`, **no** `ImageHash`/`Shipwreck.Phash`.
- **Problem:** Two v1 functional requirements (FR-14 QR, FR-12 image presence) depend on capabilities that are **not present** in the codebase and are listed only as an *open decision* (§14 item 1) or hand-waved to "a decode library." pHash isn't in the Python pins either (no `imagehash`); only OpenCV is. So both are net-new dependencies plus net-new C# (or Python) code, not "reuse."
- **Bite:** The reuse map (§3) and "≈50–60% reuse" headline quietly exclude these. FR-12 and FR-14 are in-scope MVP (PRD §6.1) but have no real implementation path costed. QR on Mexican CFDI fiscal blocks (regulated, FR-14/CL-50) is accuracy-sensitive and is being treated as a footnote.
- **Fix:** Promote QR-lib and pHash-lib from "open decision" to committed v1 dependencies with owners and a spike; add them to the reuse map's "Veriqan builds" column; cost the EmguCV-vs-pHash-lib choice for presence detection.

### C-3. ADR-V5 "additive tables only; no change to Solution 1 schema" is not achievable as written against the real DbContext
- **Where:** architecture.md §7 ADR-V5 ("additive EF Core entities + DbContext extension"), §3 reuse map ("additive VEC tables/DbContext"), FR-23. Ground truth: `Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.Database/EntityFramework/PrismaDbContext.cs` is a single **concrete** `public class PrismaDbContext : DbContext` with hardcoded `DbSet<>`s (FileMetadata, Persona, SLAStatus, ReviewCases, ReviewDecisions, AuditRecords, …). It is not partial, not generic, not a base class.
- **Problem:** "Extend the DbContext additively" has only two real implementations, neither described:
  (a) Add Veriqan `DbSet`s to `PrismaDbContext` itself — that **edits a Solution 1 file and its migration history**, directly contradicting FR-23 ("introduces no breaking change to Solution 1 code") and SM-5.
  (b) Introduce a **separate** `VeriqanDbContext` over the same physical database — viable and additive, but then ADR-V5's "extending `Infrastructure.Database` DbContext additively" is the wrong description, and you inherit migration-ownership and shared-connection/transaction concerns that are unaddressed (who owns `__EFMigrationsHistory`? two contexts, one DB).
- **Bite:** The single line "additive EF Core entities + DbContext extension" hides the single most likely place Veriqan silently breaks Solution 1 (a shared migration). It will surface late, in a release gate.
- **Fix:** Commit to a **separate `VeriqanDbContext` + separate migrations table** (or schema) over the shared DB; document migration ownership and that no Veriqan type is ever added to `PrismaDbContext`. Add an arch/test gate that `PrismaDbContext` has zero Veriqan references.

### C-4. Throughput target (≥1 statement/s/worker) is asserted, not modeled — and the cost drivers point the other way
- **Where:** architecture.md §10 ("Design point ≈1 statement/s/worker"); PRD NFR-1 (≤10 s p95 latency, ≥1/s sustained). No cold-start, Python-startup, PDF-render, or rule-fan-out budget anywhere.
- **Problem:** Per statement, v1 must: load a multi-page PDF; run pdfplumber char-level extraction (Python/CSnakes round-trip) for the font check (CL-35) and bbox geometry (CL-28); rasterize pages for image-presence pHash and blank-page detection (`pdf2image`/Poppler or pypdfium2 render — not free); decode a QR; then run 50+ rules. pdfplumber char-level parsing on a real statement is routinely **hundreds of ms to seconds**; page rasterization for pHash is similar; and this crosses the C#↔Python boundary. "≤10 s p95" and "≥1/s sustained per worker" cannot both hold unless rasterization/pdfplumber are far cheaper than typical — which is unverified. Cold-start (first statement after worker spin-up pays Python import + model_cache + JIT) is entirely unbudgeted and directly threatens the 3–5 day batch SLA at the *start* of each batch and on autoscale-out.
- **Bite:** SM-4 (100% of monthly sample within window) and NFR-1 are the operational promises; both rest on an unvalidated per-statement cost. At 26k–40k/day peak, a 3× miss on per-statement time blows the window.
- **Fix:** Replace the asserted number with a measured spike (one real statement, p50/p95 broken down by stage), explicitly budget Python/CSnakes warm-up and PDF render, and state worker warm-pool / pre-warm strategy. Treat ≥1/s as a hypothesis to be proven, not a design point.

---

## HIGH

### H-1. ADR-V6 blast radius is understated: `FusionExpedienteService` and `IFusionExpediente` are hardwired to oficio types
- **Where:** architecture.md §8 ADR-V6 ("Extract `IDataFuser<T>` from `FusionExpedienteService` … additively … with no behavior change"), §3 reuse map. Ground truth: `IFusionExpediente.cs` (Domain) signature is `FuseAsync(Expediente?, Expediente?, Expediente?, ExtractionMetadata, ExtractionMetadata, ExtractionMetadata, …)` plus `FuseFieldAsync(string, List<FieldCandidate>, …)`; `FusionExpedienteService.cs` is **2,811 lines** of ~40 hand-written `FuseXxxAsync` methods, each typed to specific `Expediente` properties (`NumeroExpediente`, `SolicitudPartes[0].Rfc`, `DiasPlazo`, …), CNBV `SourceType` (XML_HandFilled / PDF_OCR_CNBV / DOCX_OCR_Authority), and oficio business rules (R29 42-field mandatory, FechaEstimadaConclusion via business-day calculator).
- **Problem:** There is essentially **no** domain-agnostic fusion *kernel* to extract. The genuinely generic part is just `FuseFieldAsync(name, candidates)` (exact → fuzzy → weighted vote) — perhaps ~100 of 2,811 lines. The other 96% is per-field oficio orchestration. So `IDataFuser<T>` is a *new* abstraction whose only Solution-1 implementor is this giant class, which would need to be retrofitted to implement the generic seam while leaving every oficio method intact — non-trivial, and "no behavior change" must be defended across ~40 fused fields and their conflict/confidence math. The doc's framing ("extract IDataFuser<T> from FusionExpedienteService … additively") implies a clean lift that does not exist.
- **Bite:** This is named the "highest-risk integration work" (FR-27) and "the risky part" (§8) — correctly — but it is then mitigated with a single sentence ("gated by regression suite"). The regression suite gates *behavior*, not *effort*; the effort to genericize 2,811 oficio-coupled lines without regressing the confidence/voting outputs is large and under-planned.
- **Fix:** Re-scope ADR-V6 to extract only the field-fusion kernel (`FuseFieldAsync` + `FieldCandidate` + `FusionDecision`) as `IDataFuser<T>`; explicitly state that the per-field orchestration is NOT genericized (Veriqan writes its own). Budget Phase-0 as a real refactor with a characterization-test step, not a rename.

### H-2. The dependency-direction arch test does not exist and the current arch suite would actively FORBID Veriqan's design
- **Where:** architecture.md §2/§8 ("dependency rule is mechanically enforceable"; "a new architecture test … fails the build on any `Prisma → Veriqan` reference … created in Phase-0"), §13. Ground truth: `08 Tests/09 Architecture/Tests.Architecture/HexagonalArchitectureTests.cs` exists and uses **NetArchTest** (`Types.InAssembly(...).ShouldNot().HaveDependencyOn(...).GetResult()`), NOT ArchUnitNET. A grep for `Veriqan` in `09 Architecture` returns **nothing** — no direction test today.
- **Problem (two parts):**
  1. *Implementable?* Yes — NetArchTest supports `ShouldNot().HaveDependencyOn("ExxerCube.Prisma.Veriqan")` from each Solution-1 assembly, so the Veriqan→Prisma rule is buildable. But the doc calls it "ArchUnit-style" (§2 "ArchUnit-style test"), and the stack is NetArchTest; whoever implements it must not reach for ArchUnitNET (not referenced).
  2. *Conflict (the real bite):* The existing `Infrastructure_Projects_Should_Not_Depend_On_Each_Other` and `Test_Assemblies_Should_Respect_Infrastructure_Dependency_Boundaries` tests enforce **no cross-infrastructure dependencies** and **non-system tests depend on ≤1 Infrastructure assembly**. Veriqan's plan (§2) is *six* infrastructure projects (Extraction, Validation, Visual, ReferenceData, Reporting, Persistence) composed by `Veriqan.Orchestration`. The cross-infra rule is namespace-list-based and would not catch Veriqan automatically — but the moment Veriqan infra projects reference each other (e.g. Validation → ReferenceData), or a Veriqan integration test touches >1 Veriqan infra assembly, the *intent* of these guardrails is violated and the lists must be curated. None of this curation is acknowledged.
- **Bite:** "Mechanically enforceable" is doing a lot of work for a test that (a) doesn't exist, (b) is described against the wrong framework, and (c) collides with existing guardrails that need editing — itself a touch of Solution-1 test infrastructure.
- **Fix:** Correct "ArchUnit-style" → "NetArchTest" throughout; specify the exact new `Fact`s; and enumerate how the existing cross-infra / one-infra-per-test rules will be extended (allowlist Veriqan's internal composition) without weakening them for Solution 1.

### H-3. "Untrusted prototype, ~5–15% real" is acknowledged but the salvage list is treated as if it were trustworthy
- **Where:** architecture.md §9 ADR-V8 ("Salvage … `image_quality.py`, `font_detector.py`, `model_cache.py`, the CSnakes wrapper, pinned `requirements.txt`"); PRD §4.9 note; ground truth REUSE doc §3 ("It cannot import," "It has never run," missing `models/` Pydantic package, `PdfProcessor` vs `PDFProcessor` bug).
- **Problem:** The reuse doc states the prototype **cannot import** and **has never run**, yet ADR-V8 lists five artifacts to salvage as if validated. The actual VecExtraction C# project on disk (`vec_csnakes_wrapper.py`) is a thin shim that `from vec_visual_font_identification.csnakes_integration import …` — i.e. it depends on the very package the reuse doc says **does not exist**. So the "salvageable" wrapper is non-functional as shipped, and the pinned requirements.txt is the CUDA stack (see C-1). The architecture inherits a salvage list that has not been independently re-verified at file granularity.
- **Bite:** Estimation risk (R2 in the PRD): teams will plan around "salvage these 5 files" and discover at integration time that the wrapper imports a missing package and the env is the wrong one.
- **Fix:** Gate each salvage item behind a "compiles+imports+one real input" smoke test before counting it as reuse; explicitly note the wrapper's missing `vec_visual_font_identification` dependency must be rebuilt (matches PRD note but the architecture doesn't carry it forward).

### H-4. FR-25 transaction-detail reconciliation depends on `expectedTransactions` reference data that is the single biggest undefined input
- **Where:** architecture.md §4 (FR-25 routed to Validation Engine), §6 ADR-V4 (graceful degradation), §12 ("Reference data undefined"). PRD FR-25, §17 (CL-42/43/44/45 + item 58). Ground truth REUSE doc §6 phase 4 ("transaction-detail … currently consumed by rules but *never specified* … Biggest hidden gap").
- **Problem:** FR-25 is the richest data model in the system (movement-by-movement match by normalized description + amount + date-range). The architecture's data model (§7) lists `VerificationJob`, `Finding`, `StatementModel`, `Verdict`, `Disposition`, `ReferenceBundleVersion`, `EngineVersion` — but **no transaction/movement entity** and **no `ExpectedTransaction` entity**. The `StatementModel snapshot (extracted fields + locators)` is described as flat fields; a statement's *detail lines* (potentially hundreds per statement) are not modeled. FR-25 cannot be implemented or persisted without a `Transaction`/`Movement` and `ExpectedTransaction` aggregate, neither of which appears.
- **Bite:** FR-25 is mapped to the Validation Engine in §13 but has **no data-model home**. This is a data-model gap, not just a reference-data gap.
- **Fix:** Add `StatementMovement` (N per StatementModel) and `ExpectedTransaction` (N per ReferenceBundle) entities to §7; state normalized-description and amount-tolerance keying; confirm reconciliation is set-matching with INSUFFICIENT_DATA when `expectedTransactions` absent (PRD already says so — the model must support it).

---

## MEDIUM

### M-1. Prior-Statement linkage (FR-7) has no persistence/identity model
- **Where:** PRD FR-7 (CL-17, CL-36..41 cross-period), Glossary "Prior Statement"; architecture.md §5 (`VerificationContext` carries `PriorStatement`), §7 data model.
- **Problem:** `VerificationContext` "carries the `PriorStatement`," but §7 has no entity linking a `VerificationJob`/`StatementModel` to its prior Period for the same account, and no statement of where the prior statement's *closing values* live (in the ReferenceBundle? a prior StatementModel row? both?). Cross-period checks (rewards opening = prior closing, installment número-de-pago increment) need a deterministic, reproducible prior reference (NFR-5). Today it is hand-waved between "bundle" (PRD §3 says prior closing values are in the Reference Bundle) and "PriorStatement" (architecture context object) without reconciling the two.
- **Fix:** Decide the source of truth for prior-period closing values (recommend: ReferenceBundle field, versioned by Period per ADR-V5's `ReferenceBundleVersion`), and document the account+Period identity used to bind it.

### M-2. Audit/provenance (§14) claims immutability but the reused audit substrate isn't shown to provide it
- **Where:** architecture.md §7 (`Disposition` "immutable audit rows", `EngineVersion` on every Finding), §10/§14 ("reuse Shared Core audit logging"); PRD §14, FR-18. Ground truth: `Infrastructure.Database` has `AuditRecord` + `AuditLoggerService` + `AuditRetentionOptions` — a logging-style audit, not an append-only/immutable store.
- **Problem:** PRD FR-18 requires disposition records be **immutable** with before/after. The architecture asserts immutability but reuses an `AuditRecord` table that, as a normal EF entity, is mutable by anyone with DB access. "Immutable" needs a mechanism (append-only table, no UPDATE/DELETE grants, hash-chain, or temporal table) — none specified. For a regulator-facing ledger (UJ-2, CNBV §11) this is a compliance-grade claim made without a compliance-grade design.
- **Fix:** Specify the immutability mechanism (e.g., SQL temporal table or insert-only with revoked update/delete + optional hash chaining) rather than "reuse audit logging."

### M-3. Reuse map over-claims `EmguCvImageQualityAnalyzer` for VEC visual checks
- **Where:** architecture.md §3 reuse map ("Image quality | `EmguCvImageQualityAnalyzer` | font/overlap/pagination/pHash checks"); ground truth REUSE doc §2 (analyzer does blur/noise/contrast/sharpness).
- **Problem:** The existing analyzer measures *scan quality* (blur/noise/contrast/sharpness) — useful for OCR gating, largely **irrelevant** to VEC's deterministic visual checks (font from PDF dict, overlap from bbox geometry, pagination, image *presence* via pHash). The reuse map implies it "feeds" font/overlap/pHash; it doesn't. Font and overlap come from pdfplumber/Shapely (Python), not EmguCV; pHash needs a new lib (C-2). So the imaging "reuse" is thin for VEC's actual checks.
- **Fix:** Downgrade EmguCV from "reuse for visual checks" to "available for optional scan-quality gating on image-only PDFs (out of v1 primary scope)"; stop implying it covers font/overlap/presence.

### M-4. Font standard conflict (Aptos vs Arial/Times) noted in ground truth is silently resolved to Aptos with no decision record
- **Where:** PRD FR-9/CL-35 ("Aptos"); ground truth REUSE doc §7 open question 3 ("checklist says **Aptos**; PRP REQ-030 says **Arial/Times New Roman** — which governs?"); architecture.md ADR-V7 ("font names from the PDF font dictionary").
- **Problem:** The architecture hardcodes the *mechanism* (font dictionary) but the *canonical value* (Aptos) is an unresolved conflict per the reuse analysis, and ADR-V3 says thresholds/values are data not code — yet the canonical font isn't shown as ReferenceBundle data. If it's Aptos in code, that violates ADR-V3's "no magic values."
- **Fix:** Put the canonical font family in the ReferenceBundle (consistent with ADR-V3) and flag the Aptos-vs-Arial conflict as an open question carried from the reuse doc, not silently decided.

### M-5. QA Console placement is an open decision (§14.3) but FR-18 immutable disposition + RBAC are in MVP scope
- **Where:** architecture.md §2 (`07 UI/ Veriqan.QaConsole` "or module in existing Web.UI"), §14 item 3; PRD FR-18, §6.1. Ground truth: `07 UI/UI/ExxerCube.Prisma.Web.UI` exists.
- **Problem:** "Separate app vs module in Web.UI" is left open, but the two paths have very different auth/RBAC, deployment, and data-isolation (§13 "isolated from Solution 1") implications. If it's a module in the oficio Web.UI, "data isolated from Solution 1" and "independently deployable" get harder; if separate, it's a net-new UI app with its own auth wiring, uncosted. An MVP FR (disposition) sits on an unmade structural decision.
- **Fix:** Decide before epics; if separate app, cost the auth/host; if module, reconcile with the data-isolation claim.

---

## LOW

### L-1. "≈50–60% reuse" headline is inherited without subtracting the new net-new pieces
- **Where:** architecture.md §1, PRD §1; vs C-2 (QR/pHash new), H-1 (fusion mostly not reusable), M-3 (imaging thin).
- **Problem:** The reuse % predates discovery that QR, pHash, the fusion kernel, the transaction model, and a v1 Python env are net-new. The number is directional but now optimistic. Low severity (it's indicative), but it anchors stakeholder expectations.

### L-2. Idempotency-by-content-hash vs reprocessing (FR-22) has a latent contradiction
- **Where:** architecture.md §4 ("idempotent … by content hash"), §10 ("resumable, idempotent by content hash"); PRD FR-22 ("Re-running a completed statement replaces its prior result").
- **Problem:** Ingestion idempotency (same hash ⇒ no duplicate job) and FR-22 reprocessing (re-run ⇒ replace result) are two different behaviors keyed on the same hash. The architecture states idempotency but not how a deliberate reprocess overrides it (force flag? new EngineVersion supersedes?). Minor, but will confuse implementers.
- **Fix:** One sentence: reprocess creates a new result version (new EngineVersion) superseding the prior for the same content hash, recorded in audit.

### L-3. Queue/transport is an open decision (§14.2) yet NFR-3 (no batch halt, exception queue) assumes one
- **Where:** architecture.md §10/§14 item 2; PRD FR-21, NFR-3.
- **Problem:** "in-proc vs Service Bus vs SignalR/Ember hub" is open, but resumability/exception-queue semantics differ sharply between in-proc and a real broker. Reliability NFRs are written as if a durable queue exists. Acceptable as an open decision, but flag that NFR-3 effectively constrains it toward durable.

### L-4. `ReferenceBundleVersion` versioned "by Period" may be too coarse for reproducibility (NFR-5)
- **Where:** architecture.md §7; PRD NFR-5.
- **Problem:** If a bundle is corrected mid-Period (e.g., a TASA fix), versioning "by Period" can't reproduce which bundle a given job used. Recommend versioning by (Period + bundle revision/hash) and stamping the exact bundle version on each Finding alongside EngineVersion.

---

## Coverage check (FR → component home)

All FR-1…FR-27 have a named component in §13, **except** the following data-model gaps that make their mapped component non-implementable as specified:
- **FR-25** → mapped to Validation Engine, but no `StatementMovement` / `ExpectedTransaction` entity (C-4 / H-4).
- **FR-7** → mapped to Validation Engine, but no prior-statement linkage/identity in the data model (M-1).
- **FR-14 (QR)** and **FR-12 (pHash)** → mapped to Visual Inspection / Regulatory, but no implementing library exists (C-2).

No ADR directly contradicts a PRD requirement in wording, but **ADR-V7 contradicts its own salvaged artifact** (C-1) and **ADR-V5 contradicts FR-23** as written against the real `PrismaDbContext` (C-3).

## Severity counts
- Critical: 4 (C-1 ML/GPU env contradiction, C-2 missing QR/pHash libs, C-3 non-additive DbContext, C-4 unmodeled throughput)
- High: 4 (H-1 fusion blast radius, H-2 arch-test nonexistent/wrong-framework/conflicting, H-3 untrusted-salvage taken on faith, H-4 FR-25 data-model gap)
- Medium: 5 (M-1 prior-statement model, M-2 audit immutability, M-3 imaging over-claim, M-4 font conflict, M-5 QA console placement)
- Low: 4 (L-1 reuse %, L-2 idempotency vs reprocess, L-3 queue/NFR-3, L-4 bundle versioning granularity)
- **Total: 17**
