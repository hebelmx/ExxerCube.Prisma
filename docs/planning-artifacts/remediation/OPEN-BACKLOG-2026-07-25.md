# Verified Open Backlog — 2026-07-25 (RC6 [now] items + tracked residuals)

**What this is:** a ground-truth re-verification of every RC6 buildable-now (`[now]`) item plus the
residuals tracked in `TRACKER-w3-buildable-security.md` and the remediation `EXECUTION-TRACKER.md`.
The RC6 matrix (2026-06-18) was known-stale; this document supersedes it for *what is open today*.

**Method:** 3 parallel read-only scouts (Prisma W0/W1 · Prisma W2+residuals · Veriqan W0–W2), each
item verified against code (`file:line`), git history (commit hashes), and test attributes — never
planning prose. Orchestrator independently spot-checked every load-bearing OPEN/PARTIAL claim
(Athena Dockerfile grep, `BatchExceptionLog` repo-wide grep, JobVerdict designer entity-block map,
soak-test `Skip` attribute, `BundleIntegrityVerifier` freshness comment). Branch `Liv`, HEAD `d7fb6849`.

**Headline:** of ~40 RC6 `[now]` items, **only 4 engineering items are genuinely OPEN/PARTIAL**, plus
the 2 deliberately-parked W3 security residuals and a few minors. Everything else is DONE with
commit-level evidence (§3).

---

## 1. Genuinely OPEN — buildable now (ranked by severity)

| # | Item | Origin | Severity | Evidence |
|---|------|--------|----------|----------|
| O1 | ~~**Athena container cannot run OCR**~~ — ✅ **DONE 2026-07-27** (`TRACKER-O1-athena-container-ocr.md`): Emgu runtime package made property-driven (`EmguLinuxRuntimePackage`; container uses `ubuntu-24.04-x64` for the noble base, dev box unchanged), Dockerfile runtime stage got tesseract+spa/eng tessdata+both link shims+libcvextern's noble deps, new `--ocr-smoke` self-test proven in-container (EXIT 0, spa model) and wired as a CI step. **New follow-up:** the Web.UI image has the same Emgu hole (only its Tesseract half was fixed by GH#28) — O1's pattern applies verbatim. | RC6 0.9 residual | ~~Blocks~~ CLOSED | `docker run … --ocr-smoke` EXIT 0; CI step in quality-gates.yml |
| O2 | ~~**Veriqan durable dead-letter absent**~~ — ✅ **DONE 2026-07-28** (`368ec43c` + endpoint tests in close-out commit; `TRACKER-veriqan-w2-closeout.md`): `BatchExceptionLog` entity + migration `20260728155959` + EF/in-memory repos, per-run `BatchId`, never-abort write guard, `GET /exceptions?batchId=`. | RC6 2.3 | ~~Degrades~~ CLOSED | Orchestration.Tests green incl. literal submit→query endpoint AC; Persistence.IntegrationTests 37/37 |
| O3 | ~~**JobVerdict rows carry no provenance**~~ — ✅ **DONE 2026-07-28** (`5f0b9794`): `EngineVersion` + `ReferenceBundleVersion` (`BundleId ?? SchemaVersion`) on `JobVerdict`, migration `20260727233432`. **CAVEAT (new residual O7):** repo has NO versioning scheme, so `EngineVersion` is always `1.0.0.0` — column is real, value is a constant until an owner-ruled version source (MinVer / git-SHA / manual `<Version>`) exists. | RC6 2.4 | ~~Degrades~~ CLOSED (O7 residual) | live Testcontainers provenance round-trip test |
| O4 | ~~**Veriqan migrations not CI-wired**~~ — ✅ **DONE 2026-07-28** (`841ad6f8`): `veriqan-migrations` CI job — dotnet-ef 10.0.8 tool-manifest pin, `ef migrations bundle` (self-contained linux-x64) artifact, from-zero apply vs ephemeral SQL 2022; every command proven locally; runbook §8 extended. **CAVEAT:** see dormant-CI finding below. | RC6 2.5 | ~~Degrades~~ CLOSED | local from-zero apply of full 10-migration lineage, exit 0 + idempotent re-run |

