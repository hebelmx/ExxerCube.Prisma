#!/usr/bin/env python3
"""
Analyzes test projects to determine which dependencies they actually need.
Groups test projects by their reference patterns and generates appropriate GlobalUsings.
"""

import os
import re
import json
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Set, List, Dict, Tuple
from collections import defaultdict


class TestDependencyAnalyzer:
    """Analyzes test project dependencies and categorizes them."""
    
    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.tests_path = self.base_path / "code" / "src" / "tests"
        
        # Define namespace categories and their typical project references
        self.namespace_categories = {
            "Domain": {
                "projects": ["ExxerAI.Domain"],
                "namespaces": [
                    "ExxerAI.Domain",
                    "System",
                    "System.Collections.Generic",
                    "System.Linq",
                    "System.Threading",
                    "System.Threading.Tasks",
                    "System.ComponentModel",
                    "System.ComponentModel.DataAnnotations"
                ]
            },
            "Application": {
                "projects": ["ExxerAI.Application", "ExxerAI.Domain"],
                "namespaces": [
                    "ExxerAI.Application",
                    "Microsoft.Extensions.DependencyInjection",
                    "Microsoft.Extensions.Logging",
                    "Microsoft.Extensions.Options",
                    "Microsoft.Extensions.Configuration"
                ]
            },
            "Infrastructure": {
                "projects": ["ExxerAI.Infrastructure", "ExxerAI.Axis", "ExxerAI.Datastream"],
                "namespaces": [
                    "ExxerAI.Axis",
                    "ExxerAI.Datastream",
                    "Microsoft.EntityFrameworkCore",
                    "Microsoft.AspNetCore.Http",
                    "Microsoft.Extensions.Caching.Memory"
                ]
            },
            "Api": {
                "projects": ["ExxerAI.Api"],
                "namespaces": [
                    "ExxerAI.Api.Models",
                    "Microsoft.AspNetCore.Mvc",
                    "Microsoft.AspNetCore.Http"
                ]
            },
            "Integration": {
                "projects": ["Multiple"],
                "namespaces": [
                    "Microsoft.AspNetCore.TestHost",
                    "Microsoft.Extensions.DependencyInjection",
                    "Microsoft.EntityFrameworkCore"
                ]
            }
        }
        
        # Test frameworks always included
        self.test_framework_namespaces = [
            "Xunit",
            "NSubstitute", 
            "Shouldly",
            "IndQuestResults",
            "IndQuestResults.Operations",
            "IndQuestResults.Validation"
        ]
        
    def analyze_project_references(self, csproj_path: Path) -> Dict[str, any]:
        """Analyze a test project's references."""
        result = {
            "path": str(csproj_path),
            "name": csproj_path.stem,
            "references": [],
            "package_references": [],
            "category": "Unknown"
        }
        
        try:
            tree = ET.parse(csproj_path)
            root = tree.getroot()
            
            # Get project references
            for ref in root.findall(".//ProjectReference"):
                include = ref.get('Include')
                if include:
                    # Extract project name from path
                    project_name = Path(include).stem
                    result["references"].append(project_name)
            
            # Get package references
            for ref in root.findall(".//PackageReference"):
                include = ref.get('Include')
                if include:
                    result["package_references"].append(include)
                    
            # Determine category based on references
            result["category"] = self._categorize_project(result["references"])
            
        except Exception as e:
            print(f"Error parsing {csproj_path}: {e}")
            
        return result
    
    def _categorize_project(self, references: List[str]) -> str:
        """Categorize project based on its references."""
        if any("Infrastructure" in ref or "Axis" in ref or "Datastream" in ref for ref in references):
            return "Infrastructure"
        elif any("Api" in ref for ref in references):
            return "Api"
        elif any("Application" in ref for ref in references):
            return "Application"
        elif any("Domain" in ref for ref in references):
            return "Domain"
        elif len(references) > 3:
            return "Integration"
        else:
            return "Domain"  # Default to most restrictive
    
    def analyze_all_test_projects(self) -> Dict[str, List[Dict]]:
        """Analyze all test projects and group by category."""
        projects_by_category = defaultdict(list)
        
        for csproj_file in self.tests_path.rglob("*.csproj"):
            if any(skip in str(csproj_file) for skip in ['bin/', 'obj/']):
                continue
            
            project_name = csproj_file.stem
            if project_name.endswith('.Test') or project_name.endswith('.Tests'):
                analysis = self.analyze_project_references(csproj_file)
                projects_by_category[analysis["category"]].append(analysis)
        
        return dict(projects_by_category)
    
    def generate_category_report(self) -> str:
        """Generate a report of test projects by category."""
        categories = self.analyze_all_test_projects()
        
        report = ["# Test Project Dependency Analysis\n"]
        report.append(f"Total test projects analyzed: {sum(len(v) for v in categories.values())}\n")
        
        for category, projects in categories.items():
            report.append(f"\n## {category} Test Projects ({len(projects)})\n")
            
            for project in sorted(projects, key=lambda x: x['name']):
                report.append(f"### {project['name']}")
                report.append(f"- Path: {project['path']}")
                report.append(f"- References: {', '.join(project['references']) if project['references'] else 'None'}")
                if project['package_references']:
                    report.append(f"- Packages: {', '.join(project['package_references'][:5])}...")
                report.append("")
        
        return '\n'.join(report)
    
    def generate_category_globalusings(self, category: str) -> List[str]:
        """Generate appropriate using statements for a category."""
        usings = set()
        
        # Always include test framework namespaces
        usings.update(self.test_framework_namespaces)
        
        # Add common system namespaces
        usings.update([
            "System",
            "System.Collections.Generic", 
            "System.Linq",
            "System.Threading",
            "System.Threading.Tasks",
            "System.Text",
            "System.IO"
        ])
        
        # Add category-specific namespaces
        if category in self.namespace_categories:
            usings.update(self.namespace_categories[category]["namespaces"])
            
            # Add related categories
            if category == "Application":
                usings.update(self.namespace_categories["Domain"]["namespaces"])
            elif category == "Infrastructure":
                usings.update(self.namespace_categories["Domain"]["namespaces"])
                usings.update(self.namespace_categories["Application"]["namespaces"])
            elif category == "Api":
                usings.update(self.namespace_categories["Domain"]["namespaces"])
                usings.update(self.namespace_categories["Application"]["namespaces"])
            elif category == "Integration":
                # Integration tests might need everything
                for cat_data in self.namespace_categories.values():
                    usings.update(cat_data["namespaces"])
        
        return sorted(list(usings))
    
    def save_analysis(self, output_file: str):
        """Save the analysis results to JSON."""
        categories = self.analyze_all_test_projects()
        
        # Add namespace recommendations
        for category, projects in categories.items():
            for project in projects:
                project["recommended_namespaces"] = self.generate_category_globalusings(category)
        
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(categories, f, indent=2)
        
        print(f"Analysis saved to: {output_file}")


def main():
    import argparse
    
    parser = argparse.ArgumentParser(description='Analyze test project dependencies')
    parser.add_argument('--base-path', default='F:/Dynamic/ExxerAi/ExxerAI',
                       help='Base path of the ExxerAI project')
    parser.add_argument('--output', default='test_dependency_analysis.json',
                       help='Output JSON file')
    parser.add_argument('--report', action='store_true',
                       help='Generate markdown report')
    
    args = parser.parse_args()
    
    analyzer = TestDependencyAnalyzer(args.base_path)
    
    if args.report:
        report = analyzer.generate_category_report()
        report_file = "test_dependency_report.md"
        with open(report_file, 'w', encoding='utf-8') as f:
            f.write(report)
        print(f"Report saved to: {report_file}")
    
    analyzer.save_analysis(args.output)
    
    # Show summary
    categories = analyzer.analyze_all_test_projects()
    print("\nSummary by category:")
    for category, projects in categories.items():
        print(f"  {category}: {len(projects)} projects")


if __name__ == "__main__":
    main()