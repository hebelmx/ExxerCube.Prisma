# Intended Solution — S4-C: LLM hybrid extractor as a GROUNDED, FIELD-AWARE FALLBACK in the Athena pipeline

Anti-drift reference for the (future, owner-gated) S4-C build. Parent specs:
`spec-llm-hybrid-extractor.md` (S1–S3b contracts), `spec-llm-hybrid-extractor-S4A.md` (eval harness),
`spec-llm-hybrid-extractor-S4B.md` (extractor extension). Governing ADR:
`ADR-024-llm-hybrid-graduation-criteria.md` — **S4-C is "Gate B" and this spec does NOT authorize the flip.**

> **⚠️ RECONCILED 2026-07-19 to ADR-024 Addendum D9 (field-aware trigger + per-field fill-gap).**
> This file previously specified a **whole-extraction-failure trigger** (fire only when the deterministic
> Stage-3 extraction returns `IsFailure`/null) with **whole-record all-or-nothing replace** (any control-field
> mismatch discards *every* LLM field). Orchestrator ground-truth verification (2026-07-19) proved that trigger
> **cannot fire on a per-field null** — `AdaptiveTxtFieldExtractor.ExtractFieldsAsync` returns `Result.Success`
> on any non-empty OCR text, and `MapExtractedFieldsToExpediente` is non-nullable (a missing field becomes
> `string.Empty`, never null). So the old trigger fired only on *no field-extractor / no OCR text / extraction
> exception* — mostly slices where the text-LLM is **also blind** (fed the same empty/absent OCR text) and
> therefore near-vacuous (the one exception — extractor-exception on non-blank text — is disclosed and
> dispositioned in D1). A 3-role design panel (architect/PM/QA) plus an owner ruling settled the corrected
> design, recorded canonically in **ADR-024 Addendum D9**. This spec is now reconciled to that ruling:
> - **Trigger = field-aware, ADDED as an OR-branch** (D9: "the fallback *also* fires"). The fallback fires when
>   `string.IsNullOrEmpty(NumeroExpediente)` **AND** OCR text is present — the regime where the text-LLM has input
>   and which is now the primary value path. Anchor = `NumeroExpediente` **only** (a single named-field boolean;
>   do NOT generalize to a set/count/percentage without a fresh owner ruling — that would reopen D1's rejected
>   fuzzy coverage threshold).
> - **Semantics = per-field fill-gap** (owner-ruled), **replacing** the old whole-record replace. Only the empty
>   `NumeroExpediente` is LLM-eligible; every correctly-extracted deterministic field is left byte-identical. The
>   gate is scoped to the one filled field; on gate failure that field stays empty (abstain → NULL) and the rest
>   of the record is untouched. This aligns S4-C with **ADR-024 D8** (per-field abstention, not all-or-nothing).
>
> The historical whole-record-replace design (2026-07-04b design party) is retained below only where it is still
> valid (D3 thin-service reuse, D4 text-only, D5 wiring safety, D7 shadow-out); the trigger/semantics sections
> (D1/D2/D5-seam/D6/story-plan/tests/Gate-B) are rewritten to fill-gap. Design party session: `Liv`, 2026-07-04b;
> reconciliation: `Liv`, 2026-07-19 (ADR-024 D9).

## Status: DESIGN ONLY — BLOCKED on Gate-B preconditions + owner sign-off (see §7)
> **Update 2026-07-20 (`Liv`):** owner picked **Direction #1** from the S4-M evidence packet — the deterministic
> extractor was hardened for `NumeroExpediente` (whitespace-tolerant delimiter + `I↔l` OCR cleanup, unit-verified
> 214/214) instead of wiring an LLM fallback for it. This closes the **field-level** Gate-B question for
> `NumeroExpediente` as *"fallback not warranted for this field"* (pending a live-pipeline re-measurement). It does
> **not** un-block S4-C generally: the seam, if ever built, targets a field where deterministic is genuinely weak
> (`AutoridadNombre`). See `docs/evaluation/llm-hybrid-S4M-gateb-evidence-packet-2026-07.md` §8.
Today the hybrid path (`IHybridExtractionService`) is reachable **only** from the Web.UI `/hybrid-extraction`
demo page; the Athena worker never calls it. S4-C wires a **field-aware fallback** into the worker. It is
blocked (by design) on the ADR-024 D6 corpus + D7 honesty preconditions + explicit owner sign-off. **No
production code is to be written from this spec without that sign-off.**

