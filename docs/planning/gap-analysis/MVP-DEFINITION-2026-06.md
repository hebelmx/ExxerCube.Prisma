# MVP Definition — derived from PRD-vs-Discovery reconciliation

**Phase 2 of the MVP Audit** · **Date:** 2026-06-11 · **Branch:** Kt2
**Depends on:** `PRD-RECONCILIATION-2026-06.md` (Phase 1) + owner scope decisions (2026-06-11, recorded in §0).
**Status:** AGREED MVP BAR — this is the target the gap matrix (Phase 3–4) and MVP path (Phase 5) measure against.

> This document defines what "MVP-ready" *means* for Prisma, in three tiers, as binary acceptance criteria. It is the bar — Phase 3 traces the code against it; Phase 5 orders the remaining work to close it.

---

## 0. Owner scope decisions (2026-06-11)

| Decision | Ruling | Effect on MVP |
|---|---|---|
| 3-process security split (Downloader/Extractor/Reconciliator) | **Required for MVP** | Separation-of-duties is non-negotiable even for a pilot. MVP includes the Ember-coordinated split + per-stage authorization + audit. |
| Digital PDF signing (FR16/NFR10) | **P2 / bank-side** | Out of MVP. Unsigned structured exports suffice. |
| Manual-review scope | **Full review dashboard** | MVP needs list + field-level confidence/conflict annotations + override/notes + reviewer-identity audit. |
| Multi-source fusion (XML+DOCX+PDF) | **In MVP** | Close the single-source gap in the worker path. |
| Native PDF text extraction | **In MVP** | Searchable-PDF direct text (iText/PdfSharp); OCR already covers scanned. |
| SLA dashboard + deadline tracking | **In MVP** | Wire deadline calc + at-risk flagging + real telemetry. Notification *transport* still deferred. |
| Persisted identity resolution (FR10 DB) | **Out of MVP** | Per-batch in-memory RFC/alias dedup is acceptable for MVP. |

**Still-open implementation decision (not a scope blocker):** SIARA auth approach — *session-passthrough* vs *interactive one-time login*. Both avoid storing credentials. To be chosen during Downloader work.

---

## 1. The MVP, in one sentence

> **An intelligent, security-compartmentalized document-processing pipeline that automatically pulls confidential regulatory requirements from the SIARA portal, runs them through quality→OCR→multi-source-fusion→classification→export, flags low-confidence/conflicting cases to a human-review dashboard, tracks SLA deadlines, and produces SIRO-compliant structured exports — with no single process touching a document end-to-end and a complete audit trail of who accessed what.**

The bank still owns legal validation and execution; Prisma is the best-effort processor + intelligent flagger.

---

## 2. Tier 1 — DEMO-ABLE (what can be shown *today*)

These already work and are demonstrable (evidence in GAP-MATRIX §9 live run):

- [x] Web.UI boots, renders (MudBlazor), `/health` Healthy.
- [x] Document-processing demo path runs **real Tesseract OCR** on fixtures.
- [x] SIARA **download demoed** via browser-automation scraper against `tools/Siara.Simulator`.
- [x] Quality analysis + adaptive filtering, classification, SIRO XML / Excel / PDF export.
- [x] OCR→Fusion (single-source) end-to-end in Athena Worker.
- [x] ~1,670+ tests green incl. Docker SQL + Playwright/UI + E2E.

**Demo caveat (must-fix even for demo):** hardcoded `Server=DESKTOP-FB2ES22\SQL2022` connection string and leftover `Counter.razor`/`Weather.razor` template pages.

---

## 3. Tier 2 — MVP ACCEPTANCE CHECKLIST (binary; this is the bar)

