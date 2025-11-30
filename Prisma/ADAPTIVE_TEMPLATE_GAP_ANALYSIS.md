# Adaptive Bank Template Detection - Gap Analysis & Implementation Roadmap
**Date**: 2025-11-30
**Status**: ⚠️ **GAP IDENTIFIED** - Claimed but Not Implemented
**Priority**: HIGH - Critical for "No Code Changes" Promise

---

## 🎯 Executive Summary

The `SYSTEM_FLOW_DIAGRAM.md` claims the system has **"Bank Template Adapter (Auto-Detecting)"** with:
- ✅ Template schema detection
- ✅ Dynamic mapping
- ✅ No code changes needed

**Reality Check**: ❌ **NONE of this is implemented**. All export templates are hardcoded.

---

## ❌ Current State (What We Have)

### Export System Architecture
```
UnifiedMetadataRecord
    ↓
ExportService (Application Layer)
    ↓
├── SiroXmlExporter (HARDCODED XML structure)
├── ExcelLayoutGenerator (HARDCODED Excel columns)
└── CriterionMapperService (HARDCODED dictionary mapping)
```

### 1. SiroXmlExporter (`Infrastructure.Export/SiroXmlExporter.cs:167-288`)
**Problem**: XML structure is **completely hardcoded**

```csharp
private string GenerateSiroXml(UnifiedMetadataRecord metadata)
{
    // Lines 184-194: Hardcoded XML element names
    xmlWriter.WriteElementString("NumeroExpediente", expediente.NumeroExpediente);
    xmlWriter.WriteElementString("NumeroOficio", expediente.NumeroOficio);
    xmlWriter.WriteElementString("SolicitudSiara", expediente.SolicitudSiara);
    xmlWriter.WriteElementString("Folio", expediente.Folio.ToString());
    xmlWriter.WriteElementString("OficioYear", expediente.OficioYear.ToString());
    // ... 100+ more lines of hardcoded XML generation
}
```

**Impact if Bank changes XML schema:**
- ✏️ Edit `SiroXmlExporter.cs` (100+ lines)
- 🔨 Recompile entire application
- 🧪 Re-run all export tests
- 🚀 Redeploy to production
- ⏱️ Estimated: **2-4 hours of developer time per change**

### 2. ExcelLayoutGenerator (`Infrastructure.Export/ExcelLayoutGenerator.cs:79-113`)
**Problem**: Excel column layout is **completely hardcoded**

```csharp
// Lines 79-91: Hardcoded column headers
worksheet.Cell(1, 1).Value = "NumeroExpediente";
worksheet.Cell(1, 2).Value = "NumeroOficio";
worksheet.Cell(1, 3).Value = "SolicitudSiara";
worksheet.Cell(1, 4).Value = "Folio";
// ... hardcoded mapping to row 2
```

**Impact if Bank changes Excel template:**
- ✏️ Edit `ExcelLayoutGenerator.cs` (column definitions)
- 🔨 Recompile entire application
- 🧪 Re-run all layout tests
- 🚀 Redeploy to production
- ⏱️ Estimated: **1-2 hours of developer time per change**

### 3. CriterionMapperService (`Infrastructure.Export/CriterionMapperService.cs:54-67`)
**Problem**: Field mapping is **hardcoded dictionary keys**

```csharp
var criterionValue = new Dictionary<string, object>
{
    { "RequerimientoId", requirement.RequerimientoId },
    { "Descripcion", requirement.Descripcion },
    { "Tipo", requirement.Tipo },
    { "EsObligatorio", requirement.EsObligatorio }
};
```

### 4. No Configuration Infrastructure
**Missing:**
- ❌ No template schema files (`.json`, `.yaml`, `.xml`)
- ❌ No template versioning mechanism
- ❌ No schema detection logic
- ❌ No dynamic field mapper
- ❌ No template validation
- ❌ No fallback/migration strategy

---

## ✅ Claimed Capabilities (From SYSTEM_FLOW_DIAGRAM.md)

