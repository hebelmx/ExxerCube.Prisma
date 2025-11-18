#!/usr/bin/env python3
"""
Generates targeted GlobalUsings.cs files based on test project categories.
Each category gets only the namespaces it actually needs.
"""

import os
import json
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Set, List, Dict
from datetime import datetime


class TargetedGlobalUsingsGenerator:
    """Generates appropriate GlobalUsings.cs based on project dependencies."""
    
    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.tests_path = self.base_path / "code" / "src" / "tests"
        
        # Define targeted namespaces for each category
        self.category_namespaces = {
            "Domain": {
                "description": "Domain-only test projects",
                "namespaces": [
                    # Test frameworks
                    "Xunit",
                    "NSubstitute",
                    "Shouldly",
                    "IndQuestResults",
                    "IndQuestResults.Operations",
                    "IndQuestResults.Validation",
                    
                    # System basics
                    "System",
                    "System.Collections.Generic",
                    "System.Linq",
                    "System.Text",
                    "System.IO",
                    "System.Threading",
                    "System.Threading.Tasks",
                    "System.Collections.Concurrent",
                    "System.Collections.Immutable",
                    "System.ComponentModel",
                    "System.ComponentModel.DataAnnotations",
                    "System.Diagnostics",
                    "System.Diagnostics.CodeAnalysis",
                    "System.Globalization",
                    "System.Reflection",
                    "System.Text.Json",
                    "System.Text.Json.Serialization",
                    "System.Text.RegularExpressions",
                    
                    # Microsoft.Extensions basics
                    "Microsoft.Extensions.Logging",
                    
                    # ExxerAI Domain
                    "ExxerAI.Domain",
                    "ExxerAI.Domain.Common",
                    "ExxerAI.Domain.Entities",
                    "ExxerAI.Domain.Enums",
                    "ExxerAI.Domain.ValueObjects",
                    "ExxerAI.Domain.Events",
                    "ExxerAI.Domain.DT",
                    "ExxerAI.Domain.Models",
                    "ExxerAI.Domain.Configurations",
                    "Microsoft.Extensions.TimeProvider.Testing"
                ]
            },
            
            "Application": {
                "description": "Application layer test projects",
                "namespaces": [
                    # Include everything from Domain
                    # (will be merged in code)
                    
                    # Additional Microsoft.Extensions
                    "Microsoft.Extensions.DependencyInjection",
                    "Microsoft.Extensions.Configuration",
                    "Microsoft.Extensions.Options",
                    
                    # ExxerAI Application
                    "ExxerAI.Application",
                    "ExxerAI.Application.Common",
                    "ExxerAI.Application.Common.Interfaces",
                    "ExxerAI.Application.DTOs",
                    "ExxerAI.Application.Interfaces",
                    "ExxerAI.Application.Services",
                    "ExxerAI.Application.Ports"
                ]
            },
            
            "Infrastructure": {
                "description": "Infrastructure test projects",
                "namespaces": [
                    # Include everything from Application
                    # (will be merged in code)
                    
                    # EntityFramework (for infrastructure tests)
                    "Microsoft.EntityFrameworkCore",
                    "Microsoft.EntityFrameworkCore.ChangeTracking",
                    
                    # Additional Microsoft
                    "Microsoft.Extensions.Caching.Memory",
                    "Microsoft.Extensions.Http",
                    
                    # ExxerAI Infrastructure basics
                    "ExxerAI.Axis",
                    "ExxerAI.Axis.Interfaces",
                    "ExxerAI.Axis.Models",
                    "ExxerAI.Axis.Services",
                    "ExxerAI.Datastream",
                    "ExxerAI.Datastream.Data",
                    "ExxerAI.Axioms.Interfaces",
                    "ExxerAI.Axioms.Models"
                ]
            },
            
            "Api": {
                "description": "API test projects",
                "namespaces": [
                    # Include everything from Application
                    # (will be merged in code)
                    
                    # ASP.NET Core basics for API testing
                    "Microsoft.AspNetCore.Mvc",
                    "Microsoft.AspNetCore.Http",
                    
                    # API specific
                    "ExxerAI.Api.Models",
                    "ExxerAI.Api.Services"
                ]
            },
            
            "Integration": {
                "description": "Integration test projects",
                "namespaces": [
                    # Include everything from Infrastructure
                    # (will be merged in code)
                    
                    # Additional for integration testing
                    "Microsoft.AspNetCore.TestHost",
                    "Microsoft.Extensions.Hosting",
                    
                    # May need some infrastructure namespaces
                    "ExxerAI.Composition"
                ]
            }
        }
    
    def get_namespaces_for_category(self, category: str) -> List[str]:
        """Get all namespaces for a category, including inherited ones."""
        namespaces = set()
        
        # Base test frameworks (always included)
        base_namespaces = self.category_namespaces["Domain"]["namespaces"]
        namespaces.update(base_namespaces)
        
        # Add category-specific namespaces
        if category == "Application":
            namespaces.update(self.category_namespaces["Application"]["namespaces"])
        elif category == "Infrastructure":
            namespaces.update(self.category_namespaces["Application"]["namespaces"])
            namespaces.update(self.category_namespaces["Infrastructure"]["namespaces"])
        elif category == "Api":
            namespaces.update(self.category_namespaces["Application"]["namespaces"])
            namespaces.update(self.category_namespaces["Api"]["namespaces"])
        elif category == "Integration":
            namespaces.update(self.category_namespaces["Application"]["namespaces"])
            namespaces.update(self.category_namespaces["Infrastructure"]["namespaces"])
            namespaces.update(self.category_namespaces["Integration"]["namespaces"])
            
        return sorted(list(namespaces))
    
    def parse_directory_build_props(self) -> Set[str]:
        """Parse Directory.Build.props to find already defined global usings."""
        props_usings = set()
        
        props_file = self.tests_path / "Directory.Build.props"
        if props_file.exists():
            try:
                tree = ET.parse(props_file)
                root = tree.getroot()
                
                for using in root.findall(".//Using"):
                    include = using.get('Include')
                    if include:
                        props_usings.add(include)
            except Exception as e:
                print(f"Error parsing Directory.Build.props: {e}")
        
        return props_usings
    
    def generate_globalusings_content(self, category: str) -> str:
        """Generate GlobalUsings.cs content for a specific category."""
        namespaces = self.get_namespaces_for_category(category)
        
        # Remove namespaces already in Directory.Build.props
        props_usings = self.parse_directory_build_props()
        namespaces = [ns for ns in namespaces if ns not in props_usings]
        
        # Group namespaces
        system_ns = sorted([ns for ns in namespaces if ns.startswith('System')])
        microsoft_ns = sorted([ns for ns in namespaces if ns.startswith('Microsoft')])
        exxerai_ns = sorted([ns for ns in namespaces if ns.startswith('ExxerAI')])
        other_ns = sorted([ns for ns in namespaces if not any(ns.startswith(p) for p in ['System', 'Microsoft', 'ExxerAI'])])
        
        # Build content
        content = [
            "// Global using directives for test projects",
            f"// Generated on: {datetime.now().isoformat()}",
            f"// Category: {category} - {self.category_namespaces.get(category, {}).get('description', '')}",
            "// This file contains only namespaces appropriate for this test category.",
            ""
        ]
        
        if system_ns:
            content.append("// System namespaces")
            content.extend([f"global using {ns};" for ns in system_ns])
            content.append("")
            
        if microsoft_ns:
            content.append("// Microsoft namespaces")
            content.extend([f"global using {ns};" for ns in microsoft_ns])
            content.append("")
            
        if exxerai_ns:
            content.append("// ExxerAI namespaces")
            content.extend([f"global using {ns};" for ns in exxerai_ns])
            content.append("")
            
        if other_ns:
            content.append("// Testing framework namespaces")
            content.extend([f"global using {ns};" for ns in other_ns])
            content.append("")
        
        return '\n'.join(content)
    
    def load_analysis(self, analysis_file: str) -> Dict:
        """Load the test project analysis."""
        with open(analysis_file, 'r', encoding='utf-8') as f:
            return json.load(f)
    
    def deploy_targeted_globalusings(self, analysis_file: str, dry_run: bool = True):
        """Deploy targeted GlobalUsings.cs to test projects."""
        analysis = self.load_analysis(analysis_file)
        
        deployed = 0
        for category, projects in analysis.items():
            print(f"\n=== Processing {category} projects ({len(projects)}) ===")
            
            # Generate content for this category
            content = self.generate_globalusings_content(category)
            
            for project in projects:
                project_path = Path(project['path']).parent
                globalusings_file = project_path / "GlobalUsings.cs"
                
                if dry_run:
                    print(f"[DRY RUN] Would update: {globalusings_file}")
                else:
                    # Backup existing file
                    if globalusings_file.exists():
                        backup_file = globalusings_file.with_suffix('.cs.bak')
                        # If backup already exists, remove it first
                        if backup_file.exists():
                            backup_file.unlink()
                        globalusings_file.rename(backup_file)
                        print(f"  Backed up to: {backup_file}")
                    
                    # Write new content
                    globalusings_file.write_text(content, encoding='utf-8')
                    print(f"  Written: {globalusings_file}")
                    deployed += 1
        
        if not dry_run:
            print(f"\nDeployed GlobalUsings.cs to {deployed} test projects")
    
    def preview_category(self, category: str):
        """Preview GlobalUsings.cs for a specific category."""
        content = self.generate_globalusings_content(category)
        print(f"\n=== GlobalUsings.cs for {category} ===")
        print(content)


