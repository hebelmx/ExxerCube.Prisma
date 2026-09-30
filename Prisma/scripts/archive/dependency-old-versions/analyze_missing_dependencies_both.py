#!/usr/bin/env python3
"""
Analyzes CS0246 and CS0103 errors to find missing dependencies and their sources.
CS0246: The type or namespace name 'X' could not be found
CS0103: The name 'X' does not exist in the current context
Identifies whether types are defined in projects or NuGet packages.
Also analyzes GlobalUsing.cs files for unused statements.
"""

import os
import re
import json
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Set, List, Dict, Tuple, Optional
from collections import defaultdict
from datetime import datetime


class MissingDependencyAnalyzer:
    """Analyzes CS0246/CS0103 errors and finds where missing types are defined."""
    
    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.src_path = self.base_path / "code" / "src"
        self.tests_path = self.base_path / "code" / "src" / "tests"
        
        # Cache for performance
        self.type_definitions_cache = {}
        self.namespace_cache = {}
        self.package_references_cache = {}
        
        # Common type-to-namespace mappings for Microsoft types
        self.known_type_namespaces = {
            'IServiceCollection': 'Microsoft.Extensions.DependencyInjection',
            'IConfiguration': 'Microsoft.Extensions.Configuration',
            'ILogger': 'Microsoft.Extensions.Logging',
            'IOptions': 'Microsoft.Extensions.Options',
            'HttpClient': 'System.Net.Http',
            'JsonSerializer': 'System.Text.Json',
            'CancellationToken': 'System.Threading',
            'Task': 'System.Threading.Tasks',
            'ServiceProvider': 'Microsoft.Extensions.DependencyInjection',
            'ConfigurationBuilder': 'Microsoft.Extensions.Configuration',
            'IHostBuilder': 'Microsoft.Extensions.Hosting',
            'HostBuilder': 'Microsoft.Extensions.Hosting',
            'IHost': 'Microsoft.Extensions.Hosting',
            'DbContext': 'Microsoft.EntityFrameworkCore',
            'DbSet': 'Microsoft.EntityFrameworkCore',
            'HttpContext': 'Microsoft.AspNetCore.Http',
            'IActionResult': 'Microsoft.AspNetCore.Mvc',
            'Controller': 'Microsoft.AspNetCore.Mvc',
            'ControllerBase': 'Microsoft.AspNetCore.Mvc',
            'IMemoryCache': 'Microsoft.Extensions.Caching.Memory',
            'MemoryCache': 'Microsoft.Extensions.Caching.Memory',
            # Additional common types for CS0103
            'Result': 'IndQuestResults',
            'TestContext': 'Meziantou.Extensions.Logging.Xunit.v3',
            'IServiceProvider': 'Microsoft.Extensions.DependencyInjection',
            'IServiceScope': 'Microsoft.Extensions.DependencyInjection',
            'ServiceLifetime': 'Microsoft.Extensions.DependencyInjection',
            # Common test types
            'Fact': 'Xunit',
            'Theory': 'Xunit',
            'InlineData': 'Xunit',
            'ClassData': 'Xunit',
            'MemberData': 'Xunit',
            # NSubstitute
            'Substitute': 'NSubstitute',
            'Arg': 'NSubstitute',
            # Shouldly
            'ShouldBe': 'Shouldly',
            'ShouldNotBeNull': 'Shouldly',
            'ShouldBeNull': 'Shouldly',
            'ShouldBeTrue': 'Shouldly',
            'ShouldBeFalse': 'Shouldly'
        }
        
        # Known package mappings
        self.namespace_to_package = {
            'Microsoft.Extensions.DependencyInjection': 'Microsoft.Extensions.DependencyInjection',
            'Microsoft.Extensions.Configuration': 'Microsoft.Extensions.Configuration',
            'Microsoft.Extensions.Logging': 'Microsoft.Extensions.Logging',
            'Microsoft.Extensions.Options': 'Microsoft.Extensions.Options',
            'Microsoft.Extensions.Hosting': 'Microsoft.Extensions.Hosting',
            'Microsoft.Extensions.Caching.Memory': 'Microsoft.Extensions.Caching.Memory',
            'Microsoft.Extensions.Http': 'Microsoft.Extensions.Http',
            'Microsoft.EntityFrameworkCore': 'Microsoft.EntityFrameworkCore',
            'Microsoft.AspNetCore.Mvc': 'Microsoft.AspNetCore.Mvc.Core',
            'Microsoft.AspNetCore.Http': 'Microsoft.AspNetCore.Http',
            'System.Text.Json': 'System.Text.Json',
            'Azure.AI.OpenAI': 'Azure.AI.OpenAI',
            'OpenTelemetry': 'OpenTelemetry',
            'Polly': 'Polly',
            'Serilog': 'Serilog',
            'MudBlazor': 'MudBlazor',
            'IndQuestResults': 'IndQuestResults',
            'Xunit': 'xunit.v3',
            'NSubstitute': 'NSubstitute',
            'Shouldly': 'Shouldly',
            'Meziantou.Extensions.Logging.Xunit.v3': 'Meziantou.Extensions.Logging.Xunit.v3'
        }
    
    def parse_error_file(self, error_file: str) -> List[Dict]:
        """Parse CS0246 and CS0103 errors from the error file."""
        errors = []
        
        with open(error_file, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        # Skip header line
        for line in lines[1:]:
            if line.strip():
                # Parse tab-separated values
                parts = line.strip().split('\t')
                if len(parts) >= 6 and ('CS0246' in parts[1] or 'CS0103' in parts[1]):
                    # Extract type/identifier name from error message
                    match = re.search(r"'([^']+)'", parts[2])
                    if match:
                        missing_type = match.group(1)
                        errors.append({
                            'type': missing_type,
                            'error_code': 'CS0246' if 'CS0246' in parts[1] else 'CS0103',
                            'project': parts[3],
                            'file': parts[4],
                            'line': parts[5],
                            'full_error': parts[2]
                        })
        
        cs0246_count = sum(1 for e in errors if e['error_code'] == 'CS0246')
        cs0103_count = sum(1 for e in errors if e['error_code'] == 'CS0103')
        print(f"Parsed {len(errors)} errors: {cs0246_count} CS0246, {cs0103_count} CS0103")
        return errors
    
    def find_type_in_projects(self, type_name: str) -> Optional[Dict]:
        """Search for a type definition across all projects."""
        # Check cache first
        if type_name in self.type_definitions_cache:
            return self.type_definitions_cache[type_name]
        
        # Search patterns for different type definitions
        patterns = [
            rf'\bclass\s+{re.escape(type_name)}\b',
            rf'\binterface\s+{re.escape(type_name)}\b',
            rf'\benum\s+{re.escape(type_name)}\b',
            rf'\bstruct\s+{re.escape(type_name)}\b',
            rf'\brecord\s+{re.escape(type_name)}\b',
            rf'\bdelegate\s+.*\s+{re.escape(type_name)}\s*\(',
            # For CS0103, also check for static classes/methods
            rf'\bstatic\s+class\s+{re.escape(type_name)}\b',
            rf'\bstatic\s+\w+\s+{re.escape(type_name)}\s*\(',
            # Extension methods
            rf'\bstatic\s+\w+\s+{re.escape(type_name)}\s*\(\s*this\s+'
        ]
        
        combined_pattern = '|'.join(patterns)
        
        # Search in all .cs files
        for cs_file in self.src_path.rglob("*.cs"):
            # Skip test files and generated files
            if any(skip in str(cs_file) for skip in ['bin/', 'obj/', '.g.cs', '.Designer.cs']):
                continue
                
            try:
                content = cs_file.read_text(encoding='utf-8')
                
                if re.search(combined_pattern, content):
                    # Found the type, extract namespace
                    namespace_match = re.search(r'namespace\s+([\w.]+)', content)
                    if namespace_match:
                        namespace = namespace_match.group(1)
                        
                        # Find which project this file belongs to
                        project_path = self._find_project_for_file(cs_file)
                        if project_path:
                            result = {
                                'type': type_name,
                                'namespace': namespace,
                                'project': project_path.stem,
                                'project_path': str(project_path),
                                'file': str(cs_file),
                                'source': 'project'
                            }
                            self.type_definitions_cache[type_name] = result
                            return result
                            
            except Exception as e:
                pass
        
        # Not found in projects
        self.type_definitions_cache[type_name] = None
        return None
    
    def _find_project_for_file(self, file_path: Path) -> Optional[Path]:
        """Find the .csproj file for a given source file."""
        current = file_path.parent
        
        while current != self.base_path and current.parent != current:
            csproj_files = list(current.glob("*.csproj"))
            if csproj_files:
                return csproj_files[0]
            current = current.parent
            
        return None
    
    def identify_nuget_package(self, type_name: str, using_project: str) -> Optional[Dict]:
        """Identify the NuGet package providing a type."""
        # First check known mappings
        if type_name in self.known_type_namespaces:
            namespace = self.known_type_namespaces[type_name]
            package = self.namespace_to_package.get(namespace)
            
            if package:
                return {
                    'type': type_name,
                    'namespace': namespace,
                    'package': package,
                    'source': 'nuget'
                }
        
        # Check if any project using this type has relevant package references
        # This helps identify packages for types we don't have in our known mappings
        projects_using_type = self._find_projects_using_type(type_name)
        
        for project_path in projects_using_type:
            packages = self._get_package_references(project_path)
            # Try to match based on namespace patterns
            for package in packages:
                if self._could_package_provide_type(package, type_name):
                    return {
                        'type': type_name,
                        'namespace': self._guess_namespace_from_package(package, type_name),
                        'package': package,
                        'source': 'nuget'
                    }
        
        return None
    
    def _find_projects_using_type(self, type_name: str) -> List[Path]:
        """Find all projects that reference a type."""
        projects = []
        pattern = rf'\b{re.escape(type_name)}\b'
        
        for cs_file in self.src_path.rglob("*.cs"):
            if any(skip in str(cs_file) for skip in ['bin/', 'obj/', '.g.cs']):
                continue
                
            try:
                content = cs_file.read_text(encoding='utf-8')
                if re.search(pattern, content):
                    project = self._find_project_for_file(cs_file)
                    if project and project not in projects:
                        projects.append(project)
            except:
                pass
                
        return projects
    
    def _get_package_references(self, project_path: Path) -> List[str]:
        """Get all NuGet package references from a project."""
        if str(project_path) in self.package_references_cache:
            return self.package_references_cache[str(project_path)]
            
        packages = []
        try:
            tree = ET.parse(project_path)
            root = tree.getroot()
            
            for package_ref in root.findall(".//PackageReference"):
                include = package_ref.get('Include')
                if include:
                    packages.append(include)
                    
        except Exception as e:
            pass
            
        self.package_references_cache[str(project_path)] = packages
        return packages
    
    def _could_package_provide_type(self, package: str, type_name: str) -> bool:
        """Heuristic to determine if a package could provide a type."""
        # Simple heuristic based on naming patterns
        package_lower = package.lower()
        type_lower = type_name.lower()
        
        # Direct package name matches
        if any(part in package_lower for part in ['extensions', 'aspnetcore', 'entityframework']):
            return True
            
        # Type-specific patterns
        if 'configuration' in type_lower and 'configuration' in package_lower:
            return True
        if 'service' in type_lower and 'dependencyinjection' in package_lower:
            return True
        if 'http' in type_lower and 'http' in package_lower:
            return True
            
        return False
    
    def _guess_namespace_from_package(self, package: str, type_name: str) -> str:
        """Guess the namespace based on package name."""
        # Common patterns
        if package.startswith("Microsoft.Extensions."):
            return package
        if package.startswith("Microsoft.AspNetCore."):
            return package
        if package == "System.Text.Json":
            return "System.Text.Json"
            
        # Default to package name
        return package
    
    def analyze_global_usings(self) -> Dict[str, List[str]]:
        """Analyze GlobalUsings.cs files to find unused statements."""
        unused_usings = {}
        
        for globalusing_file in self.tests_path.rglob("GlobalUsings.cs"):
            project_dir = globalusing_file.parent
            project_name = project_dir.name
            
            # Get all using statements from GlobalUsings.cs
            global_usings = self._extract_usings_from_file(globalusing_file)
            
            # Check if each using is actually used in the project
            unused = []
            for using in global_usings:
                if not self._is_using_used_in_project(using, project_dir, globalusing_file):
                    unused.append(using)
            
            if unused:
                unused_usings[project_name] = unused
                
        return unused_usings
    
    def _extract_usings_from_file(self, file_path: Path) -> List[str]:
        """Extract using statements from a file."""
        usings = []
        try:
            content = file_path.read_text(encoding='utf-8')
            # Match both regular and global using statements
            pattern = r'(?:global\s+)?using\s+([\w.]+)\s*;'
            matches = re.findall(pattern, content)
            usings = list(set(matches))
        except Exception as e:
            pass
            
        return usings
    
    def _is_using_used_in_project(self, using_namespace: str, project_dir: Path, exclude_file: Path) -> bool:
        """Check if a using statement is used anywhere in the project."""
        # Create patterns to search for
        namespace_parts = using_namespace.split('.')
        
        # Search patterns for different usage scenarios
        patterns = [
            # Direct type usage (last part of namespace might be a type)
            rf'\b{re.escape(namespace_parts[-1])}\b',
            # Fully qualified usage
            rf'\b{re.escape(using_namespace)}\.[\w]+\b',
        ]
        
        # Special handling for certain namespaces
        if using_namespace in ['System.Threading', 'System.Threading.Tasks']:
            patterns.extend([r'\bTask\b', r'\bCancellationToken\b', r'\basync\b', r'\bawait\b'])
        elif using_namespace == 'System.Linq':
            patterns.extend([r'\.Where\(', r'\.Select\(', r'\.FirstOrDefault\(', r'\.Any\('])
        elif using_namespace == 'System.Collections.Generic':
            patterns.extend([r'\bList<', r'\bDictionary<', r'\bIEnumerable<', r'\bHashSet<'])
        
        combined_pattern = '|'.join(patterns)
        
        # Search in all .cs files in the project
        for cs_file in project_dir.rglob("*.cs"):
            if cs_file == exclude_file or any(skip in str(cs_file) for skip in ['bin/', 'obj/', '.g.cs']):
                continue
                
            try:
                content = cs_file.read_text(encoding='utf-8')
                if re.search(combined_pattern, content):
                    return True
            except:
                pass
                
        return False
    
    def generate_analysis_report(self, errors: List[Dict], output_file: str):
        """Generate a comprehensive analysis report."""
        report = {
            'analysis_date': datetime.now().isoformat(),
            'total_errors': len(errors),
            'error_breakdown': {
                'CS0246': sum(1 for e in errors if e.get('error_code') == 'CS0246'),
                'CS0103': sum(1 for e in errors if e.get('error_code') == 'CS0103')
            },
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
        for error in errors:
            type_name = error['type']
            project = error['project']
            
            # Skip if already analyzed
            if type_name not in unique_types:
                # Try to find in projects first
                project_def = self.find_type_in_projects(type_name)
                if project_def:
                    unique_types[type_name] = project_def
                else:
                    # Try to identify NuGet package
                    package_def = self.identify_nuget_package(type_name, project)
                    if package_def:
                        unique_types[type_name] = package_def
                    else:
                        unique_types[type_name] = {
                            'type': type_name,
                            'source': 'unknown',
                            'suggestion': 'Type not found in projects or known packages'
                        }
            
            # Add to report
            error_info = {
                **error,
                'resolution': unique_types[type_name]
            }
            report['errors_by_project'][project].append(error_info)
            
            # Update summary
            resolution = unique_types[type_name]
            if resolution['source'] == 'project':
                report['summary']['project_references_needed'][project].add(resolution['project'])
                report['summary']['namespaces_to_add'][project].add(resolution['namespace'])
            elif resolution['source'] == 'nuget':
                report['summary']['nuget_packages_needed'][project].add(resolution['package'])
                report['summary']['namespaces_to_add'][project].add(resolution['namespace'])
        
        # Add missing dependencies analysis
        report['missing_dependencies'] = unique_types
        
        # Analyze unused global usings
        print("\nAnalyzing GlobalUsings.cs files for unused statements...")
        report['unused_global_usings'] = self.analyze_global_usings()
        
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
        print(f"Total errors analyzed: {report['total_errors']}")
        print(f"  CS0246 (type/namespace not found): {report['error_breakdown']['CS0246']}")
        print(f"  CS0103 (name not in context): {report['error_breakdown']['CS0103']}")
        
        # Count by source
        sources = defaultdict(int)
        for dep in report['missing_dependencies'].values():
            sources[dep['source']] += 1
            
        print(f"\nMissing types by source:")
        for source, count in sources.items():
            print(f"  {source}: {count}")
        
        # Projects needing updates
        print(f"\nProjects needing updates: {len(report['errors_by_project'])}")
        
        # Top missing types
        type_counts = defaultdict(int)
        for errors_list in report['errors_by_project'].values():
            for error in errors_list:
                type_counts[error['type']] += 1
        
        print("\nTop 10 missing types:")
        for type_name, count in sorted(type_counts.items(), key=lambda x: -x[1])[:10]:
            resolution = report['missing_dependencies'].get(type_name, {})
            source = resolution.get('source', 'unknown')
            print(f"  {type_name}: {count} occurrences ({source})")
        
        # Unused global usings
        if report['unused_global_usings']:
            print(f"\nProjects with unused global usings: {len(report['unused_global_usings'])}")
            total_unused = sum(len(usings) for usings in report['unused_global_usings'].values())
            print(f"Total unused global using statements: {total_unused}")


def main():
    import argparse
    
    parser = argparse.ArgumentParser(description='Analyze CS0246 and CS0103 errors and find missing dependencies')
    parser.add_argument('--base-path', default='F:/Dynamic/ExxerAi/ExxerAI',
                       help='Base path of the ExxerAI project')
    parser.add_argument('--error-file', default='F:/Dynamic/ExxerAi/ExxerAI/Errors/CS0246.txt',
                       help='Path to CS0246/CS0103 error file')
    parser.add_argument('--output', default='missing_dependencies_analysis.json',
                       help='Output JSON file for analysis')
    
    args = parser.parse_args()
    
    analyzer = MissingDependencyAnalyzer(args.base_path)
    
    # Parse errors
    print("Parsing CS0246 and CS0103 errors...")
    errors = analyzer.parse_error_file(args.error_file)
    
    # Generate analysis report
    print("\nAnalyzing missing dependencies...")
    analyzer.generate_analysis_report(errors, args.output)


if __name__ == "__main__":
    main()