From lines 133-143 of `SYSTEM_FLOW_DIAGRAM.md`:

```markdown
🔧 Adaptive Capabilities (No Code Changes Needed)
├── AdaptSchema["📐 XML Schema Changes → Auto-detection"]
├── AdaptTemplate["📄 Bank Template Changes → Auto-detection"]
├── AdaptQuality["📊 PDF Quality Changes → Filter adaptation"]
└── AdaptFormat["📑 PDF Format Changes → Robust parsing"]
```

**Specific Claims:**
1. **XML Schema Changes** → Automatic detection & adaptation
2. **Bank Template Changes** → Automatic detection & mapping
3. **No Code Changes Needed** → System adapts without recompilation

---

## 🔍 The Gap Analysis

| Capability | Claimed | Actual | Gap Severity |
|------------|---------|--------|--------------|
| XML Schema Auto-Detection | ✅ Yes | ❌ No | 🔴 CRITICAL |
| Bank Template Auto-Detection | ✅ Yes | ❌ No | 🔴 CRITICAL |
| Dynamic Field Mapping | ✅ Yes | ❌ No | 🔴 CRITICAL |
| Template Versioning | ✅ Implied | ❌ No | 🟡 HIGH |
| Schema Validation | ✅ Partial | 🟡 Partial (only if schema provided) | 🟡 MEDIUM |
| No Code Changes Needed | ✅ Yes | ❌ No (requires code changes) | 🔴 CRITICAL |

### Real-World Scenario: Bank Changes Excel Template

**Current Process (Hardcoded):**
```
1. Bank sends new Excel template specification
2. Developer opens ExcelLayoutGenerator.cs
3. Developer manually edits lines 79-113 (column headers + mappings)
4. Developer runs dotnet build
5. Developer runs tests
6. Developer creates PR
7. PR reviewed and merged
8. CI/CD pipeline builds and deploys
⏱️ TOTAL TIME: 4-6 hours (with PR review)
```

**Desired Process (Adaptive):**
```
1. Bank sends new Excel template specification
2. System administrator uploads new template.json file
3. System detects new template version
4. System validates template schema
5. System automatically uses new template for next export
⏱️ TOTAL TIME: 5 minutes (no developer involvement)
```

---

## 🎯 What Adaptive Template Detection Should Do

### Core Requirements

#### 1. Template Schema Definition (Configuration)
Store templates as **external configuration** (not code):

```json
// ExcelTemplate_v1.0.json
{
  "templateVersion": "1.0",
  "templateType": "Excel",
  "effectiveDate": "2025-01-15",
  "columns": [
    {
      "index": 1,
      "header": "NumeroExpediente",
      "sourceField": "Expediente.NumeroExpediente",
      "required": true,
      "dataType": "string"
    },
    {
      "index": 2,
      "header": "NumeroOficio",
      "sourceField": "Expediente.NumeroOficio",
      "required": true,
      "dataType": "string"
    }
    // ... configurable columns
  ]
}
```

#### 2. XML Schema Detection & Adaptation
```json
// SiroXmlTemplate_v2.5.json
{
  "templateVersion": "2.5",
  "templateType": "XML",
  "namespace": "http://siro.regulatory.namespace",
  "rootElement": "SiroResponse",
  "elements": [
    {
      "name": "NumeroExpediente",
      "sourceField": "Expediente.NumeroExpediente",
      "required": true,
      "xpath": "/SiroResponse/NumeroExpediente"
    }
    // ... configurable XML structure
  ]
}
```

#### 3. Dynamic Field Mapper (Runtime)
Replace hardcoded mappings with **reflection-based mapper**:

```csharp
public interface ITemplateFieldMapper
{
    // Dynamically map UnifiedMetadataRecord to template structure
    Task<Dictionary<string, object?>> MapFieldsAsync(
        UnifiedMetadataRecord source,
        TemplateDefinition template,
        CancellationToken cancellationToken = default);

    // Validate that source data satisfies template requirements
    Task<ValidationResult> ValidateAsync(
        UnifiedMetadataRecord source,
        TemplateDefinition template,
        CancellationToken cancellationToken = default);
}
```

