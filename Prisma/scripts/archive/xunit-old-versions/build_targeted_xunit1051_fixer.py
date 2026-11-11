#!/usr/bin/env python3
"""
Build-Driven XUnit1051 Fixer for IndTrace
Fixes xUnit1051: Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken

Following the established build-driven methodology:
1. Parse build output for specific xUnit1051 errors
2. Target exact files and line numbers
3. Apply minimal, surgical fixes
4. Validate with immediate build verification

Based on insights from previous IndFusion projects but adapted for IndTrace's build-driven approach.
"""

import subprocess
import re
import os
import sys
from pathlib import Path
from typing import List, Dict, Optional, Tuple

class BuildTargetedXUnit1051Fixer:
    def __init__(self):
        self.fixed_count = 0
        self.error_count = 0
        self.modified_files = set()
        
        # Skip patterns - methods that shouldn't get token injection
        self.skip_patterns = [
            r'\.Returns\s*\(',           # NSubstitute Returns()
            r'\.ShouldBe\(',             # Shouldly assertions
            r'Assert\.',                 # xUnit assertions
            r'Task\.WhenAll\s*\(',       # Task.WhenAll
            r'TestContext\.Current\.CancellationToken',  # Already has token
            r'cancellationToken[:=]',    # Already has parameter
            r'nameof\s*\(',              # nameof expressions
            r'throw\s+new\s+',           # Exception throwing
            r'=>\s*await',               # Lambda expressions
            r'mock\.',                   # Mock setups (case insensitive handled below)
        ]
        
        # Include patterns - lines that should get token injection
        self.include_patterns = [
            r'await\s+\w+.*Async\s*\(',   # await calls to Async methods
            r'var\s+\w+\s*=\s*await\s+\w+.*Async\s*\(',  # assignment with await
        ]

    def get_build_errors(self) -> List[Dict]:
        """Get XUnit1051 errors from build output"""
        print("Getting XUnit1051 errors from build...")
        
        cmd = ['dotnet', 'build', 'Src/IndTrace.sln', '-v:n', '--no-restore']
        
        try:
            result = subprocess.run(cmd, capture_output=True, text=True, cwd='.')
            output = result.stdout + result.stderr
            
            # Parse XUnit1051 errors
            # Example: F:\Path\File.cs(123,45): warning xUnit1051: description [project.csproj]
            xunit_pattern = r'([^(]+)\((\d+),\d+\):\s+(?:warning|error)\s+xUnit1051:'
            
            errors = []
            lines = output.split('\n')
            
            for line in lines:
                match = re.search(xunit_pattern, line)
                if match:
                    file_path = match.group(1).strip()
                    line_number = int(match.group(2))
                    
                    # Convert to Path and make relative if needed
                    file_path = os.path.normpath(file_path)
                    
                    errors.append({
                        'file': file_path,
                        'line': line_number,
                        'raw_line': line.strip()
                    })
            
            print(f"Found {len(errors)} xUnit1051 errors")
            return errors
            
        except Exception as e:
            print(f"Error running build: {e}")
            return []

    def should_inject_token(self, line: str) -> bool:
        """Check if a line should get token injection"""
        line_lower = line.lower()
        
        # Check include patterns first
        has_include_pattern = any(re.search(pattern, line, re.IGNORECASE) for pattern in self.include_patterns)
        if not has_include_pattern:
            return False
        
        # Check skip patterns
        has_skip_pattern = any(re.search(pattern, line, re.IGNORECASE) for pattern in self.skip_patterns)
        if has_skip_pattern:
            return False
        
        # Additional case-insensitive checks
        if 'mock' in line_lower:
            return False
            
        # Must end with semicolon (complete statement)
        if not line.strip().endswith(';'):
            return False
            
        return True

    def inject_cancellation_token(self, line: str) -> Optional[str]:
        """
        Inject TestContext.Current.CancellationToken into the method call
        Returns fixed line or None if no fix applied
        """
        
        # Find the last closing parenthesis before the semicolon
        stripped = line.rstrip()
        if not stripped.endswith(';'):
            return None
            
        # Find the position of the last ')' before ';'
        paren_pos = stripped.rfind(')', 0, -1)  # Exclude the semicolon
        if paren_pos == -1:
            return None
        
        # Extract parts
        before_paren = stripped[:paren_pos]
        after_paren = stripped[paren_pos:]
        
        # Check if there are already arguments
        open_paren_pos = before_paren.rfind('(')
        if open_paren_pos == -1:
            return None
        
        args_section = before_paren[open_paren_pos + 1:]
        
        # Determine injection format
        token_param = "TestContext.Current.CancellationToken"
        
        if args_section.strip():  # Has existing arguments
            # Add comma and token
            fixed_line = f"{before_paren}, {token_param}{after_paren}"
        else:  # No existing arguments
            # Add token only
            fixed_line = f"{before_paren}{token_param}{after_paren}"
        
        return fixed_line

    def fix_file_line(self, file_path: str, line_number: int) -> bool:
        """
        Fix a specific line in a specific file
        Returns True if fix was applied, False otherwise
        """
        try:
            file_path_obj = Path(file_path)
            if not file_path_obj.exists():
                print(f"File not found: {file_path}")
                return False
            
            # Read file
            with open(file_path_obj, 'r', encoding='utf-8') as f:
                lines = f.readlines()
            
            if line_number < 1 or line_number > len(lines):
                print(f"Line {line_number} out of range in {file_path}")
                return False
            
            # Get the target line (1-based indexing)
            target_line = lines[line_number - 1]
            
            # Check if we should inject token
            if not self.should_inject_token(target_line):
                return False
            
            # Apply the fix
            fixed_line = self.inject_cancellation_token(target_line)
            if not fixed_line:
                return False
            
            # Check if already fixed
            if "TestContext.Current.CancellationToken" in target_line:
                return False
            
            # Apply the change
            lines[line_number - 1] = fixed_line
            
            # Write back
            with open(file_path_obj, 'w', encoding='utf-8') as f:
                f.writelines(lines)
            
            print(f"Fixed {file_path}:{line_number}")
            print(f"  OLD: {target_line.strip()}")
            print(f"  NEW: {fixed_line.strip()}")
            
            self.fixed_count += 1
            self.modified_files.add(str(file_path_obj))
            
            return True
            
        except Exception as e:
            print(f"Error fixing {file_path}:{line_number} - {e}")
            self.error_count += 1
            return False

    def run_build_validation(self) -> Tuple[bool, int]:
        """Run build to validate fixes and count remaining errors"""
        print("Running build validation...")
        
        cmd = ['dotnet', 'build', 'Src/IndTrace.sln', '-v:m', '--no-restore']
        
        try:
            result = subprocess.run(cmd, capture_output=True, text=True, cwd='.')
            output = result.stdout + result.stderr
            
            # Count remaining xUnit1051 errors
            remaining_count = len(re.findall(r'xUnit1051:', output))
            build_success = result.returncode == 0
            
            return build_success, remaining_count
            
        except Exception as e:
            print(f"Error running validation build: {e}")
            return False, -1

    def run(self) -> None:
        """Main execution method"""
        print("=" * 60)
        print("Build-Driven XUnit1051 Fixer")
        print("=" * 60)
        
        # Get initial count
        initial_errors = self.get_build_errors()
        initial_count = len(initial_errors)
        
        if initial_count == 0:
            print("No XUnit1051 errors found!")
            return
        
        print(f"Target: {initial_count} XUnit1051 errors")
        print("-" * 40)
        
        # Process each error
        for error in initial_errors:
            self.fix_file_line(error['file'], error['line'])
        
        print("-" * 40)
        print(f"Processing complete:")
        print(f"  Fixes applied: {self.fixed_count}")
        print(f"  Files modified: {len(self.modified_files)}")
        print(f"  Errors encountered: {self.error_count}")
        
        # Validate with build
        build_success, remaining_count = self.run_build_validation()
        
        if remaining_count >= 0:
            fixed_total = initial_count - remaining_count
            success_rate = (fixed_total / initial_count) * 100 if initial_count > 0 else 0
            
            print(f"\nRESULTS:")
            print(f"  Initial errors: {initial_count}")
            print(f"  Remaining errors: {remaining_count}")
            print(f"  Total fixed: {fixed_total}")
            print(f"  Success rate: {success_rate:.1f}%")
            
            if fixed_total > 0:
                print(f"\n🎉 XUNIT1051 SUCCESS: {initial_count}→{remaining_count} errors ({fixed_total} fixed!)")
            
            if not build_success and remaining_count > 0:
                print("\nNote: Build still has warnings/errors - this is expected with TreatWarningsAsErrors=true")

        # List modified files
        if self.modified_files:
            print(f"\nModified files:")
            for file_path in sorted(self.modified_files):
                print(f"  - {file_path}")

def main():
    if len(sys.argv) > 1 and sys.argv[1] in ['-h', '--help']:
        print("Build-Driven XUnit1051 Fixer")
        print("Usage: python build_targeted_xunit1051_fixer.py")
        print("\nFixes xUnit1051 warnings by injecting TestContext.Current.CancellationToken")
        print("Uses build output to target specific files and line numbers.")
        return
    
    fixer = BuildTargetedXUnit1051Fixer()
    fixer.run()

if __name__ == "__main__":
    main()