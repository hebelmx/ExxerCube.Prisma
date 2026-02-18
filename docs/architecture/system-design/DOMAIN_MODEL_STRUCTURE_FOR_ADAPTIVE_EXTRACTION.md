# Domain Model Structure for Adaptive DOCX Extraction
**Date**: 2025-11-30
**Purpose**: Document domain model structure before implementing adaptive extraction using ITDD

## Complete Object Graph

### Expediente (Main Case File Entity)
**Location**: `Domain/Entities/Expediente.cs`
**Purpose**: Main regulatory case file from CNBV/UIF

```
Expediente (Root Entity)
├── Core Properties (Case Identification)
│   ├── NumeroExpediente: string         // "A/AS1-2505-088637-PHM"
│   ├── NumeroOficio: string             // "214-1-18714972/2025"
│   ├── SolicitudSiara: string           // SIARA request number
│   ├── Folio: int
│   ├── OficioYear: int
│   └── AreaClave: int
│
├── Metadata Properties (Case Context)
│   ├── AreaDescripcion: string          // "ASEGURAMIENTO", "HACENDARIO"
│   ├── Subdivision: LegalSubdivisionKind
│   ├── FechaPublicacion: DateTime
│   ├── FechaRecepcion: DateTime
│   ├── FechaRegistro: DateTime
│   ├── FechaEstimadaConclusion: DateTime
│   └── DiasPlazo: int
│
├── Authority Properties
│   ├── AutoridadNombre: string
│   ├── AutoridadEspecificaNombre: string?
│   ├── NombreSolicitante: string?
│   └── FundamentoLegal: string
│
├── Administrative Properties
│   ├── MedioEnvio: string               // SIARA/Fisico
│   ├── EvidenciaFirma: string
│   ├── OficioOrigen: string
│   ├── AcuerdoReferencia: string
│   ├── Referencia: string
│   ├── Referencia1: string
│   ├── Referencia2: string
│   └── TieneAseguramiento: bool
│
└── **Nested Collections** (WHERE THE DATA LIVES!)
    ├── SolicitudPartes: List<SolicitudParte>           // Parties involved
    └── SolicitudEspecificas: List<SolicitudEspecifica> // Specific requests
```

### SolicitudParte (Party Involved)
**Location**: `Domain/Entities/SolicitudParte.cs`
**Purpose**: Person/entity that is the subject of the case

```
SolicitudParte
├── Paterno: string          // Paternal last name
├── Materno: string          // Maternal last name
├── Nombre: string           // First name(s)
├── Rfc: string?             // Tax ID
├── RfcVariantes: List<RfcVariant>
└── Domicilio: string?       // Address
```

**IMPORTANT**: Mexican names use THREE parts:
- `Paterno` (first last name - from father)
- `Materno` (second last name - from mother)
- `Nombre` (given names)

Example: "Juan Carlos GARCÍA LÓPEZ"
- Nombre: "Juan Carlos"
- Paterno: "GARCÍA"
- Materno: "LÓPEZ"

### SolicitudEspecifica (Specific Request)
**Location**: `Domain/Entities/SolicitudEspecifica.cs`
**Purpose**: Specific measure being requested

```
SolicitudEspecifica (THIS IS WHERE ACTION DATA LIVES!)
├── SolicitudEspecificaId: int
├── Measure: MeasureKind                         // Block, Unblock, Transfer, Info, etc.
├── InstruccionesCuentasPorConocer: string       // Legal instructions (500+ chars)
│
└── **Nested Collections** (THE ACTUAL DATA!)
    ├── PersonasSolicitud: List<PersonaSolicitud>  // Persons in this specific request
    ├── Cuentas: List<Cuenta>                      // Accounts to act on
    └── Documentos: List<DocumentItem>             // Documents to provide
```

### PersonaSolicitud (Person in Specific Request Context)
**Location**: `Domain/Entities/PersonaSolicitud.cs`
**Purpose**: Person in the context of a SolicitudEspecifica

```
PersonaSolicitud
├── Caracter: string         // Role ("Solicitante", "Demandado", etc.)
├── Persona: string          // "Fisica" or "Moral"
├── Paterno: string?
├── Materno: string?
├── Nombre: string
└── Rfc: string?
```