#### 4. Template Versioning & Hot-Reload
```csharp
public interface ITemplateRepository
{
    // Load template by version
    Task<TemplateDefinition?> GetTemplateAsync(
        string templateType,
        string version,
        CancellationToken cancellationToken = default);

    // Get latest active template
    Task<TemplateDefinition?> GetLatestTemplateAsync(
        string templateType,
        CancellationToken cancellationToken = default);

    // Watch for template changes and reload
    IObservable<TemplateChangeEvent> WatchForChanges();
}
```

#### 5. Schema Evolution Detection
Detect when CNBV/Bank changes their schema:

```csharp
public interface ISchemaEvolutionDetector
{
    // Compare incoming XML/Excel against known templates
    Task<TemplateMatchResult> DetectBestMatchAsync(
        Stream documentStream,
        string documentType,
        CancellationToken cancellationToken = default);

    // Detect schema drift (new fields, missing fields, renamed fields)
    Task<SchemaDriftReport> AnalyzeDriftAsync(
        TemplateDefinition currentTemplate,
        TemplateDefinition newTemplate,
        CancellationToken cancellationToken = default);
}
```

---

## 🏗️ Proposed Architecture

### Clean Architecture Layers

```
┌─────────────────────────────────────────────────────────┐
│ 01-Core/Domain                                          │
├─────────────────────────────────────────────────────────┤
│ ├── Entities/                                           │
│ │   └── TemplateDefinition.cs                          │
│ ├── ValueObjects/                                       │
│ │   ├── TemplateVersion.cs                             │
│ │   ├── FieldMapping.cs                                │
│ │   └── TemplateValidationResult.cs                    │
│ └── Interfaces/                                         │
│     ├── ITemplateFieldMapper.cs                        │
│     ├── ITemplateRepository.cs                         │
│     ├── ISchemaEvolutionDetector.cs                    │
│     └── IAdaptiveExporter.cs                           │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ 01-Core/Application                                     │
├─────────────────────────────────────────────────────────┤
│ └── Services/                                           │
│     ├── AdaptiveExportService.cs (NEW)                 │
│     └── TemplateValidationService.cs (NEW)             │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ 02-Infrastructure/Infrastructure.Export.Adaptive (NEW)  │
├─────────────────────────────────────────────────────────┤
│ ├── TemplateFieldMapper.cs                             │
│ ├── JsonTemplateRepository.cs                          │
│ ├── SchemaEvolutionDetector.cs                         │
│ ├── AdaptiveExcelExporter.cs                           │
│ ├── AdaptiveXmlExporter.cs                             │
│ └── DependencyInjection/                               │
│     └── ServiceCollectionExtensions.cs                 │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ Configuration (External)                                │
├─────────────────────────────────────────────────────────┤
│ └── Templates/                                          │
│     ├── Excel/                                          │
│     │   ├── ExcelTemplate_v1.0.json                    │
│     │   └── ExcelTemplate_v1.1.json                    │
│     └── Xml/                                            │
│         ├── SiroXmlTemplate_v2.5.json                  │
│         └── SiroXmlTemplate_v2.6.json                  │
└─────────────────────────────────────────────────────────┘
```

---

## 🧪 ITDD Methodology (Interface-Test-Driven Development)

This project follows **strict ITDD** with Liskov Substitution Principle verification, as proven in the Adaptive DOCX refactoring.

### 📚 Liskov Substitution Principle (Barbara Liskov, 1987)

**Formal Definition:**
> "If S is a subtype of T, then objects of type T may be replaced with objects of type S without altering any of the desirable properties of the program."

**In Our Context:**
- If `JsonTemplateRepository` implements `ITemplateRepository`
- Then **ANYWHERE** you use `ITemplateRepository`, you can substitute `JsonTemplateRepository`
- Without breaking **ANY** behavioral contracts

