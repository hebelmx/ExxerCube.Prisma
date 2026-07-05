# Intended Solution — S4-C: LLM hybrid extractor as a GROUNDED FALLBACK in the Athena pipeline

Anti-drift reference for the (future, owner-gated) S4-C build. Parent specs:
`spec-llm-hybrid-extractor.md` (S1–S3b contracts), `spec-llm-hybrid-extractor-S4A.md` (eval harness),
`spec-llm-hybrid-extractor-S4B.md` (extractor extension). Governing ADR:
`ADR-024-llm-hybrid-graduation-criteria.md` — **S4-C is "Gate B" and this spec does NOT authorize the flip.**

> **⚠️ SUPERSEDES the prior draft of this file (2026-07-04a, "replace-when-enabled").** That draft recommended
> calling `HybridExtractionService` as the Stage-3 *producer* when a flag flips (a REPLACE). The **owner
> re-ruled 2026-07-04b**: the LLM must run as a **grounded FALLBACK** — it fires **only when the deterministic
> extractor FAILS**, and any LLM result whose **control field fails a match is discarded whole**. A BMAD design
> party (Winston/architect, Amelia/dev, John/PM, Mary/analyst) then converged on the design below. The replace
> approach is **rejected** because the measured evidence (S4-B, ADR-024 D3-golden-v2/v3) shows LLM-text
> *regresses* deterministic on a full-N basis (expediente 75% < 85%; NumeroOficio 0% < 100%), and the one LLM
> "win" (AutoridadNombre) was a deterministic bug now fixed (`5a4d0b86`). A fallback that only activates on
> total deterministic failure is the only safe envelope. Design party session: `Liv`, 2026-07-04b.

## Status: DESIGN ONLY — BLOCKED on Gate-B preconditions + owner sign-off (see §7)
Today the hybrid path (`IHybridExtractionService`) is reachable **only** from the Web.UI `/hybrid-extraction`
demo page; the Athena worker never calls it. S4-C wires a **fallback** into the worker. It is blocked (by
design) on the ADR-024 D6 corpus + D7 honesty preconditions + explicit owner sign-off. **No production code is
to be written from this spec without that sign-off.**

## Owner's ratified intent (the contract every decision honors)
1. **Fallback, not replace.** The LLM extractor runs **only when the deterministic extractor FAILS**.
   Deterministic stays the authoritative primary path; on deterministic *success* nothing about today's
   behaviour changes (not one new line executes).
2. **Grounded via a control field, all-or-nothing.** If the LLM returns a control field whose value is **not a
   match**, the **entire LLM result is discarded** (every field, not just the control field). No partial trust
   of a call that got the anchor wrong.
3. **Prod-path but SAFE.** Ships on the production composition root, **dark by default**, zero regression.

## Why (confirmed from code — verified file:line)
- Worker composition root `04 Services/Athena/Prisma.Athena.Worker/Program.cs` builds DI **manually**,
  registers deterministic `IFieldExtractor<TxtSource>` → `AdaptiveTxtFieldExtractor` (~`Program.cs:174`), and
  never binds `LlmProvidersOptions` or registers `IHybridExtractionService`.
- Live extraction path: `AthenaWorkerService` → `ExtractionPipelineService.cs:164` →
  `ExtractionOrchestrator.ExtractAsync` → Stage 3 `BuildPdfExpedienteFromOcrAsync` → the deterministic call at
  **`04 Services/Athena/Prisma.Athena.Processing/ExtractionOrchestrator.cs:392`**
  `_txtFieldExtractor.ExtractFieldsAsync(txtSource, fieldDefinitions)` → `MapExtractedFieldsToExpediente`
  (~`:552`) → `_fusionService.FuseAsync` (~`:313`).
- On deterministic failure, `:393` currently logs a warning and `return (null, new ExtractionMetadata())` so
  fusion runs without PDF input. **This exact failure branch is the only place the fallback is invoked.**
- Reused primitives already exist: `ILlmExpedienteExtractor<TxtSource>` (LLM-text track, returns full
  `Expediente` incl. `SolicitudPartes`), `LlmExtractionGate` (per-field plausibility for
  expediente/RFC/CURP/monto), `LlmProvidersOptions` (runtime Ollama/Gemini).

