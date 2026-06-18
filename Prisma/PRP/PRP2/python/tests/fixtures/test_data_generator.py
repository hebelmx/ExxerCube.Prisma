"""
Combinatorial test data generator for VEC statements.

Generates test cases with variations in:
- Product types (8 VEC product types)
- Valid/invalid scenarios for each validation rule
- Edge cases (missing fields, incorrect calculations, overlaps)
- Image compliance (logos, fonts, messages)
"""

from dataclasses import dataclass
from datetime import date, timedelta
from decimal import Decimal
from enum import Enum
from typing import Optional
import json
from pathlib import Path


class ProductType(str, Enum):
    """VEC product types for test generation."""
    VISTA = "Vista"
    RECOMPRA = "Recompra"
    REPORTO = "Reporto"
    CEDE = "CEDE"
    PAGARE = "Pagaré"
    UDIBONO = "UDIBONO"
    FONDOS = "Fondos"
    ACCIONES = "Acciones"


class ValidationScenario(str, Enum):
    """Validation test scenarios."""
    VALID_PERFECT = "valid_perfect"  # All validations pass
    VALID_WITHIN_TOLERANCE = "valid_within_tolerance"  # Within $0.00-0.50 tolerance
    INVALID_CALCULATION = "invalid_calculation"  # Math errors > $0.50
    INVALID_MISSING_FIELD = "invalid_missing_field"  # Required field missing
    INVALID_FORMAT = "invalid_format"  # Format violations
    INVALID_VISUAL = "invalid_visual"  # Visual compliance issues
    INVALID_FONT = "invalid_font"  # Font violations (non-Aptos)
    INVALID_OVERLAP = "invalid_overlap"  # Text overlap
    INVALID_IMAGE = "invalid_image"  # Wrong logo/card image


@dataclass
class TestCase:
    """VEC statement test case configuration."""
    case_id: str
    description: str
    product_type: ProductType
    scenario: ValidationScenario
    period_start: date
    period_end: date

    # Header fields
    account_number: str
    account_holder: str
    contract_number: str

    # Financial fields
    initial_balance: Decimal
    final_balance: Decimal
    total_deposits: Decimal
    total_withdrawals: Decimal
    interest_rate_annual: Optional[Decimal]

    # Expected validation results
    expected_issues: list[str]  # Issue codes expected
    expected_severity: str  # "critical", "high", "medium", "low"

    # Visual compliance
    use_correct_logo: bool = True
    use_correct_font: bool = True
    has_text_overlap: bool = False

    def to_dict(self) -> dict:
        """Convert to dictionary for JSON serialization."""
        return {
            "case_id": self.case_id,
            "description": self.description,
            "product_type": self.product_type.value,
            "scenario": self.scenario.value,
            "period_start": self.period_start.isoformat(),
            "period_end": self.period_end.isoformat(),
            "account_number": self.account_number,
            "account_holder": self.account_holder,
            "contract_number": self.contract_number,
            "initial_balance": str(self.initial_balance),
            "final_balance": str(self.final_balance),
            "total_deposits": str(self.total_deposits),
            "total_withdrawals": str(self.total_withdrawals),
            "interest_rate_annual": str(self.interest_rate_annual) if self.interest_rate_annual else None,
            "expected_issues": self.expected_issues,
            "expected_severity": self.expected_severity,
            "use_correct_logo": self.use_correct_logo,
            "use_correct_font": self.use_correct_font,
            "has_text_overlap": self.has_text_overlap,
        }


