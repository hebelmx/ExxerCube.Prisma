#!/usr/bin/env python3
"""
Analyze missing dependencies using the ExxerAI type dictionary.
Automatically determines project references based on actual type locations.
"""

import os
import re
import json
from pathlib import Path
from collections import defaultdict
from datetime import datetime


class SmartDependencyAnalyzer:
    """Analyzes dependencies using scanned type information."""
    
    def __init__(self, base_path: str, types_file: str):
        self.base_path = Path(base_path)
        self.errors = []
        self.error_projects = {}
        
        # Load type dictionary
        with open(types_file, 'r', encoding='utf-8') as f:
            type_data = json.load(f)
            self.type_lookup = type_data['type_lookup']
            self.all_types = type_data['all_types']
        
        print(f"Loaded {len(self.type_lookup)} types from dictionary")
        
        # Common CLR types that don't need project references
        self.clr_types = {
            'StringBuilder': 'System.Text',
            'Regex': 'System.Text.RegularExpressions',
            'ConcurrentDictionary<,>': 'System.Collections.Concurrent',
            'ConcurrentQueue<>': 'System.Collections.Concurrent',
            'ConcurrentBag<>': 'System.Collections.Concurrent',
            'Dictionary<,>': 'System.Collections.Generic',
            'List<>': 'System.Collections.Generic',
            'HashSet<>': 'System.Collections.Generic',
            'IEnumerable<>': 'System.Collections.Generic',
            'Task': 'System.Threading.Tasks',
            'Task<>': 'System.Threading.Tasks',
            'CancellationToken': 'System.Threading',
            'DateTime': 'System',
            'TimeSpan': 'System',
            'Guid': 'System',
            'Exception': 'System',
            'ArgumentException': 'System',
            'InvalidOperationException': 'System',
            'NotImplementedException': 'System'
        }
        
        # External package types
        self.package_types = {
            # Test frameworks
            'Fact': {'namespace': 'Xunit', 'package': 'xunit.v3'},
            'Theory': {'namespace': 'Xunit', 'package': 'xunit.v3'},
            'InlineData': {'namespace': 'Xunit', 'package': 'xunit.v3'},
            'Substitute': {'namespace': 'NSubstitute', 'package': 'NSubstitute'},
            'Result': {'namespace': 'IndQuestResults', 'package': 'IndQuestResults'},
            'Result<>': {'namespace': 'IndQuestResults', 'package': 'IndQuestResults'},
            
            # Microsoft packages
            'IServiceCollection': {'namespace': 'Microsoft.Extensions.DependencyInjection', 'package': 'Microsoft.Extensions.DependencyInjection'},
            'IConfiguration': {'namespace': 'Microsoft.Extensions.Configuration', 'package': 'Microsoft.Extensions.Configuration'},
            'ILogger': {'namespace': 'Microsoft.Extensions.Logging', 'package': 'Microsoft.Extensions.Logging'},
            'WebApplicationFactory<>': {'namespace': 'Microsoft.AspNetCore.Mvc.Testing', 'package': 'Microsoft.AspNetCore.Mvc.Testing'},
            'IPlaywright': {'namespace': 'Microsoft.Playwright', 'package': 'Microsoft.Playwright'},
            'IBrowser': {'namespace': 'Microsoft.Playwright', 'package': 'Microsoft.Playwright'},
            'IPage': {'namespace': 'Microsoft.Playwright', 'package': 'Microsoft.Playwright'},
            
            # Document libraries
            'PdfSharp': {'namespace': 'PdfSharp', 'package': 'PDFsharp'},
            'SixLabors': {'namespace': 'SixLabors.ImageSharp', 'package': 'SixLabors.ImageSharp'},
            'DocumentFormat': {'namespace': 'DocumentFormat.OpenXml', 'package': 'DocumentFormat.OpenXml'}
        }
    
    def parse_error_file(self, error_file: str):
        """Parse the tab-separated error file."""
        with open(error_file, 'r', encoding='utf-8') as f:
            for line_num, line in enumerate(f, 1):
                if line_num == 1:  # Skip header
                    continue
                    
                parts = line.strip().split('\t')
                if len(parts) >= 5:
                    # Extract type name from description
                    description = parts[2]
                    match = re.search(r"'([^']+)'", description)
                    if match:
                        type_name = match.group(1)
                        project = parts[3]  # Project is in column 4
                        
                        self.errors.append({
                            'type': type_name,
                            'project': project,
                            'line': line_num
                        })
    
    def _determine_project_from_path(self, file_path: str) -> str:
        """Determine project from full file path."""
        # Normalize path
        path = file_path.replace('\\', '/')
        
        # Extract project name from path
        # Looking for patterns like /src/ExxerAI.ProjectName/ or /tests/ExxerAI.ProjectName.Test/
        match = re.search(r'/(?:src|tests)/([^/]+)/(?:obj/)?', path)
        if match:
            project_name = match.group(1)
            # Clean up obj folder references
            if project_name == 'obj':
                # Look one level up
                match = re.search(r'/(?:src|tests)/([^/]+)/obj/', path)
                if match:
                    project_name = match.group(1)
            return project_name
        
        # If we can't determine, return Unknown
        return 'Unknown'
    
    def _guess_test_project(self, file_name: str) -> str:
        """Try to guess test project from file name."""
        # Common patterns in test file names
        patterns = [
            (r'DocumentProcessing', 'ExxerAI.Application.DocumentServices.Test'),
            (r'LLM|Llm', 'ExxerAI.Application.LLMServices.Test'),
            (r'Pattern', 'ExxerAI.Application.Patterns.Test'),
            (r'CubeX|Promotion|Template', 'ExxerAI.Application.CubeXplorer.Test'),
            (r'Workflow', 'ExxerAI.Application.WorkflowServices.Test'),
            (r'Agent|Swarm', 'ExxerAI.Infrastructure.Test'),
            (r'Cortex|AI|Inference', 'ExxerAI.Domain.Cortex.Test'),
            (r'Infrastructure|Docker|Image', 'ExxerAI.Infrastructure.Test')
        ]
        
        for pattern, project in patterns:
            if re.search(pattern, file_name, re.IGNORECASE):
                return project
        
        return 'ExxerAI.Test.Unknown'
    
    def analyze_dependencies(self):
        """Analyze dependencies and determine what's needed."""
        results = {
            'total_errors': len(self.errors),
            'errors_by_type': defaultdict(int),
            'project_references_needed': defaultdict(set),
            'package_references_needed': defaultdict(set),
            'namespaces_needed': defaultdict(set),
            'unknown_types': defaultdict(set),
            'found_in_exxerai': defaultdict(dict)
        }
        
        # Group errors by project
        errors_by_project = defaultdict(list)
        for error in self.errors:
            errors_by_project[error['project']].append(error['type'])
            results['errors_by_type'][error['type']] += 1
        
        # Analyze each project's missing types
        for project, missing_types in errors_by_project.items():
            for type_name in set(missing_types):  # Unique types only
                
                # Check CLR types first
                if type_name in self.clr_types:
                    results['namespaces_needed'][project].add(self.clr_types[type_name])
                    continue
                
                # Check external packages
                if type_name in self.package_types:
                    pkg_info = self.package_types[type_name]
                    results['package_references_needed'][project].add(pkg_info['package'])
                    results['namespaces_needed'][project].add(pkg_info['namespace'])
                    continue
                
                # Check ExxerAI types
                if type_name in self.type_lookup:
                    type_info = self.type_lookup[type_name]
                    source_project = type_info['project']
                    
                    # Don't add self-references
                    if source_project != project and project != 'Unknown':
                        results['project_references_needed'][project].add(source_project)
                        results['namespaces_needed'][project].add(type_info['namespace'])
                        results['found_in_exxerai'][type_name] = type_info
                    continue
                
                # Unknown type
                results['unknown_types'][project].add(type_name)
        
        return results
    
    def generate_report(self, results: dict, output_file: str):
        """Generate the analysis report for fix_dependencies_smart.py."""
        # Convert sets to lists for JSON
        actions_needed = {
            'project_references': {},
            'package_references': {},
            'investigate': {}
        }
        
        for project, refs in results['project_references_needed'].items():
            if refs and project != 'Unknown':
                actions_needed['project_references'][project] = list(refs)
        
        for project, pkgs in results['package_references_needed'].items():
            if pkgs and project != 'Unknown':
                actions_needed['package_references'][project] = list(pkgs)
        
        for project, types in results['unknown_types'].items():
            if types and project != 'Unknown':
                actions_needed['investigate'][project] = list(types)
        
        # Count errors by project
        errors_by_project = defaultdict(int)
        for error in self.errors:
            errors_by_project[error['project']] += 1
        
        report = {
            'analysis_date': datetime.now().isoformat(),
            'total_errors': results['total_errors'],
            'unique_types': len(results['errors_by_type']),
            'errors_by_project': dict(errors_by_project),
            'actions_needed': actions_needed,
            'directory_build_props_namespaces': [],
            'found_in_exxerai': results['found_in_exxerai'],
            'top_missing_types': dict(sorted(results['errors_by_type'].items(), 
                                           key=lambda x: -x[1])[:30])
        }
        
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2)
        
        return report
    
    def print_summary(self, results: dict):
        """Print analysis summary."""
        print(f"\n=== DEPENDENCY ANALYSIS SUMMARY ===")
        print(f"Total errors: {results['total_errors']}")
        print(f"Unique missing types: {len(results['errors_by_type'])}")
        
        # Types found in ExxerAI
        found_count = len(results['found_in_exxerai'])
        unknown_count = sum(len(types) for types in results['unknown_types'].values())
        package_count = sum(len(pkgs) for pkgs in results['package_references_needed'].values())
        
        print(f"\nType resolution:")
        print(f"  Found in ExxerAI projects: {found_count}")
        print(f"  External packages needed: {package_count}")
        print(f"  Unknown types: {unknown_count}")
        
        # Project references needed
        proj_refs = results['project_references_needed']
        if proj_refs:
            print(f"\nProject references needed:")
            for project, refs in sorted(proj_refs.items()):
                if refs:
                    print(f"  {project}:")
                    for ref in sorted(refs):
                        print(f"    -> {ref}")
        
        # Top unknown types
        if results['unknown_types']:
            print(f"\nTop unknown types:")
            all_unknown = []
            for project, types in results['unknown_types'].items():
                for t in types:
                    all_unknown.append((t, results['errors_by_type'].get(t, 0)))
            
            for type_name, count in sorted(all_unknown, key=lambda x: -x[1])[:10]:
                print(f"  {type_name}: {count} occurrences")


def main():
    import argparse
    
    parser = argparse.ArgumentParser(description='Analyze dependencies using type dictionary')
    parser.add_argument('--base-path', default='F:/Dynamic/ExxerAi/ExxerAI')
    parser.add_argument('--error-file', default='F:/Dynamic/ExxerAi/ExxerAI/Errors/CS0246.txt')
    parser.add_argument('--types-file', default='exxerai_types.json')
    parser.add_argument('--output', default='smart_dependency_analysis_final.json')
    
    args = parser.parse_args()
    
    analyzer = SmartDependencyAnalyzer(args.base_path, args.types_file)
    
    print("Parsing error file...")
    analyzer.parse_error_file(args.error_file)
    
    print("Analyzing dependencies...")
    results = analyzer.analyze_dependencies()
    
    print("Generating report...")
    report = analyzer.generate_report(results, args.output)
    
    analyzer.print_summary(results)
    
    print(f"\nReport saved to: {args.output}")


if __name__ == "__main__":
    main()