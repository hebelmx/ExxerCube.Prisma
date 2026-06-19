# RC6 — Cross-Cutting Path to Production (Veriqan VEC + Prisma MVP)

**Date:** 2026-06-18
**Branch:** `Liv`
**Author:** RC6 Synthesis — Winston (Architect) — read-only; no production code modified
**Inputs consumed:** RC4 (Veriqan Readiness Matrix) · RC5 (Prisma Readiness Matrix) · RC5a–RC5d (Track B evidence) · READINESS-CHALLENGE-BRIEF-2026-06-18.md
**Supersedes:** `GAP-MATRIX-2026-06-11.md` · `GAP-MATRIX-2026-06-dual-ground-truth.md` · `MVP-PATH-2026-06-11.md` · `MVP-DEFINITION-2026-06.md` for current state of BOTH subsystems as of 2026-06-18.

---

## 1. Cross-Cutting Executive Summary

Two subsystems. Contrasting readiness postures. One production path.

**Veriqan VEC — the engine is real; the system is not deployable and has latent cardinal-rule violations.**
The validation/verdict/extraction/reporting components are genuine (468 tests green, real PdfPig, real AES-256 schema, real SMTP alerting, real EF Core migrations). But at the full-production bar, Veriqan has never been run on a single real CONDUSEF statement. The only corpus is 3 KnownSynthetic non-compliant fixtures. The Worker is an orphaned 21-line health-check shell with no ingestion entry point, no `appsettings.json`, no Dockerfile, and no observability. Five cardinal-rule violations are latent on real input classes (scanned PDF → false-RED, §20 saldo-a-favor sign → false-RED, §16 column-map → false-PASS or false-FAIL, CL-35 Aptos variants → false-RED, CL-34 card in image → false-RED). Report (marked PDF) and notify (alert email) stages are DI-wired but never called by the pipeline. Distance to full production: **Large.** Minimum deployable composition alone requires ~8 focused engineering items before a single real statement can traverse the pipeline end-to-end.

**Prisma MVP — the pipeline is built, E2E-proven on a simulator, and the headline blocker is now legal and operational, not algorithmic.**
As of 2026-06-14, all six items of the prior MVP-blocking cluster (A1–A6 ingestion + 3-process split) are DONE and wired. `StubDocumentDownloader` and `StubExxerHub` are dead code. Three real production worker processes (Orion Downloader → Athena Extractor → Reconciliator) coordinate over real IndFusion.Ember SignalR hubs with JWT per-process clearance and per-process audit. A max-fidelity E2E gate (`MaxFidelityGateFullPipelineE2ETests`, 21 minutes, ~1,670+ total tests green) proves a real case traverses Quality → OCR → Fusion → Classification → SIRO XML Export → SQL Audit with nothing stubbed on the pipeline. The two named caveats are operational facts: (1) the document source is the SIARA *simulator*, not the live `siara.cnbv.gob.mx` portal (LEGAL-GATED, P1 counsel sign-off PENDING), and (2) the cross-process SignalR transport in the capstone gate is an in-memory ASP.NET TestServer seam (real TCP proven separately). Distance to full production: **Moderate.** The engineering gap is almost entirely deploy/ops/security hardening. The single largest non-engineering blocker is the legal gate.

**Shared gaps:**
- Neither subsystem has deploy artifacts (Dockerfiles, compose, k8s).
- Neither has an operational runbook.
- Neither has an observability backend verified as connected.
- Both lack field-level encryption at rest for legal PII.
- Both lack audit immutability (no SQL ledger / append-only table).
- Both need real document corpus data before accuracy claims can be substantiated.
- Both are blocked on a non-engineering critical-path unlock before full production (E13 buyer gate for Veriqan; live-SIARA legal gate for Prisma).

**Distinct gaps:**
- Veriqan has active cardinal-rule violations requiring engineering fixes before any real statement should be processed; Prisma has no known false-positive/false-negative risks from wiring (its quality concerns are calibration-only).
- Veriqan has no ingestion entry point at all; Prisma has a fully wired ingestion chain (simulator-proven).
- Prisma has a multi-process clearance + audit security model (JWT, per-process audit, replay guard); Veriqan has zero authn/authz on its Worker.
- Prisma is at Wave 0 (make deployable); Veriqan is at Wave 0 (make it run one statement at all).

---

## 2. The Two Non-Engineering Critical-Path Unlocks

These are not engineering tasks. They are the gates that determine whether full production can proceed regardless of how much engineering work is completed.

