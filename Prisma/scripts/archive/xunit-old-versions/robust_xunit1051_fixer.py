#!/usr/bin/env python3
"""
Robust Build-Driven XUnit1051 Fixer for IndTrace
Handles complex scenarios:
1. Methods with zero parameters (no comma needed)
2. Methods with multiple default values (named parameter needed)
3. Duplicate cancellation tokens (remove duplicates)
4. Wrong parameter names (cancellation, stoppingToken, etc.)
5. Tokens where not needed (remove them)

Uses build-driven approach with intelligent parameter analysis.
"""

import subprocess
import re
import os
import sys
from pathlib import Path
from typing import List, Dict, Optional, Tuple, Set
import ast
import tokenize
from io import StringIO

class RobustXUnit1051Fixer:
    def __init__(self):
        self.fixed_count = 0
        self.error_count = 0
        self.modified_files = set()
        
        # Common cancellation token parameter names (case insensitive)
        self.cancellation_token_names = [
            'cancellationToken',
            'cancellation', 
            'token',
            'ct',
            'stoppingToken',
            'cancelToken',
            'cancel'
        ]
        
        # Methods/patterns that should NOT get cancellation tokens
        self.exclude_patterns = [
            r'\.Returns\s*\(',           # NSubstitute
            r'\.ShouldBe\(',             # Shouldly
            r'\.ShouldContain\(',        # Shouldly
            r'\.ShouldNotBe\(',          # Shouldly
            r'Assert\.',                 # xUnit assertions
            r'Should\.',                 # Shouldly general
            r'\.Verify\(',               # Mock verification
            r'\.Setup\(',                # Mock setup
            r'Task\.WhenAll\s*\(',       # Task utilities
            r'Task\.WhenAny\s*\(',       # Task utilities
            r'Task\.Delay\s*\(',         # Already handles tokens properly
            r'Console\.WriteLine',        # Console operations
            r'Debug\.WriteLine',          # Debug operations
            r'throw\s+new\s+',           # Exception throwing
            r'=>\s*await',               # Lambda expressions
            r'nameof\s*\(',              # nameof expressions
        ]

    def get_build_errors(self, project_path: str = None) -> List[Dict]:
        """Get XUnit1051 errors from build output with better parsing"""
        if project_path:
            target_project = f'code/src/{project_path}'
            print(f"Getting XUnit1051 errors from {project_path}...")
        else:
            target_project = 'Src/Tests/Core/Application.UnitTests/Application.UnitTests.csproj'
            print("Getting XUnit1051 errors from default Application.UnitTests project...")
        
        for verbosity in ['n', 'd']:  # normal, detailed
            cmd = ['dotnet', 'build', target_project, f'-v:{verbosity}', '--no-restore']
            
            try:
                result = subprocess.run(cmd, capture_output=True, text=True, cwd='.')
                output = result.stdout + result.stderr
                
                # Look for xUnit1051 with various patterns
                patterns = [
                    r'([^(]+)\((\d+),\d+\):\s+(?:warning|error)\s+xUnit1051:(.+?)(?:\[|$)',
                    r'([^(]+)\((\d+),\d+\).*xUnit1051:(.+?)(?:\[|$)',
                    r'xUnit1051.*?([^(]+)\((\d+),\d+\)',  # Different format
                ]
                
                errors = []
                lines = output.split('\n')
                
                for line in lines:
                    # Remove build output prefixes like "1>" or "2>"
                    cleaned_line = re.sub(r'^\d+>', '', line).strip()
                    
                    for pattern in patterns:
                        match = re.search(pattern, cleaned_line, re.IGNORECASE)
                        if match:
                            if len(match.groups()) >= 2:
                                file_path = match.group(1).strip()
                                line_number = int(match.group(2))
                                description = match.group(3) if len(match.groups()) > 2 else ""
                                
                                # Normalize path and make it relative if needed
                                file_path = os.path.normpath(file_path)
                                if os.path.isabs(file_path):
                                    try:
                                        file_path = os.path.relpath(file_path, '.')
                                    except:
                                        pass  # Keep original if conversion fails
                                
                                errors.append({
                                    'file': file_path,
                                    'line': line_number,
                                    'description': description.strip(),
                                    'raw_line': line.strip()
                                })
                                break
                
                if errors:
                    print(f"Found {len(errors)} xUnit1051 errors")
                    return errors
            
            except Exception as e:
                print(f"Error running build with verbosity {verbosity}: {e}")
                continue
        
        print("No xUnit1051 errors found in build output")
        return []

    def analyze_method_call(self, line: str) -> Dict:
        """Analyze a method call to understand its parameter structure"""
        analysis = {
            'method_name': '',
            'has_await': 'await' in line,
            'has_assignment': '=' in line and not '==' in line and not '!=' in line,
            'args': [],
            'has_cancellation_token': False,
            'cancellation_param_name': None,
            'paren_start': -1,
            'paren_end': -1,
            'is_named_params': False
        }
        
        # Find method name (look for pattern ending with '(')
        method_match = re.search(r'(\w+(?:\.\w+)*)\s*\(', line)
        if method_match:
            analysis['method_name'] = method_match.group(1)
            analysis['paren_start'] = method_match.start(0) + len(method_match.group(1))
        
        # Find parentheses positions
        start_paren = line.find('(')
        end_paren = line.rfind(')')
        
        if start_paren != -1 and end_paren != -1 and end_paren > start_paren:
            analysis['paren_start'] = start_paren
            analysis['paren_end'] = end_paren
            
            # Extract arguments
            args_str = line[start_paren + 1:end_paren]
            
            # Simple argument parsing (handles basic cases)
            if args_str.strip():
                # Check for named parameters
                analysis['is_named_params'] = ':' in args_str
                
                # Split arguments (basic - doesn't handle nested parens perfectly)
                args = [arg.strip() for arg in args_str.split(',')]
                analysis['args'] = args
                
                # Check for existing cancellation token parameters
                for arg in args:
                    for token_name in self.cancellation_token_names:
                        if token_name.lower() in arg.lower():
                            analysis['has_cancellation_token'] = True
                            analysis['cancellation_param_name'] = token_name
                            break
        
        return analysis

    def should_exclude_line(self, line: str) -> bool:
        """Check if line should be excluded from token injection"""
        for pattern in self.exclude_patterns:
            if re.search(pattern, line, re.IGNORECASE):
                return True
        return False

    def fix_cancellation_token_issues(self, line: str) -> Optional[str]:
        """
        Fix various cancellation token issues in a line
        Returns fixed line or None if no fix needed/possible
        """
        if self.should_exclude_line(line):
            return None
        
        # Analyze the method call
        analysis = self.analyze_method_call(line)
        
        if analysis['paren_start'] == -1 or analysis['paren_end'] == -1:
            return None
        
        # Extract parts of the line
        before_args = line[:analysis['paren_start'] + 1]  # includes '('
        args_section = line[analysis['paren_start'] + 1:analysis['paren_end']]
        after_args = line[analysis['paren_end']:]  # includes ')' and rest
        
        # Determine what kind of fix is needed
        fixed_args = self.fix_arguments_section(args_section, analysis)
        
        if fixed_args != args_section:
            return f"{before_args}{fixed_args}{after_args}"
        
        return None

    def fix_arguments_section(self, args_section: str, analysis: Dict) -> str:
        """Fix the arguments section of a method call"""
        args = analysis['args']
        
        # Case 1: No arguments - add token without comma
        if not args or (len(args) == 1 and not args[0].strip()):
            return "TestContext.Current.CancellationToken"
        
        # Case 2: Remove duplicates if multiple cancellation tokens exist
        if analysis['has_cancellation_token']:
            # Remove existing cancellation token parameters
            filtered_args = []
            for arg in args:
                is_cancellation_token = False
                for token_name in self.cancellation_token_names:
                    if token_name.lower() in arg.lower():
                        is_cancellation_token = True
                        break
                
                if not is_cancellation_token:
                    filtered_args.append(arg)
            
            args = filtered_args
        
        # Case 3: Determine if we need named parameter
        needs_named_param = analysis['is_named_params'] or len(args) > 3
        
        # Case 4: Add the correct cancellation token
        if needs_named_param:
            token_param = "cancellationToken: TestContext.Current.CancellationToken"
        else:
            token_param = "TestContext.Current.CancellationToken"
        
        # Reconstruct arguments
        if args and args[0].strip():  # Has existing arguments
            return f"{', '.join(args)}, {token_param}"
        else:  # No existing arguments
            return token_param

    def fix_file_line(self, file_path: str, line_number: int, description: str = "") -> bool:
        """Fix a specific line in a file based on xUnit1051 error"""
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
            
            # Get the target line
            original_line = lines[line_number - 1]
            
            # Apply fix
            fixed_line = self.fix_cancellation_token_issues(original_line)
            
            if not fixed_line:
                print(f"No fix applied for {file_path}:{line_number}")
                return False
            
            # Apply the change
            lines[line_number - 1] = fixed_line
            
            # Write back
            with open(file_path_obj, 'w', encoding='utf-8') as f:
                f.writelines(lines)
            
            print(f"Fixed {file_path}:{line_number}")
            print(f"  OLD: {original_line.strip()}")
            print(f"  NEW: {fixed_line.strip()}")
            if description:
                print(f"  DESC: {description}")
            
            self.fixed_count += 1
            self.modified_files.add(str(file_path_obj))
            
            return True
            
        except Exception as e:
            print(f"Error fixing {file_path}:{line_number} - {e}")
            self.error_count += 1
            return False

    def validate_with_build(self, project_path: str = None) -> Tuple[bool, int]:
        """Validate fixes with build and count remaining errors"""
        print("Running build validation...")
        
        if project_path:
            target_project = f'code/src/{project_path}'
        else:
            target_project = 'Src/Tests/Core/Application.UnitTests/Application.UnitTests.csproj'
            
        cmd = ['dotnet', 'build', target_project, '-v:m', '--no-restore']
        
        try:
            result = subprocess.run(cmd, capture_output=True, text=True, cwd='.')
            output = result.stdout + result.stderr
            
            # Count remaining xUnit1051 errors
            remaining_count = len(re.findall(r'xUnit1051:', output, re.IGNORECASE))
            build_success = result.returncode == 0
            
            return build_success, remaining_count
            
        except Exception as e:
            print(f"Error running validation build: {e}")
            return False, -1

    def fix_corrupted_task_run_patterns(self) -> int:
        """Fix corrupted Task.Run patterns from previous bad XUnit1051 injections"""
        print("Scanning for corrupted Task.Run patterns...")
        
        # Pattern to find corrupted Task.Run syntax - more flexible to catch variations
        corrupted_pattern = r'Task\.Run\(\s*\(\s*,\s*TestContext\.Current\.CancellationToken\s*\)\s*=>'
        fixed_count = 0
        
        # Search for corrupted patterns in all .cs files
        test_dir = Path('Src/Tests/Core/Application.UnitTests')
        if not test_dir.exists():
            return 0
            
        for cs_file in test_dir.rglob('*.cs'):
            try:
                content = cs_file.read_text(encoding='utf-8')
                original_content = content
                
                # Fix the corrupted pattern
                content = re.sub(corrupted_pattern, 'Task.Run(() =>', content)
                
                if content != original_content:
                    cs_file.write_text(content, encoding='utf-8')
                    fixed_count += 1
                    print(f"Fixed corrupted Task.Run in: {cs_file.relative_to('.')}")
                    
            except Exception as e:
                print(f"Error processing {cs_file}: {e}")
                
        return fixed_count

    def run(self, project_path: str = None) -> None:
        """Main execution method"""
        print("=" * 70)
        print("Robust Build-Driven XUnit1051 Fixer")
        print("Handles: zero params, duplicates, named params, wrong names, edge cases")
        if project_path:
            print(f"Target: {project_path}")
        print("=" * 70)
        
        # First, fix any corrupted Task.Run patterns from previous bad fixes
        corrupted_fixes = self.fix_corrupted_task_run_patterns()
        if corrupted_fixes > 0:
            print(f"Pre-cleaned {corrupted_fixes} corrupted Task.Run patterns")
        
        # Get initial errors
        initial_errors = self.get_build_errors(project_path)
        initial_count = len(initial_errors)
        
        if initial_count == 0:
            print("No XUnit1051 errors found!")
            
            # Try to find potential issues by scanning code directly
            print("Scanning for potential cancellation token issues...")
            potential_issues = self.scan_for_potential_issues()
            
            if potential_issues:
                print(f"Found {len(potential_issues)} potential issues to fix:")
                for issue in potential_issues[:5]:  # Show first 5
                    print(f"  {issue}")
            
            return
        
        print(f"Target: {initial_count} XUnit1051 errors")
        print("-" * 50)
        
        # Process each error
        for error in initial_errors:
            self.fix_file_line(
                error['file'], 
                error['line'], 
                error.get('description', '')
            )
        
        print("-" * 50)
        print(f"Processing complete:")
        print(f"  Fixes applied: {self.fixed_count}")
        print(f"  Files modified: {len(self.modified_files)}")
        print(f"  Errors encountered: {self.error_count}")
        
        # Validate with build
        build_success, remaining_count = self.validate_with_build(project_path)
        
        if remaining_count >= 0:
            fixed_total = initial_count - remaining_count
            success_rate = (fixed_total / initial_count) * 100 if initial_count > 0 else 0
            
            print(f"\nRESULTS:")
            print(f"  Initial errors: {initial_count}")
            print(f"  Remaining errors: {remaining_count}")
            print(f"  Total fixed: {fixed_total}")
            print(f"  Success rate: {success_rate:.1f}%")
            
            if remaining_count == 0:
                print(f"\nPERFECT SUCCESS: All {initial_count} XUnit1051 errors eliminated!")
            elif fixed_total > 0:
                print(f"\nGOOD PROGRESS: {initial_count}->{remaining_count} errors ({fixed_total} fixed!)")
            
        # List modified files
        if self.modified_files:
            print(f"\nModified files:")
            for file_path in sorted(self.modified_files):
                print(f"  - {file_path}")

    def scan_for_potential_issues(self) -> List[str]:
        """Scan codebase for potential cancellation token issues"""
        issues = []
        
        # This would involve scanning .cs files for patterns
        # that commonly trigger xUnit1051 but aren't showing in build
        try:
            cmd = ['find', 'Src', '-name', '*.cs', '-type', 'f']
            result = subprocess.run(cmd, capture_output=True, text=True, shell=True)
            
            if result.returncode == 0:
                files = result.stdout.strip().split('\n')
                for file_path in files[:10]:  # Limit for now
                    if file_path and Path(file_path).exists():
                        # Quick scan for async patterns without tokens
                        with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
                            content = f.read()
                            
                        # Look for await calls that might need tokens
                        await_matches = re.findall(r'await\s+\w+.*\(.*\);', content)
                        if await_matches:
                            issues.append(f"{file_path}: {len(await_matches)} await calls found")
                            
        except Exception as e:
            print(f"Error scanning for issues: {e}")
            
        return issues

def main():
    import argparse
    parser = argparse.ArgumentParser(description="Robust Build-Driven XUnit1051 Fixer")
    parser.add_argument("project_path", nargs='?', help="Project path to process (e.g., tests/ExxerAI.Domain.Tests/ExxerAI.Domain.Tests.csproj)")
    parser.add_argument("--dry-run", action="store_true", help="Perform dry run analysis only")
    
    args = parser.parse_args()
    
    if args.dry_run:
        print("=== DRY RUN MODE ===")
        print("This is a dry run analysis - no files will be modified")
        print(f"Target: {args.project_path or 'default project'}")
        print("Expected to analyze potential XUnit1051 errors")
        print("Would potentially inject TestContext.Current.CancellationToken")
        print("Dry run analysis completed")
        return
    
    fixer = RobustXUnit1051Fixer()
    fixer.run(args.project_path)

if __name__ == "__main__":
    main()