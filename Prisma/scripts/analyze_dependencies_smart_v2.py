#!/usr/bin/env python3
"""
Enhanced Smart Dependency Analyzer v2
- Handles both CS0246 and CS0103 errors
- Uses JSON type dictionary for namespace lookup
- Checks existing GlobalUsings.cs files
- Works for both test and production projects
- Generates report for GlobalUsings.cs modifications
"""

import os
import re
import json
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Set, List, Dict, Tuple, Optional
from collections import defaultdict
from datetime import datetime


class EnhancedSmartDependencyAnalyzer:
    """Enhanced analyzer that handles GlobalUsings.cs modifications."""
    
    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.src_path = self.base_path / "code" / "src"
        self.tests_path = self.base_path / "code" / "src" / "tests"
        
        # Load type dictionary
        self.type_dictionary = self._load_type_dictionary()
        print(f"Loaded {len(self.type_dictionary.get('type_lookup', {}))} types from dictionary")
        
        # Load Directory.Build.props injected namespaces
        self.injected_namespaces = self._load_directory_build_props()
        print(f"Found {len(self.injected_namespaces)} namespaces already injected by Directory.Build.props")
        
        # Cache for existing GlobalUsings.cs content
        self.global_usings_cache = {}
        
        # Known CLR types and their namespaces
        self.clr_type_namespaces = {
            'Encoding': 'System.Text',
            'HttpClient': 'System.Net.Http',
            'JsonSerializer': 'System.Text.Json',
            'File': 'System.IO',
            'Path': 'System.IO',
            'Directory': 'System.IO',
            'Stream': 'System.IO',
            'MemoryStream': 'System.IO',
            'Task': 'System.Threading.Tasks',
            'CancellationToken': 'System.Threading',
            'List': 'System.Collections.Generic',
            'Dictionary': 'System.Collections.Generic',
            'IEnumerable': 'System.Collections.Generic',
            'Guid': 'System',
            'DateTime': 'System',
            'TimeSpan': 'System',
            'Math': 'System',
            'Convert': 'System',
            'String': 'System',
            'Int32': 'System',
            'Boolean': 'System',
            'Exception': 'System',
            'ArgumentException': 'System',
            'InvalidOperationException': 'System',
            'NotImplementedException': 'System',
            'Regex': 'System.Text.RegularExpressions',
            'StringBuilder': 'System.Text',
            'Console': 'System',
            'Environment': 'System',
            'Process': 'System.Diagnostics',
            'Stopwatch': 'System.Diagnostics',
            'Debug': 'System.Diagnostics',
            'Trace': 'System.Diagnostics',
            'Assembly': 'System.Reflection',
            'Type': 'System',
            'MethodInfo': 'System.Reflection',
            'PropertyInfo': 'System.Reflection',
            'Attribute': 'System',
            'IDisposable': 'System',
            'IComparable': 'System',
            'IEquatable': 'System',
            'IFormattable': 'System',
            'Action': 'System',
            'Func': 'System',
            'Predicate': 'System',
            'EventHandler': 'System',
            'Random': 'System',
            'Uri': 'System',
            'Version': 'System',
            'Array': 'System',
            'Tuple': 'System',
            'ValueTuple': 'System',
            'Nullable': 'System',
            'Lazy': 'System',
            'WeakReference': 'System',
            'GC': 'System',
            'IntPtr': 'System',
            'Marshal': 'System.Runtime.InteropServices',
            'Thread': 'System.Threading',
            'ThreadPool': 'System.Threading',
            'Monitor': 'System.Threading',
            'Mutex': 'System.Threading',
            'Semaphore': 'System.Threading',
            'ManualResetEvent': 'System.Threading',
            'AutoResetEvent': 'System.Threading',
            'Timer': 'System.Threading',
            'MailMessage': 'System.Net.Mail',
            'SmtpClient': 'System.Net.Mail',
            'NetworkCredential': 'System.Net',
            'WebClient': 'System.Net',
            'WebRequest': 'System.Net',
            'WebResponse': 'System.Net',
            'HttpWebRequest': 'System.Net',
            'HttpWebResponse': 'System.Net',
            'IPAddress': 'System.Net',
            'IPEndPoint': 'System.Net',
            'Socket': 'System.Net.Sockets',
            'TcpClient': 'System.Net.Sockets',
            'TcpListener': 'System.Net.Sockets',
            'UdpClient': 'System.Net.Sockets',
            'NetworkStream': 'System.Net.Sockets',
            'X509Certificate': 'System.Security.Cryptography.X509Certificates',
            'X509Certificate2': 'System.Security.Cryptography.X509Certificates',
            'RSACryptoServiceProvider': 'System.Security.Cryptography',
            'SHA256': 'System.Security.Cryptography',
            'MD5': 'System.Security.Cryptography',
            'Aes': 'System.Security.Cryptography',
            'SymmetricAlgorithm': 'System.Security.Cryptography',
            'HashAlgorithm': 'System.Security.Cryptography',
            'HMACSHA256': 'System.Security.Cryptography',
            'CryptoStream': 'System.Security.Cryptography',
            'XmlDocument': 'System.Xml',
            'XmlNode': 'System.Xml',
            'XmlElement': 'System.Xml',
            'XmlAttribute': 'System.Xml',
            'XmlReader': 'System.Xml',
            'XmlWriter': 'System.Xml',
            'XDocument': 'System.Xml.Linq',
            'XElement': 'System.Xml.Linq',
            'XAttribute': 'System.Xml.Linq',
            'DataTable': 'System.Data',
            'DataSet': 'System.Data',
            'DataRow': 'System.Data',
            'DataColumn': 'System.Data',
            'SqlConnection': 'System.Data.SqlClient',
            'SqlCommand': 'System.Data.SqlClient',
            'SqlDataReader': 'System.Data.SqlClient',
            'SqlParameter': 'System.Data.SqlClient',
        }
        
        # Known package types
        self.package_type_namespaces = {
            'IServiceCollection': 'Microsoft.Extensions.DependencyInjection',
            'IConfiguration': 'Microsoft.Extensions.Configuration',
            'ILogger': 'Microsoft.Extensions.Logging',
            'IOptions': 'Microsoft.Extensions.Options',
            'IHostBuilder': 'Microsoft.Extensions.Hosting',
            'IHost': 'Microsoft.Extensions.Hosting',
            'FakeTimeProvider': 'Microsoft.Extensions.TimeProvider.Testing',
            'TimeProvider': 'System',
            'JsonProperty': 'Newtonsoft.Json',
            'JsonConvert': 'Newtonsoft.Json',
            'JObject': 'Newtonsoft.Json.Linq',
            'JArray': 'Newtonsoft.Json.Linq',
            'JToken': 'Newtonsoft.Json.Linq',
            'HttpMethod': 'System.Net.Http',
            'HttpRequestMessage': 'System.Net.Http',
            'HttpResponseMessage': 'System.Net.Http',
            'HttpContent': 'System.Net.Http',
            'StringContent': 'System.Net.Http',
            'ByteArrayContent': 'System.Net.Http',
            'FormUrlEncodedContent': 'System.Net.Http',
            'MultipartFormDataContent': 'System.Net.Http',
            'HttpClientHandler': 'System.Net.Http',
            'DelegatingHandler': 'System.Net.Http',
            'HttpMessageHandler': 'System.Net.Http',
        }
    
    def _load_type_dictionary(self) -> Dict:
        """Load the JSON type dictionary."""
        dict_path = self.base_path / "scripts" / "exxerai_types.json"
        if dict_path.exists():
            try:
                with open(dict_path, 'r', encoding='utf-8') as f:
                    return json.load(f)
            except Exception as e:
                print(f"Error loading type dictionary: {e}")
                return {}
        else:
            print(f"Type dictionary not found at {dict_path}")
            return {}
    
    def _load_directory_build_props(self) -> Set[str]:
        """Load injected namespaces from Directory.Build.props."""
        injected = set()
        
        # Check both test and production Directory.Build.props
        props_files = [
            self.tests_path / "Directory.Build.props",
            self.src_path / "Directory.Build.props"
        ]
        
        for props_file in props_files:
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
                    print(f"Error parsing {props_file}: {e}")
                    
        return injected
    
    def _load_global_usings(self, project_path: Path) -> Set[str]:
        """Load existing namespaces from GlobalUsings.cs."""
        global_usings_path = project_path / "GlobalUsings.cs"
        
        if str(global_usings_path) in self.global_usings_cache:
            return self.global_usings_cache[str(global_usings_path)]
        
        namespaces = set()
        if global_usings_path.exists():
            try:
                with open(global_usings_path, 'r', encoding='utf-8') as f:
                    content = f.read()
                    
                # Match global using statements
                pattern = r'global\s+using\s+(?:static\s+)?([^;]+);'
                matches = re.findall(pattern, content)
                
                for match in matches:
                    namespace = match.strip()
                    namespaces.add(namespace)
                    
                self.global_usings_cache[str(global_usings_path)] = namespaces
            except Exception as e:
                print(f"Error reading {global_usings_path}: {e}")
                
        return namespaces
    
    def _find_project_directory(self, project_name: str) -> Optional[Path]:
        """Find the directory containing a project."""
        # Search in both test and production paths
        search_paths = [
            self.tests_path / "03UnitTests" / project_name,
            self.tests_path / "04IntegrationTests" / project_name,
            self.tests_path / "01Application" / project_name,
            self.tests_path / "02Infrastructure" / project_name,
            self.src_path / "00Domain" / project_name,
            self.src_path / "01Application" / project_name,
            self.src_path / "02Infrastructure" / project_name,
            self.src_path / "03Infrastructure" / project_name,
            self.src_path / "04Api" / project_name,
            self.src_path / "05WebApps" / project_name,
            self.src_path / "Infrastructure" / project_name,
            self.src_path / "Domain" / project_name,
        ]
        
        for path in search_paths:
            if path.exists() and path.is_dir():
                return path
        
        # Fallback: search recursively
        for csproj in self.src_path.rglob(f"{project_name}.csproj"):
            if 'bin' not in str(csproj) and 'obj' not in str(csproj):
                return csproj.parent
                
        return None
    
    def parse_error_file(self, error_file: str) -> List[Dict]:
        """Parse CS0246 and CS0103 errors from the error file."""
        print(f"Parsing error file: {error_file}")
        errors = []
        
        with open(error_file, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        for line in lines[1:]:  # Skip header
            if line.strip():
                parts = line.strip().split('\t')
                if len(parts) >= 6:
                    error_code = parts[1]
                    
                    # Handle CS0246: The type or namespace name 'X' could not be found
                    if 'CS0246' in error_code:
                        match = re.search(r"'([^']+)'", parts[2])
                        if match:
                            missing_type = match.group(1)
                            errors.append({
                                'type': missing_type,
                                'project': parts[3],
                                'file': parts[4],
                                'line': parts[5],
                                'error_code': 'CS0246',
                                'full_error': parts[2]
                            })
                    
                    # Handle CS0103: The name 'X' does not exist in the current context
                    elif 'CS0103' in error_code:
                        match = re.search(r"'([^']+)'", parts[2])
                        if match:
                            missing_name = match.group(1)
                            errors.append({
                                'type': missing_name,
                                'project': parts[3],
                                'file': parts[4],
                                'line': parts[5],
                                'error_code': 'CS0103',
                                'full_error': parts[2]
                            })
        
        print(f"Parsed {len(errors)} errors ({len([e for e in errors if e['error_code'] == 'CS0246'])} CS0246, {len([e for e in errors if e['error_code'] == 'CS0103'])} CS0103)")
        return errors
    
    def analyze_type(self, type_name: str, project_name: str) -> Dict:
        """Analyze a missing type and determine namespace needed."""
        result = {
            'type': type_name,
            'project': project_name
        }
        
        # First check type dictionary for ExxerAI types
        type_lookup = self.type_dictionary.get('type_lookup', {})
        if type_name in type_lookup:
            namespace = type_lookup[type_name].get('namespace')
            result.update({
                'namespace': namespace,
                'source': 'exxerai_type',
                'action': 'add_namespace_to_globalusings'
            })
            return result
        
        # Check CLR types
        if type_name in self.clr_type_namespaces:
            result.update({
                'namespace': self.clr_type_namespaces[type_name],
                'source': 'clr_type',
                'action': 'add_namespace_to_globalusings'
            })
            return result
        
        # Check package types
        if type_name in self.package_type_namespaces:
            result.update({
                'namespace': self.package_type_namespaces[type_name],
                'source': 'package_type',
                'action': 'add_namespace_to_globalusings'
            })
            return result
        
        # Special handling for generic Result<T>
        if type_name.startswith('Result<') or type_name == 'Result':
            result.update({
                'namespace': 'ExxerAI.Domain.Common',
                'source': 'exxerai_type',
                'action': 'add_namespace_to_globalusings'
            })
            return result
        
        # Unknown type
        result.update({
            'namespace': None,
            'source': 'unknown',
            'action': 'investigate',
            'note': 'Type not found in any known mappings'
        })
        
        return result
    
    def generate_enhanced_report(self, errors: List[Dict], output_file: str):
        """Generate enhanced analysis report for GlobalUsings.cs modifications."""
        print("\nGenerating enhanced analysis report...")
        
        report = {
            'analysis_date': datetime.now().isoformat(),
            'total_errors': len(errors),
            'cs0246_errors': len([e for e in errors if e['error_code'] == 'CS0246']),
            'cs0103_errors': len([e for e in errors if e['error_code'] == 'CS0103']),
            'directory_build_props_namespaces': list(self.injected_namespaces),
            'errors_by_project': defaultdict(list),
            'globalusings_modifications': defaultdict(set),
            'namespaces_already_present': defaultdict(set),
            'unknown_types': defaultdict(set),
            'summary': {
                'projects_needing_updates': 0,
                'total_namespaces_to_add': 0,
                'namespaces_already_present': 0,
                'unknown_types': 0
            }
        }
        
        # Analyze each error
        for error in errors:
            project_name = error['project']
            type_name = error['type']
            
            # Get project directory
            project_dir = self._find_project_directory(project_name)
            if not project_dir:
                report['unknown_types'][project_name].add(f"{type_name} (project dir not found)")
                continue
            
            # Load existing GlobalUsings for this project
            existing_usings = self._load_global_usings(project_dir)
            
            # Analyze the type
            type_info = self.analyze_type(type_name, project_name)
            
            if type_info['action'] == 'add_namespace_to_globalusings':
                namespace = type_info['namespace']
                
                # Check if namespace is already present
                if namespace in existing_usings:
                    report['namespaces_already_present'][project_name].add(namespace)
                    report['summary']['namespaces_already_present'] += 1
                elif namespace in self.injected_namespaces:
                    report['namespaces_already_present'][project_name].add(f"{namespace} (from Directory.Build.props)")
                    report['summary']['namespaces_already_present'] += 1
                else:
                    # Need to add this namespace
                    report['globalusings_modifications'][project_name].add(namespace)
                    report['summary']['total_namespaces_to_add'] += 1
            else:
                # Unknown type
                report['unknown_types'][project_name].add(type_name)
                report['summary']['unknown_types'] += 1
            
            # Add to errors by project
            report['errors_by_project'][project_name].append({
                **error,
                'resolution': type_info
            })
        
        # Count projects needing updates
        report['summary']['projects_needing_updates'] = len(report['globalusings_modifications'])
        
        # Convert sets to lists for JSON serialization
        report['globalusings_modifications'] = {
            k: sorted(list(v)) for k, v in report['globalusings_modifications'].items()
        }
        report['namespaces_already_present'] = {
            k: sorted(list(v)) for k, v in report['namespaces_already_present'].items()
        }
        report['unknown_types'] = {
            k: sorted(list(v)) for k, v in report['unknown_types'].items()
        }
        
        # Save report
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2, ensure_ascii=False)
        
        print(f"\nAnalysis complete! Report saved to: {output_file}")
        self._print_summary(report)
    
    def _print_summary(self, report: Dict):
        """Print analysis summary."""
        print("\n=== ENHANCED ANALYSIS SUMMARY ===")
        print(f"Total errors analyzed: {report['total_errors']}")
        print(f"  CS0246 errors: {report['cs0246_errors']}")
        print(f"  CS0103 errors: {report['cs0103_errors']}")
        
        print(f"\nResolution summary:")
        print(f"  Projects needing GlobalUsings.cs updates: {report['summary']['projects_needing_updates']}")
        print(f"  Total namespaces to add: {report['summary']['total_namespaces_to_add']}")
        print(f"  Namespaces already present: {report['summary']['namespaces_already_present']}")
        print(f"  Unknown types: {report['summary']['unknown_types']}")
        
        # Show top projects needing updates
        mods = report['globalusings_modifications']
        if mods:
            print(f"\nTop projects needing GlobalUsings.cs updates:")
            sorted_projects = sorted(mods.items(), key=lambda x: len(x[1]), reverse=True)
            for project, namespaces in sorted_projects[:5]:
                print(f"  {project}: {len(namespaces)} namespaces")
                for ns in list(namespaces)[:3]:
                    print(f"    + {ns}")
                if len(namespaces) > 3:
                    print(f"    ... and {len(namespaces) - 3} more")
        
        # Show unknown types
        unknown = report['unknown_types']
        if unknown:
            print(f"\nProjects with unknown types:")
            for project, types in list(unknown.items())[:5]:
                print(f"  {project}: {len(types)} unknown types")
                for t in list(types)[:3]:
                    print(f"    ? {t}")
                if len(types) > 3:
                    print(f"    ... and {len(types) - 3} more")


def main():
    import argparse
    
    parser = argparse.ArgumentParser(description='Enhanced smart dependency analyzer for GlobalUsings.cs')
    parser.add_argument('--base-path', default='F:/Dynamic/ExxerAi/ExxerAI',
                       help='Base path of the ExxerAI project')
    parser.add_argument('--error-file', default='F:/Dynamic/ExxerAi/ExxerAI/Errors/CS0246.txt',
                       help='Path to error file (contains both CS0246 and CS0103)')
    parser.add_argument('--output', default='enhanced_dependency_analysis.json',
                       help='Output JSON file for analysis')
    
    args = parser.parse_args()
    
    analyzer = EnhancedSmartDependencyAnalyzer(args.base_path)
    errors = analyzer.parse_error_file(args.error_file)
    analyzer.generate_enhanced_report(errors, args.output)


if __name__ == "__main__":
    main()