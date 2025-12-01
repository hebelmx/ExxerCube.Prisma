 🚨 REAL Data Quality Issues Found in XML Samples

  Quality Problems:

  1. Trailing whitespace everywhere (NumeroOficio, NumeroExpediente)
  2. HTML entities: &nbsp; instead of null
  3. Empty RFC fields: <Rfc>             </Rfc> (13 spaces)
  4. Uncontrolled vocabularies: "Operaciones Ilícitas" (not in our controlled list)
  5. Truncated text: AutoridadNombre line break in XML
  6. Duplicate persons: Same person, 2 different RFCs (LUMH111111111 vs LUMH222222222)
  7. Human annotations: "NO SE CUENTA", "Se trata de la misma persona con variante en el RFC"
  8. Structured data in text: Amounts, RFCs, account numbers buried in InstruccionesCuentasPorConocer
  9. Typos: "CUATO MIL" instead of "CUATRO MIL"

  Critical Insight:

  - XML is NOT authoritative - it's hand-filled with human errors
  - PDF from CNBV is MORE reliable - official source document
  - DOCX from authorities - variable quality, different formats

  ---
  🧠 REVISED Data Fusion Algorithm (Reality-Based)

  Core Principle:

  NO SOURCE IS AUTHORITATIVE
  Each source votes, confidence determines winner
  Cross-validation catches human errors

  Phase 1: Pre-Processing & Sanitization

  Step 1: Clean ALL values before comparison
    - Trim whitespace
    - Remove HTML entities (&nbsp; → null)
    - Normalize case for controlled vocabularies
    - Extract structured data from unstructured text (regex patterns)

  Step 2: Validate against known patterns
    - RFC: [A-Z]{4}\d{6}[A-Z0-9]{3}
    - Expediente: [A-Z]/[A-Z]{1,2}\d+-\d+-\d+-[A-Z]+
    - CURP: [A-Z]{4}\d{6}[HM][A-Z]{5}[A-Z0-9]{2}
    - Amounts: Extract from text ("$76,813.31 pesos" → 76813.31)

  Step 3: Flag impossible values
    - RFC with 13 spaces → NULL
    - "NO SE CUENTA" → NULL
    - "el monto mencionado en el texto" → NULL (flag for extraction from text)

  Phase 2: Multi-Source Reconciliation

  Scenario Matrix:

  | Scenario                        | XML                      | PDF (OCR)    | DOCX (OCR)   | Decision
            |
  |---------------------------------|--------------------------|--------------|--------------|----------------------------
  ----------|
  | 1. All agree                    | ✅ Valid                  | ✅ Same       | ✅ Same       | Use value (confidence:
  0.95)         |
  | 2. XML valid, OCR empty         | ✅ Valid                  | ❌ Null       | ❌ Null       | Use XML (confidence:
  0.75)           |
  | 3. XML empty, PDF valid         | ❌ Null                   | ✅ Valid      | ?            | Use PDF (confidence:
  0.80)*          |
  | 4. XML has "NO SE CUENTA"       | ❌ Invalid                | ✅ Valid      | ?            | Use PDF (confidence:
  0.85)*          |
  | 5. PDF/XML disagree (similar)   | "Juan Pérez"             | "Juan Perez" | "Juan Peres" | Fuzzy match → XML
  (confidence: 0.70) |
  | 6. PDF/XML conflict (different) | "50000"                  | "76813.31"   | ?            | MANUAL REVIEW ⚠️
            |
  | 7. All sources empty            | ❌ Null                   | ❌ Null       | ❌ Null       | Check if required → Flag
   if yes      |
  | 8. Duplicate entities           | 2x Same person, diff RFC | ?            | ?            | MANUAL REVIEW ⚠️
            |

  *PDF from CNBV is more reliable than hand-filled XML

  Phase 3: Field-Specific Strategies

  FieldType: RFC
    Priority: PDF > XML > DOCX
    Validation: Regex pattern + Luhn checksum
    Fuzzy: NO (must be exact)
    Conflict: Manual review

  FieldType: NumeroExpediente
    Priority: PDF > XML > DOCX
    Validation: Regex pattern [A-Z]/[A-Z]{1,2}\d+-\d+-\d+-[A-Z]+
    Fuzzy: NO (legal identifier, must be exact)
    Conflict: Manual review

  FieldType: Name (Paterno, Materno, Nombre)
    Priority: PDF > XML > DOCX
    Validation: Not empty, not "&nbsp;", not "NO SE CUENTA"
    Fuzzy: YES (threshold 85%)
    Conflict: Use most complete name (least whitespace)

  FieldType: AreaDescripcion
    Priority: PDF > XML > DOCX
    Validation: Expand controlled vocabulary OR allow freeform
    Fuzzy: YES (85% match to known values)
    Conflict: If unknown term, keep it (e.g., "Operaciones Ilícitas")

  FieldType: Monto (Amount)
    Priority: Extract from text > Structured field
    Validation: Numeric, extract from patterns "$76,813.31 pesos"
    Fuzzy: NO (numbers must be exact)
    Conflict: Variance > 5% → Manual review

  FieldType: InstruccionesCuentasPorConocer (Unstructured)
    Strategy: Extract structured data with regex
      - Account numbers: \d{10,}
      - RFCs: [A-Z]{4}\d{6}[A-Z0-9]{3}
      - CURPs: [A-Z]{4}\d{6}[HM][A-Z]{5}[A-Z0-9]{2}
      - Amounts: \$[\d,]+\.?\d* pesos
    Store: Original text + extracted structured fields

  Phase 4: Confidence Scoring

  For each field:

    Case: All 3 sources agree (exact match)
      → Confidence = 0.95

    Case: 2 sources agree (exact match)
      → Confidence = 0.85

    Case: PDF only (XML null/invalid)
      → Confidence = 0.80 (PDF is official doc)

    Case: XML only (PDF null/invalid)
      → Confidence = 0.75 (XML hand-filled, less reliable)

    Case: Fuzzy match (similarity > 85%)
      → Confidence = similarity * 0.90

    Case: Sources conflict (cannot reconcile)
      → Confidence = 0.00 → MANUAL REVIEW

    Case: Extracted from unstructured text
      → Confidence = 0.60 (regex extraction is risky)

    Case: All sources null/invalid
      → Confidence = 0.00
        IF required field → MANUAL REVIEW
        IF optional field → NULL (acceptable)

  Phase 5: Overall Expediente Confidence

  Required Fields (e.g., NumeroExpediente, AreaDescripcion, AutoridadNombre):
    Total: 12 fields

  Optional Fields (e.g., RFC, CURP, Domicilio):
    Total: 18 fields

  Score Calculation:
    RequiredScore = Sum(confidence of required fields) / 12
    OptionalScore = Sum(confidence of optional fields) / 18

    Overall = (RequiredScore * 0.70) + (OptionalScore * 0.30)

    IF RequiredScore < 0.70 → MANUAL REVIEW ⚠️
    IF Overall > 0.85 → AUTO-PROCESS ✅
    IF Overall 0.70-0.85 → REVIEW RECOMMENDED ⚠️
    IF Overall < 0.70 → MANUAL REVIEW REQUIRED 🔴

  ---
  🔧 Implementation Strategy

  public class EnhancedMatchingPolicyService : IMatchingPolicy
  {
      private readonly IFuzzyMatcher _fuzzyMatcher; // FuzzySharp
      private readonly ILogger<EnhancedMatchingPolicyService> _logger;

      public async Task<FieldMatchResult> SelectBestValueAsync(
          string fieldName,
          List<FieldValue> rawValues,
          CancellationToken ct)
      {
          // Step 1: Sanitize
          var sanitized = rawValues
              .Select(v => SanitizeValue(v))
              .Where(v => IsValid(fieldName, v.Value))
              .ToList();

          // Step 2: Check for agreement
          if (AllExactMatch(sanitized))
          {
              return Success(sanitized.First().Value, confidence: 0.95);
          }

          // Step 3: Try fuzzy matching (name fields only)
          if (IsFuzzyField(fieldName))
          {
              var fuzzyResult = await FuzzyMatchAsync(sanitized);
              if (fuzzyResult.Similarity >= 0.85)
              {
                  return Success(
                      fuzzyResult.BestValue,
                      confidence: fuzzyResult.Similarity * 0.90);
              }
          }

          // Step 4: Apply source priority (PDF > XML > DOCX for CNBV docs)
          var prioritized = ApplySourcePriority(sanitized, fieldName);
          if (prioritized != null && prioritized.Confidence > 0.70)
          {
              return Success(prioritized.Value, confidence: prioritized.Confidence);
          }

          // Step 5: Cannot reconcile - flag for manual review
          return Conflict(
              sanitized,
              reason: $"Sources disagree on {fieldName}, requires manual review");
      }

      private FieldValue SanitizeValue(FieldValue raw)
      {
          var cleaned = raw.Value
              ?.Trim()
              .Replace("&nbsp;", "")
              .Replace("&amp;nbsp;", "");

          // Detect human annotations
          if (cleaned == "NO SE CUENTA" ||
              cleaned?.Length == 13 && cleaned.All(c => c == ' '))
          {
              cleaned = null;
          }

          return new FieldValue(raw.FieldName, cleaned, raw.Confidence, raw.Source);
      }
  }

  ---
  ❓ Questions Before I Implement

  1. Source Priority for CNBV docs: Should we always prefer PDF > XML since PDF is the official source and XML is
  hand-filled?
  2. Uncontrolled vocabularies: "Operaciones Ilícitas" isn't in our list - should we:
    - a) Expand the controlled vocabulary dynamically?
    - b) Flag as "unknown category" for review?
    - c) Allow freeform values?
  3. Duplicate entities (same person, 2 RFCs): Should we:
    - a) Create 2 separate records and flag as "possible duplicate"?
    - b) Merge them and store both RFCs?
    - c) Manual review required?
  4. Extracting structured data from text (InstruccionesCuentasPorConocer): Should I implement regex extractors for:
    - Account numbers
    - RFCs
    - CURPs
    - Amounts
  5. FuzzySharp settings: What similarity threshold? (I proposed 85%)

  Let me know and I'll implement the complete fusion engine! 🚀

  Todos
  ☒ Reconcile data model: Laws vs XML Samples vs Domain implementation
  ☒ Run build to verify no compilation errors
  ☒ Update all extractors to initialize new Expediente properties
  ☒ Implement best-effort extraction of LawMandatedFields from XML
  ☒ Implement best-effort extraction of LawMandatedFields for DOCX and PDF
  ☐ Address Finding C-2: Missing Pipeline Orchestration Event Handler (CRITICAL)
  ☐ Address Finding U-1: Incomplete Best-Effort Data Fusion Logic (URGENT)
  ☐ Address Finding I-2: Missing SLA Alerting Mechanism (IMPORTANT)
  ☐ Create concrete plan for remaining audit findings

