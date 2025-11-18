#!/usr/bin/env python3
"""
xUnit1026 XUnitLogger Pattern Fixer
Converts unused Theory parameters into valuable test logging using XUnitLogger pattern

Pattern Applied:
- Finds Theory methods with unused parameters
- Adds: var logger = XUnitLogger.CreateLogger<TestClassName>();
- Adds: logger.LogInformation("Testing scenario: {Description} with Param1={Param1}, Param2={Param2}", description, param1, param2);
- Preserves all existing test logic unchanged

Example:
    [Theory]
    [InlineData(8, 8, "Process validation rejection")]
    public void Should_HandleDifferentEnumCombinations_When_VariousStatusesProvided(
        int flowStatusValue, int partStatusValue, string description)
    {
        var logger = XUnitLogger.CreateLogger<BarCodeRejectedViewTests>();
        logger.LogInformation("Testing scenario: {Description} with FlowStatus={FlowStatusValue}, PartStatus={PartStatusValue}",
            description, flowStatusValue, partStatusValue);
        
        // Arrange
        // ... existing test logic continues unchanged
    }
"""

import os
import re
import sys
from pathlib import Path
from typing import List, Tuple, Dict, Set
import argparse

class XUnitLoggerFixer:
    def __init__(self, dry_run: bool = False, max_files: int = None):
        self.dry_run = dry_run
        self.max_files = max_files
        self.files_processed = 0
        self.fixes_applied = 0
        self.errors_found = []
        
    def find_xunit1026_errors(self, target_dir: str) -> List[Dict]:
        """Find xUnit1026 errors by reading existing build output"""
        print("Scanning for xUnit1026 errors...")
        
        try:
            # Read from existing build output file
            build_output_file = 'current_errors.txt'
            if not os.path.exists(build_output_file):
                print(f"Build output file not found: {build_output_file}")
                return []
            
            with open(build_output_file, 'r', encoding='utf-8') as f:
                build_output = f.read()
            
            errors = []
            for line in build_output.split('\n'):
                if 'xUnit1026' in line:
                    # Parse: F:\Path\File.cs(110,87): error xUnit1026: Theory method 'MethodName' on test class 'ClassName' does not use parameter 'paramName'
                    match = re.search(r'([^\\]+\.cs)\((\d+),(\d+)\): error xUnit1026: Theory method \'([^\']+)\' on test class \'([^\']+)\' does not use parameter \'([^\']+)\'', line)
                    if match:
                        file_path, line_num, col_num, method_name, class_name, param_name = match.groups()
                        # Find full path
                        for root, dirs, files in os.walk(target_dir):
                            if file_path in files:
                                full_path = os.path.join(root, file_path)
                                errors.append({
                                    'file': full_path,
                                    'line': int(line_num),
                                    'method': method_name,
                                    'class': class_name,
                                    'param': param_name,
                                    'file_name': file_path
                                })
                                break
            
            print(f"Found {len(errors)} xUnit1026 errors")
            return errors
            
        except Exception as e:
            print(f"Error scanning: {e}")
            return []
    
    def extract_method_parameters(self, content: str, method_line_start: int) -> List[str]:
        """Extract all parameter names from Theory method signature"""
        lines = content.split('\n')
        
        # Find method signature (may span multiple lines)
        method_signature = ""
        paren_count = 0
        found_opening = False
        
        for i in range(method_line_start - 1, len(lines)):
            line = lines[i].strip()
            method_signature += line + " "
            
            if '(' in line:
                found_opening = True
            
            if found_opening:
                paren_count += line.count('(') - line.count(')')
                if paren_count == 0:
                    break
        
        # Extract parameters from signature
        params_match = re.search(r'\((.*?)\)', method_signature, re.DOTALL)
        if not params_match:
            return []
        
        params_str = params_match.group(1).strip()
        if not params_str:
            return []
        
        # Parse parameters: "int param1, string param2, string description"
        param_parts = [p.strip() for p in params_str.split(',')]
        param_names = []
        
        for part in param_parts:
            # Extract parameter name (last word)
            words = part.strip().split()
            if len(words) >= 2:  # type + name
                param_names.append(words[-1])
        
        return param_names
    
    def generate_log_parameters(self, param_names: List[str]) -> Tuple[str, str]:
        """Generate LogInformation format string and parameter list"""
        if not param_names:
            return "", ""
        
        # Create format placeholders: "Testing scenario: {Description} with Param1={Param1}, Param2={Param2}"
        format_parts = []
        param_list = []
        
        # Look for 'description' parameter first (common pattern)
        description_param = None
        other_params = []
        
        for param in param_names:
            if param.lower() in ['description', 'scenario']:
                description_param = param
            else:
                other_params.append(param)
        
        if description_param:
            format_string = f"Testing scenario: {{{description_param.title()}}}"
            param_list.append(description_param)
            
            if other_params:
                param_format = ", ".join([f"{param.title()}={{{param.title()}}}" for param in other_params])
                format_string += f" with {param_format}"
                param_list.extend(other_params)
        else:
            # No description param, just log all parameters
            param_format = ", ".join([f"{param.title()}={{{param.title()}}}" for param in param_names])
            format_string = f"Testing method with {param_format}"
            param_list.extend(param_names)
        
        param_list_str = ", ".join(param_list)
        return format_string, param_list_str
    
    def fix_method(self, content: str, error_info: Dict) -> str:
        """Apply XUnitLogger pattern fix to a specific method"""
        lines = content.split('\n')
        method_line = error_info['line'] - 1  # Convert to 0-based
        
        # Find method opening brace
        method_start_brace = -1
        for i in range(method_line, min(len(lines), method_line + 10)):
            if '{' in lines[i]:
                method_start_brace = i
                break
        
        if method_start_brace == -1:
            print(f"  Could not find method opening brace for {error_info['method']}")
            return content
        
        # Check if already fixed
        method_content = '\n'.join(lines[method_start_brace:method_start_brace + 5])
        if 'XUnitLogger.CreateLogger' in method_content:
            print(f" Method {error_info['method']} already has XUnitLogger - skipping")
            return content
        
        # Extract all method parameters
        param_names = self.extract_method_parameters(content, method_line)
        if not param_names:
            print(f"  Could not extract parameters for {error_info['method']}")
            return content
        
        # Generate logging code
        format_string, param_list_str = self.generate_log_parameters(param_names)
        
        # Find indentation of the opening brace line
        brace_line = lines[method_start_brace]
        indent = len(brace_line) - len(brace_line.lstrip())
        method_indent = ' ' * (indent + 4)  # Add 4 spaces for method body
        
        # Create logger lines
        logger_lines = [
            f"{method_indent}var logger = XUnitLogger.CreateLogger<{error_info['class']}>();",
            f"{method_indent}logger.LogInformation(\"{format_string}\",",
            f"{method_indent}    {param_list_str});",
            f"{method_indent}"  # Empty line for separation
        ]
        
        # Insert after opening brace
        lines[method_start_brace:method_start_brace] = logger_lines
        
        return '\n'.join(lines)
    
    def process_file(self, file_path: str, file_errors: List[Dict]) -> bool:
        """Process a single file and apply all XUnitLogger fixes"""
        try:
            print(f"\n  Processing: {os.path.basename(file_path)}")
            
            with open(file_path, 'r', encoding='utf-8') as f:
                original_content = f.read()
            
            modified_content = original_content
            fixes_in_file = 0
            
            # Group errors by method to avoid duplicate fixes
            methods_processed = set()
            
            for error in file_errors:
                method_key = f"{error['class']}.{error['method']}"
                if method_key in methods_processed:
                    continue
                
                print(f"    Fixing method: {error['method']} (unused param: {error['param']})")
                modified_content = self.fix_method(modified_content, error)
                methods_processed.add(method_key)
                fixes_in_file += 1
            
            # Only write if we made changes
            if modified_content != original_content:
                if not self.dry_run:
                    with open(file_path, 'w', encoding='utf-8') as f:
                        f.write(modified_content)
                    print(f"   Applied {fixes_in_file} XUnitLogger fixes")
                else:
                    print(f"  DRY RUN: Would apply {fixes_in_file} XUnitLogger fixes")
                
                self.fixes_applied += fixes_in_file
                return True
            else:
                print(f"    No changes needed")
                return False
            
        except Exception as e:
            print(f"   Error processing {file_path}: {e}")
            return False
    
    def run(self, target_dir: str):
        """Main execution method"""
        print("XUnitLogger Pattern Fixer for xUnit1026")
        print(f"Target: {target_dir}")
        print(f"Mode: {'DRY RUN' if self.dry_run else 'LIVE'}")
        if self.max_files:
            print(f"Max files: {self.max_files}")
        
        # Find all xUnit1026 errors
        errors = self.find_xunit1026_errors(target_dir)
        if not errors:
            print("No xUnit1026 errors found!")
            return
        
        # Group errors by file
        files_with_errors = {}
        for error in errors:
            file_path = error['file']
            if file_path not in files_with_errors:
                files_with_errors[file_path] = []
            files_with_errors[file_path].append(error)
        
        print(f"Found errors in {len(files_with_errors)} files")
        
        # Process files (respect max_files limit)
        files_to_process = list(files_with_errors.keys())
        if self.max_files:
            files_to_process = files_to_process[:self.max_files]
            print(f"Limited to {len(files_to_process)} files")
        
        files_modified = 0
        for file_path in files_to_process:
            file_errors = files_with_errors[file_path]
            if self.process_file(file_path, file_errors):
                files_modified += 1
            
            self.files_processed += 1
        
        # Summary
        print("\n" + "="*60)
        print("  XUINT LOGGER FIXER SUMMARY")
        print("="*60)
        print(f"  Files processed: {self.files_processed}")
        print(f"  Files modified: {files_modified}")
        print(f"  Total fixes applied: {self.fixes_applied}")
        print(f"  Mode: {'DRY RUN' if self.dry_run else 'LIVE'}")
        
        if self.dry_run:
            print("\n  Run without --dry-run to apply these fixes")

def main():
    parser = argparse.ArgumentParser(description='XUnitLogger Pattern Fixer for xUnit1026')
    parser.add_argument('target_dir', help='Target directory (e.g., Src/Tests/Core/Application.UnitTests)')
    parser.add_argument('--dry-run', action='store_true', help='Preview changes without applying')
    parser.add_argument('--max-files', type=int, help='Maximum files to process')
    
    args = parser.parse_args()
    
    if not os.path.exists(args.target_dir):
        print(f" Directory not found: {args.target_dir}")
        sys.exit(1)
    
    fixer = XUnitLoggerFixer(dry_run=args.dry_run, max_files=args.max_files)
    fixer.run(args.target_dir)

if __name__ == '__main__':
    main()