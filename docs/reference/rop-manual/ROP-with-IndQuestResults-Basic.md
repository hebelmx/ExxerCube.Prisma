# Railway-Oriented Programming (ROP) with IndQuestResults

## 📘 Basic Guide: Getting Started

### Purpose

This guide introduces the fundamental concepts of Railway-Oriented Programming (ROP) using the IndQuestResults library. It's designed for developers new to ROP or functional error handling in C#.

**Prerequisites**: Basic understanding of C# and LINQ.

---

## Table of Contents

1. [What is Railway-Oriented Programming?](#1-what-is-railway-oriented-programming)
2. [Getting Started with IndQuestResults](#2-getting-started-with-indquestresults)
3. [Core Concepts](#3-core-concepts)
4. [Common Use Cases](#4-common-use-cases)
5. [Summary](#5-summary)

---

## 1. What is Railway-Oriented Programming?

Railway-Oriented Programming is a pattern for handling operations that can fail, using a dual-track model:

- **Success track**: The normal path of computation where operations succeed.
- **Failure track**: The path when something goes wrong, allowing errors to propagate without exceptions.

### Key Principles

In ROP, we:

- **Avoid exceptions for control flow** — Use `Result<T>` types instead of throwing exceptions.
- **Use Result types** — Carry both data and errors in a single type.
- **Chain operations fluently** — Build pipelines where failure short-circuits automatically.

### Visual Representation

```
Success Track:  [Operation 1] → [Operation 2] → [Operation 3] → Success
Failure Track:  [Operation 1] → [Operation 2] ✗ → Error (stops here)
```

---

## 2. Getting Started with IndQuestResults

### Installation

Install the package via NuGet:

```bash
Install-Package IndQuestResults
```

Or via .NET CLI:

```bash
dotnet add package IndQuestResults
```

### Basic Namespace

```csharp
using IndQuestResults;
```

---

## 3. Core Concepts

### 3.1 Creating Results

#### Success Results

```csharp
// Non-generic success
Result ok = Result.Success();

// Generic success with value
Result<int> good = Result<int>.Success(42);
Result<string> message = Result<string>.Success("Hello, World!");
```

#### Failure Results

```csharp
// Non-generic failure
Result fail = Result.WithFailure("Something went wrong");

// Generic failure
Result<int> bad = Result<int>.WithFailure("Invalid number");
Result<int> badMultiple = Result<int>.WithFailure("Error 1", "Error 2");
```

### 3.2 Chaining Computations

#### Map: Transform Success Values

```csharp
// Transform a successful value
var lengthResult = Result<string>.Success("hello")
    .Map(s => s.Length); // Result<int>.Success(5)
```

#### Ensure: Validate and Short-Circuit

```csharp
// Validate and fail if condition is not met
var validatedResult = Result<string>.Success("hello")
    .Map(s => s.Length)
    .Ensure(len => len > 0, "Empty string not allowed");
```

#### Bind: Chain Operations That Return Results

```csharp
// Chain operations that return Results
var chainedResult = GetUser(id)
    .Bind(user => ValidateUser(user))
    .Bind(user => SaveUser(user));
```

### 3.3 Handling Results

#### Pattern Matching with Match

```csharp
int final = lengthResult.Match(
    onSuccess: len => len,
    onFailure: errs => -1
);
```

#### Conditional Checks

```csharp
if (result.IsSuccess)
{
    var value = result.Value; // Safe to access
    // Process success
}

if (result.IsFailure)
{
    var errors = result.Errors; // Get all error messages
    // Handle failure
}
```

#### Complete Example

```csharp
public Result<int> CalculateAge(string birthDate)
{
    return Result<string>.Success(birthDate)
        .Ensure(d => !string.IsNullOrWhiteSpace(d), "Birth date cannot be empty")
        .Map(d => DateTime.Parse(d))
        .Ensure(dt => dt <= DateTime.Today, "Birth date cannot be in the future")
        .Map(dt => (DateTime.Today - dt).Days / 365);
}
```

---

## 4. Common Use Cases

### 4.1 Validating User Input

```csharp
public Result<User> CreateUser(string name, string email)
{
    return Result<string>.Success(name)
        .Ensure(n => !string.IsNullOrWhiteSpace(n), "Name is required")
        .Ensure(n => n.Length >= 3, "Name must be at least 3 characters")
        .Map(n => new { Name = n, Email = email })
        .Ensure(u => IsValidEmail(u.Email), "Invalid email format")
        .Map(u => new User(u.Name, u.Email));
}
```

### 4.2 Parsing Configuration Files

```csharp
public Result<AppConfig> LoadConfig(string configPath)
{
    return Result<string>.Success(configPath)
        .Ensure(p => File.Exists(p), "Config file not found")
        .Map(p => File.ReadAllText(p))
        .Map(json => JsonSerializer.Deserialize<AppConfig>(json))
        .Ensure(c => c != null, "Failed to parse config")
        .Ensure(c => c.ApiKey != null, "ApiKey is required");
}
```

### 4.3 Chaining Business Logic

```csharp
public Result<Order> ProcessOrder(int orderId)
{
    return GetOrder(orderId)
        .Ensure(o => o.Status == OrderStatus.Pending, "Order is not pending")
        .Bind(o => ValidateInventory(o))
        .Bind(o => CalculateTotal(o))
        .Bind(o => ApplyDiscount(o))
        .Bind(o => SaveOrder(o));
}
```

---

## 5. Summary

### Key Takeaways

- ✅ **Prefer `Result<T>` over throwing exceptions** for control flow.
- ✅ **Chain operations** using `Map`, `Bind`, and `Ensure`.
- ✅ **Use `.Match()`** to extract values in a controlled way.
- ✅ **Validate early** using `Ensure` to short-circuit on failures.

### Next Steps

- 📙 Continue to the [Advanced Guide](ROP-with-IndQuestResults-Advanced.md) for async workflows and composition.
- 📗 Review the [Best Practices Guide](ROP-with-IndQuestResults-Best-Practices.md) for production-ready patterns.

---

## Quick Reference

| Method | Purpose | Returns |
|--------|---------|---------|
| `Result.Success()` | Create success result | `Result` |
| `Result<T>.Success(value)` | Create success with value | `Result<T>` |
| `Result.WithFailure(...)` | Create failure result | `Result` |
| `Result<T>.WithFailure(...)` | Create failure result | `Result<T>` |
| `.Map(func)` | Transform success value | `Result<TOut>` |
| `.Bind(func)` | Chain Result-returning operation | `Result<TOut>` |
| `.Ensure(predicate, error)` | Validate and fail if false | `Result<T>` |
| `.Match(onSuccess, onFailure)` | Extract value or handle error | `T` |

---

*Last updated: 2024*