──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
> OK, I KNOw what to do know the algo is perfect, we need to optimize the coeficients, optimize the abstraction algorithms
  first, we had to take into acount the teseract ocr confidence, and the quality index coeficient, how are we going to
  optimize , we are going to use these aproach
  "F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\docs\OCR_FILTER_OPTIMIZATION_JOURNAL.md" , we already have a dumy
  generator, we had already optimized a  image enhacer filter coeficient, i know code, i know algos, you will have to
  notice, so please these proposla t
  
  
  🎯 My Understanding of Your Request

  Context:

  1. Optimization Methodology Already Exists: You successfully used coefficient optimization for image enhancement filters
   (documented in OCR_FILTER_OPTIMIZATION_JOURNAL.md)
  2. Same Approach for Fusion: Apply the SAME scientific optimization methodology to the fusion algorithm coefficients
  3. Data-Driven Tuning: Use your dummy data generator + real samples to optimize:
    - Confidence thresholds
    - Source priority weights
    - Fuzzy match similarity thresholds
    - Quality index coefficients
    - OCR confidence integration

  Architecture Decision:

  High Cohesion Principle: Fusion and Classification belong together because:
  - Both analyze the SAME Expediente data
  - Classification needs fusion results to determine requirement type
  - Fusion needs classification to know which fields are required
  - They iterate together (fusion → classify → validate → re-fuse if needed)

  New Interfaces to Create:

  // Domain/Interfaces/IFusionExpediente.cs
  public interface IFusionExpediente
  {
      /// <summary>
      /// Fuses data from multiple sources into reconciled Expediente(s).
      /// </summary>
      /// <param name="xmlExpediente">Data from XML source (may be null/incomplete)</param>
      /// <param name="pdfExpediente">Data from PDF OCR extraction (may be null/incomplete)</param>
      /// <param name="docxExpediente">Data from DOCX OCR extraction (may be null/incomplete)</param>
      /// <param name="xmlMetadata">Extraction metadata (confidence, quality, etc.)</param>
      /// <param name="pdfMetadata">OCR metadata (Tesseract confidence, quality index, etc.)</param>
      /// <param name="docxMetadata">OCR metadata (Tesseract confidence, quality index, etc.)</param>
      /// <returns>
      /// FusionResult containing:
      ///   - 0 Expedientes: All sources failed, cannot proceed → MANUAL REVIEW
      ///   - 1 Expediente: Successfully fused → PROCEED TO CLASSIFICATION
      ///   - 2 Expedientes: Conflict cannot be resolved → BOTH SENT TO MANUAL REVIEW
      /// Plus NextAction indicator (AutoProcess, ReviewRecommended, ManualRequired)
      /// </returns>
      Task<FusionResult> FuseAsync(
          Expediente? xmlExpediente,
          Expediente? pdfExpediente,
          Expediente? docxExpediente,
          ExtractionMetadata xmlMetadata,
          ExtractionMetadata pdfMetadata,
          ExtractionMetadata docxMetadata,
          CancellationToken cancellationToken);
  }

  // Domain/Interfaces/IExpedienteClasifier.cs
  public interface IExpedienteClasifier
  {
      /// <summary>
      /// Classifies Expediente into requirement type and determines required fields.
      /// </summary>
      Task<ClassificationResult> ClassifyAsync(
          Expediente expediente,
          CancellationToken cancellationToken);
  }

  Location Strategy:

  | Component                         | Location                                                        | Why
                                     |
  |-----------------------------------|-----------------------------------------------------------------|-----------------
  -----------------------------------|
  | IFusionExpediente                 | 01-Core/Domain/Interfaces/                                      | Domain contract
                                     |
  | IExpedienteClasifier              | 01-Core/Domain/Interfaces/                                      | Domain contract
                                     |
  | FusionExpedienteService           | 02-Infrastructure/Infrastructure.Classification/                | Implementation
  (same project due to High Cohesion) |
  | ExpedienteClasifierService        | 02-Infrastructure/Infrastructure.Classification/                | Implementation
  (same project)                      |
  | IFusionExpedienteContractTests    | 04-Tests/01-Core/Tests.Domain/Domain/Interfaces/                | ITDD contract
  tests                                |
  | IExpedienteClasifierContractTests | 04-Tests/01-Core/Tests.Domain/Domain/Interfaces/                | ITDD contract
  tests                                |
  | FusionExpedienteServiceTests      | 04-Tests/02-Infrastructure/Tests.Infrastructure.Classification/ | Implementation
  tests                               |

  Optimization Approach:

  Apply the OCR_FILTER_OPTIMIZATION_JOURNAL methodology:
  1. Baseline: Current algorithm with initial coefficients
  2. Generate Test Set: Use dummy generator + real samples (4 PRP1 samples)
  3. Define Metrics: Accuracy, precision, recall for field matching
  4. Grid Search: Test coefficient combinations
  5. Validate: Use held-out test set
  6. Document: Journal all experiments like you did for image filters

  Inputs I Need to Account For:

  - Tesseract OCR Confidence (per character/word/line)
  - Quality Index Coefficient (from image preprocessing)
  - Source Reliability (PDF > XML > DOCX for CNBV)
  - Field-Specific Strategies (from ClassificationRules.md)
  - Legal Requirements (from LegalRequirements_Summary.md, R29 PDF, SIARA Manual)

  My Tasks:

  1. ✅ Echo back understanding
  2. ✅ Read the 5 documents you specified
  3. ⏳ Finish the proposal in FusionRequirement.md (IN PROGRESS BELOW)
  4. ⏳ Design IFusionExpediente interface signature
  5. ⏳ Design IExpedienteClasifier interface signature
  6. ⏳ Define FusionResult and ClassificationResult value objects
  7. ⏳ Create ITDD contract test structure

