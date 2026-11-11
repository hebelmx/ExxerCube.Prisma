#!/usr/bin/env python3
"""
xUnit1051 Cancellation Token Fixer for IndTrace Application.UnitTests

Fixes xUnit1051: Calls to methods which accept CancellationToken should use 
TestContext.Current.CancellationToken to allow test cancellation to be more responsive.

Pattern:
- Replace CancellationToken.None with TestContext.Current.CancellationToken
- Replace new CancellationToken() with TestContext.Current.CancellationToken
- Replace default CancellationToken parameters with TestContext.Current.CancellationToken
"""

import re
import os
import sys
from pathlib import Path
from typing import Tuple, List

# Script Protection - Must be run through automation_recovery_manager.py
from protection_header import require_manager_execution
require_manager_execution()

class XUnit1051Fixer:
    def __init__(self):
        self.files_processed = 0
        self.total_fixes = 0
        self.error_files = []
        
    def fix_cancellation_token_patterns(self, content: str) -> Tuple[str, int]:
        """Fix various CancellationToken patterns to use TestContext.Current.CancellationToken"""
        patterns_fixed = 0
        
        # Pattern 1: CancellationToken.None
        pattern1 = r'CancellationToken\.None'
        replacement1 = r'TestContext.Current.CancellationToken'
        new_content, count1 = re.subn(pattern1, replacement1, content)
        patterns_fixed += count1
        
        # Pattern 2: new CancellationToken()
        pattern2 = r'new\s+CancellationToken\(\s*\)'
        replacement2 = r'TestContext.Current.CancellationToken'
        new_content, count2 = re.subn(pattern2, replacement2, new_content)
        patterns_fixed += count2
        
        # Pattern 3: default(CancellationToken)
        pattern3 = r'default\(CancellationToken\)'
        replacement3 = r'TestContext.Current.CancellationToken'
        new_content, count3 = re.subn(pattern3, replacement3, new_content)
        patterns_fixed += count3
        
        # Pattern 4: = default as last parameter in method calls
        # Look for patterns like: SomeMethod(param1, param2, default)
        pattern4 = r'(\([^)]*,\s*)default\s*\)'
        
        # Only replace if this is likely a CancellationToken parameter
        def replace_default_param(match):
            # Check if this looks like it's in a test method by looking at context
            start_pos = max(0, match.start() - 200)
            context = content[start_pos:match.start()]
            
            # If it's in a test method (has [Fact] or [Theory] nearby) and looks like async call
            if ('[Fact]' in context or '[Theory]' in context) and ('await' in context or 'async' in context):
                return match.group(1) + 'TestContext.Current.CancellationToken)'
            return match.group(0)
        
        new_content, count4 = re.subn(pattern4, replace_default_param, new_content)
        patterns_fixed += count4
        
        # Pattern 5: cancellationToken: default
        pattern5 = r'cancellationToken:\s*default'
        replacement5 = r'cancellationToken: TestContext.Current.CancellationToken'
        new_content, count5 = re.subn(pattern5, replacement5, new_content)
        patterns_fixed += count5
        
        return new_content, patterns_fixed
    
    def process_file(self, file_path: Path) -> bool:
        """Process a single file to fix xUnit1051 patterns"""
        try:
            # Read file
            content = file_path.read_text(encoding='utf-8')
            
            # Skip if no test attributes found
            if '[Fact]' not in content and '[Theory]' not in content:
                return False
            
            # Apply fixes
            fixed_content, fix_count = self.fix_cancellation_token_patterns(content)
            
            if fix_count > 0:
                # Write back the fixed content
                file_path.write_text(fixed_content, encoding='utf-8')
                self.total_fixes += fix_count
                print(f"Fixed {fix_count} xUnit1051 patterns in {file_path.name}")
                return True
                
            return False
            
        except Exception as e:
            self.error_files.append((file_path, str(e)))
            print(f"Error processing {file_path}: {e}")
            return False
    
    def process_directory(self, directory: Path, pattern: str = "*.cs") -> None:
        """Process all matching files in directory recursively"""
        cs_files = list(directory.rglob(pattern))
        print(f"Found {len(cs_files)} C# files to process")
        
        for file_path in cs_files:
            if self.process_file(file_path):
                self.files_processed += 1
    
    def print_summary(self) -> None:
        """Print processing summary"""
        print("\n" + "="*60)
        print("xUnit1051 Cancellation Token Fix Summary")
        print("="*60)
        print(f"Files processed: {self.files_processed}")
        print(f"Total fixes applied: {self.total_fixes}")
        
        if self.error_files:
            print(f"\nErrors encountered in {len(self.error_files)} files:")
            for file_path, error in self.error_files[:5]:
                print(f"  - {file_path.name}: {error}")
            if len(self.error_files) > 5:
                print(f"  ... and {len(self.error_files) - 5} more")

def main():
    if len(sys.argv) < 2:
        print("Usage: python xunit1051_cancellation_token_fixer.py <directory_path>")
        print("Example: python xunit1051_cancellation_token_fixer.py Src/Tests/Core/Application.UnitTests")
        sys.exit(1)
    
    directory_path = Path(sys.argv[1])
    
    if not directory_path.exists():
        print(f"Error: Directory '{directory_path}' does not exist")
        sys.exit(1)
    
    print(f"Processing xUnit1051 fixes in: {directory_path}")
    print("Replacing CancellationToken.None with TestContext.Current.CancellationToken")
    print("-" * 60)
    
    fixer = XUnit1051Fixer()
    fixer.process_directory(directory_path)
    fixer.print_summary()

if __name__ == "__main__":
    main()