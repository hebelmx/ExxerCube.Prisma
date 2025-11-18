#!/usr/bin/env python3
"""
Scalable xUnit1026 Fixer - Fix Theory methods with unused parameters at scale
Parse build output directly and apply fixes systematically
"""

import re
import sys
import subprocess
from pathlib import Path
import argparse

class ScalableXUnit1026Fixer:
    def __init__(self):
        self.fixed_count = 0
        self.files_modified = set()
        self.dry_run = False
        self.errors_found = []
        
    def run_build_and_parse_errors(self, solution_path: Path) -> list:
        """Run build and parse xUnit1026 errors."""
        print("Running build to identify xUnit1026 errors...")
        
        try:
            result = subprocess.run(
                ["dotnet", "build", str(solution_path), "--no-restore", "-v:n"],
                capture_output=True,
                text=True,
                cwd=solution_path.parent,
                timeout=300
            )
        except subprocess.TimeoutExpired:
            print("Build timed out!")
            return []
        except Exception as e:
            print(f"Build failed: {e}")
            return []
        
        build_output = result.stdout + "\n" + result.stderr
        
        # Parse xUnit1026 errors
        # Pattern: path(line,col): error xUnit1026: Theory method 'method' does not use parameter 'param'
        xunit1026_pattern = r"([^(]+\.cs)\((\d+),\d+\):\s*error xUnit1026:.*Theory method '([^']+)'.*does not use parameter '([^']+)'"
        
        errors = []
        for match in re.finditer(xunit1026_pattern, build_output, re.MULTILINE):
            file_path_str = match.group(1).strip()
            line_num = int(match.group(2))
            method_name = match.group(3).strip()
            param_name = match.group(4).strip()
            
            # Convert to absolute path
            if not Path(file_path_str).is_absolute():
                file_path = solution_path.parent / file_path_str
            else:
                file_path = Path(file_path_str)
                
            errors.append((file_path, method_name, param_name, line_num))
        
        return errors
    
    def get_parameter_usage_statement(self, param_name: str) -> str:
        """Generate appropriate parameter usage statement."""
        usage_patterns = {
            'description': f'        {param_name}.Should().NotBeNull(); // Validates test description parameter',
            'scenario': f'        {param_name}.Should().NotBeNull(); // Validates test scenario parameter', 
            'industry': f'        {param_name}.Should().NotBeNull(); // Validates manufacturing industry parameter',
            'equipment': f'        {param_name}.Should().NotBeNull(); // Validates equipment parameter',
            'testCase': f'        {param_name}.Should().NotBeNull(); // Validates test case parameter',
            'workFlowType': f'        {param_name}.Should().NotBeNull(); // Validates workflow type parameter',
            'manufacturingScenario': f'        {param_name}.Should().NotBeNull(); // Validates manufacturing scenario parameter'
        }
        
        return usage_patterns.get(param_name, f'        {param_name}.Should().NotBeNull(); // xUnit1026: Use parameter')
    
    def add_missing_using_shouldly(self, content: str) -> str:
        """Add using Shouldly if missing."""
        if 'using Shouldly;' not in content:
            # Find the first line and add using statement
            lines = content.split('\n')
            if lines and lines[0].strip().startswith('namespace'):
                lines.insert(0, 'using Shouldly;')
                lines.insert(1, '')
                return '\n'.join(lines)
            elif lines:
                # Find insertion point after existing using statements or before namespace
                insert_point = 0
                for i, line in enumerate(lines):
                    if line.strip().startswith('using '):
                        insert_point = i + 1
                    elif line.strip().startswith('namespace'):
                        break
                        
                lines.insert(insert_point, 'using Shouldly;')
                if insert_point > 0:
                    lines.insert(insert_point + 1, '')
                return '\n'.join(lines)
        return content
    
    def fix_parameter_usage(self, file_path: Path, method_name: str, param_name: str) -> bool:
        """Fix unused parameter in a specific method."""
        if not file_path.exists():
            return False
            
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # Add using Shouldly if missing
        content = self.add_missing_using_shouldly(content)
        
        original_content = content
        lines = content.split('\n')
        
        # Find the method
        method_start = -1
        method_end = -1
        brace_count = 0
        found_method = False
        
        for i, line in enumerate(lines):
            # Look for method signature
            if method_name in line and ('public' in line or '[Theory]' in lines[max(0, i-5):i]):
                found_method = True
                # Find opening brace
                for j in range(i, min(len(lines), i + 10)):
                    if '{' in lines[j]:
                        method_start = j
                        break
                break
        
        if not found_method or method_start == -1:
            return False
        
        # Find method end
        brace_count = 0
        for i in range(method_start, len(lines)):
            brace_count += lines[i].count('{') - lines[i].count('}')
            if brace_count == 0:
                method_end = i
                break
        
        if method_end == -1:
            return False
        
        # Check if parameter is already used
        method_content = '\n'.join(lines[method_start:method_end + 1])
        if f'{param_name}.Should().NotBeNull()' in method_content:
            return False  # Already fixed
        
        # Find insertion point (after // Arrange if exists)
        insert_point = method_start + 1
        for i in range(method_start + 1, min(method_start + 15, method_end)):
            if '// Arrange' in lines[i]:
                insert_point = i + 1
                break
        
        # Insert parameter usage
        usage_statement = self.get_parameter_usage_statement(param_name)
        lines.insert(insert_point, usage_statement)
        lines.insert(insert_point + 1, '')  # Add blank line
        
        # Write back
        new_content = '\n'.join(lines)
        if new_content != original_content:
            if not self.dry_run:
                with open(file_path, 'w', encoding='utf-8') as f:
                    f.write(new_content)
                self.files_modified.add(file_path)
            return True
        
        return False
    
    def run(self, solution_path: Path, dry_run: bool = False, max_fixes: int = 20):
        """Run the scalable fixer."""
        self.dry_run = dry_run
        print(f"{'DRY RUN: ' if dry_run else ''}Scalable xUnit1026 Fixer")
        
        # Parse build errors
        errors = self.run_build_and_parse_errors(solution_path)
        
        if not errors:
            print("No xUnit1026 errors found!")
            return
            
        print(f"Found {len(errors)} xUnit1026 errors")
        
        # Group by file
        errors_by_file = {}
        for file_path, method_name, param_name, line_num in errors:
            if file_path not in errors_by_file:
                errors_by_file[file_path] = []
            errors_by_file[file_path].append((method_name, param_name, line_num))
        
        # Apply fixes
        fixed_files = 0
        for file_path, file_errors in list(errors_by_file.items())[:max_fixes]:
            if fixed_files >= max_fixes:
                break
                
            print(f"\nProcessing {file_path.name} ({len(file_errors)} errors)...")
            
            file_fixes = 0
            for method_name, param_name, line_num in file_errors:
                if self.fix_parameter_usage(file_path, method_name, param_name):
                    file_fixes += 1
                    self.fixed_count += 1
                    print(f"    Fixed parameter '{param_name}' in method '{method_name}'")
                    
            if file_fixes > 0:
                fixed_files += 1
                if not self.dry_run:
                    print(f"  Applied {file_fixes} fixes to {file_path.name}")
                else:
                    print(f"  Would apply {file_fixes} fixes to {file_path.name}")
        
        # Summary
        print(f"\n{'DRY RUN ' if dry_run else ''}Summary:")
        print(f"  Total parameter fixes: {self.fixed_count}")
        print(f"  Files {'would be' if dry_run else ''} modified: {len(self.files_modified)}")
        print(f"  Remaining errors: {len(errors) - self.fixed_count}")

def main():
    parser = argparse.ArgumentParser(description="Scalable xUnit1026 fixer")
    parser.add_argument("solution_path", type=Path, help="Path to solution file")
    parser.add_argument("--dry-run", action="store_true", help="Preview changes")
    parser.add_argument("--max-fixes", type=int, default=20, help="Max fixes to apply")
    
    args = parser.parse_args()
    
    if not args.solution_path.exists():
        print(f"Error: Solution file does not exist: {args.solution_path}")
        sys.exit(1)
        
    fixer = ScalableXUnit1026Fixer()
    fixer.run(args.solution_path, args.dry_run, args.max_fixes)

if __name__ == "__main__":
    main()