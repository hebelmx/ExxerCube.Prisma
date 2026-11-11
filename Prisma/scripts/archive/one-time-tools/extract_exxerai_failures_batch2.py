#!/usr/bin/env python3
"""
ExxerAI Test Failure Analysis Script - Batch 2
Analyzes and categorizes the remaining 107 test failures for systematic resolution.

Usage:
    python extract_exxerai_failures_batch2.py

Input: Test log file from Application.Tests
Output: Categorized failure report with fix recommendations
"""

import re
import json
from collections import defaultdict, Counter
from pathlib import Path

class ExxerAIFailureAnalyzer:
    def __init__(self):
        self.log_path = Path("F:/Dynamic/ExxerAi/ExxerAI/code/src/tests/ExxerAI.Application.Tests/bin/Debug/net10.0/TestResults/ExxerAI.Application.Tests_net10.0_x64.log")
        self.output_path = Path("F:/Dynamic/ExxerAi/ExxerAI/TestResults/failure_analysis_batch2.json")
        self.summary_path = Path("F:/Dynamic/ExxerAi/ExxerAI/TestResults/failure_summary_batch2.md")
        
        # Known patterns from Batch 1 analysis
        self.failure_patterns = {
            'document_id_validation': {
                'keywords': ['DocumentId cannot be null or empty', 'Document ID cannot be null or empty'],
                'priority': 1,
                'fix_type': 'constructor_validation'
            },
            'nsubstitute_redundant_args': {
                'keywords': ['RedundantArgumentMatcherException', 'Some argument specifications'],
                'priority': 1,
                'fix_type': 'mock_argument_matching'
            },
            'human_review_cancellation': {
                'keywords': ['HumanReviewCheckpointService', 'Test execution timed out', 'should throw OperationCanceledException'],
                'priority': 3,
                'fix_type': 'cancellation_handling'
            },
            'business_logic_expectations': {
                'keywords': ['should be True but was False', 'should be False but was True'],
                'priority': 2,
                'fix_type': 'business_logic'
            },
            'null_reference': {
                'keywords': ['NullReferenceException', 'Object reference not set'],
                'priority': 2,
                'fix_type': 'null_safety'
            },
            'float_precision': {
                'keywords': ['but was 0.050000012f', 'floating point'],
                'priority': 1,
                'fix_type': 'precision_tolerance'
            },
            'concept_extraction': {
                'keywords': ['ConceptExtractionEngine', 'concept types'],
                'priority': 2,
                'fix_type': 'mock_configuration'
            },
            'workflow_service': {
                'keywords': ['WorkflowService', 'repository'],
                'priority': 2,
                'fix_type': 'di_configuration'
            }
        }

    def extract_test_failures(self, log_content):
        """Extract individual test failures from log content."""
        failures = []
        
        # Pattern to match failed test entries
        failure_pattern = r'failed\s+([^\(]+\([^)]*\))\s*\(.*?\n(.*?)(?=failed|passed|\[|\n\n|$)'
        
        matches = re.finditer(failure_pattern, log_content, re.DOTALL | re.MULTILINE)
        
        for match in matches:
            test_name = match.group(1).strip()
            error_details = match.group(2).strip()
            
            failures.append({
                'test_name': test_name,
                'error_details': error_details,
                'full_match': match.group(0)
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
                        'keyword_matched': keyword
                    })
                    break
        
        # If no specific pattern matched, categorize as unknown
        if not categories:
            categories.append({
                'pattern': 'unknown',
                'priority': 4,
                'fix_type': 'investigation_required',
                'keyword_matched': 'none'
            })
        
        return categories

    def analyze_log_file(self):
        """Main analysis function."""
        if not self.log_path.exists():
            print(f"ERROR: Log file not found: {self.log_path}")
            return None
        
        print(f"Analyzing test failures from: {self.log_path}")
        
        with open(self.log_path, 'r', encoding='utf-8', errors='ignore') as f:
            log_content = f.read()
        
        failures = self.extract_test_failures(log_content)
        print(f"Found {len(failures)} test failures")
        
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
            
            if pattern == 'document_id_validation':
                recommendations[pattern] = {
                    'count': failure_count,
                    'priority': 1,
                    'approach': 'Pattern-based fix using MultiEdit or search/replace',
                    'fix_template': 'Replace `new DocumentId("")` with `Should.Throw<ArgumentException>(() => new DocumentId(""))`',
                    'estimated_time': '30-60 minutes',
                    'files_to_check': ['*DocumentIngestion*Tests.cs', '*DocumentProcessing*Tests.cs']
                }
            
            elif pattern == 'nsubstitute_redundant_args':
                recommendations[pattern] = {
                    'count': failure_count,
                    'priority': 1,
                    'approach': 'Fix argument matchers in .Received() calls',
                    'fix_template': 'Match exact method signature parameters instead of Arg.Any<object[]>()',
                    'estimated_time': '45-90 minutes',
                    'files_to_check': ['*Service*Tests.cs']
                }
            
            elif pattern == 'human_review_cancellation':
                recommendations[pattern] = {
                    'count': failure_count,
                    'priority': 3,
                    'approach': 'Investigate cancellation token handling and timeout issues',
                    'fix_template': 'Review service implementation and mock configuration',
                    'estimated_time': '2-3 hours',
                    'files_to_check': ['HumanReviewCheckpointServiceTests.cs']
                }
            
            elif pattern == 'business_logic_expectations':
                recommendations[pattern] = {
                    'count': failure_count,
                    'priority': 2,
                    'approach': 'Review test expectations vs implementation behavior',
                    'fix_template': 'Update test assertions or fix mock configurations',
                    'estimated_time': '1-2 hours',
                    'files_to_check': ['Various service test files']
                }
            
            elif pattern == 'null_reference':
                recommendations[pattern] = {
                    'count': failure_count,
                    'priority': 2,
                    'approach': 'Add null checks and complete dependency setup',
                    'fix_template': 'Add `if (param == null) throw new ArgumentNullException(nameof(param));`',
                    'estimated_time': '1-2 hours',
                    'files_to_check': ['Service implementations']
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
        md_content.append("# ExxerAI Test Failure Analysis - Batch 2")
        md_content.append("")
        md_content.append(f"**Total Failures**: {analysis['summary']['total_failures']}")
        md_content.append(f"**Categories Found**: {analysis['summary']['categories_found']}")
        md_content.append("")
        
        # Priority distribution
        md_content.append("## Priority Distribution")
        for priority, count in sorted(analysis['summary']['priority_distribution'].items()):
            priority_label = {1: "Quick Wins", 2: "Medium Effort", 3: "Complex", 4: "Investigation Required"}
            md_content.append(f"- **Priority {priority}** ({priority_label.get(priority, 'Unknown')}): {count} failures")
        md_content.append("")
        
        # Fix recommendations
        md_content.append("## Fix Recommendations by Priority")
        
        recommendations = analysis['fix_recommendations']
        priority_groups = defaultdict(list)
        
        for pattern, rec in recommendations.items():
            priority_groups[rec['priority']].append((pattern, rec))
        
        for priority in sorted(priority_groups.keys()):
            priority_label = {1: "Quick Wins", 2: "Medium Effort", 3: "Complex", 4: "Investigation Required"}
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
        
        for pattern, failures in sorted_categories[:10]:  # Top 10
            md_content.append(f"### {pattern.replace('_', ' ').title()} ({len(failures)} failures)")
            md_content.append("Example failures:")
            for failure in failures[:3]:  # Show first 3 examples
                md_content.append(f"- `{failure['test_name']}`")
            if len(failures) > 3:
                md_content.append(f"- ... and {len(failures) - 3} more")
            md_content.append("")
        
        # Action plan
        md_content.append("## Recommended Action Plan")
        md_content.append("")
        md_content.append("1. **Start with Priority 1** (Quick Wins) - Should fix ~30-35 failures")
        md_content.append("2. **Move to Priority 2** (Medium Effort) - Should fix ~40-50 failures")
        md_content.append("3. **Tackle Priority 3** (Complex) - Requires deeper investigation")
        md_content.append("4. **Individual investigation** for remaining failures")
        md_content.append("")
        md_content.append("**Expected Outcome**: Reduce failures from 107 to <20 (>98% test success rate)")
        
        with open(self.summary_path, 'w', encoding='utf-8') as f:
            f.write('\n'.join(md_content))

def main():
    """Main execution function."""
    print("ExxerAI Test Failure Analysis - Batch 2")
    print("=" * 50)
    
    analyzer = ExxerAIFailureAnalyzer()
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
        print("Analysis failed - check log file location")
        return False

if __name__ == "__main__":
    main()