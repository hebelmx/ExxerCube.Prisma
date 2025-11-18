#!/usr/bin/env python3
"""
Validation Script for Fixture Propagation Script

Validates that the propagate_vault_fixtures.py script is properly structured
and can be imported without errors.

Usage:
    python scripts/validate_propagation_script.py
"""

import sys
from pathlib import Path


def validate_script():
    """Validate the propagation script structure."""

    print("=" * 70)
    print("Fixture Propagation Script Validation")
    print("=" * 70)
    print()

    # Check script exists
    script_path = Path(__file__).parent / "propagate_vault_fixtures.py"

    if not script_path.exists():
        print(f"❌ Error: Script not found at {script_path}")
        return False

    print(f"✅ Script found: {script_path}")

    # Try to import the script
    try:
        import propagate_vault_fixtures
        print("✅ Script imports successfully")
    except Exception as e:
        print(f"❌ Error importing script: {str(e)}")
        return False

    # Validate key classes exist
    classes_to_check = [
        "OperationMode",
        "FixtureMapping",
        "PropagationResult",
        "VaultFixturePropagator"
    ]

    for class_name in classes_to_check:
        if hasattr(propagate_vault_fixtures, class_name):
            print(f"✅ Class exists: {class_name}")
        else:
            print(f"❌ Missing class: {class_name}")
            return False

    # Validate VaultFixturePropagator attributes
    propagator_attrs = [
        "VAULT_NAMESPACE",
        "TARGET_COMPONENTS",
        "FIXTURE_FILES"
    ]

    for attr in propagator_attrs:
        if hasattr(propagate_vault_fixtures.VaultFixturePropagator, attr):
            value = getattr(propagate_vault_fixtures.VaultFixturePropagator, attr)
            print(f"✅ Attribute exists: {attr} (value: {value if isinstance(value, str) else f'{len(value)} items'})")
        else:
            print(f"❌ Missing attribute: {attr}")
            return False

    # List fixture files
    print()
    print("Fixture files to propagate:")
    for fixture in propagate_vault_fixtures.VaultFixturePropagator.FIXTURE_FILES:
        print(f"  - {fixture}")

    print()
    print("Target components:")
    for component in propagate_vault_fixtures.VaultFixturePropagator.TARGET_COMPONENTS:
        print(f"  - {component}")

    print()
    print("=" * 70)
    print("✅ Validation Complete - Script is properly structured")
    print("=" * 70)

    return True


if __name__ == "__main__":
    success = validate_script()
    sys.exit(0 if success else 1)
