#!/usr/bin/env python3
"""
ExxerAI Project Standardization Analyzer
========================================

Smart package analysis system to handle variety of patterns and edge cases
when standardizing .NET 10 test projects with XUnit v3 Universal Configuration.

Features:
- Comprehensive package dictionary with conflict resolution rules
- Pattern matching system for package validation
- Similarity scoring for ambiguous cases
- Smart conflict detection (xunit vs xunit.v3)
- Extension validation (additional vs conflicting packages)
"""

import json
import re
from typing import Dict, List, Set, Tuple, Optional
from dataclasses import dataclass
from enum import Enum

class PackageStatus(Enum):
    """Package validation status"""
    EXACT_MATCH = "exact_match"           # Perfect match
    COMPATIBLE = "compatible"             # Compatible version/extension
    CONFLICTING = "conflicting"           # Conflicts with standard (e.g., xunit vs xunit.v3)
    MISSING = "missing"                   # Required but not present
    UNKNOWN = "unknown"                   # Not in our dictionary
    ADDITIONAL = "additional"             # Extra package (may be OK)

@dataclass
class PackageRule:
    """Rule for package validation"""
    name: str
    required: bool
    conflicts_with: List[str] = None
    compatible_with: List[str] = None
    category: str = ""
    description: str = ""
    
    def __post_init__(self):
        if self.conflicts_with is None:
            self.conflicts_with = []
        if self.compatible_with is None:
            self.compatible_with = []

