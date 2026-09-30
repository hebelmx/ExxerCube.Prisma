#!/usr/bin/env python3
"""
CS8625 fixer - Cannot convert null literal to non-nullable reference type.
Uses exact error locations from build output.
"""

import re
from pathlib import Path
from typing import List, Dict, Tuple
import sys

class CS8625Fixer:
    """Fix CS8625: Cannot convert null literal to non-nullable reference type"""
    
    def __init__(self, error_file: str, dry_run: bool = True):
        self.error_file = error_file
        self.dry_run = dry_run
        self.fixes_applied = 0
        
    def parse_errors(self) -> List[Dict]:
        """Parse CS8625 errors from error file"""
        errors = []
        
        with open(self.error_file, 'r', encoding='utf-8') as f:
            content = f.read()
            
        # Pattern: file.cs(line,col): error CS8625: Cannot convert null literal to non-nullable reference type.
        pattern = r'^\s*\d+>(.+\.cs)\((\d+),(\d+)\):\s*error CS8625:'
        
        for match in re.finditer(pattern, content, re.MULTILINE):
            errors.append({
                'file': match.group(1).strip(),
                'line': int(match.group(2)),
                'column': int(match.group(3))
            })
            
        return errors
        
    def fix_null_literal(self, file_path: str, line_num: int, column: int) -> bool:
        """Fix a single null literal issue"""
        
        # Read file
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
            
        if line_num > len(lines):
            print(f"    ERROR: Line {line_num} out of range")
            return False
            
        line_idx = line_num - 1
        line = lines[line_idx]
        
        # Common patterns for null in method calls
        # 1. Simple null parameter: method(null) -> method(null!)
        # 2. Multiple params: method(param1, null, param3) -> method(param1, null!, param3)
        
        # Find the null at or near the column
        # Column points to the start of 'null'
        null_pos = line.find('null', max(0, column - 10))
        
        if null_pos == -1:
            print(f"    ERROR: Could not find 'null' near column {column}")
            return False
            
        # Check if it's already null! or inside a string
        if line[null_pos:null_pos+5] == 'null!' or '"' in line[max(0, null_pos-5):null_pos]:
            print(f"    SKIP: Already fixed or inside string")
            return False
            
        # Apply the fix - add ! after null
        new_line = line[:null_pos+4] + '!' + line[null_pos+4:]
        
        if not self.dry_run:
            lines[line_idx] = new_line
            with open(file_path, 'w', encoding='utf-8') as f:
                f.writelines(lines)
                
        return True
        
    def run(self) -> Dict:
        """Run the fixer"""
        print(f"\nCS8625 Fixer - {'DRY RUN' if self.dry_run else 'LIVE'}")
        print("=" * 60)
        
        errors = self.parse_errors()
        print(f"Found {len(errors)} CS8625 errors")
        
        # Group by file for efficiency
        files_errors = {}
        for error in errors:
            if error['file'] not in files_errors:
                files_errors[error['file']] = []
            files_errors[error['file']].append(error)
            
        # Process each file
        for file_path, file_errors in files_errors.items():
            print(f"\n  Processing: {Path(file_path).name} ({len(file_errors)} errors)")
            
            # Sort by line number (descending) to maintain positions
            file_errors.sort(key=lambda e: e['line'], reverse=True)
            
            fixed_count = 0
            for error in file_errors:
                print(f"    Line {error['line']}, Column {error['column']}: ", end='')
                
                if self.fix_null_literal(file_path, error['line'], error['column']):
                    fixed_count += 1
                    print("[FIXED]" if not self.dry_run else "[WOULD FIX]")
                else:
                    print("[SKIPPED]")
                    
            if fixed_count > 0:
                self.fixes_applied += fixed_count
                print(f"    Total: {fixed_count} fixes")
                
        print(f"\n\nSummary:")
        print(f"  Total {'fixes identified' if self.dry_run else 'fixes applied'}: {self.fixes_applied}")
        
        return {'fixes_applied': self.fixes_applied}

def main():
    """Main entry point"""
    import argparse
    
    parser = argparse.ArgumentParser(description='Fix CS8625 errors')
    parser.add_argument('--error-file', default='final_errors.txt',
                       help='File containing build errors')
    parser.add_argument('--dry-run', action='store_true', default=True,
                       help='Dry run mode (default: True)')
    parser.add_argument('--apply', action='store_true',
                       help='Actually apply the fixes')
    
    args = parser.parse_args()
    
    # Create fixer
    fixer = CS8625Fixer(args.error_file, dry_run=not args.apply)
    
    # Run
    results = fixer.run()
    
    sys.exit(0)

if __name__ == '__main__':
    main()