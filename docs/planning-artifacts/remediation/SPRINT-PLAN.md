# Remediation — Unified Sprint / Wave Plan

**Date:** 2026-06-18 · **Branch:** `Liv` · **Status:** PLAN-ONLY (approval gate)
**Mirrors:** `RC6-CROSS-CUTTING-PATH-TO-PRODUCTION.md` wave structure (W0→W6).
**Order:** **Prisma MVP first, then Veriqan VEC** (locked decision). Both subsystems share the W0→W6 wave skeleton; engineering waves are dependency-ordered, **no calendar dates** (per owner: dependency-ordered backlog, not a calendar timeline). Effort tags S (≤1d) / M (2–4d) / L (1–2wk) are per-story from the matrices.
**Detail:** `PRISMA-REMEDIATION-EPICS.md` · `VERIQAN-REMEDIATION-EPICS.md` (subagent briefs + ACs) · `GAP-COVERAGE-MATRIX.md` (no-orphan proof).

---

## 0. How to read this plan

- **Waves are dependency layers, not dates.** A wave's stories may run in parallel; a later wave assumes the earlier wave's outcome (e.g. W1 real-TCP proof assumes W0 Dockerfiles exist).
- **Prisma runs ahead of Veriqan.** Prisma's residual is deploy/ops/security on an E2E-proven pipeline; Veriqan must first become runnable at all. When capacity is shared, drain Prisma W0→W2 before Veriqan W0, but the two W0s are independent and may overlap if capacity allows.
- **Three non-engineering unlocks run as parallel tracks from day one** (W5/business). They gate W4 (corpus) and parts of W3/W6 — they are NOT sequenced after engineering. See §Critical Path.
- **Evidence bar = end-to-end.** Every story's DoD requires real-infra proof (Testcontainers SQL, real TCP, containerized compose, one real statement through the Worker). A green unit test is not readiness.

---

## 1. Recommended execution order — "what runs first when approved"

**Unified Wave 0 is the first thing to execute.** It makes each subsystem deployable and able to run one real input end-to-end in a container. Within W0, start with the Prisma deployable-composition epic (PRISMA-E1) — it is the shortest path to a `docker-compose up` of an already-proven pipeline — and the Veriqan crux blockers (VERIQAN-E1-S1/S4/S5: entry point + wire report/notify + persist), which are what make Veriqan run a statement at all.

**First-sprint pick (the unified Wave-0 critical few):**
1. PRISMA-E1-S1 (Dockerfiles, 4 hosts) — M
2. PRISMA-E1-S3 (config externalization sweep) — S
3. PRISMA-E1-S4 (Reconciliator + Web.UI real health probes) — S
4. VERIQAN-E1-S1 (POST /verify entry point) — S
5. VERIQAN-E1-S4 (wire orphaned report + notify stages) — S
6. VERIQAN-E1-S5 (persist JobVerdict + Findings) — M
7. VERIQAN-E1-S2 (appsettings + startup validation) — S