**Difference from SolicitudParte**:
- `SolicitudParte`: Party in the overall case (top-level)
- `PersonaSolicitud`: Person in context of specific measure (nested in SolicitudEspecifica)

### Cuenta (Account Value Object)
**Location**: `Domain/ValueObjects/Cuenta.cs`
**Purpose**: Bank account information

```
Cuenta (Value Object)
├── Numero: string           // Account number
├── Banco: string            // Bank name
├── Sucursal: string         // Branch
├── Producto: string         // Product type
├── Moneda: string           // Currency
├── Monto: decimal?          // Amount
└── TipoMonto: string        // Amount qualifier
```

### DocumentItem (Requested Document)
**Location**: `Domain/ValueObjects/DocumentItem.cs`
**Purpose**: Document being requested

```
DocumentItem (Value Object)
├── Tipo: DocumentItemKind   // Document type
├── PeriodoInicio: DateOnly?
├── PeriodoFin: DateOnly?
├── Certificada: bool?       // Certified copy required
└── Notas: string
```

## ExtractedFields (Simple DTO)

**Location**: `Domain/ValueObjects/ExtractedFields.cs`
**Purpose**: Simple DTO for extraction results - NOT an entity!

```
ExtractedFields (DTO - What Strategies Return)
├── Expediente: string?                      // Case number
├── Causa: string?                           // Cause
├── AccionSolicitada: string?                // Requested action
├── Fechas: List<string>                     // Extracted dates
├── Montos: List<AmountData>                 // Extracted amounts
└── AdditionalFields: Dictionary<string, string?>  // Extended data
```

### AmountData (Monetary Value)
**Location**: `Domain/ValueObjects/AmountData.cs`
**Purpose**: Monetary amount with currency

```
AmountData (Value Object)
├── Currency: string         // "MXN", "USD"
├── Value: decimal
└── OriginalText: string     // Where it came from
```

## Data Flow Architecture

### Current System (Low-Level)
```
DOCX Source
    ↓
DocxFieldExtractor
    ↓
ExtractedFields (Simple DTO)
    ↓
Manual Mapping to Expediente
    ↓
Expediente Entity (Complex nested structure)
```

### Adaptive System (What We're Building)
```
DOCX Source
    ↓
AdaptiveDocxExtractor (Orchestrator)
    ├→ Strategy 1: StructuredDocxStrategy
    ├→ Strategy 2: ContextualDocxStrategy
    ├→ Strategy 3: TableBasedDocxStrategy
    ├→ Strategy 4: ComplementExtractionStrategy
    └→ Strategy 5: SearchExtractionStrategy
    ↓
Merge Strategy (Combine Results)
    ↓
ExtractedFields (Simple DTO)
    ↓
** Manual Mapping Still Required **
    ↓
Expediente Entity (Complex nested structure)
```

## Critical Understanding

### What Adaptive Strategies Should Return

**Answer**: `ExtractedFields` (Simple DTO)

**Why NOT Expediente**:
1. **Expediente is complex** - nested collections (SolicitudPartes, SolicitudEspecificas)
2. **Data lives deep in graph** - Person names in PersonaSolicitud, Accounts in SolicitudEspecifica.Cuentas
3. **Mapping logic is business rule** - How to populate nested structures is domain logic
4. **Strategies extract, don't map** - Extraction != Entity population

### What Goes Where

#### ExtractedFields Properties (Core Data)
```csharp
Expediente     → Case number (e.g., "A/AS1-2505-088637-PHM")
Causa          → Legal cause/reason
AccionSolicitada → Requested action type
Fechas         → List of dates found in document
Montos         → List of monetary amounts with currency
```

