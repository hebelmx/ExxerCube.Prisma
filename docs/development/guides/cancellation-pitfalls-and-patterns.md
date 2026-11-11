# Cancellation & ROP Compliance: Pitfalls & Patterns

**Date:** 2025-01-16  
**Purpose:** Quick reference for common pitfalls and correct patterns  
**Status:** ✅ Active Reference

---

## 🚨 Common Pitfalls

### ❌ Pitfall 1: Missing CancellationToken Parameter

**WRONG:**
```csharp
public async Task<Result<T>> ProcessAsync(TData data)
{
    // Cannot handle cancellation!
}
```

**CORRECT:**
```csharp
public async Task<Result<T>> ProcessAsync(
    TData data,
    CancellationToken cancellationToken = default)
{
    // Can handle cancellation properly
}
```

---

### ❌ Pitfall 2: Missing Early Cancellation Check

**WRONG:**
```csharp
public async Task<Result<T>> ProcessAsync(
    TData data,
    CancellationToken cancellationToken = default)
{
    // Starts work even if already cancelled
    var result = await _dependency.DoWorkAsync(data, cancellationToken);
}
```

**CORRECT:**
```csharp
public async Task<Result<T>> ProcessAsync(
    TData data,
    CancellationToken cancellationToken = default)
{
    // Early check prevents unnecessary work
    if (cancellationToken.IsCancellationRequested)
    {
        return ResultExtensions.Cancelled<T>();
    }
    
    var result = await _dependency.DoWorkAsync(data, cancellationToken);
}
```

---

### ❌ Pitfall 3: Not Passing CancellationToken to Dependencies

**WRONG:**
```csharp
// Missing CT - dependency cannot be cancelled!
var result = await _dependency.DoWorkAsync(data);
```

**CORRECT:**
```csharp
// Always pass CT to dependencies
var result = await _dependency.DoWorkAsync(data, cancellationToken)
    .ConfigureAwait(false);
```

---

### ❌ Pitfall 4: Not Propagating Cancellation from Dependencies

**WRONG:**
```csharp
var result = await _dependency.DoWorkAsync(data, cancellationToken);
if (result.IsFailure)
{
    return Result<T>.WithFailure(result.Error!);
}
// Cancelled results treated as failures!
```

**CORRECT:**
```csharp
var result = await _dependency.DoWorkAsync(data, cancellationToken)
    .ConfigureAwait(false);

// Check cancellation FIRST
if (result.IsCancelled())
{
    return ResultExtensions.Cancelled<T>();
}

if (result.IsFailure)
{
    return Result<T>.WithFailure(result.Error!);
}
```

---

### ❌ Pitfall 5: Missing OperationCanceledException Handling

**WRONG:**
```csharp
try
{
    var result = await _dependency.DoWorkAsync(data, cancellationToken);
}
catch (Exception ex)
{
    // Cancellation treated as generic error!
    return Result<T>.WithFailure($"Error: {ex.Message}");
}
```

**CORRECT:**
```csharp
try
{
    var result = await _dependency.DoWorkAsync(data, cancellationToken)
        .ConfigureAwait(false);
    
    if (result.IsCancelled())
    {
        return ResultExtensions.Cancelled<T>();
    }
    
    return result;
}
catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
{
    _logger.LogInformation("Operation cancelled");
    return ResultExtensions.Cancelled<T>();
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error in operation");
    return Result<T>.WithFailure($"Error: {ex.Message}", default, ex);
}
```

---

### ❌ Pitfall 6: SemaphoreSlim Without CancellationToken

**WRONG:**
```csharp
// WILL HANG on cancellation!
await semaphore.WaitAsync();
```

**CORRECT:**
```csharp
// Always pass CT to SemaphoreSlim
await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
```

---

### ❌ Pitfall 7: Interface Without CancellationToken

**WRONG:**
```csharp
public interface IService
{
    Task<Result<T>> ProcessAsync(TData data);
    // Implementation cannot be cancellation-aware!
}
```

**CORRECT:**
```csharp
public interface IService
{
    Task<Result<T>> ProcessAsync(
        TData data,
        CancellationToken cancellationToken = default);
}
```

**⚠️ Note:** Fix interfaces FIRST, then update all implementations.

---

### ❌ Pitfall 8: Treating Cancellation as Failure

**WRONG:**
```csharp
// "HandlesGracefully" does NOT mean "do nothing"
// Cancellation is NOT a failure - it's an operational signal
if (result.IsFailure) // This catches cancelled results incorrectly!
{
    return Result<T>.WithFailure(result.Error!);
}
```

**CORRECT:**
```csharp
// Cancellation is a distinct state, not a failure
if (result.IsCancelled())
{
    return ResultExtensions.Cancelled<T>();
}

if (result.IsFailure)
{
    return Result<T>.WithFailure(result.Error!);
}
```

---

## ✅ Correct Pattern Template

Use this template for all async methods:

```csharp
public async Task<Result<TResult>> MethodAsync(
    TParams parameters,
    CancellationToken cancellationToken = default)
{
    // 1. Early cancellation check
    if (cancellationToken.IsCancellationRequested)
    {
        _logger.LogWarning("Operation cancelled before starting");
        return ResultExtensions.Cancelled<TResult>();
    }

    // 2. Input validation
    if (parameters == null)
        return Result<TResult>.WithFailure("Parameters cannot be null");

    try
    {
        // 3. Call dependencies with CT
        var result = await _dependency.DoWorkAsync(parameters, cancellationToken)
            .ConfigureAwait(false);
        
        // 4. Propagate cancellation FIRST
        if (result.IsCancelled())
        {
            _logger.LogWarning("Operation cancelled by dependency");
            return ResultExtensions.Cancelled<TResult>();
        }
        
        // 5. Check failure
        if (result.IsFailure)
        {
            return Result<TResult>.WithFailure(result.Error!);
        }
        
        // 6. Continue with work...
        return Result<TResult>.Success(value);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        _logger.LogInformation("Operation cancelled");
        return ResultExtensions.Cancelled<TResult>();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in operation");
        return Result<TResult>.WithFailure($"Error: {ex.Message}", default, ex);
    }
}
```

---

## 📋 Quick Checklist

Every async method MUST:

- ✅ Accept `CancellationToken cancellationToken = default`
- ✅ Check `cancellationToken.IsCancellationRequested` at start
- ✅ Pass `cancellationToken` to ALL dependency calls
- ✅ Use `.ConfigureAwait(false)` in library code (Application/Infrastructure)
- ✅ Check `result.IsCancelled()` after dependency calls
- ✅ Return `ResultExtensions.Cancelled<T>()` for cancellation
- ✅ Catch `OperationCanceledException` explicitly
- ✅ Log cancellation events

---

## 🎯 Reference Implementations

**Model Examples:**
- `DocumentIngestionService.cs` - Complete cancellation handling
- `DecisionLogicService.cs` - Proper propagation patterns

**See Also:**
- `docs/audit/cancellation-rop-compliance-audit.md` - Full audit report
- `docs/ROP-with-IndQuestResults-Best-Practices.md` - ROP patterns
- `docs/qa/development-checklist-async-requirements.md` - Detailed checklist

---

**Last Updated:** 2025-01-16