### CPA-1. E13 Buyer Gate — Veriqan Productization (issue #17, OPEN)

Full production of Veriqan as a regulatory pipeline gate requires multi-tenant productization (E13.1–13.4): embeddable gate API/SDK, per-tenant legal-baseline overlay and onboarding, traceability-matrix export, and Acuerdo-version provenance on every verdict. None of these are built. The economic-buyer discovery decision (GitHub issue #17) is OPEN. No engineering action should be invested in the E13 product half until issue #17 clears, as the product shape, tenant model, and integration contract are all buyer-defined.

**Owner-action:** Drive issue #17 to a decision. If positive: commission E13 engineering (W5 in the Veriqan path). If negative or delayed: treat Veriqan as a single-tenant proof-of-concept and plan accordingly.

### CPA-2. Legal Gate P1 — Live SIARA Portal Authorization — Prisma Live Ingestion

The live `siara.cnbv.gob.mx` portal requires legal pre-clearance for automated access. The codebase enforces `Siara:AllowProductionHost=false`. ADR-010 §Legal preconditions documents the requirements; counsel sign-off is PENDING. This gate blocks the entire production ingestion chain; without it, Prisma operates on the SIARA simulator only.

**Owner-action:** Engage legal counsel; obtain written authorization; record the authorization artifact; flip the code gate with evidence.

### CPA-3. Shared — Real Corpus Acquisition (both subsystems, different corpora)

- **Veriqan:** A labelled corpus of real CONDUSEF-issued statements (known-good + known-broken-by-defect-type) is required before any accuracy claim is valid, before cardinal-rule fixes can be confirmed correct, and before the calibration harness FPR/DR computations are non-vacuous.
- **Prisma:** A labelled corpus of real CNBV/Banamex documents is required before the quality model calibration has production provenance and before volume/latency characterization is representative.
These are separate data-acquisition / bank-relationship tasks, but both require the same type of decision: who owns the bank relationship? what NDA is required? who authors/approves the corpus labels?

---

## 3. Unified Path-to-Production

**Wave structure logic:**
- **W0** — make each subsystem deployable and able to run one real input end-to-end in a containerized composition (buildable now for both).
- **W1** — fix cardinal-rule violations (Veriqan only) and close operational readiness gaps (both).
- **W2** — persistence durability and audit immutability (both).
- **W3** — security hardening (both; partially business-gated; flag for dedicated security review).
- **W4** — corpus calibration and real-data validation (both; corpus-gated).
- **W5** — business/legal/corpus unlocks (parallel non-engineering tracks; must run concurrently with W0–W4 engineering).

**Tag key:**
- `[Veriqan]` / `[Prisma]` / `[Both]` — which subsystem
- `[now]` = buildable now · `[legal]` = legal-gated · `[biz]` = business/procurement-gated · `[corpus]` = corpus-gated · `[ops]` = ops-environment-gated
- Severity: **Blocks** / **Degrades**
- Effort: S (≤1d) / M (2–4d) / L (1–2wk)

---

### Wave 0 — Minimum Deployable Composition

**Goal:** after W0, each subsystem can be stood up with a single `docker-compose up` command and can process one real input end-to-end in a containerized environment.

| # | Item | Tag | Severity | Effort | Owner-action |
|---|------|-----|----------|--------|--------------|
| 0.1 | Author `Dockerfile` for Veriqan.Worker (`mcr.microsoft.com/dotnet/aspnet:10.0` base, ENV placeholders) + `docker-compose.yml` for local dev | [Veriqan][now] | **Blocks** | S | Author and commit Dockerfile + compose |
| 0.2 | Add `POST /verify` HTTP endpoint (or `BackgroundService` folder/queue watcher) in Veriqan.Worker, wired to `IVerificationPipeline.ProcessAsync` | [Veriqan][now] | **Blocks** | S | Add minimal HTTP ingestion endpoint |
| 0.3 | Author Veriqan `appsettings.json` with all required keys (`VeriqanDb`, `CsvReferenceData:RootDirectory`, SMTP); add startup validation warnings when absent | [Veriqan][now] | **Blocks** | S | Author settings file; add startup guard |
| 0.4 | Wire report (marked PDF) and notify (email alert) stages into `VerificationPipeline.ProcessAsync` after verdict (stages 8–9 currently orphaned) | [Veriqan][now] | **Blocks** | S | Call `IMarkedPdfGenerator.GenerateAsync` + `IVecAlertService.SendRedAlertAsync` in pipeline |
| 0.5 | Add Persist stage to Veriqan pipeline: write `JobVerdict` + `Finding` rows; wire `AddVeriqanDisposition` | [Veriqan][now] | **Blocks** | M | Add persist stage; wire disposition service |
| 0.6 | Move Veriqan demo reference bundle to a deployable data directory; document bundle-authoring process | [Veriqan][now] | **Blocks** | M | Move `Demo_Bank_(Iqubica)` CSV bundle; document schema + authoring process |
| 0.7 | Replace Veriqan hardcoded health stubs with real `IHealthCheck` (DB connectivity + CSV-root existence + tolerance-cache) and a real `/health/ready` endpoint | [Veriqan][now] | **Blocks** | S | Implement real health checks |
| 0.8 | Wire OTel OTLP/Prometheus exporter + Serilog sink in Veriqan.Worker `Program.cs` | [Veriqan][now] | **Blocks** | S | Add OTel + Serilog to Worker |
| 0.9 | Author Dockerfiles for all 4 Prisma production hosts (Orion, Athena, Reconciliator, Web.UI) | [Prisma][now] | **Blocks** | M | Author Dockerfiles; extend CI with image-build step |
| 0.10 | Add auto-migration runner (or `--migrate-only` CLI) to each Prisma worker; document multi-DbContext sequencing | [Prisma][now] | **Blocks** | S | Wire migration; document 3-context order |
| 0.11 | Remove hardcoded `DESKTOP-FB2ES22`, `(localdb)`, SIARA URL (`localhost:5002`), and Windows log path from Prisma Web.UI and simulator configs; produce `.env.example` | [Prisma][now] | **Blocks** | S | Config sweep; env-var replacements; `.env.example` |
| 0.12 | Wire real `IReadinessProbe` in Reconciliator and real `IHealthCheck` in Web.UI | [Prisma][now] | **Blocks** | S | Implement real health probes |
| 0.13 | Author `docker-compose.dev.yml` for the full Prisma stack (Orion + Athena + Reconciliator + Web.UI + Simulator + SQL + Seq) with correct inter-process network + shared volume | [Prisma][now] | **Degrades** | S | Author compose file |
| 0.14 | Add Serilog remote log sinks to all 3 Prisma worker `appsettings.json` | [Prisma][now] | **Degrades** | S | Add Seq/Elastic sink to worker configs |

**W0 estimated effort:** ~2 sprints total across both subsystems (4–6 weeks for a single developer, 2–3 weeks with two). Gate: after W0, `docker-compose up` runs both subsystems; a Veriqan statement can be POSTed to `/verify`; a Prisma document can flow through all three processes in a containerized deployment against the simulator.

---

### Wave 1 — Correctness and Cardinal-Rule Fixes + Operational Readiness

**Goal:** eliminate the latent false-block risks (Veriqan); close operational visibility and operational reliability gaps (both).

| # | Item | Tag | Severity | Effort | Owner-action |
|---|------|-----|----------|--------|--------------|
| 1.1 | Add text-layer-density abstain guard in Veriqan: zero/sparse words → whole-verdict BLOCKED, not RED (cardinal rule — scanned PDF false-RED) | [Veriqan][now] | **Blocks** | S | Add density guard before section detection |
| 1.2 | Fix Veriqan CL-31 pagination: anchor regex to footer band (bottom 10% of page height) to prevent body-phrase first-match | [Veriqan][now] | **Blocks** | S | Fix regex anchor |
| 1.3 | Fix Veriqan CL-34 card-number: add image-layer fallback or page-1 propagation to cover card-in-graphic case | [Veriqan][now] | **Blocks** | M | Add image-layer fallback |
| 1.4 | Fix Veriqan CL-35 Aptos font: extend suffix allowlist or use `StartsWith("Aptos")`; add `IsEmbedded` flag to `FontUsage` | [Veriqan][now] | **Blocks** | M | Fix suffix list + embedding detection |
| 1.5 | Add abstain guard for Veriqan §20 saldo-a-favor ambiguous sign; add column-count guard for §16 column-map (corpus validation will confirm fix) | [Veriqan][now/corpus] | **Blocks** | S | Add abstain on ambiguous sign; add column guard |
| 1.6 | Replace Veriqan `VerdictAggregator` `default→passCount` arm with explicit abstain/throw | [Veriqan][now] | **Degrades** | S | Fix default arm |
| 1.7 | Fix Veriqan NFR-5 non-determinism: inject `TimeProvider` into extractor for date-year repair | [Veriqan][now] | **Degrades** | S | Inject `TimeProvider` |
| 1.8 | Fix Veriqan dedup concurrency-unsafe: `ConcurrentDictionary.GetOrAdd` (InMemory); retry-on-conflict (SQL) | [Veriqan][now] | **Degrades** | S | Fix concurrency |
| 1.9 | Add persisted already-sent flag on Veriqan `JobVerdict` for duplicate-alert guard | [Veriqan][now] | **Degrades** | S | Add flag; gate send on first-RED verdict |
| 1.10 | Add Veriqan authn/authz (`[Authorize]`) on statement-submission and disposition endpoints | [Veriqan][now] | **Blocks** | M | Add JWT auth to Worker |
| 1.11 | Enforce TLS on Veriqan Worker (HTTPS redirect + HSTS) and SMTP (`EnableSsl=true`) | [Veriqan][now] | **Blocks** | S | Add HTTPS + SMTP TLS |
| 1.12 | Author Prisma real-TCP cross-process proof: acceptance test booting 3 OS processes over real TCP | [Prisma][now] | **Blocks** | M | Author real-TCP integration test |
| 1.13 | Author Prisma production operational runbook (startup, migration order, failure triage, SIARA rotation, on-call) | [Prisma][now] | **Blocks** | M | Author runbook |
| 1.14 | Wire `RecordDocumentProcessed()` into Prisma orchestrators; fix worker dashboard metrics zeros | [Prisma][now] | **Degrades** | S | Wire counter calls |
| 1.15 | Audit all Prisma `LogAuditAsync` call sites for `processId` parameter adoption | [Prisma][now] | **Degrades** | S | Grep + fix call sites |
| 1.16 | Reproduce + fix Prisma Tesseract second-init deadlock; add restart regression test | [Prisma][now] | **Degrades** | M | Isolate + fix Tesseract init |
| 1.17 | Trace Prisma Sentinel service; classify status; wire health/auth/observability or document as out-of-MVP | [Prisma][now] | **Blocks** | L | Trace and classify Sentinel |
| 1.18 | Add Prisma failure-mode integration test suite (Athena unreachable, Tesseract fail, SIARA 503, Reconciliator restart) | [Prisma][now] | **Degrades** | M | Author chaos test suite |
| 1.19 | Extend Prisma CI pipeline with Docker image build + publish + push steps | [Prisma][now] | **Degrades** | M | Add CI deploy stages |

---

### Wave 2 — Persistence Durability and Audit Immutability

**Goal:** verdicts, findings, and audit records are durable, tamper-evident, and survive process restarts.

| # | Item | Tag | Severity | Effort | Owner-action |
|---|------|-----|----------|--------|--------------|
| 2.1 | Implement Veriqan `EfVerificationResultStore` + `EfReprocessAuditRepository`; add EF migrations; wire in SQL branch | [Veriqan][now] | **Blocks** | M | Implement + wire EF persistence |
| 2.2 | Convert Veriqan `Disposition` table to SQL Server 2022 append-only ledger (`LEDGER=ON, APPEND_ONLY`) or add `DENY UPDATE, DELETE` grant + row HMAC | [Veriqan][now] | **Blocks** | M | Ledger table or DENY grant |
| 2.3 | Add Veriqan `BatchExceptionLog` EF entity + migration + repository for durable dead-letter queue | [Veriqan][now] | **Degrades** | M | Persist failed-item records |
| 2.4 | Stamp `EngineVersion` + `ReferenceBundleVersion` on Veriqan `JobVerdict` | [Veriqan][now] | **Degrades** | S | Add version columns |
| 2.5 | Add `ef migrations bundle` CI/CD migration entrypoint for Veriqan | [Veriqan][now] | **Degrades** | S | Add bundle target |
| 2.6 | Convert Prisma `AuditRecords` to SQL Server 2022 append-only ledger or add `DENY UPDATE/DELETE` + row HMAC chain; document 7-year retention policy | [Prisma][now] | **Blocks** | M | Ledger table or DENY grant |
| 2.7 | Implement Prisma `PersonIdentityResolver` DB persistence (EF entity + migration + repository) | [Prisma][now] | **Degrades** | M | Persist identity mappings |
| 2.8 | Add reconnect/retry integration test for Prisma hub-client disconnect mid-run | [Prisma][now] | **Degrades** | M | Reconnect test |
| 2.9 | Author Prisma SIARA session-longevity soak test (50 sequential documents against simulator) | [Prisma][now] | **Degrades** | M | Soak test |

---

### Wave 3 — Security Hardening (flag for dedicated security review; partially business-gated)

**Goal:** both subsystems are hardened to a level appropriate for a load-bearing regulated document-automation pipeline. This wave is intentionally flagged for a **separate, dedicated security review** — the items below are the engineering backlog that review should produce and prioritize.

| # | Item | Tag | Severity | Effort | Gate |
|---|------|-----|----------|--------|------|
| 3.1 | Replace Veriqan AES-CBC with AES-GCM (authenticated encryption); add key-id to ciphertext rows; integrate Key Vault for key custody | [Veriqan][biz] | **Blocks** | L | Key Vault provisioning |
| 3.2 | Integrate Veriqan secrets manager (Key Vault / AWS KMS) for AES key, SQL connection, SMTP credentials; prohibit plaintext in env | [Veriqan][biz] | **Blocks** | M | Procurement decision |
| 3.3 | Design and implement Veriqan LFPDPPP PII retention/erasure regime (TTL columns, ARCO path, card-number masking in logs) | [Veriqan][biz] | **Blocks** | L | Legal/privacy policy decision |
| 3.4 | Decide Veriqan fail-open vs fail-closed policy for the inline pipeline gate; implement gate timeout + circuit-breaker | [Veriqan][biz] | **Blocks** | M | Stakeholder policy decision |
| 3.5 | Add Veriqan reference-bundle SHA-256/HMAC signature verification at load time | [Veriqan][now] | **Degrades** | S | Buildable now |
| 3.6 | Implement Prisma per-process asymmetric JWT keys (RS256/ECDSA); remove shared HMAC secret; update ADR-012 | [Prisma][biz] | **Blocks** | M | Key provisioning decision |
| 3.7 | Implement Prisma vault-backed `ISiaraCredentialSource` for AutomatedLogin mode | [Prisma][ops] | **Blocks** | M | Secret store provisioning |
| 3.8 | Apply Prisma field-level AES-GCM column encryption to regulated PII in `PrismaDbContext` entities | [Prisma][biz] | **Blocks** | L | Data classification + key management decision |
| 3.9 | Implement Prisma zero-downtime JWT secret rotation (overlapping-key grace period in `IProcessClearanceTokenService`) | [Prisma][now] | **Degrades** | M | Buildable now |
| 3.10 | Design Prisma network segmentation policy (k8s NetworkPolicy) restricting hub access; evaluate mTLS for inter-process communication | [Prisma][biz] | **Degrades** | M | Network topology / provisioning decision |
| 3.11 | Design Prisma audit fail-closed mode for regulated document types; document fail-open policy | [Prisma][biz] | **Degrades** | M | Policy decision |
| 3.12 | Engage Prisma legal/compliance counsel on CNBV CUB outsourcing-regime obligations and data-residency requirements | [Prisma][biz] | **Degrades** | L | Legal counsel |
| 3.13 | Commission ISO 27001 / SOC 2 gap assessment for both subsystems; produce certification roadmap | [Both][biz] | **Degrades** | L | Executive/procurement decision |
| 3.14 | Engage Veriqan legal/compliance counsel on CNBV CUB outsourcing regime alignment | [Veriqan][biz] | **Degrades** | L | Legal counsel |
| 3.15 | Design and document CNBV CUB outsourcing-regime artefacts, mandatory independent review controls, and data-residency enforcement for both subsystems | [Both][biz] | **Degrades** | L | Legal + compliance decision |
| 3.16 | Configure production alerting (SLA breach + pipeline error) for both subsystems; wire on-call escalation path | [Both][ops] | **Blocks** | M | Ops tooling procurement |

---

### Wave 4 — Corpus Calibration and Real-Data Validation (CORPUS-GATED)

Unblockable until CPA-3 corpus acquisition is resolved for each subsystem.

| # | Item | Tag | Severity | Notes |
|---|------|-----|----------|-------|
| 4.1 | Measure Veriqan arithmetic tolerances against real corpus; confirm legal defaults; obtain compliance sign-off | [Veriqan][corpus] | **Degrades** | All rule thresholds are spec-derived or estimated |
| 4.2 | Calibrate Veriqan §6 extractor against real §6 table; verify payment-simulation recursion | [Veriqan][corpus] | **Degrades** | §6 absent in all 3 current fixtures |
| 4.3 | Build Veriqan §8 coherence check once real §8 specimens available | [Veriqan][corpus] | **Degrades** | Self-documented deferral |
| 4.4 | Replace Veriqan 3-value constant confidence with measured per-field score; recalibrate `ConfidenceGuard` | [Veriqan][corpus] | **Degrades→Blocks** | NFR-8 structurally un-deliverable without real distribution |
| 4.5 | Validate Veriqan geometry constants (X-bands, Y-bands) against real bank layouts; implement dynamic layout detection | [Veriqan][corpus] | **Degrades** | Currently calibrated to Dummie VEC fixture #1 only |
| 4.6 | Build Veriqan real advertising detection from corpus; restore §12 threshold to 700 chars | [Veriqan][corpus] | **Degrades** | Current 7-phrase allowlist is corpus-starved |
| 4.7 | Measure Veriqan NFR-1 p95 against real PDFs; resize worker pool if needed | [Veriqan][corpus] | **Degrades** | Synthetic p95 ≈ 30s (violates NFR by 3×) |
| 4.8 | Confirm Veriqan §20 saldo-a-favor sign convention from real §20 tables; fix or harden | [Veriqan][corpus] | **Blocks** | Cardinal-rule fix needs corpus validation |
| 4.9 | Calibrate Veriqan §16 column-map against real §16 specimens; replace guessed map | [Veriqan][corpus] | **Blocks** | Self-declared "no real §16 fixture available" |
| 4.10 | Run Veriqan calibration harness against real KnownGood + KnownBroken corpus; compute real FPR/DR | [Veriqan][corpus] | **Blocks** | Harness exists; corpus absent |
| 4.11 | Retrain and validate Prisma quality model against real CONDUSEF/Banamex document corpus; set `TrainedDate` | [Prisma][corpus] | **Degrades** | Real coefficients but no provenance |
| 4.12 | Prisma volume soak test at realistic cadence (≤2k/day); characterize p95 per-document latency | [Prisma][corpus] | **Degrades** | Gate ran 1 document in 21 minutes |
| 4.13 | Validate Prisma SIRO XSD conformance once Banamex supplies the `.xsd` | [Prisma][corpus/biz] | **Degrades** | Blocked on Banamex supplying `.xsd` (issue #2) |

---

### Wave 5 — Business / Legal / Corpus Unlocks (parallel non-engineering tracks)

These run **concurrently** with W0–W4 engineering. They are not sequenced after engineering; they are independent parallel tracks that must be owned and driven simultaneously. Engineering waves W4 and parts of W3 are gated on their outcomes.

| # | Item | Tag | Gate | Owner-action |
|---|------|-----|------|--------------|
| 5.1 | **E13 buyer gate** — drive GitHub issue #17 to a decision; if positive, commission E13 engineering (W5 Veriqan) | [Veriqan][biz] | CPA-1 | Product / business owner: decide and close issue #17 |
| 5.2 | **Live-SIARA legal gate** — obtain counsel sign-off on ADR-010; flip `Siara:AllowProductionHost=true`; record authorization artifact | [Prisma][legal] | CPA-2 | Legal owner: drive ADR-010 P1 to resolution |
| 5.3 | **Veriqan corpus acquisition** — identify bank relationship owner; agree NDA; acquire labelled KnownGood + KnownBroken CONDUSEF statements; import into calibration harness | [Veriqan][corpus] | CPA-3a | Business owner: drive corpus acquisition |
| 5.4 | **Prisma corpus acquisition** — acquire labelled CNBV/Banamex document samples; validate quality model | [Prisma][corpus] | CPA-3b | Business owner: drive corpus acquisition |
| 5.5 | **Key Vault / HSM provisioning** — provision Azure Key Vault (or client-equivalent); allocate key slots for AES-GCM (Veriqan), JWT key pairs (Prisma), SIARA credentials (Prisma) | [Both][ops] | ops decision | Infrastructure owner: provision and document key hierarchy |
| 5.6 | **Observability infrastructure** — provision Seq or Prometheus/Grafana in staging; verify metric ingestion; author SLA alert rules | [Both][ops] | ops decision | Ops owner: provision and connect |

---

### Wave 6 — E13 Productization (E13-GATED; Veriqan only)

Unblockable until CPA-1 (issue #17) clears.

| # | Item | Tag | Description |
|---|------|-----|-------------|
| 6.1 | Runtime tenant selection; config-only profile loading (`ITenantProfileResolver` by tenant-id) | [Veriqan][E13] | Multi-tenancy foundation |
| 6.2 | E13.1: Embeddable gate SDK / HTTP API with documented latency budget | [Veriqan][E13] | Integration contract |
| 6.3 | E13.2: Bundle `toleranceConfig` binding to tenant profile overlay | [Veriqan][E13] | Per-bank threshold overrides |
| 6.4 | E13.3: Traceability-matrix export (CheckId + DOF numeral + verdict + evidence locator) | [Veriqan][E13] | Regulatory audit trail |
| 6.5 | E13.4: `AcuerdoVersion` field on rules + `JobVerdict`; Acuerdo-edition governance process | [Veriqan][E13] | Rule provenance |

---

## 4. Consolidated Unknown-Unknowns

Items absent from any requirement register, first surfaced in the RC3/RC5 negative-space lenses. Deduped across both tracks.

| # | Unknown-unknown | Subsystem | Severity | Buildable? |
|---|-----------------|-----------|----------|------------|
| U1 | **Scanned/image-only PDF → false-RED (cardinal violation).** PdfPig extracts nothing; `BuildAllAbsent` emits all sections present-and-absent; `MandatorySectionsPresenceRule` FAILs all. No requirement covers OCR or text-density abstain guard. | Veriqan | **Blocks** | Buildable now (W1.1) |
| U2 | **No minimum-extraction-coverage floor.** Near-zero extraction → every rule abstains → GREEN. The dual of the cardinal rule (never false-PASS a defective statement) is violated silently. | Veriqan | **Blocks** | Buildable now (add coverage threshold) |
| U3 | **Report (marked PDF) and notify (alert email) stages are orphaned in Veriqan.** `IMarkedPdfGenerator` and `IVecAlertService` wired but never called by `VerificationPipeline`. A RED verdict produces no annotated PDF and no email. | Veriqan | **Blocks** | Buildable now (W0.4) |
| U4 | **Veriqan config-absence is a silent correctness trap.** Worker boots "Healthy" into a non-functional in-memory shell; misconfigured prod deploy passes all health checks and silently discards all data. No monitoring catches this. | Veriqan | **Blocks** | Buildable now (W0.3 + W0.7) |
| U5 | **Fail-open vs fail-closed gate policy is an unowned design decision (Veriqan).** No policy, no code, no timeout at pipeline entry. The first bank asking "what happens if Veriqan is slow?" has no answer. | Veriqan | **Blocks** | Business-gated (W3.4) |
| U6 | **No real reference-bundle provenance or authoring process (Veriqan).** Only a test asset exists; no defined source, owner, or deployment path for a real bank's CSVs. Every real bind → BLOCKED. | Veriqan | **Blocks** | Business-gated (W0.6) |
| U7 | **LFPDPPP PII retention/erasure regime absent (Veriqan).** RFC, CLABE, card#, client name processed with no TTL, no ARCO path, no minimization. A Mexican bank's privacy office cannot onboard a processor with no retention/erasure design. | Veriqan | **Blocks** | Business-gated (W3.3) |
| U8 | **Rule-version ↔ Acuerdo-edition governance undefined (Veriqan).** No `AcuerdoEdition` field, no in-flight migration process, no versioning policy. The first CONDUSEF Acuerdo republication makes all historical verdicts ambiguous. | Veriqan | **Degrades** | E13-gated (W6.5) |
| U9 | **Confidence is a 3-value constant; NFR-8 is structurally un-deliverable on real layout drift (Veriqan).** A wrongly-extracted value reports Extracted@1.0 and cannot trip the abstain-safety net. The abstain mechanism fails exactly when real geometry differs from the fixture. | Veriqan | **Degrades→Blocks** | Corpus-gated (W4.4) |
| U10 | **Password-protected PDF is a 100%-fail blind spot (Veriqan).** `PdfDocument.Open` with no password; any owner-password-encrypted archive fails extraction for every statement. | Veriqan | **Degrades** | Buildable now |
| U11 | **Sentinel Monitor is a black box (Prisma).** No startup code, auth, health checks, or observability found for the Sentinel service. Readiness state is fully unknown. | Prisma | **Blocks** | After tracing (W1.17) |
| U12 | **Multi-DbContext migration sequencing undocumented (Prisma).** Three DbContext types across multiple hosts; no documented application order, no CI orchestration, no runbook step. Wrong sequence on first-run could leave the schema inconsistent. | Prisma | **Blocks (first-run ops)** | Buildable now (W0.10) |
| U13 | **Tesseract second-init deadlock under pipeline restart (Prisma).** Partial-case gate test disables the pipeline to avoid this. Latent in any production Athena restart without full process restart. | Prisma | **Degrades** | Buildable now (W1.16) |
| U14 | **Quality model provenance unverifiable (Prisma).** Real coefficients, `TrainedDate=null`, `TrainingDataSize=0`. No way to know when trained, on what data, or whether still valid. A data drift event is invisible. | Prisma | **Degrades** | Corpus-gated (W4.11) |
| U15 | **No secret rotation mechanism (Prisma).** Three workers share a JWT secret; rotating it requires coordinated simultaneous redeploy with no runbook, no rolling-rotation support, no rollback path. | Prisma | **Blocks (incident response)** | Buildable now (W3.9) |
| U16 | **Worker metrics counters wired but never called (Prisma).** `RecordDocumentProcessed()` defined but zero orchestrators call it. Dashboard shows zeros even when the pipeline processes documents at full rate. Operators have no throughput visibility for headless workers. | Prisma | **Degrades** | Buildable now (W1.14) |
| U17 | **`ProcessId` audit adoption incomplete (Prisma).** The backward-compatible API exists but call-site adoption was not verified. Audit rows may systematically lack process identity, defeating forensic non-repudiation despite the schema supporting it. | Prisma | **Degrades** | Buildable now (W1.15) |
| U18 | **No chaos / failure-mode regression (Prisma).** "InsufficientData / best-effort on failure" documented but not tested at the pipeline boundary. First production failure outside covered modes may produce unhandled exception instead of graceful degradation. | Prisma | **Degrades** | Buildable now (W1.18) |
| U19 | **Reference-bundle authenticity untrusted (Veriqan).** Schema-valid but content-wrong bundles silently produce wrong verdicts at scale. No signature, no checksum, no approver record on the bundle. | Veriqan | **Degrades** | Buildable now (W3.5) |
| U20 | **SIARA session longevity across production volume unproven (Prisma).** Long-running session behaviour (warm-session across hundreds of documents, rate limits, session expiry and re-login) not tested. | Prisma | **Degrades** | Buildable now (W2.9) |

---

## 5. Supersession Statement

This document (RC6) and the two readiness matrices it integrates (RC4 and RC5) are the **canonical current-state readiness picture** for both subsystems as of 2026-06-18 (branch `Liv`). They supersede:

| Superseded document | Superseded for | Reason |
|--------------------|----------------|--------|
| `GAP-MATRIX-2026-06-11.md` | All Prisma MVP content | A1–A6 "Planned/Partial" classifications are refuted; full-production bar replaces MVP bar |
| `GAP-MATRIX-2026-06-dual-ground-truth.md` | All Prisma content | Superseded by RC5 ground-truth wiring trace |
| `MVP-PATH-2026-06-11.md` | All workstream items | All WS1–WS5 items are DONE (MVP gate issue #5 closed 2026-06-14); RC6 Wave 0–6 is the new ordered work plan |
| `MVP-DEFINITION-2026-06.md` | MVP bar definition | MVP bar was reached; this challenge targets the full-production bar (RC Brief §2) |
| All "Veriqan VEC state" sections in the above matrices | Veriqan VEC content | RC4 is the authoritative Veriqan state |

The prior documents remain in the archive for historical record and should not be updated.

**Canonical reference from this point:**
- Veriqan VEC state: `RC4-VERIQAN-READINESS-MATRIX.md`
- Prisma MVP state: `RC5-PRISMA-READINESS-MATRIX.md`
- Unified path to production: **this document (RC6)**

---

## Appendix — Gap Counts at a Glance

| Subsystem | Blocks | Degrades | Cosmetic | Total |
|-----------|--------|----------|----------|-------|
| Veriqan VEC (RC4) | 27 | 33 | 3 | 63 |
| Prisma MVP (RC5) | 16 | 24 | 4 | 44 |
| **Combined (deduplicated shared items)** | **~38** | **~52** | **~6** | **~96** |

Note: several security/compliance/ops gaps are substantively shared across both subsystems (no deploy artifacts, no observability backend, no audit immutability, no field-level PII encryption, no ISO/SOC trajectory) and are counted once in the combined figure.

---

*RC6 synthesis complete. No production code was read, modified, or created in the course of writing this document. Both RC5 and RC6 files written.*
