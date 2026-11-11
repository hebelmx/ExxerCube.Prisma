#!/usr/bin/env python3
"""
xUnit1051 Error Fixer V3 - SURGICAL PRECISION
Fixes the V2 edge case where cancellation tokens were added outside method parentheses.

CRITICAL FIX:
- V2 WRONG: tasks.Add(instance.SendAsync(message), TestContext.Current.CancellationToken);
- V3 CORRECT: tasks.Add(instance.SendAsync(message, cancellationToken: TestContext.Current.CancellationToken));

V3 uses surgical precision to insert tokens INSIDE method call parentheses.
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

class XUnit1051FixerV3:
    def __init__(self, project_root: str, dry_run: bool = True):
        self.project_root = Path(project_root)
        self.dry_run = dry_run
        self.solution_file = self.project_root / "Src" / "Tests" / "Core" / "Application.UnitTests" / "Application.UnitTests.csproj"
        self.fixes_applied = 0
        self.files_processed = 0
        self.errors_found = 0
        
        # V3 SURGICAL PRECISION PATTERNS - Insert tokens INSIDE parentheses
        self.surgical_patterns = [
            # Pattern 1: Task.Delay() - simple insertion
            {
                'name': 'Task.Delay',
                'pattern': r'(await\s+Task\.Delay\()(\d+)(\))',
                'replacement': r'\1\2, TestContext.Current.CancellationToken\3'
            },
            
            # Pattern 2: Method calls ending with single parameter - add named parameter
            {
                'name': 'Single Parameter Methods',
                'pattern': r'(await\s+\w+\.\w+Async\()(\w+)(\))',
                'replacement': r'\1\2, cancellationToken: TestContext.Current.CancellationToken\3'
            },
            
            # Pattern 3: Method calls with no parameters - add named parameter
            {
                'name': 'No Parameter Methods',
                'pattern': r'(await\s+\w+\.\w+Async\()(\))',
                'replacement': r'\1cancellationToken: TestContext.Current.CancellationToken\2'
            },
            
            # Pattern 4: Complex multi-parameter calls - surgical insertion before closing paren
            {
                'name': 'Multi-Parameter Methods',
                'pattern': r'(await\s+\w+\.\w+Async\([^)]+)(\))',
                'replacement': r'\1, cancellationToken: TestContext.Current.CancellationToken\2'
            },
            
            # Pattern 5: Replace CancellationToken.None
            {
                'name': 'CancellationToken.None Replacement',
                'pattern': r'CancellationToken\.None',
                'replacement': r'TestContext.Current.CancellationToken'
            }
        ]
        
        self.log_file = self.project_root / f"xunit1051_v3_surgical_log_{datetime.now().strftime('%Y%m%d_%H%M%S')}.txt"
        
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
        """Validate build with battle-tested safety from reference script"""
        if self.dry_run:
            return True
            
        current_errors = self.count_build_errors()
        
        if baseline_errors is None:
            return current_errors < 1000
        
        # CRITICAL SAFETY CHECK: Detect truncated builds (battle-tested from reference)
        if baseline_errors > 0 and current_errors <= baseline_errors * 0.5:
            self.log(f"SAFETY ABORT: Error count dropped by >50% ({baseline_errors} -> {current_errors}) - possible truncated build!", "ERROR")
            return False
        
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
        backup_path = filepath.with_suffix(filepath.suffix + '.v3backup')
        shutil.copy2(filepath, backup_path)
        return backup_path
    
    def restore_file(self, filepath: Path, backup_path: Path):
        """Restore file from backup"""
        shutil.copy2(backup_path, filepath)
        backup_path.unlink()
    
    def has_cancellation_token(self, text: str) -> bool:
        """Check if text already has a cancellation token parameter"""
        return any([
            'TestContext.Current.CancellationToken' in text,
            'cancellationToken:' in text,
            'cancellationToken)' in text,
            ', cancellationToken' in text,
            'CancellationToken cancellationToken' in text
        ])
    
    def apply_v3_surgical_fixes(self, content: str, filepath: str) -> Tuple[str, int]:
        """Apply V3 surgical precision fixes"""
        modified_content = content
        total_fixes = 0
        
        for pattern_info in self.surgical_patterns:
            pattern_name = pattern_info['name']
            pattern = pattern_info['pattern']
            replacement = pattern_info['replacement']
            
            # For non-replacement patterns, use function to check for existing tokens
            if pattern_name != 'CancellationToken.None Replacement':
                def surgical_replace(match):
                    full_match = match.group(0)
                    
                    # Skip if already has cancellation token
                    if self.has_cancellation_token(full_match):
                        return full_match
                    
                    # Apply surgical replacement
                    result = re.sub(pattern, replacement, full_match)
                    self.log(f"  V3 {pattern_name}: {full_match.strip()} -> {result.strip()}")
                    return result
                
                new_content, count = re.subn(pattern, surgical_replace, modified_content)
            else:
                # Direct replacement for CancellationToken.None
                new_content, count = re.subn(pattern, replacement, modified_content)
                if count > 0:
                    self.log(f"  V3 {pattern_name}: Replaced {count} occurrences")
            
            if count > 0:
                modified_content = new_content
                total_fixes += count
                self.log(f"  Applied {count} V3 {pattern_name} fixes")
        
        return modified_content, total_fixes
    
    def process_file_surgically(self, filepath: str) -> bool:
        """Process a single file with V3 surgical precision"""
        file_path = Path(filepath)
        
        if not file_path.exists():
            self.log(f"File not found: {filepath}", "WARNING")
            return False
        
        self.log(f"Processing with V3 SURGICAL PRECISION: {file_path.name}")
        
        try:
            # Read file content
            with open(file_path, 'r', encoding='utf-8') as f:
                original_content = f.read()
            
            # Apply V3 surgical fixes
            modified_content, fixes_in_file = self.apply_v3_surgical_fixes(original_content, str(file_path))
            
            if fixes_in_file == 0:
                self.log(f"  No V3 surgical fixes needed for {file_path.name}")
                return True
            
            if self.dry_run:
                self.log(f"  DRY RUN: Would apply {fixes_in_file} V3 surgical fixes to {file_path.name}")
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
                
                self.log(f"  Applied {fixes_in_file} V3 surgical fixes to {file_path.name}")
                
                # CRITICAL: Battle-tested build validation after each file
                if self.validate_build(baseline_errors):
                    self.fixes_applied += fixes_in_file
                    backup_path.unlink()  # Remove backup if successful
                    self.log(f"  V3 surgical validation passed - precision achieved")
                    return True
                else:
                    self.log(f"  SAFETY ABORT: V3 surgical validation failed, rolling back", "ERROR")
                    self.restore_file(file_path, backup_path)
                    return False
                    
            except Exception as e:
                self.log(f"  Error writing file: {e}", "ERROR")
                self.restore_file(file_path, backup_path)
                return False
                
        except Exception as e:
            self.log(f"Error processing {filepath}: {e}", "ERROR")
            return False
    
    def run_v3_surgical_fixes(self) -> bool:
        """Main execution method for V3 surgical precision"""
        self.log("=== Starting xUnit1051 V3 SURGICAL PRECISION Fix Process ===")
        
        if self.dry_run:
            self.log("Running in DRY RUN mode - surgical fixes will be previewed")
        
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
        
        # Limit files if requested
        files_to_process = list(files_with_errors.keys())
        if hasattr(self, 'max_files') and self.max_files:
            files_to_process = files_to_process[:self.max_files]
            self.log(f"LIMITED TO {len(files_to_process)} files for surgical testing")
        
        # Process each file with surgical precision (abort on first failure)
        successful_files = 0
        for filepath in files_to_process:
            self.files_processed += 1
            if self.process_file_surgically(filepath):
                successful_files += 1
            else:
                self.log(f"ABORTING V3 SURGERY: Safety validation failed on {filepath}", "ERROR")
                break  # Stop on first failure for safety
        
        # Summary
        self.log("=== V3 SURGICAL PRECISION Summary ===")
        self.log(f"Files surgically processed: {self.files_processed}")
        self.log(f"Files successfully fixed: {successful_files}")
        self.log(f"Total V3 surgical fixes applied: {self.fixes_applied}")
        self.log(f"Original errors found: {self.errors_found}")
        
        if not self.dry_run:
            # Final validation
            final_errors = self.get_xunit1051_errors()
            remaining_errors = len(final_errors)
            self.log(f"Remaining xUnit1051 errors: {remaining_errors}")
            
            if remaining_errors < self.errors_found:
                reduction_percent = ((self.errors_found - remaining_errors) / self.errors_found) * 100
                self.log(f"V3 surgical error reduction: {reduction_percent:.1f}%")
                return True
            else:
                self.log("No surgical error reduction achieved", "WARNING")
                return False
        
        return True

def main():
    parser = argparse.ArgumentParser(description='Fix xUnit1051 errors with surgical precision - V3')
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
    
    print(f"xUnit1051 Error Fixer V3 - SURGICAL PRECISION")
    print(f"Project root: {project_root}")
    print(f"Mode: {'DRY RUN' if dry_run else 'REAL SURGICAL EXECUTION'}")
    print("PRECISION SURGERY IN PROGRESS...")
    print("-" * 60)
    
    fixer = XUnit1051FixerV3(project_root, dry_run=dry_run)
    fixer.max_files = args.max_files
    success = fixer.run_v3_surgical_fixes()
    
    sys.exit(0 if success else 1)

if __name__ == "__main__":
    main()