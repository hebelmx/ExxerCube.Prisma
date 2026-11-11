# Architecture Decision Records (ADRs)

This directory contains Architecture Decision Records documenting key architectural decisions, violations discovered, and remediation approaches for the ExxerCube.Prisma project.

---

## ADR Index

### Test Architecture

- **[ADR-002: Test Project Split - Clean Architecture Violations](./adr-002-test-project-split-clean-architecture-violations.md)**
  - Documents 9 clean architecture violations discovered during test suite split
  - Provides remediation plan and effort estimates
  - **Status:** ✅ Implemented (violations identified, remediation in progress)

- **[ADR-002 Remediation Guide](./adr-002-remediation-guide.md)**
  - **Detailed step-by-step instructions** for fixing all 9 test violations
  - Includes code examples, mock configurations, and validation steps
  - **Status:** 📋 Ready for Implementation

- **[ADR-003: Test Suite Split Decision](./adr-003-test-suite-split-decision.md)**
  - Documents the decision to split monolithic test project into 10 layered projects
  - Explains rationale, structure, and dependency rules
  - **Status:** ✅ Implemented

### Data Access Architecture

- **[ADR-004: EF Core Application Layer Violation](./adr-004-efcore-application-layer-violation.md)**
  - Documents EF Core dependency violation in Application layer
  - Describes Generic Repository Pattern + Specification Pattern solution
  - Explains expression tree-based query abstraction
  - **Status:** ✅ Implemented

### Guidelines and Standards

- **[Architecture Violation Guidelines](./architecture-violation-guidelines.md)**
  - Comprehensive guide for detecting and preventing clean architecture violations
  - Common violation patterns with examples
  - Automated detection commands
  - Code review checklist
  - **Status:** 📋 Active Guidelines

---

## Quick Reference

### Violation Detection Commands

```bash
# Check Application for Infrastructure references
dotnet list Prisma/Code/Src/CSharp/Application/*.csproj reference | grep Infrastructure

# Check Application for EF Core package
dotnet list Prisma/Code/Src/CSharp/Application/*.csproj package | grep EntityFrameworkCore

# Check for Infrastructure namespaces in Application code
grep -r "using.*Infrastructure" Prisma/Code/Src/CSharp/Application/

# Check for Infrastructure type instantiation in Application
grep -r "new.*Infrastructure\." Prisma/Code/Src/CSharp/Application/

# Check Domain for external packages
dotnet list Prisma/Code/Src/CSharp/Domain/*.csproj package

# Check Infrastructure for cross-dependencies
dotnet list Prisma/Code/Src/CSharp/Infrastructure.*/*.csproj reference | grep Infrastructure
```

### Architecture Principles

1. **Dependency Rule:** Dependencies point inward toward Domain
2. **Domain Independence:** Domain has NO external dependencies
3. **Application Abstraction:** Application depends only on Domain interfaces
4. **Infrastructure Isolation:** Infrastructure implements Domain contracts
5. **Test Boundaries:** Tests follow same architecture boundaries

---

## Related Documentation

- [Clean Architecture Patterns](../../.cursor/rules/1008_CleanArchitecturePatterns.mdc)
- [Domain-Driven Design Patterns](../../.cursor/rules/1007_DomainDrivenDesignPatterns.mdc)
- [C# Coding Standards](../../.cursor/rules/1001_CSharpCodingStandards.mdc)
- [Dependency Architecture Diagram](./dependency-architecture-diagram.md)

---

**Last Updated:** 2025-01-15