## Converged design (party-locked)

### D1 — Trigger (Q1)
Fallback fires **iff** the deterministic Stage-3 extraction fails at `:392` — i.e. the existing failure
condition (`extractionResult.IsFailure || extractionResult.Value == null`). **No coverage/percentage
threshold** — a fuzzy "partial-but-thin" trigger is untestable and would fire on partial-success cases the
pipeline accepts today (a regression vector the owner explicitly ruled out). Blast radius = the population
deterministic can't touch at all.

### D2 — Control field & the match rule (Q2) — TWO gates, both must pass
Control field = **`NumeroExpediente`** (the anchor every downstream `DofNumeral`/fusion citation hangs off;
LLM-text's strongest field at 94% gate-conditioned). The returned LLM `Expediente` is trusted **only if
both** hold; if **either** fails, **discard the whole LLM result** (return NULL → pipeline behaves exactly as
the deterministic-null path):
- **(a) Plausibility** — reuse `LlmExtractionGate`'s existing expediente-format check (ADR-024 D8). No new regex.
- **(b) Source-containment** — the returned expediente string must appear in the **raw OCR source text**
  (`txtSource`) after normalization (case-insensitive, whitespace-collapsed, hyphen/dash-normalized). **No
  fuzzy / Levenshtein / semantic matching** — verbatim-after-normalization only.

**Why (b) is IN scope, not gold-plating** (John + Mary): by the time the fallback fires, deterministic already
returned null, so there is nothing to match against *except the source*. Plausibility alone only proves the
LLM produced something *shaped* like an expediente — trivially satisfiable by a hallucination or by an echo of
the **unreliable companion JSON/XML** (which is *input, never an oracle* — [[prisma-domain-3-docs-unreliable]]).
Source-containment is D7.1's eval-time discipline moved to inference time, and it is nearly free (a substring
check on text already in memory). **The risk is asymmetric:** a false *reject* → abstain → NULL → **zero
regression by construction**; a false *pass* (a hallucinated/companion-sourced expediente stamped into a legal
record) is the **only** outcome that makes prod worse than today. Gate hard on the safe side.

Known false-reject zone (accepted): docs where the correct expediente is present but **OCR-mangled** — the same
corruption that beat the regex may beat a verbatim match. Cost of that = the fallback abstains (safe). This is
the deliberate trade.

### D3 — Reuse vs new (Q3): NEW thin worker service, reuse the *components* not the *orchestration*
Do **NOT** call `HybridExtractionService` from the worker. It runs *all* tracks unconditionally and merges
per-field via `ExtractionReconciler` (deterministic-wins / LLM-fills-gaps / conflict→null+flag) — a
demo-comparison policy that (i) invokes the LLM on every document including the success path, contradicting
"fires only on failure", and (ii) partially trusts LLM output, contradicting owner's all-or-nothing. Wrapping
it to undo the merge is wrapping a mismatched abstraction.

**Build a thin worker-facing service** (name TBD at build, e.g. `IDeterministicFallbackLlmExtractor` /
`LlmFallbackTxtExtractionService`) that owns its own control-discard policy and reuses:
- `ILlmExpedienteExtractor<TxtSource>` — the LLM-text track only.
- `LlmExtractionGate` plausibility (expose the single-field check narrowly if not already public; do **not**
  alter its per-field abstention used elsewhere).

Proposed contract:
```csharp
public interface IDeterministicFallbackLlmExtractor
{
    // Returns Success(expediente) only when BOTH gates pass; Success(null) on any discard/failure/timeout;
    // Cancelled on cancellation. NEVER throws for business logic.
    Task<Result<Expediente?>> TryExtractFallbackAsync(
        TxtSource source,
        IReadOnlyList<FieldDefinition> fieldDefinitions,
        CancellationToken cancellationToken = default);
}
```
Impl steps: cancel-check → call LLM-text extractor (any exception/timeout ⇒ `Success(null)`, never throw) →
plausibility gate on NumeroExpediente → source-containment on NumeroExpediente → both pass ⇒ `Success(expediente)`,
else ⇒ `Success(null)` (whole-record discard). ~60–80 LOC.

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
  registered ⇒ dependency null ⇒ the `if (expediente is null && _fallback is not null)` branch never executes ⇒
  **byte-identical to today** (a Null-Object registration is an acceptable equivalent — either keeps the seam
  branch-free on the success path).
