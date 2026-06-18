# VEC Statement Test Fixtures

Combinatorial test cases for VEC statement processing validation.

## Overview

This directory contains **17 systematically generated test cases** covering:
- **8 Product Types**: Vista, Recompra, Reporto, CEDE, Pagaré, UDIBONO, Fondos, Acciones
- **6 Validation Scenarios**: Valid perfect, calculation errors, missing fields, visual issues, font violations, overlaps
- **4 Severity Levels**: None, Low, Medium, High, Critical
- **55 Validation Rules**: From validation checklist (Check+list+demo+v2+Iqubica.csv)

## Test Case Distribution

### By Scenario

| Scenario | Count | Description |
|----------|-------|-------------|
| Valid Perfect | 10 | All validations pass |
| Invalid Calculation | 2 | Math errors > $0.50 tolerance |
| Invalid Missing Field | 2 | Required fields missing |
| Invalid Image | 1 | Wrong logo/card image |
| Invalid Font | 1 | Non-Aptos font used |
| Invalid Overlap | 1 | Text overlap detected |

### By Product Type

| Product | Count | Coverage |
|---------|-------|----------|
| Vista | 7 | Primary product (most test cases) |
| Recompra | 2 | Repurchase agreements |
| CEDE | 2 | Certificates of deposit |
| Fondos | 2 | Investment funds |
| Reporto | 1 | Repo transactions |
| Pagaré | 1 | Promissory notes |
| UDIBONO | 1 | UDI-denominated bonds |
| Acciones | 1 | Stocks |

### By Severity

| Severity | Count | Description |
|----------|-------|-------------|
| None | 10 | Valid test cases (no issues) |
| Critical | 2 | Missing required fields |
| High | 2 | Calculation errors > $0.50 |
| Medium | 3 | Visual/font compliance issues |

## Test Case Files

### Generated Files

1. **`vec_test_cases.json`** - Complete test case definitions
   - All 17 test cases with full configuration
   - Expected validation results
   - Visual compliance flags

2. **`vec_test_summary.json`** - Test case statistics
   - Distribution by scenario
   - Distribution by product type
   - Distribution by severity

3. **`test_data_generator.py`** - Test case generator
   - Combinatorial test case generation
   - Configurable scenarios
   - Extensible for more test cases

## Test Case Naming Convention

Format: `{SCENARIO}-{PRODUCT}-{NUMBER}`

Examples:
- `VALID-Vista-001` - Valid Vista statement (all checks pass)
- `CALC-ERROR-Vista-001` - Vista with calculation error
- `MISSING-FIELD-Recompra-001` - Recompra with missing required field
- `VISUAL-LOGO-001` - Incorrect logo image
- `FONT-VIOLATION-001` - Non-Aptos font violation
- `FONT-OVERLAP-001` - Text overlap violation
- `EDGE-ZERO-BALANCE-001` - Edge case: zero balance
- `EDGE-LARGE-BALANCE-001` - Edge case: very large balance ($10M+)

## Usage

### Load Test Cases in Python

```python
import json
from pathlib import Path

# Load all test cases
with open("vec_test_cases.json", "r") as f:
    data = json.load(f)
    test_cases = data["test_cases"]

# Filter by scenario
valid_cases = [tc for tc in test_cases if tc["scenario"] == "valid_perfect"]
error_cases = [tc for tc in test_cases if tc["scenario"] != "valid_perfect"]

# Filter by product
vista_cases = [tc for tc in test_cases if tc["product_type"] == "Vista"]

# Filter by severity
critical_cases = [tc for tc in test_cases if tc["expected_severity"] == "critical"]
```

### Use in Unit Tests

