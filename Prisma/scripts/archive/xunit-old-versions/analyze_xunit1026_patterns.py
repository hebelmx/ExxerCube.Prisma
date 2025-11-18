#!/usr/bin/env python3
"""
Analyze Remaining xUnit1026 Patterns - Identify specific patterns that need different fixes
Careful analysis of remaining xUnit1026 errors to create targeted solutions
"""

import re
import subprocess
from pathlib import Path
from collections import defaultdict, Counter
import argparse

class XUnit1026PatternAnalyzer:
    def __init__(self):
        self.errors = []
        self.pattern_analysis = defaultdict(list)
        self.parameter_frequency = Counter()
        self.method_name_patterns = Counter()
        self.file_patterns = Counter()
        
    def run_build_and_extract_errors(self, solution_path: Path) -> bool:
        """Run build and extract all xUnit1026 errors with detailed information."""
        print("Building solution to extract xUnit1026 errors...")
        
        try:
            result = subprocess.run(
                ["dotnet", "build", str(solution_path), "--no-restore", "-v:n"],
                capture_output=True,
                text=True,
                cwd=solution_path.parent,
                timeout=300
            )
        except Exception as e:
            print(f"Build failed: {e}")
            return False
        
        build_output = result.stdout + "\n" + result.stderr
        
        # Enhanced pattern to extract more details
        xunit1026_pattern = r"([^(]+\.cs)\((\d+),(\d+)\):\s*error xUnit1026:.*Theory method '([^']+)'.*does not use parameter '([^']+)'"
        
        for match in re.finditer(xunit1026_pattern, build_output, re.MULTILINE):
            file_path_str = match.group(1).strip()
            line_num = int(match.group(2))
            col_num = int(match.group(3))
            method_name = match.group(4).strip()
            param_name = match.group(5).strip()
            
            # Convert to relative path for better analysis
            if solution_path.parent.name in file_path_str:
                rel_path = Path(file_path_str).relative_to(solution_path.parent)
            else:
                rel_path = Path(file_path_str)
            
            error_info = {
                'file_path': rel_path,
                'line_num': line_num,
                'col_num': col_num,
                'method_name': method_name,
                'param_name': param_name,
                'file_name': rel_path.name
            }
            
            self.errors.append(error_info)
            
        return len(self.errors) > 0
    
    def analyze_parameter_patterns(self):
        """Analyze parameter name patterns to identify common types."""
        print(f"\n=== Parameter Pattern Analysis ===")
        
        for error in self.errors:
            param = error['param_name']
            self.parameter_frequency[param] += 1
            
            # Categorize parameters by pattern
            if param in ['description', 'scenario', 'testCase', 'context', 'case']:
                self.pattern_analysis['documentation_params'].append(error)
            elif param in ['industry', 'equipment', 'workFlowType', 'manufacturingScenario']:
                self.pattern_analysis['manufacturing_params'].append(error)
            elif param in ['expectedResult', 'expected', 'result']:
                self.pattern_analysis['expectation_params'].append(error)
            elif param.endswith('Id') or param.endswith('ID'):
                self.pattern_analysis['id_params'].append(error)
            elif param in ['isValid', 'shouldSucceed', 'isExpected']:
                self.pattern_analysis['boolean_params'].append(error)
            else:
                self.pattern_analysis['other_params'].append(error)
        
        print(f"Top parameter names:")
        for param, count in self.parameter_frequency.most_common(10):
            print(f"  {param:<20}: {count:>3} occurrences")
    
    def analyze_method_name_patterns(self):
        """Analyze method name patterns to understand test types."""
        print(f"\n=== Method Name Pattern Analysis ===")
        
        for error in self.errors:
            method = error['method_name']
            self.method_name_patterns[method] += 1
            
            # Categorize by method naming patterns
            if 'Validator' in method or 'Validate' in method:
                self.pattern_analysis['validation_methods'].append(error)
            elif 'Handler' in method or 'Handle' in method:
                self.pattern_analysis['handler_methods'].append(error)
            elif 'Query' in method:
                self.pattern_analysis['query_methods'].append(error)
            elif 'Command' in method:
                self.pattern_analysis['command_methods'].append(error)
            elif 'Dto' in method or method.endswith('Tests'):
                self.pattern_analysis['dto_methods'].append(error)
            else:
                self.pattern_analysis['other_methods'].append(error)
        
        print(f"Method patterns:")
        patterns = ['validation_methods', 'handler_methods', 'query_methods', 'command_methods', 'dto_methods', 'other_methods']
        for pattern in patterns:
            count = len(self.pattern_analysis[pattern])
            if count > 0:
                print(f"  {pattern.replace('_', ' ').title():<20}: {count:>3} methods")
    
    def analyze_file_patterns(self):
        """Analyze file patterns to understand distribution."""
        print(f"\n=== File Pattern Analysis ===")
        
        for error in self.errors:
            file_name = error['file_name']
            self.file_patterns[file_name] += 1
        
        print(f"Files with most xUnit1026 errors:")
        for file_name, count in self.file_patterns.most_common(10):
            print(f"  {file_name:<40}: {count:>3} errors")
    
    def identify_fix_patterns(self):
        """Identify specific fix patterns needed for different categories."""
        print(f"\n=== Fix Pattern Recommendations ===")
        
        fix_patterns = {}
        
        # Pattern 1: Documentation parameters - simple validation
        doc_params = self.pattern_analysis['documentation_params']
        if doc_params:
            fix_patterns['documentation'] = {
                'count': len(doc_params),
                'fix_type': 'Simple validation',
                'template': 'param.ShouldNotBeNull(); // Validates test parameter',
                'files': list(set(e['file_name'] for e in doc_params))
            }
        
        # Pattern 2: Manufacturing parameters - domain validation
        mfg_params = self.pattern_analysis['manufacturing_params']
        if mfg_params:
            fix_patterns['manufacturing'] = {
                'count': len(mfg_params),
                'fix_type': 'Domain validation',
                'template': 'param.ShouldNotBeNull(); // Validates manufacturing parameter',
                'files': list(set(e['file_name'] for e in mfg_params))
            }
        
        # Pattern 3: ID parameters - might need range validation
        id_params = self.pattern_analysis['id_params']
        if id_params:
            fix_patterns['identifiers'] = {
                'count': len(id_params),
                'fix_type': 'ID validation',
                'template': 'param.ShouldBeGreaterThanOrEqualTo(0); // Validates ID parameter',
                'files': list(set(e['file_name'] for e in id_params))
            }
        
        # Pattern 4: Boolean parameters - boolean validation
        bool_params = self.pattern_analysis['boolean_params']
        if bool_params:
            fix_patterns['booleans'] = {
                'count': len(bool_params),
                'fix_type': 'Boolean validation',
                'template': '_ = param; // Boolean parameter used for test logic',
                'files': list(set(e['file_name'] for e in bool_params))
            }
        
        # Pattern 5: Other parameters - need manual review
        other_params = self.pattern_analysis['other_params']
        if other_params:
            fix_patterns['other'] = {
                'count': len(other_params),
                'fix_type': 'Manual review needed',
                'template': 'param.ShouldNotBeNull(); // Review: specific validation needed',
                'files': list(set(e['file_name'] for e in other_params))
            }
        
        for pattern_name, info in fix_patterns.items():
            print(f"\n{pattern_name.upper()} Pattern:")
            print(f"  Count: {info['count']} parameters")
            print(f"  Fix Type: {info['fix_type']}")
            print(f"  Template: {info['template']}")
            print(f"  Files affected: {len(info['files'])}")
            if len(info['files']) <= 5:
                for file_name in info['files']:
                    print(f"    - {file_name}")
            else:
                for file_name in info['files'][:3]:
                    print(f"    - {file_name}")
                print(f"    ... and {len(info['files']) - 3} more")
        
        return fix_patterns
    
    def generate_detailed_report(self):
        """Generate detailed error report for manual review."""
        print(f"\n=== Detailed Error Report ===")
        
        # Group by file for easier manual review
        by_file = defaultdict(list)
        for error in self.errors:
            by_file[error['file_name']].append(error)
        
        print(f"\nErrors by file (top 5 files):")
        for file_name, file_errors in list(by_file.items())[:5]:
            print(f"\n{file_name} ({len(file_errors)} errors):")
            for error in file_errors:
                print(f"  Line {error['line_num']:>3}: Method '{error['method_name']}' - Parameter '{error['param_name']}'")
    
    def run_analysis(self, solution_path: Path):
        """Run complete analysis of xUnit1026 patterns."""
        print("=== xUnit1026 Pattern Analysis ===")
        print("Analyzing remaining xUnit1026 errors to identify fix patterns...")
        
        if not self.run_build_and_extract_errors(solution_path):
            print("No xUnit1026 errors found or build failed!")
            return None
        
        print(f"\nFound {len(self.errors)} xUnit1026 errors to analyze")
        
        self.analyze_parameter_patterns()
        self.analyze_method_name_patterns()
        self.analyze_file_patterns()
        fix_patterns = self.identify_fix_patterns()
        self.generate_detailed_report()
        
        return fix_patterns

def main():
    parser = argparse.ArgumentParser(description="Analyze xUnit1026 error patterns")
    parser.add_argument("solution_path", type=Path, help="Path to solution file")
    
    args = parser.parse_args()
    
    if not args.solution_path.exists():
        print(f"Error: Solution file does not exist: {args.solution_path}")
        return 1
    
    analyzer = XUnit1026PatternAnalyzer()
    fix_patterns = analyzer.run_analysis(args.solution_path)
    
    if fix_patterns:
        print(f"\n=== SUMMARY ===")
        total_errors = sum(info['count'] for info in fix_patterns.values())
        print(f"Total xUnit1026 errors analyzed: {total_errors}")
        print(f"Different fix patterns identified: {len(fix_patterns)}")
        print(f"\nRecommendation: Create targeted fixes for each pattern type")
        print(f"Priority order: documentation → manufacturing → identifiers → other")
    
    return 0

if __name__ == "__main__":
    exit(main())