### A. Ingestion — real SIARA + 3-process security split
- [ ] **A1.** Working browser-automation scraper adapted into Orion's `IDocumentDownloader` port (replaces `StubDocumentDownloader`); no raw credentials stored (auth approach chosen + implemented).
- [ ] **A2.** `IngestionOrchestrator.StartAsync()` implements the SIARA poll/watch loop (replaces placeholder); idempotent via existing SHA-256 journal; cadence relaxed (≤2k docs/day).
- [ ] **A3.** `StubExxerHub` replaced with **real `IndFusion.Ember` transport** in the worker(s).
- [ ] **A4.** Pipeline runs as **three separately-hosted, Ember-coordinated processes** — **Downloader / Extractor / Reconciliator** — exchanging events/handoffs, not shared raw document access.
- [ ] **A5.** **Per-stage authorization** (role/clearance) enforced; **data minimization** between stages (pass references/derived data where a stage doesn't need full content).
- [ ] **A6.** **Audit trail of who/which-process accessed which document** (extends `AuditReportingService`).

### B. Pipeline — close the processing gaps
- [ ] **B1.** **Multi-source fusion**: XML and DOCX sources fed into the worker fusion path (not just OCR/PDF) → `FuseAsync` reconciles all three with confidence scores.
- [ ] **B2.** **Native PDF text extraction** wired (iText/PdfSharp) for searchable PDFs; OCR fallback retained for scanned.
- [ ] **B3.** (already done — keep green) Quality, OCR, classification (+precedence/special-scenario), SIRO/Excel/PDF export.

### C. Manual review — full dashboard
- [ ] **C1.** Flagged cases (low confidence / conflict / missing required field) listed in a review dashboard with filters.
- [ ] **C2.** Field-level annotations showing **source, confidence, and conflicts** per field.
- [ ] **C3.** Reviewer can **override classification, correct values, add notes**; changes persisted to the unified record.
- [ ] **C4.** All review actions logged to audit trail **with reviewer identity**.

### D. SLA tracking
- [ ] **D1.** SLA deadline calculated from intake + días plazo (business days; Mexican-holiday calendar verified or explicitly simplified).
- [ ] **D2.** At-risk cases (< threshold) flagged and surfaced on an SLA dashboard; telemetry returns **real values, not zeros**.

### E. Ops / readiness (minimal, MVP-grade)
- [ ] **E1.** Readiness probe real (`orchestrator.IsStarted`, not TODO) for each hosted process.
- [ ] **E2.** Connection strings + secrets externalized (config/env), no hardcoded host; template demo pages removed.
- [ ] **E3.** Each of the 3 processes exposes health via Ember `IServiceHealth<T>` / `/health`.

### F. Verification gate
- [ ] **F1.** One **real end-to-end run** proven: SIARA pull → 3-process pipeline → flagged case appears in review dashboard → SIRO export produced → audit trail complete — with no stub on the critical path.
- [ ] **F2.** Suite stays green (incl. the new ingestion-chain + multi-source-fusion + review-dashboard tests); the flaky live-OCR test stabilized.

---

## 4. Tier 3 — POST-MVP (explicitly deferred, with rationale)

| Item | Why deferred |
|---|---|
| Digital PDF signing / PAdES / cert management (FR16, NFR10) | Owner: bank-side; certificate-dependent. |
| Notification transport — email/SMS/Slack (FR13, FR27) | MVP flags in-UI; transport is P1. |
| Persisted cross-document identity resolution (FR10 DB) | Owner: per-batch in-memory dedup OK for MVP. |
| Trained polynomial/NSGA-II filter-selection models | Stub coefficients work; needs filtering-study dataset. |
| Deeper semantic field extraction (FR19) | Rule-based suffices for MVP. |
| 99.9% uptime (NFR7), 7-yr retention enforcement (NFR9), field-level PII encryption (NFR17) | Enterprise hardening; verify NFR17 relevance in Phase 3. |
| REST API layer (FR28), export webhooks (FR29), retention/deletion policy (FR32) | Future separation / ops. |
| Sentinel as a hosted Worker | Library real + tested; hosting is P1 (Ember `IServiceHealth` may cover MVP monitoring). |
| Worker `/dashboard` zeros (Orion/Athena) | UI dashboard uses the real metrics service; worker endpoints are out-of-band. |

---

## 5. What this implies for the path (preview — full ordering in Phase 5)

Highest-leverage, MVP-blocking work, roughly in dependency order:

1. **Ingestion + 3-process split (A1–A6)** — the dominant effort; also the security spine. Likely the critical path to F1.
2. **Multi-source fusion (B1)** + **native PDF text (B2)** — pipeline completeness; independent of A, can parallelize.
3. **Manual-review dashboard (C1–C4)** — depends on flags already produced by classification/fusion.
4. **SLA dashboard (D1–D2)** + **readiness/ops (E1–E3)** — smaller, partly cleanup.
5. **E2E verification gate (F1–F2)** — proves the whole thing.

Phase 3 (ground-truth trace) will confirm exactly how much of A–E already exists in code vs. needs building, and Phase 5 will turn this into a sized, ordered task list with per-item Definition of Done.