class ProjectStandardizationAnalyzer:
    """Analyzer for project standardization with smart conflict resolution"""
    
    def __init__(self):
        self.standard_packages = self._build_standard_dictionary()
        self.conflict_rules = self._build_conflict_rules()
        
    def _build_standard_dictionary(self) -> Dict[str, PackageRule]:
        """Build comprehensive package dictionary based on IndTrace XUnit v3 Universal Configuration"""
        
        packages = {
            # CORE TESTING - Required
            "Microsoft.NET.Test.Sdk": PackageRule(
                name="Microsoft.NET.Test.Sdk",
                required=True,
                category="Core Testing",
                description="Core testing SDK"
            ),
            
            # TESTING UTILITIES - Required
            "NSubstitute": PackageRule(
                name="NSubstitute",
                required=True,
                category="Testing Utilities",
                description="Mocking framework"
            ),
            "Shouldly": PackageRule(
                name="Shouldly",
                required=True,
                category="Testing Utilities", 
                description="Assertion framework"
            ),
            "NSubstitute.Analyzers.CSharp": PackageRule(
                name="NSubstitute.Analyzers.CSharp",
                required=True,
                category="Testing Utilities",
                description="NSubstitute analyzers"
            ),
            
            # LOGGING - Required
            "Meziantou.Extensions.Logging.Xunit.v3": PackageRule(
                name="Meziantou.Extensions.Logging.Xunit.v3",
                required=True,
                conflicts_with=["Meziantou.Extensions.Logging.Xunit"],
                category="Logging",
                description="XUnit v3 logging extensions"
            ),
            
            # TIME TESTING - Required
            "Microsoft.Extensions.TimeProvider.Testing": PackageRule(
                name="Microsoft.Extensions.TimeProvider.Testing",
                required=True,
                category="Time Testing",
                description="Time provider testing utilities"
            ),
            
            # CODE ANALYSIS - Required
            "coverlet.collector": PackageRule(
                name="coverlet.collector",
                required=True,
                category="Code Analysis",
                description="Code coverage collector"
            ),
            
            # MICROSOFT TESTING PLATFORM - Required
            "Microsoft.Testing.Platform": PackageRule(
                name="Microsoft.Testing.Platform",
                required=True,
                category="Microsoft Testing Platform",
                description="Microsoft Testing Platform core"
            ),
            "Microsoft.Testing.Platform.MSBuild": PackageRule(
                name="Microsoft.Testing.Platform.MSBuild",
                required=True,
                category="Microsoft Testing Platform",
                description="MSBuild integration"
            ),
            "Microsoft.Testing.Extensions.TrxReport": PackageRule(
                name="Microsoft.Testing.Extensions.TrxReport",
                required=True,
                category="Microsoft Testing Platform",
                description="TRX report extension"
            ),
            "Microsoft.Testing.Extensions.CodeCoverage": PackageRule(
                name="Microsoft.Testing.Extensions.CodeCoverage",
                required=True,
                category="Microsoft Testing Platform",
                description="Code coverage extension"
            ),
            "Microsoft.Testing.Extensions.VSTestBridge": PackageRule(
                name="Microsoft.Testing.Extensions.VSTestBridge",
                required=True,
                category="Microsoft Testing Platform",
                description="VSTest bridge extension"
            ),
            
            # XUNIT V3 FRAMEWORK - Required (CRITICAL: These conflict with old xunit)
            "xunit.v3": PackageRule(
                name="xunit.v3",
                required=True,
                conflicts_with=["xunit"],
                category="xUnit v3",
                description="XUnit v3 framework"
            ),
            "xunit.v3.core": PackageRule(
                name="xunit.v3.core",
                required=True,
                conflicts_with=["xunit.core"],
                category="xUnit v3",
                description="XUnit v3 core"
            ),
            "xunit.runner.visualstudio": PackageRule(
                name="xunit.runner.visualstudio",
                required=True,
                category="xUnit v3",
                description="Visual Studio runner (compatible with both v2/v3)"
            ),
            "xunit.v3.runner.inproc.console": PackageRule(
                name="xunit.v3.runner.inproc.console",
                required=True,
                category="xUnit v3",
                description="In-process console runner"
            ),
            "xunit.v3.runner.msbuild": PackageRule(
                name="xunit.v3.runner.msbuild",
                required=True,
                category="xUnit v3",
                description="MSBuild runner"
            ),
            
            # RESULT AND RESULT ANALYZERS - Required
            "IndQuestResults": PackageRule(
                name="IndQuestResults",
                required=True,
                category="Result and Result Analyzers",
                description="Result pattern implementation"
            ),
            
            # CONFLICTING PACKAGES (Old XUnit - Must be replaced)
            "xunit": PackageRule(
                name="xunit",
                required=False,
                conflicts_with=["xunit.v3"],
                category="Conflicting (Old XUnit)",
                description="OLD XUnit v2 - MUST be replaced with xunit.v3"
            ),
            "xunit.core": PackageRule(
                name="xunit.core", 
                required=False,
                conflicts_with=["xunit.v3.core"],
                category="Conflicting (Old XUnit)",
                description="OLD XUnit v2 core - MUST be replaced with xunit.v3.core"
            ),
            
            # DEPRECATED LOGGING - Must be replaced
            "Meziantou.Extensions.Logging.Xunit": PackageRule(
                name="Meziantou.Extensions.Logging.Xunit",
                required=False,
                conflicts_with=["Meziantou.Extensions.Logging.Xunit.v3"],
                category="Conflicting (Old Logging)",
                description="OLD XUnit v2 logging - MUST be replaced with v3"
            ),
        }
        
        return packages
    
    def _build_conflict_rules(self) -> Dict[str, List[str]]:
        """Build conflict resolution rules"""
        return {
            # XUnit version conflicts
            "xunit": ["xunit.v3"],
            "xunit.core": ["xunit.v3.core"],
            "xunit.v3": ["xunit"],
            "xunit.v3.core": ["xunit.core"],
            
            # Logging conflicts
            "Meziantou.Extensions.Logging.Xunit": ["Meziantou.Extensions.Logging.Xunit.v3"],
            "Meziantou.Extensions.Logging.Xunit.v3": ["Meziantou.Extensions.Logging.Xunit"],
        }
    
    def analyze_project_packages(self, project_packages: List[str]) -> Dict[str, any]:
        """Analyze project packages against standard with smart conflict detection"""
        
        analysis = {
            "exact_matches": [],
            "missing_required": [],
            "conflicting_packages": [],
            "unknown_packages": [],
            "additional_packages": [],
            "compatibility_score": 0.0,
            "critical_issues": [],
            "recommendations": []
        }
        
        project_set = set(pkg.lower() for pkg in project_packages)
        standard_set = set(pkg.lower() for pkg in self.standard_packages.keys())
        
        # Check each standard package
        for pkg_name, rule in self.standard_packages.items():
            pkg_lower = pkg_name.lower()
            
            if pkg_lower in project_set:
                analysis["exact_matches"].append(pkg_name)
            elif rule.required:
                analysis["missing_required"].append(pkg_name)
                
        # Check for conflicts - Enhanced detection
        for project_pkg in project_packages:
            pkg_lower = project_pkg.lower()
            
            # Check if this is a known package (case-insensitive lookup)
            matching_standard = None
            for std_pkg, rule in self.standard_packages.items():
                if std_pkg.lower() == pkg_lower:
                    matching_standard = (std_pkg, rule)
                    break
            
            if matching_standard:
                std_pkg, rule = matching_standard
                
                # Check if this package conflicts with others in the project
                for conflict in rule.conflicts_with:
                    if conflict.lower() in project_set:
                        analysis["conflicting_packages"].append({
                            "package": project_pkg,
                            "conflicts_with": conflict,
                            "action": f"Replace {conflict} with {project_pkg}"
                        })
                        analysis["critical_issues"].append(
                            f"CRITICAL: {conflict} conflicts with {project_pkg} - must replace old package"
                        )
                
                # CRITICAL: Check for xunit vs xunit.v3 conflicts specifically
                if pkg_lower == "xunit":
                    # This is the old xunit - flag as critical conflict
                    analysis["conflicting_packages"].append({
                        "package": project_pkg,
                        "conflicts_with": "xunit.v3",
                        "action": f"Replace {project_pkg} with xunit.v3"
                    })
                    analysis["critical_issues"].append(
                        f"🚨 CRITICAL CONFLICT: {project_pkg} is OLD XUnit v2 - MUST replace with xunit.v3"
                    )
                
                if pkg_lower == "xunit.core":
                    analysis["conflicting_packages"].append({
                        "package": project_pkg,
                        "conflicts_with": "xunit.v3.core", 
                        "action": f"Replace {project_pkg} with xunit.v3.core"
                    })
                    analysis["critical_issues"].append(
                        f"🚨 CRITICAL CONFLICT: {project_pkg} is OLD XUnit v2 - MUST replace with xunit.v3.core"
                    )
                
                if pkg_lower == "meziantou.extensions.logging.xunit":
                    analysis["conflicting_packages"].append({
                        "package": project_pkg,
                        "conflicts_with": "meziantou.extensions.logging.xunit.v3",
                        "action": f"Replace {project_pkg} with Meziantou.Extensions.Logging.Xunit.v3"
                    })
                    analysis["critical_issues"].append(
                        f"🚨 CRITICAL CONFLICT: {project_pkg} is OLD XUnit v2 logging - MUST replace with v3"
                    )
            else:
                # Unknown package - needs investigation
                analysis["unknown_packages"].append(project_pkg)
        
        # Calculate compatibility score
        total_required = len([r for r in self.standard_packages.values() if r.required])
        matched_required = len([m for m in analysis["exact_matches"] 
                               if self.standard_packages.get(m, PackageRule("", False)).required])
        
        analysis["compatibility_score"] = (matched_required / total_required) * 100 if total_required > 0 else 0
        
        # Generate recommendations
        self._generate_recommendations(analysis)
        
        return analysis
    
    def _generate_recommendations(self, analysis: Dict[str, any]):
        """Generate actionable recommendations"""
        
        # Critical conflicts first
        if analysis["conflicting_packages"]:
            analysis["recommendations"].append("🚨 CRITICAL: Resolve package conflicts immediately")
            for conflict in analysis["conflicting_packages"]:
                analysis["recommendations"].append(f"   → {conflict['action']}")
        
        # Missing required packages
        if analysis["missing_required"]:
            analysis["recommendations"].append("📦 ADD MISSING REQUIRED PACKAGES:")
            for pkg in analysis["missing_required"]:
                rule = self.standard_packages[pkg]
                analysis["recommendations"].append(f"   → {pkg} ({rule.category})")
        
        # Unknown packages need review
        if analysis["unknown_packages"]:
            analysis["recommendations"].append("🔍 REVIEW UNKNOWN PACKAGES:")
            for pkg in analysis["unknown_packages"]:
                analysis["recommendations"].append(f"   → {pkg} (verify if needed or conflicting)")
    
    def extract_packages_from_csproj(self, csproj_content: str) -> List[str]:
        """Extract package references from csproj content"""
        packages = []
        
        # Pattern to match PackageReference Include
        pattern = r'<PackageReference\s+Include="([^"]+)"'
        matches = re.findall(pattern, csproj_content, re.IGNORECASE)
        
        packages.extend(matches)
        
        return packages
    
    def generate_standardized_packages_xml(self) -> str:
        """Generate standardized packages XML for csproj"""
        
        xml_parts = []
        
        # Group by category
        categories = {}
        for pkg_name, rule in self.standard_packages.items():
            if rule.required:
                if rule.category not in categories:
                    categories[rule.category] = []
                categories[rule.category].append((pkg_name, rule))
        
        # Generate XML for each category
        for category, packages in categories.items():
            xml_parts.append(f'<!-- {category.upper()} -->')
            xml_parts.append(f'<ItemGroup Label="{category}">')
            
            for pkg_name, rule in packages:
                if pkg_name == "NSubstitute.Analyzers.CSharp":
                    xml_parts.append(f'  <PackageReference Include="{pkg_name}">')
                    xml_parts.append('    <PrivateAssets>all</PrivateAssets>')
                    xml_parts.append('    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>')
                    xml_parts.append('  </PackageReference>')
                elif pkg_name == "coverlet.collector":
                    xml_parts.append(f'  <PackageReference Include="{pkg_name}">')
                    xml_parts.append('    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>')
                    xml_parts.append('    <PrivateAssets>all</PrivateAssets>')
                    xml_parts.append('  </PackageReference>')
                elif "runner" in pkg_name:
                    xml_parts.append(f'  <PackageReference Include="{pkg_name}">')
                    xml_parts.append('    <PrivateAssets>all</PrivateAssets>')
                    xml_parts.append('    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>')
                    xml_parts.append('  </PackageReference>')
                else:
                    xml_parts.append(f'  <PackageReference Include="{pkg_name}" />')
            
            xml_parts.append('</ItemGroup>')
            xml_parts.append('')
        
        return '\n'.join(xml_parts)

