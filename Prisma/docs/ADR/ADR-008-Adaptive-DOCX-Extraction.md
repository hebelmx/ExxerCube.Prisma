# ADR-008: Adaptive DOCX Extraction System

**Date**: 2025-11-30
**Status**: Proposed
**Deciders**: Development Team
**Tags**: extraction, docx, architecture, open-closed-principle

## Context and Problem Statement

The current `DocxFieldExtractor` implementation uses simple regex patterns to extract fields from DOCX documents. This works for well-formatted CNBV documents but fails for:

1. **Variable formatting**: Documents with different label styles ("Expediente:" vs "Expediente No." vs "Número de Expediente")
2. **Table-based data**: Information presented in tables rather than label-value pairs
3. **Cross-references**: Mexican legal patterns like "la cantidad arriba mencionada" (the amount mentioned above)
4. **Complement extraction**: DOCX as a complement source when XML/OCR are missing data (EXPECTED workflow, not error handling)

## Decision Drivers

1. **Open-Closed Principle**: Must NOT break existing `DocxFieldExtractor` implementation or its consumers
2. **Zero Breaking Changes**: Existing tests, applications, and infrastructure must continue working
3. **Extensibility**: Need multiple extraction strategies that can be selected adaptively
4. **Complement Pattern**: DOCX extraction is often used to FILL GAPS in XML/OCR data (this is normal, not failure)
5. **Cross-Reference Resolution**: Must handle Mexican legal document patterns

## Considered Options

### Option 1: Modify Existing `IFieldExtractor<DocxSource>` Interface ❌
**Rejected** - Violates Open-Closed Principle, breaks all existing consumers

### Option 2: Create Parallel Adaptive System (Open-Closed Compliant) ✅
**Selected** - New interfaces/implementations alongside existing system

### Option 3: Feature Flag in Existing Extractor ⚠️
**Considered** - Less clean, mixes two approaches in one class

## Decision

**Create a NEW parallel adaptive DOCX extraction system** that coexists with the existing `DocxFieldExtractor`.

### Architecture

```
EXISTING SYSTEM (Untouched):
├── IFieldExtractor<DocxSource>
└── DocxFieldExtractor (simple regex, existing consumers)

NEW SYSTEM (Addition):
├── IAdaptiveDocxExtractor (new interface)
├── AdaptiveDocxExtractor (orchestrator)
├── IDocxExtractionStrategy (new interface)
└── Strategies:
    ├── StructuredDocxStrategy
    ├── ContextualDocxStrategy
    ├── TableBasedDocxStrategy
    ├── ComplementDocxStrategy (fills XML/OCR gaps)
    └── SearchDocxStrategy (cross-reference resolution)
```

### Key Principles

1. **No modifications** to existing `DocxFieldExtractor` or `IFieldExtractor<T>`
2. **New namespace**: `ExxerCube.Prisma.Infrastructure.Extraction.Adaptive`
3. **New interface names**: `IAdaptiveDocxExtractor`, `IDocxExtractionStrategy`
4. **Gradual migration**: Consumers can switch when ready
5. **Coexistence**: Both systems available via DI

## Consequences

### Positive

✅ **Zero breaking changes** - Existing code continues working
✅ **Open-Closed compliant** - System extended, not modified
✅ **Safe migration path** - Consumers switch at their own pace
✅ **Better testability** - New system tested independently
✅ **Clear separation** - Simple vs adaptive extraction clearly distinguished

### Negative

⚠️ **Code duplication** - Some extraction logic duplicated between systems
⚠️ **Two maintenance paths** - Must maintain both systems during transition
⚠️ **DI complexity** - Need both extractors registered

### Neutral

📝 **Documentation needed** - Clear guidance on when to use each system
📝 **Migration plan** - Document path from old to new system

## Implementation Plan

### Phase 1: Create New System (No Breaking Changes)
1. Create new namespace: `Infrastructure.Extraction.Adaptive`
2. Create `IAdaptiveDocxExtractor` interface
3. Create `IDocxExtractionStrategy` interface
4. Implement 5 strategies (Structured, Contextual, TableBased, Complement, Search)
5. Implement `AdaptiveDocxExtractor` orchestrator
6. Add DI registration for new system

