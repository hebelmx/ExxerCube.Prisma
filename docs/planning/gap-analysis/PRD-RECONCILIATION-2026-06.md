# PRD-vs-Discovery Reconciliation

**Phase 1 of the MVP Audit** (`MVP-AUDIT-PLAN-2026-06-11.md`) · **Date:** 2026-06-11 · **Branch:** Kt2
**Inputs reconciled:** `docs/product/requirements/prd.md` (v1.0, 2025-01-12) · `FINAL_REALISTIC_GAP_ASSESSMENT.md` · `REVISED_SCOPE_CLASSIFICATION.md` · `GAP-MATRIX-2026-06-dual-ground-truth.md` · ADR-001 (OCR) · ADR-009 (Ember/3-process).

> **Purpose.** The PRD is the *intent* we started with. This doc tests each material assumption against what we *discovered while building*, and marks it **Validated / Refuted / Superseded / Re-scoped**. The output feeds the real MVP definition (Phase 2). Verdicts carry the discovery rationale so the institutional knowledge is preserved.

---

## A. The headline discovery — scope philosophy inverted

The single most important finding: **the PRD framed Prisma as a regulatory-compliance *validation/execution* system; what was actually built (and what is actually wanted) is an intelligent document-*processing* assistant.**

| | PRD intent (2025-01) | Discovered reality |
|---|---|---|
| System role | "Regulatory Compliance Automation System" — validate, enforce, generate compliance packages | **Best-effort document processor**: download → OCR → extract → classify → reconcile → **flag for human review**, then hand structured data to the bank |
| Validation | Implied rigorous legal validation (ClassificationRules.md 7-level tree) | **Out of scope** — bank legal dept authenticates/validates/rejects; CNBV validates channel/format; bank ops executes freezes/transfers |
| Success metric | "Compliance" | **80%+ auto-processed, ~20% intelligently flagged**, full traceability |
| Posture | Reject invalid documents | **Never reject** — tolerant parsing, extract what's possible, flag the rest |

**Why it changed:** real inputs are messy (bad scans, malformed XML); the bank — not Prisma — owns legal risk and execution. Source: `FINAL_REALISTIC_GAP_ASSESSMENT.md` §"Reality", `REVISED_SCOPE_CLASSIFICATION.md` §Scope. **This reframing makes large parts of the PRD out-of-scope rather than incomplete — that's the difference between "we're far from done" and "we were measuring against the wrong target."**

---

## B. Functional Requirements reconciliation

