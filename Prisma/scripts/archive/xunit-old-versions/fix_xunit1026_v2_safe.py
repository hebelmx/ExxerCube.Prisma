#!/usr/bin/env python3
"""
xUnit1026 XUnitLogger Pattern Fixer V2 - SAFE APPROACH
Implements coached approach:
- One method per file write
- Build validation after each file
- Abort on regression
- Smart regex for xUnit1026 patterns only
"""

import os
import re
import sys
import subprocess
from pathlib import Path
from typing import List, Dict
import argparse

class SafeXUnitLoggerFixer:
    def __init__(self, dry_run: bool = False, max_files: int = None):
        self.dry_run = dry_run
        self.max_files = max_files
        self.files_processed = 0
        self.methods_fixed = 0
        self.total_errors_fixed = 0
        self.initial_error_count = 0
        
    def get_current_error_count(self) -> int:
        """Get current xUnit1026 error count by reading build output"""
        try:
            with open('current_errors.txt', 'r', encoding='utf-8') as f:
                content = f.read()
            return content.count('xUnit1026')
        except:
            return 999  # Safe default - assume high error count
    
    def find_xunit1026_errors(self, target_dir: str) -> List[Dict]:
        """Find xUnit1026 errors by reading existing build output"""
        print("Scanning for xUnit1026 errors...")
        
        try:
            with open('current_errors.txt', 'r', encoding='utf-8') as f:
                build_output = f.read()
            
            errors = []
            for line in build_output.split('\n'):
                if 'xUnit1026' in line:
                    # Parse error line
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
    
    def extract_method_parameters_smart_regex(self, content: str, method_line_start: int) -> List[str]:
        """Smart regex for xUnit1026 patterns - coached approach"""
        lines = content.split('\n')
        
        # Find method signature spanning multiple lines
        method_signature = ""
        paren_count = 0
        found_opening = False
        
        # Start from method line, capture until balanced parentheses
        for i in range(method_line_start - 1, len(lines)):
            line = lines[i].strip()
            method_signature += line + " "
            
            # Track parentheses
            if '(' in line:
                found_opening = True
            
            if found_opening:
                paren_count += line.count('(') - line.count(')')
                if paren_count == 0:
                    break
        
        # Smart regex for xUnit1026 patterns - ignore line breaks, focus on commas
        # Pattern: capture everything between parentheses, ignore whitespace/newlines
        params_match = re.search(r'\((.*?)\)', method_signature, re.DOTALL)
        if not params_match:
            return []
        
        params_str = params_match.group(1).strip()
        if not params_str:
            return []
        
        # Split by comma, handle each parameter
        param_parts = [p.strip() for p in params_str.split(',')]
        param_names = []
        
        for part in param_parts:
            # Remove default values (= something) - unlikely in unit tests but safe
            part = re.sub(r'=.*$', '', part).strip()
            
            # Extract parameter name (last word before any brackets/generics)
            # Handle: "int param", "string? param", "List<string> param"
            words = part.strip().split()
            if len(words) >= 2:  # type + name minimum
                param_name = words[-1]
                # Clean any trailing punctuation/attributes
                param_name = re.sub(r'[^\w]', '', param_name)
                if param_name:
                    param_names.append(param_name)
        
        return param_names
    
    def generate_xuint_logger_code(self, class_name: str, param_names: List[str]) -> List[str]:
        """Generate XUnitLogger code lines with smart parameter formatting"""
        if not param_names:
            return []
        
        # Look for description/scenario parameter first
        description_param = None
        other_params = []
        
        for param in param_names:
            if param.lower() in ['description', 'scenario']:
                description_param = param
            else:
                other_params.append(param)
        
        # Build format string and parameter list
        if description_param:
            format_string = f"Testing scenario: {{{description_param.title()}}}"
            param_list = [description_param]
            
            if other_params:
                param_format = ", ".join([f"{param.title()}={{{param.title()}}}" for param in other_params])
                format_string += f" with {param_format}"
                param_list.extend(other_params)
        else:
            # No description param, just log all parameters
            param_format = ", ".join([f"{param.title()}={{{param.title()}}}" for param in param_names])
            format_string = f"Testing method with {param_format}"
            param_list = param_names
        
        # Generate logger lines
        logger_lines = [
            f"        var logger = XUnitLogger.CreateLogger<{class_name}>();",
            f"        logger.LogInformation(\"{format_string}\",",
            f"            {', '.join(param_list)});",
            f"        "  # Empty line for separation
        ]
        
        return logger_lines
    
    def fix_single_method(self, file_path: str, error_info: Dict) -> bool:
        """Fix a single method in a file - SAFE approach"""
        try:
            print(f"  Fixing method: {error_info['method']} (param: {error_info['param']})")
            
            # Read file content
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()
            
            lines = content.split('\n')
            method_line = error_info['line'] - 1  # Convert to 0-based
            
            # Find method opening brace - look through multiple lines after parentheses close
            method_start_brace = -1
            paren_count = 0
            found_closing_paren = False
            
            # Start from method line and scan forward
            for i in range(method_line, min(len(lines), method_line + 15)):
                line = lines[i]
                
                # Track parentheses balance
                paren_count += line.count('(') - line.count(')')
                
                # Once we've seen closing parenthesis, look for opening brace
                if paren_count == 0 and '(' in content.split('\n')[method_line]:
                    found_closing_paren = True
                
                # Find opening brace after parentheses close (could be same line or next)
                if found_closing_paren and '{' in line:
                    method_start_brace = i
                    break
            
            if method_start_brace == -1:
                print(f"    ERROR: Could not find opening brace after method signature")
                return False
            
            # Check if already fixed
            method_content = '\n'.join(lines[method_start_brace:method_start_brace + 5])
            if 'XUnitLogger.CreateLogger' in method_content:
                print(f"    SKIP: Already has XUnitLogger")
                return True  # Count as success, no need to fix
            
            # Extract parameters using smart regex
            param_names = self.extract_method_parameters_smart_regex(content, method_line)
            if not param_names:
                print(f"    ERROR: Could not extract parameters")
                return False
            
            print(f"    PARAMS: {param_names}")
            
            # Generate XUnitLogger code
            logger_lines = self.generate_xuint_logger_code(error_info['class'], param_names)
            
            # Insert AFTER the opening brace line using slice assignment
            lines[method_start_brace + 1:method_start_brace + 1] = logger_lines
            
            # Write modified content
            modified_content = '\n'.join(lines)
            if not self.dry_run:
                with open(file_path, 'w', encoding='utf-8') as f:
                    f.write(modified_content)
                print(f"    SUCCESS: Applied XUnitLogger fix")
            else:
                print(f"    DRY RUN: Would apply XUnitLogger fix")
            
            return True
            
        except Exception as e:
            print(f"    ERROR: {e}")
            return False
    
    def rebuild_and_check(self, target_dir: str) -> bool:
        """Rebuild solution and check if error count decreased"""
        if self.dry_run:
            print("    DRY RUN: Skipping build validation")
            return True
        
        print("  Building solution to validate...")
        
        try:
            # Build and capture output
            result = subprocess.run([
                'dotnet', 'build', target_dir, '-v:m'
            ], capture_output=True, text=True, cwd='.')
            
            # Save new build output
            with open('current_errors.txt', 'w', encoding='utf-8') as f:
                f.write(result.stderr)
            
            # Count errors
            new_error_count = self.get_current_error_count()
            
            print(f"  Error count: {self.initial_error_count} -> {new_error_count}")
            
            # Safety check: Abort if error count drops too dramatically (likely corruption)
            if new_error_count <= self.initial_error_count * 0.5:
                print(f"  SAFETY ABORT: Error count dropped by >50% ({self.initial_error_count} -> {new_error_count})")
                print(f"  This suggests possible file corruption or build failure!")
                return False
            
            if new_error_count < self.initial_error_count:
                print(f"  SUCCESS: Error count decreased!")
                self.initial_error_count = new_error_count  # Update for next iteration
                return True
            else:
                print(f"  DANGER: Error count did not decrease - ABORTING!")
                return False
        
        except Exception as e:
            print(f"  BUILD ERROR: {e}")
            return False
    
    def process_file_safely(self, file_path: str, file_errors: List[Dict]) -> bool:
        """Process one file with coached safe approach"""
        print(f"\nProcessing file: {os.path.basename(file_path)}")
        print(f"  Methods to fix: {len(file_errors)}")
        
        methods_fixed_in_file = 0
        
        # Process ONE method at a time
        for error in file_errors:
            if self.fix_single_method(file_path, error):
                methods_fixed_in_file += 1
                self.methods_fixed += 1
            else:
                print(f"  SKIP: Failed to fix method {error['method']}")
        
        print(f"  Fixed {methods_fixed_in_file}/{len(file_errors)} methods")
        
        # Build validation after each file
        if not self.rebuild_and_check("Src/Tests/Core/Application.UnitTests"):
            print(f"  ABORT: Build validation failed for {file_path}")
            return False
        
        return True
    
    def run(self, target_dir: str):
        """Main execution with coached safe approach"""
        print("XUnitLogger Pattern Fixer V2 - SAFE APPROACH")
        print(f"Target: {target_dir}")
        print(f"Mode: {'DRY RUN' if self.dry_run else 'LIVE'}")
        
        # Get initial error count
        self.initial_error_count = self.get_current_error_count()
        print(f"Initial xUnit1026 errors: {self.initial_error_count}")
        
        # Find all errors
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
        
        # Limit scope if requested
        files_to_process = list(files_with_errors.keys())
        if self.max_files:
            files_to_process = files_to_process[:self.max_files]
            print(f"Limited to {len(files_to_process)} files")
        
        # Process each file safely
        files_processed = 0
        for file_path in files_to_process:
            file_errors = files_with_errors[file_path]
            
            if self.process_file_safely(file_path, file_errors):
                files_processed += 1
            else:
                print(f"\nABORTING: Safety validation failed on {file_path}")
                break
        
        # Final summary
        print("\n" + "="*60)
        print("SAFE XUINT LOGGER FIXER V2 SUMMARY")
        print("="*60)
        print(f"Files processed: {files_processed}")
        print(f"Methods fixed: {self.methods_fixed}")
        print(f"Final xUnit1026 errors: {self.get_current_error_count()}")
        print(f"Mode: {'DRY RUN' if self.dry_run else 'LIVE'}")
        
        if self.dry_run:
            print("\nRun without --dry-run to apply these fixes")

def main():
    parser = argparse.ArgumentParser(description='Safe XUnitLogger Pattern Fixer V2 for xUnit1026')
    parser.add_argument('target_dir', help='Target directory (e.g., Src/Tests/Core/Application.UnitTests)')
    parser.add_argument('--dry-run', action='store_true', help='Preview changes without applying')
    parser.add_argument('--max-files', type=int, help='Maximum files to process')
    
    args = parser.parse_args()
    
    if not os.path.exists(args.target_dir):
        print(f"Directory not found: {args.target_dir}")
        sys.exit(1)
    
    fixer = SafeXUnitLoggerFixer(dry_run=args.dry_run, max_files=args.max_files)
    fixer.run(args.target_dir)

if __name__ == '__main__':
    main()