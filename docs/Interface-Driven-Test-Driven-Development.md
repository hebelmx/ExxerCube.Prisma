# Interface-Driven Test-Driven Development (ITDD)

> **This is the plain-language primer.** The **canonical reference** is `ARCHITECTURE_AND_SOLUTION_GUIDELINES.md` §6.5; the **mandatory contract-test shape** (and its rationale) is fixed by **ADR-005** (`docs/architecture/adr/ADR-005-itdd-contract-tests-injected-sut.md`). Where this primer and ADR-005 differ on the exact mechanism, **ADR-005 governs** — it makes the constructor-injected, interface-typed `Sut` member the default and `CreateSut()` the sanctioned fallback. Refactor plan & tracker: `docs/planning/itdd-test-suite-refactor-plan-2026-06.md`.

## Purpose

This repository follows an Interface-Driven Test-Driven Development (ITDD) approach.

The goal is to design systems around contracts rather than implementations.

In traditional development, developers often start by implementing a class and later introduce interfaces to support testing, dependency injection, or architectural requirements.

In this repository we follow the opposite approach:

```text
Contract
    ↓
Tests
    ↓
Implementation
```

The contract is designed first.

The implementation is designed second.

This approach aligns naturally with Hexagonal Architecture, Clean Architecture, and Domain-Driven Design.

---

## What Is a Contract?

A contract consists of two parts:

```text
Contract
├── Interface
└── Contract Test Suite
```

The interface defines the syntax:

```csharp
public interface IManualReviewerPanel
{
    Task<Result<List<ReviewCase>>> GetReviewCasesAsync(...);
}
```

The interface tells us:

* Available operations
* Parameters
* Return types

The interface does not tell us:

* Expected behavior
* Invariants
* Error handling rules
* Cancellation behavior
* Null handling
* Domain guarantees

Those rules belong to the contract test suite.

---

## Why Interfaces Alone Are Not Contracts

Consider:

```csharp
public interface IRuntimeDataSource
{
    Task<TimeSeries> ReadAsync(TimeRange range);
}
```

The interface does not answer:

* Are timestamps ordered?
* Can samples be duplicated?
* Can units be missing?
* What happens when the range is empty?
* How are failures represented?

The contract tests define these expectations.

Without tests, an interface is only a method signature.

---

## Contract Tests

Contract tests define the behavior every implementation must satisfy.

Example:

```csharp
public abstract class IRuntimeDataSourceContract
{
    protected abstract IRuntimeDataSource CreateSut();

    [Fact]
    public async Task MustReturnOrderedTimestamps()
    {
        var sut = CreateSut();

        ...
    }

    [Fact]
    public async Task MustPreserveUnits()
    {
        var sut = CreateSut();

        ...
    }
}
```

Every implementation inherits the same contract:

```csharp
public class OpcUaRuntimeDataSourceTests
    : IRuntimeDataSourceContract
{
    protected override IRuntimeDataSource CreateSut()
        => new OpcUaRuntimeDataSource(...);
}
```

```csharp
public class HistorianRuntimeDataSourceTests
    : IRuntimeDataSourceContract
{
    protected override IRuntimeDataSource CreateSut()
        => new HistorianRuntimeDataSource(...);
}
```

If the implementation passes the contract suite, it is considered compliant.

---

## Mock-First Design — the Blueprint Instance

Contracts are designed **before** any implementation exists. Mocking is not merely
allowed for this — it is **encouraged**: the first inheritor of every contract
suite is a **mock-backed blueprint instance**, authored at design time, that every
concrete implementation must subsequently follow.

```csharp
// The blueprint — designed first, kept forever.
// SUT creation lives in a DEDICATED helper, never inline in the tests.
public class MockRuntimeDataSourceTests
    : IRuntimeDataSourceContract
{
    protected override IRuntimeDataSource CreateSut()
        => RuntimeDataSourceMockFactory.CreateContractConformingMock();
}
```

