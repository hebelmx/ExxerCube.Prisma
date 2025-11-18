# ADR-001: CSnakes vs Python.NET for Python-C# Interoperability

## Status
**APPROVED** - CSnakes is the preferred solution for Python-C# interoperability

## Context
The project requires seamless integration between C# and Python modules for OCR processing. Two main options were considered:
1. **CSnakes.Runtime** - Type-safe, C#-generated code approach
2. **Python.NET (pythonnet)** - Direct Python runtime integration

## Decision
**Use CSnakes.Runtime for all Python-C# interoperability.**

## Rationale

### **CSnakes Advantages**
- ✅ **Type Safety**: Generated C# code provides compile-time type checking
- ✅ **Transparency**: C#-generated code makes integration visible and debuggable
- ✅ **Clean Architecture**: Better separation of concerns and interface isolation
- ✅ **Error Handling**: More structured error management with C# exceptions
- ✅ **Performance**: Optimized for C# integration with reduced marshalling overhead
- ✅ **Maintainability**: Easier to maintain and refactor with strongly-typed interfaces

### **Python.NET Disadvantages**
- ❌ **Type Safety Issues**: Heavy use of `dynamic` objects leads to runtime errors
- ❌ **Architecture Violations**: `Py.GIL()` calls leak into infrastructure layer
- ❌ **Complexity**: Manual GIL management and resource disposal
- ❌ **Debugging Difficulty**: Runtime errors instead of compile-time validation
- ❌ **Boilerplate Code**: Extensive manual conversion between C# and Python types

## Consequences

### **Positive Consequences**
- **Clean Architecture**: Proper interface isolation and separation of concerns
- **Type Safety**: Compile-time validation reduces runtime errors
- **Maintainability**: Easier to understand and modify the codebase
- **Performance**: Better performance through optimized C# integration
- **Developer Experience**: Better IntelliSense and debugging capabilities

### **Negative Consequences**
- **Migration Effort**: Requires refactoring existing Python.NET implementation
- **Learning Curve**: Team needs to learn CSnakes patterns
- **Dependency**: Additional dependency on CSnakes.Runtime

## Implementation Plan

### **Phase 1: Research and Validation**
- [ ] Research CSnakes capabilities and limitations
- [ ] Validate CSnakes compatibility with existing Python modules
- [ ] Create proof-of-concept implementation
- [ ] Benchmark performance comparison

### **Phase 2: Architecture Refactoring**
- [ ] Create abstract interfaces for Python interop
- [ ] Remove Python.NET dependencies from infrastructure layer
- [ ] Implement CSnakes-based adapters
- [ ] Update dependency injection configuration

### **Phase 3: Integration and Testing**
- [ ] Integrate CSnakes with existing Python modules
- [ ] Update all integration tests
- [ ] Performance testing and optimization
- **Phase 4: Documentation and Cleanup**
- [ ] Update documentation with CSnakes examples
- [ ] Remove Python.NET references
- [ ] Code review and final validation

## Architectural Violations to Fix

### **Current Issues**
1. **Interface Leakage**: `Py.GIL()` calls directly in infrastructure adapters
2. **Type Safety**: Extensive use of `dynamic` objects throughout the codebase
3. **Resource Management**: Manual GIL management scattered across adapters
4. **Error Handling**: Inconsistent error handling patterns

### **Required Changes**
1. **Abstract Python Interop**: Create interfaces that hide Python implementation details
2. **Type-Safe Integration**: Replace `dynamic` objects with strongly-typed CSnakes code
3. **Resource Management**: Centralize Python resource management
4. **Error Handling**: Implement consistent Railway Oriented Programming patterns

## Risk Mitigation

### **High Risks**
1. **Breaking Changes**: Refactoring may break existing functionality
2. **Performance Regression**: CSnakes may have different performance characteristics
3. **Compatibility Issues**: CSnakes may not work with all Python modules

### **Mitigation Strategies**
1. **Incremental Refactoring**: Refactor in small, testable increments
2. **Comprehensive Testing**: Maintain extensive test coverage during refactoring
3. **Performance Benchmarking**: Measure performance before and after changes
4. **Rollback Plan**: Maintain ability to rollback to working Python.NET implementation

## References

- [CSnakes Documentation](https://github.com/csnakes/csnakes)
- [Python.NET Documentation](https://github.com/pythonnet/pythonnet)
- [Hexagonal Architecture Principles](https://alistair.cockburn.us/hexagonal-architecture/)
- [Railway Oriented Programming](https://fsharpforfunandprofit.com/rop/)

## Approval

**Approved by**: System Architect  
**Date**: [Current Date]  
**Review Date**: [Date + 6 months]

---

**Note**: This ADR supersedes any previous decisions regarding Python-C# interoperability and establishes CSnakes as the standard approach for this project.