In parallel, open the three non-engineering unlock tracks (issue #17, ADR-010 P1 counsel, corpus acquisition).

---

## 2. Wave 0 — Minimum Deployable Composition (buildable-now)

**Goal:** `docker-compose up` stands up each subsystem; a Prisma document flows through all 3 processes against the simulator; a Veriqan statement POSTed to `/verify` traverses ingest→…→verdict→persist→report→notify.

| Subsystem | Stories | Effort rollup |
|-----------|---------|---------------|
| Prisma (PRISMA-E1) | S1 Dockerfiles · S2 auto-migration · S3 config sweep · S4 real health probes · S5 docker-compose.dev · S6 worker remote log sinks + Seq env-var · S7 wire dashboard metrics | 1×M + 6×S |
| Veriqan (VERIQAN-E1) | S1 POST /verify · S2 appsettings+validation · S3 Dockerfile+compose · S4 wire report/notify · S5 persist verdict+findings · S6 deployable bundle + authoring guide · S7 real health checks · S8 OTel+Serilog · S9 runbook · S10 min-extraction-coverage floor (U2) · S11 PDF-robustness (timeout/size/password/streaming: #58,#59,#61) | 2×M + 9×S |

**W0 exit gate:** both subsystems containerized + observable; one real input traverses each end-to-end; no silent in-memory fallback (Veriqan startup validation warns loudly on missing config).

---

## 3. Wave 1 — Cardinal-Rule + Correctness Fixes + Operational Readiness (buildable-now)

**Goal:** eliminate Veriqan's latent false-block/false-pass risks on real input classes; close Prisma operational-readiness gaps.

| Subsystem | Stories | Effort rollup |
|-----------|---------|---------------|
| Veriqan cardinal/correctness (VERIQAN-E2) | S1 text-density abstain (scanned→BLOCKED) · S2 §20 sign guard · S3 §16 column guard · S4 FR-12 pHash rule · S5 CL-35 Aptos · S6 CL-34 card-in-image · S7 CL-31 pagination · S8 TimeProvider · S9 timezone · S10 es-MX number format · S11 CL-48 blank-page · S12 VerdictAggregator default arm · S13 IVA→config · S14 marked-PDF rotate/CropBox · S15 duplicate-alert guard · S16 dedup concurrency + logical-identity key | ~3×M + 13×S |
| Veriqan cosmetics (VERIQAN-COSMETIC) | S1 VerdictSignal doc · S2 §6 rule doc | 2×S |
| Prisma operational (PRISMA-E2) | S1 real-TCP 3-process proof · S2 dashboard-metrics wiring · S3 Sentinel trace+classify · S4 Tesseract deadlock fix · S5 ProcessId audit adoption · S6 chaos/failure-mode suite · S7 SIARA session soak · S8 CI image build+publish · S9 ProcessingMetrics persistence decision | ~5×M + 4×S |
| Prisma quality/cleanup (PRISMA-E5) | S1 dead-code (C1/C3) · S2 SignalREventBroadcaster decision · S3 CLAUDE.md doc edit (C4) · S4 Mexican-holiday calendar · S5 DOCX regex hardening | mixed S/M |

**Note (§20/§16 — owner decision):** VERIQAN-E2-S2/S3 build the abstain/column-count guard now; the **corpus-verify acceptance gate stays OPEN** and closes in W4 (VERIQAN-E5-S2/S3) when a real §20/§16 specimen lands. Assumes corpus acquisition is being driven in parallel.

**W1 exit gate:** no known Veriqan cardinal-rule false-RED on scanned/Aptos/card-in-image/pagination inputs; Prisma 3-process proven over real TCP; Sentinel classified; runbooks exist; CI publishes images.

---

## 4. Wave 2 — Persistence Durability + Audit Immutability (buildable-now)

**Goal:** verdicts, findings, identities, and audit records are durable, tamper-evident, and survive restart.

| Subsystem | Stories | Effort rollup |
|-----------|---------|---------------|
| Veriqan (VERIQAN-E3) | S1 EfVerificationResultStore + EfReprocessAuditRepository · S2 Disposition ledger/DENY-grant · S3 BatchExceptionLog durable dead-letter · S4 stamp EngineVersion+BundleVersion · S5 `ef migrations bundle` CI entrypoint | ~3×M + 2×S |
| Prisma (PRISMA-E4 + PRISMA-E3-S1) | E3-S1 AuditRecords ledger/DENY-grant + HMAC chain · E4-S1 PersonIdentityResolver persistence | 2×M |

**W2 exit gate:** both audit trails immutable (SQL 2022 ledger or DENY grant + row HMAC); Veriqan persistence durable across process restart; durable dead-letter queue.

---

## 5. Wave 3 — Security Hardening (FULLY SPECCED; engineering-now + non-eng-gated unlocks)

**Goal:** both subsystems hardened for a load-bearing regulated pipeline. Per owner decision, security is **fully specced** here (not stubbed) — each story carries complete engineering ACs. Where the unlock is non-engineering, the story is BLOCKED on the named unlock. The separate deep audit (A1–A6 / CNBV CUB / ISO 27001 / SOC 2) consumes this backlog; do not re-spec the audit itself.

| Subsystem | Buildable-now stories | Non-eng-gated stories (BLOCKED) |
|-----------|----------------------|----------------------------------|
| Veriqan (VERIQAN-E4) | S1 authn/authz · S2 TLS+SMTP-TLS · S7 bundle SHA-256 signature | S3 AES-GCM+key-id+KeyVault (biz) · S4 secrets manager (biz) · S5 LFPDPPP retention/erasure (biz-legal) · S6 fail-open/closed + circuit-breaker (biz-policy) · S8 CNBV CUB (biz-legal) · S9 PDF-parse process isolation (security-review) |
| Prisma (PRISMA-E3) | S1 audit ledger (in W2) · S9 first-phase compliance gap-assessment (after counsel) | S2 per-process asymmetric JWT + rotation (biz) · S3 field-level AES-GCM PII (biz) · S4 vault-backed SIARA creds (ops) · S6 alerting (ops) · S7 network segmentation/mTLS (biz) · S8 audit fail-closed mode (biz-policy) |

**W3 exit gate (engineering portion):** Veriqan Worker authenticated + TLS-enforced + bundle-signature-verified; Prisma compliance gap-assessment started. Gated items unblock as their non-eng unlock lands (Key Vault provisioning, policy decisions, counsel).

---

## 6. Wave 4 — Corpus Calibration + Real-Data Validation (CORPUS-GATED)

Unblockable only when the relevant corpus lands (CPA-3 / CPA-2). All BLOCKED until then; engineering abstain-guards already shipped in W1.

| Subsystem | Stories |
|-----------|---------|
| Veriqan (VERIQAN-E5) | S1 NFR-1 p95 on real PDFs · S2 §20 sign corpus-verify (closes #2 gate) · S3 §16 column-map calibrate (closes #3 gate) · S4 tolerance calibration + legal sign-off · S5 §6/§8/§19 calibration + latency · S6 measured confidence + ConfidenceGuard recalibration · S7 dynamic layout/geometry · S8 advertising detection + restore §12 700-char · S9 CL-33 real logo (pHash) · S10 bold glyph inference · calibration-harness FPR/DR run |
| Prisma (PRISMA-GATED) | S2 quality-model retrain + provenance (`TrainedDate`) · S3 SIRO XSD conformance (Banamex `.xsd`) · S4 volume soak p95 (also legal-gated) |

**W4 exit gate:** measured thresholds replace estimated/spec-derived ones; calibration-harness FPR/DR non-vacuous; accuracy claims substantiable.

---

## 7. Wave 5 — Business / Legal / Corpus Unlocks (PARALLEL non-engineering tracks)

These run **concurrently with W0–W4 from day one** — they are not sequenced after engineering. Engineering W4 + parts of W3/W6 gate on their outcomes.

| Track | Gate | Owner-action |
|-------|------|--------------|
| E13 buyer gate | CPA-1 | Drive GitHub issue #17 to a decision. Positive → commission W6 Veriqan productization. Negative/delayed → Veriqan stays single-tenant PoC. |
| Live-SIARA legal gate | CPA-2 | Counsel sign-off on ADR-010 P1; record authorization artifact; flip `Siara:AllowProductionHost=true`. |
| Veriqan corpus acquisition | CPA-3a | Identify bank-relationship owner; NDA; acquire labelled KnownGood + KnownBroken CONDUSEF statements; load into calibration harness. |
| Prisma corpus acquisition | CPA-3b | Acquire labelled CNBV/Banamex samples; validate quality model; obtain Banamex SIRO `.xsd`. |
| Key Vault / HSM provisioning | ops | Provision Azure Key Vault (or equivalent); allocate slots for Veriqan AES-GCM, Prisma JWT key-pairs, SIARA credentials. |
| Observability + alerting infra | ops | Provision Seq/Prometheus/Grafana in staging; verify metric ingestion; author SLA alert rules. |

---

## 8. Wave 6 — E13 Productization (E13-GATED; Veriqan only)

Unblockable until CPA-1 (issue #17) clears. Single-tenant engine stays viable as a PoC without it.

| Story | Description |
|-------|-------------|
| VERIQAN-E5-S11 | Runtime tenant selection; config-only profile loading; embeddable gate SDK/HTTP API; bundle `toleranceConfig` overlay (#35, #49) |
| VERIQAN-E5-S12 | Traceability-matrix export; `AcuerdoVersion` on rules + JobVerdict; Acuerdo-edition governance (#47-AcuerdoVersion, #54) |

---

## 9. Critical Path

```
[parallel non-eng tracks ──────────────────────────────────────────────► always on]
  issue #17 ........................................► gates W6 (Veriqan productization)
  ADR-010 P1 counsel ...............................► gates PRISMA-GATED-S1/S4 (live ingestion)
  corpus acquisition (×2) ..........................► gates W4 (both) + §20/§16 verify gates

[engineering spine]
  W0 deployable ──► W1 cardinal+ops ──► W2 persistence/audit ──► W3 security(eng) ──► W4 corpus-calibration*
                                                                                         (*needs corpus track)
```

**The binding constraints are the three non-engineering unlocks, not the engineering.** Engineering W0→W3 can complete on the simulator/synthetic fixtures without any unlock. Beyond that:
- Prisma cannot reach *live* production without the **legal gate** (no amount of engineering substitutes).
- Veriqan cannot make a calibrated accuracy claim or close the §20/§16 cardinal gates without the **corpus**.
- Veriqan productization (multi-tenant) cannot start without the **E13 buyer gate**.

**Recommendation:** open all three non-engineering tracks at approval time, in parallel with executing unified Wave 0. They have the longest lead times and the least engineering leverage.

---

## 10. Effort Rollup (indicative, dependency-ordered — no dates)

| Wave | Prisma | Veriqan | Notes |
|------|--------|---------|-------|
| W0 | ~1 sprint (1M+6S) | ~1–2 sprints (2M+9S) | both buildable-now; can overlap |
| W1 | ~2 sprints (mixed) | ~2 sprints (3M+13S+2S) | Veriqan cardinal fixes are the priority |
| W2 | ~1 sprint (2M) | ~1 sprint (3M+2S) | |
| W3 (eng portion) | ~1 sprint eng + gated tail | ~1 sprint eng + gated tail | non-eng unlocks run longer, parallel |
| W4 | corpus-gated | corpus-gated | starts when corpus lands |
| W6 | — | E13-gated | starts when issue #17 clears |

RC6's reference figure was ~6–8 weeks (single dev) for Prisma's engineering tail; Veriqan's is larger (it starts from not-runnable). With the orchestrator+subagent model, W0–W2 buildable-now stories parallelize well across both subsystems.

---

*Sprint plan complete. Plan-only; no production code modified. Mirrors RC6 W0→W6. Owner approval gate next.*
