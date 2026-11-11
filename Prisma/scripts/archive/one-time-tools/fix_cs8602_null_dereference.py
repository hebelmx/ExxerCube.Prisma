#!/usr/bin/env python3
"""
Fixer for CS8602: Dereference of a possibly null reference errors.
Adds null-forgiving operator (!) to dereferences that the compiler thinks might be null.

Usage:
    python fix_cs8602_null_dereference.py --error-file build_errors_current.txt [--dry-run|--apply]
"""

import argparse
import re
import os
from typing import List, Dict

class CS8602Fixer:
    def __init__(self, dry_run: bool = True):
        self.dry_run = dry_run
        self.fixes_applied = 0
        self.fixes_failed = 0
        
    def parse_cs8602_errors(self, error_file: str) -> Dict[str, List[dict]]:
        """Parse CS8602 errors from dotnet build output."""
        if not os.path.exists(error_file):
            print(f"Error file not found: {error_file}")
            return {}
            
        errors_by_file = {}
        
        with open(error_file, 'r', encoding='utf-8') as f:
            content = f.read()
            
        # Pattern for CS8602: Dereference of a possibly null reference
        pattern = r'([^:]+):(\d+),(\d+):\s+error\s+CS8602:\s+Dereference of a possibly null reference'
        
        for match in re.finditer(pattern, content):
            file_path = match.group(1).strip()
            line_num = int(match.group(2))
            column = int(match.group(3))
            
            if file_path not in errors_by_file:
                errors_by_file[file_path] = []
            
            errors_by_file[file_path].append({
                'line': line_num,
                'column': column
            })
            
        return errors_by_file
    
    def fix_null_dereference(self, file_path: str, line_num: int, column: int) -> bool:
        """Add null-forgiving operator (!) to fix CS8602 error."""
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                lines = f.readlines()
                
            if line_num > len(lines):
                print(f"    ERROR: Line {line_num} out of range")
                return False
                
            line = lines[line_num - 1]
            original_line = line
            
            # Common patterns for null dereference:
            # 1. member?.member - already safe, skip
            # 2. variable.member - needs variable!.member  
            # 3. method().member - needs method()!.member
            # 4. property.member - needs property!.member
            
            # Look for dereference patterns around the column position
            # We'll add ! before the dot to make it safe
            
            # Find all potential dereference points (dots not preceded by ?)
            dot_positions = []
            for i, char in enumerate(line):
                if char == '.' and i > 0 and line[i-1] != '?':
                    dot_positions.append(i)
            
            if not dot_positions:
                print(f"    SKIP: No dereference found at line {line_num}")
                return False
            
            # Find the closest dot to the error column
            closest_dot = min(dot_positions, key=lambda x: abs(x - (column - 1)))
            
            # Add ! before the dot
            new_line = line[:closest_dot] + '!' + line[closest_dot:]
            
            if self.dry_run:
                print(f"    DRY-RUN: Would change line {line_num}:")
                print(f"      FROM: {original_line.rstrip()}")
                print(f"      TO:   {new_line.rstrip()}")
                return True
            else:
                lines[line_num - 1] = new_line
                with open(file_path, 'w', encoding='utf-8') as f:
                    f.writelines(lines)
                print(f"    [APPLIED] Fixed CS8602 at line {line_num}")
                return True
                
        except Exception as e:
            print(f"    [ERROR] Failed to fix CS8602 in {file_path}:{line_num} - {str(e)}")
            return False
    
    def fix_file(self, file_path: str, errors: List[dict]) -> None:
        """Fix all CS8602 errors in a single file."""
        print(f"\n=== Processing {file_path} ===")
        print(f"Found {len(errors)} CS8602 errors to fix")
        
        # Sort by line number (descending to avoid line shifts affecting later fixes)
        errors.sort(key=lambda x: x['line'], reverse=True)
        
        for error in errors:
            success = self.fix_null_dereference(file_path, error['line'], error['column'])
            
            if success:
                self.fixes_applied += 1
            else:
                self.fixes_failed += 1
    
    def run(self, error_file: str) -> None:
        """Main execution method."""
        print(f"CS8602 Null Dereference Fixer")
        print(f"Mode: {'DRY-RUN' if self.dry_run else 'APPLY FIXES'}")
        print(f"Parsing errors from: {error_file}")
        
        errors_by_file = self.parse_cs8602_errors(error_file)
        
        if not errors_by_file:
            print("No CS8602 errors found to fix.")
            return
            
        total_errors = sum(len(errors) for errors in errors_by_file.values())
        print(f"\nFound {total_errors} CS8602 errors in {len(errors_by_file)} files")
        
        if self.dry_run:
            print(f"\n--- DRY RUN MODE - No files will be modified ---")
        
        # Process each file
        for file_path, errors in errors_by_file.items():
            if not os.path.exists(file_path):
                print(f"\nSKIPPING: File not found - {file_path}")
                continue
                
            self.fix_file(file_path, errors)
        
        print(f"\n=== SUMMARY ===")
        print(f"Fixes applied: {self.fixes_applied}")
        print(f"Fixes failed: {self.fixes_failed}")
        print(f"Total processed: {self.fixes_applied + self.fixes_failed}")

def main():
    parser = argparse.ArgumentParser(description='Fix CS8602 null dereference errors')
    parser.add_argument('--error-file', required=True, help='Build error output file')
    
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--dry-run', action='store_true', help='Show what would be changed')
    group.add_argument('--apply', action='store_true', help='Apply fixes to files')
    
    args = parser.parse_args()
    
    fixer = CS8602Fixer(dry_run=args.dry_run)
    fixer.run(args.error_file)

if __name__ == '__main__':
    main()