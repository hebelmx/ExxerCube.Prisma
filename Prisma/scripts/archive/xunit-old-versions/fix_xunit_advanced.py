#!/usr/bin/env python3
"""
Advanced xUnit fixer for xUnit1011, xUnit1013 and other xUnit analyzer issues.
Uses proven patterns from our successful Domain.UnitTests fixes.
"""

import os
import re
import subprocess
import shutil
from datetime import datetime
from typing import List, Tuple

class AdvancedXUnitFixer:
    def __init__(self, project_path: str):
        self.project_path = project_path
        self.backup_dir = f"{project_path}_backup_xunit_advanced_{datetime.now().strftime('%Y%m%d_%H%M%S')}"
        
    def create_backup(self) -> bool:
        """Create backup of project directory."""
        try:
            if os.path.exists(self.backup_dir):
                shutil.rmtree(self.backup_dir)
            shutil.copytree(self.project_path, self.backup_dir)
            print(f"[SUCCESS] Created backup: {self.backup_dir}")
            return True
        except Exception as e:
            print(f"[ERROR] Failed to create backup: {e}")
            return False
    
    def get_xunit_advanced_errors(self) -> dict:
        """Get xUnit1011, xUnit1013 and other advanced xUnit errors from build output."""
        try:
            result = subprocess.run([
                "dotnet", "build", 
                os.path.join(self.project_path, "Application.AgregationTests.csproj"),
                "--no-restore", "--verbosity", "normal"
            ], capture_output=True, text=True, cwd=os.path.dirname(self.project_path))
            
            full_output = result.stdout + "\n" + result.stderr
            
            xunit_errors = {
                'xUnit1011': [],  # There is no matching method (fact/theory methods)
                'xUnit1013': [],  # Public method should be marked as test
                'xUnit1012': [],  # Null should not be used for non-nullable type parameter
                'xUnit1051': [],  # CancellationToken usage
                'xUnit1026': []   # Parameter not used (in case there are more)
            }
            
            for line in full_output.split('\n'):
                for error_type in xunit_errors.keys():
                    if error_type in line and '.cs(' in line:
                        # Extract file path, line, and details
                        path_match = re.search(r'([^>]*\.cs)\((\d+),(\d+)\):', line)
                        
                        if path_match:
                            file_path = path_match.group(1)
                            if '>' in file_path:
                                file_path = file_path.split('>')[-1]
                            line_num = int(path_match.group(2))
                            col_num = int(path_match.group(3))
                            
                            if os.path.exists(file_path):
                                xunit_errors[error_type].append((file_path, line_num, col_num, line.strip()))
            
            return xunit_errors
        except Exception as e:
            print(f"Error getting advanced xUnit errors: {e}")
            return {}
    
    def fix_xunit1011(self, errors: List[Tuple]) -> int:
        """Fix xUnit1011 - There is no matching method."""
        print("Fixing xUnit1011 errors...")
        fixes = 0
        
        for file_path, line_num, col_num, error_msg in errors:
            try:
                with open(file_path, 'r', encoding='utf-8', errors='replace') as f:
                    lines = f.readlines()
                
                if line_num > len(lines):
                    continue
                
                line = lines[line_num - 1]
                
                # Common xUnit1011 fixes:
                # 1. Missing [Fact] or [Theory] attribute
                if 'public' in line and ('Test' in line or 'Should' in line) and 'async Task' in line:
                    # Check if previous line has attribute
                    if line_num > 1:
                        prev_line = lines[line_num - 2].strip()
                        if not prev_line.startswith('[') and not any(attr in prev_line for attr in ['[Fact]', '[Theory]']):
                            # Add [Fact] attribute
                            indent = len(line) - len(line.lstrip())
                            lines.insert(line_num - 1, ' ' * indent + '[Fact]\n')
                            fixes += 1
                
                # 2. Missing async keyword
                elif 'public' in line and 'Task' in line and 'async' not in line and ('Test' in line or 'Should' in line):
                    new_line = line.replace('public Task', 'public async Task')
                    if new_line != line:
                        lines[line_num - 1] = new_line
                        fixes += 1
                
                if fixes > 0:
                    with open(file_path, 'w', encoding='utf-8', newline='') as f:
                        f.writelines(lines)
                    print(f"  Fixed xUnit1011 in {os.path.basename(file_path)}:{line_num}")
                    
            except Exception as e:
                print(f"Error fixing xUnit1011 in {file_path}:{line_num}: {e}")
        
        return fixes
    
    def fix_xunit1013(self, errors: List[Tuple]) -> int:
        """Fix xUnit1013 - Public method should be marked as test."""
        print("Fixing xUnit1013 errors...")
        fixes = 0
        
        for file_path, line_num, col_num, error_msg in errors:
            try:
                with open(file_path, 'r', encoding='utf-8', errors='replace') as f:
                    lines = f.readlines()
                
                if line_num > len(lines):
                    continue
                
                line = lines[line_num - 1]
                
                # If it's a public method that looks like a test but missing attribute
                if 'public' in line and any(keyword in line for keyword in ['Test', 'Should', 'Verify', 'Check']):
                    # Check if it already has a test attribute
                    has_test_attribute = False
                    for i in range(max(0, line_num - 3), line_num):
                        if i < len(lines) and any(attr in lines[i] for attr in ['[Fact]', '[Theory]', '[Test]']):
                            has_test_attribute = True
                            break
                    
                    if not has_test_attribute:
                        # Add appropriate attribute
                        indent = len(line) - len(line.lstrip())
                        
                        # Determine if it should be [Theory] or [Fact]
                        method_text = line.lower()
                        if 'parameters' in method_text or 'string ' in line or 'int ' in line:
                            attribute_line = ' ' * indent + '[Theory]\n'
                        else:
                            attribute_line = ' ' * indent + '[Fact]\n'
                        
                        lines.insert(line_num - 1, attribute_line)
                        fixes += 1
                        
                        with open(file_path, 'w', encoding='utf-8', newline='') as f:
                            f.writelines(lines)
                        print(f"  Fixed xUnit1013 in {os.path.basename(file_path)}:{line_num}")
                    
            except Exception as e:
                print(f"Error fixing xUnit1013 in {file_path}:{line_num}: {e}")
        
        return fixes
    
    def fix_xunit1012(self, errors: List[Tuple]) -> int:
        """Fix xUnit1012 - Null should not be used for non-nullable type parameter."""
        print("Fixing xUnit1012 errors...")
        fixes = 0
        
        for file_path, line_num, col_num, error_msg in errors:
            try:
                with open(file_path, 'r', encoding='utf-8', errors='replace') as f:
                    content = f.read()
                
                lines = content.split('\n')
                
                # Find the method signature and make parameter nullable
                for i in range(max(0, line_num - 5), min(len(lines), line_num + 5)):
                    line = lines[i]
                    
                    # Look for InlineData with null and the method signature
                    if 'InlineData(' in line and 'null' in line:
                        # Find the method signature
                        for j in range(i, min(len(lines), i + 10)):
                            method_line = lines[j]
                            if 'public' in method_line and '(' in method_line and ')' in method_line:
                                # Make string parameters nullable
                                new_method_line = re.sub(r'\bstring\s+(\w+)', r'string? \1', method_line)
                                if new_method_line != method_line:
                                    lines[j] = new_method_line
                                    fixes += 1
                                    break
                        break
                
                if fixes > 0:
                    with open(file_path, 'w', encoding='utf-8', newline='') as f:
                        f.write('\n'.join(lines))
                    print(f"  Fixed xUnit1012 in {os.path.basename(file_path)}:{line_num}")
                    
            except Exception as e:
                print(f"Error fixing xUnit1012 in {file_path}:{line_num}: {e}")
        
        return fixes
    
    def run_advanced_fixes(self) -> bool:
        """Run all advanced xUnit fixes."""
        print("=" * 60)
        print("ADVANCED xUnit FIXES: xUnit1011, xUnit1013, xUnit1012")
        print("=" * 60)
        
        if not self.create_backup():
            return False
        
        try:
            # Get advanced xUnit errors
            print("Analyzing xUnit errors...")
            xunit_errors = self.get_xunit_advanced_errors()
            
            total_errors = sum(len(error_list) for error_list in xunit_errors.values())
            print(f"Found {total_errors} advanced xUnit errors:")
            
            for error_type, error_list in xunit_errors.items():
                if error_list:
                    print(f"  {error_type}: {len(error_list)} errors")
            
            if total_errors == 0:
                print("No advanced xUnit errors found!")
                return True
            
            total_fixes = 0
            
            # Apply fixes for each error type
            if xunit_errors['xUnit1011']:
                fixes = self.fix_xunit1011(xunit_errors['xUnit1011'])
                total_fixes += fixes
                print(f"Applied {fixes} xUnit1011 fixes")
            
            if xunit_errors['xUnit1013']:
                fixes = self.fix_xunit1013(xunit_errors['xUnit1013'])
                total_fixes += fixes
                print(f"Applied {fixes} xUnit1013 fixes")
            
            if xunit_errors['xUnit1012']:
                fixes = self.fix_xunit1012(xunit_errors['xUnit1012'])
                total_fixes += fixes
                print(f"Applied {fixes} xUnit1012 fixes")
            
            print(f"\nTotal advanced xUnit fixes applied: {total_fixes}")
            
            # Check results
            print("Checking results...")
            final_errors = self.get_xunit_advanced_errors()
            final_total = sum(len(error_list) for error_list in final_errors.values())
            
            print(f"\nRESULTS:")
            print(f"  Initial advanced xUnit errors: {total_errors}")
            print(f"  Final advanced xUnit errors: {final_total}")
            print(f"  Fixes applied: {total_fixes}")
            
            if final_total < total_errors:
                reduction = total_errors - final_total
                print(f"  [SUCCESS] Reduced advanced xUnit errors by {reduction}")
                return True
            else:
                print(f"  [INFO] Manual attention may be needed for complex cases")
                return True
                
        except Exception as e:
            print(f"Unexpected error: {e}")
            return False

def main():
    project_path = r"F:\Dynamic\IndTraceV2025\Src\Tests\Core\Aggregation.BoundedTests"
    
    if not os.path.exists(project_path):
        print(f"Error: Project path not found: {project_path}")
        return
    
    fixer = AdvancedXUnitFixer(project_path)
    success = fixer.run_advanced_fixes()
    
    if success:
        print(f"\n[COMPLETE] Advanced xUnit fixes applied")
    else:
        print(f"\n[FAILED] Check logs for issues")

if __name__ == "__main__":
    main()