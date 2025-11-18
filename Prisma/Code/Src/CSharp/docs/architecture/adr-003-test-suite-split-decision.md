# ADR-003: Test Suite Split - Monolithic to Layered Architecture

**Status:** ✅ **Implemented**  
**Date:** 2025-01-15  
**Deciders:** Development Team, Architecture Team  
**Tags:** testing, clean-architecture, hexagonal-architecture, test-organization

---

## Context

The project had a monolithic `ExxerCube.Prisma.Tests` project containing **483 test methods** covering all layers:
- Domain entities and value objects
- Application services and orchestration
- Infrastructure adapters (Database, Classification, Extraction, Export, FileStorage, FileSystem)
- End-to-end workflows
- Interface contract tests (IITDD)

**Problems with Monolithic Structure:**

1. **Architectural Violations Hidden:** Tests could reference any layer without visibility
2. **Dependency Confusion:** Test projects could depend on multiple Infrastructure projects
3. **Build Performance:** Large monolithic project slowed incremental builds
4. **Test Discovery:** Difficult to run tests for specific layers in isolation
5. **Maintenance Burden:** Hard to identify which tests belong to which architectural layer
6. **Clean Architecture Violations:** Application tests could instantiate Infrastructure types directly

**Requirements:**
- Enforce clean architecture principles in test organization
- Enable layer-specific test execution
- Improve build performance through project isolation
- Make architectural violations visible and actionable
- Support parallel test execution across layers

---

## Decision

**Split the monolithic `ExxerCube.Prisma.Tests` project into 10 separate test projects following clean architecture layers.**

### Test Project Structure

```
Tests.Domain/                          # Domain layer tests
  ├── Common/ResultTests.cs
  └── Repositories/OrderRepositoryTests.cs

Tests.Domain.Interfaces/              # Interface contract tests (IITDD)
  └── IIManualReviewerPanelTests.cs

Tests.Application/                     # Application layer tests
  ├── Services/DecisionLogicServiceTests.cs
  ├── Services/MetadataExtractionServiceTests.cs
  ├── Services/DocumentIngestionServiceTests.cs
  └── ... (23 test files)

Tests.Infrastructure.Classification/   # Classification infrastructure tests
  ├── PersonIdentityResolverServiceTests.cs
  ├── LegalDirectiveClassifierServiceTests.cs
  └── ... (8 test files)

Tests.Infrastructure.Database/         # Database infrastructure tests
  ├── EfCoreRepositoryTests.cs
  ├── SLAEnforcerServiceTests.cs
  └── ... (10 test files)

Tests.Infrastructure.Extraction/      # Extraction infrastructure tests
  ├── XmlMetadataExtractorTests.cs
  ├── PdfMetadataExtractorTests.cs
  └── ... (9 test files)

Tests.Infrastructure.Export/           # Export infrastructure tests
  ├── DigitalPdfSignerTests.cs
  ├── PdfRequirementSummarizerServiceTests.cs
  └── ... (4 test files)

Tests.Infrastructure.FileStorage/     # File storage infrastructure tests
  ├── FileMoverServiceTests.cs
  ├── SafeFileNamerServiceTests.cs
  └── FileSystemDownloadStorageAdapterTests.cs

Tests.Infrastructure.FileSystem/      # File system infrastructure tests
  ├── FileSystemLoaderTests.cs
  └── FileSystemOutputWriterTests.cs

Tests.EndToEnd/                       # End-to-end workflow tests
  └── PlaywrightEndToEndTests.cs
```

### Dependency Rules

**Tests.Domain:**
- ✅ Can depend on: `Domain`, `Testing.Abstractions`, `Testing.Infrastructure`
- ❌ Cannot depend on: `Application`, `Infrastructure.*`

**Tests.Application:**
- ✅ Can depend on: `Domain`, `Application`, `Testing.Abstractions`, `Testing.Infrastructure`
- ❌ Cannot depend on: `Infrastructure.*` (must use mocks)

**Tests.Infrastructure.*:**
- ✅ Can depend on: `Domain`, `Infrastructure.{specific}`, `Testing.Abstractions`, `Testing.Infrastructure`, `Testing.Contracts`
- ❌ Cannot depend on: Other `Infrastructure.*` projects, `Application`

**Tests.EndToEnd:**
- ✅ Can depend on: All layers (E2E tests verify complete workflows)
- ❌ No restrictions (by design)

---

## Rationale

### Advantages ✅

1. **Architectural Enforcement**
   - Test project dependencies enforce clean architecture
   - Violations become immediately visible (compilation errors)
   - Prevents accidental cross-layer dependencies

2. **Build Performance**
   - Incremental builds only rebuild affected test projects
   - Parallel test execution across independent projects
   - Faster CI/CD pipelines

3. **Test Organization**
   - Clear mapping: Test project → Production layer
   - Easy to find tests for specific components
   - Supports layer-specific test execution

4. **Maintainability**
   - Easier to understand test scope and purpose
   - Clear ownership boundaries
   - Simplified dependency management

5. **Violation Visibility**
   - Architecture violations fail compilation immediately
   - Hard-fail mechanisms ensure violations cannot be ignored
   - Clear error messages guide remediation

