#!/usr/bin/env python3
"""
xUnit1051 Error Fixer V2 - ADVANCED PATTERNS
Targets the remaining complex patterns that V1 simple regex couldn't handle.

New Patterns Targeted:
1. Task.Delay() calls without cancellation token
2. Direct service method calls 
3. Repository/handler async calls
4. Method calls in different contexts (not just TestValidateAsync)
"""

import os
import re
import subprocess
import sys
import shutil
from pathlib import Path
from typing import List, Dict, Tuple, Optional
import argparse
from datetime import datetime

class XUnit1051FixerV2:
    def __init__(self, project_root: str, dry_run: bool = True):
        self.project_root = Path(project_root)
        self.dry_run = dry_run
        self.solution_file = self.project_root / "Src" / "Tests" / "Core" / "Application.UnitTests" / "Application.UnitTests.csproj"
        self.fixes_applied = 0
        self.files_processed = 0
        self.errors_found = 0
        
        # V2 ADVANCED PATTERNS - Targeting remaining complex cases
        self.advanced_patterns = [
            # Pattern 1: Task.Delay() without cancellation token
            r'(await\s+Task\.Delay\(\d+)\)(?!\s*,\s*(?:cancellationToken|TestContext\.Current\.CancellationToken))',
            
            # Pattern 2: Service method calls in specific contexts
            r'(await\s+\w+\.(?:SaveChangesAsync|ExecuteAsync|ProcessAsync|GetAsync|CreateAsync|UpdateAsync|DeleteAsync)\(\w*\)?)(?!\s*,\s*(?:cancellationToken|TestContext\.Current\.CancellationToken))',
            
            # Pattern 3: Repository calls that end with ()
            r'(await\s+\w+\.(?:\w*Repository\.\w*Async|\w*Service\.\w*Async|\w*Handler\.\w*Async)\(\)?)(?!\s*,\s*(?:cancellationToken|TestContext\.Current\.CancellationToken))',
            
            # Pattern 4: Method calls with parameters but missing cancellation token at the end
            r'(await\s+\w+\.\w+Async\([^)]*[^,]\))(?!\s*,\s*(?:cancellationToken|TestContext\.Current\.CancellationToken))(?=\s*;)',
            
            # Pattern 5: Replace existing cancellation tokens
            r'CancellationToken\.None(?!\s*\))',
        ]
        
        self.log_file = self.project_root / f"xunit1051_v2_fix_log_{datetime.now().strftime('%Y%m%d_%H%M%S')}.txt"
        
    def log(self, message: str, level: str = "INFO"):
        """Log message to console and file"""
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        log_message = f"[{timestamp}] {level}: {message}"
        print(log_message)
        
        with open(self.log_file, 'a', encoding='utf-8') as f:
            f.write(log_message + '\n')
    
    def count_build_errors(self) -> int:
        """Count total build errors"""
        try:
            result = subprocess.run(
                ['dotnet', 'build', str(self.solution_file), '--no-restore', '-v:q'],
                capture_output=True,
                text=True,
                cwd=self.project_root
            )
            
            full_output = result.stderr + '\n' + result.stdout
            error_count = len([line for line in full_output.split('\n') if ' error ' in line and '.cs(' in line])
            return error_count
            
        except Exception as e:
            self.log(f"Error counting build errors: {e}", "ERROR")
            return float('inf')
    
    def validate_build(self, baseline_errors: int = None) -> bool:
        """Validate that the build hasn't gotten worse - WITH TRUNCATION DETECTION (V1 safety)"""
        if self.dry_run:
            return True
            
        current_errors = self.count_build_errors()
        
        if baseline_errors is None:
            return current_errors < 1000
        
        # CRITICAL SAFETY CHECK: Detect truncated builds (from reference script)
        if baseline_errors > 0 and current_errors <= baseline_errors * 0.5:
            self.log(f"SAFETY ABORT: Error count dropped by >50% ({baseline_errors} -> {current_errors}) - possible truncated build!", "ERROR")
            return False
        
        # Allow reasonable error tolerance
        is_acceptable = current_errors <= baseline_errors + 10
        
        if not is_acceptable:
            self.log(f"Build validation failed: errors increased from {baseline_errors} to {current_errors}", "ERROR")
        else:
            self.log(f"Build validation passed: {baseline_errors} -> {current_errors} errors")
        
        return is_acceptable
    
    def get_xunit1051_errors(self) -> List[Dict[str, str]]:
        """Extract xUnit1051 errors from build output"""
        self.log("Building solution to identify xUnit1051 errors...")
        
        try:
            result = subprocess.run(
                ['dotnet', 'build', str(self.solution_file), '--no-restore', '-v:m'],
                capture_output=True,
                text=True,
                cwd=self.project_root
            )
            
            errors = []
            full_output = result.stderr + '\n' + result.stdout
            
            for line in full_output.split('\n'):
                if 'xUnit1051' in line and '.cs(' in line:
                    # Parse error line format
                    match = re.match(r'([^(]+)\((\d+),\d+\):\s*error\s+xUnit1051:', line)
                    if match:
                        filepath = Path(match.group(1))
                        line_num = int(match.group(2))
                        errors.append({
                            'file': str(filepath),
                            'line': line_num,
                            'original_line': line.strip()
                        })
            
            self.log(f"Found {len(errors)} xUnit1051 errors")
            self.errors_found = len(errors)
            return errors
            
        except Exception as e:
            self.log(f"Error running build: {e}", "ERROR")
            return []
    
    def backup_file(self, filepath: Path) -> Path:
        """Create backup of file before modification"""
        backup_path = filepath.with_suffix(filepath.suffix + '.v2backup')
        shutil.copy2(filepath, backup_path)
        return backup_path
    
    def restore_file(self, filepath: Path, backup_path: Path):
        """Restore file from backup"""
        shutil.copy2(backup_path, filepath)
        backup_path.unlink()
    
    def apply_v2_fixes_to_content(self, content: str, filepath: str) -> Tuple[str, int]:
        """Apply V2 advanced fixes to file content"""
        modified_content = content
        fixes_in_file = 0
        
        # Apply V2 advanced patterns
        for i, pattern in enumerate(self.advanced_patterns[:4], 1):
            def replace_func(match):
                original = match.group(0)
                method_call = match.group(1)
                
                # Skip if already has cancellation token (ENHANCED CHECK)
                if ('TestContext.Current.CancellationToken' in original or 
                    'cancellationToken:' in original or 
                    'cancellationToken)' in original or
                    ', cancellationToken' in original):
                    return original
                
                # V2 LOGIC: Different fixes based on pattern
                if pattern == self.advanced_patterns[0]:  # Task.Delay pattern
                    new_call = f"{method_call}, TestContext.Current.CancellationToken)"
                elif 'TestValidateAsync' in method_call:  # FluentValidation pattern
                    new_call = f"{method_call}, cancellationToken: TestContext.Current.CancellationToken)"
                else:  # General async methods
                    new_call = f"{method_call}, TestContext.Current.CancellationToken)"
                
                self.log(f"  V2 Fix Pattern {i}: {original.strip()} -> {new_call.strip()}")
                return new_call
            
            new_content, count = re.subn(pattern, replace_func, modified_content)
            if count > 0:
                modified_content = new_content
                fixes_in_file += count
                self.log(f"  Applied {count} V2 fixes for pattern {i}")
        
        # Pattern 5: Replace CancellationToken.None
        new_content, count = re.subn(
            self.advanced_patterns[4], 
            'TestContext.Current.CancellationToken', 
            modified_content
        )
        if count > 0:
            modified_content = new_content
            fixes_in_file += count
            self.log(f"  Replaced {count} CancellationToken.None occurrences")
        
        return modified_content, fixes_in_file
    
    def process_file_safely(self, filepath: str) -> bool:
        """Process a single file safely with V2 patterns"""
        file_path = Path(filepath)
        
        if not file_path.exists():
            self.log(f"File not found: {filepath}", "WARNING")
            return False
        
        self.log(f"Processing file with V2 patterns: {file_path.name}")
        
        try:
            # Read file content
            with open(file_path, 'r', encoding='utf-8') as f:
                original_content = f.read()
            
            # Apply V2 fixes
            modified_content, fixes_in_file = self.apply_v2_fixes_to_content(original_content, str(file_path))
            
            if fixes_in_file == 0:
                self.log(f"  No V2 fixes needed for {file_path.name}")
                return True
            
            if self.dry_run:
                self.log(f"  DRY RUN: Would apply {fixes_in_file} V2 fixes to {file_path.name}")
                self.fixes_applied += fixes_in_file
                return True
            
            # Get baseline error count before making changes
            baseline_errors = self.count_build_errors()
            
            # Create backup
            backup_path = self.backup_file(file_path)
            
            try:
                # Write modified content
                with open(file_path, 'w', encoding='utf-8') as f:
                    f.write(modified_content)
                
                self.log(f"  Applied {fixes_in_file} V2 fixes to {file_path.name}")
                
                # CRITICAL: Validate build after EACH FILE (battle-tested safety)
                if self.validate_build(baseline_errors):
                    self.fixes_applied += fixes_in_file
                    backup_path.unlink()  # Remove backup if successful
                    self.log(f"  V2 file validation passed - continuing")
                    return True
                else:
                    self.log(f"  SAFETY ABORT: V2 validation failed for {file_path.name}, rolling back", "ERROR")
                    self.restore_file(file_path, backup_path)
                    return False
                    
            except Exception as e:
                self.log(f"  Error writing file: {e}", "ERROR")
                self.restore_file(file_path, backup_path)
                return False
                
        except Exception as e:
            self.log(f"Error processing {filepath}: {e}", "ERROR")
            return False
    
    def run_v2_fixes(self) -> bool:
        """Main execution method for V2"""
        self.log("=== Starting xUnit1051 V2 ADVANCED PATTERNS Fix Process ===")
        
        if self.dry_run:
            self.log("Running in DRY RUN mode - no files will be modified")
        
        # Get current errors
        errors = self.get_xunit1051_errors()
        if not errors:
            self.log("No xUnit1051 errors found!")
            return True
        
        # Group errors by file
        files_with_errors = {}
        for error in errors:
            filepath = error['file']
            if filepath not in files_with_errors:
                files_with_errors[filepath] = []
            files_with_errors[filepath].append(error)
        
        self.log(f"Found errors in {len(files_with_errors)} files")
        
        # Limit files if requested (for testing)
        files_to_process = list(files_with_errors.keys())
        if hasattr(self, 'max_files') and self.max_files:
            files_to_process = files_to_process[:self.max_files]
            self.log(f"LIMITED TO {len(files_to_process)} files for testing")
        
        # Process each file safely (abort on first failure for safety)
        successful_files = 0
        for filepath in files_to_process:
            self.files_processed += 1
            if self.process_file_safely(filepath):
                successful_files += 1
            else:
                self.log(f"ABORTING V2: Safety validation failed on {filepath}", "ERROR")
                break  # Stop on first failure for safety
        
        # Summary
        self.log("=== V2 Fix Process Summary ===")
        self.log(f"Files processed: {self.files_processed}")
        self.log(f"Files successfully fixed: {successful_files}")
        self.log(f"Total V2 fixes applied: {self.fixes_applied}")
        self.log(f"Original errors found: {self.errors_found}")
        
        if not self.dry_run:
            # Final validation
            final_errors = self.get_xunit1051_errors()
            remaining_errors = len(final_errors)
            self.log(f"Remaining xUnit1051 errors: {remaining_errors}")
            
            if remaining_errors < self.errors_found:
                reduction_percent = ((self.errors_found - remaining_errors) / self.errors_found) * 100
                self.log(f"V2 error reduction: {reduction_percent:.1f}%")
                return True
            else:
                self.log("No error reduction achieved", "WARNING")
                return False
        
        return True

def main():
    parser = argparse.ArgumentParser(description='Fix xUnit1051 errors automatically - V2 Advanced Patterns')
    parser.add_argument('--project-root', default='.', help='Project root directory')
    parser.add_argument('--dry-run', action='store_true', default=True, 
                       help='Run in dry-run mode (default: True)')
    parser.add_argument('--real-run', action='store_true', 
                       help='Execute real fixes (overrides --dry-run)')
    parser.add_argument('--max-files', type=int, default=None, 
                       help='Maximum files to process (for testing)')
    
    args = parser.parse_args()
    
    # Determine dry-run mode
    dry_run = args.dry_run and not args.real_run
    
    project_root = os.path.abspath(args.project_root)
    
    print(f"xUnit1051 Error Fixer V2 - ADVANCED PATTERNS")
    print(f"Project root: {project_root}")
    print(f"Mode: {'DRY RUN' if dry_run else 'REAL EXECUTION'}")
    print("-" * 60)
    
    fixer = XUnit1051FixerV2(project_root, dry_run=dry_run)
    fixer.max_files = args.max_files  # Set file limit
    success = fixer.run_v2_fixes()
    
    sys.exit(0 if success else 1)

if __name__ == "__main__":
    main()