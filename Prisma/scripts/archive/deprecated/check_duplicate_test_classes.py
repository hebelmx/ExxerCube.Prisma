#!/usr/bin/env python3
"""
DUPLICATE TEST CLASS CHECKER
============================

Checks for duplicate test class names across all test projects before migration.
Prevents conflicts when moving test classes between projects.

CHECKS:
- Existing test classes with same names in target projects
- Target project directories that already exist
- Class name conflicts within migration plan
"""

import os
from pathlib import Path
from typing import Dict, List, Set, Tuple
from collections import defaultdict

class DuplicateTestClassChecker:
    """Checks for duplicate test classes before migration"""
    
    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.tests_path = self.base_path / "code/src/tests"
        
    def get_all_test_classes(self) -> Dict[str, List[Tuple[str, str]]]:
        """Get all test class names and their locations"""
        test_classes = defaultdict(list)
        
        # Scan all test directories
        for cs_file in self.tests_path.glob("**/*.cs"):
            if (cs_file.name.endswith(".cs") and 
                not cs_file.name.endswith(".g.cs") and 
                "obj" not in str(cs_file) and
                "bin" not in str(cs_file)):
                
                try:
                    content = cs_file.read_text(encoding='utf-8')
                    
                    # Extract class names
                    for line in content.split('\n'):
                        line = line.strip()
                        if (line.startswith('public class ') or 
                            line.startswith('public sealed class ') or
                            line.startswith('internal class ')):
                            
                            # Extract class name
                            parts = line.split()
                            class_idx = parts.index('class') + 1
                            if class_idx < len(parts):
                                class_name = parts[class_idx].split(':')[0].split('<')[0]
                                
                                # Get relative path from tests root
                                rel_path = cs_file.relative_to(self.tests_path)
                                project_path = str(rel_path.parts[0] + "/" + rel_path.parts[1])
                                
                                test_classes[class_name].append((str(rel_path), project_path))
                                
                except Exception as e:
                    print(f"⚠️  Error reading {cs_file}: {e}")
        
        return test_classes
    
    def check_migration_conflicts(self) -> Dict[str, any]:
        """Check for conflicts in our specific migration plan"""
        
        # Classes we plan to move/create
        cortex_migration = {
            "domain": ["TokenUsageTests"],  # Stay in Domain
            "application": ["LanguageModelTests", "LLMIntegrationTests"],  # Move to Application
            "infrastructure": ["LLMRequestTests", "LLMResponseTests", "TextAnalysisRequestTests", "ModelCapabilitiesTests"]  # Move to Infrastructure
        }
        
        # Target projects
        target_projects = {
            "01Application/ExxerAI.Application.Cortex.Test": "application",
            "02Infrastructure/ExxerAI.Infrastructure.Cortex.Test": "infrastructure", 
            "02Infrastructure/ExxerAI.Infrastructure.Nexus.Test": "nexus_move"
        }
        
        all_test_classes = self.get_all_test_classes()
        conflicts = {
            "duplicate_classes": {},
            "target_project_exists": {},
            "class_name_conflicts": [],
            "summary": {}
        }
        
        # Check for duplicate class names
        print("🔍 CHECKING FOR DUPLICATE TEST CLASSES...")
        
        # Check Cortex classes
        for layer, classes in cortex_migration.items():
            for class_name in classes:
                if class_name in all_test_classes:
                    locations = all_test_classes[class_name]
                    if len(locations) > 1:
                        conflicts["duplicate_classes"][class_name] = locations
                        print(f"❌ DUPLICATE: {class_name} found in {len(locations)} locations:")
                        for location, project in locations:
                            print(f"   - {project}: {location}")
                    else:
                        print(f"✅ UNIQUE: {class_name} - {locations[0][1]}")
        
        # Check if target projects already exist
        print(f"\n🎯 CHECKING TARGET PROJECTS...")
        for target_project in target_projects.keys():
            target_path = self.tests_path / target_project
            if target_path.exists():
                conflicts["target_project_exists"][target_project] = str(target_path)
                print(f"❌ EXISTS: {target_project}")
                
                # Check if it has conflicting classes
                existing_classes = []
                for cs_file in target_path.glob("**/*.cs"):
                    if (cs_file.name.endswith(".cs") and 
                        not cs_file.name.endswith(".g.cs") and 
                        "obj" not in str(cs_file)):
                        try:
                            content = cs_file.read_text(encoding='utf-8')
                            for line in content.split('\n'):
                                line = line.strip()
                                if 'public class ' in line:
                                    parts = line.split()
                                    if 'class' in parts:
                                        class_idx = parts.index('class') + 1
                                        if class_idx < len(parts):
                                            class_name = parts[class_idx].split(':')[0]
                                            existing_classes.append(class_name)
                        except:
                            pass
                
                if existing_classes:
                    print(f"   Contains classes: {existing_classes}")
            else:
                print(f"✅ SAFE: {target_project} - doesn't exist")
        
        # Check all classes for global duplicates
        print(f"\n📊 GLOBAL DUPLICATE ANALYSIS...")
        total_duplicates = 0
        for class_name, locations in all_test_classes.items():
            if len(locations) > 1:
                total_duplicates += 1
                conflicts["class_name_conflicts"].append({
                    "class_name": class_name,
                    "locations": locations,
                    "count": len(locations)
                })
        
        conflicts["summary"] = {
            "total_test_classes": len(all_test_classes),
            "duplicate_classes_found": len(conflicts["duplicate_classes"]),
            "global_duplicates": total_duplicates,
            "target_projects_existing": len(conflicts["target_project_exists"]),
            "migration_safe": len(conflicts["duplicate_classes"]) == 0 and len(conflicts["target_project_exists"]) == 0
        }
        
        return conflicts
    
    def check_nexus_conflicts(self) -> Dict[str, any]:
        """Check for conflicts when moving Nexus project"""
        
        source_path = self.tests_path / "00Domain/ExxerAI.Domain.Nexus.Test"
        target_path = self.tests_path / "02Infrastructure/ExxerAI.Infrastructure.Nexus.Test"
        
        conflicts = {
            "source_exists": source_path.exists(),
            "target_exists": target_path.exists(),
            "source_classes": [],
            "target_classes": [],
            "conflicts": []
        }
        
        print(f"\n⚡ NEXUS MIGRATION CONFLICTS...")
        print(f"Source: {source_path} - {'EXISTS' if conflicts['source_exists'] else 'MISSING'}")
        print(f"Target: {target_path} - {'EXISTS' if conflicts['target_exists'] else 'SAFE'}")
        
        # Get source classes
        if conflicts["source_exists"]:
            for cs_file in source_path.glob("**/*.cs"):
                if (cs_file.name.endswith("Tests.cs") and 
                    "obj" not in str(cs_file)):
                    class_name = cs_file.stem
                    conflicts["source_classes"].append(class_name)
        
        # Get target classes if target exists
        if conflicts["target_exists"]:
            for cs_file in target_path.glob("**/*.cs"):
                if (cs_file.name.endswith("Tests.cs") and 
                    "obj" not in str(cs_file)):
                    class_name = cs_file.stem
                    conflicts["target_classes"].append(class_name)
            
            # Find conflicts
            source_set = set(conflicts["source_classes"])
            target_set = set(conflicts["target_classes"])
            conflicts["conflicts"] = list(source_set.intersection(target_set))
            
            if conflicts["conflicts"]:
                print(f"❌ CLASS CONFLICTS: {conflicts['conflicts']}")
            else:
                print(f"✅ NO CLASS CONFLICTS")
        
        return conflicts
    
    def run_full_check(self) -> bool:
        """Run complete duplicate check analysis"""
        
        print("🔍 DUPLICATE TEST CLASS ANALYSIS")
        print("=" * 50)
        
        # Check migration conflicts
        migration_conflicts = self.check_migration_conflicts()
        
        # Check Nexus conflicts  
        nexus_conflicts = self.check_nexus_conflicts()
        
        # Print summary
        print(f"\n📋 ANALYSIS SUMMARY:")
        print(f"Total test classes: {migration_conflicts['summary']['total_test_classes']}")
        print(f"Migration-specific duplicates: {migration_conflicts['summary']['duplicate_classes_found']}")
        print(f"Global duplicates: {migration_conflicts['summary']['global_duplicates']}")
        print(f"Target projects existing: {migration_conflicts['summary']['target_projects_existing']}")
        print(f"Nexus target exists: {nexus_conflicts['target_exists']}")
        
        # Overall safety
        migration_safe = migration_conflicts['summary']['migration_safe']
        nexus_safe = not nexus_conflicts['target_exists'] or len(nexus_conflicts['conflicts']) == 0
        
        overall_safe = migration_safe and nexus_safe
        
        if overall_safe:
            print(f"\n✅ MIGRATION IS SAFE - No conflicts detected!")
        else:
            print(f"\n❌ MIGRATION HAS CONFLICTS - Review above issues!")
            
            if not migration_safe:
                print("   - Cortex migration has conflicts")
            if not nexus_safe:
                print("   - Nexus migration has conflicts")
        
        return overall_safe

def main():
    """Main execution"""
    import argparse
    
    parser = argparse.ArgumentParser(description="Check for duplicate test classes before migration")
    parser.add_argument("--base-path", required=True, help="Base path to ExxerAI project")
    
    args = parser.parse_args()
    
    checker = DuplicateTestClassChecker(args.base_path)
    success = checker.run_full_check()
    
    return 0 if success else 1

if __name__ == "__main__":
    exit(main())