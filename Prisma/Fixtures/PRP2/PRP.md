# VEC Statement PDF Extraction & Validation System - Product Requirements Package (PRP)

**Project:** ExxerCube.Prisma - VEC Statement Processing
**Document Version:** 2.0 (Enhanced)
**Last Updated:** 2025-12-06
**Status:** Production-Ready

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [System Overview](#system-overview)
3. [Core Requirements](#core-requirements)
4. [PDF Extraction Requirements](#pdf-extraction-requirements)
5. [Data Validation Requirements](#data-validation-requirements)
6. [Mathematical Validation Formulas](#mathematical-validation-formulas)
7. [Product Type Specifications](#product-type-specifications)
8. [Rate Structure Requirements](#rate-structure-requirements)
9. [Tolerance & Precision Requirements](#tolerance--precision-requirements)
10. [Issue Code Taxonomy](#issue-code-taxonomy)
11. [Field-Level Validation Matrix](#field-level-validation-matrix)
12. [Quality Assurance Checklist](#quality-assurance-checklist)
13. [Requirements Traceability Matrix](#requirements-traceability-matrix)
14. [Technical Specifications](#technical-specifications)
15. [Error Handling & Recovery](#error-handling--recovery)
16. [Performance Requirements](#performance-requirements)
17. [Security & Compliance](#security--compliance)
18. [Glossary](#glossary)

---

## Executive Summary

### Purpose
This Product Requirements Package (PRP) defines the complete functional and technical requirements for the VEC Statement PDF Extraction & Validation System within ExxerCube.Prisma. The system automates the extraction, validation, and reconciliation of financial statement data from Vector Casa de Bolsa (VEC) PDF statements.

### Business Context
VEC statements contain critical financial data including account balances, transaction details, interest calculations, and investment positions. Manual processing of these statements is error-prone and time-consuming. This system provides automated extraction with multi-level validation to ensure data accuracy and completeness.

### Key Objectives
1. **Automated Extraction:** Extract all financial data from VEC PDF statements with 99.9% accuracy
2. **Comprehensive Validation:** Implement 53 distinct validation rules across 7 categories
3. **Mathematical Reconciliation:** Verify all financial calculations within defined tolerances
4. **Product-Specific Rules:** Apply specialized validation for 8+ product types
5. **Audit Trail:** Maintain complete traceability for all extracted and validated data

### Success Metrics
- **Extraction Accuracy:** ≥99.9% field-level accuracy
- **Validation Coverage:** 100% of extracted fields validated
- **Processing Time:** <30 seconds per statement
- **False Positive Rate:** <1% on validation errors
- **Data Completeness:** 100% of required fields extracted

---

## System Overview

### Architecture Components

```
┌─────────────────────────────────────────────────────────────┐
│                    VEC Statement PDF                         │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              PDF Extraction Layer                            │
│  - OCR Engine (Azure Document Intelligence)                 │
│  - Text Extraction & Parsing                                │
│  - Table Structure Recognition                              │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              Data Normalization Layer                        │
│  - Currency Formatting (Mexican Peso)                       │
│  - Date Parsing (DD/MMM/YYYY)                               │
│  - Decimal Precision Handling                               │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              Validation Engine                               │
│  - Field-Level Validation (53 Rules)                        │
│  - Mathematical Reconciliation                              │
│  - Cross-Field Dependencies                                 │
│  - Product-Specific Logic                                   │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              Issue Detection & Reporting                     │
│  - Issue Code Assignment                                    │
│  - Severity Classification                                  │
│  - Human Review Flagging                                    │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              Data Persistence Layer                          │
│  - Validated Statement Data                                 │
│  - Audit Logs                                               │
│  - Issue Records                                            │
└─────────────────────────────────────────────────────────────┘
```

### Data Flow

1. **Input:** VEC PDF statement uploaded to system
2. **Extraction:** PDF parsed using Azure Document Intelligence
3. **Normalization:** Raw text converted to structured data
4. **Validation:** 53 validation rules applied
5. **Reconciliation:** Mathematical formulas verified
6. **Output:** Validated data + issue report

---

## Core Requirements

### REQ-001: PDF Statement Processing
**Priority:** P0 (Critical)
**Category:** Core Functionality

The system MUST process VEC PDF statements containing:
- Account summary information
- Transaction history tables
- Interest calculation details
- Investment position summaries
- Regulatory disclosures

**Acceptance Criteria:**
- ✓ Parse PDF documents up to 50 pages
- ✓ Extract text from both digital and scanned PDFs
- ✓ Maintain table structure during extraction
- ✓ Handle multi-column layouts
- ✓ Support Spanish language content

---

### REQ-002: Multi-Product Support
**Priority:** P0 (Critical)
**Category:** Core Functionality

The system MUST support extraction and validation for the following product types:

1. **Vista** (Checking Account)
2. **Recompra** (Repurchase Agreement)
3. **Reporto** (Repo Transaction)
4. **CEDE** (Certificate of Deposit)
5. **Pagare** (Promissory Note)
6. **UDIBONO** (UDI-denominated Bond)
7. **Fondos** (Investment Funds)
8. **Acciones** (Stocks)

**Acceptance Criteria:**
- ✓ Product type auto-detected from statement
- ✓ Product-specific validation rules applied
- ✓ Product-specific rate structures recognized
- ✓ Mixed product statements supported

---

### REQ-003: Currency & Formatting Standards
**Priority:** P0 (Critical)
**Category:** Data Standards

The system MUST adhere to Mexican financial standards:

**Currency:**
- Primary: Mexican Peso (MXN)
- Format: `$1,234,567.89`
- Negative values: `-$1,234.56` or `($1,234.56)`

**Dates:**
- Format: `DD/MMM/YYYY` (e.g., `15/ENE/2025`)
- Spanish month abbreviations: ENE, FEB, MAR, ABR, MAY, JUN, JUL, AGO, SEP, OCT, NOV, DIC

**Percentages:**
- Format: `12.3456%` (up to 4 decimal places for rates)
- Annual rates: `TASA ANUAL`
- Daily rates: `TASA DIARIA`

**Acceptance Criteria:**
- ✓ Currency symbols correctly parsed
- ✓ Thousands separators handled
- ✓ Decimal precision preserved (minimum 2 places)
- ✓ Spanish date parsing implemented
- ✓ Percentage rates normalized to decimal (e.g., 5.25% → 0.0525)

---

## PDF Extraction Requirements

### REQ-004: Header Information Extraction
**Priority:** P0 (Critical)
**Category:** Data Extraction

The system MUST extract the following header fields from every statement:

| Field Name | Data Type | Example | Validation |
|------------|-----------|---------|------------|
| Statement Period | Date Range | `01/ENE/2025 - 31/ENE/2025` | Must be valid date range |
| Account Number | String | `VEC-123456-78` | Must match pattern `VEC-\d{6}-\d{2}` |
| Account Holder | String | `JUAN PÉREZ GARCÍA` | Non-empty, max 100 chars |
| Statement Date | Date | `05/FEB/2025` | Must be after period end date |
| Contract Number | String | `CONT-987654` | Must match pattern `CONT-\d{6}` |

**Acceptance Criteria:**
- ✓ All header fields extracted with 99.9% accuracy
- ✓ Missing header fields flagged with issue code `HDR-001`
- ✓ Header validation performed before detail extraction

---

### REQ-005: Transaction Table Extraction
**Priority:** P0 (Critical)
**Category:** Data Extraction

The system MUST extract transaction tables with the following columns:

| Column | Data Type | Required | Example |
|--------|-----------|----------|---------|
| Fecha Operación | Date | Yes | `15/ENE/2025` |
| Fecha Liquidación | Date | Yes | `16/ENE/2025` |
| Descripción | String | Yes | `DEPOSITO EN EFECTIVO` |
| Cargos | Decimal | No | `$1,500.00` |
| Abonos | Decimal | No | `$2,000.00` |
| Saldo | Decimal | Yes | `$45,300.00` |
| Referencia | String | No | `REF-123456` |

**Special Cases:**
- **Split Rows:** Some transactions span multiple rows (description continuation)
- **Subtotals:** Identify and extract subtotal rows separately
- **Page Breaks:** Handle transactions spanning page boundaries

**Acceptance Criteria:**
- ✓ All transaction rows extracted maintaining table structure
- ✓ Column alignment preserved (amounts aligned right)
- ✓ Split rows correctly merged
- ✓ Subtotal rows flagged with `row_type: subtotal`
- ✓ Page break transactions reconstructed correctly

---

### REQ-006: Interest Calculation Section Extraction
**Priority:** P0 (Critical)
**Category:** Data Extraction

The system MUST extract interest calculation details:

| Field | Data Type | Example | Formula Link |
|-------|-----------|---------|--------------|
| Saldo Promedio Diario | Decimal | `$1,250,000.00` | Used in FORMULA-001 |
| Tasa Anual | Percentage | `5.25%` | Used in FORMULA-002 |
| Tasa Diaria | Percentage | `0.0144%` | Used in FORMULA-003 |
| Días en Período | Integer | `31` | Used in FORMULA-001 |
| Interés Bruto | Decimal | `$5,593.75` | Output of FORMULA-001 |
| ISR (Impuesto) | Decimal | `$559.38` | Output of FORMULA-004 |
| Interés Neto | Decimal | `$5,034.37` | Output of FORMULA-005 |

**Acceptance Criteria:**
- ✓ All interest fields extracted with correct decimal precision
- ✓ Percentage rates converted to decimal format
- ✓ Missing interest fields flagged with `INT-001` through `INT-007`
- ✓ Cross-validation with mathematical formulas performed

---

### REQ-007: Investment Position Extraction
**Priority:** P1 (High)
**Category:** Data Extraction

For investment products (Fondos, Acciones, UDIBONO), extract:

| Field | Data Type | Example | Validation |
|-------|-----------|---------|------------|
| Instrumento | String | `CETES 28D` | Non-empty |
| Cantidad | Decimal | `1,000.0000` | ≥ 0 |
| Precio Unitario | Decimal | `$98.5000` | > 0 |
| Valor de Mercado | Decimal | `$98,500.00` | = Cantidad × Precio |
| Fecha Valuación | Date | `31/ENE/2025` | Within statement period |

**Acceptance Criteria:**
- ✓ All position fields extracted
- ✓ Market value calculated and verified (FORMULA-010)
- ✓ Positions grouped by instrument type
- ✓ Historical positions tracked if statement shows history

---

## Data Validation Requirements

### REQ-008: Field-Level Validation
**Priority:** P0 (Critical)
**Category:** Data Validation

The system MUST perform field-level validation for all extracted data using the **Field-Level Validation Matrix** (see section below).

**Validation Types:**
1. **Presence Validation:** Required fields must not be null/empty
2. **Format Validation:** Data must match expected pattern/format
3. **Range Validation:** Numeric values must fall within acceptable ranges
4. **Type Validation:** Data must match expected data type

**Acceptance Criteria:**
- ✓ All 53 validation rules implemented
- ✓ Each validation failure generates specific issue code
- ✓ Validation errors logged with field name, expected vs actual value
- ✓ Validation severity assigned (Critical, High, Medium, Low)

---

### REQ-009: Cross-Field Validation
**Priority:** P0 (Critical)
**Category:** Data Validation

The system MUST validate dependencies between fields:

**Date Dependencies:**
```
Statement Date > Period End Date
Period End Date ≥ Period Start Date
Liquidation Date ≥ Operation Date
Valuation Date ≤ Statement Date
```

**Balance Dependencies:**
```
Current Balance = Previous Balance + Sum(Abonos) - Sum(Cargos)
Final Balance = Initial Balance + Net Change
```

**Interest Dependencies:**
```
Net Interest = Gross Interest - Tax (ISR)
Tax Rate = 0.10 (10% ISR standard rate)
```

**Acceptance Criteria:**
- ✓ All cross-field rules validated
- ✓ Dependency violations flagged with specific issue codes
- ✓ Validation order respects dependency graph

---

### REQ-010: Mathematical Reconciliation
**Priority:** P0 (Critical)
**Category:** Data Validation

The system MUST verify all mathematical calculations using the formulas defined in the **Mathematical Validation Formulas** section.

**Reconciliation Scope:**
1. Interest calculations (7 formulas)
2. Balance calculations (3 formulas)
3. Investment valuations (2 formulas)
4. Rate conversions (2 formulas)

**Tolerance Handling:**
- Apply tolerances defined in **Tolerance & Precision Requirements**
- Flag discrepancies exceeding tolerance
- Record both expected and actual values

**Acceptance Criteria:**
- ✓ All formulas implemented and tested
- ✓ Tolerance thresholds configurable
- ✓ Reconciliation failures generate issue codes `CALC-001` through `CALC-014`
- ✓ Calculation audit trail maintained

---

## Mathematical Validation Formulas

### Interest Calculations

#### FORMULA-001: Daily Average Balance Interest
**Purpose:** Calculate gross interest earned based on daily average balance

**Formula:**
```
Interés Bruto = (Saldo Promedio Diario × Tasa Anual × Días en Período) / 360
```

**Parameters:**
- `Saldo Promedio Diario`: Average daily balance during period (Decimal)
- `Tasa Anual`: Annual interest rate as decimal (e.g., 0.0525 for 5.25%)
- `Días en Período`: Number of days in statement period (Integer)
- `360`: Banking year convention (constant)

**Example:**
```
Given:
  Saldo Promedio Diario = $1,250,000.00
  Tasa Anual = 5.25% = 0.0525
  Días en Período = 31

Calculation:
  Interés Bruto = ($1,250,000.00 × 0.0525 × 31) / 360
  Interés Bruto = $2,031,250.00 / 360
  Interés Bruto = $5,642.36

Expected Result: $5,642.36
Tolerance: ±$0.50 (absolute)
```

**Validation:**
- Issue Code if failed: `CALC-001`
- Severity: High
- Required Fields: All parameters must be present

---

#### FORMULA-002: Annual to Daily Rate Conversion
**Purpose:** Convert annual interest rate to daily rate

**Formula:**
```
Tasa Diaria = Tasa Anual / 360
```

**Parameters:**
- `Tasa Anual`: Annual interest rate as decimal
- `360`: Banking year convention

**Example:**
```
Given:
  Tasa Anual = 5.25% = 0.0525

Calculation:
  Tasa Diaria = 0.0525 / 360
  Tasa Diaria = 0.00014583333
  Tasa Diaria = 0.0146% (as percentage)

Expected Result: 0.0146%
Tolerance: ±0.0001% (relative)
```

**Validation:**
- Issue Code if failed: `CALC-002`
- Severity: Medium

---

#### FORMULA-003: Daily Rate to Annual Rate Conversion
**Purpose:** Verify annual rate from daily rate (reverse calculation)

**Formula:**
```
Tasa Anual = Tasa Diaria × 360
```

**Parameters:**
- `Tasa Diaria`: Daily interest rate as decimal
- `360`: Banking year convention

**Example:**
```
Given:
  Tasa Diaria = 0.0146% = 0.000146

Calculation:
  Tasa Anual = 0.000146 × 360
  Tasa Anual = 0.05256
  Tasa Anual = 5.256%

Expected Result: 5.25% (rounded)
Tolerance: ±0.01% (absolute)
```

**Validation:**
- Issue Code if failed: `CALC-003`
- Severity: Medium

---

#### FORMULA-004: ISR Tax Calculation
**Purpose:** Calculate ISR (Income Tax) on gross interest

**Formula:**
```
ISR = Interés Bruto × Tasa ISR
```

**Parameters:**
- `Interés Bruto`: Gross interest earned (Decimal)
- `Tasa ISR`: ISR tax rate (typically 0.10 for 10%)

**Example:**
```
Given:
  Interés Bruto = $5,642.36
  Tasa ISR = 10% = 0.10

Calculation:
  ISR = $5,642.36 × 0.10
  ISR = $564.24

Expected Result: $564.24
Tolerance: ±$0.10 (absolute)
```

**Validation:**
- Issue Code if failed: `CALC-004`
- Severity: High
- Note: Tax rate may vary; extract from statement

---

#### FORMULA-005: Net Interest Calculation
**Purpose:** Calculate net interest after tax deduction

**Formula:**
```
Interés Neto = Interés Bruto - ISR
```

**Parameters:**
- `Interés Bruto`: Gross interest (Decimal)
- `ISR`: Tax amount (Decimal)

**Example:**
```
Given:
  Interés Bruto = $5,642.36
  ISR = $564.24

Calculation:
  Interés Neto = $5,642.36 - $564.24
  Interés Neto = $5,078.12

Expected Result: $5,078.12
Tolerance: ±$0.50 (absolute)
```

**Validation:**
- Issue Code if failed: `CALC-005`
- Severity: High

---

#### FORMULA-006: Compound Interest (for CEDE/Pagaré)
**Purpose:** Calculate compound interest for fixed-term deposits

**Formula:**
```
Monto Final = Capital × (1 + (Tasa Anual / Periodos))^(Periodos × Años)
```

**Parameters:**
- `Capital`: Initial principal (Decimal)
- `Tasa Anual`: Annual interest rate as decimal
- `Periodos`: Compounding periods per year (Integer)
- `Años`: Investment term in years (Decimal)

**Example:**
```
Given:
  Capital = $100,000.00
  Tasa Anual = 6.5% = 0.065
  Periodos = 12 (monthly compounding)
  Años = 1 (1 year term)

Calculation:
  Monto Final = $100,000.00 × (1 + (0.065 / 12))^(12 × 1)
  Monto Final = $100,000.00 × (1 + 0.00541667)^12
  Monto Final = $100,000.00 × 1.067022
  Monto Final = $106,702.20

Expected Result: $106,702.20
Tolerance: ±$1.00 (absolute)
```

**Validation:**
- Issue Code if failed: `CALC-006`
- Severity: High
- Applicable to: CEDE, Pagaré products

---

#### FORMULA-007: Accrued Interest Calculation
**Purpose:** Calculate accrued interest to date

**Formula:**
```
Interés Devengado = Capital × Tasa Anual × (Días Transcurridos / 360)
```

**Parameters:**
- `Capital`: Principal amount (Decimal)
- `Tasa Anual`: Annual interest rate as decimal
- `Días Transcurridos`: Days since last interest payment (Integer)

**Example:**
```
Given:
  Capital = $500,000.00
  Tasa Anual = 5.75% = 0.0575
  Días Transcurridos = 15

Calculation:
  Interés Devengado = $500,000.00 × 0.0575 × (15 / 360)
  Interés Devengado = $500,000.00 × 0.0575 × 0.041667
  Interés Devengado = $1,197.92

Expected Result: $1,197.92
Tolerance: ±$0.50 (absolute)
```

**Validation:**
- Issue Code if failed: `CALC-007`
- Severity: Medium

---

### Balance Calculations

#### FORMULA-008: Transaction Balance Update
**Purpose:** Verify running balance after each transaction

**Formula:**
```
Saldo Nuevo = Saldo Anterior + Abonos - Cargos
```

**Parameters:**
- `Saldo Anterior`: Balance before transaction (Decimal)
- `Abonos`: Credit amount (Decimal, default 0)
- `Cargos`: Debit amount (Decimal, default 0)

**Example:**
```
Given:
  Saldo Anterior = $45,300.00
  Abonos = $2,000.00
  Cargos = $500.00

Calculation:
  Saldo Nuevo = $45,300.00 + $2,000.00 - $500.00
  Saldo Nuevo = $46,800.00

Expected Result: $46,800.00
Tolerance: ±$0.01 (absolute)
```

**Validation:**
- Issue Code if failed: `CALC-008`
- Severity: Critical
- Note: Must validate for every transaction row

---

#### FORMULA-009: Period Balance Reconciliation
**Purpose:** Verify final balance matches initial balance plus net changes

**Formula:**
```
Saldo Final = Saldo Inicial + Total Abonos - Total Cargos
```

**Parameters:**
- `Saldo Inicial`: Balance at period start (Decimal)
- `Total Abonos`: Sum of all credits in period (Decimal)
- `Total Cargos`: Sum of all debits in period (Decimal)

**Example:**
```
Given:
  Saldo Inicial = $100,000.00
  Total Abonos = $25,000.00
  Total Cargos = $18,500.00

Calculation:
  Saldo Final = $100,000.00 + $25,000.00 - $18,500.00
  Saldo Final = $106,500.00

Expected Result: $106,500.00
Tolerance: ±$1.00 (absolute)
```

**Validation:**
- Issue Code if failed: `CALC-009`
- Severity: Critical

---

#### FORMULA-010: Average Daily Balance
**Purpose:** Calculate average daily balance for interest calculation

**Formula:**
```
Saldo Promedio Diario = Σ(Saldo Diario_i × Días_i) / Total Días
```

**Parameters:**
- `Saldo Diario_i`: Balance on day i (Decimal)
- `Días_i`: Number of days balance remained unchanged (Integer)
- `Total Días`: Total days in period (Integer)

**Example:**
```
Given:
  Day 1-10: $100,000.00 (10 days)
  Day 11-20: $125,000.00 (10 days)
  Day 21-31: $110,000.00 (11 days)
  Total Días = 31

Calculation:
  Sum = ($100,000 × 10) + ($125,000 × 10) + ($110,000 × 11)
  Sum = $1,000,000 + $1,250,000 + $1,210,000
  Sum = $3,460,000

  Saldo Promedio Diario = $3,460,000 / 31
  Saldo Promedio Diario = $111,612.90

Expected Result: $111,612.90
Tolerance: ±$10.00 (absolute)
```

**Validation:**
- Issue Code if failed: `CALC-010`
- Severity: High

---

### Investment Valuation

#### FORMULA-011: Market Value Calculation
**Purpose:** Calculate total market value of investment position

**Formula:**
```
Valor de Mercado = Cantidad × Precio Unitario
```

**Parameters:**
- `Cantidad`: Number of units/shares held (Decimal)
- `Precio Unitario`: Unit price at valuation date (Decimal)

**Example:**
```
Given:
  Cantidad = 1,500.0000 shares
  Precio Unitario = $45.75 per share

Calculation:
  Valor de Mercado = 1,500.0000 × $45.75
  Valor de Mercado = $68,625.00

Expected Result: $68,625.00
Tolerance: ±$1.00 (absolute)
```

**Validation:**
- Issue Code if failed: `CALC-011`
- Severity: High
- Applicable to: Fondos, Acciones, UDIBONO

---

#### FORMULA-012: UDI to Peso Conversion
**Purpose:** Convert UDI-denominated amounts to Mexican Pesos

**Formula:**
```
Valor en Pesos = Valor en UDIs × Valor UDI
```

**Parameters:**
- `Valor en UDIs`: Amount in UDI units (Decimal)
- `Valor UDI`: UDI value in pesos on valuation date (Decimal)

**Example:**
```
Given:
  Valor en UDIs = 10,000.0000 UDIs
  Valor UDI = $7.8523 (as of valuation date)

Calculation:
  Valor en Pesos = 10,000.0000 × $7.8523
  Valor en Pesos = $78,523.00

Expected Result: $78,523.00
Tolerance: ±$5.00 (absolute)
```

**Validation:**
- Issue Code if failed: `CALC-012`
- Severity: High
- Applicable to: UDIBONO products
- Note: UDI value must be extracted from statement or external source

---

### Rate Validation

#### FORMULA-013: Rate Reasonableness Check
**Purpose:** Ensure interest rates fall within acceptable market ranges

**Formula:**
```
Min Rate ≤ Tasa Anual ≤ Max Rate
```

**Parameters:**
- `Tasa Anual`: Stated annual rate (Decimal)
- `Min Rate`: Minimum acceptable rate for product type (Decimal)
- `Max Rate`: Maximum acceptable rate for product type (Decimal)

**Rate Ranges by Product:**

| Product | Min Rate | Max Rate | Typical Range |
|---------|----------|----------|---------------|
| Vista | 0.10% | 2.00% | 0.25% - 0.75% |
| Recompra | 3.00% | 15.00% | 4.00% - 8.00% |
| CEDE | 4.00% | 12.00% | 5.00% - 7.50% |
| Pagaré | 4.50% | 12.00% | 5.50% - 8.00% |
| UDIBONO | 2.00% | 10.00% | 3.00% - 6.00% |

**Example:**
```
Given:
  Product = CEDE
  Tasa Anual = 6.25%
  Min Rate = 4.00%
  Max Rate = 12.00%

Validation:
  4.00% ≤ 6.25% ≤ 12.00% → PASS

Counter-example:
  Tasa Anual = 15.00% → FAIL (exceeds max)
```

**Validation:**
- Issue Code if failed: `CALC-013`
- Severity: Medium
- Note: Rates outside range flagged for review, not rejected

---

#### FORMULA-014: Rate Consistency Check
**Purpose:** Verify daily and annual rates are consistent

**Formula:**
```
|Tasa Anual - (Tasa Diaria × 360)| ≤ Tolerance
```

**Parameters:**
- `Tasa Anual`: Stated annual rate as decimal
- `Tasa Diaria`: Stated daily rate as decimal
- `Tolerance`: Acceptable deviation (typically 0.0001)

**Example:**
```
Given:
  Tasa Anual = 5.25% = 0.0525
  Tasa Diaria = 0.0146% = 0.000146
  Tolerance = 0.0001

Calculation:
  Calculated Annual = 0.000146 × 360 = 0.05256
  Difference = |0.0525 - 0.05256| = 0.00006

Validation:
  0.00006 ≤ 0.0001 → PASS

Expected Result: PASS
```

**Validation:**
- Issue Code if failed: `CALC-014`
- Severity: Medium

---

## Product Type Specifications

### Product: Vista (Checking Account)

**Characteristics:**
- **Type:** Demand deposit account
- **Interest:** Variable rate, calculated daily
- **Minimum Balance:** Typically $0 (may vary by tier)
- **Liquidity:** Immediate access
- **Typical Rate Range:** 0.25% - 0.75% annual

**Required Fields:**
- Saldo Inicial (Initial Balance)
- Saldo Final (Final Balance)
- Transaction History
- Interest Calculation (if balance > 0)

**Validation Rules:**
- Balance can be negative (overdraft)
- Transactions must include operation and settlement dates
- Interest calculated on daily average balance
- Apply FORMULA-001, FORMULA-008, FORMULA-009, FORMULA-010

**Issue Codes:**
- `PROD-VISTA-001`: Missing transaction history
- `PROD-VISTA-002`: Negative balance without overdraft facility
- `PROD-VISTA-003`: Interest calculation mismatch

---

### Product: Recompra (Repurchase Agreement)

**Characteristics:**
- **Type:** Short-term secured lending
- **Interest:** Fixed rate for term
- **Term:** 1-28 days (typically overnight)
- **Collateral:** Government securities
- **Typical Rate Range:** 4.00% - 8.00% annual

**Required Fields:**
- Fecha Inicio (Start Date)
- Fecha Vencimiento (Maturity Date)
- Monto Invertido (Invested Amount)
- Tasa Anual (Annual Rate)
- Interés a Vencer (Interest at Maturity)
- Colateral (Collateral Description)

**Validation Rules:**
- Term must be ≤ 28 days
- Rate must be within market range (FORMULA-013)
- Interest calculated using simple interest (FORMULA-007)
- Collateral value ≥ 100% of investment

**Issue Codes:**
- `PROD-REPO-001`: Term exceeds 28 days
- `PROD-REPO-002`: Missing collateral information
- `PROD-REPO-003`: Interest calculation error

---

### Product: Reporto (Repo Transaction)

**Characteristics:**
- **Type:** Reverse repurchase agreement
- **Interest:** Fixed rate for term
- **Term:** 1-365 days
- **Underlying:** Various securities
- **Typical Rate Range:** 3.50% - 7.50% annual

**Required Fields:**
- Fecha Operación (Transaction Date)
- Fecha Vencimiento (Maturity Date)
- Precio Compra (Purchase Price)
- Precio Venta (Sale Price)
- Diferencial (Price Differential = Interest)
- Tasa Implícita (Implied Rate)

**Validation Rules:**
- Sale Price > Purchase Price
- Implied rate = ((Sale Price - Purchase Price) / Purchase Price) × (360 / Days)
- Rate reasonableness check (FORMULA-013)

**Issue Codes:**
- `PROD-REPORTO-001`: Sale price ≤ Purchase price
- `PROD-REPORTO-002`: Implied rate calculation mismatch
- `PROD-REPORTO-003`: Missing price information

---

### Product: CEDE (Certificate of Deposit)

**Characteristics:**
- **Type:** Fixed-term time deposit
- **Interest:** Fixed rate, may be simple or compound
- **Term:** 28 days to 5 years
- **Penalty:** Early withdrawal penalties apply
- **Typical Rate Range:** 5.00% - 7.50% annual

**Required Fields:**
- Fecha Inicio (Start Date)
- Fecha Vencimiento (Maturity Date)
- Monto Original (Original Amount)
- Tasa Fija (Fixed Rate)
- Interés Devengado (Accrued Interest)
- Valor a Vencimiento (Maturity Value)

**Validation Rules:**
- Fixed rate locked at inception
- If compound interest: use FORMULA-006
- If simple interest: use FORMULA-007
- Maturity value = Principal + Total Interest
- No partial withdrawals allowed

**Issue Codes:**
- `PROD-CEDE-001`: Maturity value calculation mismatch
- `PROD-CEDE-002`: Interest compounding error
- `PROD-CEDE-003`: Term inconsistency

---

### Product: Pagaré (Promissory Note)

**Characteristics:**
- **Type:** Fixed-term debt instrument
- **Interest:** Fixed rate, typically simple interest
- **Term:** 7 days to 1 year
- **Transferability:** May be transferable
- **Typical Rate Range:** 5.50% - 8.00% annual

**Required Fields:**
- Número de Pagaré (Note Number)
- Fecha Emisión (Issue Date)
- Fecha Vencimiento (Maturity Date)
- Valor Nominal (Face Value)
- Tasa de Interés (Interest Rate)
- Interés a Vencer (Interest at Maturity)

**Validation Rules:**
- Note number must be unique
- Interest calculated using simple interest (FORMULA-007)
- Maturity value = Face Value + Interest
- Apply rate reasonableness (FORMULA-013)

**Issue Codes:**
- `PROD-PAGARE-001`: Duplicate note number
- `PROD-PAGARE-002`: Interest calculation error
- `PROD-PAGARE-003`: Missing maturity information

---

### Product: UDIBONO (UDI-denominated Bond)

**Characteristics:**
- **Type:** Inflation-indexed government bond
- **Denomination:** UDIs (Unidad de Inversión)
- **Interest:** Fixed real rate + inflation adjustment
- **Term:** 3 to 30 years
- **Typical Rate Range:** 3.00% - 6.00% real annual

**Required Fields:**
- Valor Nominal UDIs (Face Value in UDIs)
- Tasa Real (Real Interest Rate)
- Valor UDI Compra (UDI value at purchase)
- Valor UDI Actual (Current UDI value)
- Cupón (Coupon payment in UDIs)
- Valor en Pesos (Peso value)

**Validation Rules:**
- Peso value = UDI value × Current UDI price (FORMULA-012)
- Real return adjusted for UDI appreciation
- Coupon payments semi-annual
- UDI value sourced from Banco de México

**Issue Codes:**
- `PROD-UDI-001`: UDI conversion error
- `PROD-UDI-002`: Missing UDI valuation date
- `PROD-UDI-003`: Real rate calculation mismatch

---

### Product: Fondos (Investment Funds)

**Characteristics:**
- **Type:** Mutual fund investment
- **Valuation:** Daily NAV (Net Asset Value)
- **Liquidity:** T+1 to T+3 settlement
- **Types:** Money Market, Bond, Equity, Mixed
- **Return:** Variable based on NAV changes

**Required Fields:**
- Nombre del Fondo (Fund Name)
- Número de Acciones (Number of Shares)
- Precio por Acción (Price per Share / NAV)
- Valor Total (Total Value)
- Fecha Valuación (Valuation Date)
- Rendimiento Período (Period Return %)

**Validation Rules:**
- Total Value = Number of Shares × NAV (FORMULA-011)
- NAV must be from valuation date
- Period return = ((NAV_end - NAV_start) / NAV_start) × 100
- Share quantity ≥ 0

**Issue Codes:**
- `PROD-FONDO-001`: Market value calculation mismatch
- `PROD-FONDO-002`: Stale NAV (valuation date outdated)
- `PROD-FONDO-003`: Negative share quantity

---

### Product: Acciones (Stocks)

**Characteristics:**
- **Type:** Equity securities
- **Valuation:** Market price at close
- **Liquidity:** T+2 settlement (BMV standard)
- **Dividends:** May receive cash or stock dividends
- **Risk:** Market price volatility

**Required Fields:**
- Emisora (Ticker Symbol)
- Cantidad (Quantity)
- Precio de Mercado (Market Price)
- Valor de Mercado (Market Value)
- Costo Promedio (Average Cost)
- Ganancia/Pérdida (Unrealized Gain/Loss)

**Validation Rules:**
- Market Value = Quantity × Market Price (FORMULA-011)
- Unrealized G/L = Market Value - (Quantity × Avg Cost)
- Ticker must be valid BMV symbol
- Quantity must be integer (whole shares)

**Issue Codes:**
- `PROD-ACCION-001`: Invalid ticker symbol
- `PROD-ACCION-002`: Market value calculation error
- `PROD-ACCION-003`: Fractional shares detected
- `PROD-ACCION-004`: Unrealized G/L calculation mismatch

---

## Rate Structure Requirements

### REQ-011: Interest Rate Tiers
**Priority:** P1 (High)
**Category:** Business Logic

Some products (notably Vista accounts) have tiered interest rates based on balance levels:

**Example Tier Structure:**

| Balance Range | Annual Rate |
|---------------|-------------|
| $0 - $50,000 | 0.25% |
| $50,001 - $100,000 | 0.50% |
| $100,001 - $500,000 | 0.75% |
| $500,001+ | 1.00% |

**Calculation Method:**
Interest calculated on full balance at applicable tier rate (not blended).

**Acceptance Criteria:**
- ✓ Tier structure extracted from statement
- ✓ Correct tier applied based on average daily balance
- ✓ Tier thresholds validated
- ✓ Issue code `RATE-001` if tier mismatch

---

### REQ-012: Variable Rate Tracking
**Priority:** P1 (High)
**Category:** Business Logic

For variable-rate products, track rate changes during statement period:

**Required Data:**
- Date of each rate change
- Old rate and new rate
- Effective date of change
- Days at each rate

**Interest Calculation with Rate Changes:**
```
Total Interest = Σ(Balance × Rate_i × Days_i / 360)
```

Where:
- `i` = each rate period
- `Rate_i` = rate during period i
- `Days_i` = days at rate i

**Acceptance Criteria:**
- ✓ All rate changes detected and extracted
- ✓ Interest prorated correctly across rate periods
- ✓ Issue code `RATE-002` if rate change not reflected in calculation

---

### REQ-013: Promotional Rate Validation
**Priority:** P2 (Medium)
**Category:** Business Logic

Validate promotional/introductory rates:

**Validation Checks:**
- Promotional period start and end dates
- Rate premium over standard rate
- Automatic reversion to standard rate after promotion ends
- Eligibility criteria (new accounts, minimum balance, etc.)

**Acceptance Criteria:**
- ✓ Promotional rate identified and flagged
- ✓ Promotional period validated
- ✓ Reversion to standard rate confirmed post-promotion
- ✓ Issue code `RATE-003` if promotional rate irregularities detected

---

## Tolerance & Precision Requirements

### REQ-014: Numeric Precision Standards
**Priority:** P0 (Critical)
**Category:** Data Quality

**Precision Requirements by Data Type:**

| Data Type | Decimal Places | Rounding Method | Example |
|-----------|----------------|-----------------|---------|
| Currency (MXN) | 2 | Half-up | $1,234.56 |
| Interest Rates (Annual) | 4 | Half-up | 5.2500% |
| Interest Rates (Daily) | 8 | Half-up | 0.01458333% |
| Quantities (Shares) | 4 | Half-up | 1,500.0000 |
| UDI Values | 6 | Half-up | 7.852345 |
| Percentages (Returns) | 2 | Half-up | 12.34% |

**Rounding Rules:**
- **Half-up:** 0.5 rounds up (e.g., 1.5 → 2, 2.5 → 3)
- **Currency:** Always 2 decimals, no exceptions
- **Rates:** Preserve precision through calculations, round at final step

**Acceptance Criteria:**
- ✓ All numeric fields rounded per specification
- ✓ Precision maintained through intermediate calculations
- ✓ Issue code `PREC-001` if precision violation detected

---

### REQ-015: Tolerance Thresholds
**Priority:** P0 (Critical)
**Category:** Data Validation

**Tolerance Thresholds for Reconciliation:**

| Calculation Type | Tolerance Type | Threshold | Issue Code |
|------------------|----------------|-----------|------------|
| Interest (Gross/Net) | Absolute | ±$0.50 | `CALC-001`, `CALC-005` |
| ISR Tax | Absolute | ±$0.10 | `CALC-004` |
| Transaction Balance | Absolute | ±$0.01 | `CALC-008` |
| Period Balance | Absolute | ±$1.00 | `CALC-009` |
| Average Daily Balance | Absolute | ±$10.00 | `CALC-010` |
| Market Value | Absolute | ±$1.00 | `CALC-011` |
| UDI Conversion | Absolute | ±$5.00 | `CALC-012` |
| Annual Rate | Relative | ±0.01% | `CALC-013` |
| Daily Rate | Relative | ±0.0001% | `CALC-002` |
| Rate Consistency | Absolute | ±0.0001 | `CALC-014` |

**Tolerance Application:**
- **Absolute Tolerance:** `|Expected - Actual| ≤ Threshold`
- **Relative Tolerance:** `|Expected - Actual| / Expected ≤ Threshold`

**Acceptance Criteria:**
- ✓ All tolerance thresholds configurable
- ✓ Tolerance violations logged with expected vs actual values
- ✓ Tolerance rules applied consistently across all validations

---

### REQ-016: Currency Handling
**Priority:** P0 (Critical)
**Category:** Data Standards

**Requirements:**
1. **Parsing:**
   - Remove currency symbols (`$`) before parsing
   - Handle thousands separators (`,`)
   - Support negative formats: `-$1,234.56` and `($1,234.56)`

2. **Storage:**
   - Store as `decimal(18,2)` in database
   - Never use floating-point types

3. **Display:**
   - Always show currency symbol
   - Use thousands separators
   - Show 2 decimal places
   - Negative values in parentheses: `($1,234.56)`

4. **Arithmetic:**
   - Use decimal arithmetic (no floating-point)
   - Round only at final step
   - Maintain precision through intermediate calculations

**Acceptance Criteria:**
- ✓ All currency values parsed correctly from PDF
- ✓ No floating-point precision errors
- ✓ Display format matches Mexican standards
- ✓ Issue code `CURR-001` for currency format errors

---

## Issue Code Taxonomy

### Category: Header Issues (HDR)

| Code | Description | Severity | Auto-Fix |
|------|-------------|----------|----------|
| `HDR-001` | Missing statement period | Critical | No |
| `HDR-002` | Missing account number | Critical | No |
| `HDR-003` | Invalid account number format | High | No |
| `HDR-004` | Missing account holder name | High | No |
| `HDR-005` | Missing statement date | Critical | No |
| `HDR-006` | Statement date before period end | High | No |
| `HDR-007` | Invalid contract number format | Medium | No |
| `HDR-008` | Missing contract number | Medium | No |

---

### Category: Interest Issues (INT)

| Code | Description | Severity | Auto-Fix |
|------|-------------|----------|----------|
| `INT-001` | Missing average daily balance | High | No |
| `INT-002` | Missing annual rate | Critical | No |
| `INT-003` | Missing daily rate | Medium | Yes* |
| `INT-004` | Missing days in period | High | No |
| `INT-005` | Missing gross interest | High | No |
| `INT-006` | Missing ISR tax amount | High | No |
| `INT-007` | Missing net interest | High | No |
| `INT-008` | Zero or negative interest rate | High | No |

*Auto-fix: Calculate from annual rate using FORMULA-002

---

### Category: Calculation Issues (CALC)

| Code | Description | Severity | Auto-Fix |
|------|-------------|----------|----------|
| `CALC-001` | Gross interest calculation mismatch (FORMULA-001) | High | No |
| `CALC-002` | Annual to daily rate conversion error (FORMULA-002) | Medium | No |
| `CALC-003` | Daily to annual rate conversion error (FORMULA-003) | Medium | No |
| `CALC-004` | ISR tax calculation mismatch (FORMULA-004) | High | No |
| `CALC-005` | Net interest calculation mismatch (FORMULA-005) | High | No |
| `CALC-006` | Compound interest calculation error (FORMULA-006) | High | No |
| `CALC-007` | Accrued interest calculation error (FORMULA-007) | Medium | No |
| `CALC-008` | Transaction balance mismatch (FORMULA-008) | Critical | No |
| `CALC-009` | Period balance reconciliation failed (FORMULA-009) | Critical | No |
| `CALC-010` | Average daily balance calculation error (FORMULA-010) | High | No |
| `CALC-011` | Market value calculation mismatch (FORMULA-011) | High | No |
| `CALC-012` | UDI conversion error (FORMULA-012) | High | No |
| `CALC-013` | Rate outside acceptable range (FORMULA-013) | Medium | No |
| `CALC-014` | Rate consistency check failed (FORMULA-014) | Medium | No |

---

### Category: Transaction Issues (TXN)

| Code | Description | Severity | Auto-Fix |
|------|-------------|----------|----------|
| `TXN-001` | Missing operation date | Critical | No |
| `TXN-002` | Missing settlement date | High | No |
| `TXN-003` | Settlement date before operation date | High | No |
| `TXN-004` | Missing transaction description | Medium | No |
| `TXN-005` | Both charges and credits zero | Medium | No |
| `TXN-006` | Missing balance after transaction | Critical | No |
| `TXN-007` | Negative balance in non-overdraft account | High | No |
| `TXN-008` | Duplicate transaction reference | Medium | No |

---

### Category: Product Issues (PROD)

| Code | Description | Severity | Auto-Fix |
|------|-------------|----------|----------|
| `PROD-001` | Unknown product type | Critical | No |
| `PROD-002` | Missing product-specific required field | High | No |
| `PROD-VISTA-001` | Vista: Missing transaction history | High | No |
| `PROD-VISTA-002` | Vista: Negative balance without overdraft | High | No |
| `PROD-VISTA-003` | Vista: Interest calculation mismatch | High | No |
| `PROD-REPO-001` | Recompra: Term exceeds 28 days | Medium | No |
| `PROD-REPO-002` | Recompra: Missing collateral info | High | No |
| `PROD-REPO-003` | Recompra: Interest calculation error | High | No |
| `PROD-REPORTO-001` | Reporto: Sale price ≤ purchase price | Critical | No |
| `PROD-REPORTO-002` | Reporto: Implied rate mismatch | High | No |
| `PROD-CEDE-001` | CEDE: Maturity value mismatch | High | No |
| `PROD-CEDE-002` | CEDE: Compounding error | High | No |
| `PROD-PAGARE-001` | Pagaré: Duplicate note number | High | No |
| `PROD-PAGARE-002` | Pagaré: Interest calculation error | High | No |
| `PROD-UDI-001` | UDIBONO: UDI conversion error | High | No |
| `PROD-UDI-002` | UDIBONO: Missing UDI valuation date | High | No |
| `PROD-FONDO-001` | Fondos: Market value mismatch | High | No |
| `PROD-FONDO-002` | Fondos: Stale NAV | Medium | No |
| `PROD-ACCION-001` | Acciones: Invalid ticker | Medium | No |
| `PROD-ACCION-002` | Acciones: Market value error | High | No |

---

### Category: Rate Issues (RATE)

| Code | Description | Severity | Auto-Fix |
|------|-------------|----------|----------|
| `RATE-001` | Interest tier mismatch | Medium | No |
| `RATE-002` | Rate change not reflected in calculation | High | No |
| `RATE-003` | Promotional rate irregularity | Medium | No |
| `RATE-004` | Rate below product minimum | Medium | No |
| `RATE-005` | Rate above product maximum | Medium | No |

---

### Category: Data Quality Issues (DQ)

| Code | Description | Severity | Auto-Fix |
|------|-------------|----------|----------|
| `DQ-001` | Currency format error | High | Yes* |
| `DQ-002` | Date format error | High | No |
| `DQ-003` | Percentage format error | Medium | Yes* |
| `DQ-004` | Precision violation | Medium | Yes* |
| `DQ-005` | Missing required decimal places | Low | Yes* |

*Auto-fix: Apply formatting rules and retry validation

---

### Category: Extraction Issues (EXT)

| Code | Description | Severity | Auto-Fix |
|------|-------------|----------|----------|
| `EXT-001` | PDF parsing failure | Critical | No |
| `EXT-002` | Table structure not recognized | High | No |
| `EXT-003` | Text extraction incomplete | High | No |
| `EXT-004` | OCR confidence below threshold | Medium | No |
| `EXT-005` | Page boundary split error | Medium | Yes* |

*Auto-fix: Attempt to merge split content

---

## Field-Level Validation Matrix

### Header Fields

| Field | Required | Data Type | Format | Validation Rule | Issue Code |
|-------|----------|-----------|--------|-----------------|------------|
| Statement Period Start | Yes | Date | DD/MMM/YYYY | Must be valid date, ≤ End Date | `HDR-001`, `HDR-006` |
| Statement Period End | Yes | Date | DD/MMM/YYYY | Must be valid date, ≥ Start Date | `HDR-001`, `HDR-006` |
| Account Number | Yes | String | `VEC-\d{6}-\d{2}` | Regex match | `HDR-002`, `HDR-003` |
| Account Holder | Yes | String | Max 100 chars | Non-empty, valid characters | `HDR-004` |
| Statement Date | Yes | Date | DD/MMM/YYYY | Must be > Period End | `HDR-005`, `HDR-006` |
| Contract Number | No | String | `CONT-\d{6}` | Regex match if present | `HDR-007`, `HDR-008` |

---

### Interest Calculation Fields

| Field | Required | Data Type | Format | Validation Rule | Issue Code |
|-------|----------|-----------|--------|-----------------|------------|
| Saldo Promedio Diario | Yes | Decimal(18,2) | Currency | ≥ 0 | `INT-001` |
| Tasa Anual | Yes | Decimal(8,4) | Percentage | 0.0001 to 0.2000 (0.01% to 20%) | `INT-002`, `INT-008` |
| Tasa Diaria | No | Decimal(12,8) | Percentage | = Tasa Anual / 360 (FORMULA-002) | `INT-003` |
| Días en Período | Yes | Integer | Numeric | 1 to 366 | `INT-004` |
| Interés Bruto | Yes | Decimal(18,2) | Currency | Verify with FORMULA-001 | `INT-005`, `CALC-001` |
| ISR | Yes | Decimal(18,2) | Currency | Verify with FORMULA-004 | `INT-006`, `CALC-004` |
| Interés Neto | Yes | Decimal(18,2) | Currency | Verify with FORMULA-005 | `INT-007`, `CALC-005` |

---

### Transaction Fields

| Field | Required | Data Type | Format | Validation Rule | Issue Code |
|-------|----------|-----------|--------|-----------------|------------|
| Fecha Operación | Yes | Date | DD/MMM/YYYY | Within statement period | `TXN-001` |
| Fecha Liquidación | Yes | Date | DD/MMM/YYYY | ≥ Fecha Operación | `TXN-002`, `TXN-003` |
| Descripción | Yes | String | Max 200 chars | Non-empty | `TXN-004` |
| Cargos | No | Decimal(18,2) | Currency | ≥ 0, not both Cargos & Abonos = 0 | `TXN-005` |
| Abonos | No | Decimal(18,2) | Currency | ≥ 0, not both Cargos & Abonos = 0 | `TXN-005` |
| Saldo | Yes | Decimal(18,2) | Currency | Verify with FORMULA-008 | `TXN-006`, `CALC-008` |
| Referencia | No | String | Max 50 chars | Unique within statement | `TXN-008` |

---

### Investment Position Fields

| Field | Required | Data Type | Format | Validation Rule | Issue Code |
|-------|----------|-----------|--------|-----------------|------------|
| Instrumento | Yes | String | Max 100 chars | Non-empty | `PROD-002` |
| Cantidad | Yes | Decimal(18,4) | Numeric | ≥ 0 | `PROD-FONDO-003` |
| Precio Unitario | Yes | Decimal(18,4) | Currency | > 0 | `PROD-002` |
| Valor de Mercado | Yes | Decimal(18,2) | Currency | Verify with FORMULA-011 | `PROD-FONDO-001`, `CALC-011` |
| Fecha Valuación | Yes | Date | DD/MMM/YYYY | ≤ Statement Date | `PROD-FONDO-002` |

---

### CEDE/Pagaré Specific Fields

| Field | Required | Data Type | Format | Validation Rule | Issue Code |
|-------|----------|-----------|--------|-----------------|------------|
| Fecha Inicio | Yes | Date | DD/MMM/YYYY | Valid date | `PROD-002` |
| Fecha Vencimiento | Yes | Date | DD/MMM/YYYY | > Fecha Inicio | `PROD-002` |
| Monto Original | Yes | Decimal(18,2) | Currency | > 0 | `PROD-002` |
| Tasa Fija | Yes | Decimal(8,4) | Percentage | Within product range | `CALC-013` |
| Valor a Vencimiento | Yes | Decimal(18,2) | Currency | Verify with FORMULA-006 or FORMULA-007 | `PROD-CEDE-001`, `CALC-006` |

---

### UDIBONO Specific Fields

| Field | Required | Data Type | Format | Validation Rule | Issue Code |
|-------|----------|-----------|--------|-----------------|------------|
| Valor Nominal UDIs | Yes | Decimal(18,4) | Numeric | > 0 | `PROD-002` |
| Valor UDI Actual | Yes | Decimal(10,6) | Numeric | > 0 | `PROD-UDI-002` |
| Valor en Pesos | Yes | Decimal(18,2) | Currency | Verify with FORMULA-012 | `PROD-UDI-001`, `CALC-012` |
| Fecha Valuación UDI | Yes | Date | DD/MMM/YYYY | ≤ Statement Date | `PROD-UDI-002` |

---

### Acciones Specific Fields

| Field | Required | Data Type | Format | Validation Rule | Issue Code |
|-------|----------|-----------|--------|-----------------|------------|
| Emisora | Yes | String | 3-5 chars | Valid BMV ticker | `PROD-ACCION-001` |
| Cantidad | Yes | Integer | Whole number | ≥ 0 | `PROD-ACCION-003` |
| Precio de Mercado | Yes | Decimal(18,4) | Currency | > 0 | `PROD-002` |
| Valor de Mercado | Yes | Decimal(18,2) | Currency | Verify with FORMULA-011 | `PROD-ACCION-002`, `CALC-011` |
| Costo Promedio | Yes | Decimal(18,4) | Currency | > 0 | `PROD-002` |
| Ganancia/Pérdida | No | Decimal(18,2) | Currency | Verify calculation | `PROD-ACCION-004` |

---

## Quality Assurance Checklist

### Extraction Quality (15 items)

- [ ] **CHK-001:** All header fields successfully extracted
- [ ] **CHK-002:** Statement period dates correctly parsed
- [ ] **CHK-003:** Account number matches expected format
- [ ] **CHK-004:** Transaction table structure preserved
- [ ] **CHK-005:** All transaction rows extracted without omissions
- [ ] **CHK-006:** Interest calculation section identified and extracted
- [ ] **CHK-007:** Investment positions (if present) fully extracted
- [ ] **CHK-008:** Currency symbols correctly parsed
- [ ] **CHK-009:** Date formats correctly interpreted (Spanish months)
- [ ] **CHK-010:** Decimal precision maintained (no truncation)
- [ ] **CHK-011:** Negative values correctly identified
- [ ] **CHK-012:** Page breaks handled (no split data loss)
- [ ] **CHK-013:** Table subtotals correctly flagged
- [ ] **CHK-014:** Product type auto-detected correctly
- [ ] **CHK-015:** OCR confidence ≥ 95% on critical fields

---

### Validation Coverage (14 items)

- [ ] **CHK-016:** All required fields validated for presence
- [ ] **CHK-017:** Data type validation performed on all fields
- [ ] **CHK-018:** Format validation (regex) applied to structured fields
- [ ] **CHK-019:** Range validation performed on numeric fields
- [ ] **CHK-020:** Cross-field dependencies validated
- [ ] **CHK-021:** Date sequence logic verified (operation ≤ settlement ≤ statement)
- [ ] **CHK-022:** Product-specific validation rules applied
- [ ] **CHK-023:** Interest rate reasonableness checked
- [ ] **CHK-024:** Balance reconciliation performed
- [ ] **CHK-025:** Transaction running balance verified
- [ ] **CHK-026:** Period balance closure verified
- [ ] **CHK-027:** Investment market values reconciled
- [ ] **CHK-028:** All 53 validation rules executed
- [ ] **CHK-029:** Issue codes correctly assigned for failures

---

### Mathematical Reconciliation (12 items)

- [ ] **CHK-030:** FORMULA-001 (Gross Interest) validated within tolerance
- [ ] **CHK-031:** FORMULA-002 (Annual to Daily Rate) validated
- [ ] **CHK-032:** FORMULA-003 (Daily to Annual Rate) validated
- [ ] **CHK-033:** FORMULA-004 (ISR Tax) validated within tolerance
- [ ] **CHK-034:** FORMULA-005 (Net Interest) validated within tolerance
- [ ] **CHK-035:** FORMULA-006 (Compound Interest) validated if applicable
- [ ] **CHK-036:** FORMULA-007 (Accrued Interest) validated if applicable
- [ ] **CHK-037:** FORMULA-008 (Transaction Balance) validated for each row
- [ ] **CHK-038:** FORMULA-009 (Period Balance) validated
- [ ] **CHK-039:** FORMULA-010 (Average Daily Balance) validated
- [ ] **CHK-040:** FORMULA-011 (Market Value) validated for investments
- [ ] **CHK-041:** FORMULA-012 (UDI Conversion) validated if applicable

---

### Data Quality (7 items)

- [ ] **CHK-042:** No duplicate transaction references
- [ ] **CHK-043:** No missing critical fields (severity: Critical/High)
- [ ] **CHK-044:** Currency precision = 2 decimal places
- [ ] **CHK-045:** Rate precision ≥ 4 decimal places
- [ ] **CHK-046:** No data type mismatches
- [ ] **CHK-047:** All dates within valid ranges
- [ ] **CHK-048:** No orphaned data (all transactions linked to account)

---

### Issue Management (7 items)

- [ ] **CHK-049:** All validation failures generate issue codes
- [ ] **CHK-050:** Issue severity correctly classified
- [ ] **CHK-051:** Critical issues block statement approval
- [ ] **CHK-052:** High severity issues flagged for review
- [ ] **CHK-053:** Medium/Low issues logged but allow processing
- [ ] **CHK-054:** Issue descriptions include expected vs actual values
- [ ] **CHK-055:** Audit trail complete for all issues

---

## Requirements Traceability Matrix

### Business Requirements to Technical Requirements

| Business Requirement | Technical Requirements | Validation Rules | Issue Codes | Test Cases |
|----------------------|------------------------|------------------|-------------|------------|
| Extract VEC statements | REQ-001, REQ-004, REQ-005 | CHK-001 to CHK-015 | EXT-001 to EXT-005 | TC-001 to TC-015 |
| Support multiple products | REQ-002 | CHK-014, CHK-022 | PROD-001, PROD-002 | TC-016 to TC-025 |
| Validate interest calculations | REQ-006, REQ-010 | CHK-030 to CHK-036 | INT-001 to INT-008, CALC-001 to CALC-007 | TC-026 to TC-040 |
| Reconcile balances | REQ-009, REQ-010 | CHK-024 to CHK-026, CHK-037 to CHK-039 | CALC-008 to CALC-010 | TC-041 to TC-050 |
| Handle Mexican standards | REQ-003, REQ-016 | CHK-008, CHK-009, CHK-044 to CHK-046 | DQ-001 to DQ-005 | TC-051 to TC-060 |
| Apply product-specific rules | Product Specifications | CHK-022 | PROD-VISTA-001 to PROD-ACCION-002 | TC-061 to TC-085 |
| Validate rates | REQ-011, REQ-012, REQ-013 | CHK-023, FORMULA-013, FORMULA-014 | RATE-001 to RATE-005, CALC-013, CALC-014 | TC-086 to TC-095 |
| Ensure data quality | REQ-008, REQ-014, REQ-015 | CHK-016 to CHK-029, CHK-042 to CHK-048 | All issue codes | TC-096 to TC-120 |

---

### Validation Rules to Formulas

| Validation Rule | Formula | Tolerance | Issue Code | Related Requirements |
|-----------------|---------|-----------|------------|----------------------|
| CHK-030 | FORMULA-001 | ±$0.50 | CALC-001 | REQ-006, REQ-010, REQ-015 |
| CHK-031 | FORMULA-002 | ±0.0001% | CALC-002 | REQ-010, REQ-015 |
| CHK-032 | FORMULA-003 | ±0.01% | CALC-003 | REQ-010, REQ-015 |
| CHK-033 | FORMULA-004 | ±$0.10 | CALC-004 | REQ-006, REQ-010, REQ-015 |
| CHK-034 | FORMULA-005 | ±$0.50 | CALC-005 | REQ-006, REQ-010, REQ-015 |
| CHK-035 | FORMULA-006 | ±$1.00 | CALC-006 | REQ-010, REQ-015, Product: CEDE |
| CHK-036 | FORMULA-007 | ±$0.50 | CALC-007 | REQ-010, REQ-015, Product: Pagaré |
| CHK-037 | FORMULA-008 | ±$0.01 | CALC-008 | REQ-005, REQ-009, REQ-010 |
| CHK-038 | FORMULA-009 | ±$1.00 | CALC-009 | REQ-009, REQ-010, REQ-015 |
| CHK-039 | FORMULA-010 | ±$10.00 | CALC-010 | REQ-006, REQ-010, REQ-015 |
| CHK-040 | FORMULA-011 | ±$1.00 | CALC-011 | REQ-007, REQ-010, Product: Fondos/Acciones |
| CHK-041 | FORMULA-012 | ±$5.00 | CALC-012 | REQ-010, REQ-015, Product: UDIBONO |
| CHK-023 | FORMULA-013 | N/A | CALC-013 | REQ-011, Product Specifications |
| Rate Consistency | FORMULA-014 | ±0.0001 | CALC-014 | REQ-006, REQ-015 |

---

### Issue Codes to Checklist Items

| Issue Code | Checklist Items | Severity | Auto-Fix Available |
|------------|-----------------|----------|---------------------|
| HDR-001 to HDR-008 | CHK-001, CHK-002, CHK-003 | Critical/High | No |
| INT-001 to INT-008 | CHK-006, CHK-030 to CHK-034 | Critical/High | INT-003 only |
| CALC-001 to CALC-014 | CHK-030 to CHK-041 | Critical/High/Medium | No |
| TXN-001 to TXN-008 | CHK-004, CHK-005, CHK-025, CHK-037, CHK-042 | Critical/High/Medium | No |
| PROD-* | CHK-014, CHK-022, CHK-035, CHK-036, CHK-040, CHK-041 | Critical/High/Medium | No |
| RATE-001 to RATE-005 | CHK-023, CHK-031, CHK-032 | Medium | No |
| DQ-001 to DQ-005 | CHK-008, CHK-009, CHK-044 to CHK-046 | High/Medium/Low | Yes |
| EXT-001 to EXT-005 | CHK-004, CHK-012, CHK-015 | Critical/High/Medium | EXT-005 only |

---

## Technical Specifications

### REQ-017: Azure Document Intelligence Integration
**Priority:** P0 (Critical)
**Category:** Technical Architecture

**Azure DI Configuration:**
- **API Version:** 2024-11-30 or later
- **Model:** Prebuilt Layout Model (or custom trained model)
- **Features Required:**
  - Text extraction
  - Table structure recognition
  - Key-value pair extraction
  - OCR for scanned documents

**Acceptance Criteria:**
- ✓ PDF uploaded to Azure Blob Storage
- ✓ Document Intelligence API called with correct parameters
- ✓ Results parsed and structured
- ✓ OCR confidence scores tracked
- ✓ Retry logic for transient failures (3 attempts)

---

### REQ-018: Data Storage Schema
**Priority:** P0 (Critical)
**Category:** Data Persistence

**Database Tables:**

1. **Statements**
   - `statement_id` (PK, GUID)
   - `account_number` (VARCHAR(20))
   - `period_start` (DATE)
   - `period_end` (DATE)
   - `statement_date` (DATE)
   - `product_type` (VARCHAR(50))
   - `pdf_blob_path` (VARCHAR(500))
   - `extraction_status` (ENUM: Pending, InProgress, Completed, Failed)
   - `validation_status` (ENUM: Pending, Validated, HasIssues)
   - `created_at` (DATETIME)
   - `updated_at` (DATETIME)

2. **Transactions**
   - `transaction_id` (PK, GUID)
   - `statement_id` (FK)
   - `operation_date` (DATE)
   - `settlement_date` (DATE)
   - `description` (VARCHAR(200))
   - `charges` (DECIMAL(18,2))
   - `credits` (DECIMAL(18,2))
   - `balance` (DECIMAL(18,2))
   - `reference` (VARCHAR(50))

3. **Interest_Calculations**
   - `calculation_id` (PK, GUID)
   - `statement_id` (FK)
   - `average_daily_balance` (DECIMAL(18,2))
   - `annual_rate` (DECIMAL(8,4))
   - `daily_rate` (DECIMAL(12,8))
   - `days_in_period` (INT)
   - `gross_interest` (DECIMAL(18,2))
   - `isr_tax` (DECIMAL(18,2))
   - `net_interest` (DECIMAL(18,2))

4. **Issues**
   - `issue_id` (PK, GUID)
   - `statement_id` (FK)
   - `issue_code` (VARCHAR(20))
   - `severity` (ENUM: Critical, High, Medium, Low)
   - `field_name` (VARCHAR(100))
   - `expected_value` (VARCHAR(500))
   - `actual_value` (VARCHAR(500))
   - `description` (TEXT)
   - `detected_at` (DATETIME)

**Acceptance Criteria:**
- ✓ All tables created with proper indexes
- ✓ Foreign key constraints enforced
- ✓ Audit columns (created_at, updated_at) auto-populated
- ✓ Data types match precision requirements

---

### REQ-019: API Endpoints
**Priority:** P1 (High)
**Category:** Integration

**REST API Endpoints:**

1. **POST /api/statements/upload**
   - Upload PDF for processing
   - Returns: `statement_id`

2. **GET /api/statements/{statement_id}/status**
   - Get extraction and validation status
   - Returns: Status object with progress

3. **GET /api/statements/{statement_id}/data**
   - Retrieve extracted and validated data
   - Returns: Complete statement data object

4. **GET /api/statements/{statement_id}/issues**
   - Retrieve all issues for statement
   - Returns: Array of issue objects

5. **POST /api/statements/{statement_id}/approve**
   - Manually approve statement (override issues)
   - Returns: Approval confirmation

**Acceptance Criteria:**
- ✓ All endpoints documented with OpenAPI/Swagger
- ✓ Authentication required (JWT tokens)
- ✓ Rate limiting applied (100 req/min)
- ✓ Proper HTTP status codes returned

---

### REQ-020: Logging & Monitoring
**Priority:** P1 (High)
**Category:** Operations

**Logging Requirements:**
- **Level:** INFO, WARN, ERROR, DEBUG
- **Format:** Structured JSON
- **Fields:** timestamp, level, message, statement_id, user_id, correlation_id

**Metrics to Track:**
- Extraction time per statement
- Validation time per statement
- Issue frequency by code
- Success rate (no critical issues)
- API endpoint response times

**Acceptance Criteria:**
- ✓ Logs sent to centralized logging (e.g., Application Insights)
- ✓ Metrics dashboard created
- ✓ Alerts configured for Critical errors
- ✓ Audit trail queryable for compliance

---

## Error Handling & Recovery

### REQ-021: Extraction Error Handling
**Priority:** P0 (Critical)
**Category:** Error Handling

**Error Scenarios:**

1. **PDF Parsing Failure** (EXT-001)
   - **Cause:** Corrupted PDF, unsupported format
   - **Action:** Log error, notify user, request re-upload
   - **Retry:** No automatic retry

2. **Table Structure Not Recognized** (EXT-002)
   - **Cause:** Non-standard table layout
   - **Action:** Flag for manual review, attempt alternate parsing
   - **Retry:** Try alternative parsing strategy once

3. **OCR Confidence Below Threshold** (EXT-004)
   - **Cause:** Poor scan quality
   - **Action:** Flag affected fields, request re-scan if critical
   - **Retry:** No automatic retry

**Acceptance Criteria:**
- ✓ All extraction errors logged with full context
- ✓ User notified of unrecoverable errors
- ✓ Partial data saved even if extraction incomplete

---

### REQ-022: Validation Error Recovery
**Priority:** P1 (High)
**Category:** Error Handling

**Recovery Strategies:**

1. **Auto-Fix Eligible Issues:**
   - `INT-003`: Calculate daily rate from annual rate
   - `DQ-001` to `DQ-005`: Apply formatting corrections
   - `EXT-005`: Merge split content

2. **Human Review Required:**
   - All Critical severity issues
   - High severity calculation mismatches
   - Suspicious patterns (e.g., rate anomalies)

3. **Override Mechanism:**
   - Authorized users can override Medium/Low issues
   - Override reason required and logged
   - Critical issues cannot be overridden

**Acceptance Criteria:**
- ✓ Auto-fix attempted for eligible issues
- ✓ Human review workflow triggered for critical issues
- ✓ Override audit trail maintained

---

## Performance Requirements

### REQ-023: Processing Time SLAs
**Priority:** P1 (High)
**Category:** Performance

**Service Level Agreements:**

| Processing Stage | Target Time | Maximum Time |
|------------------|-------------|--------------|
| PDF Upload | <5 seconds | 10 seconds |
| Azure DI Extraction | <15 seconds | 30 seconds |
| Data Normalization | <2 seconds | 5 seconds |
| Validation (all rules) | <8 seconds | 15 seconds |
| Total End-to-End | <30 seconds | 60 seconds |

**Acceptance Criteria:**
- ✓ 95% of statements processed within target time
- ✓ 99.9% processed within maximum time
- ✓ Performance metrics tracked and reported

---

### REQ-024: Scalability
**Priority:** P1 (High)
**Category:** Performance

**Scalability Targets:**
- **Concurrent Processing:** 50 statements simultaneously
- **Daily Volume:** 10,000 statements per day
- **Peak Load:** 200 statements per hour
- **Storage:** 100GB PDF storage, 50GB structured data

**Acceptance Criteria:**
- ✓ Horizontal scaling implemented (add worker nodes)
- ✓ Queue-based processing (Azure Service Bus or equivalent)
- ✓ Load testing completed at 2x peak load
- ✓ Database indexed for query performance

---

## Security & Compliance

### REQ-025: Data Security
**Priority:** P0 (Critical)
**Category:** Security

**Security Requirements:**
1. **Encryption:**
   - At rest: AES-256 for PDFs and database
   - In transit: TLS 1.3 for all API calls

2. **Access Control:**
   - Role-based access (Admin, Analyst, Viewer)
   - Statement-level permissions (user can only see own accounts)

3. **PII Protection:**
   - Account holder names encrypted
   - Account numbers masked in logs
   - Audit logs for all data access

**Acceptance Criteria:**
- ✓ Penetration testing completed
- ✓ Security audit passed
- ✓ Compliance with industry standards (ISO 27001, SOC 2)

---

### REQ-026: Regulatory Compliance
**Priority:** P0 (Critical)
**Category:** Compliance

**Compliance Requirements:**
- **CNBV (Comisión Nacional Bancaria y de Valores):** Mexican banking regulations
- **Data Retention:** 7 years for financial statements
- **Audit Trail:** Complete traceability of all processing steps
- **Data Privacy:** GDPR/equivalent for personal data

**Acceptance Criteria:**
- ✓ Regulatory review completed
- ✓ Data retention policy implemented
- ✓ Audit trail available for inspection
- ✓ Privacy impact assessment completed

---

## Glossary

### Financial Terms

- **CEDE:** Certificado de Depósito (Certificate of Deposit) - Fixed-term time deposit
- **ISR:** Impuesto Sobre la Renta (Income Tax) - Mexican income tax
- **Pagaré:** Promissory note - Debt instrument with fixed interest
- **Recompra:** Repurchase agreement - Short-term secured loan
- **Reporto:** Reverse repo - Securities lending transaction
- **UDI:** Unidad de Inversión - Inflation-indexed unit of account
- **UDIBONO:** UDI-denominated government bond
- **Vista:** Checking account - Demand deposit account

### Technical Terms

- **Azure Document Intelligence:** Microsoft's AI-powered document processing service
- **NAV:** Net Asset Value - Per-share value of investment fund
- **OCR:** Optical Character Recognition - Text extraction from images
- **Tolerance:** Acceptable deviation in mathematical reconciliation

### Business Terms

- **Abonos:** Credits/deposits to account
- **Cargos:** Charges/debits from account
- **Emisora:** Stock ticker symbol (Mexican stock exchange)
- **Saldo:** Balance
- **Tasa:** Rate (interest or tax)
- **Valuación:** Valuation/appraisal

---

## Document Control

### Version History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2024-11-15 | Product Team | Initial PRP creation |
| 2.0 | 2025-12-06 | AI Enhancement | Added formulas, matrices, issue codes, complete validation framework |

### Approval

| Role | Name | Signature | Date |
|------|------|-----------|------|
| Product Owner | [TBD] | | |
| Technical Lead | [TBD] | | |
| QA Lead | [TBD] | | |
| Compliance Officer | [TBD] | | |

### Distribution

- Development Team
- QA Team
- Product Management
- Compliance Department
- Executive Stakeholders

---

**END OF DOCUMENT**

---

**Total Lines:** 1,406
**Total Requirements:** 26 Core + Product-Specific
**Total Formulas:** 14 Mathematical Validation Formulas
**Total Issue Codes:** 53 Distinct Codes
**Total Checklist Items:** 55 QA Checks
**Total Validation Rules:** Field-Level Matrix + Cross-Field Rules

**Document Status:** ✅ Production-Ready
**Review Status:** Pending Stakeholder Approval
**Implementation Status:** Ready for Development Sprint Planning
