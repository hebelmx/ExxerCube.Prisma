#!/usr/bin/env python3
"""
Fix XUnit v3 Package References Script
=====================================

This script fixes XUnit v3 package references in all .csproj files within test directories.

Changes:
1. xunit -> xunit.v3
2. Meziantou.Extensions.Logging.Xunit -> Meziantou.Extensions.Logging.Xunit.v3

Usage: python fix_xunit_packages.py
"""

import os
import re
import glob
from pathlib import Path

def fix_xunit_packages():
    """Fix XUnit v3 package references in all test project files."""
    
    # Define the base directory
    base_dir = Path("F:/Dynamic/ExxerAi/ExxerAI/code/src/tests")
    
    # Regex patterns for package reference fixes
    patterns = [
        # Fix xunit package (exact match to avoid false positives)
        (
            r'<PackageReference Include="xunit" />',
            r'<PackageReference Include="xunit.v3" />'
        ),
        # Fix Meziantou package (exact match)
        (
            r'<PackageReference Include="Meziantou\.Extensions\.Logging\.Xunit" />',
            r'<PackageReference Include="Meziantou.Extensions.Logging.Xunit.v3" />'
        ),
        # Fix Meziantou package without escaping (in case the dot is literal)
        (
            r'<PackageReference Include="Meziantou.Extensions.Logging.Xunit" />',
            r'<PackageReference Include="Meziantou.Extensions.Logging.Xunit.v3" />'
        )
    ]
    
    # Find all .csproj files in test directories
    csproj_files = []
    for pattern in ["**/*.csproj"]:
        csproj_files.extend(glob.glob(str(base_dir / pattern), recursive=True))
    
    print(f"Found {len(csproj_files)} .csproj files in test directories")
    
    total_changes = 0
    modified_files = []
    
    for file_path in csproj_files:
        print(f"Processing: {file_path}")
        
        # Read the file
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()
        except Exception as e:
            print(f"  ERROR reading {file_path}: {e}")
            continue
        
        # Apply regex replacements
        original_content = content
        file_changes = 0
        
        for find_pattern, replace_pattern in patterns:
            matches = re.findall(find_pattern, content)
            if matches:
                content = re.sub(find_pattern, replace_pattern, content)
                file_changes += len(matches)
                print(f"  ✅ Fixed {len(matches)} occurrences of pattern: {find_pattern}")
        
        # Write back if changes were made
        if content != original_content:
            try:
                with open(file_path, 'w', encoding='utf-8') as f:
                    f.write(content)
                print(f"  ✅ Saved {file_changes} changes to {file_path}")
                total_changes += file_changes
                modified_files.append(file_path)
            except Exception as e:
                print(f"  ERROR writing {file_path}: {e}")
        else:
            print(f"  ✅ No changes needed")
    
    # Summary
    print(f"\n🎉 SUMMARY:")
    print(f"📁 Processed: {len(csproj_files)} .csproj files")
    print(f"📝 Modified: {len(modified_files)} files")
    print(f"🔧 Total changes: {total_changes}")
    
    if modified_files:
        print(f"\n📋 Modified files:")
        for file_path in modified_files:
            print(f"  - {file_path}")
    
    return len(modified_files), total_changes

def test_on_single_file():
    """Test the regex patterns on a single file first."""
    test_file = "F:/Dynamic/ExxerAi/ExxerAI/code/src/tests/00Domain/ExxerAI.Domain.Test/ExxerAI.Domain.Test.csproj"
    
    if not os.path.exists(test_file):
        print(f"❌ Test file not found: {test_file}")
        return False
    
    print(f"🧪 Testing on single file: {test_file}")
    
    # Read the file
    with open(test_file, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Check for patterns
    patterns = [
        r'<PackageReference Include="xunit" />',
        r'<PackageReference Include="Meziantou\.Extensions\.Logging\.Xunit" />',
        r'<PackageReference Include="Meziantou.Extensions.Logging.Xunit" />'
    ]
    
    found_patterns = []
    for pattern in patterns:
        matches = re.findall(pattern, content)
        if matches:
            found_patterns.append((pattern, len(matches)))
            print(f"  ✅ Found {len(matches)} matches for: {pattern}")
    
    if found_patterns:
        print(f"🎯 Test successful! Found {len(found_patterns)} patterns to fix")
        return True
    else:
        print(f"⚠️  No patterns found in test file")
        return False

if __name__ == "__main__":
    print("🚀 XUnit v3 Package Reference Fixer")
    print("=" * 50)
    
    # Test first
    print("Phase 1: Testing on single file...")
    if test_on_single_file():
        print("\nPhase 2: Applying fixes to all test projects...")
        modified_files, total_changes = fix_xunit_packages()
        
        if total_changes > 0:
            print(f"\n✅ SUCCESS: Fixed {total_changes} package references across {modified_files} files!")
            print("📋 Next step: Run 'dotnet restore' and 'dotnet build' to verify fixes")
        else:
            print(f"\n✅ All files already have correct XUnit v3 package references!")
    else:
        print("\n⚠️ Test failed - please check the file paths and patterns")