**What Liskov Really Means:**
- Same inputs → Same outputs (observable behavior)
- Same preconditions → Same postconditions
- Same exceptions → Same error behavior
- Same side effects → Same state changes
- **The implementation MUST honor ALL promises made by the interface**

### ✅ ITDD Workflow (Step-by-Step)

#### **Step 1: Domain Layer**
Create interfaces + domain entities (NO implementations)
- Define `ITemplateRepository` interface
- Define `TemplateDefinition` entity
- Define `TemplateVersion` value object
- **Zero implementation code**

#### **Step 2: Interface Contract Tests (ITDD - Think BEHAVIOR)**
Write behavioral tests using **MOCKED interfaces**
- Test the **abstraction**, NOT implementation details
- Think about **RESULTS**, not **HOW**
- Forces you to design the interface contract properly
- Uses `Mock<ITemplateRepository>` with `.Setup()` and `.ReturnsAsync()`

**Example:**
```csharp
// File: Tests.Domain/Contracts/ITemplateRepositoryContractTests.cs
public class ITemplateRepositoryContractTests
{
    [Fact]
    public async Task GetTemplateAsync_WhenTemplateExists_ReturnsTemplateDefinition()
    {
        // Arrange: Mock the interface (thinking about BEHAVIOR)
        var mockRepo = new Mock<ITemplateRepository>();
        var expectedTemplate = new TemplateDefinition
        {
            TemplateType = "Excel",
            Version = "1.0.0"
        };

        mockRepo.Setup(x => x.GetTemplateAsync("Excel", "1.0.0", default))
                .ReturnsAsync(expectedTemplate);

        // Act: Use the mocked abstraction
        var result = await mockRepo.Object.GetTemplateAsync("Excel", "1.0.0", default);

        // Assert: Verify expected BEHAVIOR
        Assert.NotNull(result);
        Assert.Equal("Excel", result.TemplateType);
        Assert.Equal("1.0.0", result.Version);
    }

    [Fact]
    public async Task GetTemplateAsync_WhenTemplateNotFound_ReturnsNull()
    {
        // Arrange: Mock returns null (thinking about RESULTS)
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(x => x.GetTemplateAsync("Invalid", "9.9.9", default))
                .ReturnsAsync((TemplateDefinition?)null);

        // Act
        var result = await mockRepo.Object.GetTemplateAsync("Invalid", "9.9.9", default);

        // Assert
        Assert.Null(result);
    }
}
```

#### **Step 2.5: Make Interface Tests GREEN** ⬅️ **KEY INSIGHT**
All tests pass because interfaces are **MOCKED**
- Tests verify the **BEHAVIOR** you expect from the abstraction
- This proves the interface contract is **sound and complete**
- Forces you to think **RESULTS FIRST**, **HOW SECOND**
- **Status**: ✅ **GREEN** (mocks always return what you tell them)

#### **Step 3.0: Create Implementation Tests (TDD RED Phase)**
Create **IDENTICAL** test class for the implementation
- **Same test names** (contracts are identical)
- **Same scenarios** (same inputs)
- **Same expectations** (same outputs)
- But using **REAL implementation** (no mocks)
- **Status**: 🔴 **RED** (no implementation yet)