───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
📋 CRITICAL INSIGHTS FROM R29 & SIARA MANUAL
───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

## R29 A-2911: 42 Mandatory Fields (NO NULLS PERMITTED)

The R29 report is the GOLD STANDARD for what constitutes a "complete" Expediente. Monthly reporting
within 10 days requires ALL 42 fields populated with valid data.

### Section I: Identificador del Reporte (3 fields)
1. PERIODO (AAAAMM)
2. CLAVE_INSTITUCION (6 chars)
3. REPORTE (2911)

### Section II: Datos del Requerimiento (7 fields)
4. MEDIO_SOLICITUD (100=Directo, 200=Vía CNBV)
5. AUTORIDAD_CLAVE (from catalog)
6. AUTORIDAD_DESCRIPCION (must match catalog)
7. NUMERO_OFICIO (30 chars max, unique per titular/cotitular)
8. FECHA_SOLICITUD (AAAAMMDD)
9. FOLIO_SIARA (format: 18dígitos/AAAA/6dígitos) OR referencia directa
10. MONTO_SOLICITADO (rounded pesos, 0 if not specified)

### Section III: Datos del Titular (7 fields)
11. PERSONALIDAD_JURIDICA_TITULAR (1=Física, 2=Moral)
12. CARACTER_TITULAR (from 40+ catalog values: ACT, DEMADO, CON, etc.)
13. RFC_TITULAR (13 chars física, _+12 chars moral, format: XXXXAAMMDDXXX or _XXXAAMMDDXXX)
14. RAZON_SOCIAL_TITULAR (250 chars, SIN tipo sociedad)
15. NOMBRE_TITULAR (100 chars, no titles/honorifics)
16. APELLIDO_PATERNO_TITULAR (100 chars)
17. APELLIDO_MATERNO_TITULAR (100 chars)

### Section IV: Datos del Cotitular (7 fields)
18. PERSONALIDAD_JURIDICA_COTITULAR (0=Sin Cotitular, 1=Física, 2=Moral)
19. CARACTER_COTITULAR (same catalog as titular)
20. RFC_COTITULAR (same format as titular)
21. RAZON_SOCIAL_COTITULAR
22. NOMBRE_COTITULAR
23. APELLIDO_PATERNO_COTITULAR
24. APELLIDO_MATERNO_COTITULAR

### Section V: Información de la Cuenta (10 fields)
25. CLAVE_SUCURSAL (30 chars)
26. ESTADO_INEGI (5 digits, from catalog)
27. LOCALIDAD_INEGI (14 digits, from catalog)
28. CODIGO_POSTAL (5 digits)
29. MODALIDAD (21=Nómina, 22=Mercado Abierto)
30. TIPO_NIVEL_CUENTA (401-404=Nivel 1-4, 405=Tarjeta Crédito, 406=Inversión)
31. NUMERO_CUENTA (30 chars, CLABE 18 dígitos preferred)
32. PRODUCTO (from catalog: 1-106, e.g. 101=Depósitos vista, 102=Inversión)
33. MONEDA_CUENTA (0=Pesos, 1=Dólares, 2=Otra moneda extranjera)
34. MONTO_INICIAL_ASEGURADO (rounded pesos, actual blocked amount)

### Section VI: Datos de la Operación (8 fields)
35. TIPO_OPERACION (101=Bloqueo, 102=Desbloqueo, 103=Transferencia, 104=Situación fondos)
36. NUMERO_OFICIO_OPERACION (must match field 7 if same operation)
37. FECHA_REQUERIMIENTO_OPERACION (AAAAMMDD)
38. FOLIO_SIARA_OPERACION (when not direct)
39. FECHA_APLICACION (AAAAMMDD, when bank executed)
40. MONTO_OPERACION (rounded pesos, amount requested by authority)
41. MONEDA_OPERACION (same as field 33)
42. SALDO_FINAL (rounded pesos, after operation)

## CRITICAL VALIDATION RULES FROM R29:

1. **NO EMPTY FIELDS**: "Todas las columnas deberán reportarse con dato, por lo que no se aceptarán
   campos vacíos en el envío de la información."

2. **Catalog Exactness**: "Para las columnas que utilizan catálogos el dato es obligatorio y la clave
   debe anotarse exactamente como está presentada en la sección de catálogos del SITI."

3. **Numeric Format**: Sin decimales, sin comas, sin puntos, cifras positivas, redondeo >0.5 sube, <0.5 baja.
   Example: $236,569.68 → 236570