### W3 security residuals (deliberately parked 2026-07-23 — confirmed nothing landed since)
| # | Item | Notes |
|---|------|-------|
| O5 | **S1 bundle freshness/rollback replay** — `generatedAt:` parsed but never enforced (`BundleIntegrityVerifier.cs:38` self-documents "no freshness/TTL policy"); per-row `imageSha256`/`documentRefSha256` remain unverified payload (`CsvReferenceDataAdapter.cs:893`); Veriqan Web.UI `appsettings.json` `CsvReferenceData` section lacks `BundleHmacKey` doc-parity entry. | Needs a design decision (external state / TTL policy) before code. |
| O6 | **S2 rotation startup diagnostics** — `ProcessIdentitySigningKeys.cs:46` silently filters blank secrets; no weak-key (<128-bit) check; no never-retired-`PreviousJwtSecrets` diagnostic (forgotten Phase C = permanent second key, silently); blank-JwtSecret path asymmetry. Misconfig-only; fails closed. | Small, self-contained. |

### New residuals (2026-07-28 W2-closeout adversarial review)
- **O7 — no repo versioning scheme → provenance `EngineVersion` is a constant `1.0.0.0`**: no
  `<Version>`/MinVer/GitVersion anywhere; O3's column (and `Finding.EngineVersion` before it)
  carries no discriminating value. Needs an owner ruling on the version source, then a small
  build-props change. (`TRACKER-veriqan-w2-closeout.md` F1.)
- **O8 — ALL CI is dormant**: no repo-root `.github/workflows/`; `quality-gates.yml` lives only
  under `Prisma/Code/Src/CSharp/.github/` where GitHub Actions never executes it — O1's
  `--ocr-smoke`, the E2-S8 publish matrix, and the new `veriqan-migrations` job all included.
  Activation = move to root + fix stale paths (root-less `dotnet restore`, e2e job paths);
  several jobs would go red immediately. Needs its own decision-gated pass.

### Minor / env-gated residuals
- **Soak (RC6 2.9):** `SiaraSessionLongSoakE2ETests` (N=50) exists but `[Fact(Skip="…needs a dedicated performance pass…")]` (`SoakE2ETests.cs:324`). Mechanism + corpus proven; the soak itself never runs. → needs a dedicated perf pass on a non-contended box.
- **QueueDepth hardcoded 0** in both `AthenaDashboardService`/`OrionDashboardService` (`:46`) — counters otherwise live (`RecordDocumentProcessed` wired, E1-S7).
- **Runbook on-call section is a placeholder**; reviewer sign-off pending (`PRISMA-PRODUCTION-RUNBOOK.md`, `62ba0c2a`).
- **Audit widen live-SQL apply** — migration `20260624120000_WidenAuditErrorMessageColumn` merged (`1a5dc11d`) but the apply to the live SQL box remains owner-gated.
- **Simulator load roadmap** (`siara-simulator-load-roadmap.md`) still PLANNED: no CaseService auto-start/replenish, no 10k corpus scale-out.

### Security-review flags (for the dedicated W3 pass, not buildable-now stories)
- **Veriqan 2.2 immutability is trigger-based** (`AFTER UPDATE,DELETE THROW 51000` + interceptor, `abd2001e`) — `db_owner` can `DISABLE TRIGGER`; the RC6-spec'd `LEDGER=ON`/DENY/row-HMAC was not used (migration comment concedes this). Contrast: **Prisma** AuditRecords DID get native `LEDGER=ON (APPEND_ONLY=ON)` (`f2e257d0`).
- Prisma 2.6 has no app-level row-HMAC chain; tamper-evidence rests on SQL 2022 native ledger hashing (acceptable, but note for the audit).

---