#### ExtractedFields.AdditionalFields (Extended Data)
```csharp
AdditionalFields["NumeroOficio"]           → "214-1-18714972/2025"
AdditionalFields["AutoridadNombre"]        → "PGR"
AdditionalFields["NombreCompleto"]         → "Juan Carlos GARCÍA LÓPEZ"
AdditionalFields["Paterno"]                → "GARCÍA"
AdditionalFields["Materno"]                → "LÓPEZ"
AdditionalFields["Nombre"]                 → "Juan Carlos"
AdditionalFields["Rfc"]                    → "GALJ850101XXX"
AdditionalFields["NumeroCuenta"]           → "0123456789012345"
AdditionalFields["Banco"]                  → "BANAMEX"
AdditionalFields["CLABE"]                  → "012345678901234567"
AdditionalFields["FechaPublicacion"]       → "2025-11-15"
AdditionalFields["DiasPlazo"]              → "5"
// ... any other field extracted from DOCX
```

### Mexican Name Pattern Recognition

**Full Name Patterns**:
```regex
// Pattern 1: PATERNO MATERNO NOMBRE
"GARCÍA LÓPEZ JUAN CARLOS"
  ↓
  Paterno: "GARCÍA"
  Materno: "LÓPEZ"
  Nombre: "JUAN CARLOS"

// Pattern 2: NOMBRE PATERNO MATERNO
"JUAN CARLOS GARCÍA LÓPEZ"
  ↓
  Nombre: "JUAN CARLOS"
  Paterno: "GARCÍA"
  Materno: "LÓPEZ"

// Pattern 3: Labeled
"Paterno: GARCÍA  Materno: LÓPEZ  Nombre: JUAN CARLOS"
```

**Fuzzy Matching Considerations**:
- "GARCIA" vs "GARCÍA" (accents)
- "PEREZ" vs "PÉREZ"
- "MA. TERESA" vs "MARIA TERESA"
- "FCO. JAVIER" vs "FRANCISCO JAVIER"

## Strategy Patterns

### 1. Structured DOCX Strategy
**Best For**: Well-formatted CNBV documents with standard labels

**Extraction Pattern**:
```
Expediente No.: A/AS1-2505-088637-PHM    → ExtractedFields.Expediente
Oficio: 214-1-18714972/2025              → AdditionalFields["NumeroOficio"]
Autoridad: PGR                           → AdditionalFields["AutoridadNombre"]
```

### 2. Contextual DOCX Strategy
**Best For**: Semi-structured with label variations

**Extraction Pattern**:
```
"Expediente Número: ..."     → ExtractedFields.Expediente
"Número de Expediente: ..."  → ExtractedFields.Expediente
"Exp. No.: ..."              → ExtractedFields.Expediente
```

### 3. Table-Based DOCX Strategy
**Best For**: Tabular data

**Extraction Pattern**:
```
| Nombre           | RFC           | Cuenta          |
|------------------|---------------|-----------------|
| GARCÍA LÓPEZ JC  | GALJ850101XXX | 0123456789012345|
    ↓
    AdditionalFields["NombreCompleto"] = "GARCÍA LÓPEZ JC"
    AdditionalFields["Rfc"] = "GALJ850101XXX"
    AdditionalFields["NumeroCuenta"] = "0123456789012345"
```

### 4. Complement Extraction Strategy
**Purpose**: Fill XML/OCR gaps (EXPECTED workflow, not error handling)
**Pattern**: When XML has incomplete data, DOCX complements it

**Example**:
```
XML has:     Expediente="A/AS1-2505-088637-PHM"
XML missing: Account numbers

DOCX strategy extracts:
    AdditionalFields["NumeroCuenta"] = "0123456789012345"
    AdditionalFields["CLABE"] = "012345678901234567"

Result: Complete data set (XML + DOCX complement)
```

### 5. Search Extraction Strategy
**Purpose**: Resolve cross-references in Mexican legal documents

**Cross-Reference Patterns**:
```
"cantidad arriba mencionada"      → Search backward for amount
"cuenta anteriormente indicada"   → Search backward for account
"persona antes citada"            → Search backward for name
"oficio referido"                 → Search backward for oficio number
```

**Example**:
```
Document text:
"...cuenta número 0123456789012345 del banco BANAMEX..."
(500 words later)
"...transferir la cuenta anteriormente indicada..."

Strategy:
1. Finds "cuenta anteriormente indicada" (cross-reference keyword)
2. Searches backward in document
3. Finds "cuenta número 0123456789012345"
4. Extracts: AdditionalFields["NumeroCuenta"] = "0123456789012345"
```