4. **RFC Format Validation**:
   - Física: XXXXAAMMDDXXX (13 chars, if missing homoclave use XXX)
   - Moral: _XXXAAMMDDXXX (underscore prefix + 12 chars)
   - Pattern matching REQUIRED before acceptance

5. **Date Format**: AAAAMMDD (no hyphens, no slashes)

6. **Multiple Titulares**: If >2 titulares or >2 cotitulares, append "-XXX" (001-999) to NUMERO_OFICIO

## SIARA MANUAL INSIGHTS:

### Operation-Specific Requirements

**BLOQUEO (101):**
- MONTO_SOLICITADO (field 10): Authority specifies crédito fiscal amount, or 0 if "toda la cuenta"
- INSTRUCCIONES: Must specify forma de disposición (cheque caja, billete depósito, transferencia)

**DESBLOQUEO (102):**
- Requires ANTECEDENTES: número oficio original + fecha + expediente original
- Must reference FOLIO_SIARA of original bloqueo

**TRANSFERENCIA (103):**
- Requires cuenta destino in INSTRUCCIONES
- Electronic transfer via SPEI

**INFORMACIÓN/DOCUMENTACIÓN:**
- Types requested:
  - Estados de cuenta (simple o certificada)
  - Contrato de apertura
  - Tarjeta de firmas
  - IDs presentadas al aperturar
  - Comprobantes domicilio
  - Poderes notariales
  - Transferencias: nombre titular + cuenta + entidad ORIGEN y DESTINO
  - Fichas de depósito
  - Cheques (anverso y reverso)

### Field-Level Instructions Validation

From SIARA Manual section 3.1.3 "Solicitud específica":
- En caso de exhortos: señalar juzgado exhortante + expediente origen
- Instrucciones must be:
  - Congruent with entity type operations
  - Specific about copy type (simple vs certificada)
  - Include date ranges when applicable
  - Reference supporting documents by specific identifier

───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
🔬 ENHANCED FUSION ALGORITHM WITH OCR CONFIDENCE INTEGRATION
───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

## Phase 1: Input Normalization & Feature Extraction

### Step 1.1: Extract Metadata from Each Source

```csharp
public class ExtractionMetadata
{
    // OCR Quality Metrics (from Tesseract)
    public double? MeanConfidence { get; set; }       // 0.0-1.0, average confidence all words
    public double? MinConfidence { get; set; }        // 0.0-1.0, lowest word confidence
    public int? TotalWords { get; set; }              // Word count
    public int? LowConfidenceWords { get; set; }      // Words < 60% confidence

    // Image Quality Metrics (from preprocessing)
    public double? QualityIndex { get; set; }         // 0.0-1.0, from optimized filters
    public double? BlurScore { get; set; }            // Variance of Laplacian
    public double? ContrastScore { get; set; }        // RMS contrast
    public double? NoiseEstimate { get; set; }        // Estimated noise level
    public double? EdgeDensity { get; set; }          // Edge pixel density

    // Extraction Success Metrics
    public int RegexMatches { get; set; }             // How many fields matched known patterns
    public int TotalFieldsExtracted { get; set; }     // How many non-null fields
    public int CatalogValidations { get; set; }       // How many fields validated against catalogs
    public int PatternViolations { get; set; }        // How many failed pattern validation

    // Source Type
    public SourceType Source { get; set; }            // XML, PDF_OCR, DOCX_OCR
    public DateTime ExtractionTimestamp { get; set; }
}

public enum SourceType
{
    XML_HandFilled,      // Lowest base reliability (0.60)
    PDF_OCR_CNBV,        // Highest base reliability (0.85) - official source
    DOCX_OCR_Authority   // Medium base reliability (0.70) - varies by authority
}
```

### Step 1.2: Calculate Source Reliability Weight (Dynamic)

This is where we integrate OCR confidence and quality metrics to adjust the static priority.

```csharp
public double CalculateSourceReliability(ExtractionMetadata metadata)
{
    // Base reliability (from source type)
    double baseReliability = metadata.Source switch
    {
        SourceType.XML_HandFilled => 0.60,      // Hand-filled, prone to typos
        SourceType.PDF_OCR_CNBV => 0.85,        // Official PDF from CNBV
        SourceType.DOCX_OCR_Authority => 0.70,  // Authority-generated, variable quality
        _ => 0.50
    };

    // If source is XML (no OCR), return base reliability
    if (metadata.Source == SourceType.XML_HandFilled)
    {
        // Adjust based on pattern violations and catalog validations
        double xmlQuality = 1.0 - (metadata.PatternViolations / (double)metadata.TotalFieldsExtracted);
        double catalogAccuracy = metadata.CatalogValidations / (double)metadata.TotalFieldsExtracted;

        return baseReliability * (0.5 * xmlQuality + 0.5 * catalogAccuracy);
    }

    // OCR sources: apply confidence and quality adjustments
    double ocrConfidenceMultiplier = CalculateOCRConfidenceMultiplier(metadata);
    double imageQualityMultiplier = CalculateImageQualityMultiplier(metadata);
    double extractionSuccessMultiplier = CalculateExtractionSuccessMultiplier(metadata);

    // Weighted combination (coefficients to be optimized via GA)
    double w1 = 0.50;  // OCR confidence weight
    double w2 = 0.30;  // Image quality weight
    double w3 = 0.20;  // Extraction success weight

    double adjustedReliability = baseReliability *
        (w1 * ocrConfidenceMultiplier + w2 * imageQualityMultiplier + w3 * extractionSuccessMultiplier);

    return Math.Clamp(adjustedReliability, 0.0, 1.0);
}

private double CalculateOCRConfidenceMultiplier(ExtractionMetadata metadata)
{
    if (!metadata.MeanConfidence.HasValue) return 1.0;

    // Tesseract confidence: penalize heavily if mean < 70%, reward if > 90%
    double meanConf = metadata.MeanConfidence.Value;
    double lowConfRatio = metadata.LowConfidenceWords / (double)metadata.TotalWords;

    // Coefficients to optimize
    double alpha = 1.5;   // Mean confidence exponent
    double beta = -0.8;   // Low confidence penalty weight

    double multiplier = Math.Pow(meanConf, alpha) * (1.0 + beta * lowConfRatio);
    return Math.Clamp(multiplier, 0.3, 1.2);  // Allow 20% boost for excellent OCR
}

private double CalculateImageQualityMultiplier(ExtractionMetadata metadata)
{
    if (!metadata.QualityIndex.HasValue) return 1.0;

    // Quality index from optimized preprocessing pipeline
    double qualityIndex = metadata.QualityIndex.Value;

    // Additional adjustments from individual metrics
    double blurPenalty = metadata.BlurScore < 100 ? 0.8 : 1.0;
    double contrastBoost = metadata.ContrastScore > 50 ? 1.1 : 1.0;

    return qualityIndex * blurPenalty * contrastBoost;
}

private double CalculateExtractionSuccessMultiplier(ExtractionMetadata metadata)
{
    // Regex pattern matches indicate structure preservation
    double patternMatchRate = metadata.RegexMatches / (double)metadata.TotalFieldsExtracted;

    // Catalog validations indicate correct value extraction
    double catalogValidationRate = metadata.CatalogValidations / (double)metadata.TotalFieldsExtracted;

    // Pattern violations indicate OCR errors
    double errorRate = metadata.PatternViolations / (double)metadata.TotalFieldsExtracted;

    return (0.4 * patternMatchRate + 0.4 * catalogValidationRate + 0.2 * (1.0 - errorRate));
}
```

