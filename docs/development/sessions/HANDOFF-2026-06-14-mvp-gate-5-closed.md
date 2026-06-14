# HANDOFF — 2026-06-14 — MVP Gate (#5) CLOSED — ✅ MVP REACHED

**Branch:** `Kt2` (pushed) · **Driver:** bmad-orchestrator (delegate → **verify every chunk from ground truth**).
**Predecessor:** `HANDOFF-2026-06-14-max-fidelity-gate-build.md` (gate built) + `HANDOFF-2026-06-14-gh6-iscomplete-review-wire.md` (GH #6).

---

## TL;DR

Issue **#5 (the MVP gate, 5.1 + 5.2)** is the last item on `MVP-PATH-2026-06-11.md`. This session **verified it
from ground truth** (re-ran the live gate, not trusting prior prose), found + fixed a real flake the prior
de-flake missed, extended best-effort coverage per a new owner ruling, ran an adversarial completion review,
and **closed issue #5 → MVP reached.** No production code changed; all work is tests + one stale-comment fix.

## What was verified / done (each verified from ground truth — build + run + git, not summaries)

1. **Solution build 0/0** (full `.sln`, 70 projects).
2. **5.2 live gate re-run GREEN 2/2** (`MaxFidelityGateE2ETests`, 21m 24s on this Docker+Playwright+Tesseract box):
   - `RealSiaraCase_FlowsAcrossAllThreeProcesses_WithRealPipeline_AndPersistsAudit` — real SIARA Playwright pull →
     real `SiaraDocumentDownloader` → real Tesseract OCR → real `FusionExpedienteService` → real `FileClassifierService`
     → real `SiroXmlExporter` → audit to **Testcontainers SQL**, across both real SignalR edges + JWT clearance.
     No stub on the data path (the in-memory SignalR *transport* seam is the one accepted, documented limitation).
   - `PartialCase_MissingCompanionFile_StillProcessesBestEffort_AndFlagsIncomplete` — best-effort partial path.
3. **5.1 de-flake — NEW real flake found + fixed** (`1ffe1de`): the prior 2026-06-13 de-flake fixed only the PDF-OCR
   timeout tests; the suite was still **24/25** because `DocumentPipelineEventPersistenceTests` asserts eventual
   consistency on a **2s** poll budget — starved past 2s when its class runs in parallel with the assembly's
   CPU-heavy live-OCR classes. Raised the budget to **30s** (poll returns early on count-met → only guards a hang;
   same doctrine as the PDF de-flake). **System.Ocr.Pipeline 25/25 x2** under load + 3/3 in isolation.
4. **5.1 / item 2 reframed by owner → best-effort across ANY file combination** (`3a923d3`,
   `CaseCombinationBestEffortTests`): owner ruled that a SIARA *requerimiento de la autoridad* arriving with any
   subset of files — **only the DOCX, only the XML, any two, any combination** — is a VALID request that must NEVER
   be invalidated; the FI answers best-effort, and no missing/corrupt file invalidates it. New deterministic tests
   over the REAL extraction+fusion chain prove: **DOCX-only** (no throw, `QualityRejected=false`, Stage 3 completes
   with an empty expediente — answered, not invalidated), **XML-only** (fuses `EXP-2598-2020`), **XML+DOCX-no-PDF**
   (XML carries the expediente; non-matching DOCX doesn't break it). Athena.Processing.Tests **91/91** (was 88).
5. **Stale comment fix** (`f5579a3`): the gate's `[DEV MISSING]` doc said review-case persistence "NOT yet wired",
   but GH #6 (`2c5b01d`) landed it — corrected the note (surfaced by the adversarial review).

## Ground-truth FINDING (logged to issue #2, NOT MVP-blocking)

On **real** SIARA documents the DOCX is the SAT *requerimiento* letter whose only id is like
`AGAFADAFSON2/2023/031698`, which does **not** match `DocxFieldExtractor.ExtractExpediente`'s regex
`[A-Z]/[A-Z]{1,2}\d+-\d+-\d+-[A-Z]+` (`Infrastructure.Extraction/Teseract/DocxFieldExtractor.cs:189`) — so the
real DOCX contributes **no expediente** in production; the **XML** (`<Cnbv_NumeroExpediente>`) is the canonical
source and the PDF-OCR corroborates. The synthetic `MultiSourceFusionIntegrationTests` 3-source fuse only works
because its fixtures are crafted to match the regexes. Best-effort holds (XML carries the expediente; the case
still exports + audits), so this is **hardening**, not a blocker — widen the DOCX extractor patterns to the real
SAT/requerimiento shapes if richer DOCX field extraction is wanted.

## Adversarial completion review (plan-completion-reviewer)

**VERDICT: COMPLETE, no Blocker/Major; 3 Minors** — all addressed or deferred:
- (fixed) stale `[DEV MISSING]` comment → `f5579a3`.
- (done) issue #5 closed with commit refs.
- (deferred → #2) the live gate's SIRO assertion is shallow (size>0, doesn't parse XML content). Reviewer itself
  graded it "not a correctness gap"; logged as hardening. Declined to expand the 21-min live gate for it now.
Reviewer reproduced build 0/0, OCR 25/25, Athena 91/91; confirmed no skipped/tautological tests and no stub on
the gate data path.

## Commits (Kt2, pushed)

- `1ffe1de` test(#5): de-flake event-persistence eventual-consistency timeout under load
- `3a923d3` test(#5): best-effort across any case-file combination (owner ruling)
- `f5579a3` test(#5): correct stale [DEV MISSING] note — review-case persistence is wired (GH #6)

## Remaining (all post-MVP, tracked on issue #2 "Deferred hardening")

- `DocxFieldExtractor` regex too narrow for real SAT requerimiento DOCX (the finding above).
- SIRO XSD schema validation (blocked on the Banamex `.xsd`).
- Deeper SIRO-content assertion in the live gate (parse XML root / NumeroExpediente survival).
- Pre-existing issue-#2 items: jti replay, `file_id==Empty` reject, per-process asymmetric keys, Kestrel-path E2E,
  SIRO artifact written to shared storage.

## Hard constraints (unchanged)

- ITDD per ADR-005; `Result<T>` + `CancellationToken` everywhere; xUnit v3 + Shouldly + NSubstitute.
- Commit code+tests SEPARATELY from docs; push `Kt2` (never `main`). Never pipe `dotnet test` through `tail`.
- MTP filter-query needs 4 segments `/Assembly/Namespace/Class/Method`.
- **Verify every chunk from ground truth** — this session re-ran the live gate rather than trusting the prior
  "gate GREEN" handoff, and that re-grounding is what found the second event-persistence flake.
