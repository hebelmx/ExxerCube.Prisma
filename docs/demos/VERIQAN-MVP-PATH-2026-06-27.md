# Veriqan VEC — MVP / Demo-Readiness Path (commit-log-verified)

**Date:** 2026-06-27 · **Branch:** `Liv` · **Author:** Claude Code
**Supersedes:** `RC4-VERIQAN-READINESS-MATRIX.md` (2026-06-18) *and* the first-pass W0 agent
re-trace (which trusted RC4's stale file:line claims and under-reported Wave-1).
**Basis (ground truth):** the **git commit log** on `Veriqan.*` paths, cross-checked against
current rule bodies. The matrix lied; the first re-trace lied; the commits don't.

---

## 1. The actual state — read the commits, not the matrix

Since RC4 (2026-06-18), **two full waves landed and were adversarially reviewed**:

**Wave-0 (deployability) — DONE:** `POST /verify`+`/batch` (`3957b3c6`), persist verdict+findings
(`61dd6371`), report+notify stages wired (`4ded1760`), appsettings + loud config validation
(`7160806a`), real health checks (`fe936377`), OTel/OTLP + Serilog spans (`6fbba6f5`),
Dockerfile + docker-compose (`7ae1bda4`), streaming batch + poison-PDF safeguards (`d2dd7a82`),
extraction-coverage floor (`8cb842b5`).

**Wave-1 (correctness — every Tier-A false-RED) — DONE:**

| Gap | Fix commit | Current evidence |
|---|---|---|
| #1 scanned/image-only false-RED | `60fe635d` text-layer-density abstain | `VerificationPipeline` density guard |
| #2 §20 saldo-a-favor sign | `d0ab2485` sign-convention guard | `Section20PaymentDistributionRule` |
| #3 §16 column-map | `8df83b01` column-count **abstain** guard | `Section16OtherCreditLinesRule.cs:196–202` |
| #4 FR-12 catalog-image (pHash) | `dd9a3b7a` rule built (+`81c9e328` color-type) | new `Cl*CatalogImage*` rule |
| #5 CL-35 Aptos font | `8ba45202` prefix match + `IsEmbedded` (+`99382e4a`) | `Cl35FontComplianceRule.cs:157–167`; `FontUsage.IsEmbedded` |
| #6 CL-34 card-in-image | `391e7426`+`d70e4571` page-1 propagation + masked + collision | `Cl34CardNumberPresenceRule.cs:117–153` |
| #7 CL-31 pagination | `f15d4f10` footer-band anchor + last-match (+`81c9e328` recall) | extractor `:3029` `footerMatches` |
| N2 batch backpressure | `d2dd7a82` streaming processor | `BatchProcessor` |
| (extra) CL-48, es-MX numbers, year-repair determinism, VerdictAggregator abstain, marked-PDF rotation/Y-flip, dup-alert guard, dedup concurrency, IVA→config | `7e771d2c`,`7341b5f0`,`5771e227`/`aa998bdd`/`923c8b91`,`dcc3ec73`,`f9d5e8e1`/`1adfcbc9`,`5e1eea27`,`e9fc1287`/`6e429a05`,`c9640ef9` | — |

**Bottom line:** the demo's **functional and correctness blockers are essentially closed.**
What's left is **data, presentation, and production hardening.**

---

## 2. What is GENUINELY still open (verified 2026-06-27)

### Tier A′ — demo gating, but data/presentation not correctness
| # | Gap | State | Evidence | Eff | Owner |
|---|-----|-------|----------|-----|-------|
| 13 | **Real reference bundle** for the (anonymized) demo bank — rates/products/legends/tolerances/prior-statements. Wiring done (`d5636c6d` binds CSV root from config); the **data** is missing. | data-gated | `CsvReferenceDataAdapter`; `AddVeriqan` root binding | L | **owner (in progress)** |
| W-UI | **Visual demo UI** — no Razor/Blazor surface exists for Veriqan at all. | greenfield | `git ls-files`→0 Veriqan UI files | M–L | build |
| W4 | **`VecChecklistDemoE2ETests`** deterministic proof over the 4-fixture corpus. | to author | — | M | build |
| W5 | **Capture runbook** + recording. | to author | — | S–M | build |

### Tier B — full-production readiness (genuinely open)
| # | Gap | State | Evidence | Sev | Eff | Dep |
|---|-----|-------|----------|-----|-----|-----|
| 17 | **No authn/authz** on `/verify` + `/batch`. | OPEN | `Program.cs` (grep: no `Authoriz*`/`Authentic*`/`JwtBearer`) | B | M | tech |
| 20 | **No TLS/HSTS; SMTP `EnableSsl=false`.** | OPEN | `Program.cs` (no `UseHttpsRedirection`/`UseHsts`) | B | S | tech |
| N3 | **No CORS / origin validation.** | OPEN | `Program.cs` (no `Cors`) | D | S | tech |
| 15 | **Result/reprocess/job/disposition stores default to in-memory** — lost on restart. (EF impls exist for disposition+verdict persistence; result/reprocess have no EF impl.) | OPEN | `VeriqanOrchestrationExtensions.cs:116–141` (all `InMemory*`) | B | M | tech |
| 19 | **Audit immutability is app-convention only** — `EfDispositionRepository` exists but no DB ledger/trigger/temporal table/deny-grant. | OPEN | `Persistence/...EfDispositionRepository.cs`; no trigger/ledger migration | B | M | tech |
| 21 | **Secrets plaintext; AES-CBC, no key-id/rotation/Key Vault.** | OPEN | `ConfigurationCryptoKeyProvider.cs` | B | L | business-gated |
| 22 | **No LFPDPPP retention/erasure/ARCO regime.** | OPEN | (absent) | B | L | business-gated |
| 25 | **No operational runbook** (batch start, exception triage, bundle update, resume, on-call). | OPEN | (absent) | D | S | tech |
| 26 | **No fail-open/closed policy / circuit-breaker** at host→gate boundary. | OPEN | (absent) | D | M | tech |
| N1 | **Password-protected PDFs silently fail** — `PdfDocument.Open(bytes)` no password param. | OPEN | `PdfPigStatementFieldExtractor.cs:~483` | D | M | tech |
| N4 | **No `ef migrations bundle` CLI** — migrations run at host boot. | OPEN | `VeriqanLegalBaselineStartupService.cs:~63` | D | S | tech |

### Tier D — post-MVP
| # | Gap | Dep |
|---|-----|-----|
| 2 | §20 sign-convention — abstains now; confirm vs a real §20 specimen | corpus |
| 27 | NFR-1 p95 ≤ 10 s — unproven without real load test | corpus |

---

## 3. Corrected execution path

**Stage 1 — Demo (functional blockers already closed):**
1. **W3 corpus + reference bundle** *(critical path — owner supplying anonymized statements;
   author a real/realistic bundle for the anonymized bank).* The remaining true blocker.
2. **W1 verify** — one fixture ingest → verdict → marked PDF → DB rows → audit, no shims. *(S)*
3. **W4** `VecChecklistDemoE2ETests` over the 4-fixture corpus. *(M)*
4. **W-UI** Blazor presentation surface over real pipeline output. *(M–L)*
5. **W5** capture runbook + record captures 0–5. *(S–M)*

**Stage 2 — Full-production readiness (parallel; no longer correctness work):**
6. Security: #17 auth → #20 TLS+SMTP-TLS → N3 CORS.
7. Durability/compliance: #15 EF result/reprocess stores → #19 DB-enforced audit ledger →
   N1 password-PDF → N4 migration bundle → #21 secrets / #22 LFPDPPP (design; business-gated).
8. Ops: #25 runbook → #26 fail-open/closed policy.

**Stage 3 — Post-MVP:** #2 §20 corpus confirm · #27 load test (both corpus-gated).

---

## 4. Business-gated unlocks (escalate, don't block the demo)
- Real CONDUSEF corpus for threshold **calibration** (issue #17 / E13 buyer gate).
- Real reference-bundle ownership/sign-off (relates to #13; demo uses the anonymized stand-in).
- Key Vault (#21) and LFPDPPP (#22) — legal + infra owners.

---

## 5. GO/NO-GO gates for the recording
| Capture | Depends on | Gate |
|---|---|---|
| 2 — GREEN | W3 GREEN fixture + bundle | compliant statement returns GREEN (cardinal false-REDs already fixed) |
| 3 — RED | marked-PDF stage (CLOSED) | marked PDF highlights CL-21/CL-35 at correct locators |
| 4 — BLOCKED | density guard #1 (CLOSED) | image-only statement returns BLOCKED, not RED |
| 5 — Disposition | W-UI + audit | append-only audit row written and visible |

> **Process note for this whole effort:** three successive "current-state" sources
> (RC4 → first W0 agent re-trace → my own assumptions) each under-reported how much was
> done. The reliable ground truth was the **git commit log + the actual rule bodies**.
> Anchor future status passes there first.
