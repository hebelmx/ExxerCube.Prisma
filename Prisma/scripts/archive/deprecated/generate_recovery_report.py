#!/usr/bin/env python3
"""
Test Recovery Recommendations Report Generator
Creates a comprehensive markdown report with prioritized recovery recommendations
"""

import json
from pathlib import Path
from datetime import datetime
from typing import Dict, List
from collections import defaultdict

class TestRecoveryReportGenerator:
    """Generates comprehensive test recovery recommendations."""
    
    def __init__(self, analysis_file: str):
        self.analysis_file = analysis_file
        with open(analysis_file, 'r') as f:
            self.analysis = json.load(f)
    
    def generate_markdown_report(self, output_file: str):
        """Generate comprehensive markdown recovery report."""
        
        summary = self.analysis['summary']
        metadata = self.analysis['metadata']
        
        report_lines = [
            "# ExxerAI Test Recovery Analysis Report",
            f"*Generated on: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}*",
            "",
            "## Executive Summary",
            "",
            f"This report analyzes **{summary['total_backup_methods']} test methods** from orphaned backup projects against **{summary['total_current_methods']} current test methods** to identify:",
            "",
            "- 📊 **Test Coverage Gaps**: Methods that exist only in backup projects",
            "- 🎯 **Recovery Priorities**: High-value tests worth recovering",  
            "- 🔄 **Duplicates**: Tests that already exist in current codebase",
            "- 🌊 **Code Drift**: Similar tests that may have evolved",
            "",
            "### Key Findings",
            "",
            f"| Metric | Count | Percentage |",
            f"|--------|--------|------------|",
            f"| **Total Backup Methods** | {summary['total_backup_methods']} | 100% |",
            f"| **Exact Matches** | {summary['exact_matches']} | {100*summary['exact_matches']//summary['total_backup_methods']}% |",
            f"| **High Similarity** | {summary['high_similarity_matches']} | {100*summary['high_similarity_matches']//summary['total_backup_methods'] if summary['total_backup_methods'] > 0 else 0}% |",
            f"| **Code Drift Candidates** | {summary['code_drift_candidates']} | {100*summary['code_drift_candidates']//summary['total_backup_methods'] if summary['total_backup_methods'] > 0 else 0}% |",
            f"| **Unique Recovery Candidates** | {summary['unique_recovery_candidates']} | {100*summary['unique_recovery_candidates']//summary['total_backup_methods'] if summary['total_backup_methods'] > 0 else 0}% |",
            f"| **High Priority Recoveries** | {summary['high_priority_recoveries']} | {100*summary['high_priority_recoveries']//summary['total_backup_methods'] if summary['total_backup_methods'] > 0 else 0}% |",
            "",
            "---",
            ""
        ]
        
        # High Priority Recovery Candidates
        self._add_high_priority_section(report_lines)
        
        # Integration and System Tests
        self._add_integration_tests_section(report_lines)
        
        # BDD Tests Recovery
        self._add_bdd_tests_section(report_lines)
        
        # Code Drift Analysis
        self._add_code_drift_section(report_lines)
        
        # Project-wise Analysis
        self._add_project_analysis_section(report_lines)
        
        # Implementation Recommendations
        self._add_implementation_recommendations(report_lines)
        
        # Technical Details
        self._add_technical_details(report_lines)
        
        # Write the report
        with open(output_file, 'w', encoding='utf-8') as f:
            f.write('\n'.join(report_lines))
            
        print(f"Comprehensive recovery report generated: {output_file}")
        
    def _add_high_priority_section(self, lines: List[str]):
        """Add high priority recovery candidates section."""
        
        high_priority = [m for m in self.analysis['unique_backup_methods'] if m['is_high_priority']]
        
        lines.extend([
            "## 🎯 High Priority Recovery Candidates",
            "",
            f"**{len(high_priority)} critical tests** identified for immediate recovery consideration:",
            ""
        ])
        
        # Group by category
        categories = defaultdict(list)
        for candidate in high_priority:
            method_name = candidate['backup_method'].lower()
            if 'integration' in method_name or 'e2e' in method_name:
                categories['Integration Tests'].append(candidate)
            elif 'system' in method_name:
                categories['System Tests'].append(candidate)
            elif 'performance' in method_name:
                categories['Performance Tests'].append(candidate)
            else:
                categories['Business Logic Tests'].append(candidate)
        
        for category, candidates in categories.items():
            if candidates:
                lines.extend([
                    f"### {category} ({len(candidates)} tests)",
                    ""
                ])
                
                for candidate in candidates[:10]:  # Limit to top 10 per category
                    method_name = candidate['backup_method']
                    score = candidate['recovery_priority_score']
                    recommendation = candidate['recommendation']
                    
                    lines.extend([
                        f"**`{method_name}`**",
                        f"- Priority Score: {score:.2f}",
                        f"- {recommendation}",
                        f"- Projects: {', '.join(candidate['backup_projects'])}",
                        ""
                    ])
        
        lines.append("---\n")
        
    def _add_integration_tests_section(self, lines: List[str]):
        """Add integration tests analysis."""
        
        integration_tests = [
            m for m in self.analysis['unique_backup_methods']
            if any('integration' in inst['fullPath'].lower() or 'integration' in m['backup_method'].lower()
                   for inst in m['backup_instances'])
        ]
        
        if integration_tests:
            lines.extend([
                "## 🔗 Integration Tests Recovery",
                "",
                f"**{len(integration_tests)} integration tests** found in backup projects that may fill coverage gaps:",
                ""
            ])
            
            for test in integration_tests[:15]:  # Show top 15
                method_name = test['backup_method']
                score = test['recovery_priority_score']
                
                lines.extend([
                    f"- **`{method_name}`** (Priority: {score:.2f})",
                    f"  - {test['recommendation']}",
                    ""
                ])
                
            lines.append("---\n")
    
    def _add_bdd_tests_section(self, lines: List[str]):
        """Add BDD tests analysis."""
        
        bdd_tests = []
        for method in self.analysis['unique_backup_methods']:
            for instance in method['backup_instances']:
                if instance.get('testPattern') in ['BDD_Should', 'BDD_When', 'BDD_Given']:
                    bdd_tests.append(method)
                    break
        
        if bdd_tests:
            lines.extend([
                "## 🎭 BDD Tests Recovery",
                "",
                f"**{len(bdd_tests)} BDD-style tests** using Should/When/Given patterns:",
                ""
            ])
            
            # Group by pattern
            by_pattern = defaultdict(list)
            for test in bdd_tests:
                for instance in test['backup_instances']:
                    pattern = instance.get('testPattern', 'Other')
                    if pattern in ['BDD_Should', 'BDD_When', 'BDD_Given']:
                        by_pattern[pattern].append(test)
                        break
            
            for pattern, tests in by_pattern.items():
                lines.extend([
                    f"### {pattern} Tests ({len(tests)})",
                    ""
                ])
                
                for test in tests[:10]:  # Top 10 per pattern
                    method_name = test['backup_method']
                    score = test['recovery_priority_score']
                    lines.append(f"- `{method_name}` (Priority: {score:.2f})")
                
                lines.append("")
            
            lines.append("---\n")
    
    def _add_code_drift_section(self, lines: List[str]):
        """Add code drift analysis."""
        
        drift_candidates = self.analysis['code_drift_candidates']
        
        if drift_candidates:
            lines.extend([
                "## 🌊 Code Drift Analysis",
                "",
                f"**{len(drift_candidates)} methods** show code drift patterns (similar but modified):",
                ""
            ])
            
            for drift in drift_candidates[:10]:  # Show top 10
                backup_method = drift['backup_method']
                similar_methods = drift['similar_methods']
                
                lines.extend([
                    f"**`{backup_method}`**",
                    "- Similar to:"
                ])
                
                for similar in similar_methods[:3]:  # Top 3 similar
                    similarity = similar['similarity_score']
                    current_method = similar['method_name']
                    lines.append(f"  - `{current_method}` (similarity: {similarity:.2f})")
                
                lines.extend([
                    f"- {drift['recommendation']}",
                    ""
                ])
            
            lines.append("---\n")
    
    def _add_project_analysis_section(self, lines: List[str]):
        """Add project-wise analysis."""
        
        # Analyze by backup projects
        project_stats = defaultdict(lambda: {'unique': 0, 'high_priority': 0, 'duplicates': 0})
        
        for method in self.analysis['unique_backup_methods']:
            for project in method['backup_projects']:
                project_stats[project]['unique'] += 1
                if method['is_high_priority']:
                    project_stats[project]['high_priority'] += 1
        
        for match in self.analysis['exact_matches']:
            for project in match['backup_projects']:
                project_stats[project]['duplicates'] += 1
        
        lines.extend([
            "## 📊 Project-wise Recovery Analysis",
            "",
            "| Project | Unique Tests | High Priority | Duplicates | Recovery Value |",
            "|---------|-------------|---------------|------------|----------------|"
        ])
        
        for project, stats in sorted(project_stats.items(), key=lambda x: x[1]['high_priority'], reverse=True):
            total = stats['unique'] + stats['duplicates']
            recovery_value = "High" if stats['high_priority'] > 5 else "Medium" if stats['unique'] > 10 else "Low"
            
            lines.append(f"| {project} | {stats['unique']} | {stats['high_priority']} | {stats['duplicates']} | {recovery_value} |")
        
        lines.extend(["", "---", ""])
    
    def _add_implementation_recommendations(self, lines: List[str]):
        """Add implementation recommendations."""
        
        lines.extend([
            "## 🚀 Implementation Recommendations",
            "",
            "### Immediate Actions (This Sprint)",
            "",
            "1. **Recovery High Priority Integration Tests**",
            "   - Focus on tests with 'integration', 'e2e', or 'system' keywords",
            "   - These provide the highest coverage value",
            "",
            "2. **Review Code Drift Candidates**", 
            "   - Compare backup vs current implementations",
            "   - Merge valuable test scenarios that may have been lost",
            "",
            "### Short-term Actions (Next 2-3 Sprints)",
            "",
            "3. **BDD Test Recovery**",
            "   - Restore Should/When/Given pattern tests",
            "   - Improve behavioral test coverage",
            "",
            "4. **Project-specific Recovery**",
            "   - Prioritize projects with high unique test counts",
            "   - Focus on business-critical domains",
            "",
            "### Quality Assurance",
            "",
            "5. **Test Validation Process**",
            "   - Ensure recovered tests compile and pass",
            "   - Update dependencies and framework references",
            "   - Verify test isolation and determinism",
            "",
            "6. **Coverage Analysis**",
            "   - Run coverage reports before and after recovery",
            "   - Measure actual coverage improvement",
            "",
            "---",
            ""
        ])
    
    def _add_technical_details(self, lines: List[str]):
        """Add technical analysis details."""
        
        metadata = self.analysis['metadata']
        
        lines.extend([
            "## 🔧 Technical Analysis Details",
            "",
            f"**Analysis Metadata:**",
            f"- Generated: {metadata['generated_on']}",
            f"- Analyzer Version: {metadata['analyzer_version']}",
            f"- Backup Methods File: {metadata['backup_methods_file']}",
            "",
            "**Similarity Thresholds:**",
            f"- Exact Match: {metadata['similarity_thresholds']['exact_match']}",
            f"- High Similarity: {metadata['similarity_thresholds']['high_similarity']}",
            f"- Code Drift: {metadata['similarity_thresholds']['code_drift']}",
            f"- Different: {metadata['similarity_thresholds']['different']}",
            "",
            "**Algorithm Used:**",
            "- Primary: Hash-based exact matching for performance",
            "- Secondary: N-gram similarity for fuzzy matching",
            "- Fallback: Levenshtein distance for code drift detection",
            "",
            "**Performance Optimizations:**",
            "- Batch processing with progress tracking",
            "- Length-based candidate filtering",
            "- Early termination for exact matches",
            "- Hash-based lookup tables",
            "",
            "---",
            "",
            "*This report provides data-driven recommendations for test recovery.*",
            "*Review and validate recommendations before implementing.*"
        ])

def main():
    import sys
    
    # Use the most recent analysis file
    analysis_file = "method_duplicate_analysis_fast_20251031_104145.json"
    if len(sys.argv) > 1:
        analysis_file = sys.argv[1]
    
    generator = TestRecoveryReportGenerator(analysis_file)
    
    # Generate output filename
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    output_file = f"test_recovery_recommendations_{timestamp}.md"
    
    generator.generate_markdown_report(output_file)
    print(f"\n✅ Comprehensive recovery report generated: {output_file}")

if __name__ == "__main__":
    main()