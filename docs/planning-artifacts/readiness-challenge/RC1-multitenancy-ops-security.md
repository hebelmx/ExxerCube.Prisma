# RC.1 — Requirement→Evidence Trace: Multi-Tenancy (E13) / Observability-Ops / Security-Flag Cluster

**Date:** 2026-06-18 · **Branch:** `Liv` · **Auditor:** Claude Code (RC.1 cluster agent, read-only — no production code modified)
**Anchor:** `docs/planning-artifacts/READINESS-CHALLENGE-BRIEF-2026-06-18.md`
**Inputs:** `RC0a-scope-register.md` (intended scope), `RC0b-reality-map.md` (built reality)
**Bar:** Full production — end-to-end evidence, NOT a green unit test. Believe wiring + file:line, never comments/prose.

**Built-state classes:** `Real+Wired+E2E` · `Real-unwired` · `Partial` · `Stub` · `Missing` · `Unknown`

---

## Sub-area 1 — Multi-Tenancy (E13)

> All rows carry their RC0a dependency-tag. The E9 **seam half** of multi-tenancy is `technical` (and largely **built**); the E13 **product half** is `E13-gated` on issue #17 (buyer discovery) and is mostly **Missing** or **Partial**.

| Requirement-ID / Topic | Intent | Built-state class | Evidence (file:line / none) | Gap | Dep-tag |
|---|---|---|---|---|---|
| **E9.1 dual-verdict** (seam) | Every rule returns legal-baseline + tenant-profile verdict, independently readable; ID distinct from Fail/Pass | **Real+Wired** | `RuleFinding.cs` (LegalBaselineVerdict + Verdict); `VerdictSummary.cs:117-162` (LegalBreachCheckIds / TenantOnlyFailCheckIds / LegalBaselineSignal); 468/468 Validation tests | None for the seam; never exercised E2E with a non-baseline profile (no real tenant ever instantiated) | technical |
| **E9.3-AC2 baseline-locked enforcement** (seam) | Overlay loosening a baseline-locked rule / pushing below legal floor → config **rejected** with Result failure | **Real+Wired** | `TenantProfileResolver.cs:76-90` (BaselineLocked always rejected); `:111-139` (TenantTightenableOnly refuses loosening); `:167-178` (MinFieldConfidence legal floor 0.8 enforced) | None — refusal logic is real and unit-tested; not exercised E2E | technical |
| **E9.4 typed tolerance bands** (seam) | Per-rule range-bounded tolerances with legal default; override outside range/below floor rejected at config time | **Real+Wired** | `TenantProfileResolver.cs:105-157` (Tolerance.Resolve, OverrideRejected); `ResolvedTenantProfile.GetEffectiveTolerance` | None for seam | technical |
| **FR-39 / E9.3-AC1 tenant overlay (tighten)** | Per-tenant legal-baseline ⊕ overlay applies a tightened threshold; both verdicts still compute | **Partial** | `TenantProfile.cs` (ToleranceOverrides map exists); `TenantProfileResolver.cs` resolves overlays | **Resolver works, but nothing selects a tenant profile at runtime.** Worker wires a single fixed `TenantProfile.LegalBaseline()` singleton (`VeriqanVerdictExtensions.cs:43`); overlay path is exercised only in unit tests | E13-gated |
| **E13.1 embeddable pipeline-gate API/SDK** | Veriqan invocable as an embeddable library/gate in a statement-generation pipeline; Fail→blocking, InsufficientData→non-blocking-flagged, Pass→non-blocking; batch-mode QC alt invocation | **Missing** | `IVerificationPipeline` exists but is internal orchestration; **no public SDK/gate surface, no API contract, no NuGet/library boundary**; Worker has no entry point at all (`Program.cs` = 21 lines, health-only) | No embeddable gate; no host-pipeline failure semantics; no latency budget (PG-20) | E13-gated |
| **E13.2 per-tenant onboarding (config-only)** | New tenant onboarded by configuring a profile (overlay + tolerances + enabled client checks) **without code**; two profiles → different effective rule sets | **Missing** | `TenantProfile` is constructed in C# only; **no config-driven profile loader, no profile store/registry, no `ITenantProfileResolver` selection by tenant id** | No tenant repository, no config binding for profiles, no per-tenant enabled-check toggling | E13-gated |
| **E13.3 traceability-matrix export** | Export every evaluated rule with DOF numeral, verdict (legal + tenant), evidence locator; states engine + Acuerdo version; reproducible | **Partial (seam only)** | `IVecValidationEngine.GetCoverageMap()` returns `(CheckId, DofNumeral)` pairs (`IVecValidationEngine.cs:63`); each rule declares `DofNumeral` (`IVecValidationRule.cs:78`); `DofNumeralRegistryTests.cs` enforces non-empty | **No export surface.** Coverage map is in-memory `(CheckId,DofNumeral)` only — no verdict (legal+tenant), no evidence locator, no engine/Acuerdo version, no file/report generator | E13-gated |
| **E13.4 / AR-9 regulatory-version provenance** | Every verdict stamped with EngineVersion + ReferenceBundleVersion + **Acuerdo edition/date**; older statement vs newer engine detectable | **Partial** | `Finding.cs:63-64` (EngineVersion, required); `Disposition.cs:151-161` (optional EngineVersion + ReferenceBundleVersion) | **No `AcuerdoVersion`/edition/date field anywhere** (grep: none). EngineVersion on Finding is real; ReferenceBundleVersion only on Disposition (optional, nullable); no stamping at verdict aggregation; the "older-vs-newer detectable" claim unverifiable without Acuerdo version | E13-gated |
| **PG-20 pipeline-gate integration contract** | Documented latency budget + host-pipeline failure semantics for inline gate use | **Missing** | none | No integration contract exists (depends on E13.1) | E13-gated |

