# Railway-Oriented Programming (ROP) with IndQuestResults

## 📙 Advanced Guide: Async Workflows and Composition

### Target Audience

This guide is for developers comfortable with basic `Result<T>` usage who are ready to handle:

- Async workflows and pipelines
- Multiple result aggregation
- Error recovery and fallback strategies
- Exception handling in ROP context
- Cancellation token support

**Prerequisites**: Understanding of the [Basic Guide](ROP-with-IndQuestResults-Basic.md) and async/await patterns in C#.

---

## Table of Contents

1. [Async Pipelines](#1-async-pipelines)
2. [Composing Multiple Results](#2-composing-multiple-results)
3. [Recovering Gracefully](#3-recovering-gracefully)
4. [Working with Nullables](#4-working-with-nullables)
5. [Exception Handling](#5-exception-handling)
6. [Async Cancellation](#6-async-cancellation)
7. [Summary](#7-summary)

---

## 1. Async Pipelines

### Overview

Async pipelines allow you to chain asynchronous operations while maintaining the ROP pattern. Operations automatically short-circuit on failure.

### Basic Async Chain

```csharp
var result = await GetUserAsync(id)
    .ThenValidateAsync(u => ValidateUserAsync(u))
    .ThenAsync(u => CreateProfileAsync(u))
    .ThenTap(p => CacheProfileAsync(p));
```

### Key Async Methods

#### ThenAsync: Continue with Async Operation

```csharp
// Chain async operations that return Results
var result = await GetUserAsync(id)
    .ThenAsync(user => SaveUserAsync(user))
    .ThenAsync(user => SendNotificationAsync(user));
```

#### MapAsync: Transform with Async Function

```csharp
// Transform value using async function
var result = await GetUserAsync(id)
    .MapAsync(user => LoadUserPreferencesAsync(user.Id));
```

#### ThenTap: Side Effects Without Changing Result

```csharp
// Execute side effect without affecting the result
var result = await GetUserAsync(id)
    .ThenTap(user => LogUserAccessAsync(user))
    .ThenTap(user => UpdateLastSeenAsync(user));
// Result still contains the original user
```

#### ThenValidateAsync: Async Validation

```csharp
// Validate asynchronously within the chain
var result = await GetUserAsync(id)
    .ThenValidateAsync(user => IsUserActiveAsync(user))
    .ThenAsync(user => ProcessUserAsync(user));
```

### Complete Async Example

```csharp
public async Task<Result<Order>> ProcessOrderAsync(int orderId, CancellationToken ct = default)
{
    return await GetOrderAsync(orderId, ct)
        .ThenValidateAsync(o => ValidateOrderAsync(o, ct))
        .ThenAsync(o => CheckInventoryAsync(o, ct))
        .ThenAsync(o => CalculateTotalAsync(o, ct))
        .ThenAsync(o => ApplyDiscountAsync(o, ct))
        .ThenAsync(o => SaveOrderAsync(o, ct))
        .ThenTap(o => SendConfirmationEmailAsync(o, ct));
}
```

---

## 2. Composing Multiple Results

### Combining Multiple Results

When you need to aggregate multiple operations, use `Combine` to merge their results.

#### Basic Combine

```csharp
var result1 = ValidateInput(input);
var result2 = CheckPermissions(user);
var result3 = VerifyResource(resource);

var combined = Result.Combine(result1, result2, result3);

if (combined.IsFailure)
{
    // All errors are aggregated
    return combined.Errors;
}
```

#### Combine with Values

```csharp
var userResult = GetUserAsync(id);
var profileResult = GetProfileAsync(id);
var settingsResult = GetSettingsAsync(id);

var combined = Result.Combine(
    userResult,
    profileResult,
    settingsResult
);

if (combined.IsSuccess)
{
    var user = userResult.Value;
    var profile = profileResult.Value;
    var settings = settingsResult.Value;
    // Use all values
}
```

#### Combine with Result<T>

```csharp
// Preserve successful values when combining
var combined = Result<(User, Profile, Settings)>.Combine(
    userResult,
    profileResult,
    settingsResult
);

if (combined.IsSuccess)
{
    var (user, profile, settings) = combined.Value;
}
```

### Async Combine

```csharp
var userTask = GetUserAsync(id);
var profileTask = GetProfileAsync(id);
var settingsTask = GetSettingsAsync(id);

await Task.WhenAll(userTask, profileTask, settingsTask);

var combined = Result.Combine(
    await userTask,
    await profileTask,
    await settingsTask
);
```

---

## 3. Recovering Gracefully

### Recover: Provide Fallback on Failure

The `Recover` method allows you to provide a fallback value or operation when a result fails.

#### Basic Recovery

```csharp
// Provide default value on failure
var fallback = riskyResult.Recover(errors => Result<int>.Success(-1));
```

#### Recovery with Operation

```csharp
// Attempt recovery operation
var result = await LoadFromCacheAsync(key)
    .RecoverAsync(errors => LoadFromDatabaseAsync(key))
    .RecoverAsync(errors => LoadFromBackupAsync(key));
```

#### Conditional Recovery

```csharp
var result = riskyOperation()
    .Recover(errors => 
    {
        if (errors.Any(e => e.Contains("timeout")))
        {
            return RetryOperation();
        }
        return Result<int>.WithFailure("Unrecoverable error");
    });
```

### Complete Recovery Example

```csharp
public async Task<Result<User>> GetUserWithFallbackAsync(int id, CancellationToken ct = default)
{
    return await GetUserFromCacheAsync(id, ct)
        .RecoverAsync(errors => GetUserFromDatabaseAsync(id, ct))
        .RecoverAsync(errors => GetUserFromBackupAsync(id, ct))
        .RecoverAsync(errors => CreateDefaultUserAsync(id, ct));
}
```

---

## 4. Working with Nullables

### Handling Nullable Values

ROP works seamlessly with nullable types, allowing you to handle null values explicitly.

#### Basic Nullable Handling

```csharp
Result<int?> nullable = Result<int?>.Success(null);

var adjusted = nullable
    .Ensure(val => val.HasValue, "Missing value")
    .Map(val => val!.Value + 1);
```

#### Nullable in Chains

```csharp
public Result<int> ProcessNullable(int? value)
{
    return Result<int?>.Success(value)
        .Ensure(v => v.HasValue, "Value is required")
        .Map(v => v!.Value)
        .Ensure(v => v > 0, "Value must be positive");
}
```

#### Nullable with Default

```csharp
var result = nullableValue
    .Recover(errors => Result<int?>.Success(defaultValue))
    .Map(v => v ?? 0);
```

---

## 5. Exception Handling

### Preserving Exceptions (Not Throwing)

In ROP, exceptions are captured and preserved in the Result, not thrown. This maintains the functional flow.

#### Try: Capture Exceptions

```csharp
var result = ResultTryExtensions.Try(
    () => riskyFunc(),
    ex => $"Error: {ex.Message}"
);
```

#### Handling Faulted Results

```csharp
var result = ResultTryExtensions.Try(
    () => riskyOperation(),
    ex => $"Operation failed: {ex.Message}"
);

if (result.IsFailure && result.IsFaulted)
{
    logger.LogError(result.Exception, "Operation failed");
    // Exception is preserved, not thrown
}
```

#### Async Try

```csharp
var result = await ResultTryExtensions.TryAsync(
    async () => await riskyAsyncOperation(),
    ex => $"Async operation failed: {ex.Message}"
);
```

### Complete Exception Handling Example

```csharp
public Result<Data> LoadDataSafely(string path)
{
    return ResultTryExtensions.Try(
        () => File.ReadAllText(path),
        ex => $"Failed to read file: {ex.Message}"
    )
    .Map(content => JsonSerializer.Deserialize<Data>(content))
    .Ensure(data => data != null, "Invalid JSON format");
}
```

---

## 6. Async Cancellation

### Cancellation-Aware Results

Handle cancellation tokens gracefully in async ROP pipelines.

#### WrapCancellationAware

```csharp
var result = await CancellationAwareResult.WrapCancellationAware(
    async ct => await LoadAsync(ct),
    cancellationToken
);

if (result.IsCancelled())
{
    // Handle cancellation gracefully
    return Result<T>.WithFailure("Operation was cancelled");
}
```

#### WrapWithTimeout

```csharp
var result = await CancellationAwareResult.WrapWithTimeout(
    async ct => await DoWorkAsync(ct),
    timeout: TimeSpan.FromSeconds(10),
    cancellationToken
);

if (result.IsCancelled())
{
    // Handle timeout or cancellation
}
```

#### Complete Cancellation Example

```csharp
public async Task<Result<Report>> GenerateReportAsync(
    int reportId, 
    CancellationToken ct = default)
{
    return await CancellationAwareResult.WrapCancellationAware(
        async cancellationToken => await LoadDataAsync(reportId, cancellationToken)
            .ThenAsync(data => ProcessDataAsync(data, cancellationToken))
            .ThenAsync(data => FormatReportAsync(data, cancellationToken)),
        ct
    );
}
```

---

## 7. Summary

### Key Advanced Patterns

- ✅ **Use `ThenAsync`, `MapAsync`, `ThenTap`** to build composable async chains.
- ✅ **Always prefer Result-based flows** to exceptions for control flow.
- ✅ **Combine multiple Results** using `Result.Combine()` for aggregation.
- ✅ **Recover from failures** using `Recover()` and `RecoverAsync()`.
- ✅ **Handle cancellation** using `CancellationAwareResult` wrappers.
- ✅ **Preserve exceptions** in Results rather than throwing them.

### Best Practices

| Pattern | When to Use | Example |
|---------|-------------|---------|
| `ThenAsync` | Chain async operations | `GetUser().ThenAsync(SaveUser)` |
| `MapAsync` | Transform with async function | `GetUser().MapAsync(LoadProfile)` |
| `ThenTap` | Side effects | `GetUser().ThenTap(LogAccess)` |
| `Combine` | Aggregate multiple Results | `Result.Combine(r1, r2, r3)` |
| `Recover` | Fallback on failure | `LoadCache().Recover(LoadDb)` |
| `Try` | Capture exceptions | `ResultTryExtensions.Try(risky)` |

### Next Steps

- 📗 Review the [Best Practices Guide](ROP-with-IndQuestResults-Best-Practices.md) for production-ready patterns and code quality.
- 🔍 Explore analyzer rules for enforcing ROP patterns in your codebase.

---

*Last updated: 2024*

