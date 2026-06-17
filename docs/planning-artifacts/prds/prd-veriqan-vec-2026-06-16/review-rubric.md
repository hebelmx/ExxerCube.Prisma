# PRD Quality Review — Veriqan VEC (Bank Statement Quality Verifier)

## Overall verdict

This is a strong, launch-grade PRD for a regulated brownfield product: it has a genuine thesis (deterministic-first, assist-not-replace, additive module), FRs whose testable consequences trace to named CL items, honest scope partitioning between v1 and v2, and counter-metrics that explicitly guard against gaming the primary metrics. The biggest risk is not the PRD's structure but its *foundations being unconfirmed* — sample size, SLA window, reference-data delivery, and PDF nature are all open, and several quantitative claims (volume, savings, retention) are admitted to be soft or inconsistent. Done-ness is excellent at the FR level; the residual softness is concentrated in a few cross-cutting NFR adjectives ("in seconds") and the dependence of multiple checks on reference/catalog data whose sourcing is still an Open Question.

## Decision-readiness — strong

The PRD makes its decisions as decisions, not as hedges. The central bet — "By starting with deterministic checks … VEC delivers reliable value in weeks rather than waiting on fragile ML" (§1) — is stated plainly and then carried consistently through scope (§6.2 defers CLIP/LayoutLMv3), risks (R3), and metrics (SM-3 gates ML on a measured accuracy bar). Trade-offs name what was given up: §6.2 calls catalog-order/CLIP matching "the visually 'smart' differentiator" and explicitly parks it; the v1 image path is downgraded to presence-only with the cost (no order/identity matching) made visible in FR-12.

The Open Questions in §8 are genuinely open and consequential (sample size, reference-data delivery mechanism, PDF nature), not rhetorical. `[NOTE FOR PM]` callouts sit at real tensions — PCI-DSS scope for storing the PAN (§11), the internally-inconsistent savings figures (§16), and the OCR primary-case caveat (FR-5 notes). These are the places a reviewer would actually push, and the PRD meets them rather than smoothing them.

### Findings
- **medium** Several FRs silently depend on unresolved OQs (§8) — FR-12 presence checks and FR-7 cross-period checks both require reference/catalog data whose sourcing is OQ-2/OQ-3, and the whole throughput design rests on OQ-1. The PRD acknowledges each OQ but does not flag that a "no" or "messy" answer materially de-scopes v1 (R1 covers data *quality* degradation, not data *absence at v1 boundary*). *Fix:* add a one-line dependency note on FR-12/FR-7 ("depends on OQ-3/OQ-2 resolution; if unmet, this FR moves to v1.1"), so downstream epic planning knows these are conditional.

## Substance over theater — strong

Content is earned. The JTBD list (§2.1) is four roles and each one drives a distinct part of the spec — Analyst → FR-15…18 + UJ-1; Compliance Owner → §11/§14 + UJ-2; Operations Lead → FR-21/22 + SM-4; internal Eng → FR-23/24 + UJ-3. No persona is decorative. The differentiation claim is honest: the PRD does not pretend deterministic arithmetic is novel; it explicitly labels the *deferred* CLIP work as "the visually 'smart' differentiator" (§6.2) and keeps v1 framed as reliable plumbing, not innovation.

NFRs (§10) mostly avoid boilerplate — NFR-2 cites concrete numbers (≥99% precision/recall, FP < 1%) tied to SM-C1/C2, NFR-5 names reproducibility as audit-critical with a rationale. The Vision (§1) is product-specific (Mexican banks, 13–20M monthly statements, 2–4 hr manual cost, the 55-point checklist) and could not be swapped into another PRD.

### Findings
- **low** NFR-1 "completes in seconds" is the one adjective-shaped NFR (see Done-ness). *Fix:* bound it.

## Strategic coherence — strong

There is a clear thesis and the features follow from it rather than from a backlog. The arc is: anchor to the 55-item Excel as the single source of truth → reuse 50–60% of proven Shared Core → ship deterministic checks first → keep a human in the loop → defer ML behind an accuracy gate. Feature ordering reflects this: the "deterministic heart" (§4.3 Financial Consistency) is the full-scope centerpiece, while the genuinely hard visual ML is the *only* thing pushed to v2. That is thesis-driven prioritization, not easy-first.