## Owner's ratified intent (the contract every decision honors)
1. **Fallback, not replace.** The LLM extractor runs **only when the deterministic extractor leaves the record's
   identity key empty** — i.e. `NumeroExpediente` is empty and OCR text is present. Deterministic stays the
   authoritative primary path; on a deterministic `NumeroExpediente` hit nothing about today's behaviour changes
   (no LLM call, no field touched).
2. **Fill-gap, per field, gated.** The fallback may fill **only the empty `NumeroExpediente`**. A
   correctly-extracted deterministic field is **never** overwritten by an LLM guess. The single recovered value
   is trusted **only if** it passes the control gate (plausibility + source-containment, §D2); on gate failure it
   stays empty (abstain → NULL). No other LLM field (authority, monto, `SolicitudPartes`, …) is taken by this
   seam — see the scope note in D3.
3. **Prod-path but SAFE.** Ships on the production composition root, **dark by default**, zero regression: with
   the flag off the fallback dependency is null and the field-aware branch short-circuits, leaving output
   byte-identical to today.

## Why (confirmed from code — verified file:line)
- Worker composition root `04 Services/Athena/Prisma.Athena.Worker/Program.cs` builds DI **manually**,
  registers deterministic `IFieldExtractor<TxtSource>` → `AdaptiveTxtFieldExtractor` (~`Program.cs:174`), and
  never binds `LlmProvidersOptions` or registers `IHybridExtractionService`.
- Live extraction path: `AthenaWorkerService` → `ExtractionPipelineService.cs:164` →
  `ExtractionOrchestrator.ExtractAsync` → Stage 3 `BuildPdfExpedienteFromOcrAsync` → the deterministic call at
  **`04 Services/Athena/Prisma.Athena.Processing/ExtractionOrchestrator.cs:392`**
  `_txtFieldExtractor.ExtractFieldsAsync(txtSource, fieldDefinitions)` → `MapExtractedFieldsToExpediente`
  (~`:552`) → `_fusionService.FuseAsync` (~`:313`).