## Phase 2: Field-Level Fusion with Dynamic Weighting

### Step 2.1: For Each Field, Collect All Source Values

```csharp
public class FieldCandidate
{
    public string FieldName { get; set; }
    public string? Value { get; set; }
    public SourceType Source { get; set; }
    public double SourceReliability { get; set; }    // From CalculateSourceReliability()
    public double FieldConfidence { get; set; }      // Tesseract confidence for this specific field
    public bool MatchesPattern { get; set; }         // Did value pass regex validation?
    public bool MatchesCatalog { get; set; }         // Did value match catalog entry?
    public bool IsSanitized { get; set; }            // After cleaning (trim, remove &nbsp;, etc.)
}
```

### Step 2.2: Sanitize and Validate Each Candidate

```csharp
private FieldCandidate SanitizeAndValidate(FieldCandidate raw, string fieldName)
{
    var sanitized = new FieldCandidate
    {
        FieldName = raw.FieldName,
        Source = raw.Source,
        SourceReliability = raw.SourceReliability,
        FieldConfidence = raw.FieldConfidence,
        IsSanitized = true
    };

    // Step 1: Clean value
    string? cleaned = raw.Value
        ?.Trim()
        .Replace("&nbsp;", "")
        .Replace("&amp;nbsp;", "")
        .Replace("\r\n", " ")
        .Replace("\n", " ");

    // Step 2: Detect human annotations (XML source)
    if (cleaned == "NO SE CUENTA" ||
        cleaned == "el monto mencionado en el texto" ||
        cleaned?.All(c => c == ' ') == true)
    {
        cleaned = null;  // Mark as invalid
    }

    // Step 3: Pattern validation
    sanitized.MatchesPattern = ValidatePattern(fieldName, cleaned);

    // Step 4: Catalog validation (if applicable)
    sanitized.MatchesCatalog = ValidateCatalog(fieldName, cleaned);

    // Step 5: Store cleaned value
    sanitized.Value = sanitized.MatchesPattern ? cleaned : null;

    return sanitized;
}

private bool ValidatePattern(string fieldName, string? value)
{
    if (string.IsNullOrWhiteSpace(value)) return false;

    return fieldName switch
    {
        "RFC" => Regex.IsMatch(value, @"^(_)?[A-Z]{3,4}\d{6}[A-Z0-9]{3}$"),
        "CURP" => Regex.IsMatch(value, @"^[A-Z]{4}\d{6}[HM][A-Z]{5}[A-Z0-9]{2}$"),
        "NumeroExpediente" => Regex.IsMatch(value, @"^[A-Z]/[A-Z]{1,2}\d+-\d+-\d+-[A-Z]+$"),
        "NumeroOficio" => value.Length <= 30,
        "CLABE" => Regex.IsMatch(value, @"^\d{18}$"),
        "FechaSolicitud" => Regex.IsMatch(value, @"^\d{8}$") && DateTime.TryParseExact(value, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out _),
        "Monto" => decimal.TryParse(value, out _),
        _ => true  // No specific pattern for this field
    };
}
```

### Step 2.3: Select Best Value Using Optimized Coefficients

```csharp
public async Task<FieldFusionResult> FuseFieldAsync(
    string fieldName,
    List<FieldCandidate> candidates,
    CancellationToken ct)
{
    // Step 1: Remove null/invalid candidates
    var validCandidates = candidates
        .Where(c => c.Value != null && c.MatchesPattern)
        .ToList();

    if (validCandidates.Count == 0)
    {
        return new FieldFusionResult
        {
            Value = null,
            Confidence = 0.0,
            Decision = FusionDecision.AllSourcesNull,
            RequiresManualReview = IsRequiredField(fieldName)
        };
    }

    // Step 2: Check for exact agreement (all sources same value)
    var distinctValues = validCandidates.Select(c => c.Value).Distinct().ToList();
    if (distinctValues.Count == 1)
    {
        double agreementConfidence = validCandidates.Count == 3 ? 0.95 : 0.85;
        return new FieldFusionResult
        {
            Value = distinctValues.First(),
            Confidence = agreementConfidence,
            Decision = FusionDecision.AllAgree,
            ContributingSources = validCandidates.Select(c => c.Source).ToList()
        };
    }

    // Step 3: Try fuzzy matching (for name fields only)
    if (IsFuzzyField(fieldName))
    {
        var fuzzyResult = await TryFuzzyMatch(validCandidates, ct);
        if (fuzzyResult.Success && fuzzyResult.Similarity >= 0.85)
        {
            return new FieldFusionResult
            {
                Value = fuzzyResult.CanonicalValue,
                Confidence = fuzzyResult.Similarity * 0.90,  // Penalty for fuzzy match
                Decision = FusionDecision.FuzzyAgreement,
                FuzzySimilarity = fuzzyResult.Similarity
            };
        }
    }

    // Step 4: Sources disagree - use weighted voting
    var scorecard = validCandidates
        .Select(c => new
        {
            Candidate = c,
            Score = CalculateFieldScore(c, fieldName)
        })
        .OrderByDescending(x => x.Score)
        .ToList();

    var winner = scorecard.First();
    var runnerUp = scorecard.Skip(1).FirstOrDefault();

    // If winner score significantly higher than runner-up, use winner
    if (runnerUp == null || (winner.Score - runnerUp.Score) > 0.15)
    {
        return new FieldFusionResult
        {
            Value = winner.Candidate.Value,
            Confidence = winner.Score,
            Decision = FusionDecision.WeightedVoting,
            WinningSource = winner.Candidate.Source
        };
    }

    // Step 5: Too close to call - manual review for critical fields
    if (IsRequiredField(fieldName) || IsCriticalField(fieldName))
    {
        return new FieldFusionResult
        {
            Value = null,
            Confidence = 0.0,
            Decision = FusionDecision.Conflict,
            RequiresManualReview = true,
            ConflictingValues = validCandidates.Select(c => (c.Source, c.Value)).ToList()
        };
    }

    // Step 6: Non-critical field - use winner but flag for review
    return new FieldFusionResult
    {
        Value = winner.Candidate.Value,
        Confidence = winner.Score * 0.70,  // Penalty for uncertainty
        Decision = FusionDecision.BestEffort,
        SuggestReview = true
    };
}

private double CalculateFieldScore(FieldCandidate candidate, string fieldName)
{
    // Base score from source reliability
    double score = candidate.SourceReliability;

    // Boost from field-specific OCR confidence
    if (candidate.FieldConfidence > 0)
    {
        score *= (0.5 + 0.5 * candidate.FieldConfidence);  // Range: 0.5x - 1.0x
    }

    // Boost from pattern match
    if (candidate.MatchesPattern)
    {
        score *= 1.10;
    }

    // Boost from catalog validation
    if (candidate.MatchesCatalog)
    {
        score *= 1.15;
    }

    // Field-specific adjustments
    if (IsCriticalField(fieldName))
    {
        // For critical fields, heavily weight pattern/catalog matches
        if (!candidate.MatchesPattern) score *= 0.50;
        if (!candidate.MatchesCatalog) score *= 0.60;
    }

    return Math.Clamp(score, 0.0, 1.0);
}
```

