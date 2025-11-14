# Development Checklist: Async/Await Requirements

**Purpose:** This checklist ensures all future stories comply with critical async/await requirements discovered during development.

**Reference Stories:** Story 1.4 (Identity Resolution) - Issues discovered and remediated  
**Last Updated:** 2025-01-16

---

## ✅ Pre-Implementation Checklist

### Interface Design
- [ ] All new async interface methods include `CancellationToken cancellationToken = default` parameter
- [ ] Check existing interfaces for missing CancellationToken before implementing
- [ ] Update existing interfaces if CancellationToken is missing (breaking change, document impact)

### Method Signature
- [ ] All async methods accept `CancellationToken cancellationToken = default`
- [ ] All async methods return `Task<Result<T>>` or `Task<Result>` (ROP compliance)

---

## ✅ Implementation Checklist

### Cancellation Token Handling

#### Early Cancellation Check
- [ ] Early cancellation check at method start (before any work begins)
- [ ] Returns `ResultExtensions.Cancelled<T>()` when cancellation detected
- [ ] Logs cancellation event for audit trail

#### Cancellation Propagation
- [ ] Passes `CancellationToken` to ALL dependency calls
- [ ] Checks `.IsCancelled()` after each dependency call
- [ ] Propagates cancellation using `ResultExtensions.Cancelled<T>()` (not generic failure)
- [ ] Handles partial results when cancellation occurs mid-operation

#### Exception Handling
- [ ] Catches `OperationCanceledException` explicitly
- [ ] Returns cancelled result (not failure) for cancellation exceptions
- [ ] Proper cleanup in cancellation scenarios (close resources, release locks, etc.)

#### Critical Operations
- [ ] `SemaphoreSlim.WaitAsync()` includes cancellation token
- [ ] File I/O operations include cancellation token
- [ ] Database operations include cancellation token
- [ ] All async dependency calls include cancellation token

### ConfigureAwait(false) Usage

#### Library Code (Application & Infrastructure)
- [ ] ALL `await` statements use `.ConfigureAwait(false)`
- [ ] Dependency calls: `await _service.MethodAsync(...).ConfigureAwait(false)`
- [ ] File I/O: `await File.ReadAllBytesAsync(...).ConfigureAwait(false)`
- [ ] Semaphore waits: `await semaphore.WaitAsync(...).ConfigureAwait(false)`
- [ ] Database operations: `await context.SaveChangesAsync(...).ConfigureAwait(false)`
- [ ] Task operations: `await Task.WhenAll(...).ConfigureAwait(false)`

#### Scope Verification
- [ ] Application layer code uses ConfigureAwait(false) ✅
- [ ] Infrastructure layer code uses ConfigureAwait(false) ✅
- [ ] UI layer code does NOT use ConfigureAwait(false) ✅ (correct)
- [ ] Test code excluded from ConfigureAwait requirement ✅

---

## ✅ Code Review Checklist

### Cancellation Compliance
- [ ] All async methods have CancellationToken parameter
- [ ] Early cancellation check present
- [ ] Cancellation propagation implemented
- [ ] OperationCanceledException handled explicitly
- [ ] Cancellation events logged

### ConfigureAwait Compliance
- [ ] All library code await statements use ConfigureAwait(false)
- [ ] No missing ConfigureAwait(false) in Application layer
- [ ] No missing ConfigureAwait(false) in Infrastructure layer
- [ ] UI code correctly does NOT use ConfigureAwait(false)

### ROP Compliance
- [ ] All methods return `Result<T>` or `Result`
- [ ] No exceptions thrown for business logic errors
- [ ] Proper error handling with Result pattern

---

## 📋 Reference Patterns

### Model Implementation: DocumentIngestionService
**Location:** `Prisma/Code/Src/CSharp/Application/Services/DocumentIngestionService.cs`

**Key Features:**
- ✅ Early cancellation check
- ✅ Cancellation propagation
- ✅ Explicit OperationCanceledException handling
- ✅ Proper cleanup on cancellation
- ✅ ConfigureAwait(false) on all await calls

### Model Implementation: DecisionLogicService
**Location:** `Prisma/Code/Src/CSharp/Application/Services/DecisionLogicService.cs`

**Key Features:**
- ✅ Comprehensive cancellation token handling
- ✅ Partial result preservation on cancellation
- ✅ Cancellation checks between iterations
- ✅ ConfigureAwait(false) on all await calls

---

## 🚨 Common Violations to Avoid

### Cancellation Violations
- ❌ Missing CancellationToken parameter in interface or implementation
- ❌ No early cancellation check (checking only after work starts)
- ❌ Not propagating cancellation from dependencies (treating cancelled as failure)
- ❌ Missing cancellation token on `SemaphoreSlim.WaitAsync()` (will hang!)
- ❌ Not catching `OperationCanceledException` explicitly

### ConfigureAwait Violations
- ❌ Missing `.ConfigureAwait(false)` on await statements in library code
- ❌ Using ConfigureAwait(false) in UI code (incorrect)
- ❌ Forgetting ConfigureAwait(false) on file I/O operations
- ❌ Missing ConfigureAwait(false) on semaphore waits

---

## 📚 Reference Documentation

### Audit Reports
- **Cancellation Audit:** `docs/audit/cancellation-rop-compliance-audit.md`
- **Cancellation Remediation:** `docs/audit/cancellation-rop-compliance-remediation-report.md`
- **ConfigureAwait Remediation:** `docs/qa/configureawait-remediation-report.md`

### Best Practices
- **ROP Best Practices:** `docs/ROP-with-IndQuestResults-Best-Practices.md`
- **Architecture Standards:** `docs/qa/architecture.md#coding-standards-and-conventions`

### Rules
- **ConfigureAwait Rule:** `.cursor/rules/1015_RuleForConfigureAwaitUsage.mdc`
- **Cancellation Rule:** `.cursor/rules/1002_CancellationTokenStandards.mdc`
- **Result Pattern Rule:** `.cursor/rules/1025_ResultCancellationHandling.mdc`

---

## ✅ Verification Commands

### Check for Missing CancellationToken
```bash
# Find async methods without CancellationToken parameter
grep -r "async Task.*Async(" Prisma/Code/Src/CSharp/Application --include="*.cs" | grep -v "CancellationToken"
```

### Check for Missing ConfigureAwait(false)
```bash
# Find await statements without ConfigureAwait in library code
grep -r "await.*Async(" Prisma/Code/Src/CSharp/Application --include="*.cs" | grep -v "ConfigureAwait"
grep -r "await.*Async(" Prisma/Code/Src/CSharp/Infrastructure --include="*.cs" | grep -v "ConfigureAwait"
```

### Verify UI Code Does NOT Use ConfigureAwait
```bash
# Should return no results (UI correctly does NOT use ConfigureAwait)
grep -r "ConfigureAwait(false)" Prisma/Code/Src/CSharp/UI --include="*.cs"
```

---

**Status:** ✅ Active - Use this checklist for all future stories  
**Maintained By:** QA Team  
**Last Review:** 2025-01-16

