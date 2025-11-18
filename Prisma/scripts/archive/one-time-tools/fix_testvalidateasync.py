#!/usr/bin/env python3
"""
Fix TestValidateAsync calls that pass CancellationToken as 3rd parameter
The method only accepts 2 parameters.
"""

import re
import sys
from pathlib import Path
import argparse

def fix_file(file_path: Path, dry_run: bool = False) -> int:
    """Fix TestValidateAsync calls in a single file."""
    if not file_path.exists():
        return 0
        
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
        
    original = content
    
    # Pattern: TestValidateAsync(command, CancellationToken) -> TestValidateAsync(command)
    # The error says it expects ValidationStrategy as 3rd param, meaning we're passing 2 params
    # when it expects either 1 param or 2 params with the 2nd being ValidationStrategy
    pattern = r'(\.TestValidateAsync\s*\([^,)]+),\s*TestContext\.Current\.CancellationToken\s*\)'
    replacement = r'\1)'
    
    content = re.sub(pattern, replacement, content)
    
    if content != original:
        count = len(re.findall(pattern, original))
        print(f"  Fixed {count} TestValidateAsync calls in {file_path.name}")
        if not dry_run:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(content)
        return count
    return 0

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("target_dir", type=Path)
    parser.add_argument("--dry-run", action="store_true")
    
    args = parser.parse_args()
    
    # Target specific files with this error
    files = [
        "Features/ConfigApps/UpdateConfigAppValidatorTests.cs",
        "Features/ConfigApps/GetConfigAppsDetailQueryValidatorTests.cs",
        "Features/ConfigApps/GetConfigAppsListQueryValidatorTests.cs",
        "Features/Barcodes/BarCodeDetailsValidatorTests.cs",
        "Features/ConfigStations/GetConfigStationListQueryValidatorTests.cs",
        "Features/ConfigStations/GetConfigStationDetailQueryValidatorTests.cs",
        "Queries/GetEventsListQueryValidatorTests.cs",
        "Features/Cycles/UpdateCyclesOkCommandValidatorTests.cs",
        "Features/Cycles/CreateCyclesCommandValidatorTests.cs",
        "Features/WorkFlows/UpdateWorkFlowValidatorTests.cs",
        "Features/Cycles/UpdateCyclesNotOkCommandValidatorTests.cs",
    ]
    
    print(f"{'DRY RUN: ' if args.dry_run else ''}Fixing TestValidateAsync calls")
    total = 0
    
    for rel_path in files:
        file_path = args.target_dir / rel_path
        if file_path.exists():
            count = fix_file(file_path, args.dry_run)
            total += count
            
    print(f"\nTotal fixes: {total}")

if __name__ == "__main__":
    main()