## ITDD Interface Design Guidelines

### What Interface Should Define

```csharp
/// <summary>
/// Adaptive DOCX extraction strategy interface.
/// </summary>
/// <remarks>
/// Strategies extract structured data from DOCX documents using various patterns.
/// Returns ExtractedFields DTO, NOT Expediente entity.
/// Mapping ExtractedFields to Expediente is separate business logic.
/// </remarks>
public interface IAdaptiveDocxExtractionStrategy
{
    /// <summary>
    /// Extracts structured data from DOCX document.
    /// </summary>
    /// <param name="docxText">Full text content of DOCX document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>ExtractedFields DTO with core fields and additional data.</returns>
    Task<ExtractedFields?> ExtractAsync(
        string docxText,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if this strategy is applicable to the document.
    /// </summary>
    /// <param name="docxText">Full text content of DOCX document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if strategy can extract from this document.</returns>
    Task<bool> CanExtractAsync(
        string docxText,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the confidence score for this strategy's applicability.
    /// </summary>
    /// <param name="docxText">Full text content of DOCX document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Confidence score 0-100.</returns>
    Task<int> GetConfidenceAsync(
        string docxText,
        CancellationToken cancellationToken = default);
}
```

### What Interface Should NOT Do

❌ **Return Expediente Entity**
```csharp
// WRONG - Strategies don't understand entity mapping
Task<Expediente?> ExtractAsync(...);
```

❌ **Take Complex Parameters**
```csharp
// WRONG - Strategies don't need entity context
Task<ExtractedFields?> ExtractAsync(Expediente existing, ...);
```

❌ **Mix Concerns**
```csharp
// WRONG - Extraction and persistence are separate
Task<bool> ExtractAndSaveAsync(...);
```

## Next Steps (ITDD Workflow)

### 1. Define Interface in Domain ✅ (Ready to start)
**File**: `Domain/Interfaces/IAdaptiveDocxExtractionStrategy.cs`
**Namespace**: `ExxerCube.Prisma.Domain.Interfaces`
**Dependencies**: Only Domain types (ExtractedFields)

### 2. Write Interface Tests with Mocks
**File**: `Tests.Domain/Interfaces/AdaptiveDocxExtractionStrategyContractTests.cs`
**Purpose**: Prove Liskov Substitution Principle
**Pattern**: Test interface contract, not implementation

### 3. Implement First Strategy
**File**: `Infrastructure.Extraction.Adaptive/Strategies/StructuredDocxStrategy.cs`
**Namespace**: `ExxerCube.Prisma.Infrastructure.Extraction.Adaptive`
**Test**: Must pass interface contract tests

### 4. Verify Liskov
**Principle**: Any implementation satisfying interface tests is correct
**Test**: Run interface tests against concrete implementation

## Summary

### Key Learnings

1. **Expediente is complex** - nested collections, not flat structure
2. **ExtractedFields is correct return type** - simple DTO, not entity
3. **Data lives in nested collections** - PersonaSolicitud, Cuenta in SolicitudEspecifica
4. **Strategies extract, don't map** - Mapping to Expediente is separate concern
5. **Mexican names have 3 parts** - Paterno, Materno, Nombre (not "NombreCompleto")
6. **AdditionalFields is extension point** - Any extracted data not in core properties

### Mistakes to Avoid

❌ Don't return Expediente from strategies (wrong abstraction level)
❌ Don't assume flat structure (data is deeply nested)
❌ Don't conflate extraction with entity mapping (separate concerns)
❌ Don't implement before reading models (failed attempt documented in ADAPTIVE_DOCX_STATUS_FINAL.md)
❌ Don't modify existing interfaces (Open-Closed Principle violation)

### Success Criteria

✅ Interface only knows Domain types
✅ Tests prove Liskov with mocks only
✅ Implementation passes interface tests
✅ Zero breaking changes to existing code
✅ Comprehensive code coverage
✅ One project per implementation