def main():
    """Test the analyzer with sample data"""
    analyzer = ProjectStandardizationAnalyzer()
    
    # Test cases from our 3 projects
    test_cases = {
        "ExxerAI.Domain.Cortex.Test": [
            "xunit", "xunit.runner.visualstudio", "Microsoft.NET.Test.Sdk",
            "Microsoft.Testing.Platform.MSBuild", "Microsoft.Testing.Extensions.TrxReport",
            "Shouldly", "NSubstitute", "Meziantou.Extensions.Logging.Xunit",
            "Microsoft.Extensions.Configuration", "Microsoft.Extensions.DependencyInjection"
        ],
        "ExxerAI.Application.Core.Test": [
            "xunit", "xunit.runner.visualstudio", "Microsoft.NET.Test.Sdk",
            "Microsoft.Testing.Platform.MSBuild", "Microsoft.Testing.Extensions.TrxReport",
            "Shouldly", "NSubstitute", "Meziantou.Extensions.Logging.Xunit.v3"
        ],
        "ExxerAI.Integration.Test": [
            # This project has no explicit packages (relies on Directory.Build.props)
        ]
    }
    
    print("🔍 ExxerAI Project Standardization Analysis")
    print("=" * 50)
    
    for project_name, packages in test_cases.items():
        print(f"\n📋 {project_name}")
        print("-" * 40)
        
        analysis = analyzer.analyze_project_packages(packages)
        
        print(f"Compatibility Score: {analysis['compatibility_score']:.1f}%")
        print(f"Exact Matches: {len(analysis['exact_matches'])}")
        print(f"Missing Required: {len(analysis['missing_required'])}")
        print(f"Conflicting: {len(analysis['conflicting_packages'])}")
        print(f"Unknown: {len(analysis['unknown_packages'])}")
        
        if analysis['critical_issues']:
            print("\n🚨 CRITICAL ISSUES:")
            for issue in analysis['critical_issues']:
                print(f"  {issue}")
        
        if analysis['recommendations']:
            print("\n💡 RECOMMENDATIONS:")
            for rec in analysis['recommendations']:
                print(f"  {rec}")
    
    print(f"\n📦 STANDARD PACKAGES XML TEMPLATE:")
    print("=" * 50)
    print(analyzer.generate_standardized_packages_xml())

if __name__ == "__main__":
    main()