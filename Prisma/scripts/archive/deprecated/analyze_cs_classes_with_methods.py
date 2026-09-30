#!/usr/bin/env python3
"""
Enhanced C# Class Analysis Script with Test Methods
Extracts complete metadata including test methods for granular analysis

Enhanced JSON structure:
{
    "className": "TestSolverAgent",
    "project": "TddDebuggerAgent", 
    "fullPath": "code/legacy/TddDebuggerAgent/Services/TestSolverAgent.cs",
    "fileName": "TestSolverAgent.cs",
    "type": "class",
    "namespace": "ExxerAI.Agents.TddDebuggerAgent",
    "dependencies": ["Microsoft.Extensions.Logging"],
    "globalUsings": ["System"],
    "fileUsings": ["Microsoft.Extensions.Logging"],
    "methods": [
        {
            "methodName": "ProcessAsync_ShouldReturnSuccess_WhenValidInput",
            "isTestMethod": true,
            "testAttributes": ["Fact", "Theory"],
            "returnType": "Task",
            "parameters": ["string input"],
            "accessibility": "public"
        }
    ],
    "testMethodCount": 5,
    "isTestClass": true
}
"""

import os
import re
import json
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Dict, List, Set, Optional, Tuple
from datetime import datetime

class EnhancedCsMethodAnalyzer:
    """Enhanced analyzer for C# classes with detailed method extraction."""
    
    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.backup_legacy_path = Path("F:/Dynamic/ExxerAi/ExxerAI/backups/legacy")
        
        # Cache for project dependencies and global usings
        self.project_dependencies = {}
        self.global_usings_cache = {}
        
        # Test method patterns
        self.test_attributes = ['Fact', 'Theory', 'Test', 'TestMethod', 'TestCase']
        
    def extract_methods_from_content(self, content: str) -> List[Dict]:
        """Extract all methods from C# class content with test detection."""
        methods = []
        
        # Enhanced method pattern to capture more details
        method_pattern = r'''
            (?P<attributes>\[(?:[^\[\]]*(?:\[[^\]]*\])?[^\[\]]*)*\])?\s*          # Attributes
            (?P<access>public|private|protected|internal)?\s*                      # Access modifier
            (?P<modifiers>(?:static|virtual|override|abstract|async)\s+)*         # Method modifiers
            (?P<return_type>[\w<>\[\],\s\?]+)\s+                                   # Return type
            (?P<method_name>\w+)\s*                                                # Method name
            \((?P<parameters>[^)]*)\)                                              # Parameters
        '''
        
        matches = re.finditer(method_pattern, content, re.VERBOSE | re.MULTILINE | re.IGNORECASE)
        
        for match in matches:
            method_name = match.group('method_name')
            attributes_text = match.group('attributes') or ''
            return_type = match.group('return_type') or 'void'
            parameters = match.group('parameters') or ''
            access = match.group('access') or 'private'
            
            # Skip constructors, properties, and common non-method patterns
            if (method_name in ['get', 'set', 'add', 'remove'] or 
                method_name[0].isupper() and '(' not in return_type):
                continue
                
            # Extract test attributes
            test_attributes = []
            is_test_method = False
            
            for test_attr in self.test_attributes:
                if test_attr.lower() in attributes_text.lower():
                    test_attributes.append(test_attr)
                    is_test_method = True
            
            # Additional test method detection
            if not is_test_method:
                test_indicators = ['test', 'should', 'when', 'given', 'then']
                method_lower = method_name.lower()
                is_test_method = any(indicator in method_lower for indicator in test_indicators)
            
            method_info = {
                'methodName': method_name,
                'isTestMethod': is_test_method,
                'testAttributes': test_attributes,
                'returnType': return_type.strip(),
                'parameters': [p.strip() for p in parameters.split(',') if p.strip()],
                'accessibility': access,
                'rawAttributes': attributes_text
            }
            
            methods.append(method_info)
            
        return methods
        
    def extract_class_info_from_content(self, content: str, file_path: Path) -> List[Dict]:
        """Extract all class/interface/enum definitions with methods from C# file content."""
        classes = []
        
        # Extract namespace
        namespace = self.extract_namespace(content)
        
        # Extract file-level usings
        file_usings = self.extract_file_usings(content)
        
        # Patterns for different C# type definitions
        patterns = {
            'class': r'(?:public|internal|private|protected)?\s*(?:static|abstract|sealed)?\s*class\s+(\w+)',
            'interface': r'(?:public|internal|private|protected)?\s*interface\s+(\w+)',
            'enum': r'(?:public|internal|private|protected)?\s*enum\s+(\w+)',
            'struct': r'(?:public|internal|private|protected)?\s*(?:readonly)?\s*struct\s+(\w+)',
            'record': r'(?:public|internal|private|protected)?\s*record\s+(\w+)',
            'delegate': r'(?:public|internal|private|protected)?\s*delegate\s+\w+\s+(\w+)'
        }
        
        for kind, pattern in patterns.items():
            matches = re.finditer(pattern, content, re.MULTILINE | re.IGNORECASE)
            for match in matches:
                class_name = match.group(1)
                
                # Skip generic type parameters and common false positives
                if '<' in class_name or class_name in ['var', 'dynamic']:
                    continue
                
                # Extract methods for this class
                methods = self.extract_methods_from_content(content)
                
                # Count test methods
                test_methods = [m for m in methods if m['isTestMethod']]
                test_method_count = len(test_methods)
                
                # Determine if this is a test class
                is_test_class = (
                    test_method_count > 0 or
                    'test' in class_name.lower() or
                    'test' in file_path.name.lower() or
                    'tests' in str(file_path).lower()
                )
                
                class_info = {
                    'className': class_name,
                    'project': self.get_project_name(file_path),
                    'fullPath': str(file_path).replace('\\', '/'),
                    'fileName': file_path.name,
                    'type': kind,
                    'kind': kind,
                    'namespace': namespace,
                    'dependencies': self.get_project_dependencies(file_path),
                    'globalUsings': self.get_global_usings(file_path),
                    'fileUsings': file_usings,
                    'methods': methods,
                    'testMethodCount': test_method_count,
                    'isTestClass': is_test_class,
                    'testMethods': test_methods  # Separate list of just test methods
                }
                classes.append(class_info)
                
        return classes
        
    def extract_namespace(self, content: str) -> str:
        """Extract namespace from C# file content."""
        # Look for namespace declaration
        namespace_match = re.search(r'namespace\s+([^\s\{;]+)', content)
        if namespace_match:
            return namespace_match.group(1).strip()
        
        # Look for file-scoped namespace (C# 10+)
        file_namespace_match = re.search(r'namespace\s+([^\s;]+);', content)
        if file_namespace_match:
            return file_namespace_match.group(1).strip()
            
        return "Unknown"
        
    def extract_file_usings(self, content: str) -> List[str]:
        """Extract using statements from C# file."""
        usings = []
        
        # Pattern for using statements
        using_pattern = r'using\s+(?:static\s+)?([^;=]+);'
        matches = re.findall(using_pattern, content)
        
        for match in matches:
            # Clean up the using statement
            using = match.strip()
            # Skip global using and alias usings
            if not using.startswith('global') and '=' not in using:
                usings.append(using)
                
        return sorted(list(set(usings)))
        
    def get_project_name(self, file_path: Path) -> str:
        """Extract project name from file path."""
        if 'backups' in str(file_path) and 'legacy' in str(file_path):
            # For backup files: backups/legacy/ProjectName/...
            try:
                parts = str(file_path).split('/')
                legacy_idx = parts.index('legacy')
                if legacy_idx + 1 < len(parts):
                    return parts[legacy_idx + 1]
            except:
                pass
        return "Unknown"
        
    def get_project_dependencies(self, file_path: Path) -> List[str]:
        """Extract dependencies from project file (.csproj)."""
        project_name = self.get_project_name(file_path)
        
        if project_name in self.project_dependencies:
            return self.project_dependencies[project_name]
            
        # Find the .csproj file
        project_dir = file_path.parent
        while project_dir != project_dir.parent:
            csproj_files = list(project_dir.glob("*.csproj"))
            if csproj_files:
                dependencies = self.parse_project_dependencies(csproj_files[0])
                self.project_dependencies[project_name] = dependencies
                return dependencies
            project_dir = project_dir.parent
            
        self.project_dependencies[project_name] = []
        return []
        
    def parse_project_dependencies(self, csproj_path: Path) -> List[str]:
        """Parse dependencies from .csproj file."""
        dependencies = []
        
        try:
            tree = ET.parse(csproj_path)
            root = tree.getroot()
            
            # Extract PackageReference dependencies
            for package_ref in root.findall(".//PackageReference"):
                include = package_ref.get("Include")
                if include:
                    dependencies.append(include)
                    
            # Extract ProjectReference dependencies
            for project_ref in root.findall(".//ProjectReference"):
                include = project_ref.get("Include")
                if include:
                    # Extract project name from path
                    project_name = Path(include).stem
                    dependencies.append(project_name)
                    
        except Exception as e:
            print(f"Error parsing {csproj_path}: {e}")
            
        return sorted(dependencies)
        
    def get_global_usings(self, file_path: Path) -> List[str]:
        """Extract global usings from GlobalUsings.cs files."""
        project_name = self.get_project_name(file_path)
        
        if project_name in self.global_usings_cache:
            return self.global_usings_cache[project_name]
            
        global_usings = []
        
        # Look for GlobalUsings.cs in the project directory
        project_dir = file_path.parent
        while project_dir != project_dir.parent:
            global_using_files = list(project_dir.glob("GlobalUsings.cs")) + list(project_dir.glob("GlobalUsing.cs"))
            if global_using_files:
                for global_file in global_using_files:
                    try:
                        with open(global_file, 'r', encoding='utf-8') as f:
                            content = f.read()
                            # Extract global using statements
                            global_pattern = r'global\s+using\s+([^;]+);'
                            matches = re.findall(global_pattern, content)
                            for match in matches:
                                global_usings.append(match.strip())
                    except Exception as e:
                        print(f"Error reading {global_file}: {e}")
                break
            project_dir = project_dir.parent
            
        global_usings = sorted(list(set(global_usings)))
        self.global_usings_cache[project_name] = global_usings
        return global_usings
        
    def analyze_directory(self, directory: Path) -> List[Dict]:
        """Analyze all C# files in a directory recursively."""
        all_classes = []
        
        for cs_file in directory.rglob("*.cs"):
            try:
                with open(cs_file, 'r', encoding='utf-8') as f:
                    content = f.read()
                    
                classes = self.extract_class_info_from_content(content, cs_file)
                all_classes.extend(classes)
                
            except Exception as e:
                print(f"Error processing {cs_file}: {e}")
                
        return all_classes
        
    def analyze_backup_legacy_projects(self) -> List[Dict]:
        """Analyze all C# files in backup legacy projects."""
        print(f"Analyzing backup legacy projects with methods in: {self.backup_legacy_path}")
        return self.analyze_directory(self.backup_legacy_path)
        
    def generate_enhanced_statistics(self, classes: List[Dict]) -> Dict:
        """Generate enhanced statistics including method-level data."""
        stats = {
            'total_classes': len(classes),
            'total_methods': sum(len(cls.get('methods', [])) for cls in classes),
            'total_test_methods': sum(cls.get('testMethodCount', 0) for cls in classes),
            'test_classes': len([cls for cls in classes if cls.get('isTestClass', False)]),
            'by_type': {},
            'by_project': {},
            'test_methods_by_project': {},
            'common_test_patterns': {}
        }
        
        for cls in classes:
            # Count by type
            cls_type = cls['type']
            stats['by_type'][cls_type] = stats['by_type'].get(cls_type, 0) + 1
            
            # Count by project
            project = cls['project']
            stats['by_project'][project] = stats['by_project'].get(project, 0) + 1
            
            # Test methods by project
            if cls.get('isTestClass', False):
                test_count = cls.get('testMethodCount', 0)
                if project not in stats['test_methods_by_project']:
                    stats['test_methods_by_project'][project] = 0
                stats['test_methods_by_project'][project] += test_count
            
            # Analyze test method patterns
            for method in cls.get('testMethods', []):
                method_name = method['methodName']
                # Extract patterns (Should, When, Given, etc.)
                patterns = ['Should', 'When', 'Given', 'Then', 'Test', 'Can', 'Must']
                for pattern in patterns:
                    if pattern in method_name:
                        if pattern not in stats['common_test_patterns']:
                            stats['common_test_patterns'][pattern] = 0
                        stats['common_test_patterns'][pattern] += 1
                        
        return stats
        
    def generate_comprehensive_report(self, classes: List[Dict], output_file: str):
        """Generate comprehensive JSON report with method details."""
        report = {
            'metadata': {
                'generated_on': datetime.now().isoformat(),
                'total_classes': len(classes),
                'analyzer_version': '2.0.0_with_methods',
                'base_path': str(self.base_path)
            },
            'statistics': self.generate_enhanced_statistics(classes),
            'classes': classes
        }
        
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2, ensure_ascii=False)
            
        print(f"Enhanced report with methods generated: {output_file}")
        return report
        
    def run(self):
        """Execute enhanced C# analysis with methods."""
        print("Starting ENHANCED C# class analysis with method extraction...")
        
        if not self.backup_legacy_path.exists():
            print(f"Backup legacy path does not exist: {self.backup_legacy_path}")
            return
            
        backup_classes = self.analyze_backup_legacy_projects()
        
        if not backup_classes:
            print("No C# files found to analyze")
            return
            
        # Generate comprehensive JSON report
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        output_file = f"cs_class_methods_analysis_{timestamp}.json"
        
        report = self.generate_comprehensive_report(backup_classes, output_file)
        
        stats = report['statistics']
        print("\n" + "="*60)
        print("ENHANCED C# ANALYSIS WITH METHODS COMPLETE!")
        print(f"Total classes analyzed: {stats['total_classes']}")
        print(f"Total methods found: {stats['total_methods']}")
        print(f"Test classes: {stats['test_classes']}")
        print(f"Test methods: {stats['total_test_methods']}")
        print(f"Output file: {output_file}")
        print("="*60)
        
        return report

def main():
    import sys
    
    if len(sys.argv) > 1:
        base_path = sys.argv[1]
    else:
        base_path = "."
        
    analyzer = EnhancedCsMethodAnalyzer(base_path)
    analyzer.run()

if __name__ == "__main__":
    main()