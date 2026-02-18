## IndQuestResults Result Manual

This manual documents the consumer-facing API for `Result` and `Result<T>` and their fluent extensions, grounded in the current code. All behaviors are validated by unit tests under `Src/Code/tests/IndQuestResults.Tests.Unit`.

### Overview

- Purpose: Functional, type-safe success/failure flow without exceptions for control flow.
- Core types and namespaces:
  - `IndQuestResults.Result` (non-generic)
  - `IndQuestResults.Result<T>` (generic)
  - Extensions in `IndQuestResults.Operations` (validation, async chaining, LINQ, helpers)
- Highlights: fluent composition (Map/Bind/Match), error aggregation, warnings + quality metadata on success, async pipelines, validation utilities.

### Quick Start

```csharp
using IndQuestResults;
using IndQuestResults.Operations;

// Success/failure
var ok = Result.Success();
var fail = Result.WithFailure("Something went wrong");

// Typed
Result<int> parsed = Result<int>.Success(42);
Result<int> bad = Result<int>.WithFailure("Parse error");

// Fluent chain
var length = Result<string>.Success("hello")
    .Map(s => s.Length)                  // Success(5)
    .Ensure(n => n > 0, "Zero length"); // still Success(5)

// Async chain
var profile = await GetUserAsync(id)
    .ThenAsync(u => LoadProfileAsync(u.Id))
    .ThenTap(p => CacheAsync(p));
```

### Cheat Sheet

- Create: `Result.Success()`, `Result.WithFailure("err")`, `Result<T>.Success(v)`, `Result<T>.WithFailure("err")`
- Map/Bind: `.Map(f)`, `.Bind(f)`; LINQ: `from x in r1 from y in r2 select ...`
- Ensure/Tap: `.Ensure(pred, "err")`, `.Tap(a)`; side-effects without breaking the chain
- Match: `.Match(onSuccess, onFailure)` returns a plain value for generic; use `MatchValue` for explicit value selection or `MapBoth` to produce a `Result<TOut>`
- Combine: `r.Combine(r2, r3)` aggregates errors; `Result.CombineErrors(e1, e2)` merges sets
- Value helpers: `.ValueOr(default)`, `.OrElse(fallback)`
- Error helpers: `.MapError(map)`, `.TapError(log)`, `.Recover(errors => ...)`
- Async: `.ThenAsync`, `.ThenMap`, `.ThenTap`, `.ThenValidate`, `.ThenRecover`, `.CombineAsync`
- Cancellation: `ResultExtensions.Cancelled<T>()`, `.IsCancelled()`
- Serialization: `Result<T>` is JSON-serializable via `System.Text.Json` attributes (`[JsonConstructor]`); custom converters are not required
- Warnings: `Result<T>.WithWarnings(warnings, value, confidence, missingDataRatio)`

---

# Consumer API

## Non-generic `Result`

Namespace: `IndQuestResults`

### Construction

- `Result.Success()`
  - Returns a successful `Result` with empty `Errors`.
  - `ToString()` => `ResultConstants.SuccessPrefix`.

- `Result.WithFailure(string error)`
  - Returns failure with a single error.

- `Result.WithFailure(IEnumerable<string> errors)` / `Result.WithFailure(string[] errors)`
  - Returns failure with all provided errors.
  - Null/empty input becomes `[ResultConstants.DefaultErrorMessage]`.

### State

- `IsSuccess` / `IsFailure`: success vs failure.
- `Errors` / `Error`: all messages and the first non-empty message.

### Functional API

- `OnSuccess(Action)` executes only on success; returns same instance.
- `OnFailure(Action<IEnumerable<string>>)` executes only on failure; returns same instance.
- `Map<T>(Func<T>) : Result<T>` maps success to typed result; propagates errors on failure.
- `Bind<T>(Func<Result<T>>) : Result<T>` binds success to next result; propagates errors on failure.
- `Ensure(Func<bool>, string errorMessage) : Result` checks condition on success; returns failure when false.
- `Tap(Action) : Result` side effect on success; returns same instance.
- `Combine(params Result[]) : Result` aggregates errors across inputs (success if none).
- `Match<T>(Func<T> onSuccess, Func<IEnumerable<string>, T> onFailure) : T` selects a branch value.
- `Recover(Func<Result>) : Result` returns recovery result only on failure.
- `CombineErrors(IEnumerable<string>? primary, IEnumerable<string>? secondary) : Result` failed result with both sets (or `NoErrorsFound` if both empty).

### Formatting

- `ToString()`
  - Success: `ResultConstants.SuccessPrefix`.
  - Failure: `ResultConstants.FailurePrefix: err1, err2, ...` (uses `Result.FormatErrorsString`).

---

## Generic `Result<T>`

Namespace: `IndQuestResults`

### Construction

- `Result<T>.Success(T value)` / `WithSuccess(T value)`
  - Success with value (null is valid for nullable `T`).

- `Result<T>.WithFailure(...)`
  - Overloads: `(IEnumerable<string>? errors, T? value = default)`, `(T? value = default, IEnumerable<string>? errors = default)`, `(string[] errors, T? value = default)`, `(string error, T? value = default)`.
  - Null/empty errors become `[ResultConstants.DefaultErrorMessage]`.