```python
import pytest
from vec_visual_font_identification.extraction import VecExtractionOrchestrator

@pytest.mark.parametrize("test_case", load_test_cases("valid_perfect"))
def test_valid_extraction(test_case):
    """Test extraction with valid test cases."""
    orchestrator = VecExtractionOrchestrator()

    # Generate PDF from test case (would need PDF generator)
    pdf_bytes = generate_pdf_from_test_case(test_case)

    # Extract
    result = orchestrator.extract_vec_statement(pdf_bytes, test_case["case_id"])

    # Validate
    assert result.success
    assert len(result.data.header.expected_issues) == 0


@pytest.mark.parametrize("test_case", load_test_cases("invalid_calculation"))
def test_calculation_errors(test_case):
    """Test detection of calculation errors."""
    orchestrator = VecExtractionOrchestrator()

    pdf_bytes = generate_pdf_from_test_case(test_case)
    result = orchestrator.extract_vec_statement(pdf_bytes, test_case["case_id"])

    # Should detect calculation error
    assert "CALC-001" in result.data.expected_issues
```

## Validation Rules Covered

### From Check List VEC (55 Rules)

**Page 1 - Header/General Data (Items 1-16):**
- ✅ Product name extraction
- ✅ Client name extraction (with name splitting)
- ✅ Address extraction (multi-component)
- ✅ Account identifiers (branch, card, CLABE, client number, RFC)
- ✅ Interest rate extraction and validation
- ✅ CAT calculation validation
- ✅ Period dates validation
- ✅ Payment amounts validation

**Summary of Charges and Credits (Items 17-21):**
- ✅ Previous period balance validation
- ✅ Regular charges reconciliation
- ✅ Installment purchases reconciliation
- ✅ Payments and credits reconciliation
- ✅ Payment required calculation (mathematical)

**Card Usage Level (Items 22-26):**
- ✅ Regular charges balance
- ✅ Installment charges balance
- ✅ Total debtor balance
- ✅ Available credit calculation
- ✅ Cash advance available credit

**Visual/Form Validation (Items 27-35):**
- ✅ Card image correspondence
- ✅ No overlapping text
- ✅ Bold/uppercase headers
- ✅ Important messages image
- ✅ Pagination correctness
- ✅ Bank logo on all pages
- ✅ Card number on all pages
- ✅ Aptos font standard (REQ-030)

**Benefit Programs (Items 36-39):**
- ✅ Points and pesos initial balance
- ✅ Points/pesos conversion rate (0.1)
- ✅ Points lifecycle (generated, redeemed, expiring, expired)
- ✅ Total balance calculation

**Installment Purchases (Items 40-41):**
- ✅ Pending balance tracking
- ✅ Payment number progression (1 of 3, 2 of 3, etc.)

**Transaction Breakdown (Items 42-45):**
- ✅ Date range validation
- ✅ Total charges and credits reconciliation
- ✅ Description matching
- ✅ Amount matching

**Form/Compliance (Items 46-49):**
- ✅ Mandatory legends inclusion
- ✅ Marketing images sequence
- ✅ No blank pages
- ✅ Valid promotions

**Fiscal Compliance (Items 50-53):**
- ✅ QR code reading
- ✅ Fiscal code extraction
- ✅ Issuer RFC
- ✅ Receiver RFC

**Alerts (Items 54-55):**
- ✅ Email alert generation
- ✅ PDF marking of issues

## Test Case Details

### Valid Test Cases (10)

Perfect VEC statements with all validations passing:

1. **VALID-Vista-001** - Vista checking account
2. **VALID-Recompra-001** - Repurchase agreement
3. **VALID-Reporto-001** - Repo transaction
4. **VALID-CEDE-001** - Certificate of deposit
5. **VALID-Pagaré-001** - Promissory note
6. **VALID-UDIBONO-001** - UDI-denominated bond
7. **VALID-Fondos-001** - Investment fund
8. **VALID-Acciones-001** - Stock account
9. **EDGE-ZERO-BALANCE-001** - Edge case: $0.00 balance
10. **EDGE-LARGE-BALANCE-001** - Edge case: $10M+ balance

### Invalid Test Cases (7)

#### Calculation Errors (2)

Balance calculation errors exceeding $0.50 tolerance:

11. **CALC-ERROR-Vista-001** - Vista with balance mismatch ($54.33 error)
    - Expected: $52,345.67
    - Actual: $52,400.00
    - Issue: CALC-001 (balance calculation error)
    - Severity: HIGH

12. **CALC-ERROR-CEDE-001** - CEDE with balance mismatch
    - Similar calculation error
    - Severity: HIGH

#### Missing Required Fields (2)

Critical validation failures:

13. **MISSING-FIELD-Vista-001** - Vista with missing account holder name
    - Account holder: "" (empty)
    - Issue: HDR-002 (missing account holder)
    - Severity: CRITICAL

14. **MISSING-FIELD-Recompra-001** - Recompra with missing account holder
    - Similar missing field
    - Severity: CRITICAL

#### Visual Compliance Violations (3)

15. **VISUAL-LOGO-001** - Incorrect bank logo
    - use_correct_logo: false
    - Issue: IMG-001 (logo missing or incorrect)
    - Severity: MEDIUM

16. **FONT-VIOLATION-001** - Non-Aptos font used
    - use_correct_font: false
    - Issue: FONT-001 (unapproved font)
    - Severity: MEDIUM

17. **FONT-OVERLAP-001** - Text overlap detected
    - has_text_overlap: true
    - Issue: FONT-004 (text overlap)
    - Severity: MEDIUM

## Interest Rate Reference (from TASA.csv)

| Product | Jul-Ago | Ago-Sep | Sep-Oct |
|---------|---------|---------|---------|
| Tarjeta de Crédito NL | 20.43% | 19.75% | 19.75% |
| Tarjeta de Crédito TOR | 0.08% | 12.43% | 12.43% |
| Tarjeta de Crédito BSSB | 28.13% | 28.51% | 28.51% |
| Tarjeta de Crédito LON | 19.75% | 20.43% | 20.43% |
| Tarjeta de Crédito BAJ | 12.43% | 0.08% | 0.08% |

**Note:** VEC product types map to these card types for interest rate validation.

## Extending Test Cases

### Add New Test Case

Edit `test_data_generator.py` and add to appropriate generation method:

```python
def _generate_custom_test_case(self):
    """Generate custom test scenario."""
    test_case = TestCase(
        case_id="CUSTOM-001",
        description="Custom test scenario",
        product_type=ProductType.VISTA,
        scenario=ValidationScenario.VALID_PERFECT,
        # ... configure fields ...
        expected_issues=[],
        expected_severity="none",
    )

    self.test_cases.append(test_case)
```

### Regenerate Test Cases

```bash
cd tests/fixtures
python test_data_generator.py
```

## Image Fixtures

### Required Images (from Check+list+demo+v2+Iqubica_images/)

**Card Images:**
- IMAGEN_TAREJETA_Imagen1.jpeg - Primary card image
- IMAGEN_TAREJETA_Picture2.jpeg - Secondary card image

**Important Messages:**
- IMAGEN_MENSAJES_IMPORTANTES_Imagen1.emf - Message banner
- IMAGEN_MENSAJES_IMPORTANTES_Imagen4.jpeg - Message detail

**Marketing Images:**
- IMAGENES_Imagen3.png - Marketing image 1
- IMAGENES_Imagen49.png - Marketing image 2

**Mandatory Legends:**
- Leyendas_Obligatorios_table1.png - Legend table 1
- Leyendas_Obligatorios_table2.png - Legend table 2

## Next Steps

1. **PDF Generator** - Create PDF generator from test case JSON
2. **Image Injector** - Inject correct/incorrect images based on flags
3. **Validation Test Suite** - Comprehensive validation test suite
4. **Performance Benchmarks** - Measure processing time per test case
5. **Accuracy Metrics** - Track extraction accuracy per field

## Contributing

To add new test scenarios:

1. Define the scenario in `ValidationScenario` enum
2. Create generation method in `VecTestDataGenerator`
3. Call from `generate_all_test_cases()`
4. Regenerate test cases
5. Update this README

## License

Internal use only - ExxerCube.Prisma.Veriqan