**Example:**
```csharp
// File: Tests.Infrastructure.Export.Adaptive/JsonTemplateRepositoryTests.cs
public class JsonTemplateRepositoryTests
{
    // IDENTICAL test name = IDENTICAL contract
    [Fact]
    public async Task GetTemplateAsync_WhenTemplateExists_ReturnsTemplateDefinition()
    {
        // Arrange: REAL implementation (no mocks)
        var dbContext = CreateInMemoryDbContext();
        var repo = new JsonTemplateRepository(dbContext, logger);

        // Setup: Create actual template in database
        await dbContext.Templates.AddAsync(new TemplateEntity
        {
            TemplateType = "Excel",
            Version = "1.0.0",
            // ... real data
        });
        await dbContext.SaveChangesAsync();

        // Act: Call REAL method
        var result = await repo.GetTemplateAsync("Excel", "1.0.0", default);

        // Assert: SAME expectations as interface test (Liskov!)
        Assert.NotNull(result);
        Assert.Equal("Excel", result.TemplateType);
        Assert.Equal("1.0.0", result.Version);
    }

    // IDENTICAL test name = IDENTICAL contract
    [Fact]
    public async Task GetTemplateAsync_WhenTemplateNotFound_ReturnsNull()
    {
        // Arrange: REAL implementation with empty database
        var dbContext = CreateInMemoryDbContext();
        var repo = new JsonTemplateRepository(dbContext, logger);

        // Act: Query non-existent template
        var result = await repo.GetTemplateAsync("Invalid", "9.9.9", default);

        // Assert: SAME expectation (Liskov!)
        Assert.Null(result);
    }
}
```

**Status**: 🔴 **RED** (no implementation exists yet - true TDD red phase)

#### **Step 3.5: Implement to Make Tests GREEN**
Write the actual implementation to satisfy the contract
- Implement `JsonTemplateRepository.GetTemplateAsync()`
- Make all implementation tests pass
- **Status**: ✅ **GREEN**

#### **Step 4: Liskov Verification PASSED**
Implementation tests pass → **Liskov Substitution Principle satisfied**
- `JsonTemplateRepository` is a valid substitute for `ITemplateRepository`
- **Same test names** prove behavioral equivalence
- **Same assertions** prove contract fulfillment

### 🎯 Why This ITDD Approach Works

1. **Interface tests** define the **behavioral contract** (abstraction thinking - RESULTS)
2. **Implementation tests** verify **Liskov substitution** (concrete thinking - HOW)
3. **Same test names** = Same contracts = Liskov proof
4. **Forces you to think RESULTS first, IMPLEMENTATION second**
5. **Green interface tests** prove your abstraction is well-designed
6. **Green implementation tests** prove your concrete class honors the contract

### 📏 ITDD Architecture Rules (Enforced)

- ✅ All interfaces → `Domain/Interfaces`
- ✅ All entities/value objects → `Domain/Entities` or `Domain/ValueObjects`
- ✅ ITDD: Contract tests FIRST (mocked, behavioral, Liskov)
- ✅ Tests GREEN before ANY implementation
- ✅ Each implementation → Own project (`Infrastructure.Export.Adaptive`)
- ✅ Liskov verification: Implementation passes same interface tests (identical names)
- ✅ NO infrastructure-to-infrastructure dependencies
- ✅ CPM package management
- ✅ System tests only for cross-concerns with live objects
- ✅ **TDD First Principles**: RED → GREEN → REFACTOR

### 🧪 TDD First Principles (Enforced)

- Start **RED**: Add a clear, behavior-driven failing test before any production code
- Make it **GREEN** with the simplest implementation; no speculative code
- Keep cycles **short and incremental**: red → green → refactor
- Use **expressive test names** (e.g., `Method_Scenario_Expectation` or Given/When/Then)
- Maintain tests as **first-class code**: clear AAA flow, explicit assertions, minimal hidden helpers
- Favor **fast, deterministic, isolated** tests; seed randomness and avoid external state
- **Don't mask failures**: Fix code or correct a bad test explicitly, don't weaken checks
- Let tests **drive design**; refactor only with green tests and explain design changes briefly
- Be **explicit** with fixtures/seeds and generated code; document intent when auto-creating tests
- Run **focused subsets** during iteration; reserve full suites for validation once changes are stable

### 📊 Test Organization Pattern

