#!/usr/bin/env python3
"""
Smart dependency analyzer that respects Directory.Build.props injected usings.
Only suggests additions for things NOT already provided by Directory.Build.props.
"""

import os
import re
import json
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Set, List, Dict, Tuple, Optional
from collections import defaultdict
from datetime import datetime


class SmartDependencyAnalyzer:
    """Smart analyzer that understands Directory.Build.props."""
    
    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.src_path = self.base_path / "code" / "src"
        self.tests_path = self.base_path / "code" / "src" / "tests"
        
        # Load Directory.Build.props injected namespaces
        self.injected_namespaces = self._load_directory_build_props()
        print(f"Found {len(self.injected_namespaces)} namespaces already injected by Directory.Build.props")
        
        # Cache
        self.type_definitions_cache = {}
        
        # Known type mappings that require PROJECT references (not packages)
        self.exxerai_type_to_project = {
            # Domain types
            'DateRange': 'ExxerAI.Domain.Nexus',
            'EIADescriptiveStatistics': 'ExxerAI.Domain.Common',
            'BalanceValidator': 'ExxerAI.Domain.CubeExplorer',
            'ParsedStatement': 'ExxerAI.Domain.CubeExplorer',
            'BalanceInformation': 'ExxerAI.Domain.CubeExplorer',
            'DefaultStatisticsEngine': 'ExxerAI.Domain.CubeExplorer',
            'DocumentChangeType': 'ExxerAI.Domain.Common',
            'TruthRecordStatus': 'ExxerAI.Domain.Nexus',
            'TextGenerationOptions': 'ExxerAI.Domain.Cortex',
            'InferenceResult': 'ExxerAI.Domain.Cortex',
            'InferenceRequest': 'ExxerAI.Domain.Cortex',
            'CompletionResponse': 'ExxerAI.Domain.Cortex',
            'CompletionRequest': 'ExxerAI.Domain.Cortex',
            'TokenUsageStatistics': 'ExxerAI.Domain.Cortex',
            'ProcessedResponse': 'ExxerAI.Domain.Cortex',
            'ResponseProcessingOptions': 'ExxerAI.Domain.Cortex',
            'ValidationCriteria': 'ExxerAI.Domain.Cortex',
            'EmbeddingResponse': 'ExxerAI.Domain.Cortex',
            'EmbeddingRequest': 'ExxerAI.Domain.Cortex',
            'SemanticAnalysisResult': 'ExxerAI.Domain.Cortex',
            'AdaptationFeedback': 'ExxerAI.Domain.Cortex',
            'MemoryConsolidationRequest': 'ExxerAI.Domain.Cortex',
            'KnowledgeDistillationRequest': 'ExxerAI.Domain.Cortex',
            'ModelOptimizationRequest': 'ExxerAI.Domain.Cortex',
            'OptimizedInferenceResult': 'ExxerAI.Domain.Cortex',
            'Result': 'ExxerAI.Domain.Common',
            'DocumentMetadata': 'ExxerAI.Domain.Nexus',
            'OCRRegionPattern': 'ExxerAI.Domain.Nexus',
            'LanguageModel': 'ExxerAI.Domain.Cortex',
            'ExtractionContext': 'ExxerAI.Domain.Nexus',
            'DocumentType': 'ExxerAI.Domain.Common',
            'DocumentProcessingResult': 'ExxerAI.Domain.Nexus',
            'ExxerAIDbContext': 'ExxerAI.Datastream',
            'KnowledgeStoreContainerFixture': 'ExxerAI.Infrastructure.Test'
        }
        
        # Types that need package references (already have in Directory.Build.props)
        self.known_package_types = {
            'IServiceCollection': 'Microsoft.Extensions.DependencyInjection',
            'IConfiguration': 'Microsoft.Extensions.Configuration',
            'ILogger': None,  # Already in Directory.Build.props
            'IOptions': 'Microsoft.Extensions.Options',
            'IHostBuilder': 'Microsoft.Extensions.Hosting',
            'IHost': 'Microsoft.Extensions.Hosting',
            'FakeTimeProvider': None,  # Already have Microsoft.Extensions.TimeProvider.Testing
            'TimeProvider': None,  # Part of .NET
        }
    
    def _load_directory_build_props(self) -> Set[str]:
        """Load injected namespaces from Directory.Build.props."""
        injected = set()
        props_file = self.tests_path / "Directory.Build.props"
        
        if props_file.exists():
            try:
                tree = ET.parse(props_file)
                root = tree.getroot()
                
                # Find all Using elements
                for using in root.findall(".//Using"):
                    include = using.get('Include')
                    if include:
                        injected.add(include)
                        
            except Exception as e:
                print(f"Error parsing Directory.Build.props: {e}")
                
        return injected
    
    def parse_error_file(self, error_file: str) -> List[Dict]:
        """Parse CS0246 errors from the error file."""
        print("Parsing error file...")
        errors = []
        
        with open(error_file, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        for line in lines[1:]:  # Skip header
            if line.strip():
                parts = line.strip().split('\t')
                if len(parts) >= 6 and 'CS0246' in parts[1]:
                    match = re.search(r"'([^']+)'", parts[2])
                    if match:
                        missing_type = match.group(1)
                        errors.append({
                            'type': missing_type,
                            'project': parts[3],
                            'file': parts[4],
                            'line': parts[5],
                            'full_error': parts[2]
                        })
        
        print(f"Parsed {len(errors)} CS0246 errors")
        return errors
    
    def analyze_type(self, type_name: str) -> Dict:
        """Analyze a missing type and determine what's needed."""
        # Check cache
        if type_name in self.type_definitions_cache:
            return self.type_definitions_cache[type_name]
        
        result = {'type': type_name}
        
        # Check if it's an ExxerAI type needing project reference
        if type_name in self.exxerai_type_to_project:
            result.update({
                'source': 'project',
                'project': self.exxerai_type_to_project[type_name],
                'action': 'add_project_reference'
            })
        # Check if it needs a package reference
        elif type_name in self.known_package_types:
            package = self.known_package_types[type_name]
            if package:
                result.update({
                    'source': 'nuget',
                    'package': package,
                    'action': 'add_package_reference'
                })
            else:
                result.update({
                    'source': 'already_available',
                    'action': 'none',
                    'note': 'Already provided by Directory.Build.props'
                })
        # Special case for SixLabors
        elif type_name == 'SixLabors' or type_name.startswith('SixLabors.'):
            result.update({
                'source': 'nuget',
                'package': 'SixLabors.ImageSharp',
                'action': 'add_package_reference'
            })
        # Generic Result<T> pattern
        elif type_name.startswith('Result<') or type_name == 'Result':
            result.update({
                'source': 'project',
                'project': 'ExxerAI.Domain.Common',
                'action': 'add_project_reference'
            })
        else:
            result.update({
                'source': 'unknown',
                'action': 'investigate',
                'note': 'Type not found in known mappings'
            })
        
        self.type_definitions_cache[type_name] = result
        return result
    
    def generate_smart_report(self, errors: List[Dict], output_file: str):
        """Generate a smart analysis report."""
        print("\nGenerating smart analysis report...")
        
        report = {
            'analysis_date': datetime.now().isoformat(),
            'total_errors': len(errors),
            'directory_build_props_namespaces': list(self.injected_namespaces),
            'errors_by_project': defaultdict(list),
            'actions_needed': {
                'project_references': defaultdict(set),
                'package_references': defaultdict(set),
                'investigate': defaultdict(set)
            },
            'summary': {
                'types_needing_project_ref': 0,
                'types_needing_package_ref': 0,
                'types_already_available': 0,
                'types_unknown': 0
            }
        }
        
        # Analyze each unique type
        unique_types = {}
        for error in errors:
            type_name = error['type']
            project_name = error['project']
            
            if type_name not in unique_types:
                unique_types[type_name] = self.analyze_type(type_name)
            
            type_info = unique_types[type_name]
            
            # Track what action is needed
            if type_info['action'] == 'add_project_reference':
                report['actions_needed']['project_references'][project_name].add(type_info['project'])
                report['summary']['types_needing_project_ref'] += 1
            elif type_info['action'] == 'add_package_reference':
                report['actions_needed']['package_references'][project_name].add(type_info['package'])
                report['summary']['types_needing_package_ref'] += 1
            elif type_info['action'] == 'none':
                report['summary']['types_already_available'] += 1
            else:
                report['actions_needed']['investigate'][project_name].add(type_name)
                report['summary']['types_unknown'] += 1
            
            # Add to errors by project
            report['errors_by_project'][project_name].append({
                **error,
                'resolution': type_info
            })
        
        # Convert sets to lists for JSON
        for action_type in report['actions_needed']:
            report['actions_needed'][action_type] = {
                k: list(v) for k, v in report['actions_needed'][action_type].items()
            }
        
        # Save report
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2, ensure_ascii=False)
        
        print(f"\nAnalysis complete! Report saved to: {output_file}")
        self._print_summary(report, unique_types)
        
    def _print_summary(self, report: Dict, unique_types: Dict):
        """Print a summary of the analysis."""
        print("\n=== SMART ANALYSIS SUMMARY ===")
        print(f"Total CS0246 errors: {report['total_errors']}")
        print(f"Unique types: {len(unique_types)}")
        
        print(f"\nResolution summary:")
        print(f"  Need project references: {report['summary']['types_needing_project_ref']}")
        print(f"  Need package references: {report['summary']['types_needing_package_ref']}")
        print(f"  Already available: {report['summary']['types_already_available']}")
        print(f"  Unknown (need investigation): {report['summary']['types_unknown']}")
        
        # Show projects needing updates
        proj_refs = report['actions_needed']['project_references']
        pkg_refs = report['actions_needed']['package_references']
        
        if proj_refs:
            print(f"\nProjects needing PROJECT references:")
            for project, refs in list(proj_refs.items())[:5]:
                print(f"  {project}: {', '.join(refs)}")
            if len(proj_refs) > 5:
                print(f"  ... and {len(proj_refs) - 5} more projects")
        
        if pkg_refs:
            print(f"\nProjects needing PACKAGE references:")
            for project, refs in list(pkg_refs.items())[:5]:
                print(f"  {project}: {', '.join(refs)}")
            if len(pkg_refs) > 5:
                print(f"  ... and {len(pkg_refs) - 5} more projects")
        
        # Show top unknown types
        unknown_types = [(t, info) for t, info in unique_types.items() if info['action'] == 'investigate']
        if unknown_types:
            print(f"\nTop unknown types needing investigation:")
            for type_name, _ in unknown_types[:10]:
                print(f"  - {type_name}")


def main():
    import argparse
    
    parser = argparse.ArgumentParser(description='Smart CS0246 error analyzer')
    parser.add_argument('--base-path', default='F:/Dynamic/ExxerAi/ExxerAI',
                       help='Base path of the ExxerAI project')
    parser.add_argument('--error-file', default='F:/Dynamic/ExxerAi/ExxerAI/Errors/CS0246.txt',
                       help='Path to CS0246 error file')
    parser.add_argument('--output', default='smart_dependency_analysis.json',
                       help='Output JSON file for analysis')
    
    args = parser.parse_args()
    
    analyzer = SmartDependencyAnalyzer(args.base_path)
    errors = analyzer.parse_error_file(args.error_file)
    analyzer.generate_smart_report(errors, args.output)


if __name__ == "__main__":
    main()