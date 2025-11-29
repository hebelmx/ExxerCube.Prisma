# REVISED SCOPE: Classification Rules Analysis
**Updated**: 2025-11-29
**Context**: Prisma is a document processing system, NOT a banking execution system

---

## SCOPE CLARIFICATION

### ✅ YOUR RESPONSIBILITY (IN SCOPE)

**Document Processing Pipeline**:
1. **Download** documents from SIARA/email/physical scan
2. **OCR extraction** (Tesseract + GOT-OCR2)
3. **Classify requirement type** (100, 101, 102, 103, 104, 999)
4. **Extract key fields**:
   - NumeroRequerimiento (Request Number)
   - FechaEmision (Issue Date)
   - AutoridadRequiriente (Requesting Authority)
   - TipoRequerimiento (Requirement Type)
   - Subject data (Name, RFC, Account)
   - Amounts, dates, details
5. **Generate reports** for bank to act on
6. **Audit trail** of all processing
7. **Flag issues** for human review (low confidence, missing fields, etc.)

**Your System's Output**:
- Structured data export (XML/Excel/PDF)
- Classification confidence scores
- Extracted field values
- Validation warnings/flags

---

### ❌ NOT YOUR RESPONSIBILITY (OUT OF SCOPE)

**Bank Legal Department's Responsibility**:
- Authenticate document validity (letterhead, signature)
- Validate authority has legal standing
- Validate authority competence per Article 142 LIC
- **REJECT** invalid requests per Article 17
- Final legal review before bank acts

**Bank Operations Team's Responsibility**:
- Actually **freeze/unfreeze** accounts
- Actually **execute transfers**
- Actually **issue cashier's checks**
- Access to bank core systems
- Multi-person authorization workflows (e.g., 3 people to approve freeze)

**CNBV's Responsibility**:
- Validate SIARA channel authenticity
- Format validation (TIFF/PDF specs per Anexo 1)
- Business hours enforcement

---

## REVISED GAP ANALYSIS

### ✅ ALREADY IMPLEMENTED (SUFFICIENT FOR YOUR SCOPE)

**Classification by Type** (Level 4):
- ✅ Basic keyword matching in `LegalDirectiveClassifierService`
- ✅ Types: Block, Unblock, Document, Transfer, Information
- ✅ Confidence scoring
- ⚠️ **Minor Issue**: Enum naming (`Judicial` should be `InformationRequest`)

**Field Extraction**:
- ✅ Account numbers
- ✅ Amounts (with sophisticated multi-pattern matching)
- ✅ Product types

**Classification Output**:
- ✅ Confidence scores
- ✅ Multiple action detection (hybrid requests)
- ✅ Extracted details (account, amount, product type)

---

### ⚠️ ACTUALLY NEEDED IMPROVEMENTS (IN YOUR SCOPE)

#### 1. Fix RequirementType Enum Naming ⭐ CRITICAL

**Current** (Confusing):
```csharp
public static readonly RequirementType Judicial = new(100, "Judicial", "Solicitud de Información");
```

**Problem**: "Judicial" is an authority TYPE, not a requirement type
- Semantically wrong
- Confusing for developers
- Doesn't match CNBV documentation

**Fix** (30 minutes):
```csharp
// Requirement TYPES (what to do)
public static readonly RequirementType InformationRequest = new(100, "InformationRequest", "Solicitud de Información");
public static readonly RequirementType Aseguramiento = new(101, "Aseguramiento", "Aseguramiento/Bloqueo");
public static readonly RequirementType Desbloqueo = new(102, "Desbloqueo", "Desbloqueo");
public static readonly RequirementType TransferenciaElectronica = new(103, "TransferenciaElectronica", "Transferencia Electrónica");
public static readonly RequirementType SituacionFondos = new(104, "SituacionFondos", "Situación de Fondos");
public static readonly RequirementType Unknown = new(999, "Unknown", "Desconocido");
```

**Effort**: 30 min (rename + update all references)
**Impact**: Semantic clarity, matches CNBV docs

---

#### 2. Add Oficio de Seguimiento Detection (NEW Dec 2021)

**What It Is**: Status inquiry requests (Article 2(IV))
- Authority asks: "What's the status of my previous request?"
- Keywords: "estado que guarda", "seguimiento del requerimiento", "estatus de"
- **Important**: NOT a new requirement, NOT included in R29 reports