```
04-Tests/
├── 01-Core/
│   └── Tests.Domain/
│       └── Contracts/                         # Interface contract tests (GREEN with mocks)
│           ├── ITemplateRepositoryContractTests.cs
│           ├── ITemplateFieldMapperContractTests.cs
│           └── IAdaptiveExporterContractTests.cs
│
├── 02-Infrastructure/
│   └── Tests.Infrastructure.Export.Adaptive/  # Implementation tests (RED → GREEN)
│       ├── JsonTemplateRepositoryTests.cs
│       ├── TemplateFieldMapperTests.cs
│       └── AdaptiveXmlExporterTests.cs
│
└── 03-System/
    └── Tests.System/                          # E2E tests (live objects, no mocks)
        └── AdaptiveTemplateE2ETests.cs
```

---

## 📋 Implementation Roadmap

### Phase 1: Foundation (Week 1-2)
**Goal**: Define domain models and interfaces

- [ ] Create `TemplateDefinition` entity
- [ ] Create `FieldMapping` value object
- [ ] Create `TemplateVersion` value object
- [ ] Define `ITemplateFieldMapper` interface
- [ ] Define `ITemplateRepository` interface
- [ ] Define `IAdaptiveExporter` interface
- [ ] Create ITDD contract tests for all interfaces

**Deliverable**: Interfaces + contract tests (0 implementations)

### Phase 2: Template Repository (Week 3)
**Goal**: Load templates from JSON files

- [ ] Implement `JsonTemplateRepository`
- [ ] Create Excel template JSON schema
- [ ] Create XML template JSON schema
- [ ] Implement template validation (JSON schema validation)
- [ ] Implement template versioning logic
- [ ] Write integration tests with sample templates

**Deliverable**: Template loading from external config files

### Phase 3: Dynamic Field Mapper (Week 4-5)
**Goal**: Runtime field mapping using reflection

- [ ] Implement `TemplateFieldMapper`
- [ ] Support nested field paths (e.g., `Expediente.NumeroExpediente`)
- [ ] Support collection navigation (e.g., `Personas[0].Nombre`)
- [ ] Support computed fields (e.g., date formatting)
- [ ] Handle nullable fields gracefully
- [ ] Write comprehensive mapping tests

**Deliverable**: Dynamic mapping from `UnifiedMetadataRecord` to any template

### Phase 4: Adaptive Exporters (Week 6-7)
**Goal**: Replace hardcoded exporters with adaptive versions

- [ ] Implement `AdaptiveExcelExporter`
  - Use `ITemplateRepository` to load template
  - Use `ITemplateFieldMapper` to map fields
  - Generate Excel dynamically from template
- [ ] Implement `AdaptiveXmlExporter`
  - Use `ITemplateRepository` to load template
  - Use `ITemplateFieldMapper` to map fields
  - Generate XML dynamically from template
- [ ] Create adapter pattern for backward compatibility
  - Old: `IResponseExporter` → `SiroXmlExporter` (hardcoded)
  - New: `IResponseExporter` → `AdaptiveExporterAdapter` → `AdaptiveXmlExporter`
- [ ] Write E2E tests with multiple template versions

**Deliverable**: Adaptive exporters with zero-downtime migration path

### Phase 5: Schema Evolution Detection (Week 8)
**Goal**: Detect template changes automatically

- [ ] Implement `SchemaEvolutionDetector`
- [ ] Detect new fields in source data
- [ ] Detect missing fields in template
- [ ] Detect renamed fields (fuzzy matching) we are using a lot of fuzzy these is on partity with our actual efforts 
- [ ] Generate schema drift reports
- [ ] Write tests with evolving schemas

**Deliverable**: Automatic detection of schema changes

### Phase 6: Hot-Reload & Monitoring (Week 9)
**Goal**: Support runtime template updates

- [ ] Implement file system watcher for template changes
- [ ] Implement template hot-reload without restart <--- Save to database and load during runtime?  load as IMonitorOption , Load as a service Configuration ?
- [ ] Add telemetry for template usage
- [ ] Add alerting for schema drift detection
- [ ] Add admin UI for template management (optional) <---Prefered, also for obserbabilty, consulting and tracking, almost mandatory for sistems like these one

**Deliverable**: Production-ready adaptive template system

