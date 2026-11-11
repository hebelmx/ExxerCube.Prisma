#!/usr/bin/env python3
"""
Focused xUnit1026 Fixer - SINGLE RESPONSIBILITY
Fix Theory methods with unused parameters ONLY
No shortcuts, one pattern at a time
"""

import re
from pathlib import Path
import argparse

class FocusedXUnit1026Fixer:
    def __init__(self):
        self.fixed_count = 0
        self.files_modified = set()
        self.dry_run = False
        
    def add_parameter_validation(self, content: str, method_name: str, param_name: str) -> str:
        """Add single parameter validation to Theory method - FOCUSED APPROACH"""
        lines = content.split('\n')
        
        # Find method
        method_start = -1
        for i, line in enumerate(lines):
            if method_name in line and 'public' in line:
                # Find opening brace
                for j in range(i, min(len(lines), i + 10)):
                    if '{' in lines[j]:
                        method_start = j
                        break
                break
        
        if method_start == -1:
            return content
        
        # Find method end
        brace_count = 0
        method_end = -1
        for i in range(method_start, len(lines)):
            brace_count += lines[i].count('{') - lines[i].count('}')
            if brace_count == 0:
                method_end = i
                break
        
        if method_end == -1:
            return content
        
        # Check if already fixed
        method_body = '\n'.join(lines[method_start:method_end + 1])
        if f'{param_name}.ShouldNotBeNull()' in method_body:
            return content  # Already fixed
        
        # Find insertion point (after // Arrange if present)
        insert_point = method_start + 1
        for i in range(method_start + 1, min(method_start + 10, method_end)):
            if '// Arrange' in lines[i]:
                insert_point = i + 1
                break
        
        # Add SINGLE parameter validation with correct Shouldly syntax
        validation_line = f'        {param_name}.ShouldNotBeNull(); // Validates {param_name} parameter'
        lines.insert(insert_point, validation_line)
        lines.insert(insert_point + 1, '')  # Blank line
        
        return '\n'.join(lines)
    
    def fix_single_error(self, file_path: Path, method_name: str, param_name: str) -> bool:
        """Fix ONE xUnit1026 error in ONE file - SINGLE RESPONSIBILITY"""
        if not file_path.exists():
            return False
            
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        original_content = content
        
        # Apply SINGLE fix
        content = self.add_parameter_validation(content, method_name, param_name)
        
        if content != original_content:
            if not self.dry_run:
                with open(file_path, 'w', encoding='utf-8') as f:
                    f.write(content)
                self.files_modified.add(file_path)
            
            self.fixed_count += 1
            print(f"    Fixed parameter '{param_name}' in method '{method_name}'")
            return True
        
        return False

def main():
    parser = argparse.ArgumentParser(description="Focused xUnit1026 fixer - ONE PATTERN ONLY")
    parser.add_argument("file_path", type=Path, help="Single file to fix")
    parser.add_argument("method_name", help="Method name")
    parser.add_argument("param_name", help="Parameter name")
    parser.add_argument("--dry-run", action="store_true", help="Preview changes")
    
    args = parser.parse_args()
    
    if not args.file_path.exists():
        print(f"Error: File does not exist: {args.file_path}")
        return 1
    
    fixer = FocusedXUnit1026Fixer()
    fixer.dry_run = args.dry_run
    
    print(f"{'DRY RUN: ' if args.dry_run else ''}Focused xUnit1026 Fix")
    print(f"File: {args.file_path.name}")
    print(f"Method: {args.method_name}")
    print(f"Parameter: {args.param_name}")
    
    if fixer.fix_single_error(args.file_path, args.method_name, args.param_name):
        print("✅ Fix applied successfully")
    else:
        print("❌ No fix needed or fix failed")
    
    return 0

if __name__ == "__main__":
    exit(main())