## Phase 3: Overall Expediente Confidence & Decision

### Step 3.1: Required Fields by Operation Type

```csharp
private List<string> GetRequiredFields(TipoOperacion operationType)
{
    // Base required fields (always needed)
    var required = new List<string>
    {
        "NumeroExpediente",
        "NumeroOficio",
        "FechaSolicitud",
        "AutoridadNombre",
        "AreaDescripcion",
        "TipoOperacion"
    };

    // Operation-specific requirements (from R29 + SIARA Manual)
    switch (operationType)
    {
        case TipoOperacion.Bloqueo:
            required.AddRange(new[] {
                "RFC",  // Must identify person
                "NumeroCuenta",  // Or at least some account identifier
                "MontoSolicitado",  // Amount to block (or 0 for entire account)
                "Instrucciones"  // How to dispose of blocked amount
            });
            break;

        case TipoOperacion.Desbloqueo:
            required.AddRange(new[] {
                "AntecedentesDocumentales",  // Original blocking reference
                "FolioSiaraOriginal",
                "NumeroOficioOriginal"
            });
            break;

        case TipoOperacion.Transferencia:
            required.AddRange(new[] {
                "RFC",
                "NumeroCuentaOrigen",
                "NumeroCuentaDestino",
                "EntidadDestino",
                "MontoTransferencia"
            });
            break;

        case TipoOperacion.InformacionDocumentacion:
            required.AddRange(new[] {
                "RFC",  // Or CURP, or Nombre completo
                "TipoDocumentoSolicitado",
                "PeriodoInicio",  // If applicable (estados de cuenta)
                "PeriodoFin"
            });
            break;
    }

    return required;
}
```

### Step 3.2: Calculate Overall Expediente Confidence

```csharp
public class OverallFusionResult
{
    public Expediente? FusedExpediente { get; set; }
    public double OverallConfidence { get; set; }
    public double RequiredFieldsScore { get; set; }
    public double OptionalFieldsScore { get; set; }
    public FusionDecision Decision { get; set; }
    public List<string> MissingRequiredFields { get; set; } = new();
    public List<string> ConflictingFields { get; set; } = new();
    public bool RequiresManualReview { get; set; }
    public NextAction RecommendedAction { get; set; }
}

public enum NextAction
{
    AutoProcess,           // Confidence > 0.85, all required fields present
    ReviewRecommended,     // Confidence 0.70-0.85, proceed but flag
    ManualReviewRequired   // Confidence < 0.70 or missing required fields
}

private OverallFusionResult CalculateOverallConfidence(
    Dictionary<string, FieldFusionResult> fieldResults,
    TipoOperacion operationType)
{
    var requiredFields = GetRequiredFields(operationType);
    var allFields = fieldResults.Keys.ToList();
    var optionalFields = allFields.Except(requiredFields).ToList();

    // Required fields score
    var requiredScores = requiredFields
        .Select(f => fieldResults.ContainsKey(f) ? fieldResults[f].Confidence : 0.0)
        .ToList();

    double requiredScore = requiredScores.Count > 0
        ? requiredScores.Average()
        : 0.0;

    var missingRequired = requiredFields
        .Where(f => !fieldResults.ContainsKey(f) || fieldResults[f].Value == null)
        .ToList();

    // Optional fields score
    var optionalScores = optionalFields
        .Where(f => fieldResults.ContainsKey(f))
        .Select(f => fieldResults[f].Confidence)
        .ToList();

    double optionalScore = optionalScores.Count > 0
        ? optionalScores.Average()
        : 0.0;

    // Overall weighted score (70% required, 30% optional)
    double overall = (requiredScore * 0.70) + (optionalScore * 0.30);

    // Conflicting fields
    var conflicts = fieldResults
        .Where(kvp => kvp.Value.Decision == FusionDecision.Conflict)
        .Select(kvp => kvp.Key)
        .ToList();

    // Decision logic
    NextAction action;
    if (missingRequired.Any() || requiredScore < 0.70)
    {
        action = NextAction.ManualReviewRequired;
    }
    else if (overall > 0.85 && conflicts.Count == 0)
    {
        action = NextAction.AutoProcess;
    }
    else
    {
        action = NextAction.ReviewRecommended;
    }

    return new OverallFusionResult
    {
        OverallConfidence = overall,
        RequiredFieldsScore = requiredScore,
        OptionalFieldsScore = optionalScore,
        MissingRequiredFields = missingRequired,
        ConflictingFields = conflicts,
        RequiresManualReview = action == NextAction.ManualReviewRequired,
        RecommendedAction = action
    };
}
```

───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
🧬 COEFFICIENT OPTIMIZATION STRATEGY (from OCR_FILTER_OPTIMIZATION_JOURNAL.md)
───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

## Optimization Parameters (Genes)

```csharp
public class FusionCoefficients
{
    // Source reliability base values
    public double XML_BaseReliability { get; set; } = 0.60;
    public double PDF_BaseReliability { get; set; } = 0.85;
    public double DOCX_BaseReliability { get; set; } = 0.70;

    // Metadata weight distribution
    public double OCR_ConfidenceWeight { get; set; } = 0.50;
    public double ImageQualityWeight { get; set; } = 0.30;
    public double ExtractionSuccessWeight { get; set; } = 0.20;

    // OCR confidence multiplier formula
    public double MeanConfidenceExponent { get; set; } = 1.5;
    public double LowConfidencePenaltyWeight { get; set; } = -0.8;

    // Field score calculation
    public double PatternMatchBoost { get; set; } = 1.10;
    public double CatalogValidationBoost { get; set; } = 1.15;
    public double CriticalFieldPatternPenalty { get; set; } = 0.50;
    public double CriticalFieldCatalogPenalty { get; set; } = 0.60;

    // Fuzzy matching
    public double FuzzyMatchThreshold { get; set; } = 0.85;
    public double FuzzyMatchConfidencePenalty { get; set; } = 0.90;

    // Overall confidence thresholds
    public double RequiredFieldsWeight { get; set; } = 0.70;
    public double OptionalFieldsWeight { get; set; } = 0.30;
    public double AutoProcessThreshold { get; set; } = 0.85;
    public double ManualReviewThreshold { get; set; } = 0.70;
}
```

## Optimization Methodology

