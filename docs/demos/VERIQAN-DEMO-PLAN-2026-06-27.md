# Veriqan VEC — Demo Plan (Production-Readiness Forcing Function)

**Date:** 2026-06-27 · **Rev:** v2 (owner decisions locked + W0 ground-truth re-trace done)
**Branch:** `Liv` · **Author:** Claude Code (planning)
**Companion:** `VERIQAN-MVP-PATH-2026-06-27.md` (the ordered, W0-verified gap closure list).

---

## 0. Why this document exists

The Prisma client demo was our most effective production-readiness driver: preparing a
faithful end-to-end client capture **forced real gaps to the surface** and gave us a tracked
loop to close them. We are deliberately repeating that playbook for **Veriqan VEC** (the
bank-statement quality verifier).

| Prisma artifact | Veriqan analogue | Status |
|---|---|---|
| 5-phase MVP audit method | reuse the method (§6) | reused |
| `RC5-PRISMA-READINESS-MATRIX.md` | `RC4-VERIQAN-READINESS-MATRIX.md` (2026-06-18, **superseded by W0 re-trace**) | ⚠ stale → see §3 |
| `MVP-PATH-2026-06-11.md` | **`VERIQAN-MVP-PATH-2026-06-27.md`** | ✅ authored (W0) |
| `CLIENT-DEMO-CAPTURE-RUNBOOK-2026-06-15.md` | `VERIQAN-DEMO-CAPTURE-RUNBOOK.md` | to author (W5) |
| `DemoChecklistSevenStepsTests` | `VecChecklistDemoE2ETests` | to author (W4) |
| Rich Blazor demo pages | **Veriqan visual demo UI** | to author (W-UI) |
| Named fixture corpus + intentional mismatch | anonymized-real 4-PDF corpus inc. a GREEN case | to author (W3) |

---

## 1. Locked decisions (owner, 2026-06-27)

1. **Demo surface = hybrid: deterministic E2E test *plus* a visual UI.**
   A deterministic `VecChecklistDemoE2ETests` drives the pipeline and guarantees the data is
   real (not staged). **And** a **visual Blazor demo UI presents the processing flow** so the
   mixed stakeholder audience (technical + legal + accounting + financial) can *see* how each
   statement is verified. The UI need **not be production-live** — it is a presentation
   surface over real pipeline output. The test proves it's real; the UI makes it legible.
2. **Audience = banking client capture (like Prisma).** Recorded clips, Spanish narration,
   each segment mapped to the **55-item VEC checklist** (CL-1…CL-55).
3. **Corpus = anonymized real data (preferred) + synthetic fallback.** Owner will supply
   **real statements; we anonymize the data but keep it derived from real documents** for
   fidelity. Synthetic authoring is the fallback for any case (e.g. a clean GREEN, an
   image-only BLOCKED) not covered by the supplied set.
4. **Readiness bar = full (like Prisma).** Demo prep drives the whole verified gap list:
   correctness (kill false-REDs), security (auth/TLS/secrets), durable persistence, audit
   immutability, ops — not just a happy path. Tracked in `VERIQAN-MVP-PATH`.

---

## 2. What Veriqan is (context for the narrative)

**VEC = Verificación de Estado de Cuenta** — automated quality verification of bank
credit-card statements against a **55-item regulatory checklist** (CL-1…CL-55). Mexican
banks manually verify a sliver of 13–20M monthly statements; Veriqan automates that gate and
**degrades gracefully** (abstains / routes to a human; never silently auto-rejects). It is an
additive module on ExxerCube.Prisma (`Veriqan → Prisma`, separate `veriqan.*` schema).

**Pipeline (`Veriqan.Orchestration/…/VerificationPipeline.cs`):**
`Ingestion → Extraction → Binding → Validation → Verdict → Persist → Report(marked PDF) →
Notify(email) → Disposition`. Verdict precedence **BLOCKED → RED → GREEN**;
`InsufficientData` never escalates to RED (abstain-safety).

---

## 3. Current state — commit-log-verified (2026-06-27)

> **RC4 (2026-06-18) AND my first W0 agent re-trace are both superseded by the git commit
> log.** Two waves landed and were adversarially reviewed; the demo's **functional +
> correctness blockers are essentially closed.** Commit-by-commit evidence in
> `VERIQAN-MVP-PATH-2026-06-27.md` §1. Headline:

