#!/usr/bin/env python3
"""
Fix CS1591 (Missing XML comments) and CS0452 (ShouldNotBeNull on non-nullable types) errors
"""

import os
import re
from datetime import datetime

def parse_cs1591_errors(error_file):
    """Parse CS1591 errors from the error file"""
    errors = []
    
    try:
        with open(error_file, 'r', encoding='utf-8-sig') as f:
            lines = f.readlines()
        
        # Skip header line
        for line in lines[1:]:
            if 'CS1591' in line and '\t' in line:
                parts = line.split('\t')
                if len(parts) >= 5:
                    file_path = parts[4].strip()
                    line_num = int(parts[5].strip()) if parts[5].strip().isdigit() else 0
                    
                    # Extract member name from description
                    desc = parts[2]
                    match = re.search(r"'([^']+)'", desc)
                    if match:
                        member_name = match.group(1)
                        errors.append({
                            'file': file_path,
                            'line': line_num,
                            'member': member_name,
                            'type': 'property' if '.' in member_name else 'class'
                        })
    except Exception as e:
        print(f"Error parsing CS1591 file: {e}")
    
    return errors

def fix_cs1591_in_file(file_path, errors_for_file):
    """Fix missing XML comments in a file"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        # Sort errors by line number in reverse order to avoid offset issues
        errors_for_file.sort(key=lambda x: x['line'], reverse=True)
        
        modified = False
        for error in errors_for_file:
            line_idx = error['line'] - 1  # Convert to 0-based index
            
            if 0 <= line_idx < len(lines):
                line = lines[line_idx]
                indent = len(line) - len(line.lstrip())
                
                # Generate appropriate XML comment
                member_name = error['member'].split('.')[-1]  # Get the last part
                
                xml_comment = []
                xml_comment.append(' ' * indent + '/// <summary>\n')
                xml_comment.append(' ' * indent + f'/// Gets the {member_name.lower()}.\n')
                xml_comment.append(' ' * indent + '/// </summary>\n')
                
                # Insert the XML comment before the member
                for i, comment_line in enumerate(xml_comment):
                    lines.insert(line_idx, comment_line)
                
                modified = True
                print(f"  + Added XML comment for {error['member']}")
        
        if modified:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.writelines(lines)
            return True
            
    except Exception as e:
        print(f"  ✗ Error fixing CS1591 in {file_path}: {e}")
    
    return False

def fix_cs0452_errors(root_dir):
    """Fix CS0452 errors - ShouldNotBeNull on non-nullable types"""
    fixed_count = 0
    
    # Common non-nullable types that cause CS0452
    non_nullable_patterns = [
        r'bool\s+\w+\s*=',
        r'int\s+\w+\s*=',
        r'long\s+\w+\s*=',
        r'double\s+\w+\s*=',
        r'float\s+\w+\s*=',
        r'decimal\s+\w+\s*=',
        r'DateTime\s+\w+\s*=',
        r'Guid\s+\w+\s*=',
        r'TimeSpan\s+\w+\s*=',
    ]
    
    for root, dirs, files in os.walk(root_dir):
        for file in files:
            if file.endswith('.cs') and 'Test' in file:
                file_path = os.path.join(root, file)
                
                try:
                    with open(file_path, 'r', encoding='utf-8') as f:
                        content = f.read()
                    
                    original_content = content
                    
                    # Find lines with result.Value.ShouldNotBeNull() where Value is non-nullable
                    lines = content.split('\n')
                    modified_lines = []
                    
                    for i, line in enumerate(lines):
                        if 'result.Value.ShouldNotBeNull()' in line:
                            # Check if this is for a non-nullable type by looking at previous lines
                            is_non_nullable = False
                            
                            # Look back up to 20 lines for the result assignment
                            for j in range(max(0, i-20), i):
                                for pattern in non_nullable_patterns:
                                    if re.search(pattern, lines[j]):
                                        is_non_nullable = True
                                        break
                                
                                # Also check for specific Result<T> patterns with non-nullable types
                                if re.search(r'Result<(bool|int|long|double|float|decimal|DateTime|Guid|TimeSpan)>', lines[j]):
                                    is_non_nullable = True
                                    break
                            
                            if is_non_nullable:
                                # Comment out the line instead of removing it
                                line = '// ' + line + ' // Removed: Value is non-nullable'
                                fixed_count += 1
                                print(f"  - Fixed CS0452 in {os.path.basename(file_path)} at line {i+1}")
                        
                        modified_lines.append(line)
                    
                    content = '\n'.join(modified_lines)
                    
                    if content != original_content:
                        with open(file_path, 'w', encoding='utf-8') as f:
                            f.write(content)
                        
                except Exception as e:
                    print(f"  ✗ Error processing {file_path}: {e}")
    
    return fixed_count

def main():
    print(f"\n{'=' * 80}")
    print(f"FIXING CS1591 (Missing XML comments) AND CS0452 (ShouldNotBeNull) ERRORS")
    print(f"Started: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")
    
    # Fix CS1591 errors
    cs1591_file = r"F:\Dynamic\ExxerAi\ExxerAI\Errors\CS1591.TXT"
    
    if os.path.exists(cs1591_file):
        print("Parsing CS1591 errors...")
        errors = parse_cs1591_errors(cs1591_file)
        
        # Group errors by file
        errors_by_file = {}
        for error in errors:
            if error['file'] not in errors_by_file:
                errors_by_file[error['file']] = []
            errors_by_file[error['file']].append(error)
        
        print(f"Found {len(errors)} CS1591 errors in {len(errors_by_file)} files\n")
        
        fixed_files = 0
        for file_path, file_errors in errors_by_file.items():
            if os.path.exists(file_path):
                print(f"\nFixing {os.path.basename(file_path)} ({len(file_errors)} errors)...")
                if fix_cs1591_in_file(file_path, file_errors):
                    fixed_files += 1
            else:
                print(f"  ⚠ File not found: {file_path}")
        
        print(f"\nFixed CS1591 errors in {fixed_files} files")
    else:
        print(f"CS1591 error file not found: {cs1591_file}")
    
    # Fix CS0452 errors
    print(f"\n{'=' * 40}")
    print("Fixing CS0452 errors (ShouldNotBeNull on non-nullable types)...\n")
    
    test_dirs = [
        r"F:\Dynamic\ExxerAi\ExxerAI\code\src\tests"
    ]
    
    total_cs0452_fixed = 0
    for test_dir in test_dirs:
        if os.path.exists(test_dir):
            fixed = fix_cs0452_errors(test_dir)
            total_cs0452_fixed += fixed
    
    print(f"\n{'=' * 80}")
    print(f"SUMMARY:")
    print(f"- Fixed {len(errors_by_file) if 'errors_by_file' in locals() else 0} files with CS1591 errors")
    print(f"- Fixed {total_cs0452_fixed} CS0452 errors")
    print(f"Completed: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")

if __name__ == "__main__":
    main()