**Sub-area 1 verdict:** The **E9 seam** (dual-verdict, baseline-locked enforcement, typed tolerances, MinFieldConfidence floor, per-finding DOF numeral) is genuinely **built, wired, and unit-tested** — this is the real, completed half. The **E13 product half** (runtime tenant selection, config-only onboarding, traceability-matrix export, Acuerdo-version provenance, embeddable gate/SDK) is **Missing/Partial** and gated on issue #17. Critically, even the built seam is **never exercised end-to-end with a non-baseline tenant** because the composition root hardcodes one `LegalBaseline()` singleton.

---

## Sub-area 2 — Observability / Ops Readiness

| Requirement-ID / Topic | Intent | Built-state class | Evidence (file:line / none) | Gap |
|---|---|---|---|---|
| **NFR-4 / PG-13 observability backend** | Logs, metrics, traces ingested by a real observability stack (not stdout); alert rules for error-rate / throughput-drop / SLA-miss | **Stub** | `VeriqanMetrics.cs:23-90` (OTel `Meter` + 3 instruments: duration histogram, processed counter, exceptions counter); registered Singleton (`VeriqanOrchestrationExtensions.cs:88`) | **No exporter wired** — no OTLP/Prometheus/AddOpenTelemetry in Worker (`Program.cs` has none). Meter emits to whatever listener attaches; in production **nothing listens**. No traces (no ActivitySource). **No alert rules anywhere** |
| **NFR-4 structured logging / correlation IDs** | Every job/Check/Finding logged with correlation IDs | **Real+Wired** (emit only) | `ObservabilityTests.cs:461-546` confirms `VerificationPipeline.ProcessAsync` opens BeginScope with `VeriqanCorrelationId` (Guid) + `VerificationJobId` | Logging is real but **only inside the pipeline, which the Worker never invokes**; no Serilog sink/aggregation configured in Worker; no log destination |
| **NFR-1 metrics content** | Throughput, latency p95, verdict distribution, exception counts emitted | **Partial** | `VeriqanMetrics.RecordStatement` (duration + verdict-tagged counter), `RecordException`; `BatchReport` computes ThroughputPerSecond + P95 (`ObservabilityTests.cs:287-325`) | Metrics are computed **in batch-processor / pipeline code paths that the Worker never calls**; never emitted in a running process |
| **PG-12 real health/readiness probes** | `/health/live` + `/health/ready`; ready probe reflects actual pipeline state | **Stub** | `Program.cs:10-11` — `/health` and `/health/live` both return hardcoded `{ status = "Healthy" }` | **No `/health/ready`.** Both probes are constant literals — not wired to DB connectivity, legal-baseline seed state, or any component health. Trivially always-green |
| **PG-11 deploy artifacts** | Repeatable deployment package / container image; no hardcoded dev paths | **Missing** | `git ls-files` Veriqan: **no Dockerfile, no docker-compose, no .yml/.yaml, no k8s manifest** anywhere in the Veriqan tree | No container image, no deploy package, no CI publish target for Veriqan.Worker |
| **Config story (appsettings / secrets)** | Externalized config; connection string, SMTP, encryption key, CSV root supplied | **Missing** | Worker project contains **only `Program.cs` + `.csproj`** (`git ls-files`) — **no appsettings.json, no appsettings.*.json, no env template** | With no config: `VeriqanDb` connection absent → **silently falls to in-memory persistence** (`VeriqanOrchestrationExtensions.cs:73-85`); CSV root unset → reference data unresolvable; SMTP unset → alerts can't send; encryption key absent → SQL path would throw at startup. Worker boots into a non-functional in-memory shell |
| **NFR-3 / PG-17/18/19 failure modes** | Single failure never halts batch; exception queue; graceful degrade; downstream-outage parks cleanly | **Partial** | `BatchProcessor` bounded concurrency + exception counter (`ObservabilityTests.cs:243-281`); `Result<T>` throughout; `SmtpEmailSender` Polly retry | Real in code, **never run at volume or against malformed PDFs / downstream outage in a deployed process**; only test-harness coverage |
| **PG-14 runbook** | Documented runbook: batch init, exception-queue triage, resume, ref-data update, on-call escalation | **Missing** | none found | No operational runbook for Veriqan |
| **PG-15 / PG-16 / NFR-1 SLA + throughput** | NFR-1 p95 ≤ 10 s / ≥ 1 stmt/s/worker measured on real volume; monthly sample within 3–5 day window | **Missing (unverified)** | P95/throughput math exists in `BatchReport`; never measured on real corpus | corpus-gated; no real-volume measurement; SLA surface emits no real values |
| **PG-1 / PG-2 end-to-end composition + ingestion** | Real composition root runs full pipeline against real inputs; production intake wired in Worker | **Missing** | `Program.cs` — no HTTP POST endpoint, no IHostedService, no folder/queue watcher; `IBatchProcessor` wired Singleton but **never invoked** (RC0b §Part 1) | The Worker is an orphaned shell — no operational surface for a statement to enter |