def main():
    import argparse
    
    parser = argparse.ArgumentParser(description='Generate targeted GlobalUsings for test projects')
    parser.add_argument('--base-path', default='F:/Dynamic/ExxerAi/ExxerAI',
                       help='Base path of the ExxerAI project')
    parser.add_argument('--analysis', default='test_dependency_analysis.json',
                       help='Test dependency analysis file')
    parser.add_argument('--preview', choices=['Domain', 'Application', 'Infrastructure', 'Api', 'Integration'],
                       help='Preview GlobalUsings for a specific category')
    parser.add_argument('--deploy', action='store_true',
                       help='Deploy GlobalUsings to test projects')
    parser.add_argument('--dry-run', action='store_true',
                       help='Run in dry-run mode')
    
    args = parser.parse_args()
    
    generator = TargetedGlobalUsingsGenerator(args.base_path)
    
    if args.preview:
        generator.preview_category(args.preview)
    elif args.deploy:
        generator.deploy_targeted_globalusings(args.analysis, dry_run=args.dry_run)
    else:
        # Show available categories
        print("Available test project categories:")
        for category, info in generator.category_namespaces.items():
            print(f"  {category}: {info['description']}")
        print("\nUse --preview <category> to see GlobalUsings for a category")
        print("Use --deploy --dry-run to see what would be deployed")


if __name__ == "__main__":
    main()