The dedicated mock factory (in `Testing.Contracts`) centralizes the NSubstitute
configuration in one place. That configuration **is the executable design
specification**: it encodes, before a line of production code exists, exactly the
behavior any implementation must exhibit. Where contract tests assert behavior a
simple stub cannot satisfy, the factory grows argument-sensitive stubbing or small
fake logic — evolving toward a *reference fake* is expected and desirable.

A contract suite therefore always has **N + 1 inheritors**:

```text
IRuntimeDataSourceContract            (abstract — the contract)
├── MockRuntimeDataSourceTests        (blueprint — designed first, never deleted)
├── OpcUaRuntimeDataSourceTests       (implementation 1)
└── HistorianRuntimeDataSourceTests   (implementation 2)
```

The blueprint instance is never deleted when implementations arrive: it remains
the living design document, and a contract test the factory's mock cannot satisfy
signals that the design specification itself is incomplete.

---

## Architectural Benefits

### Design Before Implementation

Developers must first answer:

"What service is being provided?"

before answering:

"How is it implemented?"

This leads to better boundaries and better abstractions.

### Separation of Concerns

The contract defines WHAT.

The implementation defines HOW.

Example:

```text
Contract:
    Retrieve runtime data.

Implementation:
    OPC-UA
    SQL Historian
    Replay File
    In-Memory Store
```

The contract remains stable while implementations evolve.

### Executable Documentation

Contract tests are executable specifications.

New developers can understand expected behavior by reading and running the contract suite.

### Parallel Development

Teams can work independently:

```text
Team A
    Domain Logic

Team B
    OPC-UA Adapter

Team C
    Historian Adapter
```

All teams target the same contract.

### Liskov Substitution Principle

Any implementation should be replaceable by another implementation without changing system behavior.

The contract suite verifies this property.

---

## Dependency Injection

Contract tests use constructor injection or factory methods.

A dependency injection container is not required.

Default (per ADR-005) — constructor-injected, interface-typed `Sut`:

```csharp
public abstract class IRuntimeDataSourceContract
{
    protected IRuntimeDataSourceContract(IRuntimeDataSource sut) => Sut = sut;

    protected IRuntimeDataSource Sut { get; }
}
```

Sanctioned fallback — a factory method, for implementations whose construction
needs per-implementation fixtures (databases, files):

```csharp
protected abstract IRuntimeDataSource CreateSut();
```

(The examples in this primer use the `CreateSut()` form for brevity; ADR-005
governs which form a given contract should use.)

Avoid:

```csharp
var provider = BuildContainer();
```

inside contract tests.

The contract should depend only on the abstraction, not on infrastructure.

---

## What Contract Tests Are Not

Contract tests are not mock verification tests.

Avoid tests whose only purpose is:

```csharp
repository.Verify(x => x.Save(...));
```

These tests verify interactions rather than behavior.

Contract tests must validate observable outcomes, invariants, and domain guarantees.

**This does not contradict the blueprint instance.** The distinction:

* **Wrong:** a *standalone* test class that stubs `mock.Method(...).Returns(x)`
  inline and then asserts `x` — the stub and the assertion live in the same test,
  so the test can never fail and protects no implementation.
* **Right:** contract test bodies that assert observable behavior against an
  abstract `Sut`, with the mock configured once in a dedicated factory and run
  through the *same* bodies every real implementation runs. The bodies stay
  honest because real implementations must pass them too; the factory stays
  honest because the bodies were not written around it.

Standalone mock classes written before this convention are not deleted — they are
converted into blueprint instances of the contract suite (test bodies → the
abstract contract, inline stubbing → the mock factory).

---

## Repository Rule

A new interface intended to be a stable architectural boundary should normally be introduced together with:

1. The interface.
2. The contract test suite **and its mock-backed blueprint instance** (with the dedicated mock factory) — this pair is the design step and may exist before any implementation does.
3. At least one implementation, inheriting the same contract suite.

An interface without a contract test suite is considered incomplete.

The contract test suite is part of the architecture and should be treated as a first-class asset.
