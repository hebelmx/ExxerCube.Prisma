#!/usr/bin/env python3
"""
Script to analyze public members without XML documentation in the ExxerAI codebase.

This script scans all C# files in the /code/src directory and identifies:
- Public classes, interfaces, enums, structs without XML documentation
- Public methods, properties, fields without XML documentation  
- Public constructors without XML documentation

The results are categorized by project and evocative domain to help prioritize
documentation efforts according to the ExxerAI architectural principles.
"""

import os
import re
import json
from pathlib import Path
from typing import Dict, List, Tuple, Set
from dataclasses import dataclass, asdict
from datetime import datetime

@dataclass
class UndocumentedMember:
    """Represents a public member missing XML documentation."""
    file_path: str
    line_number: int
    member_type: str  # class, interface, method, property, field, constructor, enum, struct
    member_name: str
    member_signature: str
    evocative_domain: str  # Axis, Datastream, Cortex, etc.
    project_name: str
    accessibility: str  # public, protected, internal public
    
@dataclass
class ProjectSummary:
    """Summary of undocumented members by project."""
    project_name: str
    evocative_domain: str
    total_undocumented: int
    by_type: Dict[str, int]
    files_affected: Set[str]

class XmlDocAnalyzer:
    """Analyzes C# source files for missing XML documentation on public members."""
    
    # ExxerAI Evocative Architecture domains
    EVOCATIVE_DOMAINS = {
        'Axis': '🏗️ Structural backbone - DI, extensions, shared contracts',
        'Datastream': '🌊 Data flow - repositories, EF Core, migrations',
        'Cortex': '🧠 AI brain - LLM adapters, embeddings, inference',
        'Gatekeeper': '🚪 External guardian - API integrations, connectors',
        'Vault': '🏛️ Semantic memory - vector/graph storage',
        'Sentinel': '🛡️ Security protection - auth, credentials',
        'Conduit': '📡 Communication - messaging, agent coordination',
        'Nexus': '⚡ Transformation - document processing, enrichment',
        'Chronos': '⏰ Time master - scheduling, orchestration',
        'Signal': '📊 System heartbeat - monitoring, health checks',
        'Helix': '🧬 Knowledge DNA - graph relationships, semantic linking',
        'Nebula': '🌌 Innovation - experimental, R&D features',
        'Wisdom': '🦉 Ethical compass - governance, compliance'
    }
    
    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.src_path = self.base_path / "code" / "src"
        self.undocumented_members: List[UndocumentedMember] = []
        
    def get_evocative_domain(self, file_path: str) -> str:
        """Determine the evocative domain from the file path."""
        file_path_lower = file_path.lower()
        
        for domain in self.EVOCATIVE_DOMAINS:
            if domain.lower() in file_path_lower:
                return domain
                
        # Check for specific patterns
        if 'application' in file_path_lower:
            return 'Axis'  # Application layer is part of structural backbone
        elif 'domain' in file_path_lower:
            return 'Axis'  # Domain is core structural
        elif 'infrastructure' in file_path_lower:
            return 'Datastream'  # Infrastructure typically handles data flow
        elif 'ui' in file_path_lower or 'web' in file_path_lower:
            return 'Nexus'  # UI/Web is transformation/presentation
        elif 'api' in file_path_lower:
            return 'Gatekeeper'  # APIs are external gateways
        elif 'cli' in file_path_lower:
            return 'Sentinel'  # CLI tools are typically operational/security
        elif 'agents' in file_path_lower:
            return 'Conduit'  # Agent communication
        elif 'mcp' in file_path_lower:
            return 'Conduit'  # Model Context Protocol is communication
            
        return 'Unknown'
    
    def get_project_name(self, file_path: str) -> str:
        """Extract project name from file path."""
        path_parts = Path(file_path).parts
        
        # Look for .csproj pattern in path
        for part in path_parts:
            if part.startswith('ExxerAI.') or part.startswith('CubeXplorer.'):
                return part
                
        # Fallback to directory name
        if len(path_parts) >= 2:
            return path_parts[-2]
            
        return 'Unknown'
    
    def has_xml_documentation(self, lines: List[str], line_index: int) -> bool:
        """Check if the member at line_index has XML documentation above it."""
        # Look backwards from the current line for XML comments
        for i in range(line_index - 1, max(0, line_index - 10), -1):
            line = lines[i].strip()
            
            # Skip empty lines and attributes
            if not line or line.startswith('['):
                continue
                
            # Found XML comment
            if line.startswith('///'):
                return True
                
            # Found other content, stop looking
            if line and not line.startswith('//'):
                break
                
        return False
    
    def analyze_file(self, file_path: Path) -> None:
        """Analyze a single C# file for undocumented public members."""
        try:
            with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
                content = f.read()
                lines = content.split('\n')
        except Exception as e:
            print(f"Error reading {file_path}: {e}")
            return
            
        evocative_domain = self.get_evocative_domain(str(file_path))
        project_name = self.get_project_name(str(file_path))
        
        # Patterns for public members
        patterns = {
            'class': re.compile(r'^\s*public\s+(sealed\s+|static\s+|abstract\s+)?class\s+(\w+)', re.MULTILINE),
            'interface': re.compile(r'^\s*public\s+interface\s+(\w+)', re.MULTILINE),
            'enum': re.compile(r'^\s*public\s+enum\s+(\w+)', re.MULTILINE),
            'struct': re.compile(r'^\s*public\s+(readonly\s+)?struct\s+(\w+)', re.MULTILINE),
            'method': re.compile(r'^\s*public\s+(?:static\s+|virtual\s+|override\s+|abstract\s+)?(?:async\s+)?[\w<>\[\],\s]+\s+(\w+)\s*\(', re.MULTILINE),
            'property': re.compile(r'^\s*public\s+(?:static\s+|virtual\s+|override\s+|abstract\s+)?[\w<>\[\],\s]+\s+(\w+)\s*\{\s*(?:get|set)', re.MULTILINE),
            'field': re.compile(r'^\s*public\s+(?:static\s+|readonly\s+|const\s+)?[\w<>\[\],\s]+\s+(\w+)\s*[;=]', re.MULTILINE),
            'constructor': re.compile(r'^\s*public\s+(\w+)\s*\(', re.MULTILINE)
        }
        
        for member_type, pattern in patterns.items():
            for match in pattern.finditer(content):
                line_number = content[:match.start()].count('\n') + 1
                
                # Skip if has XML documentation
                if self.has_xml_documentation(lines, line_number - 1):
                    continue
                    
                # Extract member name
                if member_type in ['struct', 'class'] and len(match.groups()) > 1:
                    member_name = match.group(2)  # Second group for struct/class with modifiers
                else:
                    member_name = match.group(1)
                    
                # Get the full line for signature
                try:
                    member_signature = lines[line_number - 1].strip()
                except IndexError:
                    member_signature = match.group(0)
                
                undocumented = UndocumentedMember(
                    file_path=str(file_path.relative_to(self.base_path)),
                    line_number=line_number,
                    member_type=member_type,
                    member_name=member_name,
                    member_signature=member_signature,
                    evocative_domain=evocative_domain,
                    project_name=project_name,
                    accessibility='public'
                )
                
                self.undocumented_members.append(undocumented)
    
    def analyze_directory(self) -> None:
        """Analyze all C# files in the source directory."""
        if not self.src_path.exists():
            print(f"Source path does not exist: {self.src_path}")
            return
            
        print(f"Analyzing C# files in: {self.src_path}")
        
        cs_files = list(self.src_path.rglob("*.cs"))
        print(f"Found {len(cs_files)} C# files")
        
        for file_path in cs_files:
            # Skip test files, generated files, and temporary files
            file_str = str(file_path).lower()
            if any(skip in file_str for skip in ['test', 'generated', 'temp', '.designer.', 'assemblyinfo']):
                continue
                
            self.analyze_file(file_path)
            
        print(f"Analysis complete. Found {len(self.undocumented_members)} undocumented public members.")
    
    def generate_report(self) -> Dict:
        """Generate a comprehensive report of findings."""
        # Group by project and evocative domain
        by_project = {}
        by_domain = {}
        by_type = {}
        
        for member in self.undocumented_members:
            # By project
            if member.project_name not in by_project:
                by_project[member.project_name] = []
            by_project[member.project_name].append(member)
            
            # By evocative domain
            if member.evocative_domain not in by_domain:
                by_domain[member.evocative_domain] = []
            by_domain[member.evocative_domain].append(member)
            
            # By member type
            if member.member_type not in by_type:
                by_type[member.member_type] = []
            by_type[member.member_type].append(member)
        
        # Generate project summaries
        project_summaries = []
        for project_name, members in by_project.items():
            evocative_domain = members[0].evocative_domain if members else 'Unknown'
            type_counts = {}
            files_affected = set()
            
            for member in members:
                type_counts[member.member_type] = type_counts.get(member.member_type, 0) + 1
                files_affected.add(member.file_path)
            
            summary = ProjectSummary(
                project_name=project_name,
                evocative_domain=evocative_domain,
                total_undocumented=len(members),
                by_type=type_counts,
                files_affected=files_affected
            )
            project_summaries.append(summary)
        
        # Sort by priority (total undocumented descending)
        project_summaries.sort(key=lambda x: x.total_undocumented, reverse=True)
        
        report = {
            'analysis_timestamp': datetime.now().isoformat(),
            'total_undocumented_members': len(self.undocumented_members),
            'summary_by_type': {k: len(v) for k, v in by_type.items()},
            'summary_by_domain': {k: len(v) for k, v in by_domain.items()},
            'project_summaries': [asdict(s) for s in project_summaries],
            'evocative_domains_info': self.EVOCATIVE_DOMAINS,
            'detailed_members': [asdict(m) for m in self.undocumented_members]
        }
        
        return report
    
    def save_report(self, output_path: str) -> None:
        """Save the analysis report to a JSON file."""
        report = self.generate_report()
        
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2, ensure_ascii=False, default=str)
            
        print(f"Analysis report saved to: {output_path}")
    
    def print_summary(self) -> None:
        """Print a summary of the analysis to console."""
        report = self.generate_report()
        
        print("\n" + "="*80)
        print("🎭 EXXERAI XML DOCUMENTATION ANALYSIS SUMMARY")
        print("="*80)
        print(f"📊 Total undocumented public members: {report['total_undocumented_members']}")
        print()
        
        print("📋 BY MEMBER TYPE:")
        for member_type, count in sorted(report['summary_by_type'].items(), key=lambda x: x[1], reverse=True):
            print(f"  {member_type:12}: {count:4} members")
        print()
        
        print("🎭 BY EVOCATIVE DOMAIN:")
        for domain, count in sorted(report['summary_by_domain'].items(), key=lambda x: x[1], reverse=True):
            domain_info = self.EVOCATIVE_DOMAINS.get(domain, "Unknown domain")
            print(f"  {domain:12}: {count:4} members - {domain_info}")
        print()
        
        print("🏗️ TOP PROJECTS NEEDING DOCUMENTATION:")
        for i, summary in enumerate(report['project_summaries'][:10], 1):
            print(f"  {i:2}. {summary['project_name']:30} ({summary['evocative_domain']}) - {summary['total_undocumented']} members")
        print()
        
        print("💡 RECOMMENDATION:")
        print("   Focus on documenting core Axis and Datastream components first,")
        print("   as they form the architectural foundation. Follow the evocative")
        print("   naming principles to create meaningful, architectural documentation.")
        print("="*80)

def main():
    """Main execution function."""
    import argparse
    
    parser = argparse.ArgumentParser(description='Analyze missing XML documentation in ExxerAI codebase')
    parser.add_argument('--base-path', default='.', help='Base path to ExxerAI repository')
    parser.add_argument('--output', default='missing_xml_docs_analysis.json', help='Output JSON file')
    parser.add_argument('--summary', action='store_true', help='Print summary to console')
    
    args = parser.parse_args()
    
    analyzer = XmlDocAnalyzer(args.base_path)
    analyzer.analyze_directory()
    analyzer.save_report(args.output)
    
    if args.summary:
        analyzer.print_summary()

if __name__ == "__main__":
    main()