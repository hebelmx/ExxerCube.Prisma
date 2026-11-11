#!/usr/bin/env python3
"""
Fix CS8602 null reference warnings
Add proper null checks for Result.Value and Result.Errors
"""

import os
import re
from datetime import datetime

def fix_null_reference_in_tests(file_path):
    """Fix null reference warnings in test files by adding ShouldNotBeNull() assertions"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        modified = False
        i = 0
        while i < len(lines):
            line = lines[i]
            
            # Check for Result.Value access without prior null check
            if 'result.Value' in line and 'ShouldNotBeNull()' not in line:
                # Look back to see if there's already a ShouldNotBeNull check
                has_null_check = False
                for j in range(max(0, i-5), i):
                    if 'result.Value.ShouldNotBeNull()' in lines[j]:
                        has_null_check = True
                        break
                
                if not has_null_check:
                    # Insert null check before the current line
                    indent = len(line) - len(line.lstrip())
                    null_check = ' ' * indent + 'result.Value.ShouldNotBeNull();\n'
                    lines.insert(i, null_check)
                    i += 1  # Skip the inserted line
                    modified = True
                    print(f"  - Added result.Value.ShouldNotBeNull() before line {i}")
            
            # Check for Result.Errors access without prior null check
            elif 'result.Errors' in line and 'ShouldNotBeNull()' not in line:
                # Look back to see if there's already a ShouldNotBeNull check
                has_null_check = False
                for j in range(max(0, i-5), i):
                    if 'result.Errors.ShouldNotBeNull()' in lines[j]:
                        has_null_check = True
                        break
                
                if not has_null_check:
                    # Insert null check before the current line
                    indent = len(line) - len(line.lstrip())
                    null_check = ' ' * indent + 'result.Errors.ShouldNotBeNull();\n'
                    lines.insert(i, null_check)
                    i += 1  # Skip the inserted line
                    modified = True
                    print(f"  - Added result.Errors.ShouldNotBeNull() before line {i}")
            
            # Check for Result.Error access without prior null check
            elif 'result.Error' in line and 'ShouldNotBeNull()' not in line and 'result.Errors' not in line:
                # Look back to see if there's already a ShouldNotBeNull check
                has_null_check = False
                for j in range(max(0, i-5), i):
                    if 'result.Error.ShouldNotBeNull()' in lines[j]:
                        has_null_check = True
                        break
                
                if not has_null_check:
                    # Insert null check before the current line
                    indent = len(line) - len(line.lstrip())
                    null_check = ' ' * indent + 'result.Error.ShouldNotBeNull();\n'
                    lines.insert(i, null_check)
                    i += 1  # Skip the inserted line
                    modified = True
                    print(f"  - Added result.Error.ShouldNotBeNull() before line {i}")
            
            i += 1
        
        if modified:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.writelines(lines)
            print(f"  ✓ Fixed null reference warnings in {os.path.basename(file_path)}")
        
        return modified
    except Exception as e:
        print(f"  ✗ Error fixing {file_path}: {e}")
        return False

def fix_null_reference_in_production(file_path):
    """Fix null reference warnings in production code by adding null checks"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        modified = False
        
        # Pattern to find result.Value access without null check
        pattern = r'(\s*)(.+?)result\.Value\.(\w+)'
        
        def add_null_check(match):
            indent = match.group(1)
            prefix = match.group(2)
            property = match.group(3)
            
            # Check if it's already inside an if statement
            if 'if' in prefix:
                return match.group(0)
            
            # Add null check
            return f"{indent}if (result.Value is not null)\n{indent}{{\n{indent}    {prefix}result.Value.{property}"
        
        # Apply the pattern
        new_content = re.sub(pattern, add_null_check, content)
        
        # Also handle result.Errors
        pattern2 = r'(\s*)(.+?)result\.Errors\.(\w+)'
        
        def add_errors_null_check(match):
            indent = match.group(1)
            prefix = match.group(2)
            property = match.group(3)
            
            # Check if it's already inside an if statement
            if 'if' in prefix:
                return match.group(0)
            
            # Add null check
            return f"{indent}if (result.Errors is not null)\n{indent}{{\n{indent}    {prefix}result.Errors.{property}"
        
        new_content = re.sub(pattern2, add_errors_null_check, new_content)
        
        if new_content != content:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(new_content)
            modified = True
            print(f"  ✓ Fixed null reference warnings in {os.path.basename(file_path)}")
        
        return modified
    except Exception as e:
        print(f"  ✗ Error fixing {file_path}: {e}")
        return False

def main():
    print(f"\n{'=' * 80}")
    print(f"FIXING CS8602 NULL REFERENCE WARNINGS")
    print(f"Started: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")
    
    # Specific file from the error list
    test_file = r"F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\05IntegrationTests\ExxerAI.EnhancedRag.Integration.Test\Services\RerankingServiceTests.cs"
    
    print("Fixing test file:")
    if os.path.exists(test_file):
        fix_null_reference_in_tests(test_file)
    else:
        print(f"  ⚠ File not found: {test_file}")
    
    # Search for more files with potential null reference issues
    print("\nSearching for more files with Result.Value or Result.Errors usage...")
    
    # Find all test files
    test_dirs = [
        r"F:\Dynamic\ExxerAi\ExxerAI\code\src\tests",
    ]
    
    fixed_count = 0
    for test_dir in test_dirs:
        if os.path.exists(test_dir):
            for root, dirs, files in os.walk(test_dir):
                for file in files:
                    if file.endswith('.cs') and 'Test' in file:
                        file_path = os.path.join(root, file)
                        try:
                            with open(file_path, 'r', encoding='utf-8') as f:
                                content = f.read()
                            
                            # Check if file contains result.Value or result.Errors
                            if 'result.Value' in content or 'result.Errors' in content or 'result.Error' in content:
                                print(f"\nChecking: {os.path.basename(file_path)}")
                                if fix_null_reference_in_tests(file_path):
                                    fixed_count += 1
                        except:
                            pass
    
    print(f"\n{'=' * 80}")
    print(f"SUMMARY: Fixed {fixed_count} files")
    print(f"Completed: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")
    
    print("Note: For production code, null checks should be added as:")
    print("  if (result.Value is not null) { ... }")
    print("  if (result.Errors is not null) { ... }")

if __name__ == "__main__":
    main()