- Warnings (success with diagnostics)
  - `WithWarnings(IEnumerable<string> warnings, T value)` => success with `Warnings` and `HasWarnings`.
  - `WithWarnings(IEnumerable<string> warnings, T value, double confidence, double missingDataRatio)` => clamps metadata to [0,1].

- Conversions & deconstruction
  - Implicit `T -> Result<T>` as `Success(T)`.
  - Implicit `Result<T> -> Result` preserving success/failure.
  - Deconstructs to `(bool succeeded, T? data, IEnumerable<string> errors)`.

### State

- `Value` (T?)
- `IsSuccessMayBeNull`: success, even if `Value` is null (nullable `T`).
- `IsSuccess` / `IsSuccessNotNull`: success and non-null `Value`.
- `IsSuccessValueNull`: success with null `Value`.
- `IsFailure`: negation of success flag.
- `HasErrors`: warnings or failure messages present.
- `HasWarnings`: success with diagnostic messages.
- `Warnings`, `Confidence`, `MissingDataRatio`: diagnostics and quality metadata.

### Functional API

- `OnSuccess(Action<T>) : Result<T>` executes for success; for nullable `T`, runs even if `Value` is null.
- `OnFailure(Action<IEnumerable<string>>) : Result<T>` executes only on failure (default error injected when none present).
- `Map<TOut>(Func<T, TOut>) : Result<TOut>` maps success; for non-nullable `T` with null `Value`, returns failure indicating null map for non-nullable type.
- `Bind<TOut>(Func<T, Result<TOut>>) : Result<TOut>` same nullability semantics as `Map`.
- `Ensure(Func<T,bool>, string errorMessage) : Result<T>` returns failure if predicate false; for success with null `Value`, returns `ConditionEvaluationWithNullValue`.
- `Tap(Action<T>) : Result<T>` side effect on success; mirrors `OnSuccess` nullability rules.
- `Combine(params Result[]) : Result<T>` aggregates errors across non-generic results and self; returns success preserving current value.
- `Match<TOut>(Func<T,TOut> onSuccess, Func<IEnumerable<string>,TOut> onFailure) : Result<TOut>` returns a success-wrapped branch value.
- `Recover(Func<Result<T>>) : Result<T>` only on failure; otherwise returns same instance.
- `RecoverWith<TOut>(Func<Result<TOut>>) : Result<TOut>` failure recovers to another type; on success attempts safe conversion to `TOut` or returns typed failure.

Nullability example (from tests):

```csharp
Result<int?> ri = new Result<int?>(true, errors: null, value: null);
ri.OnSuccess(_ => /* runs */);
ri.Map(i => (i ?? 0) + 1);            // Success(1)
ri.Bind(i => Result<string>.Success((i ?? 0).ToString()));
```

### Formatting

- Success: `"Success: <Value>"` (Value may be null for nullable `T`).
- Failure: `ResultConstants.FailurePrefix: err1, err2, ...`.

---

# Fluent Helpers

Namespace: `IndQuestResults.Operations`

- LINQ (`ResultLinqExtensions`)
  - `Select` maps via `Map`
  - `SelectMany` binds via `Bind` (overloads support 2-source queries)
  - `Where` filters via `Ensure`

- Error helpers (`ResultErrorExtensions`)
  - `MapError(this Result|Result<T>, Func<IEnumerable<string>, IEnumerable<string>>)` transforms errors on failures
  - `TapError(this Result|Result<T>, Action<IEnumerable<string>>)` side-effect on errors
  - `Recover(this Result|Result<T>, Func<IEnumerable<string>, Result|Result<T>>)` error-aware recovery

- Value helpers (`ResultValueExtensions`)
  - `ValueOr(default)` / `ValueOr(Func<T>)`
  - `OrElse(Result<T>)` / `OrElse(Func<Result<T>>)`
  - `MatchValue(onSuccess, onFailure)` returns a plain value
  - `OnBoth(Action)` / `OnBoth(Action<T>, Action<IEnumerable<string>>)` / `Finally(Action)`
  - `MapBoth(onSuccess, onFailure)` returns `Result<TOut>`

- Try helpers (`ResultTryExtensions`)
  - `ResultTryExtensions.Try<T>(Func<T>, Func<Exception,string>)`
  - `ResultTryExtensions.TryAsync<T>(Func<Task<T>>, Func<Exception,string>)`
  - `MapTry` and `BindTry` variants to wrap throwing delegates

---

# Async Fluent API

- Namespace: `IndQuestResults.Async` (preferred)
  - Chaining: `ResultAsync.BindAsync`, `ResultAsync.MapAsync`, `ResultAsync.TapAsync`, `ResultAsync.RecoverAsync`
  - Collections: `ResultAsync.SequenceAsync`, `ResultAsync.TraverseAsync`, `ResultAsync.TraverseParallelAsync`
  - ValueTask support: `ValueTask<Result<T>>.ThenAsync`, `.ThenMap`, `.ThenTap`

