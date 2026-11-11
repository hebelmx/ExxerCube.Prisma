#!/usr/bin/env python3
"""
Foreign vs Internal Type Analysis Script
Analyzes compilation errors to distinguish between:
1. Missing internal types (need to be implemented)
2. Foreign types from NuGet packages (need package references)
"""

import os
import re
import json
from pathlib import Path
from typing import Set, Dict, List, Tuple
from collections import defaultdict

def extract_error_types_from_cs0246(error_file: str) -> Set[str]:
    """Extract all missing type names from CS0246 errors."""
    missing_types = set()
    
    try:
        with open(error_file, 'r', encoding='utf-8') as f:
            for line in f:
                # Skip header
                if line.startswith('Severity'):
                    continue
                    
                # Look for CS0246 errors
                if 'CS0246' in line and 'could not be found' in line:
                    # Extract type name from error message
                    match = re.search(r"The type or namespace name '([^']+)' could not be found", line)
                    if match:
                        type_name = match.group(1)
                        # Clean up generic types and attributes
                        type_name = re.sub(r'<.*?>', '', type_name)  # Remove generics
                        type_name = re.sub(r'Attribute$', '', type_name)  # Remove Attribute suffix
                        missing_types.add(type_name)
                        
    except FileNotFoundError:
        print(f"Error file not found: {error_file}")
        
    return missing_types

def scan_internal_types(src_path: str) -> Set[str]:
    """Scan all .cs files to find internally defined types."""
    internal_types = set()
    
    # Patterns to match type definitions
    type_patterns = [
        r'public\s+(?:sealed\s+)?class\s+(\w+)',
        r'public\s+(?:sealed\s+)?record\s+(\w+)',
        r'public\s+interface\s+(\w+)',
        r'public\s+enum\s+(\w+)',
        r'public\s+struct\s+(\w+)',
        r'internal\s+(?:sealed\s+)?class\s+(\w+)',
        r'internal\s+interface\s+(\w+)',
        r'internal\s+enum\s+(\w+)',
        r'internal\s+struct\s+(\w+)',
    ]
    
    for root, dirs, files in os.walk(src_path):
        # Skip test directories for now - focus on main types
        if 'test' in root.lower() or 'Test' in root:
            continue
            
        for file in files:
            if file.endswith('.cs'):
                file_path = os.path.join(root, file)
                try:
                    with open(file_path, 'r', encoding='utf-8') as f:
                        content = f.read()
                        
                        for pattern in type_patterns:
                            matches = re.findall(pattern, content, re.MULTILINE)
                            for match in matches:
                                internal_types.add(match)
                                
                except UnicodeDecodeError:
                    continue
                except Exception as e:
                    print(f"Error reading {file_path}: {e}")
                    continue
    
    return internal_types

def analyze_package_props(props_path: str) -> Dict[str, List[str]]:
    """Analyze Directory.Packages.props to find available NuGet packages."""
    packages = {}
    
    try:
        with open(props_path, 'r', encoding='utf-8') as f:
            content = f.read()
            
            # Extract PackageReference entries
            pattern = r'<PackageReference\s+Include="([^"]+)"\s+Version="([^"]+)"'
            matches = re.findall(pattern, content)
            
            for package_name, version in matches:
                packages[package_name] = [version]
                
    except FileNotFoundError:
        print(f"Package props file not found: {props_path}")
        
    return packages

def categorize_missing_types(missing_types: Set[str], internal_types: Set[str], packages: Dict[str, List[str]]) -> Dict[str, List[str]]:
    """Categorize missing types into internal vs foreign."""
    result = {
        'definitely_internal': [],  # Missing types that should be internal
        'likely_foreign': [],       # Types that look like they're from NuGet packages
        'unknown': []              # Ambiguous cases
    }
    
    # Known internal prefixes/patterns
    internal_prefixes = {'ExxerAI', 'Exxer', 'Graph', 'Vector', 'Document', 'Agent', 'Workflow'}
    
    # Known foreign patterns (common NuGet package types)
    foreign_patterns = [
        r'^Test',           # Testing frameworks
        r'^Mock',           # Mocking frameworks  
        r'^Http',           # HTTP client types
        r'^Json',           # JSON serialization
        r'^Sql',            # Database types
        r'^Entity',         # Entity Framework
        r'^Options',        # Configuration
        r'^Service',        # Generic service types
        r'^Logger',         # Logging
        r'^Memory',         # Memory/caching
        r'^Configuration',  # Configuration
        r'^Collection',     # Collections
        r'^Async',          # Async utilities
        r'^Concurrent',     # Threading
        r'^Threading',      # Threading
    ]
    
    for missing_type in missing_types:
        # Skip if already defined internally
        if missing_type in internal_types:
            continue
            
        # Check if looks internal
        is_likely_internal = any(prefix in missing_type for prefix in internal_prefixes)
        
        # Check if looks foreign
        is_likely_foreign = any(re.match(pattern, missing_type) for pattern in foreign_patterns)
        
        if is_likely_internal:
            result['definitely_internal'].append(missing_type)
        elif is_likely_foreign:
            result['likely_foreign'].append(missing_type)
        else:
            result['unknown'].append(missing_type)
    
    return result