- **Trigger reality (D9, verified):** `ExtractFieldsAsync` returns `Result.Success` whenever OCR text is
  non-empty; `MapExtractedFieldsToExpediente` (`:552-570`) is **non-nullable** and sets
  `NumeroExpediente = fields.Expediente ?? string.Empty` (`:558`). So the mapped `Expediente` is **never null**
  on the success path, and a missing expediente surfaces as `NumeroExpediente == string.Empty` — **not** as the
  `:393` failure return. The old failure branch at `:393` (`return (null, new ExtractionMetadata())`) fires only
  on three sub-cases: *no field-extractor*, *no OCR text*, or *an extraction exception*. On the first two the
  text-LLM is blind too (it would be fed the same empty/absent OCR text). The third — **extraction exception on
  non-blank OCR text** — is the one narrow sub-case where OCR text *does* exist and the text-LLM *would* have
  input (D9's architect explicitly flagged this as "one narrow live exception — rare"); it is disclosed and
  dispositioned in D1 below, not silently dropped.
  ⟹ the meaningful fallback seam is **after mapping**, gated on `string.IsNullOrEmpty(mapped.NumeroExpediente)`
  **AND** OCR text present — not inside the `:393` branch.
- Reused primitives already exist: `ILlmExpedienteExtractor<TxtSource>` (LLM-text track, returns full
  `Expediente` incl. `SolicitudPartes` — of which this seam consumes **only** `NumeroExpediente`),
  `LlmExtractionGate` (per-field plausibility for expediente/RFC/CURP/monto), `LlmProvidersOptions` (runtime
  Ollama/Gemini).

## Converged design (party-locked; trigger/semantics reconciled to ADR-024 D9)

### D1 — Trigger (Q1): field-aware on `NumeroExpediente`
Fallback fires **iff** the deterministic Stage-3 extraction produced a record whose identity key is empty **and**
there is OCR text to work with:

```
fire  ⟺  string.IsNullOrEmpty(mapped.NumeroExpediente)  &&  !string.IsNullOrWhiteSpace(ocrResult.Text)
```

- **Anchor field = `NumeroExpediente` ONLY.** A single named-field boolean stays crisp and 2-fixture testable and
  therefore does **NOT** reopen the previously-rejected "fuzzy partial-but-thin coverage/percentage threshold."
  Do **not** generalize the anchor to a set/count/weighting without a fresh owner ruling (ADR-024 D9). The line is
  crossed the moment "mandatory field present" becomes a *cardinality or weighting* decision.
- **This is ADDITIVE, per D9** ("add an OR-branch to D1 … the fallback *also* fires"). The field-aware condition
  above is a NEW branch added to D1, not a replacement of the old `IsFailure`/null condition. The **primary value
  path** is the field-aware branch, because it is the only regime that both fires often (deterministic left the
  anchor empty on a document it otherwise read) AND gives the text-LLM real input (OCR text present).
- **Seam location** for the field-aware branch is **after `MapExtractedFieldsToExpediente`** (where
  `NumeroExpediente` exists as `string.Empty`), on the success path, before fusion consumes the record — not
  inside the old `:393` failure branch.
- **The old `:393` failure branch is retained behaviorally as-is.** On its *no-field-extractor* and *no-OCR-text*
  sub-cases the text-fallback is near-vacuous (no input) and this spec does not route those through the fallback.
  The remaining *extraction-exception-on-non-blank-text* sub-case (D9's flagged "narrow live exception") is where
  OCR text exists and the LLM would have input — but there the deterministic `Expediente` is **null** (whole
  mapping never ran), so there is no populated record for fill-gap to preserve. **Disposition (disclosed, not a
  silent narrowing):** routing that rare case is DEFERRED to the S4-C build as an explicit open sub-item — it
  needs a small decision (recover `NumeroExpediente` into an otherwise-empty record vs. keep today's abstain),
  which is a build-time call, not settled here. It is called out so the omission from the primary value path is
  a *documented* deferral, honoring D9's additive intent, rather than an undisclosed scope cut.
  (A vision fallback could also see the image on these branches, but vision is OUT — D4.)

### D2 — Control gate on the recovered field (Q2) — TWO checks, both must pass, scoped to `NumeroExpediente`
The fallback calls the LLM-text extractor and takes **only** its `NumeroExpediente`. That single value is written
into the record **only if both** checks pass; if **either** fails, the value is discarded and `NumeroExpediente`
stays empty (abstain → NULL) — **the rest of the deterministic record is unaffected either way** (fill-gap):
- **(a) Plausibility** — reuse `LlmExtractionGate`'s existing expediente-format check (ADR-024 D8). No new regex.
- **(b) Source-containment** — the returned expediente string must appear in the **raw OCR source text**
  (`txtSource`) after normalization (case-insensitive, whitespace-collapsed, hyphen/dash-normalized). **No
  fuzzy / Levenshtein / semantic matching** — verbatim-after-normalization only.

**Why (b) is IN scope, not gold-plating** (John + Mary): by the time the fallback fires, deterministic left
`NumeroExpediente` empty, so there is nothing to match against *except the source*. Plausibility alone only proves
the LLM produced something *shaped* like an expediente — trivially satisfiable by a hallucination or by an echo of
the **unreliable companion JSON/XML** (which is *input, never an oracle* — [[prisma-domain-3-docs-unreliable]]).
Source-containment is D7.1's eval-time discipline moved to inference time, and it is nearly free (a substring
check on text already in memory). **The risk is asymmetric:** a false *reject* → abstain → `NumeroExpediente`
stays empty → **behaves exactly like the deterministic-empty record does today → zero regression by
construction**; a false *pass* (a hallucinated/companion-sourced expediente stamped into a legal record) is the
**only** outcome that makes prod worse than today. Gate hard on the safe side.

**No "whole-record discard" language remains** (the old all-or-nothing rule): under fill-gap the seam only ever
takes one field, so "discard the rest" is degenerate — the deterministic fields were never candidates for
replacement. The gate governs a single write, not a record-level accept/reject.

Known false-reject zone (accepted): docs where the correct expediente is present but **OCR-mangled** — the same
corruption that beat the deterministic extractor may beat a verbatim match. Cost of that = the fallback abstains
(safe, field stays empty). This is the deliberate trade.

### D3 — Reuse vs new (Q3): NEW thin worker service, reuse the *components* not the *orchestration*
Do **NOT** call `HybridExtractionService` from the worker. It runs *all* tracks unconditionally and merges
per-field via `ExtractionReconciler` — a demo-comparison policy that invokes the LLM on every document (including
the success path, contradicting "fires only when the anchor is empty"). Wrapping it to isolate a single field is
wrapping a mismatched abstraction.

**Build a thin worker-facing service** (name TBD at build, e.g. `IDeterministicFallbackLlmExtractor` /
`LlmFallbackNumeroExpedienteExtractor`) that owns its own field-gate policy and reuses:
- `ILlmExpedienteExtractor<TxtSource>` — the LLM-text track only.
- `LlmExtractionGate` plausibility (expose the single-field expediente check narrowly if not already public; do
  **not** alter its per-field abstention used elsewhere).

Proposed contract (recovers a single field, not a whole record — the fill-gap shape):
```csharp
public interface IDeterministicFallbackLlmExtractor
{
    // Returns Success(numeroExpediente) only when BOTH gates pass on that value;
    // Success(null) on any abstain/discard/failure/timeout; Cancelled on cancellation.
    // NEVER throws for business logic. The caller fills ONLY NumeroExpediente with the returned value.
    Task<Result<string?>> TryRecoverNumeroExpedienteAsync(
        TxtSource source,
        IReadOnlyList<FieldDefinition> fieldDefinitions,
        CancellationToken cancellationToken = default);
}
```
Impl steps: cancel-check → call LLM-text extractor (any exception/timeout ⇒ `Success(null)`, never throw) → take
its `NumeroExpediente` → plausibility gate → source-containment gate → both pass ⇒ `Success(value)`, else ⇒
`Success(null)` (abstain). ~50–70 LOC. (Exact return shape — bare `string?` vs a small fill-instruction record —
is a build-time detail; the invariant is "one field in, one gated field-value out, deterministic fields never
touched.")

> **Scope consequence of fill-gap (flag for the owner):** the LLM-text extractor also returns `SolicitudPartes`
> (which deterministic cannot populate). Under D9's "only the empty `NumeroExpediente` is LLM-eligible" ruling,
> this seam does **NOT** take partes (or authority, monto, RFC, CURP). So the worker fallback is now a
> **single-field identity-key recovery**, not a partes-filler. Consequence: it cannot regress `SolicitudPartes`
> (it never writes it) — the ADR-024 D2 partes-no-regression Gate-B item is satisfied by construction. Widening
> the eligible-field set (e.g. to also fill partes when deterministic leaves them empty) is a **separate future
> ruling**, subject to the same "don't generalize without an owner decision" discipline as the trigger anchor.

### D4 — Vision (Q4): TEXT-ONLY
No vision track in the worker. Owner froze LLM-vision for the demo (`2c93d06c`/`37c2d4d1`); a fallback path meant
to be the *safe* option should not spend the vision latency/token budget, and the evidence gives no vision-specific
lift. `LlmProvidersOptions` vision flag stays unread by this seam.

### D5 — Wiring & safety (Q5)
- **New worker flag, distinct from the Web.UI demo flags** (belt-and-suspenders: flipping the demo flag must
  not light up the worker). Default **false**. E.g. `LlmProviders:FallbackEnabled` (+ `FallbackTimeoutSeconds`,
  default ~8–12s). Both the new flag AND a configured provider must be present for any real call.
- **Orchestrator injects the fallback as an OPTIONAL nullable dependency** (`IDeterministicFallbackLlmExtractor?
  = null`), matching the orchestrator's existing nullable-optional-ctor style. Flag off / section absent ⇒ not
  registered ⇒ dependency null ⇒ the `if (string.IsNullOrEmpty(mapped.NumeroExpediente) && _fallback is not null)`
  branch never enters the LLM call ⇒ **byte-identical to today** (a Null-Object registration is an acceptable
  equivalent).
- **Seam edit sits after `MapExtractedFieldsToExpediente`, on the success path.** Honest cost accounting: the
  field-aware trigger means a cheap `string.IsNullOrEmpty(NumeroExpediente)` check runs on **every** document
  (not "zero new lines on the success path" as the old whole-failure-branch design claimed). But with the flag
  off the check short-circuits on the `_fallback is null` clause before any allocation, and with the flag on but
  `NumeroExpediente` populated it short-circuits on the empty-check — so in both cases **no LLM call and no field
  mutation occur**, and output stays byte-identical. Only when the anchor is empty AND the flag is on does the
  fallback run and (on gate-pass) fill `NumeroExpediente` **only**, tagging provenance (see D6); on gate-fail or
  null it leaves the record exactly as deterministic produced it.
- **Timeout/cancellation:** `CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)` + a config-driven
  timeout; also cap the provider `HttpClient.Timeout`. Any `OperationCanceledException`/`TaskCanceledException`
  (timeout or real cancel) is caught inside the service → `Success(null)`, never rethrown. **Ollama down / slow /
  cancelled all collapse to the deterministic-empty outcome** — the fallback fails *safe*, not *loud*.
- **CancellationToken** threads unbroken: orchestrator → `TryRecoverNumeroExpedienteAsync` → LLM extractor →
  HttpClient. No `CancellationToken.None`/`default` mid-chain.
- **Ollama-only in production** (Mary, compliance): the runtime-switchable Gemini provider must **not** be used
  on the worker fallback path without explicit compliance sign-off — OCR text of Spanish legal docs carries
  CURP/RFC/monto (the LFPDPPP concern that shelved Veriqan GH#20). Worker fallback defaults Ollama; Gemini on
  this path is a Gate-B checklist item, not a config convenience.

### D6 — ManualReview / provenance threading (honesty carry-through)
The single field produced by the fallback (`NumeroExpediente`) must be **visibly distinguishable from a
deterministic hit** downstream — never indistinguishable in the review UI (Mary). Minimum: set an
`ExtractionMetadata` provenance marker (e.g. `Source = LlmFallback` / a `LlmFallbackApplied` flag) **scoped to
that field** on the fill branch, and thread it so the review dashboard/export gate can surface "NumeroExpediente:
LLM-fallback (gated)" provenance. Because fill-gap touches exactly one field, provenance is **per-field** (the
filled `NumeroExpediente`), not per-record — a change from the old whole-record design.

### D7 — Shadow mode: OUT of S4-C
Rejected for this epic (John): flag-off **already is** dark and zero-cost; a "compute-but-discard against live
prod traffic" shadow adds live Ollama load/latency/cost in prod to de-risk a *later* step. It is a legitimate
**Gate-B calibration tool / separate follow-up**, not S4-C code. The owner scoped S4-C as *planned before
built* — don't absorb an observability sub-project into it.

## Ordered story plan (composition root touched LAST)
- **S4-C.1 — Single-field control gate.** Plausibility + source-containment on a candidate `NumeroExpediente`,
  in isolation. *AC:* a plausible-but-not-in-OCR-text expediente ⇒ the gate returns abstain (null); a
  plausible-and-contained one ⇒ returns the value; unit-tested; no orchestrator touched.
- **S4-C.2 — Thin fallback service.** Wraps `ILlmExpedienteExtractor<TxtSource>` + S4-C.1 gate + timeout budget;
  extracts and gates **only** `NumeroExpediente`. *AC:* returns `Success(value)` only when both gates pass;
  Ollama down/timeout/exception ⇒ `Success(null)`, never throws; cancellation ⇒ `Cancelled`; fully tested with a
  fake LLM client; wired to nothing real yet.
- **S4-C.3 — New worker flag.** `LlmProviders:FallbackEnabled` (+ timeout), default false, distinct from demo
  flags. *AC:* flag exists, defaults false, existing orchestrator suite untouched + green.
- **S4-C.4 — Seam wiring (field-aware fill-gap).** Inject optional fallback into `ExtractionOrchestrator`; after
  `MapExtractedFieldsToExpediente`, when `NumeroExpediente` is empty AND OCR text present AND fallback non-null,
  call it and fill **only** `NumeroExpediente` on gate-pass; provenance-tag that field. *AC:* flag ON in a test
  harness — an empty-expediente fixture with populated OCR text gets `NumeroExpediente` filled by the fallback
  and flows into fusion tagged as LLM-fallback, **while every other deterministic field is byte-identical
  pre/post** (the fill-gap invariant test); flag OFF — behaviour byte-identical to today, proven by the existing
  regression suite staying green and the fallback substitute asserting `Received(0)` on both the anchor-populated
  and flag-off paths.
- **S4-C.5 — DI registration + dark deploy.** Register in Athena Worker `Program.cs` (minimal targeted
  registration — do NOT pull `AddPrismaInfrastructure` wholesale); prod `appsettings` flag false. *AC:* worker
  boots, health green, full suite green, ships dark.

## Tests (no live Ollama in CI — mock `ILlmProvider`/canned JSON, per S1–S4B)
Mirror path `08 Tests/04 Services/Athena/Prisma.Athena.Processing.Tests/Extraction/`:
- `ExtractionOrchestrator…_ExpedientePopulated_FallbackNeverInvoked` — substitute fallback, `Received(0)`,
  output deep-equals pre-S4-C golden fixture (byte-identical proof for the common path).
- `ExtractionOrchestrator…_FlagOff_ExpedienteEmpty_BehavesLikeToday_NoThrow` — fallback dependency null → empty
  `NumeroExpediente` preserved, no exception (proves dark path is the *old* path).
- `ExtractionOrchestrator…_ExpedienteEmpty_OcrTextPresent_FallbackInvokedOnce_WithPropagatedToken` — exactly one
  call, same CT instance.
- **`ExtractionOrchestrator…_FallbackFills_OnlyNumeroExpediente_OtherFieldsByteIdentical`** — THE fill-gap
  invariant: seed a deterministic record with populated authority/monto/partes + empty expediente; after a
  gate-passing fallback, `NumeroExpediente` is filled (tagged) and **every other field is byte-identical** to the
  pre-fallback record.
- `…FallbackService_ControlPlausibleAndContained_ReturnsValue`.
- `…FallbackService_PlausibleButNotInSourceText_Abstains_ReturnsNull` — THE source-containment proof.
- `…FallbackService_ImplausibleExpediente_Abstains_ReturnsNull` — garbage candidate → null, record untouched.
- `…FallbackService_LlmThrows_ReturnsSuccessNull_NeverThrows`.
- `…FallbackService_OllamaTimeoutExceedsBudget_DegradesToNullWithinWindow`.
- `…FallbackService_CancellationRequested_ReturnsCancelled_NoThrow`.
- DI: worker composition-root test resolves the fallback + binds the flag section.
Regression gate: existing suites hold exact green counts (`Extraction.Txt` 210/210, worker/orchestration
baselines) — CI diff additive-only.

## Gate-B graduation checklist (ADR-024 D9) — ALL required before the flip; NONE assumed met  {#7}
1. **THE missing measurement (Mary, headline) — now FIELD-LEVEL (D9):**
   **`P(LLM-text NumeroExpediente correct | NumeroExpediente empty, OCR text present)`** — the precision of the
   fallback on exactly the slice it now fires on. This *replaces* the old record-level "P(LLM correct |
   deterministic NULL)" (which described the wrong, near-vacuous no-OCR-text slice). Measure on the
   empty-`NumeroExpediente`-with-OCR-text slice of the corpus, against source-contained generator-manifest gold
   (never the companion JSON/XML): gate pass-rate vs abstain-rate; **precision of the pass set** (the number that
   must be high — a wrong pass is the only regressing failure); and of the fail set, correct-abstain vs
   false-reject. Repeatable automated enforcement test, not a one-off. **Corpus caveat (D9/QA):** the S4-M
   deterministic-empty corpus grounding is only *partially* observed (mode1/`FI1` grounded; mode2/mode3 partially
   refuted) — firm the grounding before trusting any precision number off it.
2. **D6 corpus N ≥ 30** stratified by quality tier (PRP1-golden is 20; the empty-`NumeroExpediente` slice is only
   ~3/20 — **too small to be a go/no-go number until grown**; expand the corpus or construct source-contained
   fixtures that trigger empty-`NumeroExpediente`-with-OCR-text first).
3. **Fill-gap invariant (replaces the old "correct-field-clobbering rate" metric):** because only
   `NumeroExpediente` is ever written, correct-field clobbering is **structurally zero by construction** — so the
   checklist item is the automated invariant test *"every deterministically-populated field is byte-identical
   pre/post fallback"* (S4-C.4 AC), not a measured rate. (D2 SolicitudPartes-no-regression is likewise satisfied
   by construction — the seam never writes partes.)
4. **Ollama-down / timeout fault-injection** proven in staging → clean fall-through to deterministic-empty, no
   throw.
5. **Empty-`NumeroExpediente` rate** measured on a real sample — if deterministic almost never leaves it empty,
   S4-C is low-value and the owner should know before flipping.
6. **Compliance ruling** on Gemini-vs-Ollama for the worker path (D5 data-egress).
7. **Provenance visible** to manual-review reviewers (D6) — per-field on the filled `NumeroExpediente`.
8. **~10 real accept/discard examples owner-eyeballed** + **explicit written owner go/no-go**. Flip via the
   ADR-024 procedure (env/appsettings override, never the checked-in default), traceable to the authorising
   baseline artifact. ADR-024: Gate B "does not pre-authorize any part of that wiring."

## Standards
.NET 10, `Result<T>` (never throw for business logic), `CancellationToken` on every async, `ConfigureAwait(false)`
in library code (NOT Blazor UI), warnings-as-errors, xUnit v3 + Shouldly + NSubstitute, ITDD (ADR-005),
`Method_Scenario_Expected`. Deterministic path stays the source of truth; do **not** modify
`AdaptiveTxtFieldExtractor` or `ExtractionReconciler` policy.

## Definition of done (for THIS spec deliverable)
This document exists on `Liv`, reconciled to ADR-024 Addendum D9, and captures: the owner's **field-aware fallback
+ per-field fill-gap** ruling, the party-converged design (D1–D7 with the resolved source-containment fork and the
D9-reconciled trigger/semantics), the ordered story plan, the test plan (esp. the fill-gap byte-identical
invariant + flag-off equivalence + source-containment abstain), and the Gate-B checklist with the named **field-
level** missing measurement. **No production code was written.** S4-C build starts only after the owner clears the
Gate-B checklist and signs off.
