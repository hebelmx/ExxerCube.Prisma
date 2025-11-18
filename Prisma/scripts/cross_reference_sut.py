#!/usr/bin/env python3
"""
Cross-Reference Analysis for Production Code and Test Code
Uses in-memory hash tables for efficient comparison without large file loading.

Analyzes:
- Which production types are tested
- Which tests don't have corresponding production code
- Test coverage gaps
- Architectural alignment between code and tests

Author: Claude Code Agent
Date: 2025-11-08
"""

import json
from pathlib import Path
from typing import Dict, Set, List, Tuple
from collections import defaultdict
from datetime import datetime


class SUTCrossReferenceAnalyzer:
    """Analyzes relationships between production code and test code using in-memory hash tables."""

    def __init__(self, production_json: str, test_json: str):
        # Load data
        with open(production_json, 'r', encoding='utf-8') as f:
            self.production_data = json.load(f)

        with open(test_json, 'r', encoding='utf-8') as f:
            self.test_data = json.load(f)

        # Create in-memory hash tables for fast lookup
        self.prod_type_to_project = {}  # type_name -> project
        self.prod_type_to_location = {}  # type_name -> file_path
        self.test_sut_to_tests = defaultdict(set)  # sut_type -> test_files
        self.test_project_suts = defaultdict(set)  # test_project -> sut_types

        self._build_hash_tables()

    def _build_hash_tables(self):
        """Build in-memory hash tables from loaded data."""
        # Build production type lookup
        for full_name, info in self.production_data.get('all_types', {}).items():
            type_name = info['name']
            # Prefer non-test projects
            if 'Test' not in info['project']:
                if type_name not in self.prod_type_to_project or 'Test' in self.prod_type_to_project[type_name]:
                    self.prod_type_to_project[type_name] = info['project']
                    self.prod_type_to_location[type_name] = info['file']

        # Build test SUT lookup
        sut_mapping = self.test_data.get('sut_to_tests_mapping', {})
        for sut, test_files in sut_mapping.items():
            self.test_sut_to_tests[sut] = set(test_files)

        # Build test project SUTs lookup
        for project, analysis in self.test_data.get('project_analysis', {}).items():
            if 'sut_types' in analysis:
                self.test_project_suts[project] = set(analysis['sut_types'])

    def analyze_test_coverage(self) -> Dict:
        """Analyze which production types are tested and which are not."""
        tested_types = set()
        untested_types = set()
        test_only_types = set()

        # Find tested types (SUTs that exist in production)
        for sut in self.test_sut_to_tests.keys():
            if sut in self.prod_type_to_project:
                tested_types.add(sut)
            else:
                test_only_types.add(sut)

        # Find untested types (production types without tests)
        for type_name in self.prod_type_to_project.keys():
            if type_name not in tested_types:
                untested_types.add(type_name)

        return {
            'tested_types': sorted(list(tested_types)),
            'untested_types': sorted(list(untested_types))[:100],  # Limit for readability
            'test_only_types': sorted(list(test_only_types)),  # Tests for types that don't exist
            'statistics': {
                'total_production_types': len(self.prod_type_to_project),
                'tested_count': len(tested_types),
                'untested_count': len(untested_types),
                'test_only_count': len(test_only_types),
                'coverage_percentage': round((len(tested_types) / len(self.prod_type_to_project)) * 100, 2) if self.prod_type_to_project else 0
            }
        }

    def analyze_project_alignment(self, test_project: str) -> Dict:
        """Analyze architectural alignment for a specific test project."""
        if test_project not in self.test_project_suts:
            return {'error': f'Test project {test_project} not found'}

        suts = self.test_project_suts[test_project]
        prod_projects = defaultdict(set)  # production_project -> suts

        # Map SUTs to their production projects
        for sut in suts:
            if sut in self.prod_type_to_project:
                prod_project = self.prod_type_to_project[sut]
                prod_projects[prod_project].add(sut)

        # Determine if test project aligns with single production project
        primary_project = None
        if len(prod_projects) == 1:
            primary_project = list(prod_projects.keys())[0]
            alignment = 'perfect'
        elif len(prod_projects) > 1:
            # Find dominant project
            primary_project = max(prod_projects.items(), key=lambda x: len(x[1]))[0]
            alignment = 'mixed'
        else:
            alignment = 'orphaned'

        return {
            'test_project': test_project,
            'alignment': alignment,
            'primary_production_project': primary_project,
            'production_projects_referenced': {
                proj: sorted(list(suts_in_proj))
                for proj, suts_in_proj in prod_projects.items()
            },
            'total_suts': len(suts),
            'recommendation': self._generate_recommendation(alignment, test_project, prod_projects)
        }

    def _generate_recommendation(self, alignment: str, test_project: str, prod_projects: Dict) -> str:
        """Generate recommendation based on alignment analysis."""
        if alignment == 'perfect':
            return f"✅ Perfect alignment - {test_project} tests a single production project"

        elif alignment == 'mixed':
            primary = max(prod_projects.items(), key=lambda x: len(x[1]))[0]
            others = [p for p in prod_projects.keys() if p != primary]
            return f"⚠️ Mixed concerns - Primary: {primary}, but also tests: {', '.join(others)}. Consider splitting."

        else:
            return f"❌ Orphaned tests - No production code found. May need cleanup or missing production code."

    def analyze_split_candidates(self, test_project: str) -> Dict:
        """Analyze if a test project should be split and suggest split strategy."""
        alignment = self.analyze_project_alignment(test_project)

        if alignment['alignment'] != 'mixed':
            return {
                'should_split': False,
                'reason': f"Project has {alignment['alignment']} alignment - no split needed"
            }

        # Analyze split candidates
        prod_projects = alignment['production_projects_referenced']

        if len(prod_projects) <= 1:
            return {
                'should_split': False,
                'reason': "Only one production project referenced"
            }

        # Calculate distribution of SUTs across production projects
        total_suts = sum(len(suts) for suts in prod_projects.values())
        distribution = {
            proj: {
                'sut_count': len(suts),
                'percentage': round((len(suts) / total_suts) * 100, 2),
                'suts': suts
            }
            for proj, suts in prod_projects.items()
        }

        # Determine if split is warranted (no single project dominates > 80%)
        max_percentage = max(d['percentage'] for d in distribution.values())

        if max_percentage > 80:
            return {
                'should_split': False,
                'reason': f"One project dominates ({max_percentage}%) - split not warranted",
                'distribution': distribution
            }

        # Generate split recommendations
        split_suggestions = []
        for proj, data in distribution.items():
            if data['percentage'] >= 15:  # Only suggest split if >= 15% of tests
                new_project_name = f"{proj}.Adapter.Tests"
                split_suggestions.append({
                    'new_project_name': new_project_name,
                    'suts_to_move': data['suts'],
                    'sut_count': data['sut_count'],
                    'justification': f"Tests {data['sut_count']} SUTs from {proj} ({data['percentage']}% of total)"
                })

        return {
            'should_split': len(split_suggestions) > 1,
            'reason': f"Multiple production projects tested with balanced distribution",
            'distribution': distribution,
            'split_suggestions': split_suggestions
        }

    def generate_report(self, focus_projects: List[str] = None) -> Dict:
        """Generate comprehensive cross-reference report."""
        report = {
            'analysis_date': datetime.now().isoformat(),
            'overall_coverage': self.analyze_test_coverage(),
            'project_alignments': {}
        }

        # Analyze specific projects or all
        projects_to_analyze = focus_projects if focus_projects else list(self.test_project_suts.keys())

        for project in projects_to_analyze:
            alignment = self.analyze_project_alignment(project)
            split_analysis = self.analyze_split_candidates(project)

            report['project_alignments'][project] = {
                'alignment': alignment,
                'split_analysis': split_analysis
            }

        return report

    def save_report(self, output_file: str, focus_projects: List[str] = None):
        """Save cross-reference report to JSON file."""
        report = self.generate_report(focus_projects)

        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2)

        print(f"\nCross-reference report saved to: {output_file}")

        # Print summary
        coverage = report['overall_coverage']['statistics']
        print(f"\nOverall Coverage:")
        print(f"  Total Production Types: {coverage['total_production_types']}")
        print(f"  Tested: {coverage['tested_count']} ({coverage['coverage_percentage']}%)")
        print(f"  Untested: {coverage['untested_count']}")
        print(f"  Test-Only: {coverage['test_only_count']}")

        print(f"\nProject Alignment Summary:")
        for project, data in report['project_alignments'].items():
            alignment = data['alignment']['alignment']
            split = data['split_analysis']['should_split']
            emoji = '✅' if alignment == 'perfect' else '⚠️' if alignment == 'mixed' else '❌'
            print(f"  {emoji} {project}: {alignment}")
            if split:
                print(f"     └─ ⚡ Split recommended: {len(data['split_analysis']['split_suggestions'])} new projects")


def main():
    import argparse

    parser = argparse.ArgumentParser(description='Cross-reference production and test code')
    parser.add_argument('--production', required=True,
                       help='Production code JSON file (from scan_exxerai_types.py)')
    parser.add_argument('--tests', required=True,
                       help='Test code JSON file (from scan_test_sut.py)')
    parser.add_argument('--output', default='cross_reference_report.json',
                       help='Output report file')
    parser.add_argument('--focus', nargs='+',
                       help='Specific test projects to analyze (default: all)')

    args = parser.parse_args()

    analyzer = SUTCrossReferenceAnalyzer(args.production, args.tests)
    analyzer.save_report(args.output, args.focus)


if __name__ == "__main__":
    main()
