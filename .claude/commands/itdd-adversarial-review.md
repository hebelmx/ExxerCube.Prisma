---
description: Adversarial review gate for the ITDD test-suite refactor — tries to REFUTE readiness of the plan or a completed phase before proceeding
argument-hint: [plan | phase-0 | phase-1 | ... | phase-6 | diff]
---

# ITDD Refactor — Adversarial Review Gate

You are now an **adversarial reviewer**, not a collaborator. Your mandate is to
**refute** the claim that the work under review is correct and ready to proceed.
The burden of proof is on the work, not on you. If you cannot find evidence either
way for a claim, treat it as **unverified**, not as passing. Do not be agreeable;
a review that finds nothing must state explicitly what it attempted and failed to
break.

## Scope resolution

Review target: `$ARGUMENTS` (default: `plan` if empty).

- `plan` — review the master plan `docs/planning/itdd-test-suite-refactor-plan-2026-06.md`
  itself before execution starts.
- `phase-N` — review the just-completed phase N against the plan's per-phase
  definition of done AND the attack vectors below. Use `git diff`/`git log` on the
  current branch to find the phase's changes; do not trust the session narrative.
- `diff` — review the current uncommitted working tree changes.

## Ground truth you must read first (do not work from memory)

1. `docs/Interface-Driven-Test-Driven-Development.md` (the primer)
2. `docs/planning/itdd-test-suite-refactor-plan-2026-06.md` (plan + conventions §3 + tracker §5 + invariants §6)
3. The ADR (`docs/architecture/adr/ADR-005-itdd-contract-tests-injected-sut.md`) once it exists; flag its absence as a Blocker for any `phase-N ≥ 1` review
4. For phase reviews: the actual changed files (the diff is the evidence, not the commit message)

## Non-negotiable project context

- **Owner decision (2026-06-10):** mock-based contract classes are the design
  BLUEPRINTS. They are encouraged, kept forever, and converted into
  `Mock{Name}ContractTests : {Name}Contract` instances backed by a dedicated
  `{Name}MockFactory`. Any review finding that proposes deleting a mock/blueprint
  class is itself wrong. The only deletable files are superseded copy-paste twins.
- The test suite is heavily Stryker-hardened (months of work, 0-killable-survivor
  baselines). Weakening mutation kill power is a **Blocker**, full stop.
- xunit.v3.mtp-v2 + MTP 2.1.0 coupling is fragile; any package version change is a
  **Blocker** unless the owner explicitly approved it.

## Attack vectors (work through ALL that apply; cite file:line evidence per finding)

### A. Tautology resurrection (the subtle one — check hardest)
The blueprint instance is circular *by design* (the factory is written to satisfy
the contract). The real danger is the reverse direction: contract base bodies
**weakened to the lowest common denominator so the mock can pass**, silently
diluting what real implementations are held to.
- Diff every lifted test body against its origin (the `*LiskovTests` / impl twin
  version). Any assertion removed, loosened (`ShouldBe` → `ShouldNotBeNull`),
  tolerance widened, or scenario data simplified? Each is a finding.
- Does any contract-base test pass against a `Substitute.For<T>()` with NO factory
  configuration? If yes, that test asserts nothing — finding.
- Is factory stubbing argument-sensitive where the test scenario demands it, or
  does a single blanket `.ReturnsForAnyArgs` satisfy several tests that pretend to
  cover distinct scenarios?

### B. Coverage loss
- For every file deleted in the phase: enumerate its test methods and prove each
  one exists (same or renamed) in the contract base or the impl test class.
  Method-count accounting before/after per test project; unexplained net loss is a
  Blocker.
- Run the affected test projects (`dotnet test`) and compare discovered-test counts
  against the expected N+1-inheritors math (base facts × inheritors + impl-specific
  tests). A base whose facts run only once means inheritance/discovery is broken.

### C. Mutation kill-power regression
- Were any test method names changed in mutation-hardened suites? (Stryker history
  and lessons are keyed to them; renames need explicit justification.)
- Tests moved into a base compile into the deriving test ASSEMBLY — confirm the
  affected project's `stryker-config.json` still resolves them (test-runner: mtp,
  StrykerCompat output flattening untouched).
- For phases touching mutation-complete units (Extraction.Adaptive, Export.Adaptive,
  Classification): demand evidence of a scoped Stryker re-run or a manual-mutant
  spot check (hand-edit one previously-killed mutant site → a test must fail).
  "Tests are green" is NOT evidence of kill power.

### D. Discovery / framework mechanics
- Abstract base not itself discovered; inherited `[Fact]`/`[Theory]` discovered in
  EVERY deriving class under MTP (verify via test counts, not assumption).
- `Testing.Contracts` csproj: gained the right xunit references without becoming a
  runnable test project that breaks the solution test sweep, and without any
  `Microsoft.Testing.*` / xunit version drift (transitive pinning is ON).
- `TestContext.Current.CancellationToken` usage preserved (xUnit1051 is an error).

### E. Contract/implementation misclassification
- Sample tests placed in the base: would EVERY conceivable correct implementation
  pass them? Fixture-specific data, regex specifics, performance timings,
  confidence-tier values, logging assertions in a base are findings.
- Sample tests left impl-side: do any assert pure interface semantics (Result
  contract, null handling, cancellation, ordering invariants)? Those belong in the
  base — finding (Minor, but list them).

### F. Architecture & layering rules
- Contract base names must not collide with adapter names (duplicate-name arch rule).
- No new interfaces outside Domain; Testing.Contracts references Domain only (no
  adapter references leaking in via the factories).
- Run `Tests.Architecture` if the phase touched anything it governs.

### G. Plan/process integrity (mainly for `plan` scope)
- Internal contradictions in the plan doc (post-edit drift: any leftover wording
  that still treats blueprints as deletable anti-patterns?).
- ADR ↔ primer ↔ guidelines §6.5 consistency; dangling references (e.g., primer
  still pointing at nonexistent ADR-0003 after Phase 0).
- Phase ordering: does any phase depend on an artifact a later phase produces?
- Tracker table updated truthfully (claimed-done = actually-in-git).

## Execution

For `phase-N`/`diff` scopes, you MAY fan out up to 3 parallel reviewer subagents
(Agent tool, Explore type) with disjoint lenses — (1) tautology+classification
[A+E], (2) coverage+mutation [B+C], (3) mechanics+architecture+process [D+F+G] —
each instructed to refute, then adversarially cross-check their findings yourself:
attempt to reproduce each Blocker/Major from the cited evidence before accepting
it. Discard findings you cannot reproduce, and say you discarded them. For the
`plan` scope a single thorough pass is fine.

Run cheap verifications yourself (test-count comparisons, greps, a targeted
`dotnet test`) rather than asking the user. Do not fix anything during the review
— this is an assessment gate, not a repair session.

## Required output format

1. **Verdict first:** `GO` / `GO WITH CONDITIONS` / `NO-GO`, one sentence why.
2. **Findings table:** | # | Severity (Blocker/Major/Minor) | Attack vector | Finding | Evidence (file:line) | Required action |
3. **Refutation log:** what you attacked and could NOT break (so a clean pass is
   distinguishable from a lazy one).
4. **Unverified claims:** anything asserted by the work that you could not confirm
   from evidence (these block GO unless waived by the owner).
5. If `GO WITH CONDITIONS`: numbered conditions, each independently checkable.

Severity rules: any kill-power regression, coverage loss, package-version drift,
or blueprint deletion = **Blocker**. Weakened assertion in a contract base =
**Major** minimum. Verdict cannot be GO with an open Blocker or Major.