Success Metrics validate the thesis rather than measuring activity: SM-1 (coverage of the checklist), SM-3 (accuracy on the deterministic checks the thesis bets on), SM-5 (zero Solution 1 regressions — the brownfield promise). Counter-metrics are present and pointed: SM-C1 "do not chase coverage (SM-1) by emitting speculative fails" and SM-C2 "silent passes … must be 0" directly counterbalance the primary metrics. MVP scope kind is coherently "problem-solving" (automate a manual quality gate), and the scope logic matches.

## Done-ness clarity — strong (with isolated soft spots)

This is the dimension the PRD invests most in, and it largely earns it. Every FR carries a **Consequences (testable)** block, and most consequences are genuinely verifiable with a concrete threshold and a CL anchor — e.g. FR-6: "deviation > $0.50 MXN ⇒ FAIL (CL-21)"; FR-1 idempotency "the same file content (by hash) does not create duplicate jobs"; FR-23 "an architecture test fails the build on a Prisma → Veriqan reference." An engineer can write a story and a test directly from these.

The soft spots are few but real and worth nailing before this feeds epics/stories:

### Findings
- **medium** NFR-1 throughput is unbounded prose — "completes in seconds" and "completes within the review window" (§10). "Seconds" has no upper bound and the window itself is OQ-1. *Fix:* give a target ceiling (e.g. "p95 ≤ N seconds/statement on a text-layer PDF") even if provisional, marked `[ASSUMPTION]` pending OQ-1.
- **medium** FR-10 leans on "a configured threshold" for overlap (CL-28) and "bold + uppercase" for headers (CL-29) without stating where the threshold/default lives or what counts as a header region. The Reference Bundle carries `toleranceConfig` for numeric checks (FR-8) but the visual thresholds are not obviously sourced. *Fix:* state that geometry thresholds are config-driven (and where), parallel to FR-8.
- **low** FR-3 "including aliases" and FR-4 address "split into components" imply normalization rules that are undefined here. Acceptable to defer to extraction spec, but a downstream story author will need the alias/component source. *Fix:* point to the Reference Bundle `product catalog` as the alias source explicitly.
- **low** FR-16 "highlighted at its locator" assumes every Finding has a usable page/region locator; FR-6/FR-7 arithmetic findings may be statement-level, not region-anchored. *Fix:* clarify what a Marked-PDF highlight does for a non-spatial arithmetic FAIL (e.g. anchor to the summary table region).

## Scope honesty — strong

Omissions are explicit, not inferred. §5 Non-Goals does real work (no statement generation, no auto-reject, no non-credit-card families, no writable dependency back to Solution 1, not fraud detection) and §6.2 restates the v2 deferrals with rationale. Inline `[NON-GOAL for MVP]` tags FR-12's v2 path at the point of use. `[ASSUMPTION: …]` tags sit on real inferences (credit-card-only, fiscal-block presence, GPU-free v1) and every inline assumption round-trips into the §9 Assumptions Index. `[NOTE FOR PM]` callouts mark the deferred/contested decisions (PCI scope, savings figures, support tier, OCR primary case).

Open-items density is appropriate for the stakes: 7 Open Questions + ~5 indexed assumptions + ~5 PM notes on a launch-grade, unconfirmed-foundations PRD is reasonable, not a blocker — and critically, the PRD does not pretend the OQs are resolved. The §16 admission that cited savings figures are "internally inconsistent; restate … before quoting" is exemplary scope honesty.

### Findings
- **low** §9 Assumptions Index entries are keyed to sections but a couple of inline `[ASSUMPTION]`s use OQ-style phrasing ("confirm volume/window per OQ-1" in NFR-1) that blurs assumption vs. open question. *Fix:* keep `[ASSUMPTION]` for what the PRD is *proceeding on* and route "confirm X" items to §8; NFR-1's volume note is really an OQ pointer.

## Downstream usability — strong

