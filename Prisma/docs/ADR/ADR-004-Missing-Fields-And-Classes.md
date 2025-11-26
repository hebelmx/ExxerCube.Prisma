# ADR-004: Missing Fields and Classes in Expediente Domain Model

## Status
Proposed

## Context

During the implementation of the XML Extraction Demo with real PRP1 fixtures, we discovered potential gaps between the CNBV XML schema and our `Expediente` domain model. This ADR documents the missing fields and classes that need to be added to achieve complete coverage.

## Current State

### Expediente Class Location
`F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp\Domain\ValueObjects\Expediente.cs`

### Current Fields (Confirmed Present)
Based on the XML Extraction Demo implementation in `OCRDemo.razor`:

**Base Expediente Fields**:
- `NumeroExpediente` (string)
- `NumeroOficio` (string)
- `SolicitudSiara` (string)
- `Folio` (int)
- `OficioYear` (int)
- `AreaClave` (int)
- `AreaDescripcion` (string)
- `FechaPublicacion` (DateTime)
- `DiasPlazo` (int)
- `AutoridadNombre` (string)
- `AutoridadEspecificaNombre` (string)
- `NombreSolicitante` (string)
- `Referencia` (string)
- `Referencia1` (string)
- `Referencia2` (string)

**Nested Collections**:
- `SolicitudPartes` (List<SolicitudParte>)
- `SolicitudEspecificas` (List<SolicitudEspecifica>)

### SolicitudParte Class Fields
- `ParteId` (int)
- `Caracter` (string)
- `PersonaTipo` (string)
- `Paterno` (string)
- `Materno` (string)
- `Nombre` (string)
- `Rfc` (string)
- `Relacion` (string)
- `Domicilio` (string)
- `Complementarios` (string)

### SolicitudEspecifica Class Fields
- `SolicitudEspecificaId` (int)
- `InstruccionesCuentasPorConocer` (string)

## Analysis of CNBV XML Schema

### Known CNBV Fields (from PRP1 fixtures)

We need to analyze the actual XML structure of the 4 PRP1 fixtures to identify:
1. Fields present in XML but missing in domain model
2. Fields in domain model but never populated from XML
3. Optional vs required fields
4. Data type mismatches

### PRP1 Fixture Analysis Required

The following fixtures need systematic analysis:
- `222AAA-44444444442025.xml`
- `222AAA-55555555552025.xml`
- `222AAA-66666666662025.xml`
- `222AAA-77777777772025.xml`

## Missing Fields (Suspected)

### Category 1: Metadata Fields
Potentially missing from `Expediente`:
- `FechaRecepcion` (DateTime) - Reception date
- `EstadoExpediente` (string) - Current status
- `TipoDocumento` (string) - Document type classification
- `VersionXml` (string) - XML schema version

### Category 2: Authority Information
Potentially missing from `Expediente`:
- `AutoridadClave` (int) - Authority code
- `AutoridadEspecificaClave` (int) - Specific authority code
- `DelegacionRegional` (string) - Regional delegation

### Category 3: Temporal Tracking
Potentially missing from `Expediente`:
- `FechaLimiteRespuesta` (DateTime) - Response deadline (calculated from FechaPublicacion + DiasPlazo)
- `FechaNotificacion` (DateTime) - Notification date
- `HoraRecepcion` (TimeSpan) - Reception time

### Category 4: Classification
Potentially missing from `Expediente`:
- `ClasificacionAsunto` (string) - Matter classification
- `Prioridad` (string) - Priority level
- `Confidencialidad` (string) - Confidentiality level

### Category 5: SolicitudParte Enhancements
Potentially missing from `SolicitudParte`:
- `Email` (string) - Contact email
- `Telefono` (string) - Contact phone
- `RazonSocial` (string) - Corporate name (for legal entities)
- `RepresentanteLegal` (string) - Legal representative

### Category 6: SolicitudEspecifica Enhancements
Potentially missing from `SolicitudEspecifica`:
- `TipoInstruccion` (string) - Instruction type
- `PrioridadInstruccion` (int) - Instruction priority
- `FechaLimite` (DateTime) - Instruction deadline

## Decision

### Option 1: Add All Suspected Fields (Conservative)
**Pros**:
- Complete coverage, no data loss
- Future-proof for schema changes
- Supports all potential use cases

**Cons**:
- Larger domain model
- May include unused fields
- More maintenance overhead