**Sub-area 2 headline:** Observability **types** exist (`VeriqanMetrics` OTel Meter, correlation-ID log scopes, batch P95/throughput) and are real in code, but **nothing exports to a backend, no exporter is wired, no traces, no alerts**. Health probes are **hardcoded literals** (no `/ready`, no real checks). **No appsettings.json, no Dockerfile/compose, no runbook.** The Worker boots into an in-memory shell that cannot process a statement. Ops readiness is effectively **zero deployable surface**.

---

## Sub-area 3 — Security (FLAG ONLY — deep audit is a SEPARATE pass)

> Per owner ruling: surface gaps, severity-tagged, **do NOT deep-dive**. Each row marked **→ defer to dedicated security review**. Severity ∈ {Critical, High, Medium, Low}.

| Topic | Observed state | Severity | Evidence (file:line / none) | Note |
|---|---|---|---|---|
| **Authn/authz on Worker** | **None.** No `AddAuthentication`, `UseAuthorization`, `[Authorize]`, JwtBearer anywhere in Veriqan (grep: 0 files). QA-console disposition actions, batch trigger, marked-PDF download — all unauthenticated by construction (PG-7) | **Critical** | grep over `**/Veriqan*/**/*.cs`: no auth primitives; `Program.cs` has no auth middleware | → defer to security review |
| **Encryption in transit (TLS)** | **None enforced.** No `UseHttpsRedirection` / `RequireHttps` / HSTS in Worker; no TLS config for SMTP alert dispatch or reference-data adapter calls (PG-8) | **High** | `Program.cs` (plain `MapGet`, no HTTPS); grep: no HTTPS primitives | → defer to security review |
| **Encryption at rest — key management** | AES-256 column encryption for legal-baseline tolerances exists (`AesEncryptedDecimalConverter`), but **key is read from plain `IConfiguration["Veriqan:LegalBaseline:EncryptionKey"]`** — no Key Vault, no rotation, no KMS. XML doc *says* "use Key Vault for prod" but **no code path does** (PG-4) | **High** | `ConfigurationCryptoKeyProvider.cs:34-61` (reads base64 from config); `SqlLegalBaselineStore.cs:18-26` (TDE noted as un-enforced OPS step) | Encryption mechanism is real; **key governance is a dev placeholder** → defer to security review |
| **Audit immutability** | Disposition table is **designed** append-only (private EF ctor, `Actor NOT NULL`, doc-asserted insert-only), but **no DB-level grant/trigger enforces it** — relies on convention; not tamper-evident (no hash chain / signing) (PG-5) | **Medium** | `Disposition.cs:10-36` (append-only contract in prose + NOT NULL); no INSERT-only grant or tamper-evidence in migrations | Design intent good; enforcement is convention-only → defer to security review |
| **Secrets handling** | Connection string, SMTP creds, AES key all flow through `IConfiguration` with **no secrets manager, no `dotnet user-secrets`, no env template, no vault**. No appsettings.json means secrets would land in env vars or inline at deploy with no governance | **High** | `VeriqanOrchestrationExtensions.cs:73` (raw `GetConnectionString`); `ConfigurationCryptoKeyProvider.cs:38`; no secret-store wiring | → defer to security review |
| **PII handling (PAN/card numbers)** | No evidence of card-number masking in logs/UI; `PdfPigStatementFieldExtractor` extracts card# (16 digits) but no masking policy; PCI-DSS scope unresolved (PG-6, RC0a C5) | **High** | none (absence of masking); RC0a C5 (PCI scope open question) | → defer to security review |
| **CNBV CUB outsourcing regime** | If deployed as third-party processor for a regulated bank, applicable CUB outsourcing provisions are **undocumented and unaddressed in architecture** (PG-9, RC0a C8 data-residency open) | **Medium (compliance)** | none; business-gated (RC0a C8) | → defer to security review |
| **ISO 27001 / SOC 2 trajectory** | No documented control set, no control-gap register, no audit-trajectory artifacts (PG-10) | **Medium (compliance)** | none | → defer to security review |
| **Prisma 3-process split (A1–A6) relation to Veriqan** | Solution-1's security-mandated 3-process split (Downloader/Extractor/Reconciliator, ADR-009) is a Prisma-MVP gate; Veriqan currently a **single-process Worker** with no process isolation between ingestion, extraction, and verdict — same trust boundary handles PDF parse + DB-encryption key | **Medium** | `Program.cs` (monolithic single host); CLAUDE.md A1–A6 context | Veriqan does not (yet) adopt the split; PDF-parsing attack surface shares the process with the encryption key → defer to security review |

