# ADR-005: ITDD Contract Tests — Abstract Bases with Injected SUT and Mock-Backed Blueprint Instances

**Date**: 2026-06-10
**Status**: Accepted
**Deciders**: Owner (design stance 2026-06-10) + Development Team
**Tags**: testing, itdd, contract-tests, xunit-v3, mtp, nsubstitute, architecture

> Ratifies §3 of `docs/planning/itdd-test-suite-refactor-plan-2026-06.md` (the
> master plan) and fixes the mandatory contract-test shape referenced by the ITDD
> primer (`docs/Interface-Driven-Test-Driven-Development.md`, line 3). Where the
> primer and this ADR differ on mechanism, **this ADR governs**.

## Context and Problem Statement

The repository follows Interface-Driven TDD: a stable interface is introduced
together with a contract test suite that any implementation must pass. The primer
deferred the *mandatory mechanism* to an ADR that never existed ("ADR-0003"), and
in its absence four divergent shapes accumulated (master plan §1):

1. Standalone mock-SUT "contract tests" (9 classes / 168 tests) — the design
   blueprints, but structured so implementations cannot inherit them.
2. Copy-paste implementation twins (`*LiskovTests` etc.) that drifted from the
   blueprint (zero → severe drift; map in master plan §2).
3. Real-SUT classes *named* `*ContractTests` conflating contract and
   implementation concerns in the implementation test project.
4. One static-helper contract (never invoked) plus a fake-green placeholder test.

No abstract contract base class exists anywhere in the solution (verified
2026-06-10). The number **ADR-005** was chosen because "ADR-0003" collides in
spirit with the existing ADR-003 (metrics placement) and the repo uses no
leading-zero style.

## Decision

### 1. Home and packaging