**Implementation** (1 hour):
```csharp
// In LegalDirectiveClassifierService.cs
private static readonly string[] SeguimientoKeywords =
    { "ESTADO QUE GUARDA", "SEGUIMIENTO", "ESTATUS DEL REQUERIMIENTO", "ESTATUS DE" };

private static bool ContainsSeguimientoDirective(string text) =>
    SeguimientoKeywords.Any(keyword => text.Contains(keyword)) &&
    (text.Contains("NÚMERO DE OFICIO") || text.Contains("FOLIO") || text.Contains("ANTERIOR"));
```

**Output**: Flag as "Status Inquiry - No Action Required" in classification

**Effort**: 1 hour
**Impact**: Avoid creating unnecessary R29 records for status inquiries

---

#### 3. Improve Classification Precedence Rules

**Current Issue**: Simple keyword matching without precedence
**Problem**: Document saying "desbloquear el aseguramiento anterior" might match BOTH aseguramiento and desbloqueo

**Fix** (1-2 hours):
```csharp
public Task<Result<RequirementType>> ClassifyRequirementTypeAsync(string documentText)
{
    var text = documentText.ToUpperInvariant();

    // PRIORITY 1: Oficio de Seguimiento (not a requirement)
    if (ContainsSeguimientoDirective(text))
        return Task.FromResult(Result.Success(RequirementType.OficioSeguimiento));

    // PRIORITY 2: Desbloqueo takes precedence over Aseguramiento
    if (ContainsUnblockDirective(text))
        return Task.FromResult(Result.Success(RequirementType.Desbloqueo));

    // PRIORITY 3: Specific operations
    if (ContainsBlockDirective(text))
        return Task.FromResult(Result.Success(RequirementType.Aseguramiento));

    if (ContainsTransferDirective(text) && ContainsCLABE(text))
        return Task.FromResult(Result.Success(RequirementType.TransferenciaElectronica));

    if (ContainsCashiersCheckDirective(text))
        return Task.FromResult(Result.Success(RequirementType.SituacionFondos));

    // PRIORITY 4: Information request (default for valid requests)
    if (ContainsInformationDirective(text))
        return Task.FromResult(Result.Success(RequirementType.InformationRequest));

    // FALLBACK: Unknown - flag for manual review
    return Task.FromResult(Result.Success(RequirementType.Unknown));
}
```

**Effort**: 1-2 hours
**Impact**: More accurate classification

---

#### 4. Add CLABE Detection for Transferencia

**What**: Transferencia Electrónica requires CLABE (18-digit bank account code)
**Your Job**: Extract and flag if missing

**Implementation** (1 hour):
```csharp
private static bool ContainsCLABE(string text)
{
    // CLABE is exactly 18 digits
    var clabePattern = new Regex(@"\b\d{18}\b");
    return clabePattern.IsMatch(text);
}

private static string? ExtractCLABE(string text)
{
    var clabePattern = new Regex(@"\b(\d{18})\b");
    var match = clabePattern.Match(text);
    return match.Success ? match.Groups[1].Value : null;
}
```

**Output**:
- If Transferencia detected but no CLABE → Flag: "Missing CLABE - Human Review Required"
- If CLABE found → Extract and include in report

**Effort**: 1 hour
**Impact**: Flag incomplete transfer requests

---

#### 5. Add Prior Order Reference for Desbloqueo

**What**: Desbloqueo (unblocking) must reference the original blocking order
**Your Job**: Extract reference if present, flag if missing

**Implementation** (1 hour):
```csharp
private static string? ExtractPriorOrderReference(string text)
{
    // Pattern: "oficio número ABC/123/2023" or "folio ABC/123/2023"
    var patterns = new[]
    {
        new Regex(@"oficio\s+n[uú]mero\s+([A-Z0-9\/\-]+)", RegexOptions.IgnoreCase),
        new Regex(@"folio\s+([A-Z0-9\/\-]+)", RegexOptions.IgnoreCase),
        new Regex(@"aseguramiento\s+([A-Z0-9\/\-]+)", RegexOptions.IgnoreCase)
    };

    foreach (var pattern in patterns)
    {
        var match = pattern.Match(text);
        if (match.Success)
            return match.Groups[1].Value;
    }

    return null;
}
```

**Output**:
- If Desbloqueo detected but no prior order → Flag: "Missing Prior Order Reference - Human Review Required"
- If reference found → Extract and include in report

