#!/usr/bin/env python3
"""
Comprehensive C# Class Analysis Script
Extracts complete metadata for all C# files to JSON format

Output JSON structure for each class:
{
    "className": "TestSolverAgent",
    "project": "TddDebuggerAgent", 
    "fullPath": "code/legacy/TddDebuggerAgent/Services/TestSolverAgent.cs",
    "fileName": "TestSolverAgent.cs",
    "type": "class|interface|enum|struct|record|delegate",
    "kind": "class",
    "namespace": "ExxerAI.Agents.TddDebuggerAgent",
    "dependencies": ["Microsoft.Extensions.Logging", "System.Threading.Tasks"],
    "globalUsings": ["System", "System.Collections.Generic"],
    "fileUsings": ["Microsoft.Extensions.Logging", "ExxerAI.Domain"]
}
"""

import os
import re
import json
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Dict, List, Set, Optional, Tuple
from datetime import datetime

class ComprehensiveCsAnalyzer:
    """Comprehensive analyzer for C# classes, interfaces, enums, etc."""
    
    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.legacy_path = self.base_path / "code" / "legacy"
        self.backup_legacy_path = Path("F:/Dynamic/ExxerAi/ExxerAI/backups/legacy")
        self.src_path = self.base_path / "code" / "src"
        
        # Cache for project dependencies and global usings
        self.project_dependencies = {}
        self.global_usings_cache = {}
        
    def extract_class_info_from_content(self, content: str, file_path: Path) -> List[Dict]:
        """Extract all class/interface/enum definitions from C# file content."""
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
                    'fileUsings': file_usings
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
        if 'legacy' in str(file_path):
            # For legacy files: code/legacy/ProjectName/...
            parts = file_path.relative_to(self.legacy_path).parts if self.legacy_path in file_path.parents else file_path.parts
        else:
            # For src files: code/src/.../ProjectName/...
            parts = file_path.relative_to(self.src_path).parts if self.src_path in file_path.parents else file_path.parts
            
        return parts[0] if parts else "Unknown"
        
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
        
    def analyze_legacy_projects(self) -> List[Dict]:
        """Analyze all C# files in legacy projects."""
        print(f"Analyzing legacy projects in: {self.legacy_path}")
        return self.analyze_directory(self.legacy_path)
        
    def analyze_src_projects(self) -> List[Dict]:
        """Analyze all C# files in src projects."""
        print(f"Analyzing src projects in: {self.src_path}")
        return self.analyze_directory(self.src_path)
        
    def generate_comprehensive_report(self, classes: List[Dict], output_file: str):
        """Generate comprehensive JSON report."""
        report = {
            'metadata': {
                'generated_on': datetime.now().isoformat(),
                'total_classes': len(classes),
                'analyzer_version': '1.0.0',
                'base_path': str(self.base_path)
            },
            'statistics': self.generate_statistics(classes),
            'classes': classes
        }
        
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2, ensure_ascii=False)
            
        print(f"Comprehensive report generated: {output_file}")
        return report
        
    def generate_statistics(self, classes: List[Dict]) -> Dict:
        """Generate statistics from analyzed classes."""
        stats = {
            'total_classes': len(classes),
            'by_type': {},
            'by_project': {},
            'by_namespace': {},
            'unique_dependencies': set(),
            'unique_namespaces': set()
        }
        
        for cls in classes:
            # Count by type
            cls_type = cls['type']
            stats['by_type'][cls_type] = stats['by_type'].get(cls_type, 0) + 1
            
            # Count by project
            project = cls['project']
            stats['by_project'][project] = stats['by_project'].get(project, 0) + 1
            
            # Count by namespace
            namespace = cls['namespace']
            stats['by_namespace'][namespace] = stats['by_namespace'].get(namespace, 0) + 1
            
            # Collect unique dependencies and namespaces
            stats['unique_dependencies'].update(cls['dependencies'])
            stats['unique_namespaces'].add(namespace)
            
        # Convert sets to sorted lists for JSON serialization
        stats['unique_dependencies'] = sorted(list(stats['unique_dependencies']))
        stats['unique_namespaces'] = sorted(list(stats['unique_namespaces']))
        
        return stats
        
    def analyze_backup_legacy_projects(self) -> List[Dict]:
        """Analyze all C# files in backup legacy projects."""
        print(f"Analyzing backup legacy projects in: {self.backup_legacy_path}")
        return self.analyze_directory(self.backup_legacy_path)

    def run(self, analyze_legacy: bool = True, analyze_src: bool = False, analyze_backup: bool = False):
        """Execute comprehensive C# analysis."""
        print("Starting comprehensive C# class analysis...")
        
        all_classes = []
        
        if analyze_legacy and self.legacy_path.exists():
            legacy_classes = self.analyze_legacy_projects()
            all_classes.extend(legacy_classes)
            print(f"Found {len(legacy_classes)} classes in legacy projects")
            
        if analyze_backup and self.backup_legacy_path.exists():
            backup_classes = self.analyze_backup_legacy_projects()
            all_classes.extend(backup_classes)
            print(f"Found {len(backup_classes)} classes in backup legacy projects")
            
        if analyze_src and self.src_path.exists():
            src_classes = self.analyze_src_projects()
            all_classes.extend(src_classes)
            print(f"Found {len(src_classes)} classes in src projects")
            
        if not all_classes:
            print("No C# files found to analyze")
            return
            
        # Generate comprehensive JSON report
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        output_file = f"cs_class_analysis_{timestamp}.json"
        
        report = self.generate_comprehensive_report(all_classes, output_file)
        
        print("\n" + "="*60)
        print("COMPREHENSIVE C# ANALYSIS COMPLETE!")
        print(f"Total classes analyzed: {len(all_classes)}")
        print(f"Types found: {', '.join(report['statistics']['by_type'].keys())}")
        print(f"Projects: {len(report['statistics']['by_project'])}")
        print(f"Output file: {output_file}")
        print("="*60)
        
        return report

def main():
    import sys
    
    if len(sys.argv) > 1:
        base_path = sys.argv[1]
    else:
        base_path = "."
        
    # Command line options
    analyze_legacy = "--legacy" in sys.argv or "--all" in sys.argv
    analyze_src = "--src" in sys.argv or "--all" in sys.argv
    analyze_backup = "--backup" in sys.argv or "--all" in sys.argv
    
    # Default to backup if no specific option
    if not analyze_legacy and not analyze_src and not analyze_backup:
        analyze_backup = True
        
    analyzer = ComprehensiveCsAnalyzer(base_path)
    analyzer.run(analyze_legacy=analyze_legacy, analyze_src=analyze_src, analyze_backup=analyze_backup)

if __name__ == "__main__":
    main()