### Phase 7: Migration & Rollout (Week 10)
**Goal**: Replace old exporters with adaptive versions

- [ ] Create migration guide
- [ ] Convert existing hardcoded templates to JSON
- [ ] Deploy adapter pattern to production
- [ ] Monitor performance and errors
- [ ] Gradual rollout with feature flag
- [ ] Deprecate old exporters

**Deliverable**: Full migration to adaptive template system

---

## 🔧 Technical Design Decisions

### 1. Why JSON for Templates?
- ✅ Human-readable and editable
- ✅ Schema validation via JSON Schema
- ✅ Version control friendly (Git diffs)
- ✅ No code compilation required
- ✅ Cross-platform compatibility

**Alternative considered**: YAML (too loose), XML (verbose), C# code (requires compilation)

### 2. Why Reflection for Field Mapping?
- ✅ Supports nested property paths (`Expediente.NumeroExpediente`)
- ✅ No code generation needed
- ✅ Runtime flexibility
- ⚠️ Performance overhead (mitigated by caching compiled expressions)

**Alternative considered**: Expression trees (complex), code generation (compilation required)

### 3. Why Adapter Pattern for Migration?
- ✅ Zero breaking changes to existing consumers
- ✅ One-line DI change to switch implementations
- ✅ Easy rollback if issues detected
- ✅ Parallel running for comparison testing

```csharp
// OLD (hardcoded):
services.AddScoped<IResponseExporter, SiroXmlExporter>();

// NEW (adaptive):
services.AddScoped<IResponseExporter, AdaptiveExporterAdapter>();
```

---

## ✅ Success Criteria

### Functional Requirements
- [ ] **FR1**: Bank changes Excel column order → System adapts without code changes
- [ ] **FR2**: CNBV adds new XML field → System detects and logs schema drift
- [ ] **FR3**: Template version upgrade → System loads new template automatically
- [ ] **FR4**: Invalid template → System falls back to previous version + alerts
- [ ] **FR5**: Multiple template versions → System supports A/B testing

### Non-Functional Requirements
- [ ] **NFR1**: Template load time < 100ms (cached)
- [ ] **NFR2**: Field mapping overhead < 5% vs hardcoded
- [ ] **NFR3**: Hot-reload without application restart
- [ ] **NFR4**: 100% backward compatible with existing exports
- [ ] **NFR5**: Full audit trail of template changes

---

## 📊 Comparison: Before vs After

| Aspect | Before (Hardcoded) | After (Adaptive) | Improvement |
|--------|-------------------|------------------|-------------|
| **Template Change** | Edit code + recompile + redeploy | Upload JSON file | **96% faster** |
| **Developer Time** | 4-6 hours per change | 5 minutes admin task | **98% reduction** |
| **Deployment Risk** | Full app redeployment | Config-only change | **Zero code risk** |
| **Schema Evolution** | Manual code review | Automatic detection | **100% automated** |
| **Version Management** | Git commits only | Template versioning + Git | **Better tracking** |
| **A/B Testing** | Impossible | Multiple templates | **New capability** |
| **Audit Trail** | Code diffs | Template change log | **Better compliance** |

---

## 🚨 Risks & Mitigation

### Risk 1: Performance Overhead from Reflection
**Probability**: Medium
**Impact**: Low
**Mitigation**:
- Cache compiled expression trees
- Benchmark against hardcoded version (target: < 5% overhead)
- Profile and optimize hot paths

### Risk 2: Template Misconfiguration
**Probability**: High (human error)
**Impact**: High (broken exports)
**Mitigation**:
- JSON schema validation on load
- Template validation tests
- Dry-run mode before applying
- Automatic rollback on errors
- Admin UI with preview

### Risk 3: Breaking Changes in Template Format
**Probability**: Medium
**Impact**: Medium
**Mitigation**:
- Template format versioning (v1, v2, etc.)
- Migration scripts for template upgrades
- Support multiple template formats simultaneously

---

## 📚 References