**Sub-area 3 headline (flags only):** The three highest are **(1) no authn/authz on the Worker at all (Critical)**, **(2) no TLS / encryption-in-transit (High)**, and **(3) AES key + all secrets read from plain `IConfiguration` with no vault/rotation (High)**. All deferred to the dedicated security review.

---

## Biggest readiness gaps (≤6)

1. **No deployable surface** — Worker is a 21-line health-only shell with no statement-ingestion entry point, no appsettings.json, no Dockerfile/compose. It boots into an in-memory shell that cannot process a statement (PG-1/2/11, blocks everything operational).
2. **No observability backend** — `VeriqanMetrics` Meter + correlation-ID log scopes exist but **no exporter, no traces, no alerts**; metrics are emitted only from code paths the Worker never calls (NFR-4/PG-13).
3. **Health probes are hardcoded literals** — `/health` + `/health/live` return constant `"Healthy"`; no `/health/ready`, no real component checks (PG-12).
4. **E13 product half entirely Missing/Partial** — no runtime tenant selection (single hardcoded `LegalBaseline()`), no config-only onboarding, no traceability-matrix export, no Acuerdo-version provenance, no embeddable gate/SDK (E13.1-13.4; gated on issue #17).
5. **No authn/authz and no TLS on the Worker (Critical/High security)** — disposition, batch trigger, marked-PDF download all unauthenticated; no encryption in transit.
6. **Secrets/key governance is a dev placeholder** — AES-256 key and all secrets read from plain `IConfiguration`; no vault, no rotation, no secrets manager; audit immutability is convention-only, not DB-enforced.

---

## Per-class tally

| Built-state class | Count | Rows |
|---|---|---|
| **Real+Wired+E2E** | 0 | (no row in this cluster is verified end-to-end in a deployed process) |
| **Real+Wired** (unit-tested, not E2E) | 4 | E9.1 dual-verdict; E9.3-AC2 baseline-locked enforcement; E9.4 typed tolerances; NFR-4 structured logging/correlation (emit-only) |
| **Real-unwired** | 0 | — |
| **Partial** | 7 | FR-39/E9.3-AC1 overlay; E13.3 traceability (seam only); E13.4/AR-9 provenance; NFR-1 metrics content; NFR-3/PG-17-19 failure modes; (security) audit immutability; (security) encryption-at-rest key mgmt |
| **Stub** | 2 | NFR-4/PG-13 observability backend; PG-12 health probes |
| **Missing** | 9 | E13.1 SDK/gate; E13.2 onboarding; PG-20 integration contract; PG-11 deploy artifacts; config/appsettings; PG-14 runbook; PG-15/16 SLA; PG-1/2 composition+ingestion |
| **Unknown** | 0 | — |

> Security sub-area rows are severity-tagged rather than build-classed (flag-only pass); the two security items that map to a build class (audit immutability, encryption-at-rest key mgmt) are counted under Partial above.

---

*This trace covers the M-T / Ops / Security-flag cluster only. The deep security audit is a separate pass (RC owner ruling). E13-gated rows remain out of scope until issue #17 (buyer discovery) clears.*
