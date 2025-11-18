#!/usr/bin/env python3
"""
Fix remaining CS0452 errors from the error file
"""

import os
import re
from datetime import datetime

def parse_cs0452_errors(error_file):
    """Parse CS0452 errors from the error file"""
    errors = []
    
    try:
        with open(error_file, 'r', encoding='utf-8-sig') as f:
            lines = f.readlines()
        
        # Skip header line
        for line in lines[1:]:
            if 'CS0452' in line and '\t' in line:
                parts = line.split('\t')
                if len(parts) >= 5:
                    file_path = parts[4].strip()
                    line_num = int(parts[5].strip()) if parts[5].strip().isdigit() else 0
                    
                    errors.append({
                        'file': file_path,
                        'line': line_num
                    })
    except Exception as e:
        print(f"Error parsing CS0452 file: {e}")
    
    return errors

def fix_cs0452_in_file(file_path, line_numbers):
    """Fix CS0452 errors at specific line numbers in a file"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        modified = False
        # Sort line numbers in reverse order to avoid offset issues
        line_numbers.sort(reverse=True)
        
        for line_num in line_numbers:
            line_idx = line_num - 1  # Convert to 0-based index
            
            if 0 <= line_idx < len(lines):
                line = lines[line_idx]
                
                # Check if this line has result.Value.ShouldNotBeNull()
                if 'result.Value.ShouldNotBeNull()' in line and not line.strip().startswith('//'):
                    # Comment out the line
                    indent = len(line) - len(line.lstrip())
                    lines[line_idx] = ' ' * indent + '// ' + line.lstrip().rstrip() + ' // CS0452: Value is non-nullable\n'
                    modified = True
                    print(f"  - Fixed CS0452 at line {line_num}")
        
        if modified:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.writelines(lines)
            return True
            
    except Exception as e:
        print(f"  ✗ Error fixing {file_path}: {e}")
    
    return False

def main():
    print(f"\n{'=' * 80}")
    print(f"FIXING REMAINING CS0452 ERRORS FROM ERROR FILE")
    print(f"Started: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")
    
    cs0452_file = r"F:\Dynamic\ExxerAi\ExxerAI\Errors\CS0452.txt"
    
    if os.path.exists(cs0452_file):
        print("Parsing CS0452 errors...")
        errors = parse_cs0452_errors(cs0452_file)
        
        # Group errors by file
        errors_by_file = {}
        for error in errors:
            if error['file'] not in errors_by_file:
                errors_by_file[error['file']] = []
            errors_by_file[error['file']].append(error['line'])
        
        print(f"Found {len(errors)} CS0452 errors in {len(errors_by_file)} files\n")
        
        fixed_files = 0
        total_fixed = 0
        
        for file_path, line_numbers in errors_by_file.items():
            if os.path.exists(file_path):
                print(f"\nFixing {os.path.basename(file_path)} ({len(line_numbers)} errors)...")
                if fix_cs0452_in_file(file_path, line_numbers):
                    fixed_files += 1
                    total_fixed += len(line_numbers)
            else:
                print(f"  ⚠ File not found: {file_path}")
        
        print(f"\n{'=' * 80}")
        print(f"SUMMARY:")
        print(f"- Fixed {total_fixed} CS0452 errors in {fixed_files} files")
        print(f"Completed: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
        print(f"{'=' * 80}\n")
    else:
        print(f"CS0452 error file not found: {cs0452_file}")

if __name__ == "__main__":
    main()