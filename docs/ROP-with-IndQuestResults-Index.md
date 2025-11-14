# Railway-Oriented Programming (ROP) with IndQuestResults

## Complete Documentation Index

This documentation provides a comprehensive guide to Railway-Oriented Programming (ROP) using the IndQuestResults library. The guides are structured to take you from basic concepts to advanced patterns and best practices.

---

## 📚 Documentation Structure

### 📘 [Basic Guide: Getting Started](ROP-with-IndQuestResults-Basic.md)

**Target Audience**: Developers new to ROP or functional error handling in C#.

**What You'll Learn**:
- What is Railway-Oriented Programming?
- Basic `Result<T>` constructs
- Creating success and failure results
- Chaining computations with `Map`, `Bind`, and `Ensure`
- Handling results with pattern matching
- Common use cases and examples

**Start Here**: If you're new to ROP or the IndQuestResults library.

---

### 📙 [Advanced Guide: Async Workflows and Composition](ROP-with-IndQuestResults-Advanced.md)

**Target Audience**: Developers comfortable with basic `Result<T>` usage.

**What You'll Learn**:
- Async pipelines with `ThenAsync`, `MapAsync`, `ThenTap`
- Composing multiple results with `Combine`
- Recovering from failures with `Recover`
- Working with nullable values
- Exception handling in ROP context
- Async cancellation support

**Prerequisites**: Understanding of the Basic Guide and async/await patterns.

---

### 📗 [Best Practices Guide: Production-Ready Patterns](ROP-with-IndQuestResults-Best-Practices.md)

**Target Audience**: Advanced developers and code reviewers.

**What You'll Learn**:
- Recommended patterns and anti-patterns
- Diagnostic metadata (warnings, confidence scoring)
- Validation extensions
- Proper cancellation handling
- Analyzer rules and CI enforcement
- Code quality checklist

**Prerequisites**: Understanding of both Basic and Advanced guides.

---

## 🚀 Quick Start

### Installation

```bash
dotnet add package IndQuestResults
```

### Basic Example

```csharp
using IndQuestResults;

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

## 📖 Reading Path

### For Beginners

1. Start with the [Basic Guide](ROP-with-IndQuestResults-Basic.md)
2. Practice with the examples
3. Move to the [Advanced Guide](ROP-with-IndQuestResults-Advanced.md) when ready
4. Review the [Best Practices Guide](ROP-with-IndQuestResults-Best-Practices.md) before production

### For Experienced Developers

1. Skim the [Basic Guide](ROP-with-IndQuestResults-Basic.md) for syntax
2. Focus on the [Advanced Guide](ROP-with-IndQuestResults-Advanced.md) for async patterns
3. Study the [Best Practices Guide](ROP-with-IndQuestResults-Best-Practices.md) for production patterns

---

## 🔑 Key Concepts

### Railway-Oriented Programming

ROP uses a dual-track model:
- **Success track**: Normal computation path
- **Failure track**: Error propagation path

### Core Principles

- ✅ Avoid exceptions for control flow
- ✅ Use `Result<T>` types to carry data and errors
- ✅ Chain operations fluently with automatic short-circuiting
- ✅ Preserve exceptions in Results, don't throw them

### Essential Methods

| Method | Purpose | Example |
|--------|---------|---------|
| `Result<T>.Success(value)` | Create success | `Result<int>.Success(42)` |
| `Result<T>.WithFailure(...)` | Create failure | `Result<int>.WithFailure("Error")` |
| `.Map(func)` | Transform value | `.Map(s => s.Length)` |
| `.Bind(func)` | Chain Result operations | `.Bind(u => ValidateUser(u))` |
| `.Ensure(pred, error)` | Validate and fail | `.Ensure(v => v > 0, "Must be positive")` |
| `.Match(onS, onF)` | Extract value | `.Match(ok => ok, err => -1)` |

---

## 📋 Quick Reference

### Creating Results

```csharp
// Success
Result ok = Result.Success();
Result<int> value = Result<int>.Success(42);

// Failure
Result fail = Result.WithFailure("Error message");
Result<int> error = Result<int>.WithFailure("Invalid input");
```

### Chaining Operations

```csharp
var result = GetUser(id)
    .Ensure(u => u != null, "User not found")
    .Map(u => u.Name)
    .Ensure(n => !string.IsNullOrEmpty(n), "Name is required");
```

### Handling Results

```csharp
var output = result.Match(
    onSuccess: value => ProcessSuccess(value),
    onFailure: errors => HandleFailure(errors)
);
```

---

## 🎯 Use Cases

- ✅ **Input Validation**: Validate user input without exceptions
- ✅ **Configuration Parsing**: Parse and validate configuration files
- ✅ **Business Logic**: Chain business operations with automatic error handling
- ✅ **API Responses**: Return structured success/failure responses
- ✅ **Data Processing**: Process data pipelines with error propagation
- ✅ **Async Operations**: Handle async workflows with cancellation support

---

## 🔗 Related Resources

### Documentation

- [IndQuestResults GitHub Repository](https://github.com/your-org/IndQuestResults)
- [Result Manual](../ExxerRules/docs/Result-Manual.md) - Complete API reference
- [Functional .NET Patterns](../.cursor/rules/1021_NullParameterInitializerHandling.mdc)

### Code Standards & Rules

The following rules enforce ROP patterns and best practices in the codebase:

#### Exception Handling

- **[1026_ExceptionHandlingWithResultT.mdc](../.cursor/rules/1026_ExceptionHandlingWithResultT.mdc)** - Comprehensive standards for managing exceptions using `Result<T>` instead of throwing exceptions for control flow
  - Never throw exceptions for control flow
  - Use `ResultTryExtensions.Try` to capture and preserve exceptions
  - Handle `OperationCanceledException` separately
  - Log preserved exceptions with structured logging
  - Use recovery patterns with `Recover`

#### Cancellation Token Management

- **[1002_CancellationTokenStandards.mdc](../.cursor/rules/1002_CancellationTokenStandards.mdc)** - Comprehensive standards for accepting, respecting, enforcing, transmitting, and acknowledging cancellation tokens
  - **ACCEPT**: All async methods must accept `CancellationToken`
  - **RESPECT**: Check cancellation before expensive operations
  - **ENFORCE**: Use `CancellationAwareResult.WrapWithTimeout` for timeout enforcement
  - **TRANSMIT**: Propagate `CancellationToken` to all downstream async calls
  - **ACKNOWLEDGE**: Return functional cancellation results, never throw

#### General Result Patterns

- **[1025_ResultCancellationHandling.mdc](../.cursor/rules/1025_ResultCancellationHandling.mdc)** - Handling cancellation in functional .NET applications
- **[1043_ExxerAIResultPattern.mdc](../.cursor/rules/1043_ExxerAIResultPattern.mdc)** - General `Result<T>` pattern usage

---

## 📝 Contributing

Found an issue or have a suggestion? Please contribute to improve these guides!

---

*Last updated: 2025-01-06*