| FR | Intent | Verdict | Discovery / evidence | MVP impact |
|---|---|---|---|---|
| **FR1** Auto-download from UIF/CNBV via browser automation | **Re-scoped** | No SIARA **API** exists; login is a **basic web form**; system must **never store credentials** (session-passthrough or interactive one-time login — undecided). Scraping path **works + was demoed** (`SiaraNavigationTarget`, BrowserAutomation.E2E 18/18). Real gap = adapting it into Orion's `IDocumentDownloader` port + a poll/watcher. | **MVP-critical** (this is the top gap) but narrower than "build automation": it's *integration*, not greenfield |
| **FR2** Checksum dedup / download history | **Validated** | `FileIngestionJournal` real, SHA-256 dedup, file-backed (Orion.Worker). | Done |
| **FR3** XML metadata extraction | **Validated** | `XmlFieldExtractor` real CNBV/PRP1 extraction (hardened, 16 tests). | Done |
| **FR4** DOCX structured extraction | **Validated** | Adaptive DOCX (5 strategies) real + tested (126/126). | Done (extractor); see FR9 for fusion wiring |
| **FR5** PDF + OCR fallback | **Partial** | Tesseract OCR real; but `PdfMetadataExtractor` direct-text path returns `string.Empty` (needs iText/PdfSharp) — OCR works, native-PDF-text doesn't. | MVP-relevant for searchable PDFs |
| **FR6** Detect scanned PDF, preprocess | **Validated** | Quality analysis + adaptive filters real (Imaging 44/44). | Done (trained models = placeholder, see NFR) |
| **FR7/FR8** Level-1/2/3 classification (deterministic) | **Validated + expanded** | `FileClassifierService` real; discovery added precedence rules, Oficio de Seguimiento, special scenarios (Recordatorio/Alcance/Precisión), authority-type — several implemented (FINAL_REALISTIC "all 4 gaps complete"). | Done for MVP scope |
| **FR9** Match/consolidate fields across XML+DOCX+PDF → unified record + confidence | **Partial** | `FusionExpedienteService` real; OCR→PDF source now fed (fixed 2026-06). **But worker path is single-source: XML/DOCX still null.** | **MVP-relevant** — multi-source fusion is a core promise |
| **FR10** Identity resolution (RFC variants, aliases, dedup) | **Partial** | Resolver logic real + mutation-tested (GenerateRfcVariants/NormalizeName/Dedup pure & covered); **DB persistence deferred** (`FindByRfcAsync` is a stub). | MVP needs decision: in-memory per-batch vs persisted |
| **FR11** Legal-directive → compliance-action mapping | **Validated (re-scoped)** | Real keyword/precedence classifier producing action + confidence + warnings. Note: this **classifies/suggests**, it does not *enforce* (per reframing). | Done for MVP scope |
| **FR12** SLA deadline calc (business days) | **Partial** | SLA tracking real; `SLAMetricsCollector` has `return 0` telemetry stubs; Mexican-holiday calendar unverified. | MVP-relevant if SLA dashboard is in MVP |
| **FR13** SLA escalation < threshold + alerts | **Partial / Re-scoped** | Threshold logic exists; **notification transport unspecified** (PRD itself flagged this as missing). HMI notification-queue is a **prototype inside a test project**, not wired. | Defer transport; MVP = flag in UI? (owner call) |
| **FR14** Manual review interface | **Partial** | `IManualReviewerPanel` + reviewer service real + tested; degree of UI wiring needs Phase-3 trace. | **MVP-relevant** — flagging is the core value prop |
| **FR15** SIRO-compliant XML export | **Validated** | `AdaptiveExporter` / SIRO XML real + wired (Athena + UI), most complete stage. | Done |
| **FR16** Digitally **signed** PDF (PAdES, X.509) | **Re-scoped (likely P2)** | No evidence of wired PAdES signing; PDF export exists, signing does not. Likely bank-side / certificate-dependent. | **Owner decision: MVP or P2?** |
| **FR17** Immutable audit log | **Partial→Validated** | `AuditReportingService` real + mutation-tested; immutability/retention guarantees unverified. | MVP-relevant (also the seed for separation-of-duties audit) |
| **FR18** Excel layout export | **Validated** | `ExcelLayoutGenerator` real + tested. | Done |
| **FR19** PDF summarization into requirement categories (semantic) | **Partial** | `SemanticAnalyzerService` real path; deeper semantic field extraction is TODO (accounts/amounts/refs). | Re-scope: rule-based suffices for MVP? |
| **FR20** Field-completeness validation pre-export | **Validated** | Export-side validation real (mutation-tested; e.g. blank NumeroExpediente flagged). | Done |
| **FR21–23** (retry/queue/recovery, ToT-added) | **Partial** | Result<T> + manual-review queue exist; explicit retry/backoff unverified. | Re-scope per ops need |
| **FR24–26** (EF persistence, migrations, referential integrity) | **Validated** | EF Core + migrations + System.Storage 39/39, Database 110/110 (Testcontainers). | Done |
| **FR27** Notification integrations (email/SMS/Slack) | **Planned/Deferred** | Not wired (see FR13). | P1/P2 |
| **FR28** REST API for review UI | **Re-scoped** | Blazor Server calls services directly; no separate API layer. PRD itself noted "future separation." | Not MVP |
| **FR29** Export webhooks | **Planned/Deferred** | Not present. | P2 |
| **FR30** RBAC for manual review | **Re-scoped → elevated** | Now part of the **separation-of-duties security model** (per-stage clearance), bigger than "RBAC on a screen." Auth abstraction real-but-unwired. | **MVP-relevant if security model is MVP** (owner call) |
| **FR31** Non-notification enforcement (don't tip off client) | **Unverified** | Mentioned in PRP; not traced. | Verify in Phase 3 |
| **FR32** Retention/archival/deletion policy | **Planned/Deferred** | Not implemented. | P2 |

---

## C. Non-Functional Requirements reconciliation (MVP lens)

| NFR | Verdict | Note |
|---|---|---|
| NFR1 OCR memory ≤+20% | **Re-scoped** | Engine changed (Tesseract-C#); original baseline moot. |
| NFR2 Async/concurrent | **Validated** | Result<T> + async throughout; CancellationToken now honored across impls. |
| NFR3 Browser ≤5s · NFR4 extract timings · NFR5 classify ≤500ms | **Unverified / Re-scoped** | No perf benchmarks run; **volume is low (500–2,000 docs/day, random hours)** so these targets are non-binding for MVP. |
| NFR6 Horizontal scaling / microservices | **Superseded → required** | Not "optional scaling" anymore — the **3-process split is a security boundary** (see §D). |
| NFR7 99.9% uptime | **Deferred** | Aspirational; not an MVP gate at this volume. |
| NFR8 Encryption at rest/in transit (TLS 1.3) · NFR17 field-level PII encryption | **Re-scoped / verify** | Confidentiality is now a **first-order driver** (need-to-know). Field-level PII encryption may be MVP-relevant. Owner call. |
| NFR9 7-year audit retention | **Deferred** | Policy/infra; audit *logging* exists, retention enforcement doesn't. |
| NFR10 Digital signature infra | **Re-scoped (P2)** | Pairs with FR16. |
| NFR11 Structured logging + correlation IDs | **Validated** | Serilog + scopes + OpenTelemetry. |
| NFR12 Backward-compat OCR interfaces | **Superseded** | Interfaces evolved (`IFieldExtractor<T>`); "backward compat" with the old shape is no longer a goal. |
| NFR13 Config-driven matching policies | **Validated** | `MatchingPolicyService` / `NameMatchingPolicy` real + tested. |
| NFR14 Graceful error handling | **Validated** | Result<T> pattern enforced. |
| NFR15 Batch processing | **Partial** | Pipeline supports it; throughput targets non-binding. |
| NFR16 Azure AD / Identity Server auth | **Re-scoped** | UI uses ASP.NET Identity (cookie); hexagonal auth port (JWT) real-but-unwired. |

---

## D. Architectural-assumption reconciliation

| PRD assumption | Verdict | Discovery |
|---|---|---|
| **OCR via CSnakes Python + VLM** (GOT-OCR2/DocTR) | **Superseded** | Python.NET removed; CSnakes hard to operationalize → **Tesseract-C# is the engine of record** (deliberate). Python/VLM scaffolding retained **dormant by design** for future optionality. ADR-001. *Not a gap.* "DocTR production-ready" mission doc = research experiment, not shipped. |
| **Deploy as monolith first; microservices optional** | **Superseded** | The **Downloader / Extractor / Reconciliator** 3-process split is now driven by **separation-of-duties security**: confidential docs, strict need-to-know, **no single process touches a doc end-to-end**. Coordinated via `IndFusion.Ember` (`IExxerHub<T>`/`IServiceHealth<T>`/`Dashboard<T>`). ADR-009. Today only monolithic Orion/Athena hosts exist; Ember real in UI, stubbed in workers. |
| **SignalR in-repo for real-time** | **Superseded** | Extracted to published **`IndFusion.Ember`** NuGet; reference the package, don't revive in-repo SignalR. |
| **UIF/CNBV websites, consistent structure, automatable** | **Refuted (partially)** | No API; basic-form login; **no credential storage allowed**. Scraping works against the purpose-built `tools/Siara.Simulator`. Real connector = web-scraping adapted from the simulator scraper. |
| **Only UIF/CNBV authorities** | **Re-scoped (broader)** | Authority taxonomy expanded: Judicial / Fiscal (SAT/FGR/SHCP) / Ministerial / Administrative (UIF/CONDUSEF) / Amparo. |
| **Additive-only DB schema, EF migrations** | **Validated** | EF Core migrations; integration tests green. |
| **Python doc-generator dependency** | **Validated (caveat)** | Exists; the `CSharp/Python/` vs `Src/Python/` tree duplication is a **known deferred** reconciliation, not a bug. |
| **Sentinel monitoring service** | **Partial** | Real, tested library (16/16) but **no Worker host** — built, not deployed as a running service. |

---

## E. Open judgment calls for the owner (these set the Phase-2 MVP bar)

These are the decisions that the code can't answer — they're scope/business calls that determine what "MVP" means. They're carried into Phase 2:

1. **3-process security split vs MVP timing.** Is the Downloader/Extractor/Reconciliator separation-of-duties split a **prerequisite for MVP** (because the docs are confidential and compartmentalization is non-negotiable even for a pilot), or can a **single-tenant monolith pilot** ship as MVP with the split as the immediate next phase?
2. **SIARA auth approach.** Session-passthrough vs interactive-one-time-login — undecided. This shapes the Downloader and is on the MVP-critical path (FR1).
3. **Digital PDF signing (FR16/NFR10).** MVP or P2? (Leaning P2 / bank-side.)
4. **Manual-review depth for MVP.** Is MVP "flag + review in the existing UI," or does it need the full review dashboard + RBAC?
5. **Identity persistence (FR10).** Per-batch in-memory dedup acceptable for MVP, or is cross-document persisted identity resolution required?
6. **SLA + notifications (FR12/13).** Is the SLA dashboard in MVP, and is "flag in UI" enough (defer email/SMS/Slack to P1)?

---

## F. One-paragraph synthesis

Measured against the *original* PRD, Prisma looks ~50% done. Measured against the *discovered* scope — an intelligent best-effort document processor that flags for human review, with the bank owning validation/execution — it is **substantially built**: ingestion-extraction-classification-fusion-export all have real, tested implementations. The genuine remaining MVP gaps are **few and specific**: (1) real SIARA ingestion wired into Orion's port + watcher (the top one), (2) multi-source fusion (XML/DOCX into the worker path, not just OCR), (3) the security-driven 3-process split *if* required for MVP, and (4) a short list of partials (PDF native text, identity persistence, SLA telemetry, manual-review UI depth). The PRD's heavy compliance-validation, digital-signing, and enterprise-NFR clauses are mostly **re-scoped out of MVP**, not unfinished. That is exactly the "small but disqualifying gaps" picture — and the next step (Phase 2) is to turn §E's answers into a crisp MVP acceptance checklist.