- Namespace: `IndQuestResults.Operations.ResultExtensions`
  - Chaining: `ThenAsync`, `ThenMap`, `ThenTap`, `ThenDo`, `ThenValidate`, `ThenValidateAsync`, `ThenEnsure`, `ThenSwitch`, `ThenRecover`, `ThenLogErrors`
  - Composition: `CombineAsync` (tuple), `WhenAllAsync`, `DefaultIfFailure`, `ThenAsyncCancellable`

- Utilities
  - `DefaultIfFailure<T>(Task<Result<T>>, T defaultValue)`
  - `ThenAsyncCancellable<TIn,TOut>(..., Func<TIn,CancellationToken,Task<Result<TOut>>>, CancellationToken)`
  - `Cancelled()` / `Cancelled<T>()` and `IsCancelled(this Result|Result<T>)`

Example:

```csharp
using IndQuestResults;
using IndQuestResults.Operations;

var result = await GetUserAsync(id)
    .ThenValidateAsync(u => ValidateUserAsync(u))
    .ThenAsync(u => CreateProfileAsync(u))
    .ThenTap(p => CacheAsync(p));

if (result.IsFailure)
{
    await LogAsync(string.Join(", ", result.Errors));
}
```

---

# Validation Utilities

Namespace: `IndQuestResults.Operations` and error types in `IndQuestResults.Validation`

- `EnsureNotNull<T>(T? value, string parameterName)` overloads for class and `Nullable<T>` structs
- `ValidateNotNull(params (object? value, string parameterName)[] validations)`
- `CreateIfValid<T>(Func<T> factory, params (object? value, string parameterName)[] validations)`
- `FailForNullArgument<T>(string parameterName, string? message = null)` and non-generic `FailForNullArgument`
- `FailForNullArguments<T>(params string[] names)` and non-generic `FailForNullArguments`
- Error types: `NullArgumentError`, `MultipleNullArgumentsError`

Examples:

```csharp
using IndQuestResults;
using IndQuestResults.Operations;

var validated = ResultExtensions.ValidateNotNull((user, nameof(user)), (user?.Email, nameof(user.Email)));

var created = ResultExtensions.CreateIfValid(
    factory: () => new User(id!),
    (id, nameof(id))
);
```

---

# Formatting & Performance Notes

- `Result.ToString()`
  - Success: `ResultConstants.SuccessPrefix`
  - Failure: `"WithFailure: err1, err2, ..."` via `Result.FormatErrorsString`
- `Result<T>.ToString()`
  - Success: `"Success: <Value>"` (Value may be null for nullable `T`)
  - Failure: formatted errors as above
- Internals optimize small collections and strings via spans and pre-sizing; large collections use `StringBuilder`.

---

# Integration Patterns

Service layer

```csharp
using IndQuestResults;
using IndQuestResults.Operations;

public class UserService
{
    public Task<Result<User>> CreateUserAsync(CreateUserRequest request)
    {
        return ValidateRequest(request)
            .ThenAsync(r => CheckEmailUnique(r.Email))
            .ThenAsync(r => CreateUser(r))
            .ThenTap(u => SendWelcomeEmail(u));
    }
}
```

Controller

```csharp
using IndQuestResults;

[ApiController]
public class UsersController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser(CreateUserRequest request)
    {
        var result = await _userService.CreateUserAsync(request);
        return result.Match(
            onSuccess: user => Ok(user.ToDto()),
            onFailure: errors => BadRequest(new { Errors = errors })
        );
    }
}
```

---

# Appendix: Behavior Summary (grounded by tests)

- Success constructors set flags and keep `Errors` empty.
- Failure constructors inject a default error when none provided.
- Map/Bind propagate failures without executing delegates.
- Ensure converts success to failure when predicate fails (no-op on failure).
- Combine aggregates errors in order across many results.
- Match calls the correct branch; `Result<T>.Match` wraps return in a success result.
- Nullability rules: for nullable `T`, success operations run even when `Value` is null; for non-nullable `T`, null `Value` maps/binds to a typed failure.
- Async helpers preserve the same semantics and support cancellation.

See tests under `Src/Code/tests/IndQuestResults.Tests.Unit/*` for executable specifications of all behaviors.

---

# Linting & Analyzers

- Package: `IndQuestResults.Analyzers` (optional). Add to your solution to get guidance and code fixes.
- Rule IQR0001: Prefer `ResultAsync` for async chains
  - Triggers when calling `ThenAsync` from `IndQuestResults.Operations.ResultExtensions` on `Task<Result<T>>`.
  - Rationale: `ResultAsync` centralizes async exception/cancellation semantics consistently across chains.
  - Code fix: rewrites `task.ThenAsync(next)` to `ResultAsync.BindAsync(task, next)` (adds `using IndQuestResults.Async` or uses fully-qualified call).
  - Configure severity via `.editorconfig`:
    - `dotnet_diagnostic.IQR0001.severity = suggestion` (default)
    - `dotnet_diagnostic.IQR0001.severity = warning` (stricter)
    - `dotnet_diagnostic.IQR0001.severity = none` (disable)