**Effort**: 1 hour
**Impact**: Flag incomplete unblocking requests

---

#### 6. Add Special Scenario Detection (Recordatorio/Alcance/Precisión)

**What These Are** (Article 5):
- **Recordatorio**: Reminder of previous request (doesn't create new record)
- **Alcance**: Scope expansion (creates new record, links to original)
- **Precisión**: Clarification (updates existing record)

**Your Job**: Detect and flag for special handling

**Implementation** (2 hours):
```csharp
public enum SpecialScenario
{
    None,
    Recordatorio,  // Reminder
    Alcance,       // Scope expansion
    Precision      // Clarification
}

public SpecialScenario DetectSpecialScenario(string text)
{
    var upperText = text.ToUpperInvariant();

    if (upperText.Contains("RECORDATORIO DEL OFICIO"))
        return SpecialScenario.Recordatorio;

    if (upperText.Contains("ALCANCE AL OFICIO") || upperText.Contains("AMPLÍA INFORMACIÓN"))
        return SpecialScenario.Alcance;

    if (upperText.Contains("PRECISIÓN") || upperText.Contains("ACLARA") || upperText.Contains("CORRIGE"))
        return SpecialScenario.Precision;

    return SpecialScenario.None;
}
```

**Output**:
- Add `SpecialScenario` field to classification result
- Bank can handle accordingly (don't duplicate Recordatorio, link Alcance, update Precisión)

**Effort**: 2 hours
**Impact**: Proper handling of follow-up documents

---

#### 7. Add Authority Type Classification (For Reporting)

**What**: Classify authority into categories (Judicial, Fiscal, Ministerial, Administrative, Amparo)
**Your Job**: Pattern matching on authority name for reporting purposes

**Implementation** (2 hours):
```csharp
public enum AuthorityType
{
    Unknown,
    Judicial,       // Juzgados, Tribunales
    Fiscal,         // SAT, FGR fiscal, SHCP
    Ministerial,    // Fiscalías, Ministerio Público
    Administrative, // UIF, CONDUSEF
    Amparo          // Juzgados de Distrito en Materia de Amparo
}

public AuthorityType ClassifyAuthorityType(string authorityName)
{
    var upper = authorityName.ToUpperInvariant();

    if (upper.Contains("JUZGADO") && upper.Contains("AMPARO"))
        return AuthorityType.Amparo;

    if (upper.Contains("JUZGADO") || upper.Contains("TRIBUNAL"))
        return AuthorityType.Judicial;

    if (upper.Contains("SAT") || upper.Contains("FGR") || upper.Contains("SHCP"))
        return AuthorityType.Fiscal;

    if (upper.Contains("FISCALÍA") || upper.Contains("MINISTERIO PÚBLICO"))
        return AuthorityType.Ministerial;

    if (upper.Contains("UIF") || upper.Contains("CONDUSEF"))
        return AuthorityType.Administrative;

    return AuthorityType.Unknown;
}
```

**Output**: Add `AuthorityType` to classification for better reporting/statistics

**Effort**: 2 hours
**Impact**: Better categorization for analytics

---

### ❌ NOT NEEDED (OUT OF YOUR SCOPE)

~~Level 1: Document Authenticity~~ → Bank legal department validates
~~Level 2: Notification Channel~~ → CNBV/bank validates
~~Level 3: Authority Competence Validation~~ → Bank legal department
~~Level 6: Reject Invalid Requests~~ → Bank legal department decides
~~Format Validation (TIFF/PDF specs)~~ → CNBV handles at ingestion
~~Multi-person authorization~~ → Bank internal controls

**Your job**: Process and classify. **Bank's job**: Validate and execute.

---

## REVISED PRIORITY GAPS (REALISTIC SCOPE)

### 🔥 CRITICAL (Must Fix for Clarity)

**Gap 9A: Fix RequirementType Enum Naming** (30 min) ⭐
- Rename `Judicial` → `InformationRequest`
- Update all references
- **Why**: Semantic correctness, matches CNBV docs

### ⚡ HIGH VALUE (Should Add for Accuracy)

**Gap 9B: Classification Precedence Rules** (1-2 hours)
- Desbloqueo takes precedence over Aseguramiento
- Oficio de Seguimiento detection
- **Why**: Avoid misclassification

**Gap 9C: Missing Field Flagging** (2-3 hours)
- CLABE for Transferencia
- Prior order for Desbloqueo
- **Why**: Flag incomplete requests for human review

### 📊 NICE-TO-HAVE (Improves Reporting)

**Gap 9D: Special Scenario Detection** (2 hours)
- Recordatorio/Alcance/Precisión
- **Why**: Proper document lifecycle handling

**Gap 9E: Authority Type Classification** (2 hours)
- Judicial/Fiscal/Ministerial/Administrative/Amparo
- **Why**: Better analytics and reporting

---

## REVISED IMPLEMENTATION PLAN

### Option 1: MINIMUM FIX (30 min) - Recommended for Demo
**What**: Fix enum naming only
**Effort**: 30 min
**Result**: Semantically correct, demo-ready

### Option 2: CLASSIFICATION IMPROVEMENTS (4-6 hours) - Best Value
**What**:
- Fix enum naming (30 min)
- Add precedence rules (1-2 hours)
- Add missing field flagging (2-3 hours)

**Effort**: 4-6 hours
**Result**: Production-quality classification within your scope

### Option 3: FULL CLASSIFICATION SUITE (8-10 hours) - Complete
**What**: All improvements (9A through 9E)
**Effort**: 8-10 hours (1 day)
**Result**: Comprehensive classification for all scenarios

---

## DEMO TALKING POINTS (REVISED)

**When stakeholders ask about classification**:

> "Our system handles document processing and classification - downloading from SIARA, OCR extraction, requirement type classification, and field extraction. We classify into the 5 CNBV requirement types (Information, Aseguramiento, Desbloqueo, Transferencia, Situación de Fondos) based on keyword pattern matching and extract key fields like request numbers, authorities, RFCs, amounts, and account details.
>
> We flag issues for human review - missing CLABEs on transfer requests, missing prior order references on unblocking requests, low OCR confidence, etc. The bank's legal department performs the final validation of authority competence and document authenticity before acting on our classification.
>
> This separation of concerns is by design: we're the intelligent document processor, the bank is the authorized executor. This keeps our system scope-focused and the bank's risk controls intact."

**If they ask about validation**:

> "We don't reject documents - that's the bank's legal department's job. We classify, extract, flag issues, and provide confidence scores. Think of us as the 'smart assistant' that reads all the documents and prepares the information for the bank's decision-makers."

---

## UPDATED TOTAL GAP COUNT

**From Previous Analysis**:
1. Historical Search (Step 5) - 2-3 days
2. Document Organization (Step 2) - 1-2 days
3. Error Handling Fixtures (Step 3) - 3-4 hours
4. Real-Time Reporting UI (Step 4) - 4-6 hours
5. Fix 30 Test Failures - 1-2 days
6. ROI Calculator - 2-3 hours
7. Stakeholder Dashboard - 3-4 hours
8. Demo Script - 1-2 hours

**NEW Classification Gaps** (Revised to Realistic Scope):
9A. Fix enum naming - 30 min ⭐ CRITICAL
9B. Classification precedence - 1-2 hours (High Value)
9C. Missing field flagging - 2-3 hours (High Value)
9D. Special scenario detection - 2 hours (Nice-to-have)
9E. Authority type classification - 2 hours (Nice-to-have)

**Total NEW Gap Time**:
- **Minimum**: 30 min (9A only)
- **Recommended**: 4-6 hours (9A + 9B + 9C)
- **Complete**: 8-10 hours (all 9A-9E)

---

## MY REVISED RECOMMENDATION

### For Demo This Week:
**Do Gap 9A** (30 min) - Fix the enum naming
- Quick
- Eliminates semantic confusion
- Shows you understand the domain

**Optionally Add 9B + 9C** (4-6 hours) if you have time:
- Significantly improves classification accuracy
- Shows production thinking
- Still within reasonable MVP scope

### For P1:
**Complete 9D + 9E** (4 hours):
- Special scenario handling
- Authority type classification
- Better reporting and analytics

---

## BOTTOM LINE

**Good news**: You're NOT responsible for the complex legal validation and execution parts. That's the bank's job.

**Your scope**: Document processing, classification, field extraction, issue flagging.

**Current status**: 80-90% complete for YOUR scope (not the 50% I initially thought)

**Quick fix**: 30 minutes to fix the enum naming bug

**Better fix**: 4-6 hours to add precedence rules + missing field detection

**You're in MUCH better shape than I initially assessed!**