- **Abstract contract bases and mock factories live in
  `ExxerCube.Prisma.Testing.Contracts`**
  (`09 Testing\01 Abstractions\Testing\Contracts\`).
- Testing.Contracts remains a **non-runnable class library**. It references
  **Domain only** (plus Testing.Abstractions) and the packages **Shouldly**,
  **`xunit.v3.extensibility.core`**, and **NSubstitute** (added by this ADR for
  the mock factories) — all centrally pinned, no version changes.
- It must **NEVER** reference `xunit.v3.mtp-v2`, `Microsoft.Testing.*`, or set
  `IsTestProject` — that would turn it into an executable test project and change
  the solution test sweep. `xunit.v3.extensibility.core` provides
  `[Fact]`/`[Theory]`/`TestContext` for compilation; discovery and execution
  happen only in the runnable test projects that derive from the bases.
- **Blueprint (mock-backed) instances live in
  `ExxerCube.Prisma.Tests.Domain.Interfaces`** — a runnable test project that
  references Testing.Contracts. Implementation instances live in each
  implementation's existing test project, which gains a `ProjectReference` to
  Testing.Contracts in its refactor phase.

### 2. Shape — abstract base, N + 1 inheritors

```text
{Name}Contract                       (abstract, Testing.Contracts — NOT discovered by xUnit)
├── Mock{Name}ContractTests          (blueprint — Tests.Domain.Interfaces, SUT from {Name}MockFactory)
├── {ImplA}ContractTests             (implementation instance — ImplA's test project)
└── {ImplB}ContractTests             (implementation instance — ImplB's test project)
```

Because the base is `abstract`, xUnit v3 (under Microsoft.Testing.Platform) does
not discover it; every inherited `[Fact]`/`[Theory]` runs **once per deriving
class**.

### 3. SUT mechanisms — injected `Sut` is the default, `CreateSut()` the fallback

**Default: constructor-injected, interface-typed `Sut`.** Use whenever the SUT
can be constructed in one expression by the deriving class (pure services,
services with simple injectable dependencies such as loggers):

```csharp
// Testing.Contracts
public abstract class FileTypeIdentifierContract
{
    protected FileTypeIdentifierContract(IFileTypeIdentifier sut) => Sut = sut;

    protected IFileTypeIdentifier Sut { get; }

    [Fact]
    public async Task IdentifyFileTypeAsync_NullContent_ReturnsFailure() { /* uses Sut */ }
}
```

*Example A — pure service (no dependencies):*

```csharp
public sealed class ReferenceFileTypeIdentifierContractTests : FileTypeIdentifierContract
{
    public ReferenceFileTypeIdentifierContractTests()
        : base(new ReferenceFileTypeIdentifier()) { }
}
```

*Example B — service with logger (dependencies built inline; xUnit creates the
test class per test, so this stays isolated):*

```csharp
public sealed class FileTypeIdentifierServiceContractTests : FileTypeIdentifierContract
{
    public FileTypeIdentifierServiceContractTests()
        : base(new FileTypeIdentifierService(
            Substitute.For<ILogger<FileTypeIdentifierService>>())) { }
}
```

**Sanctioned fallback: `protected abstract T CreateSut();`** — only where
construction needs per-implementation *fixtures* (database contexts, file
systems, containers) that cannot be a single base-constructor argument, or where
the SUT must be created lazily/per-scenario:

*Example C — persistence-backed (EF InMemory or a Testcontainers fixture):*

```csharp
public abstract class TemplateRepositoryContract
{
    protected abstract ITemplateRepository CreateSut(); // impl owns fixture lifetime

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsFailure()
    {
        var sut = CreateSut();
        // ...
    }
}

public sealed class TemplateRepositoryContractTests : TemplateRepositoryContract, IDisposable
{
    private readonly PrismaDbContext _context = InMemoryContextFactory.Create();

    protected override ITemplateRepository CreateSut() => new TemplateRepository(_context);

    public void Dispose() => _context.Dispose();
}
```

A contract base uses **one** mechanism, chosen when the base is authored; do not
mix both in one base.

### 4. Naming

- Base: **`{InterfaceNameWithoutI}Contract`** (e.g. `FieldMergeStrategyContract`)
  — avoids the duplicate-class-name architecture rule and the confusing `II*`
  prefix.
- Blueprint instance: **`Mock{InterfaceNameWithoutI}ContractTests`** in
  `Tests.Domain.Interfaces`.
- Implementation instance: **`{ImplementationName}ContractTests`** in the
  implementation's test project.
- Mock factory: **`{InterfaceNameWithoutI}MockFactory`** with
  `CreateContractConformingMock()`, in Testing.Contracts next to the base.

### 5. Scope rule — what belongs in a contract base

A test belongs in the base **iff any correct implementation must pass it**:
Result semantics (never-null result, failure instead of throw), null/empty input
handling, cancellation behavior (`IsCancelled()` on a pre-cancelled token, per
the repository-wide CancellationToken mandate), ordering/invariants, and
behaviors promised by the interface's XML documentation.

Implementation-specific tests stay in the implementation's test project —
**alongside, never inside, the contract**: fixture-specific data, regex/format
specifics, performance timings, confidence-tier values, caching, logging
assertions, and **all mutation-killing classes** (`*MutationTests`,
`*MutationKillingTests` are implementation-pinning by definition and are never
merged into a base).

### 6. Mock-first design — blueprints are kept forever

Mock-based contract classes are the **design blueprints** (owner decision,
2026-06-10): authored first, when the contract is designed and no implementation
exists. The refactor **never deletes them** — each is converted into the
blueprint instance of its contract base (test bodies → the base; inline
`.Returns(...)` stubbing → the dedicated mock factory). The factory's NSubstitute
configuration is the **executable design specification**. Where contract bodies
assert behavior a naive blanket stub cannot satisfy, the factory grows
argument-sensitive stubbing or hand-written fake logic — evolution toward a
*reference fake* is expected and desirable. If a contract base gains a test the
factory's mock cannot satisfy, extend the factory in the same change: a red
blueprint instance means the design spec itself is incomplete.

The only files the refactor deletes are **superseded copy-paste twins**, and only
after their bodies live in the base and run via the deriving implementation
instance.

### 7. Worked example (added with this ADR — Phase 0)

`IFileTypeIdentifier` (small, stable Domain port; async + `Result<T>` +
CancellationToken + optional parameter — exercises every contract-grade
category):

- `Testing.Contracts\FileTypeIdentifierContract.cs` — abstract base, injected-`Sut`
  mechanism (the default), 6 contract tests.
- `Testing.Contracts\FileTypeIdentifierMockFactory.cs` — argument-sensitive
  NSubstitute configuration (the executable design spec).
- `Tests.Domain.Interfaces\MockFileTypeIdentifierContractTests.cs` — blueprint instance.
- `Tests.Domain.Interfaces\ReferenceFileTypeIdentifierContractTests.cs` — a
  hand-written reference implementation + its instance (stands in for a
  production implementation so the N+1 mechanics are proven without touching
  Infrastructure test projects before their phase).

Known, intentional consequence: the production `FileTypeIdentifierService`
(Infrastructure.Extraction) does not yet inherit the base — it is converted in
the Phase 6 sweep. Its current code ignores the cancellation token, so the
contract's cancellation test is expected to surface a **finding** there
(implementation-bug per the repository CancellationToken mandate — the blueprint
is the spec; implementations conform to it, not vice versa).

### 8. Supersession

`docs/qa/test-plans/iitdd-contract-test-tasks.md` (Story 1.3–1.8 templates,
standalone `II{Name}Tests.cs` shape) is **superseded by this ADR** for the
*shape*; the mock-first design step it teaches remains the encouraged way to
author a new contract (now as base + factory + blueprint instance).

## Empirical proof (Phase 0 gate — results)

The mechanism was unproven in this solution until Phase 0; the worked example
demonstrates, on real builds/runs (evidence recorded in the master plan tracker
and the Phase 0 commit):

- (a) `[Fact]`s compile in Testing.Contracts via `xunit.v3.extensibility.core`
  alone; the project stays a non-runnable library; solution builds 0 errors / 0 warnings.
- (b) The abstract base is **not** discovered as a test class.
- (c) Inherited facts run **once per deriving class** under MTP: Tests.Domain.Interfaces
  went from 19 discovered tests to 31 (= 19 + 6 facts × 2 inheritors).

## Consequences

- Every phase of the refactor (master plan §4) lifts real test bodies into bases
  **verbatim** (mutation kill power is the crown jewel — names and bodies are
  preserved), converts the mock class into the blueprint instance, derives one
  instance per implementation, and only then deletes the superseded twin.
- New stable interfaces follow the Repository Rule (primer): interface + contract
  base + mock factory + blueprint instance ship together, before implementations.
- Architecture guardrail (Phase 6, optional earlier): every class named
  `*Contract` in Testing.Contracts must be abstract and have ≥ 1 inheritor.
- No xunit/MTP package **version** changes are needed or permitted by this ADR
  (the stack is version-coupled and fragile; see CLAUDE.md).

## Related

- Master plan: `docs/planning/itdd-test-suite-refactor-plan-2026-06.md`
- Primer: `docs/Interface-Driven-Test-Driven-Development.md`
- Placement: `ARCHITECTURE_AND_SOLUTION_GUIDELINES.md` §6.5
- Superseded: `docs/qa/test-plans/iitdd-contract-test-tasks.md`
- Adversarial gate: `.claude/commands/itdd-adversarial-review.md`