This PRD is explicitly chain-top (it names `bmad-create-architecture` and `bmad-create-epics-and-stories` as consumers in §0), so traceability matters most here, and it holds up well. The Glossary (§3) is present and the domain nouns — Finding, Verdict, Reference Bundle, Disposition, Tolerance Band, CL-N — are used consistently across FRs, UJs, NFRs, and SM definitions. FR IDs are contiguous (FR-1…FR-24) and unique; SM IDs (SM-1…5, SM-C1/C2) and UJ IDs (UJ-1…3) are clean. Cross-references resolve: FR consequences cite CL items, SMs cite the FRs they validate, risks cite FRs/SMs/OQs.

Sections are mostly self-contained — each FR carries its CL anchor and consequences inline rather than "see above." The addendum cleanly separates mechanism (libraries, salvage list, namespaces) from capability, so the PRD stays at the right altitude for architecture intake.

### Findings
- **medium** CL coverage is asserted (55 items, CL-1…CL-55) but the FR set references a subset and the PRD provides no CL→FR coverage map. Some CL numbers never appear (e.g. CL-11…CL-14, CL-22, CL-23, CL-37, CL-38, CL-42…CL-45, CL-49 are not cited inline; CL-12 etc.). SM-1 targets "≥ 90% evaluated" which tacitly admits not all 55 are covered, but the gap is not enumerated. For a checklist-anchored regulated product feeding story creation, the missing-CL set is load-bearing. *Fix:* add a CL→FR traceability table (or appendix) marking each CL as v1-covered / v2 / out-of-scope; this is the single highest-value addition for downstream epics.
- **low** UJ-3 protagonist "Sofía (internal eng)" carries less inline context than UJ-1's Lucía (no entry state / climax / resolution structure). Acceptable for a non-UX journey, but slightly asymmetric. *Fix:* fine as-is given shape; no change required.

## Shape fit — strong

The shape matches the product. This is a multi-stakeholder, regulated brownfield product, and the PRD uses exactly the instruments that shape calls for: named-protagonist UJs where there is real human workflow (UJ-1 Lucía, UJ-2 Marco), a capability-spec register for the internal/operational FRs, constraint traceability for the regulated parts (§11 Compliance, §14 Audit Trail, CL anchoring throughout), and explicit brownfield discipline (FR-23/24, §4.9, addendum F). New capability vs. reused existing code is clearly distinguished — the Shared Core reuse is mapped in addendum A and the existing PRP2 scaffolding is explicitly flagged as "an untrusted prototype (~5–15% real)" (FR-24 notes / addendum D), which is precisely the brownfield accuracy the rubric demands.

It is neither over- nor under-formalized: it does not invent UJs for the batch/throughput plumbing, and it does not omit them for the human-facing QA flow. No findings.

## Mechanical notes

- **Glossary drift:** minimal. Verdict labels (`GREEN`/`RED`/`BLOCKED`) and Finding verdicts (`PASS`/`FAIL`/`INSUFFICIENT_DATA`) are used consistently. Minor: "Marked PDF" (§3) vs. "color-marked PDF" (§1, FR-16 heading) vs. "Marked PDF" — same concept, slight label variation; harmless.
- **ID continuity:** FR-1…FR-24, SM-1…5 + SM-C1/C2, UJ-1…3, NFR-1…6, R1…R6 all contiguous and unique. Cross-references (SM→FR, R→FR/SM/OQ) resolve. "decision #5" (FR-24 notes, R2) and "decision #5" reference an external decision log not included here — verify that log exists; it is the one dangling cross-ref.
- **CL continuity:** CL-1…CL-55 asserted as the source-of-truth set; inline citations are a partial subset (see Downstream finding). No CL→FR map. Recommend the traceability table as the top mechanical fix.
- **Assumptions Index roundtrip:** the §9 entries map to inline `[ASSUMPTION]` tags in §2.1/§6, §4.2, §4.5, §10, §4.9 — roundtrip holds. One inline assumption (§13 residency, §12 GPU) appears inline but is thin in the index; minor.
- **`[NOTE FOR PM]` placement:** all five sit at genuine tensions (FR-5 OCR, FR-24 prototype trust, §11 PCI, §12 support tier, §16 savings). Good.
- **Required sections:** all present for a launch-grade regulated brownfield PRD — Vision, Users/JTBD, Glossary, Features/FRs, Non-Goals, MVP Scope, Success Metrics + counter-metrics, Open Questions, Assumptions Index, NFRs, Compliance, Operational, Data Governance, Audit Trail, Risks, ROI. Comprehensive.