### Phase 1: Generate Labeled Dataset

```plaintext
1. Use existing 4 PRP1 XML samples (ground truth known)
2. Generate synthetic degraded PDFs (blur, noise, skew, resolution variations)
3. Generate synthetic degraded DOCX (OCR errors, formatting loss)
4. Use dummy data generator to create additional samples with known variations
5. Target: 100+ labeled Expedientes with known correct values for all 42 R29 fields
```

### Phase 2: Cluster by Input Properties

```csharp
// Cluster samples by measurable properties (NOT by confidence - that's what we're optimizing!)
public class SampleProperties
{
    public double AvgOCRConfidence { get; set; }
    public double ImageQualityIndex { get; set; }
    public double RegexMatchRate { get; set; }
    public int TotalFields { get; set; }
    public SourceType DominantSource { get; set; }
}

// Use K-Means clustering (K=5 to K=10 clusters)
// Each cluster represents a "difficulty level" for fusion
```

### Phase 3: Genetic Algorithm per Cluster

```csharp
// For each cluster, optimize coefficients using GA
public class GeneticAlgorithmConfig
{
    public int PopulationSize { get; set; } = 50;
    public int Generations { get; set; } = 100;
    public double MutationRate { get; set; } = 0.10;
    public double CrossoverRate { get; set; } = 0.70;
    public double ElitismRate { get; set; } = 0.10;
}

// Fitness function: Accuracy of fused Expedientes compared to ground truth
public double CalculateFitness(FusionCoefficients coeffs, List<Expediente> testSamples)
{
    int correctFields = 0;
    int totalFields = 0;

    foreach (var sample in testSamples)
    {
        var fusedExpediente = FuseWithCoefficients(sample, coeffs);

        // Compare each field to ground truth
        foreach (var field in GetR29Fields())
        {
            totalFields++;
            if (fusedExpediente.GetField(field) == sample.GroundTruth.GetField(field))
            {
                correctFields++;
            }
        }
    }

    return correctFields / (double)totalFields;  // Accuracy 0.0-1.0
}
```

### Phase 4: Polynomial Regression Across Clusters

```csharp
// After GA optimization per cluster, fit polynomial model
// Inputs: SampleProperties (continuous variables)
// Outputs: Optimized coefficient values (continuous)
// Model: 2nd or 3rd degree polynomial

// Example for XML_BaseReliability:
// XML_BaseReliability =
//   β0 + β1*OCRConf + β2*QualityIdx + β3*RegexMatchRate +
//   β4*OCRConf² + β5*QualityIdx² + β6*OCRConf*QualityIdx

// This allows interpolation for new samples with properties between cluster centroids
```

### Phase 5: Validation on Held-Out Test Set

```plaintext
1. Reserve 20% of labeled data for final validation
2. Apply optimized coefficients (via polynomial model)
3. Measure:
   - Field-level accuracy (% fields correctly fused)
   - Expediente-level accuracy (% Expedientes fully correct)
   - Precision/Recall for manual review flagging
   - False positive rate (flagged for review but actually correct)
   - False negative rate (auto-processed but actually wrong)
4. Target metrics:
   - Field accuracy > 95%
   - Expediente accuracy > 90%
   - False negative rate < 2% (CRITICAL - cannot auto-process bad data)
   - False positive rate < 15% (acceptable - better safe than sorry)
```

───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
📦 VALUE OBJECT DEFINITIONS
───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

```csharp
// Domain/ValueObjects/FusionResult.cs
public class FusionResult
{
    /// <summary>
    /// The fused Expediente (may be null if all sources failed)
    /// </summary>
    public Expediente? FusedExpediente { get; set; }

    /// <summary>
    /// Overall confidence score 0.0-1.0
    /// </summary>
    public double OverallConfidence { get; set; }

    /// <summary>
    /// Confidence score for required fields only
    /// </summary>
    public double RequiredFieldsScore { get; set; }

    /// <summary>
    /// Confidence score for optional fields
    /// </summary>
    public double OptionalFieldsScore { get; set; }

    /// <summary>
    /// Fields that could not be fused (conflicting sources)
    /// </summary>
    public List<string> ConflictingFields { get; set; } = new();

    /// <summary>
    /// Required fields that are missing (null or all sources null)
    /// </summary>
    public List<string> MissingRequiredFields { get; set; } = new();

    /// <summary>
    /// Recommended next action
    /// </summary>
    public NextAction NextAction { get; set; }

    /// <summary>
    /// Detailed per-field fusion results
    /// </summary>
    public Dictionary<string, FieldFusionResult> FieldResults { get; set; } = new();

    /// <summary>
    /// Source reliability scores
    /// </summary>
    public Dictionary<SourceType, double> SourceReliabilities { get; set; } = new();

    /// <summary>
    /// Validation state
    /// </summary>
    public ValidationState Validation { get; } = new();
}

public enum NextAction
{
    AutoProcess,           // All good, send to classification
    ReviewRecommended,     // Proceed but flag for spot-check
    ManualReviewRequired   // Must review before processing
}

// Domain/ValueObjects/FieldFusionResult.cs
public class FieldFusionResult
{
    public string? Value { get; set; }
    public double Confidence { get; set; }
    public FusionDecision Decision { get; set; }
    public List<SourceType> ContributingSources { get; set; } = new();
    public SourceType? WinningSource { get; set; }
    public double? FuzzySimilarity { get; set; }
    public bool RequiresManualReview { get; set; }
    public bool SuggestReview { get; set; }
    public List<(SourceType Source, string? Value)> ConflictingValues { get; set; } = new();
}

public enum FusionDecision
{
    AllAgree,              // All sources had same value
    FuzzyAgreement,        // Sources similar via fuzzy match
    WeightedVoting,        // Sources disagreed, used highest weighted source
    BestEffort,            // Weak winner, flagged for review
    Conflict,              // Cannot reconcile, manual review required
    AllSourcesNull         // No source had valid value
}

// Domain/ValueObjects/ClassificationResult.cs
public class ClassificationResult
{
    /// <summary>
    /// Type of requirement (100-104)
    /// 100 = Información/Documentación
    /// 101 = Aseguramiento (Bloqueo)
    /// 102 = Desbloqueo
    /// 103 = Transferencia
    /// 104 = Situación de fondos
    /// </summary>
    public TipoRequerimiento RequirementType { get; set; }

    /// <summary>
    /// Confidence in classification 0.0-1.0
    /// </summary>
    public double ClassificationConfidence { get; set; }

    /// <summary>
    /// Authority type (determines which fields are required)
    /// </summary>
    public TipoAutoridad AuthorityType { get; set; }

    /// <summary>
    /// Required fields for this specific classification
    /// </summary>
    public List<string> RequiredFields { get; set; } = new();

    /// <summary>
    /// Validation results against legal requirements
    /// </summary>
    public ArticleValidationResult ArticleValidation { get; set; }

    /// <summary>
    /// Semantic analysis (the 5 situations)
    /// </summary>
    public SemanticAnalysis SemanticAnalysis { get; set; }

    /// <summary>
    /// Rejection reasons (if Article 17 applies)
    /// </summary>
    public List<RejectionReason> RejectionReasons { get; set; } = new();

    /// <summary>
    /// Validation state
    /// </summary>
    public ValidationState Validation { get; } = new();
}

public enum TipoRequerimiento
{
    InformacionDocumentacion = 100,
    Aseguramiento = 101,
    Desbloqueo = 102,
    Transferencia = 103,
    SituacionFondos = 104
}

public class ArticleValidationResult
{
    public bool IsValid { get; set; }
    public bool HasAuthentication { get; set; }     // Letterhead, signature
    public bool HasLegalFoundation { get; set; }    // Article citation
    public bool HasCompetentAuthority { get; set; } // From catalog
    public bool HasRequiredFields { get; set; }     // Article 4 compliance
    public List<string> MissingFields { get; set; } = new();
    public List<string> ViolatedArticles { get; set; } = new();
}

public class RejectionReason
{
    public string Article { get; set; }  // e.g., "Artículo 17 Fracción I"
    public string Description { get; set; }
    public RejectionSeverity Severity { get; set; }
}

public enum RejectionSeverity
{
    Minor,      // Can be corrected
    Major,      // Requires resubmission
    Fatal       // Cannot be processed
}
```

