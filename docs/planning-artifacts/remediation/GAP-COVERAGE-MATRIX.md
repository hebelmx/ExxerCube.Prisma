# Remediation — Gap Coverage Matrix

**Date:** 2026-06-18 · **Branch:** `Liv` · **Status:** PLAN-ONLY (approval gate)
**Purpose:** prove every readiness-challenge gap maps to ≥1 remediation story (no orphan Blocks gaps). This is the anti-drift gate for the approval decision.
**Sources:** `RC5-PRISMA-READINESS-MATRIX.md` (44 gaps) · `RC4-VERIQAN-READINESS-MATRIX.md` (64 gaps).
**Detail lives in:** `PRISMA-REMEDIATION-EPICS.md` · `VERIQAN-REMEDIATION-EPICS.md` (per-story subagent briefs + ACs).

---

## 1. Coverage Headline

| Subsystem | Blocks | Degrades | Cosmetic | Total | Orphan Blocks |
|-----------|--------|----------|----------|-------|---------------|
| Prisma MVP | 16 (P1–P16) | 24 (D1–D24) | 4 (C1–C4) | 44 | **0** |
| Veriqan VEC | 27 open (#1–#28, #16 RESOLVED) | 33 (#29–#61) | 3 (#62–#64) | 63 open | **0** |
| **Total open** | **43** | **57** | **7** | **107** | **0** |

**Every Blocks-production gap maps to at least one story. Zero orphans.** Gated gaps are present as BLOCKED stories with the unlock named (they are *covered*, not built). Veriqan #16 is RESOLVED (commit `d0d9ef65`) and needs no story.

Buildable-now vs gated split of the **Blocks** set:
- Prisma: 11 buildable-now (P1–P6, P9, P10, P12, P15, P16) · 5 gated (P7 legal, P8 ops, P11 ops, P13 biz, P14 biz).
- Veriqan: 22 buildable-now (incl. §20/§16 guard halves) · 5 gated (#18 biz, #21 biz, #22 biz, #26 biz, #27 corpus).

---

## 2. Prisma — Blocks (P1–P16)

| Gap | Story | Gate / Status |
|-----|-------|---------------|
| P1 — no Dockerfiles (4 hosts) | PRISMA-E1-S1 | buildable-now |
| P2 — no auto-migration runner | PRISMA-E1-S2 | buildable-now |
| P3 — Web.UI hardcodes DESKTOP-FB2ES22/(localdb) | PRISMA-E1-S3 | buildable-now |
| P4 — SIARA URL hardcoded localhost:5002 | PRISMA-E1-S3 | buildable-now |
| P5 — Windows log path in simulator prod config | PRISMA-E1-S3 | buildable-now |
| P6 — real-TCP cross-process never proven w/ pipeline | PRISMA-E2-S1 | buildable-now |
| P7 — live SIARA legal gate | PRISMA-GATED-S1 | **BLOCKED — legal-gated (P1 counsel)** |
| P8 — SIARA credential vault not wired | PRISMA-E3-S4 | **BLOCKED — ops-gated (Key Vault)** |
| P9 — Reconciliator health probe stub | PRISMA-E1-S4 | buildable-now |
| P10 — Web.UI health probe stub | PRISMA-E1-S4 | buildable-now |
| P11 — no operational alerting | PRISMA-E3-S6 | **BLOCKED — ops-gated (alerting procurement)** |
| P12 — worker dashboard metrics zeros | PRISMA-E2-S2 | buildable-now |
| P13 — shared HMAC JWT secret (per-process keys) | PRISMA-E3-S2 | **BLOCKED — business-gated (key provisioning)** |
| P14 — no field-level PII encryption-at-rest | PRISMA-E3-S3 | **BLOCKED — business-gated (data classification + KV)** |
| P15 — audit trail not immutable | PRISMA-E3-S1 | buildable-now |
| P16 — Sentinel service black box | PRISMA-E2-S3 | buildable-now (trace-then-classify) |

## 3. Prisma — Degrades (D1–D24)

| Gap | Story | Gate / Status |
|-----|-------|---------------|
| D1 quality-model corpus calibration | PRISMA-GATED-S2 | BLOCKED — corpus-gated |
| D2 DOCX extractor regex hardening | PRISMA-E5-S5 | buildable-now |
| D3 semantic/NLP classification | PRISMA-E5-S6 | BLOCKED — business-gated (ML/NLP decision) |
| D4 SIRO XSD validation | PRISMA-GATED-S3 | BLOCKED — business-gated (Banamex `.xsd`) |
| D5 Mexican-holiday calendar | PRISMA-E5-S4 | buildable-now |
| D6 PersonIdentityResolver persistence | PRISMA-E4-S1 | buildable-now |
| D7 Seq endpoint hardcoded | PRISMA-E1-S6 | buildable-now |
| D8 worker logs console-only | PRISMA-E1-S6 | buildable-now |
| D9 no docker-compose full stack | PRISMA-E1-S5 | buildable-now |
| D10 in-memory SignalR transport / reconnect | PRISMA-E2-S1 (reconnect sub-scenario) | buildable-now |
| D11 SIARA session longevity unproven | PRISMA-E2-S7 | buildable-now |
| D12 worker dashboard zeros (counter wiring) | PRISMA-E1-S7 | buildable-now |
| D13 ProcessingMetrics in-memory only | PRISMA-E2-S9 | buildable-now |
| D14 observability backend unverified | PRISMA-E3-S6 | BLOCKED — ops-gated |
| D15 no mTLS between processes | PRISMA-E3-S7 | BLOCKED — business-gated (network topology) |
| D16 audit fail-open | PRISMA-E3-S8 | BLOCKED — business-gated (audit policy) |
| D17 no JWT secret rotation | PRISMA-E3-S2 (rotation grace period) | BLOCKED — business-gated (key provisioning) |
| D18 CNBV CUB obligations undocumented | PRISMA-E3-S9 | BLOCKED — business-gated (legal counsel) |
| D19 E2E latency under volume uncharacterized | PRISMA-GATED-S4 | BLOCKED — legal+corpus-gated |
| D20 no chaos/failure-mode tests | PRISMA-E2-S6 | buildable-now |
| D21 Tesseract second-init deadlock | PRISMA-E2-S4 | buildable-now |
| D22 ProcessId audit adoption incomplete | PRISMA-E2-S5 | buildable-now |
| D23 ISO 27001 / SOC 2 trajectory undefined | PRISMA-E3-S9 | BLOCKED — business-gated (executive decision) |
| D24 no CI deploy pipeline | PRISMA-E2-S8 | buildable-now |

## 4. Prisma — Cosmetic (C1–C4)

| Gap | Story | Status |
|-----|-------|--------|
| C1 EfCoreIdentityAdapter unregistered (add comment) | PRISMA-E5-S1 | buildable-now |
| C2 SignalREventBroadcaster commented out | PRISMA-E5-S2 | buildable-now |
| C3 legacy ProcessingOrchestrator ROP dead code | PRISMA-E5-S1 | buildable-now |
| C4 Counter/Weather already removed | PRISMA-E5-S3 | **RESOLVED** — residual is CLAUDE.md doc edit only |

---

## 5. Veriqan — Blocks (#1–#28; #16 RESOLVED)

| Gap | Story | Gate / Status |
|-----|-------|---------------|
| #1 scanned PDF false-RED (cardinal) | VERIQAN-E2-S1 | buildable-now |
| #2 §20 saldo-a-favor sign (cardinal) | VERIQAN-E2-S2 (guard) + VERIQAN-E5-S2 (verify) | guard buildable-now; **verify corpus-gated** |
| #3 §16 column-map (cardinal) | VERIQAN-E2-S3 (guard) + VERIQAN-E5-S3 (verify) | guard buildable-now; **verify corpus-gated** |
| #4 FR-12 pHash catalog-image missing | VERIQAN-E2-S4 | buildable-now |
| #5 CL-35 Aptos font variants (cardinal) | VERIQAN-E2-S5 | buildable-now |
| #6 CL-34 card-in-image (cardinal) | VERIQAN-E2-S6 | buildable-now |
| #7 CL-31 pagination first-match (cardinal) | VERIQAN-E2-S7 | buildable-now |
| #8 no ingestion entry point | VERIQAN-E1-S1 | buildable-now |
| #9 report/notify stages orphaned | VERIQAN-E1-S4 | buildable-now |
| #10 no appsettings.json | VERIQAN-E1-S2 | buildable-now |
| #11 no Dockerfile | VERIQAN-E1-S3 | buildable-now |
| #12 no real ingestion path | VERIQAN-E1-S1 | buildable-now |
| #13 no real reference-bundle provenance | VERIQAN-E1-S6 | buildable-now (eng) / business-gated (bank data) |
| #14 verdict/findings never persisted | VERIQAN-E1-S5 | buildable-now |
| #15 resume-state/reprocess-audit in-memory | VERIQAN-E3-S1 | buildable-now |
| #16 2 persistence tests RED | — | **RESOLVED (commit d0d9ef65)** |
| #17 no authn/authz on Worker | VERIQAN-E4-S1 | buildable-now |
| #18 AES-CBC / no key management | VERIQAN-E4-S3 | **BLOCKED — business-gated (Key Vault)** |
| #19 audit immutability not enforced | VERIQAN-E3-S2 | buildable-now |
| #20 no TLS | VERIQAN-E4-S2 | buildable-now |
| #21 no secrets management | VERIQAN-E4-S4 | **BLOCKED — business-gated (vault procurement)** |
| #22 LFPDPPP retention/erasure absent | VERIQAN-E4-S5 | **BLOCKED — business-gated (legal/privacy policy)** |
| #23 health probes are stubs | VERIQAN-E1-S7 | buildable-now |
| #24 no OTel backend | VERIQAN-E1-S8 | buildable-now |
| #25 no runbook | VERIQAN-E1-S9 | buildable-now |
| #26 fail-open/closed policy unowned | VERIQAN-E4-S6 | **BLOCKED — business-gated (stakeholder policy)** |
| #27 no latency characterization | VERIQAN-E5-S1 | **BLOCKED — corpus-gated** |
| #28 missing reference bundle → BLOCKED | VERIQAN-E1-S6 | buildable-now (eng) / business-gated (bank data) |

## 6. Veriqan — Degrades (#29–#61) + Cosmetic (#62–#64) + U2

| Gap | Story | Gate / Status |
|-----|-------|---------------|
| #29 tolerance defaults ESTIMATED | VERIQAN-E5-S4 | BLOCKED — corpus-gated |
| #30 §6 recursion never run on real input | VERIQAN-E5-S5 | BLOCKED — corpus-gated |
| #31 §8 coherence check omitted | VERIQAN-E5-S5 | BLOCKED — corpus-gated |
| #32 confidence 3-value constant | VERIQAN-E5-S6 | BLOCKED — corpus-gated |
| #33 geometry hardcoded to fixture #1 | VERIQAN-E5-S7 | BLOCKED — corpus-gated |
| #34 advertising 7-phrase allowlist | VERIQAN-E5-S8 | BLOCKED — corpus-gated |
| #35 toleranceConfig override not wired | VERIQAN-E5-S11 | BLOCKED — E13-gated |
| #36 NFR-5 ambient-clock non-determinism | VERIQAN-E2-S8 | buildable-now |
| #37 timezone undefined | VERIQAN-E2-S9 | buildable-now |
| #38 European number-format mis-parse | VERIQAN-E2-S10 | buildable-now |
| #39 bold detection font-name substring | VERIQAN-E5-S10 | BLOCKED — corpus-gated |
| #40 CL-33 logo image-count proxy | VERIQAN-E5-S9 | BLOCKED — corpus-gated (+ pHash infra E2-S4) |
| #41 CL-48 blank-page invisible-glyph | VERIQAN-E2-S11 | buildable-now |
| #42 VerdictAggregator default→passCount | VERIQAN-E2-S12 | buildable-now |
| #43 §19 rate normalisation thresholds | VERIQAN-E5-S5 | BLOCKED — corpus-gated |
| #44 IVA hard-coded 16% | VERIQAN-E2-S13 | buildable-now |
| #45 FR-16 marked-PDF Y-flip rotated pages | VERIQAN-E2-S14 | buildable-now |
| #46 FR-17 duplicate alert on retry | VERIQAN-E2-S15 | buildable-now |
| #47 AR-9 verdict provenance | VERIQAN-E3-S4 (versions) + VERIQAN-E5-S12 (AcuerdoVersion) | versions buildable-now; AcuerdoVersion E13-gated |
| #48 dedup concurrency-unsafe | VERIQAN-E2-S16 | buildable-now |
| #49 E13 product half missing | VERIQAN-E5-S11 | BLOCKED — E13-gated |
| #50 exception queue in-memory | VERIQAN-E3-S3 | buildable-now |
| #51 PDF-parse + key same process | VERIQAN-E4-S9 | BLOCKED — business-gated (security review) |
| #52 no CNBV CUB alignment | VERIQAN-E4-S8 | BLOCKED — business-gated (legal counsel) |
| #53 bundle authenticity untrusted | VERIQAN-E4-S7 | buildable-now |
| #54 rule-version ↔ Acuerdo governance | VERIQAN-E5-S12 | BLOCKED — E13-gated |
| #55 StatementContextKey schema undocumented | VERIQAN-E1-S6 | buildable-now |
| #56 no EF migration CI/CD entrypoint | VERIQAN-E3-S5 | buildable-now |
| #57 §6 moat latency unknown | VERIQAN-E5-S5 | BLOCKED — corpus-gated |
| #58 BatchProcessor loads all tasks | VERIQAN-E1-S11 | buildable-now |
| #59 password-protected PDF silent fail | VERIQAN-E1-S11 | buildable-now |
| #60 idempotency key raw byte-hash | VERIQAN-E2-S16 | buildable-now |
| #61 poison-PDF DoS no parse timeout | VERIQAN-E1-S11 | buildable-now |
| #62 VerdictSignal.Blocked XML doc | VERIQAN-COSMETIC-S1 | buildable-now |
| #63 §6 rule stale doc-comment | VERIQAN-COSMETIC-S2 | buildable-now |
| #64 MinFieldConfidence 0.8 untuned | VERIQAN-E5-S6 | BLOCKED — corpus-gated |
| U2 no min-extraction-coverage floor (false-PASS dual) | VERIQAN-E1-S10 | buildable-now |

---

## 7. Deferral Register (gaps with no buildable-now engineering this run)

Every item below is **covered by a BLOCKED story** — none is dropped. Listed so the dependency is explicit.

| Gap(s) | Subsystem | Gate | Unlock owner |
|--------|-----------|------|--------------|
| P7, D19 | Prisma | legal-gated | Counsel: ADR-010 P1 sign-off (`Siara:AllowProductionHost=true`) |
| D1, D4 (partial), D19 | Prisma | corpus-gated | Business: CNBV/Banamex corpus + Banamex SIRO `.xsd` |
| P8, P11, P13, P14, D14, D15, D16, D17, D18, D23 | Prisma | business/ops-gated | Key Vault provisioning · alerting procurement · data-classification · audit-write policy · network topology · counsel · executive cert decision |
| D3 | Prisma | business-gated | Owner: ML/NLP capability decision |
| #18, #21, #22, #26, #51, #52 | Veriqan | business-gated | Key Vault · vault procurement · LFPDPPP legal policy · fail-open/closed stakeholder decision · security-review process-split · CNBV CUB counsel |
| #2-verify, #3-verify, #27, #29–#34, #39, #40, #43, #57, #64 | Veriqan | corpus-gated | Business: real CONDUSEF KnownGood+KnownBroken corpus (CPA-2) |
| #35, #47-AcuerdoVersion, #49, #54 | Veriqan | E13-gated | Product: GitHub issue #17 buyer discovery |

**Three non-engineering critical-path unlocks** (must be driven in parallel — see SPRINT-PLAN §Critical Path):
1. **E13 buyer gate** — GitHub issue #17 (Veriqan productization).
2. **Live-SIARA legal gate** — ADR-010 P1 counsel sign-off (Prisma live ingestion).
3. **Real corpus acquisition** — two corpora (Veriqan CONDUSEF · Prisma CNBV/Banamex).

---

*Coverage matrix complete. Zero orphan Blocks-production gaps across both subsystems. Plan-only; no production code modified.*