### Phase 2: Testing & Validation
1. Unit tests for each strategy
2. Integration tests for adaptive extractor
3. Compare results: old vs new system
4. Validate on real CNBV documents

### Phase 3: Gradual Migration
1. Update `DocumentComparisonService` to use new extractor (if beneficial)
2. Monitor performance and accuracy
3. Document migration patterns
4. Eventually deprecate old system (6+ months)

## Technical Details

### Data Flow - Complement Pattern

```
Normal Flow:
XML Source → Has Expediente ✓
OCR Source → Has Cuenta ✓
DOCX Source → NOT CONSULTED (unnecessary)

Complement Flow (EXPECTED):
XML Source → Has Expediente ✓, Missing Cuenta ✗
OCR Source → Has partial data, Missing Cuenta ✗
DOCX Source → FILLS GAP with Cuenta ✓ ← This is NORMAL, not error!

Result: Merged data from all 3 sources (XML priority > OCR > DOCX)
```

### Critical Strategies

#### ComplementDocxStrategy
- **Purpose**: Fill gaps when XML/OCR missing data
- **Confidence**: 50 (always available but lower priority)
- **Usage**: Called when other sources incomplete
- **Not**: Error recovery mechanism (this is expected workflow!)

#### SearchDocxStrategy
- **Purpose**: Resolve cross-references in Mexican legal text
- **Patterns**: "cantidad arriba mencionada", "cuenta anteriormente indicada"
- **Method**: Search backward in document for referenced values
- **Confidence**: 80 if cross-refs detected, 0 otherwise

### Strategy Selection Algorithm

```csharp
1. Analyze document structure (DocxStructureAnalyzer)
2. Query all strategies: CanHandle(text) → confidence scores
3. Select strategy with highest confidence
4. If cross-references detected → also run SearchStrategy
5. If mode=Complement → use ComplementStrategy
6. Merge results from multiple strategies
```

## Compliance with SOLID Principles

### Single Responsibility ✅
- Each strategy handles ONE extraction approach
- Orchestrator handles selection logic only

### Open-Closed ✅
- **This ADR's primary goal!**
- Existing system unchanged (closed for modification)
- New strategies can be added (open for extension)

### Liskov Substitution ✅
- All strategies implement `IDocxExtractionStrategy`
- Interchangeable without breaking behavior

### Interface Segregation ✅
- `IDocxExtractionStrategy`: Only 2 methods (Extract, CanHandle)
- `IAdaptiveDocxExtractor`: Focused interface

### Dependency Inversion ✅
- Orchestrator depends on `IDocxExtractionStrategy` abstraction
- Strategies injected via DI

## Related Documents

- `CODE_REVIEW_DOCX_EXTRACTION.md` - Original requirements analysis
- `DOCX_EXTRACTION_IMPLEMENTATION_STATUS.md` - Implementation status
- `SYSTEM_FLOW_DIAGRAM.md` - System architecture context

## Notes

### Why Not Modify Existing Interface?

Changing `IFieldExtractor<DocxSource>` would impact:
- ✗ Application layer usage
- ✗ Infrastructure implementations
- ✗ Client/consumer code
- ✗ Unit tests (dozens of tests)
- ✗ Integration tests
- ✗ System tests
- **Result**: 84+ compilation errors, unknown runtime issues

### Migration Strategy

Old system → New system migration should be:
1. **Opt-in**: Consumers explicitly choose new extractor
2. **Gradual**: One consumer at a time
3. **Reversible**: Can fall back to old system if issues
4. **Measured**: Track accuracy improvements

## Decision Outcome

**Chosen option**: Option 2 - Create Parallel Adaptive System

**Rationale**:
- Respects Open-Closed Principle
- Zero risk to existing functionality
- Clean separation of concerns
- Safe, gradual migration path
- Can measure improvements objectively

**Next Steps**:
1. Rename existing strategy files to use new namespace
2. Create `IAdaptiveDocxExtractor` interface
3. Update strategy implementations to use new interface
4. Register both systems in DI
5. Document usage patterns