### Existing Adaptive Patterns in Codebase
- **Adaptive DOCX Extraction**: `ADAPTIVE_DOCX_REFACTORING_STATUS.md`
  - 5 extraction strategies with confidence-based selection
  - Similar pattern: multiple strategies → orchestrator → adapter
  - Lesson: Adapter pattern enables zero-downtime migration

### Similar Systems
- **Apache NiFi**: Data flow templates (JSON/XML)
- **Logstash**: Pipeline configuration (YAML/JSON)
- **Entity Framework Migrations**: Schema evolution tracking
- **AutoMapper**: Runtime object mapping (similar to field mapper)

---

## 🎯 Next Steps (Immediate Actions)

1. **Get User Approval** on this gap analysis and roadmap
2. **Prioritize Phase 1** (Foundation - Domain Models)
3. **Create Feature Branch**: `feature/adaptive-template-system`
4. **Set Up Project Structure**:
   ```
   Infrastructure.Export.Adaptive/
   ├── Domain/
   ├── Templates/
   └── Tests/
   ```
5. **Start ITDD**: Write contract tests for `ITemplateFieldMapper`

---

## 📝 Document History

| Date | Author | Change |
|------|--------|--------|
| 2025-11-30 | Claude Code | Initial gap analysis and implementation roadmap |

---

## ❓ Open Questions for User

1. **Template Storage Location**:
   - Option A: File system (`/Templates/*.json`)
   - Option B: Database (versioned templates table)<----Option B
   - Option C: Azure Blob Storage / S3 (cloud-first)
   - **Recommendation**: Start with file system (simplest), add DB later

2. **Template Versioning Strategy**:
   - Option A: Semantic versioning (v1.0.0, v1.1.0)<----Option A
   - Option B: Date-based (2025-01-15, 2025-02-01)
   - **Recommendation**: Semantic versioning (clearer breaking changes)

3. **Migration Timeline**:
   - Option A: Big-bang migration (replace all at once)<----Option A (DI injection nothing break during develepment only new interface implementaion is injected)
   - Option B: Gradual migration (adapter pattern, feature flag)
   - **Recommendation**: Gradual with adapter pattern (safer)

4. **Admin UI Priority**:
   - Option A: CLI tools only (developer-focused)
   - Option B: Web UI for template management <----Option A
   - **Recommendation**: Start with CLI, add UI in Phase 6

---

**Status**: Ready for review and approval ✅

<-- Notes from User -->

Approved for Implementation

Architecture rules enforced for linters and architecture testing
All interfaces must live on domain Interfaces
All interfaces must be tested ITTD wihtout implemention, eq, all must be mockes, behavioral test, to probe liskov
These test must be complete and green beroe an implementation is on place.
All implementation must live on her own project Implementation
All implementation must to pass the same test as the interface to probe liskov
Aditiona test can be added because details matter but must be meaninful behavioral test we dont test setter and geetters 
None infrastructure project can take depency on anoter infrastructure project, only on domain, No on aplication either
These is a CPM manages packages
Only system can take multiple depencies and had to test cross concerns, test with live system and live objects nothing can be mocked
All development had to be made TDD, all test must to pass using TDD first principles, remember the test name is the contract no the test code.

• - Start red: add a clear, behavior-driven failing test before any production code.
  - Make it green with the simplest implementation; no speculative code.
  - Keep cycles short and incremental: red → green → refactor.
  - Use expressive test names (e.g., Method_Scenario_Expectation or Given/When/Then).
  - Maintain tests as first-class code: clear AAA flow, explicit assertions, minimal hidden helpers.
  - Favor fast, deterministic, isolated tests; seed randomness and avoid external state.
  - Don’t mask failures: fix code or correct a bad test explicitly, don’t weaken checks.
  - Let tests drive design; refactor only with green tests and explain design changes briefly.
  - Be explicit with fixtures/seeds and generated code; document intent when auto-creating tests.
  - Run focused subsets during iteration; reserve full suites for validation once changes are stable.
