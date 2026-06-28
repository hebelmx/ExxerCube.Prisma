---
stepsCompleted: ["step-01-validate-prerequisites", "step-02-design-epics", "step-03-create-stories"]
inputDocuments:
  - docs/demos/VERIQAN-DEMO-PLAN-2026-06-27.md
  - docs/demos/VERIQAN-MVP-PATH-2026-06-27.md
  - docs/demos/VERIQAN-DEMO-CAPTURE-RUNBOOK.md
  - docs/planning-artifacts/prds/prd-veriqan-vec-2026-06-16/prd.md
generatedBy: bmad-create-epics-and-stories (driven by orchestrator on owner's behalf)
status: PLANNED — for a fresh-context implementing agent. NOT yet built.
---

# Veriqan VEC — Demo Hardening Epic Breakdown

## Overview

Context-rich epics + stories for the next implementing agent (fresh context). Builds on the
**honest** current state (branch `Liv`, commits up to `72f08063`): the extractor was fixed
(8→21 fields, no bypass), the demo E2E runs the real path and asserts true verdicts —
`good.pdf` = **RED** (genuine CONDUSEF non-compliance: missing Compara-tu-Tarjeta/CL-32,
Comportamiento, Advertencias, several §sections, §26 Notas / §27 Glosario verbatim),
`bad-font-cl35` = RED CL-35, `scanned` = BLOCKED, `bad-math-cl21` CL-21 = InsufficientData.

> **GLOBAL CONSTRAINT — read before any story.**
> - **Golden-master / blind evaluation (Demo Plan §5c):** the corpus is the *master golden
>   dataset = production-quality statements*. Never frame it as anonymized/synthetic/test, never
>   excuse a finding by its origin, never bypass a guard (no `minExtractionCoverageCount: 0`).
> - **Stack:** .NET 10; `Result<T>` (no exceptions for business logic); `CancellationToken` on
>   every async; nullable + `TreatWarningsAsErrors`; `GenerateDocumentationFile`.
> - **Tests:** xUnit v3 + Shouldly + NSubstitute (NO Moq/FluentAssertions). In this repo
>   `dotnet test <csproj>` falsely reports "Zero tests ran" — run the built binary via
>   `dotnet exec <BuildArtifacts>/Prisma/bin/<asm>/net10.0/<asm>.dll`. Use
>   `TestContext.Current.CancellationToken`.
> - Verify every claim from ground truth (build/test/git), commit in meaningful chunks.

## Requirements Inventory

### Functional Requirements (this planning round; extends PRD FR-1…FR-27)
- **FR-T1** Two-tier verdict: evaluate each statement against the **bank's internal checklist**
  AND the **CONDUSEF regulatory checklist**, producing an independent verdict per tier.
- **FR-T2** New **YELLOW** signal: bank-tier = YELLOW when the statement passes the bank checklist
  but has remaining gaps reframed as **"bank improvement opportunities."**
- **FR-T3** CONDUSEF tier = RED on any regulatory non-compliance; a statement may be YELLOW (bank)
  AND RED (CONDUSEF) at once. GREEN only when BOTH tiers fully pass.
- **FR-R1** Per-issue **highly-visible** compliance report: every non-compliance prominent on the
  marked PDF and the UI, with locator, severity, tier, and CL/LAW id.
- **FR-M1** Build an **enhanced CONDUSEF-compliant master** from `good.pdf` with the missing
  features added; it should verify GREEN (or surface only remaining true-positives).
- **FR-C1** Every verdict and finding carries a **confidence degree**.
- **FR-C2** **BLOCKED** is reserved for documents with a genuine defect (a deliberate human
  callback); a low-extraction inability is a **system gap**, surfaced as such — never a verdict.
- **FR-D1** CL-21 must be able to fire on `bad-math-cl21` (extract the payment-distribution operands).

### Non-Functional Requirements
- **NFR-1** No guard bypasses / no manufactured verdicts (enforces §5c).
- **NFR-2** Extraction & verdict changes keep `Veriqan.Infrastructure.Extraction.Tests` (186) and
  `Veriqan.Orchestration.Tests` (78) green; full solution build 0/0.
- **NFR-3** Tier classification + confidence are auditable (persisted with the verdict/findings).

### Additional Requirements (Architecture / carried)
- Reuse the existing `VerdictAggregator`, `VerificationPipeline`, `IMarkedPdfGenerator`,
  `CsvReferenceDataAdapter`, and the W-UI (`ExxerCube.Prisma.Veriqan.Web.UI`). Respect the
  one-way `Veriqan → Prisma` dependency rule (architecture test enforces it).
- Production-hardening Tier B items (#15/#19/#21/#22/#25/#26/N1/N4) per MVP-Path Tier B.

## Epic List
1. **Two-Tier Compliance Verdict (Bank + CONDUSEF) with YELLOW** — the verdict-model core.
2. **Highly-Visible Per-Issue Compliance Report** (marked PDF + UI).
3. **Enhanced CONDUSEF-Compliant Master Document** (iterate `good.pdf` → GREEN).
4. **Verdict Confidence + Honest BLOCKED Semantics.**
5. **Demo Detection Completeness** (CL-21 operands).
6. **Production-Readiness Hardening (Tier B).**

---

## Epic 1: Two-Tier Compliance Verdict (Bank + CONDUSEF) with YELLOW

Introduce a two-tier verdict so a statement is judged against the bank's own checklist (YELLOW =
passes-with-improvement-opportunities) and the CONDUSEF regulatory checklist (RED) independently.
Touch: `Veriqan.Domain` (VerdictSignal/enums), `Veriqan.Application/Verdict` (VerdictAggregator),
the reference-bundle/checklist metadata, persistence (JobVerdict), and the UI.

### Story 1.1: Add YELLOW signal + two-tier verdict model
As a compliance analyst, I want each statement to carry a separate bank-tier and CONDUSEF-tier
verdict, so that bank-acceptable-but-improvable statements are distinguished from regulatory failures.

**Acceptance Criteria:**
**Given** the `VerdictSignal` enum (currently Green/Red/Blocked/InsufficientData)
**When** the model is extended
**Then** a `Yellow` signal exists, and the verdict outcome exposes `BankTierVerdict` and
`CondusefTierVerdict` (each a VerdictSignal) plus the existing overall signal.
**And** GREEN overall requires BOTH tiers GREEN; YELLOW overall when bank passes with opportunities
and CONDUSEF is not RED; RED overall when CONDUSEF is RED.
**And** `Veriqan.Application.Tests` + `Orchestration.Tests` stay green (run via `dotnet exec`).

*Impl context:* `VerdictSignal` in `01 Core/Veriqan.Domain`; `VerdictAggregator.cs` precedence
logic (BLOCKED→RED→GREEN today). Keep abstain-safety (InsufficientData never escalates to RED).

### Story 1.2: Classify each check into a tier (bank vs CONDUSEF) + bank checklist catalog
As a product owner, I want every CL/LAW rule tagged with the checklist tier it belongs to, so the
aggregator can compute the two tiers.
**Acceptance Criteria:**
**Given** the rule registry (CL-1…CL-55, LAW-*)
**When** each rule declares (or a catalog maps) its tier: `Bank`, `Condusef`, or `Both`
**Then** the aggregator partitions findings by tier.
**And** the bank-checklist membership is data-driven (reference bundle / config), not hardcoded,
so a bank can supply its own checklist.

*Impl context:* rules in `02 Infrastructure/Veriqan.Infrastructure.Validation/Rules` and
`…Visual/Rules`; `IVecValidationRule`. Add a `ChecklistTier` to the rule metadata or a bundle CSV.

### Story 1.3: VerdictAggregator computes both tiers + "improvement opportunities"
As an analyst, I want bank-tier gaps surfaced as improvement opportunities (not hard failures),
so the report communicates bank-acceptable status with a path to full compliance.
**Acceptance Criteria:**
**Given** findings partitioned by tier
**When** the bank tier has only bank-improvement gaps (no bank-blocking failures)
**Then** BankTierVerdict = YELLOW and those gaps are labelled "improvement opportunity."
**When** any CONDUSEF-tier check fails **Then** CondusefTierVerdict = RED.
**And** `good.pdf` yields BankTier=YELLOW + CondusefTier=RED under the real bundle (assert the true
result; do not force).

### Story 1.4: Persist + expose the two-tier verdict
As an auditor, I want both tier verdicts + the opportunity/non-compliance split persisted with the
job, so the audit trail is complete.
**Acceptance Criteria:**
**Given** a completed verification **When** persisted **Then** `JobVerdict` stores both tiers and
each finding's tier; the API/outcome exposes them. **And** EF migration added; persistence tests green.

---

## Epic 2: Highly-Visible Per-Issue Compliance Report

Make every non-compliance unmissable on the marked PDF and in the UI, grouped by tier.

### Story 2.1: Per-finding marked-PDF emphasis
As an analyst, I want each finding highlighted at its exact locator with tier-coded emphasis, so I
can see every issue on the document itself.
**Acceptance Criteria:**
**Given** a RED/YELLOW outcome with findings carrying locators
**When** the marked PDF is generated **Then** each finding renders a prominent highlight (color by
tier: CONDUSEF-RED vs bank-improvement-amber) with a callout/number tying to the report list.
**And** findings without a precise locator render a page-level banner. **And** reporting tests green.

*Impl context:* `IMarkedPdfGenerator` / `MarkedPdfGenerator.cs` (PdfSharp; Y-flip + rotation already
handled). Today it renders generic highlights — extend to per-tier styling + numbered callouts.

### Story 2.2: UI compliance-report page (per-issue, grouped by tier)
As an analyst, I want a UI report listing every issue with locator, severity, tier, CL/LAW id and a
jump-to-document link, so I can triage quickly.
**Acceptance Criteria:**
**Given** a verified statement in the demo UI
**When** I open its report **Then** I see two clearly-separated sections — "CONDUSEF
non-compliance (RED)" and "Bank improvement opportunities (YELLOW)" — each item showing CL/LAW id,
human description, locator/page, severity, and confidence. **And** a GREEN/YELLOW/RED tier banner.

*Impl context:* `07 UI/UI/ExxerCube.Prisma.Veriqan.Web.UI` (MudBlazor). Extend `RedCase.razor`/the
55-check grid + `DemoDataService` shape to carry tier + confidence.

### Story 2.3: Tri-state verdict banner + legend
As a stakeholder, I want a single banner that reads GREEN / YELLOW / RED with a one-line legend, so
non-technical reviewers grasp the status instantly.

---

## Epic 3: Enhanced CONDUSEF-Compliant Master Document

Realize the §5c "iterate the master toward an approved compliant statement" path: produce a master
that adds the features `good.pdf` genuinely lacks, and verify it GREEN honestly.

### Story 3.1: Author the enhanced master from good.pdf
As a corpus engineer, I want an enhanced statement derived from `good.pdf` that adds the missing
CONDUSEF features, so we have a candidate compliant master.
**Acceptance Criteria:**
**Given** `good.pdf` (golden master) and its true findings
**When** the enhanced master is produced **Then** it contains: Compara-tu-Tarjeta (CL-32),
Comportamiento, Advertencias, the missing §sections, the §26 Notas verbatim texts, and the §27
Glosario verbatim terms — rendered to match the layout (extraction must still succeed).
**And** it is added as a golden compliant-master fixture (e.g. `compliant-master.pdf`), PII-clean.
**And** evaluated blind (no "this was edited" framing reaches any rule/agent).

*Impl context:* extend `scripts/veriqan-corpus/anonymize.py` (or a sibling `enhance.py`) to inject
the mandated sections/legends from the §17/§26/§27 catalogs; keep values consistent + valid.

### Story 3.2: Verify the enhanced master honestly
As an analyst, I want the enhanced master's true verdict, so we know how close to GREEN it is.
**Acceptance Criteria:**
**Given** the enhanced master + the real bundle
**When** verified **Then** the test asserts its TRUE verdict (target GREEN; if residual
true-positives remain, assert them and document the remaining punch-list). No forcing, no bypass.

---

## Epic 4: Verdict Confidence + Honest BLOCKED Semantics

### Story 4.1: Confidence degree on verdicts + findings
As an analyst, I want a confidence score on every verdict and finding, so borderline results route
to human review appropriately.
**Acceptance Criteria:**
**Given** a rule evaluation **When** it produces a finding/verdict **Then** a confidence (0–1, or a
defined scale) is attached, persisted, and surfaced in the UI/report. **And** aggregation defines
how per-finding confidence rolls up to the verdict.

*Impl context:* `RuleFinding`, `VerdictSummary`, `VerdictAggregator`; persistence + UI.

### Story 4.2: BLOCKED reserved for real defects; low-extraction is a system gap
As a product owner, I want BLOCKED to mean "genuine defect → human callback," not "couldn't read
the layout," so verdicts are honest.
**Acceptance Criteria:**
**Given** a statement the extractor cannot sufficiently parse
**When** coverage is below floor **Then** the outcome is flagged as a SYSTEM/extraction gap
(distinct from a document BLOCKED), logged + surfaced for engineering — not presented as a
compliance verdict. **And** document-defect BLOCKED remains a deliberate human callback with confidence.

---

## Epic 5: Demo Detection Completeness

### Story 5.1: Extract payment-distribution operands so CL-21 fires
As a demo operator, I want CL-21 to evaluate the injected arithmetic defect on `bad-math-cl21`, so
the math-defect capture works.
**Acceptance Criteria:**
**Given** the Banamex Visa layout **When** the extractor runs **Then** `AdeudoPeriodoAnterior` and
`PagosYAbonos` are extracted (or, if genuinely absent from this layout, that is reported and the
math fixture is re-targeted to an injectable check that the layout supports). **And** on
`bad-math-cl21` CL-21 returns RED (not InsufficientData). **And** extraction tests stay green.

*Impl context:* `PdfPigStatementFieldExtractor.cs` (see commit `72f08063` for the layout fixes);
`Section20PaymentDistributionRule` / CL-21 rule. Tracker task #10.

---

## Epic 6: Production-Readiness Hardening (Tier B)

From MVP-Path Tier B — each a self-contained story. Not demo-blocking; sequence after the demo set.

### Story 6.1: Durable EF reprocess/result stores (#15)
Replace in-memory `IVerificationResultStore`/`IReprocessAuditRepository` (DI lines ~116–141 in
`VeriqanOrchestrationExtensions.cs`) with EF implementations + migration. AC: survives restart; tests green.

### Story 6.2: DB-enforced audit immutability ledger (#19)
Convert the Disposition/verdict audit to a tamper-evident ledger (trigger/temporal table/deny-grant),
not app-convention. AC: a DB-level mutation attempt is rejected; covered by an integration test.

### Story 6.3: Secrets / Key Vault (#21)
Move AES key + JWT signing key + SMTP creds out of plaintext config into a provider abstraction
(Key Vault/KMS); key-id + rotation. (Business-gated on infra choice — design + wire the abstraction.)

### Story 6.4: LFPDPPP retention / erasure (#22)
Design + implement retention TTL + ARCO erasure path for stored statements/PII. (Legal-gated — produce
the design + the data-model hooks.)

### Story 6.5: Operational runbook (#25)
Author the Worker ops runbook: batch start, exception-queue triage, reference-bundle update, mid-batch
resume, on-call escalation.

### Story 6.6: Fail-open/closed policy + circuit breaker (#26)
Define + implement the host→gate failure policy with a documented decision + a circuit breaker + entry
timeout.

### Story 6.7: Password-protected PDF handling (N1)
`PdfDocument.Open` currently has no password param → silent failure. AC: a password-protected input is
detected and surfaced (not silently dropped); optional password provider.

### Story 6.8: `ef migrations bundle` CLI (N4)
Add a migration-bundle entrypoint so CI runs migrations separately from host boot
(`VeriqanLegalBaselineStartupService` runs them at boot today).

---

## FR Coverage Map
| FR | Epic.Story |
|---|---|
| FR-T1/T2/T3 | 1.1, 1.2, 1.3, 1.4 |
| FR-R1 | 2.1, 2.2, 2.3 |
| FR-M1 | 3.1, 3.2 |
| FR-C1 | 4.1 |
| FR-C2 | 4.2 |
| FR-D1 | 5.1 |
| Tier B (#15/#19/#21/#22/#25/#26/N1/N4) | 6.1–6.8 |

## Suggested sequence for the implementing agent
Epic 1 (verdict model) → Epic 2 (report surfaces it) → Epic 5 (CL-21, small) → Epic 3 (enhanced
master, proves GREEN path) → Epic 4 (confidence/BLOCKED semantics) → Epic 6 (hardening, parallelizable).
Each story: build → `dotnet exec` the relevant test binary → commit. Honor §5c throughout.