───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
🔌 COMPLETE INTERFACE SIGNATURES
───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

```csharp
// Domain/Interfaces/IFusionExpediente.cs
namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Fuses data from multiple unreliable sources (XML, PDF OCR, DOCX OCR) into a single
/// reconciled Expediente with confidence scoring.
/// </summary>
public interface IFusionExpediente
{
    /// <summary>
    /// Fuses data from up to 3 sources into a reconciled Expediente.
    /// Uses dynamic source reliability weighting based on OCR confidence, image quality,
    /// and extraction success metrics.
    /// </summary>
    /// <param name="xmlExpediente">Expediente from XML source (hand-filled, may have errors)</param>
    /// <param name="pdfExpediente">Expediente from PDF OCR (CNBV official source)</param>
    /// <param name="docxExpediente">Expediente from DOCX OCR (authority source, variable quality)</param>
    /// <param name="xmlMetadata">Extraction metadata for XML (pattern matches, catalog validations)</param>
    /// <param name="pdfMetadata">OCR metadata for PDF (Tesseract confidence, quality index)</param>
    /// <param name="docxMetadata">OCR metadata for DOCX (Tesseract confidence, quality index)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// FusionResult containing:
    /// - Fused Expediente (may be null if all sources failed)
    /// - Overall confidence score (0.0-1.0)
    /// - Required fields score
    /// - Optional fields score
    /// - Conflicting fields list
    /// - Missing required fields list
    /// - NextAction recommendation (AutoProcess/ReviewRecommended/ManualRequired)
    /// - Per-field fusion details
    /// </returns>
    Task<FusionResult> FuseAsync(
        Expediente? xmlExpediente,
        Expediente? pdfExpediente,
        Expediente? docxExpediente,
        ExtractionMetadata xmlMetadata,
        ExtractionMetadata pdfMetadata,
        ExtractionMetadata docxMetadata,
        CancellationToken cancellationToken);

    /// <summary>
    /// Fuses a single field from multiple source values using weighted voting.
    /// Accounts for source reliability, OCR confidence, pattern validation, and catalog matching.
    /// </summary>
    /// <param name="fieldName">Name of field to fuse</param>
    /// <param name="candidates">Candidate values from different sources</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Field fusion result with selected value and confidence</returns>
    Task<FieldFusionResult> FuseFieldAsync(
        string fieldName,
        List<FieldCandidate> candidates,
        CancellationToken cancellationToken);
}

// Domain/Interfaces/IExpedienteClasifier.cs
namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Classifies Expediente into requirement type and validates against legal requirements.
/// Determines which fields are required based on classification and performs Article validation.
/// </summary>
public interface IExpedienteClasifier
{
    /// <summary>
    /// Classifies Expediente into one of 5 requirement types (100-104) and validates
    /// against CNBV legal requirements (Articles 4, 17, 142 LIC, 34 LACP).
    /// </summary>
    /// <param name="expediente">Expediente to classify (post-fusion)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// ClassificationResult containing:
    /// - RequirementType (100-104)
    /// - ClassificationConfidence (0.0-1.0)
    /// - AuthorityType
    /// - RequiredFields for this classification
    /// - ArticleValidation results
    /// - SemanticAnalysis (the 5 situations)
    /// - RejectionReasons if invalid
    /// </returns>
    Task<ClassificationResult> ClassifyAsync(
        Expediente expediente,
        CancellationToken cancellationToken);

    /// <summary>
    /// Validates Expediente against Article 4 requirements (required fields per type).
    /// </summary>
    Task<ArticleValidationResult> ValidateArticle4Async(
        Expediente expediente,
        TipoRequerimiento requirementType,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks if Expediente should be rejected per Article 17 (6 rejection grounds).
    /// </summary>
    Task<List<RejectionReason>> CheckArticle17RejectionAsync(
        Expediente expediente,
        CancellationToken cancellationToken);

    /// <summary>
    /// Performs semantic analysis to determine the "5 situations":
    /// 1. Bloqueo
    /// 2. Desbloqueo
    /// 3. Documentación
    /// 4. Transferencia
    /// 5. Información General
    /// </summary>
    Task<SemanticAnalysis> AnalyzeSemanticRequirementsAsync(
        Expediente expediente,
        CancellationToken cancellationToken);
}
```

───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
✅ PROPOSAL COMPLETE - NEXT STEPS
───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

## Summary

This proposal integrates:

1. **R29 42 Mandatory Fields**: All fusion logic now aware of R29 reporting requirements
2. **SIARA Operation Types**: Classification determines which fields are required
3. **OCR Confidence Integration**: Tesseract confidence + image quality → dynamic source reliability
4. **Optimization Methodology**: GA + polynomial regression (proven 18.4% improvement on OCR filters)
5. **Complete Interface Signatures**: IFusionExpediente + IExpedienteClasifier with full documentation
6. **Value Objects**: FusionResult, FieldFusionResult, ClassificationResult, ArticleValidationResult
7. **High Cohesion Architecture**: Both services in Infrastructure.Classification project

## Ready for Implementation

Tasks remaining:
1. Create IFusionExpediente and IExpedienteClasifier interfaces in Domain
2. Create value objects (FusionResult, ClassificationResult, etc.)
3. Write ITDD contract tests
4. Implement FusionExpedienteService and ExpedienteClasifierService
5. Generate labeled dataset for coefficient optimization
6. Run genetic algorithm optimization per cluster
7. Fit polynomial regression model
8. Validate on held-out test set

**Estimated effort**:
- Interface + value objects + tests: 4-6 hours
- Implementation (fusion + classification): 12-16 hours
- Dataset generation: 3-4 hours
- Optimization experiments: 6-8 hours
- **Total: 25-34 hours**

Let me know and I'll start implementing! 🚀
