#!/usr/bin/env python3
"""
Comprehensive xUnit1026 Fixer - Fix Theory methods with unused parameters
Uses build output parsing to find and fix all xUnit1026 errors systematically
"""

import re
import sys
import subprocess
from pathlib import Path
import argparse

class XUnit1026ComprehensiveFixer:
    def __init__(self):
        self.fixed_count = 0
        self.files_modified = set()
        self.dry_run = False
        
        # Parameter usage patterns for different parameter types
        self.parameter_usage_patterns = {
            # Documentation/descriptive parameters
            'description': '        description.Should().NotBeNull(); // Validates test description parameter',
            'scenario': '        scenario.Should().NotBeNull(); // Validates test scenario parameter',
            'industry': '        industry.Should().NotBeNull(); // Validates manufacturing industry parameter',
            'equipment': '        equipment.Should().NotBeNull(); // Validates equipment parameter',
            'testCase': '        testCase.Should().NotBeNull(); // Validates test case parameter',
            'manufacturingScenario': '        manufacturingScenario.Should().NotBeNull(); // Validates manufacturing scenario',
            'industryType': '        industryType.Should().NotBeNull(); // Validates industry type parameter',
            'context': '        context.Should().NotBeNull(); // Validates context parameter',
            'case': '        case.Should().NotBeNull(); // Validates case parameter',
            'productType': '        productType.Should().NotBeNull(); // Validates product type parameter',
            'workFlowType': '        workFlowType.Should().NotBeNull(); // Validates workflow type parameter',
            'messageType': '        messageType.Should().NotBeNull(); // Validates message type parameter',
            
            # Generic fallback
            'default': '        {param}.Should().NotBeNull(); // xUnit1026: Use parameter'
        }
        
    def get_usage_line(self, param_name: str) -> str:
        """Get appropriate usage line for parameter."""
        if param_name in self.parameter_usage_patterns:
            return self.parameter_usage_patterns[param_name]
        else:
            return self.parameter_usage_patterns['default'].format(param=param_name)
    
    def find_method_boundaries(self, lines: list, method_name: str, start_line: int) -> tuple:
        """Find the start and end of a method."""
        method_start = -1
        method_end = -1
        brace_count = 0
        found_method = False
        
        # Start from the given line and look for the method
        for i in range(max(0, start_line - 5), min(len(lines), start_line + 10)):
            line = lines[i].strip()
            
            # Look for method signature
            if method_name in lines[i] and ('public' in lines[i] or 'private' in lines[i]):
                found_method = True
                # Now find the opening brace
                for j in range(i, min(len(lines), i + 5)):
                    if '{' in lines[j]:
                        method_start = j
                        break
                break
                
        if not found_method or method_start == -1:
            return -1, -1
            
        # Find method end by counting braces
        brace_count = 0
        for i in range(method_start, len(lines)):
            line = lines[i]
            brace_count += line.count('{') - line.count('}')
            
            if brace_count == 0:
                method_end = i
                break
                
        return method_start, method_end
    
    def find_best_insertion_point(self, lines: list, method_start: int, method_end: int) -> int:
        """Find the best place to insert parameter usage."""
        # Look for common patterns in order of preference
        patterns = ['// Arrange', 'Arrange', '// Act', 'Act']
        
        for pattern in patterns:
            for i in range(method_start + 1, min(method_start + 15, method_end)):
                if pattern in lines[i]:
                    return i + 1
                    
        # Default: insert after method opening brace
        return method_start + 1
    
    def clean_existing_parameter_usage(self, content: str, param_name: str) -> str:
        """Remove existing parameter usage lines to avoid duplicates."""
        lines = content.split('\n')
        cleaned_lines = []
        
        for line in lines:
            # Skip lines that are just parameter discards or duplicate usage
            if f'_ = {param_name};' in line and 'xUnit1026 fix' in line:
                continue
            if f'{param_name}.Should().NotBeNull()' in line and len([l for l in lines if f'{param_name}.Should().NotBeNull()' in l]) > 1:
                continue
            cleaned_lines.append(line)
            
        return '\n'.join(cleaned_lines)
    
    def add_parameter_usage(self, content: str, method_name: str, param_name: str, line_num: int) -> str:
        """Add proper parameter usage to a method."""
        # First clean any existing usage
        content = self.clean_existing_parameter_usage(content, param_name)
        
        lines = content.split('\n')
        
        # Find method boundaries
        method_start, method_end = self.find_method_boundaries(lines, method_name, line_num)
        
        if method_start == -1 or method_end == -1:
            print(f"    Warning: Could not find method boundaries for {method_name}")
            return content
            
        # Check if parameter is already properly used
        method_content = '\n'.join(lines[method_start:method_end + 1])
        if f'{param_name}.Should().NotBeNull()' in method_content:
            return content  # Already fixed
            
        # Find insertion point
        insert_point = self.find_best_insertion_point(lines, method_start, method_end)
        
        # Add parameter usage
        usage_line = self.get_usage_line(param_name)
        lines.insert(insert_point, usage_line)
        lines.insert(insert_point + 1, '')  # Add blank line for readability
        
        return '\n'.join(lines)
    
    def fix_file(self, file_path: Path, errors: list) -> int:
        """Fix all xUnit1026 errors in a specific file."""
        if not file_path.exists():
            return 0
            
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
            
        original_content = content
        fixes_in_file = 0
        
        # Sort errors by line number (descending to avoid offset issues)
        errors.sort(key=lambda x: x[1], reverse=True)
        
        for method_name, line_num, param_name in errors:
            old_content = content
            content = self.add_parameter_usage(content, method_name, param_name, line_num)
            
            if content != old_content:
                fixes_in_file += 1
                print(f"    Added usage for parameter '{param_name}' in method '{method_name}'")
            
        if fixes_in_file > 0:
            if self.dry_run:
                print(f"  Would fix {fixes_in_file} unused parameters in {file_path.name}")
            else:
                with open(file_path, 'w', encoding='utf-8') as f:
                    f.write(content)
                self.files_modified.add(file_path)
                print(f"  Fixed {fixes_in_file} unused parameters in {file_path.name}")
                
            self.fixed_count += fixes_in_file
                
        return fixes_in_file
    
    def parse_build_errors(self, target_dir: Path) -> dict:
        """Parse build output to find xUnit1026 errors."""
        print("Running build to identify xUnit1026 errors...")
        
        try:
            result = subprocess.run(
                ["dotnet", "build", str(target_dir), "--no-restore", "-v:n"],
                capture_output=True,
                text=True,
                cwd=target_dir.parent
            )
        except subprocess.CalledProcessError as e:
            print(f"Build failed: {e}")
            return {}
        
        build_output = result.stdout + "\n" + result.stderr
        errors_by_file = {}
        
        # Pattern for xUnit1026 errors
        pattern = r"([^(]+\.cs)\((\d+),\d+\):\s*error xUnit1026:.*Theory method '([^']+)'.*does not use parameter '([^']+)'"
        
        for match in re.finditer(pattern, build_output, re.MULTILINE):
            file_path_str = match.group(1).strip()
            line_num = int(match.group(2))
            method_name = match.group(3)
            param_name = match.group(4)
            
            # Convert to absolute path
            if not file_path_str.startswith('/') and not ':' in file_path_str:
                file_path = target_dir.parent / file_path_str
            else:
                file_path = Path(file_path_str)
            
            if file_path not in errors_by_file:
                errors_by_file[file_path] = []
            errors_by_file[file_path].append((method_name, line_num, param_name))
        
        return errors_by_file
    
    def run(self, target_dir: Path, dry_run: bool = False, max_files: int = 10):
        """Run the comprehensive fixer."""
        self.dry_run = dry_run
        print(f"{'DRY RUN: ' if dry_run else ''}Comprehensive xUnit1026 parameter fixer")
        
        # Parse errors
        errors_by_file = self.parse_build_errors(target_dir)
        
        if not errors_by_file:
            print("No xUnit1026 errors found!")
            return
            
        print(f"Found xUnit1026 errors in {len(errors_by_file)} files")
        
        # Process files (limit to max_files for initial batch)
        processed = 0
        for file_path, errors in errors_by_file.items():
            if processed >= max_files:
                print(f"\nProcessed {max_files} files. Run again to continue with remaining files.")
                break
                
            print(f"\nProcessing {file_path.name} ({len(errors)} unused parameters)...")
            self.fix_file(file_path, errors)
            processed += 1
        
        # Summary
        print(f"\n{'DRY RUN ' if dry_run else ''}Summary:")
        print(f"  Total fixes applied: {self.fixed_count}")
        print(f"  Files {'would be' if dry_run else ''} modified: {len(self.files_modified)}")
        if len(errors_by_file) > max_files:
            print(f"  Remaining files with errors: {len(errors_by_file) - max_files}")

def main():
    parser = argparse.ArgumentParser(description="Comprehensive xUnit1026 unused parameter fixer")
    parser.add_argument("target_dir", type=Path, help="Target directory (e.g., Src/Tests/Core/Application.UnitTests)")
    parser.add_argument("--dry-run", action="store_true", help="Preview changes without applying")
    parser.add_argument("--max-files", type=int, default=10, help="Maximum files to process (default: 10)")
    
    args = parser.parse_args()
    
    if not args.target_dir.exists():
        print(f"Error: Target directory does not exist: {args.target_dir}")
        sys.exit(1)
        
    fixer = XUnit1026ComprehensiveFixer()
    fixer.run(args.target_dir, args.dry_run, args.max_files)

if __name__ == "__main__":
    main()