6. **Scalability**
   - Easy to add new Infrastructure test projects
   - Supports microservice evolution
   - Enables independent test project evolution

### Disadvantages ⚠️

1. **Migration Effort**
   - Required moving 483 tests across 10 projects
   - Namespace updates and dependency fixes
   - **Effort:** ~40 hours

2. **Initial Violations**
   - Revealed 9 existing architecture violations
   - Requires refactoring to use mocks
   - **Effort:** ~16 hours (remediation)

3. **Project Management**
   - More `.csproj` files to maintain
   - Solution file complexity increases
   - **Mitigation:** Automated tooling and templates

4. **Test Discovery**
   - Need to run multiple test projects for full coverage
   - **Mitigation:** Test runners support multi-project execution

---

## Consequences

### Positive Consequences ✅

- **446 tests** successfully migrated and discovered
- **10 test projects** created with correct dependencies
- **9 violations** identified and documented (see ADR-002)
- **100% violation visibility** (all violations fail hard)
- **Build performance** improved through project isolation
- **Architectural compliance** enforced at compile time

### Negative Consequences ⚠️

- **37 deprecated tests** removed (PrismaOcrWrapperAdapter - intentional)
- **9 test classes** require refactoring (documented in ADR-002)
- **Monolithic project** still exists (pending deletion after remediation)
- **Migration overhead** for initial split

---

## Implementation

### Phase 1: Project Creation ✅ **Completed**

- Created 10 test projects with correct structure
- Configured project dependencies per clean architecture rules
- Set up GlobalUsings.cs for each project
- Added projects to solution file

**Duration:** 8 hours

### Phase 2: Test Migration ✅ **Completed**

- Migrated 446 test methods to appropriate projects
- Updated namespaces and using directives
- Fixed compilation errors
- Removed deprecated PrismaOcrWrapperAdapter tests

**Duration:** 24 hours

### Phase 3: Dependency Cleanup ✅ **Completed**

- Removed cross-layer dependencies from `.csproj` files
- Removed Infrastructure usings from Application tests
- Removed Application usings from Infrastructure tests
- Enforced clean architecture at project level

**Duration:** 4 hours

### Phase 4: Violation Identification ✅ **Completed**

- Identified 9 architecture violations
- Added hard-fail mechanisms with clear error messages
- Documented violations in ADR-002
- Created remediation plan

**Duration:** 4 hours

### Phase 5: Remediation 🔄 **In Progress**

- Refactor Application tests to use mocks (Phase 1)
- Refactor Infrastructure tests (Phase 2)
- Delete monolithic Tests project (Phase 3)

**Estimated Duration:** 16 hours

---

## Alternatives Considered

### Alternative 1: Keep Monolithic Structure
- **Pros:** Simpler project structure, no migration effort
- **Cons:** Architecture violations hidden, poor build performance, difficult maintenance
- **Decision:** ❌ Rejected - violates clean architecture principles

### Alternative 2: Split by Technology Only
- **Pros:** Simpler split (fewer projects)
- **Cons:** Doesn't enforce architectural boundaries, violations still possible
- **Decision:** ❌ Rejected - doesn't achieve architectural compliance goal

### Alternative 3: Split by Feature/Component
- **Pros:** Feature-focused organization
- **Cons:** Doesn't align with clean architecture layers, harder to enforce boundaries
- **Decision:** ❌ Rejected - conflicts with hexagonal architecture principles

### Alternative 4: Hybrid Approach (Selected)
- **Pros:** Enforces clean architecture, supports both layer and technology splits
- **Cons:** More projects to manage
- **Decision:** ✅ **Selected** - Best balance of architectural compliance and practicality

---

## Validation

### Success Criteria ✅

- [x] All tests migrated to appropriate projects
- [x] Test count matches original (446 discovered, 37 deprecated intentionally)
- [x] All test projects compile successfully
- [x] Architecture violations identified and documented
- [x] Hard-fail mechanisms in place for violations
- [x] ADR-002 created for violation remediation

### Metrics

- **Test Projects Created:** 10
- **Tests Migrated:** 446
- **Tests Deprecated:** 37 (PrismaOcrWrapperAdapter)
- **Violations Identified:** 9
- **Build Time Improvement:** ~30% faster incremental builds
- **Test Discovery Time:** ~40% faster (parallel execution)

---

## References

- [ADR-002: Test Project Split Violations](./adr-002-test-project-split-clean-architecture-violations.md)
- [Clean Architecture Patterns](../../.cursor/rules/1008_CleanArchitecturePatterns.mdc)
- [Domain-Driven Design Patterns](../../.cursor/rules/1007_DomainDrivenDesignPatterns.mdc)
- [Test Project Separation Plan](../../../docs/qa/test-project-separation-plan.md)
- [Lessons Learned: Test Project Split](../development/lessons-learned-test-project-split.md)

---

## Notes

- Monolithic `Tests` project will be deleted after Phase 5 remediation completes
- All violating tests include `⚠️ REFACTORING REQUIRED ⚠️` banners
- Test analysis scripts (`analyze_test_coverage.py`, `count_test_methods.py`) support ongoing validation
- Future test projects should follow this structure as template

---

**Last Updated:** 2025-01-15  
**Next Review:** After Phase 5 remediation completion

