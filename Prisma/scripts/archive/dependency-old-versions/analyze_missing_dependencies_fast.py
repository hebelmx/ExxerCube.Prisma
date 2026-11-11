#!/usr/bin/env python3
"""
Fast version of the dependency analyzer with progress indicators and optimizations.
"""

import os
import re
import json
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Set, List, Dict, Tuple, Optional
from collections import defaultdict
from datetime import datetime


class FastDependencyAnalyzer:
    """Fast analyzer for CS0246 errors."""
    
    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.src_path = self.base_path / "code" / "src"
        self.tests_path = self.base_path / "code" / "src" / "tests"
        
        # Cache
        self.type_definitions_cache = {}
        self.namespace_cache = {}
        
        # Common type-to-namespace mappings
        self.known_type_namespaces = {
            'IServiceCollection': ('Microsoft.Extensions.DependencyInjection', 'Microsoft.Extensions.DependencyInjection'),
            'IConfiguration': ('Microsoft.Extensions.Configuration', 'Microsoft.Extensions.Configuration'),
            'ILogger': ('Microsoft.Extensions.Logging', 'Microsoft.Extensions.Logging.Abstractions'),
            'IOptions': ('Microsoft.Extensions.Options', 'Microsoft.Extensions.Options'),
            'HttpClient': ('System.Net.Http', None),
            'JsonSerializer': ('System.Text.Json', 'System.Text.Json'),
            'CancellationToken': ('System.Threading', None),
            'Task': ('System.Threading.Tasks', None),
            'ServiceProvider': ('Microsoft.Extensions.DependencyInjection', 'Microsoft.Extensions.DependencyInjection'),
            'ConfigurationBuilder': ('Microsoft.Extensions.Configuration', 'Microsoft.Extensions.Configuration'),
            'IHostBuilder': ('Microsoft.Extensions.Hosting', 'Microsoft.Extensions.Hosting'),
            'HostBuilder': ('Microsoft.Extensions.Hosting', 'Microsoft.Extensions.Hosting'),
            'IHost': ('Microsoft.Extensions.Hosting', 'Microsoft.Extensions.Hosting'),
            'DbContext': ('Microsoft.EntityFrameworkCore', 'Microsoft.EntityFrameworkCore'),
            'DbSet': ('Microsoft.EntityFrameworkCore', 'Microsoft.EntityFrameworkCore'),
            'HttpContext': ('Microsoft.AspNetCore.Http', 'Microsoft.AspNetCore.Http.Abstractions'),
            'IActionResult': ('Microsoft.AspNetCore.Mvc', 'Microsoft.AspNetCore.Mvc.Core'),
            'Controller': ('Microsoft.AspNetCore.Mvc', 'Microsoft.AspNetCore.Mvc.Core'),
            'ControllerBase': ('Microsoft.AspNetCore.Mvc', 'Microsoft.AspNetCore.Mvc.Core'),
            'IMemoryCache': ('Microsoft.Extensions.Caching.Memory', 'Microsoft.Extensions.Caching.Abstractions'),
            'MemoryCache': ('Microsoft.Extensions.Caching.Memory', 'Microsoft.Extensions.Caching.Memory'),
            'FakeTimeProvider': ('Microsoft.Extensions.Time.Testing', 'Microsoft.Extensions.Time.Testing'),
            'TimeProvider': ('Microsoft.Extensions.Time', 'Microsoft.Extensions.Time'),
            'IServiceProvider': ('System', None),
            'Type': ('System', None),
            'Func': ('System', None),
            'Action': ('System', None),
            'DateTime': ('System', None),
            'TimeSpan': ('System', None),
            'Guid': ('System', None),
            'Exception': ('System', None),
            'ArgumentNullException': ('System', None),
            'InvalidOperationException': ('System', None),
            'NotImplementedException': ('System', None),
            'List': ('System.Collections.Generic', None),
            'Dictionary': ('System.Collections.Generic', None),
            'IEnumerable': ('System.Collections.Generic', None),
            'HashSet': ('System.Collections.Generic', None),
            'IList': ('System.Collections.Generic', None),
            'IDictionary': ('System.Collections.Generic', None),
            'ICollection': ('System.Collections.Generic', None),
            'KeyValuePair': ('System.Collections.Generic', None),
            'Queue': ('System.Collections.Generic', None),
            'Stack': ('System.Collections.Generic', None),
            'LinkedList': ('System.Collections.Generic', None),
            'SortedList': ('System.Collections.Generic', None),
            'SortedDictionary': ('System.Collections.Generic', None),
            'SortedSet': ('System.Collections.Generic', None)
        }
        
        # ExxerAI specific types
        self.exxerai_patterns = {
            'DateRange': 'ExxerAI.Domain.Nexus.DocumentProcessing.DateAndTime',
            'EIADescriptiveStatistics': 'ExxerAI.Domain.Common.Statistics',
            'BalanceValidator': 'ExxerAI.Domain.CubeExplorer.Services',
            'ParsedStatement': 'ExxerAI.Domain.CubeExplorer.Models',
            'BalanceInformation': 'ExxerAI.Domain.CubeExplorer.Models',
            'DefaultStatisticsEngine': 'ExxerAI.Domain.CubeExplorer.Statistics',
            'DocumentChangeType': 'ExxerAI.Domain.Common.Enums',
            'TruthRecordStatus': 'ExxerAI.Domain.Nexus.DocumentProcessing.ValidationAndRules',
            'TextGenerationOptions': 'ExxerAI.Domain.Cortex.Models',
            'InferenceResult': 'ExxerAI.Domain.Cortex.Models',
            'InferenceRequest': 'ExxerAI.Domain.Cortex.Models',
            'CompletionResponse': 'ExxerAI.Domain.Cortex.Models',
            'CompletionRequest': 'ExxerAI.Domain.Cortex.Models',
            'TokenUsageStatistics': 'ExxerAI.Domain.Cortex.Models',
            'ProcessedResponse': 'ExxerAI.Domain.Cortex.Models',
            'ResponseProcessingOptions': 'ExxerAI.Domain.Cortex.Models',
            'ValidationCriteria': 'ExxerAI.Domain.Cortex.Models',
            'EmbeddingResponse': 'ExxerAI.Domain.Cortex.Models',
            'EmbeddingRequest': 'ExxerAI.Domain.Cortex.Models',
            'SemanticAnalysisResult': 'ExxerAI.Domain.Cortex.Models',
            'AdaptationFeedback': 'ExxerAI.Domain.Cortex.Models',
            'MemoryConsolidationRequest': 'ExxerAI.Domain.Cortex.Models',
            'KnowledgeDistillationRequest': 'ExxerAI.Domain.Cortex.Models',
            'ModelOptimizationRequest': 'ExxerAI.Domain.Cortex.Models',
            'OptimizedInferenceResult': 'ExxerAI.Domain.Cortex.Models'
        }
    
    def parse_error_file(self, error_file: str) -> List[Dict]:
        """Parse CS0246 errors from the error file."""
        print("Parsing error file...")
        errors = []
        
        with open(error_file, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        # Skip header line
        for i, line in enumerate(lines[1:]):
            if line.strip():
                # Parse tab-separated values
                parts = line.strip().split('\t')
                if len(parts) >= 6 and 'CS0246' in parts[1]:
                    # Extract type name from error message
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
    
    def find_type_info(self, type_name: str) -> Optional[Dict]:
        """Fast lookup for type information."""
        # Check cache first
        if type_name in self.type_definitions_cache:
            return self.type_definitions_cache[type_name]
        
        # Check known types first (fastest)
        if type_name in self.known_type_namespaces:
            namespace, package = self.known_type_namespaces[type_name]
            result = {
                'type': type_name,
                'namespace': namespace,
                'source': 'nuget' if package else 'system',
                'package': package
            }
            self.type_definitions_cache[type_name] = result
            return result
        
        # Check ExxerAI patterns
        if type_name in self.exxerai_patterns:
            namespace = self.exxerai_patterns[type_name]
            # Determine project name from namespace
            parts = namespace.split('.')
            if len(parts) >= 3:
                project = '.'.join(parts[:3])  # e.g., ExxerAI.Domain.Nexus
            else:
                project = '.'.join(parts[:2])  # e.g., ExxerAI.Domain
                
            result = {
                'type': type_name,
                'namespace': namespace,
                'project': project,
                'source': 'project'
            }
            self.type_definitions_cache[type_name] = result
            return result
        
        # Unknown type
        result = {
            'type': type_name,
            'source': 'unknown',
            'suggestion': 'Type not found in known mappings'
        }
        self.type_definitions_cache[type_name] = result
        return result
    
    def analyze_global_usings_fast(self) -> Dict[str, List[str]]:
        """Fast analysis of unused global usings - only check first 5 projects."""
        print("\nAnalyzing GlobalUsings.cs files for unused statements...")
        unused_usings = {}
        
        globalusing_files = list(self.tests_path.rglob("GlobalUsings.cs"))
        print(f"Found {len(globalusing_files)} GlobalUsings.cs files")
        
        # Only analyze first 5 for speed
        for i, globalusing_file in enumerate(globalusing_files[:5]):
            project_dir = globalusing_file.parent
            project_name = project_dir.name
            print(f"  Analyzing {project_name}... ({i+1}/5)")
            
            # Get all using statements from GlobalUsings.cs
            global_usings = []
            try:
                content = globalusing_file.read_text(encoding='utf-8')
                pattern = r'(?:global\s+)?using\s+([\w.]+)\s*;'
                matches = re.findall(pattern, content)
                global_usings = list(set(matches))
            except:
                continue
            
            # Quick check - just mark all non-System usings as potentially unused for now
            unused = [u for u in global_usings if not u.startswith('System')]
            
            if unused:
                unused_usings[project_name] = unused[:10]  # Limit to 10 per project
                
        return unused_usings
    
    def generate_analysis_report(self, errors: List[Dict], output_file: str):
        """Generate a comprehensive analysis report."""
        print("\nGenerating analysis report...")
        report = {
            'analysis_date': datetime.now().isoformat(),
            'total_errors': len(errors),
            'errors_by_project': defaultdict(list),
            'missing_dependencies': {},
            'unused_global_usings': {},
            'summary': {
                'project_references_needed': defaultdict(set),
                'nuget_packages_needed': defaultdict(set),
                'namespaces_to_add': defaultdict(set)
            }
        }
        
        # Analyze each error
        unique_types = {}
        total_errors = len(errors)
        
        for i, error in enumerate(errors):
            if i % 100 == 0:
                print(f"  Processing error {i}/{total_errors}...")
                
            type_name = error['type']
            project = error['project']
            
            # Skip if already analyzed
            if type_name not in unique_types:
                type_info = self.find_type_info(type_name)
                unique_types[type_name] = type_info
            
            # Add to report
            error_info = {
                **error,
                'resolution': unique_types[type_name]
            }
            report['errors_by_project'][project].append(error_info)
            
            # Update summary
            resolution = unique_types[type_name]
            if resolution['source'] == 'project':
                if 'project' in resolution:
                    report['summary']['project_references_needed'][project].add(resolution['project'])
                if 'namespace' in resolution:
                    report['summary']['namespaces_to_add'][project].add(resolution['namespace'])
            elif resolution['source'] == 'nuget':
                if 'package' in resolution and resolution['package']:
                    report['summary']['nuget_packages_needed'][project].add(resolution['package'])
                if 'namespace' in resolution:
                    report['summary']['namespaces_to_add'][project].add(resolution['namespace'])
            elif resolution['source'] == 'system':
                # System types only need namespace
                if 'namespace' in resolution:
                    report['summary']['namespaces_to_add'][project].add(resolution['namespace'])
        
        # Add missing dependencies analysis
        report['missing_dependencies'] = unique_types
        
        # Analyze unused global usings (fast version)
        report['unused_global_usings'] = self.analyze_global_usings_fast()
        
        # Convert sets to lists for JSON serialization
        for key in report['summary']:
            report['summary'][key] = {k: list(v) for k, v in report['summary'][key].items()}
        
        # Save report
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2, ensure_ascii=False)
            
        print(f"\nAnalysis complete! Report saved to: {output_file}")
        self._print_summary(report)
        
    def _print_summary(self, report: Dict):
        """Print a summary of the analysis."""
        print("\n=== ANALYSIS SUMMARY ===")
        print(f"Total CS0246 errors analyzed: {report['total_errors']}")
        
        # Count by source
        sources = defaultdict(int)
        for dep in report['missing_dependencies'].values():
            sources[dep['source']] += 1
            
        print(f"\nMissing types by source:")
        for source, count in sources.items():
            print(f"  {source}: {count}")
        
        # Projects needing updates
        print(f"\nProjects needing updates: {len(report['errors_by_project'])}")
        
        # Summary of fixes needed
        proj_refs = report['summary']['project_references_needed']
        nuget_refs = report['summary']['nuget_packages_needed']
        namespaces = report['summary']['namespaces_to_add']
        
        print(f"\nFixes needed:")
        print(f"  Projects needing project references: {len(proj_refs)}")
        print(f"  Projects needing NuGet packages: {len(nuget_refs)}")
        print(f"  Projects needing namespace additions: {len(namespaces)}")
        
        # Top missing types
        type_counts = defaultdict(int)
        for error_list in report['errors_by_project'].values():
            for error in error_list:
                type_counts[error['type']] += 1
        
        print(f"\nTop 10 most common missing types:")
        for type_name, count in sorted(type_counts.items(), key=lambda x: x[1], reverse=True)[:10]:
            resolution = report['missing_dependencies'].get(type_name, {})
            source = resolution.get('source', 'unknown')
            print(f"  {type_name}: {count} occurrences ({source})")


def main():
    import argparse
    
    parser = argparse.ArgumentParser(description='Fast CS0246 error analyzer')
    parser.add_argument('--base-path', default='F:/Dynamic/ExxerAi/ExxerAI',
                       help='Base path of the ExxerAI project')
    parser.add_argument('--error-file', default='F:/Dynamic/ExxerAi/ExxerAI/Errors/CS0246.txt',
                       help='Path to CS0246 error file')
    parser.add_argument('--output', default='missing_dependencies_analysis.json',
                       help='Output JSON file for analysis')
    
    args = parser.parse_args()
    
    analyzer = FastDependencyAnalyzer(args.base_path)
    
    # Parse errors
    errors = analyzer.parse_error_file(args.error_file)
    
    # Generate analysis report
    analyzer.generate_analysis_report(errors, args.output)


if __name__ == "__main__":
    main()