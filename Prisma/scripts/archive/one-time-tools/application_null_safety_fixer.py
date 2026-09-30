#!/usr/bin/env python3
"""
Application.UnitTests Null Safety Fixer
Systematic fix for CS8625 and CS8618 errors based on proven manual templates
"""

import os
import re
import argparse
import shutil
from pathlib import Path
from datetime import datetime

# Script Protection - Must be run through automation_recovery_manager.py
from protection_header import require_manager_execution
require_manager_execution()

class ApplicationNullSafetyFixer:
    def __init__(self):
        self.fixes_applied = 0
        self.files_modified = 0
        self.backup_dir = None
        
    def create_backup(self, target_path):
        """Create timestamped backup of target directory"""
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        backup_name = f"{target_path.name}_backup_nullsafety_{timestamp}"
        self.backup_dir = target_path.parent / backup_name
        
        print(f"Creating backup: {self.backup_dir}")
        shutil.copytree(target_path, self.backup_dir)
        return self.backup_dir
    
    def fix_cs8618_field_initialization(self, content):
        """Fix CS8618: Non-nullable field initialization in test classes"""
        patterns_fixed = 0
        
        # Pattern: private readonly Type _field; (in test classes)
        pattern = r'(private\s+readonly\s+\w+(?:<[^>]+>)?\s+_\w+)\s*;'
        replacement = r'\1 = null!;'
        
        new_content, count = re.subn(pattern, replacement, content)
        patterns_fixed += count
        
        return new_content, patterns_fixed
    
    def fix_cs8625_null_assignments(self, content):
        """Fix CS8625: null literal assignments to non-nullable types"""
        patterns_fixed = 0
        
        # Pattern 1: property = null; (in test methods)
        pattern1 = r'(\w+(?:\.\w+)*)\s*=\s*null\s*;'
        replacement1 = r'\1 = null!;'
        new_content, count1 = re.subn(pattern1, replacement1, content)
        patterns_fixed += count1
        
        # Pattern 2: property = null, (in object initializers)
        pattern2 = r'(\w+)\s*=\s*null\s*,'
        replacement2 = r'\1 = null!,'
        new_content, count2 = re.subn(pattern2, replacement2, new_content)
        patterns_fixed += count2
        
        return new_content, patterns_fixed
    
    def fix_cs8602_result_value_first_pattern(self, content):
        """Fix CS8602: Result<T>.Value.First() dereference patterns"""
        patterns_fixed = 0
        
        # Pattern: response.Value.First().PropertyName -> safe pattern
        pattern = r'(\w+)\.Value\.First\(\)\.(\w+)'
        
        def replacement(match):
            result_var = match.group(1)
            property_access = match.group(2)
            return f'''{result_var}.Value.ShouldNotBeNull();
        var firstItem = {result_var}.Value.First();
        firstItem.{property_access}'''
        
        new_content, count = re.subn(pattern, replacement, content)
        patterns_fixed += count
        
        return new_content, patterns_fixed
    
    def fix_cs8602_result_value_property_pattern(self, content):
        """Fix CS8602: var x = result.Value; x.Property -> add null check after assignment"""
        patterns_fixed = 0
        
        # Find pattern: var someVar = resultVar.Value; followed by someVar.Property
        lines = content.split('\n')
        new_lines = []
        i = 0
        
        while i < len(lines):
            line = lines[i]
            new_lines.append(line)
            
            # Look for: var variableName = resultVar.Value;
            match = re.match(r'(\s+)var\s+(\w+)\s*=\s*(\w+)\.Value;', line)
            if match:
                indent = match.group(1)
                var_name = match.group(2)
                result_var = match.group(3)
                
                # Check if next few lines use this variable without null check
                j = i + 1
                needs_null_check = False
                while j < len(lines) and j < i + 5:  # Look ahead up to 5 lines
                    next_line = lines[j]
                    # If we find usage of var_name.something and no ShouldNotBeNull
                    if re.search(rf'\b{var_name}\.', next_line) and 'ShouldNotBeNull' not in next_line:
                        needs_null_check = True
                        break
                    # Stop if we hit another variable assignment or method end
                    if re.match(r'\s+(var\s+\w+|}\s*$|private|public)', next_line):
                        break
                    j += 1
                
                if needs_null_check:
                    new_lines.append(f'{indent}{var_name}.ShouldNotBeNull();')
                    patterns_fixed += 1
            
            i += 1
        
        return '\n'.join(new_lines), patterns_fixed
    
    def fix_cs8602_result_value_count_pattern(self, content):
        """Fix CS8602: Result<T>.Value.Count dereference patterns - IMPROVED: Check for existing null checks"""
        patterns_fixed = 0
        
        # Find all .Value.Count patterns
        pattern = r'(\w+)\.Value\.Count'
        matches = list(re.finditer(pattern, content))
        
        for match in reversed(matches):  # Process in reverse to maintain positions
            result_var = match.group(1)
            
            # Look for existing ShouldNotBeNull() call in preceding lines (within 150 chars)
            start_search = max(0, match.start() - 150)
            preceding_text = content[start_search:match.start()]
            
            # Check if there's already a null check for this variable
            if f'{result_var}.Value.ShouldNotBeNull()' not in preceding_text:
                # Find proper indentation
                line_start = content.rfind('\n', 0, match.start()) + 1
                current_line = content[line_start:match.start()]
                indent = re.match(r'(\s*)', current_line).group(1)
                
                # Insert null check before the .Count access
                replacement = f'''{result_var}.Value.ShouldNotBeNull();
{indent}{result_var}.Value.Count'''
                content = content[:match.start()] + replacement + content[match.end():]
                patterns_fixed += 1
        
        return content, patterns_fixed
    
    def fix_cs8604_result_value_method_call_pattern(self, content):
        """Fix CS8604: SomeMethod(result.Value) patterns - IMPROVED: Check for existing null checks"""
        patterns_fixed = 0
        
        # Pattern 1: var something = SomeMethod.ToEntity(resultVar.Value);
        pattern1 = r'(\s+)var\s+(\w+)\s*=\s*(\w+)\.ToEntity\((\w+)\.Value\);'
        matches1 = list(re.finditer(pattern1, content))
        
        for match in reversed(matches1):
            indent = match.group(1)
            var_name = match.group(2)
            method_name = match.group(3)
            result_var = match.group(4)
            
            # Look for existing null check
            start_search = max(0, match.start() - 150)
            preceding_text = content[start_search:match.start()]
            
            if f'{result_var}.Value.ShouldNotBeNull()' not in preceding_text:
                replacement = f'''{indent}{result_var}.Value.ShouldNotBeNull();
{indent}var {var_name} = {method_name}.ToEntity({result_var}.Value);'''
                content = content[:match.start()] + replacement + content[match.end():]
                patterns_fixed += 1
        
        # Pattern 2: SomeMethod(resultVar.Value) - direct method calls
        pattern2 = r'(\s+)(\w+)\((\w+)\.Value\);'
        matches2 = list(re.finditer(pattern2, content))
        
        for match in reversed(matches2):
            indent = match.group(1)
            method_name = match.group(2)
            result_var = match.group(3)
            
            # Skip if it's an assertion method (ShouldBe, ShouldNotBeNull, etc.)
            if method_name.startswith('Should'):
                continue
                
            # Look for existing null check
            start_search = max(0, match.start() - 150)
            preceding_text = content[start_search:match.start()]
            
            if f'{result_var}.Value.ShouldNotBeNull()' not in preceding_text:
                replacement = f'''{indent}{result_var}.Value.ShouldNotBeNull();
{indent}{method_name}({result_var}.Value);'''
                content = content[:match.start()] + replacement + content[match.end():]
                patterns_fixed += 1
        
        return content, patterns_fixed
    
    def fix_cs8620_exception_nullable_pattern(self, content):
        """Fix CS8620: Exception vs Exception? in Func parameters"""
        patterns_fixed = 0
        
        # Pattern: Func<object, Exception, string> -> Func<object, Exception?, string>
        pattern = r'Func<([^>]*), Exception, ([^>]*)>'
        replacement = r'Func<\1, Exception?, \2>'
        
        new_content, count = re.subn(pattern, replacement, content)
        patterns_fixed += count
        
        return new_content, patterns_fixed
    
    def fix_cs8620_task_result_nullable_pattern(self, content):
        """Fix CS8620: Task<Result<T?>> vs Task<Result<T>> nullability differences in NSubstitute Returns calls"""
        patterns_fixed = 0
        
        # Pattern: .Returns(Task.FromResult(Result<T>.Success(null))) -> add !
        pattern1 = r'(\.Returns\(Task\.FromResult\(Result<[^>]+>\.Success\()null(\)\)\))'
        replacement1 = r'\1null!\2'
        new_content, count1 = re.subn(pattern1, replacement1, content)
        patterns_fixed += count1
        
        # Pattern: setup methods that return Task<Result<T?>> but should be Task<Result<T>>
        # Look for mock setup pattern: mockRepo.Setup().Returns() with nullable result
        pattern2 = r'(\.Setup\([^)]*\)\.Returns\(Task\.FromResult\(Result<[^>]+>\.Success\()null!(\)\)\))'
        # This pattern is already fixed by pattern1, so we focus on other variations
        
        return new_content, patterns_fixed
    
    def fix_cs8625_default_to_nullable_pattern(self, content):
        """Fix CS8625: default to non-nullable - add ! operator"""
        patterns_fixed = 0
        
        # Pattern: SomeMethod(default) -> SomeMethod(default!)
        pattern = r'(\w+)\(default\)'
        replacement = r'\1(default!)'
        
        new_content, count = re.subn(pattern, replacement, content)
        patterns_fixed += count
        
        return new_content, patterns_fixed
    
    def fix_cs8601_null_reference_assignment_pattern(self, content):
        """Fix CS8601: Possible null reference assignment"""
        patterns_fixed = 0
        
        # Pattern: variable = someMethod?.Property; -> variable = someMethod?.Property!;
        pattern = r'(\w+)\s*=\s*([^;]+\?\.[\w\.]+);'
        replacement = r'\1 = \2!;'
        
        new_content, count = re.subn(pattern, replacement, content)
        patterns_fixed += count
        
        return new_content, patterns_fixed
    
    def fix_cs8619_task_nullability_pattern(self, content):
        """Fix CS8619: Nullability differences in Task return types"""
        patterns_fixed = 0
        
        # Pattern: Task<string> doesn't match Task<string?>
        # Look for Returns() calls with Task.FromResult that need null-forgiving operator
        pattern = r'(\.Returns\(Task\.FromResult\()([^)]+)\)\)'
        
        def replacement_func(match):
            prefix = match.group(1)
            value = match.group(2)
            # If the value looks like it might be nullable but isn't marked
            if 'null' in value.lower() and '!' not in value:
                return f'{prefix}{value}!))'
            return match.group(0)
        
        new_content, count = re.subn(pattern, replacement_func, content)
        patterns_fixed += count
        
        return new_content, patterns_fixed
    
    def fix_cs8604_shouldly_assertion_pattern(self, content):
        """Fix CS8604: Shouldly assertion method arguments like ShouldContain(null)"""
        patterns_fixed = 0
        
        # Pattern: .ShouldContain(someVar) where someVar might be null
        pattern = r'(\s+)(\w+)\.ShouldContain\((\w+)\);'
        matches = list(re.finditer(pattern, content))
        
        for match in reversed(matches):
            indent = match.group(1)
            obj_name = match.group(2)
            arg_name = match.group(3)
            
            # Skip if argument already has null-forgiving operator
            if '!' in arg_name:
                continue
                
            # Add null-forgiving operator to the argument
            replacement = f'{indent}{obj_name}.ShouldContain({arg_name}!);'
            content = content[:match.start()] + replacement + content[match.end():]
            patterns_fixed += 1
        
        return content, patterns_fixed
    
    def process_file(self, file_path):
        """Process a single C# file"""
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                original_content = f.read()
            
            content = original_content
            total_fixes = 0
            
            # Apply CS8618 fixes - ENABLED for testing
            content, cs8618_fixes = self.fix_cs8618_field_initialization(content)
            total_fixes += cs8618_fixes
            
            # Apply CS8625 fixes - ENABLED for testing
            content, cs8625_fixes = self.fix_cs8625_null_assignments(content)
            total_fixes += cs8625_fixes
            
            # Apply CS8602 fixes - ENABLED: First() pattern (safe, different from Count)
            content, cs8602_first_fixes = self.fix_cs8602_result_value_first_pattern(content)
            total_fixes += cs8602_first_fixes
            
            # Apply CS8602 fixes - NEW: var x = result.Value; x.Property patterns
            content, cs8602_property_fixes = self.fix_cs8602_result_value_property_pattern(content)
            total_fixes += cs8602_property_fixes
            
            # RE-ENABLED - Count pattern with improved duplicate detection
            content, cs8602_count_fixes = self.fix_cs8602_result_value_count_pattern(content)
            total_fixes += cs8602_count_fixes
            
            # Apply CS8604 fixes - ENABLED: ToEntity method calls
            content, cs8604_method_fixes = self.fix_cs8604_result_value_method_call_pattern(content)
            total_fixes += cs8604_method_fixes
            
            # Apply CS8620 fixes - ENABLED for testing  
            content, cs8620_exception_fixes = self.fix_cs8620_exception_nullable_pattern(content)
            total_fixes += cs8620_exception_fixes
            
            # Apply CS8620 Task Result fixes - NEW
            content, cs8620_task_fixes = self.fix_cs8620_task_result_nullable_pattern(content)
            total_fixes += cs8620_task_fixes
            
            # Apply additional CS8625 fixes - ENABLED: default to default!
            content, cs8625_default_fixes = self.fix_cs8625_default_to_nullable_pattern(content)
            total_fixes += cs8625_default_fixes
            
            # Apply CS8601 fixes - NEW: null reference assignments
            content, cs8601_fixes = self.fix_cs8601_null_reference_assignment_pattern(content)
            total_fixes += cs8601_fixes
            
            # Apply CS8619 fixes - NEW: Task nullability differences
            content, cs8619_fixes = self.fix_cs8619_task_nullability_pattern(content)
            total_fixes += cs8619_fixes
            
            # Apply CS8604 fixes - NEW: Shouldly assertion arguments
            content, cs8604_shouldly_fixes = self.fix_cs8604_shouldly_assertion_pattern(content)
            total_fixes += cs8604_shouldly_fixes
            
            if total_fixes > 0:
                with open(file_path, 'w', encoding='utf-8') as f:
                    f.write(content)
                
                self.fixes_applied += total_fixes
                self.files_modified += 1
                print(f"  [OK] {file_path.name}: {cs8618_fixes} CS8618 + {cs8625_fixes+cs8625_default_fixes} CS8625 + {cs8602_first_fixes+cs8602_property_fixes} CS8602F/P + {cs8602_count_fixes} CS8602C + {cs8604_method_fixes+cs8604_shouldly_fixes} CS8604M/S + {cs8620_exception_fixes+cs8620_task_fixes} CS8620 + {cs8601_fixes} CS8601 + {cs8619_fixes} CS8619 = {total_fixes} fixes")
                
        except Exception as e:
            print(f"  [ERROR] Error processing {file_path}: {e}")
    
    def process_directory(self, target_path, test_mode=False):
        """Process all C# files in directory"""
        cs_files = list(target_path.rglob('*.cs'))
        
        if test_mode and len(cs_files) > 3:
            cs_files = cs_files[:3]  # Limit to first 3 files in test mode
            print(f"[TEST] Processing only {len(cs_files)} files")
        
        print(f"Processing {len(cs_files)} C# files...")
        
        for file_path in cs_files:
            self.process_file(file_path)
    
    def run(self, target_path, test_mode=False, create_backup=True):
        """Main execution method"""
        target_path = Path(target_path)
        
        if not target_path.exists():
            raise FileNotFoundError(f"Target path does not exist: {target_path}")
        
        print(f"[FIXER] Application.UnitTests Null Safety Fixer")
        print(f"[TARGET] {target_path}")
        print(f"[TEST MODE] {test_mode}")
        
        # Create backup if requested
        if create_backup:
            self.create_backup(target_path)
        
        # Process files
        start_time = datetime.now()
        self.process_directory(target_path, test_mode)
        elapsed = datetime.now() - start_time
        
        # Report results
        print(f"\n[RESULTS]:")
        print(f"   Files Modified: {self.files_modified}")
        print(f"   Fixes Applied: {self.fixes_applied}")
        print(f"   Time Elapsed: {elapsed.total_seconds():.2f}s")
        
        if self.backup_dir:
            print(f"   Backup Created: {self.backup_dir}")
        
        return self.fixes_applied > 0

def main():
    parser = argparse.ArgumentParser(description='Fix null safety issues in Application.UnitTests')
    parser.add_argument('target', help='Target directory path')
    parser.add_argument('--test', action='store_true', help='Test mode (process only 3 files)')
    parser.add_argument('--no-backup', action='store_true', help='Skip backup creation')
    
    args = parser.parse_args()
    
    fixer = ApplicationNullSafetyFixer()
    
    try:
        success = fixer.run(
            target_path=args.target,
            test_mode=args.test,
            create_backup=not args.no_backup
        )
        
        if success:
            print(f"\n[SUCCESS] Null safety fixes applied successfully!")
            print(f"[NEXT] Run 'dotnet build' to verify error reduction")
        else:
            print(f"\n[WARNING] No fixes were applied")
            
    except Exception as e:
        print(f"\n[ERROR] {e}")
        return 1
    
    return 0

if __name__ == "__main__":
    exit(main())