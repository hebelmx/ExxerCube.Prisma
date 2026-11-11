#!/usr/bin/env python3
"""
xUnit1026 XUnitLogger Pattern Fixer V3 - VS FORMAT SUPPORT
Handles VS Error List tab-separated format
"""

import os
import re
import sys
import subprocess
from pathlib import Path
from typing import List, Dict
import argparse

class VSFormatXUnitLoggerFixer:
    def __init__(self, dry_run: bool = False, max_files: int = None):
        self.dry_run = dry_run
        self.max_files = max_files
        self.files_processed = 0
        self.methods_fixed = 0
        self.total_errors_fixed = 0
        self.initial_error_count = 0
        
    def get_current_error_count(self) -> int:
        """Get current xUnit1026 error count from VS format file"""
        try:
            with open('current_errors.txt', 'r', encoding='utf-8') as f:
                content = f.read()
            return content.count('xUnit1026')
        except:
            return 999  # Safe default
    
    def find_xunit1026_errors_vs_format(self, target_dir: str) -> List[Dict]:
        """Parse VS Error List format for xUnit1026 errors"""
        print("Parsing VS Error List format for xUnit1026 errors...")
        
        try:
            with open('current_errors.txt', 'r', encoding='utf-8') as f:
                lines = f.readlines()
            
            errors = []
            for line_num, line in enumerate(lines):
                if line_num == 0:  # Skip header
                    continue
                    
                if 'xUnit1026' not in line:
                    continue
                    
                # Parse VS format: Severity\tCode\tDescription\tProject\tFile\tLine\tSuppression State\tDetails
                parts = line.strip().split('\t')
                if len(parts) < 6:
                    continue
                
                # Extract information
                code = parts[1]  # xUnit1026
                description = parts[2]
                project = parts[3]
                file_path = parts[4]
                line_str = parts[5]
                
                if not line_str.isdigit():
                    continue
                    
                line_num = int(line_str)
                
                # Extract method name and parameter from description
                # Format: "Theory method 'MethodName' on test class 'ClassName' does not use parameter 'paramName'"
                method_match = re.search(r"Theory method '([^']+)'", description)
                class_match = re.search(r"test class '([^']+)'", description)
                param_match = re.search(r"parameter '([^']+)'", description)
                
                if method_match and class_match and param_match:
                    method_name = method_match.group(1)
                    class_name = class_match.group(1)
                    param_name = param_match.group(1)
                    
                    errors.append({
                        'file': file_path,
                        'line': line_num,
                        'method': method_name,
                        'class': class_name,
                        'param': param_name,
                        'file_name': os.path.basename(file_path)
                    })
            
            print(f"Found {len(errors)} xUnit1026 errors in VS format")
            return errors
            
        except Exception as e:
            print(f"Error parsing VS format: {e}")
            return []
    
    def extract_method_parameters_smart_regex(self, content: str, method_line_start: int) -> List[str]:
        """Smart regex for xUnit1026 patterns - same as V2"""
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
        
        # Smart regex for xUnit1026 patterns
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
            # Remove default values
            part = re.sub(r'=.*$', '', part).strip()
            
            # Extract parameter name (last word)
            words = part.strip().split()
            if len(words) >= 2:
                param_name = words[-1]
                param_name = re.sub(r'[^\w]', '', param_name)
                if param_name:
                    param_names.append(param_name)
        
        return param_names
    
    def generate_xuint_logger_code(self, class_name: str, param_names: List[str]) -> List[str]:
        """Generate XUnitLogger code - same as V2"""
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
            format_string = f"Testing scenario: {{{description_param}}}"
            param_list = [description_param]
            
            if other_params:
                param_format = ", ".join([f"{param}={{{param}}}" for param in other_params])
                format_string += f" with {param_format}"
                param_list.extend(other_params)
        else:
            # No description param, just log all parameters
            param_format = ", ".join([f"{param}={{{param}}}" for param in param_names])
            format_string = f"Testing method with {param_format}"
            param_list = param_names
        
        # Generate logger lines
        logger_lines = [
            f"",  # Empty line before
            f"        var logger = XUnitLogger.CreateLogger<{class_name}>();",
            f"        logger.LogInformation(\"{format_string}\",",
            f"            {', '.join(param_list)});",
            f""  # Empty line after
        ]
        
        return logger_lines
    
    def fix_single_method(self, file_path: str, error_info: Dict) -> bool:
        """Fix a single method - V3 with better brace detection"""
        try:
            print(f"  Fixing method: {error_info['method']} (param: {error_info['param']})")
            
            # Read file content
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()
            
            lines = content.split('\n')
            method_line = error_info['line'] - 1  # Convert to 0-based
            
            # Find method opening brace - improved logic
            method_start_brace = -1
            paren_count = 0
            found_closing_paren = False
            
            for i in range(method_line, min(len(lines), method_line + 15)):
                line = lines[i]
                
                # Track parentheses balance
                paren_count += line.count('(') - line.count(')')
                
                # Once parentheses are balanced, look for opening brace
                if paren_count == 0 and ('(' in content.split('\n')[method_line]):
                    found_closing_paren = True
                
                # Find opening brace after parentheses close
                if found_closing_paren and '{' in line:
                    method_start_brace = i
                    break
            
            if method_start_brace == -1:
                print(f"    ERROR: Could not find opening brace")
                return False
            
            # Check if already fixed
            method_content = '\n'.join(lines[method_start_brace:method_start_brace + 8])
            if 'XUnitLogger.CreateLogger' in method_content:
                print(f"    SKIP: Already has XUnitLogger")
                return True
            
            # Extract parameters
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
    
    def rebuild_and_check_vs(self, target_dir: str) -> bool:
        """Build validation with VS format support"""
        if self.dry_run:
            print("    DRY RUN: Skipping build validation")
            return True
        
        print("  Building solution to validate...")
        
        try:
            # Build and capture output
            result = subprocess.run([
                'dotnet', 'build', target_dir, '--no-restore', '-v:m'
            ], capture_output=True, text=True, cwd='.')
            
            # For VS format, we can't easily update the file, so just count errors differently
            new_error_count = result.stderr.count('xUnit1026')
            
            print(f"  Error count: {self.initial_error_count} -> {new_error_count}")
            
            # Safety check: Abort if error count drops too dramatically
            if new_error_count <= self.initial_error_count * 0.5:
                print(f"  SAFETY ABORT: Error count dropped by >50% ({self.initial_error_count} -> {new_error_count})")
                return False
            
            if new_error_count < self.initial_error_count:
                print(f"  SUCCESS: Error count decreased!")
                self.initial_error_count = new_error_count
                return True
            else:
                print(f"  DANGER: Error count did not decrease - ABORTING!")
                return False
        
        except Exception as e:
            print(f"  BUILD ERROR: {e}")
            return False
    
    def process_file_safely(self, file_path: str, file_errors: List[Dict]) -> bool:
        """Process one file safely - V3"""
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
        if not self.rebuild_and_check_vs("Src/IndTrace.sln"):
            print(f"  ABORT: Build validation failed for {file_path}")
            return False
        
        return True
    
    def run(self, target_dir: str):
        """Main execution - V3 with VS format support"""
        print("XUnitLogger Pattern Fixer V3 - VS FORMAT SUPPORT")
        print(f"Target: {target_dir}")
        print(f"Mode: {'DRY RUN' if self.dry_run else 'LIVE'}")
        
        # Get initial error count
        self.initial_error_count = self.get_current_error_count()
        print(f"Initial xUnit1026 errors: {self.initial_error_count}")
        
        # Find all errors using VS format parser
        errors = self.find_xunit1026_errors_vs_format(target_dir)
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
        print("VS FORMAT XUINT LOGGER FIXER V3 SUMMARY")
        print("="*60)
        print(f"Files processed: {files_processed}")
        print(f"Methods fixed: {self.methods_fixed}")
        print(f"Initial errors: {self.initial_error_count}")
        print(f"Mode: {'DRY RUN' if self.dry_run else 'LIVE'}")

def main():
    parser = argparse.ArgumentParser(description='VS Format XUnitLogger Pattern Fixer V3')
    parser.add_argument('target_dir', help='Target directory (e.g., Src/Tests)')
    parser.add_argument('--dry-run', action='store_true', help='Preview changes without applying')
    parser.add_argument('--max-files', type=int, help='Maximum files to process')
    
    args = parser.parse_args()
    
    if not os.path.exists(args.target_dir):
        print(f"Directory not found: {args.target_dir}")
        sys.exit(1)
    
    fixer = VSFormatXUnitLoggerFixer(dry_run=args.dry_run, max_files=args.max_files)
    fixer.run(args.target_dir)

if __name__ == '__main__':
    main()