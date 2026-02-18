# Railway-Oriented Programming (ROP) with IndQuestResults

## 📗 Best Practices Guide: Robust and Scalable ROP in Production

### Objective

This guide outlines best practices for writing maintainable and scalable ROP-based logic using IndQuestResults. It is targeted at advanced developers and code reviewers who want to ensure production-ready, maintainable code.

**Prerequisites**: Understanding of the [Basic Guide](ROP-with-IndQuestResults-Basic.md) and [Advanced Guide](ROP-with-IndQuestResults-Advanced.md).

---

## Table of Contents

1. [Do This: Recommended Patterns](#1-do-this-recommended-patterns)
2. [Avoid This: Anti-Patterns](#2-avoid-this-anti-patterns)
3. [Diagnostic Metadata](#3-diagnostic-metadata)
4. [Validation Extensions](#4-validation-extensions)
5. [Cancellation Handling](#5-cancellation-handling)
6. [Analyzers and CI Enforcement](#6-analyzers-and-ci-enforcement)
7. [Summary](#7-summary)

---

## 1. Do This: Recommended Patterns

### 1.1 Use `Result<T>` Everywhere for Failure-Prone Logic

**✅ Good**: Return `Result<T>` for operations that can fail.

```csharp
public Result<User> CreateUser(string name)
{
    return name.EnsureNotNull("name")
               .Ensure(n => !string.IsNullOrWhiteSpace(n), "Name cannot be empty")
               .Ensure(n => n.Length >= 3, "Name must be at least 3 characters")
               .Map(n => new User(n));
}
```

**❌ Bad**: Throwing exceptions for validation.

```csharp
public User CreateUser(string name)
{
    if (string.IsNullOrWhiteSpace(name))
        throw new ArgumentException("Name cannot be empty");
    return new User(name);
}
```

### 1.2 Prefer Fluent Pipelines Over If-Chains

**✅ Good**: Use fluent chaining for readability.

```csharp
return GetUser()
    .Ensure(u => u.Age >= 18, "User must be an adult")
    .Ensure(u => u.IsActive, "User must be active")
    .Map(u => u.ToDto());
```

**❌ Bad**: Nested if statements.

```csharp
var user = GetUser();
if (user.IsSuccess)
{
    if (user.Value.Age >= 18)
    {
        if (user.Value.IsActive)
        {
            return user.Value.ToDto();
        }
    }
}
return Result<UserDto>.WithFailure("Validation failed");
```

### 1.3 Always Handle `IsFailure`

**✅ Good**: Explicitly check and handle failures.

```csharp
var result = ProcessOrder(orderId);

if (result.IsFailure)
{
    logger.LogWarning("Order processing failed: {Errors}", result.Errors);
    return BadRequest(result.Errors);
}

return Ok(result.Value);
```

**❌ Bad**: Accessing `.Value` without checking.

```csharp
var result = ProcessOrder(orderId);
return Ok(result.Value); // ⚠️ May throw if IsFailure
```

### 1.4 Use `.Match()` to Unify Flow

**✅ Good**: Use pattern matching for clean flow control.

```csharp
var output = result.Match(
    onSuccess: val => Ok(val),
    onFailure: errs => BadRequest(errs)
);
```

**✅ Alternative**: Using switch expressions (C# 8+).

```csharp
var output = result switch
{
    { IsSuccess: true } => Ok(result.Value),
    { IsFailure: true } => BadRequest(result.Errors)
};
```

### 1.5 Logging in ROP Pipelines

**✅ Good**: Log at appropriate points in the pipeline.

```csharp
public async Task<Result<Order>> ProcessOrderAsync(int orderId, ILogger logger)
{
    logger.LogInformation("Processing order {OrderId}", orderId);
    
    return await GetOrderAsync(orderId)
        .ThenTap(o => logger.LogDebug("Retrieved order {OrderId}", o.Id))
        .ThenValidateAsync(o => ValidateOrderAsync(o))
        .ThenTap(o => logger.LogInformation("Order {OrderId} validated", o.Id))
        .ThenAsync(o => SaveOrderAsync(o))
        .ThenTap(o => logger.LogInformation("Order {OrderId} processed successfully", o.Id));
}
```

---

## 2. Avoid This: Anti-Patterns

### 2.1 Throwing Exceptions for Nulls

**❌ Bad**: Using exceptions for null checks.

```csharp
public Result<User> GetUser(int id)
{
    var user = _repository.Find(id);
    if (user == null)
        throw new ArgumentNullException(nameof(user));
    return Result<User>.Success(user);
}
```

**✅ Good**: Using Result-based validation.

```csharp
public Result<User> GetUser(int id)
{
    return Result<int>.Success(id)
        .Bind(id => _repository.Find(id))
        .Ensure(user => user != null, "User not found")
        .Map(user => user!);
}
```

### 2.2 Accessing `.Value` Without `.IsSuccess`

**❌ Bad**: Unsafe value access.

```csharp
var result = GetUser(id);
var name = result.Value.Name; // ⚠️ May throw
```

**✅ Good**: Safe value access.

```csharp
var result = GetUser(id);
if (result.IsSuccess)
{
    var name = result.Value.Name; // Safe
}
```

**✅ Better**: Using Match.

```csharp
var name = result.Match(
    onSuccess: user => user.Name,
    onFailure: _ => "Unknown"
);
```

### 2.3 Mixing Exceptions and Results

**❌ Bad**: Throwing exceptions inside Result methods.

```csharp
public Result<int> Divide(int a, int b)
{
    if (b == 0)
        throw new DivideByZeroException(); // ❌ Don't throw
    
    return Result<int>.Success(a / b);
}
```

**✅ Good**: Returning failure Results.

```csharp
public Result<int> Divide(int a, int b)
{
    return Result<int>.Success(a)
        .Ensure(_ => b != 0, "Division by zero is not allowed")
        .Map(_ => a / b);
}
```

### 2.4 Ignoring Errors

**❌ Bad**: Swallowing errors without handling.

```csharp
var result = ProcessData();
// No error handling - errors are lost
```

**✅ Good**: Always handle or propagate errors.

```csharp
var result = ProcessData();
if (result.IsFailure)
{
    logger.LogError("Processing failed: {Errors}", result.Errors);
    return result; // Propagate error
}
```

---

## 3. Diagnostic Metadata

### Using Warnings and Confidence Scoring

For operations that may succeed but with degraded quality, use warnings and confidence metrics.

### Basic Warning Usage

```csharp
return Result<int>.WithWarnings(
    warnings: ["Partial data available"],
    value: 42,
    confidence: 0.6,
    missingDataRatio: 0.4
);
```

### Checking Warnings

```csharp
if (result.HasWarnings)
{
    logger.LogWarning("Result has warnings: {Warnings}", result.Warnings);
    // Continue processing but log degradation
}
```

### Complete Example

```csharp
public Result<Data> LoadDataWithFallback(int id)
{
    var primaryResult = LoadFromPrimarySource(id);
    
    if (primaryResult.IsSuccess)
    {
        return primaryResult;
    }
    
    var fallbackResult = LoadFromFallbackSource(id);
    
    if (fallbackResult.IsSuccess)
    {
        return Result<Data>.WithWarnings(
            warnings: ["Data loaded from fallback source"],
            value: fallbackResult.Value,
            confidence: 0.7
        );
    }
    
    return Result<Data>.WithFailure("Unable to load data from any source");
}
```

---

## 4. Validation Extensions

### Discoverable Validations

Use extension methods for common validation patterns to improve readability and consistency.

### Custom Validation Extensions

```csharp
public static class ValidationExtensions
{
    public static Result<string> EnsureValidEmail(this Result<string> result, string parameterName = "email")
    {
        return result.Ensure(
            email => !string.IsNullOrWhiteSpace(email) && email.Contains("@"),
            $"{parameterName} must be a valid email address"
        );
    }
    
    public static Result<int> EnsurePositive(this Result<int> result, string parameterName = "value")
    {
        return result.Ensure(
            value => value > 0,
            $"{parameterName} must be positive"
        );
    }
    
    public static Result<T> EnsureNotNull<T>(this Result<T?> result, string parameterName) where T : class
    {
        return result.Ensure(
            value => value != null,
            $"{parameterName} cannot be null"
        ).Map(value => value!);
    }
}
```

### Usage

```csharp
public Result<User> CreateUser(string name, string email, int age)
{
    return Result<string>.Success(name)
        .EnsureNotNull("name")
        .Ensure(n => n.Length >= 3, "Name must be at least 3 characters")
        .Map(_ => email)
        .EnsureValidEmail("email")
        .Map(_ => age)
        .EnsurePositive("age")
        .Map(a => new User(name, email, age));
}
```

---

## 5. Cancellation Handling

### Proper Cancellation Token Support

Always support cancellation tokens in async operations.

### ✅ Good: Cancellation-Aware Operations

```csharp
public async Task<Result<Report>> GenerateReportAsync(
    int reportId,
    CancellationToken cancellationToken = default)
{
    return await CancellationAwareResult.WrapWithTimeout(
        async ct => await LoadDataAsync(reportId, ct)
            .ThenAsync(data => ProcessDataAsync(data, ct))
            .ThenAsync(data => FormatReportAsync(data, ct)),
        timeout: TimeSpan.FromSeconds(30),
        cancellationToken
    );
}
```

### ✅ Good: Propagating Cancellation

```csharp
public async Task<Result<List<User>>> GetUsersAsync(
    int page,
    int pageSize,
    CancellationToken cancellationToken = default)
{
    return await ValidatePagination(page, pageSize)
        .ThenAsync(_ => LoadUsersAsync(page, pageSize, cancellationToken))
        .ThenAsync(users => EnrichUsersAsync(users, cancellationToken));
}
```

### ✅ Good: Handling Partial Results with Cancellation

**Pattern**: When cancellation occurs during batch operations, return partial results using `WithWarnings()` instead of losing completed work.

```csharp
public async Task<Result<List<ProcessingResult>>> ProcessDocumentsAsync(
    IEnumerable<ImageData> imageDataList,
    ProcessingConfig config,
    int maxConcurrency = 5,
    CancellationToken cancellationToken = default)
{
    // Early cancellation check
    if (cancellationToken.IsCancellationRequested)
    {
        return ResultExtensions.Cancelled<List<ProcessingResult>>();
    }

    var tasks = imageDataList.Select(async imageData =>
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            return await ProcessDocumentAsync(imageData, config, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    });

    var results = await Task.WhenAll(tasks);

    // Categorize results
    var successfulResults = new List<ProcessingResult>();
    var cancelledResults = results.Where(r => r.IsCancelled()).ToList();
    var failedResults = results.Where(r => !r.IsSuccess && !r.IsCancelled()).ToList();

    foreach (var result in results)
    {
        if (result.IsSuccess && result.Value != null)
        {
            successfulResults.Add(result.Value);
        }
    }

    // Handle cancellation with partial results
    var wasCancelled = cancellationToken.IsCancellationRequested || cancelledResults.Any();

    if (wasCancelled)
    {
        if (successfulResults.Count > 0)
        {
            // Return partial results with warning about cancellation
            var totalRequested = imageDataList.Count();
            var completed = successfulResults.Count;
            var cancelled = cancelledResults.Count;
            var confidence = (double)completed / totalRequested;
            var missingDataRatio = (double)(cancelled + failedResults.Count) / totalRequested;

            _logger.LogWarning(
                "Batch processing cancelled. Returning {CompletedCount} of {TotalCount} processed items. " +
                "Cancelled: {CancelledCount}, Failed: {FailedCount}",
                completed, totalRequested, cancelled, failedResults.Count);

            return Result<List<ProcessingResult>>.WithWarnings(
                warnings: new[] { $"Operation was cancelled. Processed {completed} of {totalRequested} items." },
                value: successfulResults,
                confidence: confidence,
                missingDataRatio: missingDataRatio
            );
        }
        else
        {
            // No partial results - return cancelled
            return ResultExtensions.Cancelled<List<ProcessingResult>>();
        }
    }

    // No cancellation - return success or handle failures
    if (failedResults.Any())
    {
        _logger.LogWarning("Batch processing completed with {FailedCount} failures", failedResults.Count);
    }

    return Result<List<ProcessingResult>>.Success(successfulResults);
}
```

**Key Points**:
- ✅ **Preserve completed work**: Don't lose partial results when cancellation occurs
- ✅ **Use `WithWarnings()`**: Signal cancellation while returning partial data
- ✅ **Calculate metrics**: Provide `confidence` and `missingDataRatio` for quality assessment
- ✅ **Log appropriately**: Use structured logging to track cancellation and partial completion
- ✅ **Return cancelled only when no work done**: If no items completed, return `Cancelled<T>()`

**Consumer Pattern**:

```csharp
var result = await ProcessDocumentsAsync(documents, config, cancellationToken);

if (result.HasWarnings)
{
    // Partial results with cancellation - still usable!
    _logger.LogWarning("Received partial results: {Warnings}", result.Warnings);
    _logger.LogInformation("Confidence: {Confidence}, Missing: {MissingRatio}", 
        result.Confidence, result.MissingDataRatio);
    
    // Process partial results
    await ProcessResultsAsync(result.Value);
}
else if (result.IsCancelled())
{
    // No work completed - handle as full cancellation
    _logger.LogInformation("Operation was cancelled with no completed work");
}
else if (result.IsSuccess)
{
    // Full success - process all results
    await ProcessResultsAsync(result.Value);
}
```

### ❌ Bad: Ignoring Cancellation

```csharp
public async Task<Result<List<User>>> GetUsersAsync(int page, int pageSize)
{
    // ❌ No cancellation token support
    return await LoadUsersAsync(page, pageSize)
        .ThenAsync(users => EnrichUsersAsync(users));
}
```

### ❌ Bad: Losing Partial Results on Cancellation

```csharp
// ❌ WRONG: Loses all completed work when cancellation occurs
if (cancellationToken.IsCancellationRequested)
{
    return ResultExtensions.Cancelled<List<ProcessingResult>>(); // All work lost!
}
```

---

## 6. Analyzers and CI Enforcement

### Recommended Analyzer Rules

Configure analyzers to enforce ROP patterns in your codebase:

#### IQR201: No `ArgumentNullException`

**Rule**: Prefer `Result<T>.WithFailure()` over throwing `ArgumentNullException`.

**Configuration** (`.editorconfig`):

```ini
[*.cs]
dotnet_diagnostic.IQR201.severity = error
```

#### IQR202: No `throw` in `Result<T>` Methods

**Rule**: Methods returning `Result<T>` should not throw exceptions.

**Configuration**:

```ini
[*.cs]
dotnet_diagnostic.IQR202.severity = error
```

#### IQR301–302: Preserve Exceptions in Failure Results

**Rule**: When capturing exceptions, always preserve them in the Result.

**Configuration**:

```ini
[*.cs]
dotnet_diagnostic.IQR301.severity = warning
dotnet_diagnostic.IQR302.severity = warning
```

### CI Integration

Add analyzer checks to your CI pipeline:

```yaml
- name: Run Analyzers
  run: dotnet build --no-restore -warnaserror
```

---

## 7. Summary

### Quick Reference Table

| Principle | Guidance | Example |
|-----------|----------|---------|
| **Use `Result<T>`** | Everywhere instead of throw | `Result<User>.Success(user)` |
| **Avoid exception flow** | Use Map/Bind/Ensure | `.Map(u => u.ToDto())` |
| **Validate fluently** | Use `.EnsureNotNull()` or extensions | `.EnsureNotNull("user")` |
| **Aggregate and recover** | `Combine`, `Recover`, `Match` | `Result.Combine(r1, r2)` |
| **Handle cancellation** | Always support `CancellationToken` | `async (ct) => await Load(ct)` |
| **Partial results on cancel** | Use `WithWarnings()` to preserve work | `Result<T>.WithWarnings(warnings, partialData)` |
| **Log appropriately** | Use structured logging in pipelines | `.ThenTap(o => logger.Log(...))` |
| **Audit in CI** | Use analyzers for rule enforcement | Configure `.editorconfig` |

### Code Quality Checklist

- [ ] All failure-prone operations return `Result<T>`
- [ ] No exceptions thrown for control flow
- [ ] All async methods support `CancellationToken`
- [ ] Batch operations preserve partial results on cancellation using `WithWarnings()`
- [ ] Failures are explicitly handled or propagated
- [ ] Validation uses fluent extensions where appropriate
- [ ] Logging is integrated into pipelines
- [ ] Analyzers are configured and enforced in CI

### Related Guides

- 📘 [Basic Guide](ROP-with-IndQuestResults-Basic.md) - Getting started with ROP
- 📙 [Advanced Guide](ROP-with-IndQuestResults-Advanced.md) - Async workflows and composition

---

By following these guidelines, your ROP adoption will be consistent, robust, and production-ready.

*Last updated: 2024*