- **Seam edit is strictly inside the existing `:392` failure branch.** Success path: zero new lines execute.
  Failure branch: call the fallback; on `Success(non-null)` use it as the Stage-3 producer and **tag provenance**
  (see D8); on null, fall through to today's `return (null, new ExtractionMetadata())`.
- **Timeout/cancellation:** `CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)` + a config-driven
  timeout; also cap the provider `HttpClient.Timeout`. Any `OperationCanceledException`/`TaskCanceledException`
  (timeout or real cancel) is caught inside the service → `Success(null)`, never rethrown. **Ollama down / slow /
  cancelled all collapse to today's deterministic-null outcome** — the fallback fails *safe*, not *loud*.
- **CancellationToken** threads unbroken: orchestrator → `TryExtractFallbackAsync` → LLM extractor → HttpClient.
  No `CancellationToken.None`/`default` mid-chain.
- **Ollama-only in production** (Mary, compliance): the runtime-switchable Gemini provider must **not** be used
  on the worker fallback path without explicit compliance sign-off — OCR text of Spanish legal docs carries
  CURP/RFC/monto (the LFPDPPP concern that shelved Veriqan GH#20). Worker fallback defaults Ollama; Gemini on
  this path is a Gate-B checklist item, not a config convenience.

### D6 — ManualReview / provenance threading (honesty carry-through)
A field produced by the fallback must be **visibly distinguishable from a deterministic hit** downstream —
never indistinguishable in the review UI (Mary). Minimum: set an `ExtractionMetadata` provenance marker
(e.g. `Source = LlmFallback` / a `LlmFallbackApplied` flag) on the fallback branch, and thread it so the review
dashboard/export gate can surface "LLM-fallback (gated)" provenance. (Note: because it's all-or-nothing and only
fires on deterministic-null, there is no per-field merge to flag — provenance is per-record.)

### D7 — Shadow mode: OUT of S4-C
Rejected for this epic (John): flag-off **already is** dark and zero-cost; a "compute-but-discard against live
prod traffic" shadow adds live Ollama load/latency/cost in prod to de-risk a *later* step. It is a legitimate
**Gate-B calibration tool / separate follow-up**, not S4-C code. The owner scoped S4-C as *planned before
built* — don't absorb an observability sub-project into it.

## Ordered story plan (composition root touched LAST)
- **S4-C.1 — Control-discard gate.** Plausibility + source-containment on NumeroExpediente, all-or-nothing,
  in isolation. *AC:* a plausible-but-not-in-OCR-text expediente ⇒ the gate discards **all** fields from that
  LLM result; unit-tested; no orchestrator touched.
- **S4-C.2 — Thin fallback service.** Wraps `ILlmExpedienteExtractor<TxtSource>` + S4-C.1 gate + timeout budget.
  *AC:* returns `Success(expediente)` only when both gates pass; Ollama down/timeout/exception ⇒ `Success(null)`,
  never throws; cancellation ⇒ `Cancelled`; fully tested with a fake LLM client; wired to nothing real yet.
- **S4-C.3 — New worker flag.** `LlmProviders:FallbackEnabled` (+ timeout), default false, distinct from demo
  flags. *AC:* flag exists, defaults false, existing orchestrator suite untouched + green.
- **S4-C.4 — Seam wiring.** Inject optional fallback into `ExtractionOrchestrator`; call inside the `:392`
  failure branch only; provenance tag on the fallback branch. *AC:* flag ON in a test harness — a
  deterministic-fail fixture is filled by the fallback and flows into fusion tagged as LLM-fallback; flag OFF —
  behaviour byte-identical to today, proven by the existing regression suite staying green and the fallback
  substitute asserting `Received(0)` on the success path.
- **S4-C.5 — DI registration + dark deploy.** Register in Athena Worker `Program.cs` (minimal targeted
  registration — do NOT pull `AddPrismaInfrastructure` wholesale); prod `appsettings` flag false. *AC:* worker
  boots, health green, full suite green, ships dark.

## Tests (no live Ollama in CI — mock `ILlmProvider`/canned JSON, per S1–S4B)
Mirror path `08 Tests/04 Services/Athena/Prisma.Athena.Processing.Tests/Extraction/`:
- `ExtractionOrchestrator…_DeterministicSucceeds_FallbackNeverInvoked` — substitute fallback, `Received(0)`,
  output deep-equals pre-S4-C golden fixture (byte-identical proof).
- `…_FallbackNull_DeterministicFails_BehavesLikeToday_NoThrow` — fallback dependency null (flag off) → null
  expediente, no exception (proves dark path is the *old* path).
- `…_DeterministicFails_FallbackInvokedOnce_WithPropagatedToken` — exactly one call, same CT instance.
- `…FallbackService_ControlPlausibleAndContained_ReturnsWholeExpediente`.
- `…FallbackService_PlausibleButNotInSourceText_DiscardsWholeRecord` — THE source-containment proof.
- `…FallbackService_ImplausibleExpediente_DiscardsWholeRecord_OtherFieldsIgnored` — seed a good AutoridadNombre
  beside a garbage NumeroExpediente → result NULL, not partially filled.
- `…FallbackService_LlmThrows_ReturnsSuccessNull_NeverThrows`.
- `…FallbackService_OllamaTimeoutExceedsBudget_DegradesToNullWithinWindow`.
- `…FallbackService_CancellationRequested_ReturnsCancelled_NoThrow`.
- DI: worker composition-root test resolves the fallback + binds the flag section.
Regression gate: existing suites hold exact green counts (`Extraction.Txt` 210/210, worker/orchestration
baselines) — CI diff additive-only.

## Gate-B graduation checklist (ADR-024) — ALL required before the flip; NONE assumed met  {#7}
1. **THE missing measurement (Mary, headline):** *fallback-subset precision on the deterministic-NULL slice.*
   The 85%/75% full-N numbers do **not** describe this subpopulation — the fallback only ever runs on docs where
   deterministic returned NULL, and `P(LLM correct | deterministic NULL)` **does not exist in any S4-B artifact
   yet.** Measure on the deterministic-expediente-NULL slice of the corpus, against source-contained
   generator-manifest gold (never the companion JSON/XML): gate pass-rate vs abstain-rate; **precision of the
   pass set** (the number that must be high — a wrong pass is the only regressing failure); and of the fail set,
   correct-abstain vs false-reject. Make it a repeatable automated enforcement test, not a one-off.
2. **D6 corpus N ≥ 30** stratified by quality tier (PRP1-golden is 20; the deterministic-NULL slice is only
   ~3/20 — **too small to be a go/no-go number until grown**; expand the corpus or construct source-contained
   fixtures that trigger deterministic-NULL first).
3. **D2 SolicitudPartes no-regression** — fallback must not lose partes vs the deterministic-null baseline.
4. **Ollama-down / timeout fault-injection** proven in staging → clean fall-through, no throw.
5. **Deterministic-fail rate** measured on a real sample — if deterministic almost never fails, S4-C is
   low-value and the owner should know before flipping.
6. **Compliance ruling** on Gemini-vs-Ollama for the worker path (D5 data-egress).
7. **Provenance visible** to manual-review reviewers (D6).
8. **~10 real accept/discard examples owner-eyeballed** + **explicit written owner go/no-go**. Flip via the
   ADR-024 procedure (env/appsettings override, never the checked-in default), traceable to the authorising
   baseline artifact. ADR-024: Gate B "does not pre-authorize any part of that wiring."

## Standards
.NET 10, `Result<T>` (never throw for business logic), `CancellationToken` on every async, `ConfigureAwait(false)`
in library code (NOT Blazor UI), warnings-as-errors, xUnit v3 + Shouldly + NSubstitute, ITDD (ADR-005),
`Method_Scenario_Expected`. Deterministic path stays the source of truth; do **not** modify
`AdaptiveTxtFieldExtractor` or `ExtractionReconciler` policy.

## Definition of done (for THIS spec deliverable)
This document exists on `Liv` and captures the owner's fallback ruling, the party-converged design (D1–D7 with
the resolved source-containment fork), the ordered story plan, the test plan (esp. flag-off equivalence +
source-containment discard), and the Gate-B checklist with the named missing measurement. **No production code
was written.** S4-C build starts only after the owner clears the Gate-B checklist and signs off.