class VecTestDataGenerator:
    """Generate combinatorial test cases for VEC statements."""

    # Interest rates from validation checklist (TASA.csv)
    INTEREST_RATES = {
        ProductType.VISTA: {
            "Jul-Ago": Decimal("0.1975"),
            "Ago-Sep": Decimal("0.1975"),
            "Sep-Oct": Decimal("0.1975"),
        },
        # Simplified for now, can add all 5 products from CSV
    }

    def __init__(self):
        """Initialize test data generator."""
        self.test_cases: list[TestCase] = []

    def generate_all_test_cases(self) -> list[TestCase]:
        """Generate complete combinatorial test suite."""
        self.test_cases = []

        # 1. Valid test cases (one per product type)
        for product_type in ProductType:
            self._generate_valid_test_case(product_type)

        # 2. Calculation error test cases
        for product_type in [ProductType.VISTA, ProductType.CEDE]:
            self._generate_calculation_error_test_case(product_type)

        # 3. Missing field test cases
        for product_type in [ProductType.VISTA, ProductType.RECOMPRA]:
            self._generate_missing_field_test_case(product_type)

        # 4. Visual compliance test cases
        self._generate_visual_compliance_test_cases()

        # 5. Font violation test cases
        self._generate_font_violation_test_cases()

        # 6. Edge cases
        self._generate_edge_cases()

        return self.test_cases

    def _generate_valid_test_case(self, product_type: ProductType):
        """Generate a valid test case for a product type."""
        period_start = date(2025, 7, 1)
        period_end = date(2025, 7, 31)

        test_case = TestCase(
            case_id=f"VALID-{product_type.value}-001",
            description=f"Valid {product_type.value} statement - all validations pass",
            product_type=product_type,
            scenario=ValidationScenario.VALID_PERFECT,
            period_start=period_start,
            period_end=period_end,
            account_number=f"VEC-{product_type.value[:3].upper()}-123456",
            account_holder="JUAN PÉREZ GARCÍA",
            contract_number=f"CONT-{product_type.value[:3].upper()}-001",
            initial_balance=Decimal("50000.00"),
            final_balance=Decimal("52345.67"),
            total_deposits=Decimal("10000.00"),
            total_withdrawals=Decimal("7654.33"),
            interest_rate_annual=self._get_interest_rate(product_type, period_start),
            expected_issues=[],
            expected_severity="none",
            use_correct_logo=True,
            use_correct_font=True,
            has_text_overlap=False,
        )

        self.test_cases.append(test_case)

    def _generate_calculation_error_test_case(self, product_type: ProductType):
        """Generate test case with calculation errors > $0.50 tolerance."""
        period_start = date(2025, 8, 1)
        period_end = date(2025, 8, 31)

        # Create intentional calculation error
        initial_balance = Decimal("50000.00")
        deposits = Decimal("10000.00")
        withdrawals = Decimal("7654.33")
        # Correct final balance should be: 50000 + 10000 - 7654.33 = 52345.67
        # But we'll set it to 52400.00 (error of $54.33 > $0.50)
        incorrect_final_balance = Decimal("52400.00")

        test_case = TestCase(
            case_id=f"CALC-ERROR-{product_type.value}-001",
            description=f"Calculation error {product_type.value} - balance mismatch > $0.50",
            product_type=product_type,
            scenario=ValidationScenario.INVALID_CALCULATION,
            period_start=period_start,
            period_end=period_end,
            account_number=f"VEC-{product_type.value[:3].upper()}-ERR-001",
            account_holder="MARÍA LÓPEZ HERNÁNDEZ",
            contract_number=f"CONT-{product_type.value[:3].upper()}-ERR-001",
            initial_balance=initial_balance,
            final_balance=incorrect_final_balance,
            total_deposits=deposits,
            total_withdrawals=withdrawals,
            interest_rate_annual=self._get_interest_rate(product_type, period_start),
            expected_issues=["CALC-001"],  # Balance calculation error
            expected_severity="high",
            use_correct_logo=True,
            use_correct_font=True,
            has_text_overlap=False,
        )

        self.test_cases.append(test_case)

    def _generate_missing_field_test_case(self, product_type: ProductType):
        """Generate test case with missing required fields."""
        period_start = date(2025, 9, 1)
        period_end = date(2025, 9, 30)

        test_case = TestCase(
            case_id=f"MISSING-FIELD-{product_type.value}-001",
            description=f"Missing required field {product_type.value} - account holder name",
            product_type=product_type,
            scenario=ValidationScenario.INVALID_MISSING_FIELD,
            period_start=period_start,
            period_end=period_end,
            account_number=f"VEC-{product_type.value[:3].upper()}-MISS-001",
            account_holder="",  # MISSING - Required field
            contract_number=f"CONT-{product_type.value[:3].upper()}-MISS-001",
            initial_balance=Decimal("25000.00"),
            final_balance=Decimal("26500.00"),
            total_deposits=Decimal("5000.00"),
            total_withdrawals=Decimal("3500.00"),
            interest_rate_annual=self._get_interest_rate(product_type, period_start),
            expected_issues=["HDR-002"],  # Missing account holder
            expected_severity="critical",
            use_correct_logo=True,
            use_correct_font=True,
            has_text_overlap=False,
        )

        self.test_cases.append(test_case)

    def _generate_visual_compliance_test_cases(self):
        """Generate visual compliance violation test cases."""
        # Wrong logo test case
        test_case_logo = TestCase(
            case_id="VISUAL-LOGO-001",
            description="Visual compliance - incorrect logo",
            product_type=ProductType.VISTA,
            scenario=ValidationScenario.INVALID_IMAGE,
            period_start=date(2025, 7, 1),
            period_end=date(2025, 7, 31),
            account_number="VEC-VIS-LOGO-001",
            account_holder="CARLOS GONZÁLEZ MARTÍNEZ",
            contract_number="CONT-VIS-LOGO-001",
            initial_balance=Decimal("30000.00"),
            final_balance=Decimal("31000.00"),
            total_deposits=Decimal("2000.00"),
            total_withdrawals=Decimal("1000.00"),
            interest_rate_annual=Decimal("0.1975"),
            expected_issues=["IMG-001"],  # Logo missing or incorrect
            expected_severity="medium",
            use_correct_logo=False,  # INTENTIONAL ERROR
            use_correct_font=True,
            has_text_overlap=False,
        )

        self.test_cases.append(test_case_logo)

    def _generate_font_violation_test_cases(self):
        """Generate font violation test cases (non-Aptos font)."""
        test_case_font = TestCase(
            case_id="FONT-VIOLATION-001",
            description="Font violation - non-Aptos font used",
            product_type=ProductType.VISTA,
            scenario=ValidationScenario.INVALID_FONT,
            period_start=date(2025, 8, 1),
            period_end=date(2025, 8, 31),
            account_number="VEC-VIS-FONT-001",
            account_holder="ANA RAMÍREZ SÁNCHEZ",
            contract_number="CONT-VIS-FONT-001",
            initial_balance=Decimal("40000.00"),
            final_balance=Decimal("42000.00"),
            total_deposits=Decimal("3000.00"),
            total_withdrawals=Decimal("1000.00"),
            interest_rate_annual=Decimal("0.1975"),
            expected_issues=["FONT-001"],  # Unapproved font (non-Aptos)
            expected_severity="medium",
            use_correct_logo=True,
            use_correct_font=False,  # INTENTIONAL ERROR
            has_text_overlap=False,
        )

        self.test_cases.append(test_case_font)

        # Text overlap test case
        test_case_overlap = TestCase(
            case_id="FONT-OVERLAP-001",
            description="Text overlap violation",
            product_type=ProductType.VISTA,
            scenario=ValidationScenario.INVALID_OVERLAP,
            period_start=date(2025, 9, 1),
            period_end=date(2025, 9, 30),
            account_number="VEC-VIS-OVERLAP-001",
            account_holder="LUIS TORRES FLORES",
            contract_number="CONT-VIS-OVERLAP-001",
            initial_balance=Decimal("35000.00"),
            final_balance=Decimal("36500.00"),
            total_deposits=Decimal("2500.00"),
            total_withdrawals=Decimal("1000.00"),
            interest_rate_annual=Decimal("0.1975"),
            expected_issues=["FONT-004"],  # Text overlap detected
            expected_severity="medium",
            use_correct_logo=True,
            use_correct_font=True,
            has_text_overlap=True,  # INTENTIONAL ERROR
        )

        self.test_cases.append(test_case_overlap)

    def _generate_edge_cases(self):
        """Generate edge case test scenarios."""
        # Zero balance test case
        test_case_zero = TestCase(
            case_id="EDGE-ZERO-BALANCE-001",
            description="Edge case - zero balance throughout period",
            product_type=ProductType.VISTA,
            scenario=ValidationScenario.VALID_PERFECT,
            period_start=date(2025, 7, 1),
            period_end=date(2025, 7, 31),
            account_number="VEC-VIS-ZERO-001",
            account_holder="PATRICIA CRUZ MORALES",
            contract_number="CONT-VIS-ZERO-001",
            initial_balance=Decimal("0.00"),
            final_balance=Decimal("0.00"),
            total_deposits=Decimal("0.00"),
            total_withdrawals=Decimal("0.00"),
            interest_rate_annual=Decimal("0.1975"),
            expected_issues=[],
            expected_severity="none",
            use_correct_logo=True,
            use_correct_font=True,
            has_text_overlap=False,
        )

        self.test_cases.append(test_case_zero)

        # Large balance test case
        test_case_large = TestCase(
            case_id="EDGE-LARGE-BALANCE-001",
            description="Edge case - very large balance ($10M+)",
            product_type=ProductType.FONDOS,
            scenario=ValidationScenario.VALID_PERFECT,
            period_start=date(2025, 8, 1),
            period_end=date(2025, 8, 31),
            account_number="VEC-FON-LARGE-001",
            account_holder="ROBERTO FERNÁNDEZ VEGA",
            contract_number="CONT-FON-LARGE-001",
            initial_balance=Decimal("10000000.00"),
            final_balance=Decimal("10500000.00"),
            total_deposits=Decimal("1000000.00"),
            total_withdrawals=Decimal("500000.00"),
            interest_rate_annual=Decimal("0.05"),
            expected_issues=[],
            expected_severity="none",
            use_correct_logo=True,
            use_correct_font=True,
            has_text_overlap=False,
        )

        self.test_cases.append(test_case_large)

    def _get_interest_rate(self, product_type: ProductType, period_start: date) -> Optional[Decimal]:
        """Get interest rate for product type and period."""
        period_key = f"{period_start.strftime('%b')}-{(period_start + timedelta(days=30)).strftime('%b')}"

        if product_type in self.INTEREST_RATES:
            return self.INTEREST_RATES[product_type].get(period_key, Decimal("0.05"))

        # Default rates by product type
        default_rates = {
            ProductType.VISTA: Decimal("0.1975"),
            ProductType.RECOMPRA: Decimal("0.04"),
            ProductType.REPORTO: Decimal("0.04"),
            ProductType.CEDE: Decimal("0.06"),
            ProductType.PAGARE: Decimal("0.065"),
            ProductType.UDIBONO: Decimal("0.05"),
            ProductType.FONDOS: Decimal("0.05"),
            ProductType.ACCIONES: None,  # No interest
        }

        return default_rates.get(product_type, Decimal("0.05"))

    def export_to_json(self, output_path: Path):
        """Export test cases to JSON file."""
        test_cases_dict = [tc.to_dict() for tc in self.test_cases]

        with open(output_path, "w", encoding="utf-8") as f:
            json.dump(
                {
                    "total_cases": len(self.test_cases),
                    "generated_date": date.today().isoformat(),
                    "test_cases": test_cases_dict,
                },
                f,
                indent=2,
                ensure_ascii=False,
            )

    def export_summary(self, output_path: Path):
        """Export test case summary."""
        summary = {
            "total_cases": len(self.test_cases),
            "by_scenario": {},
            "by_product": {},
            "by_severity": {},
        }

        for tc in self.test_cases:
            # By scenario
            scenario_key = tc.scenario.value
            summary["by_scenario"][scenario_key] = summary["by_scenario"].get(scenario_key, 0) + 1

            # By product
            product_key = tc.product_type.value
            summary["by_product"][product_key] = summary["by_product"].get(product_key, 0) + 1

            # By severity
            severity_key = tc.expected_severity
            summary["by_severity"][severity_key] = summary["by_severity"].get(severity_key, 0) + 1

        with open(output_path, "w", encoding="utf-8") as f:
            json.dump(summary, f, indent=2, ensure_ascii=False)


if __name__ == "__main__":
    # Generate test cases
    generator = VecTestDataGenerator()
    test_cases = generator.generate_all_test_cases()

    print(f"Generated {len(test_cases)} test cases")

    # Export to JSON
    output_dir = Path(__file__).parent
    generator.export_to_json(output_dir / "vec_test_cases.json")
    generator.export_summary(output_dir / "vec_test_summary.json")

    print(f"Test cases exported to {output_dir / 'vec_test_cases.json'}")
    print(f"Summary exported to {output_dir / 'vec_test_summary.json'}")

    # Print summary
    print("\n=== Test Case Summary ===")
    for tc in test_cases:
        print(f"{tc.case_id}: {tc.description} [{tc.scenario.value}]")
