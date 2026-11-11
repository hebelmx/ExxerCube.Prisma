#!/usr/bin/env python3
"""
ExxerAI Current Test Failure Analysis Script
Analyzes and categorizes the current 74 test failures for systematic resolution.

This script processes the test log with improved Unicode handling and provides
categorized failure analysis with fix recommendations.
"""

import re
import json
from collections import defaultdict, Counter
from pathlib import Path

class CurrentFailureAnalyzer:
    def __init__(self):
        self.log_path = Path("F:/Dynamic/ExxerAi/ExxerAI/code/src/tests/ExxerAI.Application.Tests/bin/Debug/net10.0/TestResults/ExxerAI.Application.Tests_net10.0_x64.log")
        self.output_path = Path("F:/Dynamic/ExxerAi/ExxerAI/TestResults/current_failure_analysis.json")
        self.summary_path = Path("F:/Dynamic/ExxerAi/ExxerAI/TestResults/current_failure_summary.md")
        
        # Updated patterns based on the sample log content we can see
        self.failure_patterns = {
            'dependency_injection_registration': {
                'keywords': ['No service for type', 'IServiceScopeFactory', 'has been registered'],
                'priority': 1,
                'fix_type': 'service_registration',
                'description': 'Missing service registrations in DI container'
            },
            'nsubstitute_redundant_args': {
                'keywords': ['RedundantArgumentMatcherException', 'Some argument specifications', 'argument spec'],
                'priority': 1,
                'fix_type': 'mock_argument_matching',
                'description': 'NSubstitute argument matcher issues'
            },
            'constructor_dependency_errors': {
                'keywords': ['CreateInstanceDefaultCtor', 'ArgumentNullException', 'constructor'],
                'priority': 2,
                'fix_type': 'constructor_validation',
                'description': 'Constructor parameter and dependency issues'
            },
            'healthcheck_service_errors': {
                'keywords': ['HealthCheckServiceTests', 'HealthCheckService'],
                'priority': 2,
                'fix_type': 'service_implementation',
                'description': 'HealthCheckService implementation issues'
            },
            'logging_interceptor_errors': {
                'keywords': ['LoggingInterceptorServiceTests', 'LoggingInterceptorService'],
                'priority': 2,
                'fix_type': 'logging_configuration',
                'description': 'Logging interceptor configuration issues'
            },
            'business_logic_expectations': {
                'keywords': ['should be True but was False', 'should be False but was True'],
                'priority': 3,
                'fix_type': 'business_logic',
                'description': 'Business logic expectation mismatches'
            },
            'cancellation_handling': {
                'keywords': ['OperationCanceledException', 'cancellation', 'timeout'],
                'priority': 3,
                'fix_type': 'cancellation_handling',
                'description': 'Cancellation token handling issues'
            },
            'null_reference': {
                'keywords': ['NullReferenceException', 'Object reference not set'],
                'priority': 2,
                'fix_type': 'null_safety',
                'description': 'Null reference issues'
            }
        }

    def clean_unicode_log(self, content):
        """Clean up Unicode issues in the log content."""
        # Handle the null byte issues we see in the log
        content = content.replace('\x00', '')
        
        # Handle wide character spacing issues
        cleaned_lines = []
        for line in content.split('\n'):
            # Remove excessive spacing that appears to be unicode artifacts
            cleaned_line = re.sub(r'([a-zA-Z])\s+([a-zA-Z])', r'\1\2', line)
            cleaned_lines.append(cleaned_line)
        
        return '\n'.join(cleaned_lines)

    def extract_test_failures(self, log_content):
        """Extract individual test failures from log content with improved pattern matching."""
        failures = []
        
        # Clean the log content first
        cleaned_content = self.clean_unicode_log(log_content)
        
        # Pattern to match failed test entries - more flexible for unicode issues
        failure_pattern = r'failed\s+([^\(]+.*?)\s*\([^)]*\)'
        
        # Split into lines and look for failure indicators
        lines = cleaned_content.split('\n')
        current_failure = None
        collecting_error = False
        error_details = []
        
        for line in lines:
            line = line.strip()
            
            # Look for failed test pattern
            if 'failed' in line and ('Tests.' in line or 'Test.' in line):
                # Save previous failure if we were collecting one
                if current_failure is not None:
                    failures.append({
                        'test_name': current_failure,
                        'error_details': '\n'.join(error_details),
                        'full_match': current_failure + '\n' + '\n'.join(error_details)
                    })
                
                # Start new failure
                current_failure = line
                error_details = []
                collecting_error = True
            
            elif collecting_error and line:
                # Continue collecting error details until we hit another failed test or end
                if 'failed' in line and ('Tests.' in line or 'Test.' in line):
                    # This is a new failure, so process the current one first
                    if current_failure is not None:
                        failures.append({
                            'test_name': current_failure,
                            'error_details': '\n'.join(error_details),
                            'full_match': current_failure + '\n' + '\n'.join(error_details)
                        })
                    
                    # Start the new failure
                    current_failure = line
                    error_details = []
                else:
                    error_details.append(line)
        
        # Don't forget the last failure
        if current_failure is not None:
            failures.append({
                'test_name': current_failure,
                'error_details': '\n'.join(error_details),
                'full_match': current_failure + '\n' + '\n'.join(error_details)
            })
        
        return failures

    def categorize_failure(self, failure):
        """Categorize a failure based on known patterns."""
        error_text = failure['error_details'].lower()
        test_name = failure['test_name'].lower()
        
        categories = []
        
        for pattern_name, pattern_info in self.failure_patterns.items():
            for keyword in pattern_info['keywords']:
                if keyword.lower() in error_text or keyword.lower() in test_name:
                    categories.append({
                        'pattern': pattern_name,
                        'priority': pattern_info['priority'],
                        'fix_type': pattern_info['fix_type'],
                        'description': pattern_info['description'],
                        'keyword_matched': keyword
                    })
                    break
        
        # If no specific pattern matched, categorize as unknown
        if not categories:
            categories.append({
                'pattern': 'unknown',
                'priority': 4,
                'fix_type': 'investigation_required',
                'description': 'Requires individual investigation',
                'keyword_matched': 'none'
            })
        
        return categories

    def analyze_log_file(self):
        """Main analysis function."""
        if not self.log_path.exists():
            print(f"ERROR: Log file not found: {self.log_path}")
            return None
        
        print(f"Analyzing test failures from: {self.log_path}")
        
        # Try different encodings to handle unicode issues
        content = None
        for encoding in ['utf-8', 'utf-16', 'latin-1', 'cp1252']:
            try:
                with open(self.log_path, 'r', encoding=encoding, errors='ignore') as f:
                    content = f.read()
                print(f"Successfully read log with {encoding} encoding")
                break
            except Exception as e:
                print(f"Failed to read with {encoding}: {e}")
                continue
        
        if content is None:
            print("Failed to read log file with any encoding")
            return None
        
        failures = self.extract_test_failures(content)
        print(f"Found {len(failures)} test failures")
        
        if not failures:
            print("No failures found - this might indicate a parsing issue")
            print("First 500 characters of log:")
            print(repr(content[:500]))
            return None
        
        # Show first few failures for verification
        print("\nFirst few failures detected:")
        for i, failure in enumerate(failures[:3]):
            print(f"{i+1}. {failure['test_name'][:100]}...")
        
        # Categorize failures
        categorized_failures = defaultdict(list)
        priority_count = Counter()
        fix_type_count = Counter()
        
        for failure in failures:
            categories = self.categorize_failure(failure)
            
            for category in categories:
                categorized_failures[category['pattern']].append({
                    **failure,
                    'category': category
                })
                priority_count[category['priority']] += 1
                fix_type_count[category['fix_type']] += 1
        
        # Generate analysis report
        analysis = {
            'summary': {
                'total_failures': len(failures),
                'categories_found': len(categorized_failures),
                'priority_distribution': dict(priority_count),
                'fix_type_distribution': dict(fix_type_count)
            },
            'categorized_failures': dict(categorized_failures),
            'fix_recommendations': self.generate_fix_recommendations(categorized_failures)
        }
        
        return analysis

    def generate_fix_recommendations(self, categorized_failures):
        """Generate specific fix recommendations for each category."""
        recommendations = {}
        
        for pattern, failures in categorized_failures.items():
            failure_count = len(failures)
            
            if pattern == 'dependency_injection_registration':
                recommendations[pattern] = {
                    'count': failure_count,
                    'priority': 1,
                    'approach': 'Add missing service registrations to DI container',
                    'fix_template': 'services.AddScoped<IServiceScopeFactory>(); or proper test setup',
                    'estimated_time': '1-2 hours',
                    'files_to_check': ['Test constructors using IServiceScopeFactory']
                }
            
            elif pattern == 'nsubstitute_redundant_args':
                recommendations[pattern] = {
                    'count': failure_count,
                    'priority': 1,
                    'approach': 'Fix argument matchers in .Received() calls',
                    'fix_template': 'Match exact method signature parameters instead of Arg.Any<object[]>()',
                    'estimated_time': '45-90 minutes',
                    'files_to_check': ['LoggingInterceptorServiceTests.cs']
                }
            
            elif pattern == 'healthcheck_service_errors':
                recommendations[pattern] = {
                    'count': failure_count,
                    'priority': 2,
                    'approach': 'Fix HealthCheckService test constructor and dependencies',
                    'fix_template': 'Add proper IServiceScopeFactory mock or restructure tests',
                    'estimated_time': '1-2 hours',
                    'files_to_check': ['HealthCheckServiceTests.cs']
                }
            
            elif pattern == 'constructor_dependency_errors':
                recommendations[pattern] = {
                    'count': failure_count,
                    'priority': 2,
                    'approach': 'Fix constructor dependency injection in tests',
                    'fix_template': 'Add missing constructor parameters or mocks',
                    'estimated_time': '30-60 minutes',
                    'files_to_check': ['Various test constructors']
                }
            
            elif pattern == 'logging_interceptor_errors':
                recommendations[pattern] = {
                    'count': failure_count,
                    'priority': 2,
                    'approach': 'Fix logging interceptor test configuration',
                    'fix_template': 'Update mock setup and argument matching',
                    'estimated_time': '1 hour',
                    'files_to_check': ['LoggingInterceptorServiceTests.cs']
                }
            
            else:
                recommendations[pattern] = {
                    'count': failure_count,
                    'priority': 4,
                    'approach': 'Individual investigation required',
                    'fix_template': 'Case-by-case analysis',
                    'estimated_time': '30 minutes per failure',
                    'files_to_check': ['Various']
                }
        
        return recommendations

    def save_analysis(self, analysis):
        """Save analysis to JSON and markdown files."""
        # Ensure output directory exists
        self.output_path.parent.mkdir(parents=True, exist_ok=True)
        
        # Save detailed JSON
        with open(self.output_path, 'w', encoding='utf-8') as f:
            json.dump(analysis, f, indent=2, ensure_ascii=False)
        
        # Save summary markdown
        self.generate_markdown_summary(analysis)
        
        print(f"Detailed analysis saved to: {self.output_path}")
        print(f"Summary report saved to: {self.summary_path}")

    def generate_markdown_summary(self, analysis):
        """Generate a markdown summary report."""
        md_content = []
        md_content.append("# ExxerAI Current Test Failure Analysis")
        md_content.append("")
        md_content.append(f"**Analysis Date**: {Path(__file__).stat().st_mtime}")
        md_content.append(f"**Total Failures**: {analysis['summary']['total_failures']}")
        md_content.append(f"**Categories Found**: {analysis['summary']['categories_found']}")
        md_content.append("")
        
        # Priority distribution
        md_content.append("## Priority Distribution")
        for priority, count in sorted(analysis['summary']['priority_distribution'].items()):
            priority_label = {1: "🟢 Quick Wins", 2: "🟡 Medium Effort", 3: "🟠 Complex", 4: "🔴 Investigation Required"}
            md_content.append(f"- **Priority {priority}** ({priority_label.get(priority, 'Unknown')}): {count} failures")
        md_content.append("")
        
        # Fix recommendations by priority
        md_content.append("## Fix Recommendations by Priority")
        
        recommendations = analysis['fix_recommendations']
        priority_groups = defaultdict(list)
        
        for pattern, rec in recommendations.items():
            priority_groups[rec['priority']].append((pattern, rec))
        
        for priority in sorted(priority_groups.keys()):
            priority_label = {1: "🟢 Quick Wins", 2: "🟡 Medium Effort", 3: "🟠 Complex", 4: "🔴 Investigation Required"}
            md_content.append(f"### Priority {priority}: {priority_label.get(priority, 'Unknown')}")
            md_content.append("")
            
            for pattern, rec in priority_groups[priority]:
                md_content.append(f"#### {pattern.replace('_', ' ').title()} ({rec['count']} failures)")
                md_content.append(f"- **Approach**: {rec['approach']}")
                md_content.append(f"- **Fix Template**: {rec['fix_template']}")
                md_content.append(f"- **Estimated Time**: {rec['estimated_time']}")
                md_content.append(f"- **Files to Check**: {', '.join(rec['files_to_check'])}")
                md_content.append("")
        
        # Top failing test patterns
        md_content.append("## Top Failing Test Patterns")
        categorized = analysis['categorized_failures']
        sorted_categories = sorted(categorized.items(), key=lambda x: len(x[1]), reverse=True)
        
        for pattern, failures in sorted_categories[:5]:  # Top 5
            md_content.append(f"### {pattern.replace('_', ' ').title()} ({len(failures)} failures)")
            md_content.append("Example failures:")
            for failure in failures[:2]:  # Show first 2 examples
                test_name = failure['test_name'][:80] + "..." if len(failure['test_name']) > 80 else failure['test_name']
                md_content.append(f"- `{test_name}`")
            if len(failures) > 2:
                md_content.append(f"- ... and {len(failures) - 2} more")
            md_content.append("")
        
        # Action plan
        md_content.append("## Recommended Action Plan")
        md_content.append("")
        md_content.append("1. **🟢 Start with Priority 1** (Quick Wins) - Focus on DI registration and NSubstitute issues")
        md_content.append("2. **🟡 Move to Priority 2** (Medium Effort) - Fix service implementation issues")
        md_content.append("3. **🟠 Tackle Priority 3** (Complex) - Business logic and cancellation issues")
        md_content.append("4. **🔴 Individual investigation** for remaining failures")
        md_content.append("")
        total_failures = analysis['summary']['total_failures']
        estimated_fixable = sum(rec['count'] for rec in recommendations.values() if rec['priority'] <= 2)
        md_content.append(f"**Expected Outcome**: Reduce failures from {total_failures} to <{total_failures - estimated_fixable} (~{(estimated_fixable/total_failures*100):.1f}% improvement)")
        
        with open(self.summary_path, 'w', encoding='utf-8') as f:
            f.write('\n'.join(md_content))

def main():
    """Main execution function."""
    print("ExxerAI Current Test Failure Analysis")
    print("=" * 50)
    
    analyzer = CurrentFailureAnalyzer()
    analysis = analyzer.analyze_log_file()
    
    if analysis:
        analyzer.save_analysis(analysis)
        
        # Print quick summary
        print("\nQuick Summary:")
        print(f"Total failures analyzed: {analysis['summary']['total_failures']}")
        for pattern, failures in analysis['categorized_failures'].items():
            print(f"  {pattern.replace('_', ' ').title()}: {len(failures)} failures")
        
        print("\nNext Steps:")
        print("1. Review the generated markdown summary")
        print("2. Start with Priority 1 fixes (Quick Wins)")
        print("3. Use the fix templates provided")
        print("4. Run tests after each batch of fixes")
        
        return True
    else:
        print("Analysis failed - check log file location and format")
        return False

if __name__ == "__main__":
    main()