## 2. Standing gates (unchanged — not buildable)
- **E13 buyer gate** (issue #17) → Veriqan Wave 6 productization.
- **Live-SIARA legal gate** (ADR-010 P1 counsel; `Siara:AllowProductionHost=false`).
- **Corpus acquisition** (Veriqan CONDUSEF + Prisma CNBV) → Wave 4 calibration items.
- **[biz]/[ops] W3 items** (Key Vault, AES-GCM, LFPDPPP, ES256 per-process keys per ADR-012 addendum, mTLS/segmentation, alerting infra).

---

## 3. Verified DONE (was open in RC6 2026-06-18) — do NOT re-plan

Prisma: 0.9 Dockerfiles×4 (`ded95284`, minus O1) · 0.10 `--migrate-only`+sequencing (`56621a6e`) ·
0.11 config sweep+`.env.example` (`523737f1`) · 0.12 real probes (`ebc7d644`) · 0.13 full-stack compose
(`9a36444e`) · 0.14 Seq sinks (`27b78e0c`) · 1.12 real-TCP 3-process proof `RealTcpThreeProcessE2ETests`
(`d627397f`) · 1.13 runbook (`62ba0c2a`) · 1.14 metrics wiring (`03ae2a87`) · 1.15 processId adoption,
33 call sites (`321e0791`) · 1.16 Tesseract second-init deadlock fix + regression (`b9ecd85a`; distinct
from SIGSEGV `eab4bdad`) · 1.17 Sentinel traced/classified OUT-OF-MVP · 1.18 chaos suite
`FailureModeE2ETests` 4/4 scenarios (`6adc7299`) · 1.19 CI docker build+publish (`d33adced`) ·
2.6 AuditRecords native ledger + DDL trigger + retention policy (`f2e257d0`, `320f8ece`) ·
2.7 PersonIdentityResolver DB persistence via `Persona` reuse + `FindOrCreateAsync` in production path
(`7a3703d0`, `4cdd4bce`, `c3a5e6b3`) · 2.8 reconnect test (in `FailureModeE2ETests`) ·
3.9 JWT rotation grace (`00a062ee`+`0cad7904`) · FU1 dashboard SignalR bridge (`da375c2e` — supersedes
older tracker "owner-gated" note).

Veriqan: ALL of Wave 0 (0.1–0.8: Dockerfile+compose, `/verify`+`/batch` endpoints, appsettings+validator,
report/notify stages, persist stage, bundle relocation+authoring guide `046f6905`, real health checks,
OTel+Serilog) · ALL of Wave 1 (1.1 density guard → BLOCKED; 1.2 superseded by CL-31 plausibility gate
`4d550573`; 1.3 `391e7426`; 1.4 `8ba45202`; 1.5 `d0ab2485`+`8df83b01`; 1.6 `dcc3ec73`; 1.7 `aa998bdd`;
1.8 `e9fc1287`; 1.9 `AlertSentAt` `5e1eea27`; 1.10 JWT+fallback-policy auth; 1.11 HSTS+SMTP TLS) ·
2.1 EF durable stores (`481e805d`) · 2.2 immutability (trigger-mechanism caveat above) · 3.5 bundle
HMAC (`58b4621e`+`be5f1df9`) · U2 extraction-coverage floor (BLOCKED on near-zero extraction) ·
U10 password-PDF (`7f7240d9`) · fail-CLOSED gate + circuit breaker (`f394fab8`).

---

## 4. Recommended next epics (owner picks)
1. **O1 Athena container OCR** — the only Blocks-severity buildable item; bounded (Dockerfile + compose + a containerized OCR smoke proof). Reuse Web.UI Dockerfile's OCR stanza + `eng/link-emgu-native.sh` pattern.
2. **O2+O3+O4 as one "Veriqan W2 close-out" epic** — dead-letter entity, JobVerdict provenance columns, CI migrate step. All small, one subsystem, one migration wave.
3. **O6 (+O5 design spike)** — W3-sec residual hardening; O5 needs an owner/design decision on freshness policy first.