**Now CLOSED — Wave-0 (deployability):** `POST /verify`+`/batch`, pipeline stages 8–10
(persist/marked-PDF/email), verdict+finding persistence, appsettings + config validator,
Dockerfile + compose, OpenTelemetry + Seq, real health checks, streaming batch.

**Now CLOSED — Wave-1 (every Tier-A false-RED, all reviewed):** #1 scanned→BLOCKED density
guard (`60fe635d`), #2 §20 sign (`d0ab2485`), #3 §16 column-count abstain (`8df83b01`),
#4 FR-12 catalog-image pHash rule (`dd9a3b7a`), #5 CL-35 Aptos prefix-match + `IsEmbedded`
(`8ba45202`), #6 CL-34 card page-1 propagation + image-only abstain (`391e7426`/`d70e4571`),
#7 CL-31 footer-band pagination (`f15d4f10`), N2 streaming batch (`d2dd7a82`), plus CL-48,
es-MX numbers, deterministic year-repair, VerdictAggregator abstain, marked-PDF rotation.

**Genuinely still OPEN — data + presentation (demo-gating, not correctness):**
- **Real reference bundle (#13)** — wiring done (`d5636c6d`); the *data* for the anonymized
  bank is the true remaining blocker. **(owner supplying)**
- **Visual demo UI (W-UI)** — greenfield; no Veriqan Razor/Blazor surface exists.
- **`VecChecklistDemoE2ETests` (W4)** and **capture runbook (W5)** — to author.

**Genuinely still OPEN — production hardening (full-readiness bar):** no auth (#17), no
TLS/HSTS + SMTP TLS off (#20), no CORS (N3), in-memory result/reprocess stores (#15), audit
immutability app-convention only — no DB ledger/trigger (#19), plaintext secrets/Key Vault
(#21, business-gated), LFPDPPP retention (#22, business-gated), password-PDF silent fail (N1),
migration-bundle CLI (N4), ops runbook (#25), fail-open/closed policy (#26).

**Business-gated (call out, don't block the demo):** E13 buyer gate (issue #17) and a real
CONDUSEF corpus for threshold calibration. The demo runs on anonymized-real + synthetic by design.

---

## 4. The demo narrative (banking client, mapped to the checklist)

Mirrors Prisma's *etapa → capture*. The **E2E test produces the artifacts; the visual UI
displays them**; narrate in Spanish, tie each segment to checklist items. Record deterministic
segments first, any live bit last (Prisma ordering lesson).

| Capture | Story segment | Checklist tie | On-screen (UI view over real output) |
|---|---|---|---|
| **0 — Panorama** | Regulatory problem (13–20M/month, manual gate); the 55-item checklist. | CL-1…CL-55 overview | Checklist + flow diagram |
| **1 — Ingesta + Extracción** | Submit statement; hash; extract headers/period/balances/movements. | CL-1, CL-2…CL-9 | UI: uploaded statement + extracted-fields panel |
| **2 — Verificación GREEN** | The **compliant** statement passes all 55 checks → **GREEN**. | all CL pass | UI: 55-check grid all green + GREEN verdict |
| **3 — Hallazgos (RED)** | Defective statement: CL-21 arithmetic + CL-35 font fail; **marked PDF highlights the exact spots**. | CL-21, CL-35 | UI: failed checks + **marked PDF** side-by-side |
| **4 — Degradación segura (BLOCKED)** | Image-only statement → **BLOCKED/InsufficientData**, never false-reject; routed to human. | abstain discipline | UI: BLOCKED banner + reason + "needs human" |
| **5 — Disposición + Auditoría** | Analyst dispositions the finding; append-only audit row; verdict persisted. Close with the **green E2E run** as proof. | FR-16, FR-18 | UI: disposition action + audit trail; terminal: green test |

**Threaded messages (Prisma spirit):** full traceability (hash → verdict → audit), defensive
intelligence (graceful degradation, never silent reject), real infrastructure (real
PdfPig/PdfSharp/EF Core), calibration-ready (every finding captured for future tuning).

---

## 5. The demo corpus (Decision 3 — anonymized real)

Small, named, honest. Owner supplies real statements → **we anonymize (mask PAN, names,
account numbers) while preserving layout/fonts/geometry** (so CL-34/CL-35/CL-31 behave as on
real input). Synthetic authoring fills any missing verdict class.

| Fixture | Source | Expected verdict | Drives capture |
|---|---|---|---|
| `good.pdf` | anonymized real compliant (or authored) | **GREEN** | 2 |
| `bad-math.pdf` | anonymized real / existing Dummie | **RED** (CL-21) | 3 |
| `bad-font.pdf` | anonymized real / existing Dummie | **RED** (CL-35) | 3 |
| `scanned.pdf` | image-only (authored if not supplied) | **BLOCKED** | 4 |

> **Anonymization = substitution with COHERENT FAKES, not redaction (owner requirement).**
> Replace each sensitive real value with a shape-valid, internally-consistent fake (PAN keeps
> network/BIN + Luhn; valid RFC/CLABE; consistent across a product's 4 months so reconciliation
> continuity holds) so the statement stays a **complete, processable** document — Veriqan needs
> the fields populated to verify. Rationale: legally we can't hold or share real statements on a
> zero-trust platform. Mechanism: *redact the real glyphs (truly remove — white-rect cover is NOT
> enough; `pdftotext` must never read the real value) then re-render the fake in place.*
> **Gate:** zero real last-4 in card-shaped tokens AND the fake last-4 present (proves replace,
> not delete). Balances/amounts/dates stay unchanged (arithmetic must still validate).
>
> **Fidelity matters more here than for Prisma:** several cardinal rules (CL-34 card, CL-35 font,
> CL-31 pagination) are layout/graphics-sensitive — preserve *rendering* while swapping *content*.
> `scanned.pdf` doubles as the regression fixture proving cardinal gap #1 stays BLOCKED (not RED).

### 5a. Verified corpus inventory (2026-06-27)

Owner supplied 20 real Banamex PDFs (in `/home/abel/Downloads`, **never** in the repo).
Classified + deduped (verified by normalized-text fingerprint) → **12 unique statements =
3 distinct products × 4 consecutive months**, organized at
`/home/abel/Downloads/bank-statements-sorted/` (manifest: `statement-manifest.json`, local only):

| Product (label) | Type | Months | Masked acct |
|---|---|---|---|
| account-A-priority | **Cuenta Priority (checking)** | 2026-02 → 05 | ****0003 (fake) |
| account-B-visa | **Visa credit card** | 2026-03 → 06 | ****0001 (fake) |
| account-C-mc | **Mastercard credit card** | 2026-02 → 05 | ****0002 (fake) |

> Note: VEC's 55-item checklist targets **credit-card** statements → B and C are the natural
> SUTs; A (checking) is a distinct product the bank also wants processed.

### 5b. Reconciliation-from-neighbors — the anti-tautology design (owner methodology)

The bank's production system is *given* the expected reference data per the checklist. We don't
have it for these specimens. Naively asserting "the statement contains X" using X we injected
would be **tautological**. Instead:

- For each product, the **intermediate month is the SUT** (System Under Test); each statement is
  independently a SUT for the system.
- The **prior month (m−1) and next month (m+1)** are deeply analyzed to **reconstruct the
  reference bundle** (prior/closing balances for continuity, products, rates, sequential images,
  mandatory legends, etc.). The verification of month *m* draws its ground truth from the
  **neighbours**, *not* from month *m* itself.
- All 3 statements of the product are **anonymized/transformed identically** (same fake
  identity/logo) so continuity across m−1, m, m+1 still holds after anonymization.
- This makes the E2E test a genuine verification of the SUT month, not a restatement of injected data.

Maps cleanly onto the VEC reference bundle (`prior-statements.csv`, `sequential-images.csv`,
products, rates): m−1 closing balance = m opening balance; m+1 confirms m's closing.

---

## 6. Method (reuse Prisma's 5-phase audit)

Reconcile PRD↔discovery → define the demo+MVP bar (§1, §4) → **re-trace ground truth from the
Worker composition root** (done, §3) → adversarially verify each surviving gap → publish the
ordered MVP path (done) and run the **blocker loop**: prep → blocker → root-cause doc → fix →
re-run → next blocker.

---

## 7. Workstreams (revised after W0)

Order: foundational/deterministic first, fragile/cosmetic last. Sizes S/M/L. See
`VERIQAN-MVP-PATH-2026-06-27.md` for per-gap DoD/deps.

- **W0 — Ground-truth re-trace + MVP path.** ✅ **DONE** (this rev + companion doc).
- **W1 — Pipeline spine.** ✅ **Mostly done by Wave 0** (ingest + stages 8–10 wired). *Remaining:*
  a short verification pass that one fixture goes ingest → verdict → marked PDF → DB rows →
  audit with no test-only shims. *(S)*
- **W2 — Cardinal-rule correctness (kill false-REDs).** ✅ **DONE in Wave-1** — #5 CL-35 font,
  #6 CL-34 card, #7 CL-31 pagination, #3 §16 abstain, #1 scanned, #2 §20 all closed + reviewed
  (see `VERIQAN-MVP-PATH` §1). *Remaining:* re-confirm against the anonymized corpus once it lands.
- **W3 — Corpus + reference bundle + reset.** Anonymize owner's real statements (preserve
  rendering); fill missing classes (GREEN, image-only); stand up a **real/realistic reference
  bundle** (#13) so binds succeed; seed/reset recipe between takes. *(M–L; depends on W2 for verdicts)*
- **W-UI — Visual demo UI (Decision 1).** Blazor pages presenting: upload + extracted fields,
  the 55-check grid (green/red/abstain), verdict banner (GREEN/RED/BLOCKED), marked-PDF viewer,
  disposition action, audit trail. Reads real pipeline output; **not production-wired**. Reuse
  Prisma MudBlazor components where possible. *(M–L; depends on W1)*
- **W4 — Deterministic proof test `VecChecklistDemoE2ETests`.** Runs the 4-fixture corpus through
  the wired pipeline; asserts the §4 verdicts + that marked PDF / audit row / disposition exist;
  outputs to a known `exports/` dir the UI and capture consume. *(M; depends on W1–W3)*
- **W5 — Capture runbook + recording.** Author `VERIQAN-DEMO-CAPTURE-RUNBOOK.md` (shot list §4,
  env setup, record order, Spanish narration, GO/NO-GO); record captures 0–5. *(S–M; depends on W-UI, W4)*
- **W6 — Full-readiness hardening (Decision 4; parallel after W1).** Auth/authz (#17), TLS+HSTS
  + SMTP TLS (#20), secrets management (#21), DB-enforced audit immutability (#19), durable EF
  reprocess/result stores (#15), CORS (N3), password-PDF handling (N1), migration-bundle CLI (N4),
  ops runbook (#25). LFPDPPP (#22) and Key Vault (#21) are partly business-gated — design now. *(M–L)*

---

## 8. Blocker loop & tracking

As Prisma: prep the capture; a segment that can't be recorded **is a blocker** → short
root-cause doc → fix → re-run the validation command → next blocker. Track each in
`VERIQAN-MVP-PATH` with: blocker, owner, remediation file, which capture it blocks, GO/NO-GO.
Anticipated Veriqan blockers: cardinal false-REDs (W2), missing real reference bundle (W3),
anonymization that destroys layout fidelity (W3). Specialist help available: a synthetic/
anonymized corpus generator (cf. `siara-corpus-generator`).

---

## 9. Sequencing

```
W0 ✅ ─> W1 verify ─┬─> W2 cardinal fixes ─> W3 corpus+bundle ─┬─> W4 E2E test ─> W5 capture/record
                    ├─> W-UI visual demo UI ──────────────────┘
                    └─> W6 hardening (parallel)
```

**Rough size:** W1 S · W2 M · W3 M–L · W-UI M–L · W4 M · W5 S–M · W6 M–L → ~2–3 focused weeks
to a recordable, full-readiness demo (real-corpus threshold calibration excluded by design).

---

## 10. Status of the original open questions

1. **Visual UI** — RESOLVED: in scope (W-UI); UI presents real pipeline output, not prod-live.
2. **GREEN fixture** — RESOLVED: anonymized real data preferred, synthetic fallback (§5).
3. **Recording** — RESOLVED: same toolchain/audience as the Prisma client capture.
4. **Execute W0 now** — DONE (§3 + companion MVP-path doc).

**New question for the owner:** when can you share the real statements to anonymize (and a real
reference bundle, even partial)? That set is on the critical path for W3 and gates captures 2–4.
```