### Option 2: Add Only Fields Present in PRP1 Fixtures (Pragmatic)
**Pros**:
- Lean domain model
- Only handles real-world data
- Easier to maintain

**Cons**:
- May miss edge cases
- Requires schema updates later
- Limited flexibility

### Option 3: Add Fields On-Demand (Agile)
**Pros**:
- Just-in-time implementation
- Driven by real requirements
- Minimal waste

**Cons**:
- May require frequent updates
- Could miss regulatory requirements
- Reactive rather than proactive

## Recommended Decision

**Option 2: Add Only Fields Present in PRP1 Fixtures**

**Rationale**:
1. PRP1 fixtures represent real CNBV regulatory documents
2. Hexagonal architecture makes adding fields later straightforward
3. Stakeholder demo needs real data, not theoretical fields
4. We can extend incrementally as new fixtures arrive

**Implementation Strategy**:
1. Parse all 4 PRP1 XML fixtures
2. Extract complete field list from actual XML
3. Compare with current `Expediente` class
4. Add missing fields found in real data
5. Mark optional fields with nullable types
6. Document which PRP1 fixtures use which fields

## Action Items

### 1. XML Schema Analysis
```bash
# Read all 4 PRP1 fixtures
# Extract unique XML elements
# Compare against Expediente class
# Generate field mapping report
```

### 2. Domain Model Updates
- Update `Expediente.cs` with missing fields
- Update `SolicitudParte.cs` with missing fields
- Update `SolicitudEspecifica.cs` with missing fields
- Add XML documentation for all new fields

### 3. Parser Updates
- Update `IXmlNullableParser<Expediente>` implementation
- Add parsing logic for new fields
- Handle nullable fields gracefully
- Add validation for required fields

### 4. Test Updates
- Update E2E tests to verify new fields
- Add assertions for all 4 PRP1 fixtures
- Verify nullable field handling
- Test edge cases (missing optional fields)

### 5. Demo Page Updates
- Add new fields to "Información General" tab
- Update statistics to include new field counts
- Add side-by-side comparison for new fields
- Update Complete Object JSON dump

## Verification

### Acceptance Criteria
- [ ] All fields present in PRP1 XML are captured in domain model
- [ ] Parser extracts all fields successfully from all 4 fixtures
- [ ] E2E tests pass with 100% field coverage
- [ ] Demo page displays all extracted fields
- [ ] No data loss when round-tripping through parser

### Testing Strategy
1. Run E2E tests against all 4 PRP1 fixtures
2. Manually verify each fixture in demo page
3. Export Complete Object JSON for each fixture
4. Compare JSON against source XML field-by-field
5. Verify no parsing errors in logs

## Consequences

### Positive
- Complete and accurate data extraction from CNBV XML
- Stakeholder confidence in system completeness
- Clear documentation of field mapping
- Foundation for future enhancements

### Negative
- Requires immediate work to analyze XML and update classes
- May discover more complex nested structures
- Could require database schema updates (if persisting)
- Demo page may need UI updates for new fields

### Neutral
- Establishes pattern for handling schema evolution
- Documents decision-making process for future reference
- Provides clear action items for implementation team

## Related Documents

- User Manual: `Prisma/Docs/Features/XML-Extraction-Demo-User-Manual.md`
- Expediente Class: `Prisma/Code/Src/CSharp/Domain/ValueObjects/Expediente.cs`
- E2E Tests: `Prisma/Code/Src/CSharp/Tests.Infrastructure.BrowserAutomation.E2E/XmlExtractionE2ETests.cs`
- Demo Page: `Prisma/Code/Src/CSharp/UI/ExxerCube.Prisma.Web.UI/Components/Pages/OCRDemo.razor`

## Next Steps

1. **Immediate**: Read all 4 PRP1 XML fixtures and extract complete element list
2. **Short-term**: Compare against current domain model and identify gaps
3. **Medium-term**: Implement missing fields in domain model and parser
4. **Long-term**: Establish process for handling CNBV schema evolution

## References

- CNBV XML Schema Documentation (if available)
- PRP1 Fixture Files: `Prisma/Fixtures/PRP1/*.xml`
- Hexagonal Architecture Guidelines
- Domain-Driven Design principles

## Decision Date
2025-11-25

## Decision Makers
- Development Team
- Domain Architect
- Stakeholder Representatives

## Review Date
After implementation of missing fields (estimated: 1 week)