def suggest_packages_for_types(foreign_types: List[str], packages: Dict[str, List[str]]) -> Dict[str, List[str]]:
    """Suggest which packages might contain foreign types."""
    suggestions = defaultdict(list)
    
    # Common type-to-package mappings
    package_hints = {
        'Test': ['xunit.v3', 'Microsoft.NET.Test.Sdk', 'NUnit'],
        'Mock': ['NSubstitute', 'Moq'],
        'Http': ['Microsoft.AspNetCore', 'System.Net.Http'],
        'Json': ['System.Text.Json', 'Newtonsoft.Json'],
        'Entity': ['Microsoft.EntityFrameworkCore'],
        'Sql': ['Microsoft.Data.SqlClient', 'System.Data.SqlClient'],
        'Logger': ['Microsoft.Extensions.Logging'],
        'Memory': ['Microsoft.Extensions.Caching.Memory'],
        'Configuration': ['Microsoft.Extensions.Configuration'],
        'Options': ['Microsoft.Extensions.Options'],
        'Collection': ['System.Collections', 'System.Collections.Generic'],
        'Async': ['System.Threading.Tasks'],
        'Playwright': ['Microsoft.Playwright'],
        'Shouldly': ['Shouldly'],
        'NSubstitute': ['NSubstitute'],
    }
    
    for type_name in foreign_types:
        for hint, potential_packages in package_hints.items():
            if hint in type_name:
                for pkg in potential_packages:
                    if pkg in packages:
                        suggestions[type_name].append(pkg)
    
    return dict(suggestions)

def main():
    base_path = "F:/Dynamic/ExxerAi/ExxerAI"
    error_file = os.path.join(base_path, "Errors", "CS0246.txt")
    src_path = os.path.join(base_path, "code", "src")
    props_path = os.path.join(src_path, "Directory.Packages.props")
    
    print("🔍 Starting Foreign vs Internal Type Analysis...")
    
    # Step 1: Extract missing types from errors
    print("\n📋 Extracting missing types from CS0246 errors...")
    missing_types = extract_error_types_from_cs0246(error_file)
    print(f"   Found {len(missing_types)} missing types")
    
    # Step 2: Scan internal type definitions
    print("\n🔍 Scanning internal type definitions...")
    internal_types = scan_internal_types(src_path)
    print(f"   Found {len(internal_types)} internal types")
    
    # Step 3: Analyze available packages
    print("\n📦 Analyzing available NuGet packages...")
    packages = analyze_package_props(props_path)
    print(f"   Found {len(packages)} available packages")
    
    # Step 4: Categorize missing types
    print("\n🎯 Categorizing missing types...")
    categorized = categorize_missing_types(missing_types, internal_types, packages)
    
    # Step 5: Suggest packages for foreign types
    print("\n💡 Suggesting packages for foreign types...")
    package_suggestions = suggest_packages_for_types(categorized['likely_foreign'], packages)
    
    # Generate report
    print("\n" + "="*60)
    print("📊 FOREIGN vs INTERNAL TYPE ANALYSIS REPORT")
    print("="*60)
    
    print(f"\n🏠 DEFINITELY INTERNAL ({len(categorized['definitely_internal'])}):")
    print("   These types should be implemented in our codebase:")
    for type_name in sorted(categorized['definitely_internal']):
        print(f"   • {type_name}")
    
    print(f"\n📦 LIKELY FOREIGN ({len(categorized['likely_foreign'])}):")
    print("   These types are probably from NuGet packages:")
    for type_name in sorted(categorized['likely_foreign']):
        suggestions = package_suggestions.get(type_name, ['Unknown package'])
        print(f"   • {type_name} → {', '.join(suggestions)}")
    
    print(f"\n❓ UNKNOWN ({len(categorized['unknown'])}):")
    print("   These types need manual investigation:")
    for type_name in sorted(categorized['unknown']):
        print(f"   • {type_name}")
    
    print(f"\n📈 SUMMARY:")
    print(f"   • Total missing types: {len(missing_types)}")
    print(f"   • Already defined internally: {len(missing_types) - len(categorized['definitely_internal']) - len(categorized['likely_foreign']) - len(categorized['unknown'])}")
    print(f"   • Need to implement: {len(categorized['definitely_internal'])}")
    print(f"   • Need package references: {len(categorized['likely_foreign'])}")
    print(f"   • Need investigation: {len(categorized['unknown'])}")
    
    # Save detailed analysis
    output_file = os.path.join(base_path, "scripts", "foreign_vs_internal_analysis.json")
    analysis_data = {
        'missing_types': list(missing_types),
        'internal_types': list(internal_types),
        'categorized': categorized,
        'package_suggestions': package_suggestions,
        'available_packages': packages
    }
    
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(analysis_data, f, indent=2)
    
    print(f"\n💾 Detailed analysis saved to: {output_file}")
    
    print("\n🚀 NEXT STEPS:")
    print("   1. Implement the 'definitely_internal' types")
    print("   2. Add missing package references for 'likely_foreign' types")
    print("   3. Investigate 'unknown' types manually")
    print("   4. Run build again to verify fixes")

if __name__ == "__main__":
    main()