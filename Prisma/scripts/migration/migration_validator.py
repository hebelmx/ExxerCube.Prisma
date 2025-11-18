#!/usr/bin/env python3
"""
ExxerAI Infrastructure Migration Validator
Validates migration progress and identifies potential issues
"""

import os
import json
import subprocess
import re
from pathlib import Path
from typing import Dict, List, Set, Tuple
import logging

class MigrationValidator:
    """Validates infrastructure migration progress and health"""
    
    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.source_path = self.base_path / "code/src/Infrastructure/ExxerAI.Infrastructure"
        self.target_base = self.base_path / "code/src/Infrastructure"
        
        self.target_projects = [
            "ExxerAI.Data", "ExxerAI.AI", "ExxerAI.External", 
            "ExxerAI.Security", "ExxerAI.Storage", "ExxerAI.Messaging", 
            "ExxerAI.Processing"
        ]
        
        self.setup_logging()
    
    def setup_logging(self):
        """Setup logging configuration"""
        logging.basicConfig(
            level=logging.INFO,
            format='%(asctime)s - %(levelname)s - %(message)s'
        )
        self.logger = logging.getLogger(__name__)
    
    def validate_project_structure(self) -> Dict[str, bool]:
        """Validate that all target projects have proper structure"""
        results = {}
        
        for project in self.target_projects:
            project_path = self.target_base / project
            results[project] = {
                "exists": project_path.exists(),
                "has_csproj": (project_path / f"{project}.csproj").exists(),
                "has_directories": True,
                "structure_valid": False
            }
            
            if results[project]["exists"]:
                # Check for basic directory structure
                required_dirs = ["Interfaces", "Services", "Extensions"]
                has_dirs = all((project_path / d).exists() for d in required_dirs)
                results[project]["has_directories"] = has_dirs
                
                # Overall structure validation
                results[project]["structure_valid"] = (
                    results[project]["has_csproj"] and 
                    results[project]["has_directories"]
                )
        
        return results
    
    def analyze_circular_dependencies(self) -> Dict[str, List[str]]:
        """Analyze project references for circular dependencies"""
        dependencies = {}
        
        for project in self.target_projects:
            project_file = self.target_base / project / f"{project}.csproj"
            dependencies[project] = []
            
            if project_file.exists():
                with open(project_file, 'r', encoding='utf-8') as f:
                    content = f.read()
                
                # Extract project references
                pattern = r'<ProjectReference Include="[^"]*([^/\\]+)\.csproj"'
                matches = re.findall(pattern, content)
                dependencies[project] = [match for match in matches if match.startswith('ExxerAI')]
        
        # Check for circular dependencies
        circular_deps = {}
        for project, deps in dependencies.items():
            circular_deps[project] = []
            for dep in deps:
                if dep in dependencies and project in dependencies[dep]:
                    circular_deps[project].append(dep)
        
        return circular_deps
    
    def validate_namespace_consistency(self) -> Dict[str, Dict]:
        """Validate namespace consistency across projects"""
        results = {}
        
        for project in self.target_projects:
            project_path = self.target_base / project
            results[project] = {
                "total_files": 0,
                "correct_namespace": 0,
                "incorrect_namespace": 0,
                "mixed_namespaces": [],
                "consistency_score": 0.0
            }
            
            if not project_path.exists():
                continue
            
            expected_namespace = project.replace("ExxerAI.", "ExxerAI.")
            
            for cs_file in project_path.rglob("*.cs"):
                results[project]["total_files"] += 1
                
                try:
                    with open(cs_file, 'r', encoding='utf-8') as f:
                        content = f.read()
                    
                    # Find namespace declarations
                    namespace_pattern = r'namespace\s+([\w\.]+)'
                    namespaces = re.findall(namespace_pattern, content)
                    
                    if namespaces:
                        correct = any(ns.startswith(expected_namespace) for ns in namespaces)
                        if correct:
                            results[project]["correct_namespace"] += 1
                        else:
                            results[project]["incorrect_namespace"] += 1
                            results[project]["mixed_namespaces"].extend(namespaces)
                
                except Exception as e:
                    self.logger.warning(f"Could not analyze {cs_file}: {e}")
            
            # Calculate consistency score
            if results[project]["total_files"] > 0:
                results[project]["consistency_score"] = (
                    results[project]["correct_namespace"] / results[project]["total_files"]
                )
        
        return results
    
    def check_missing_dependencies(self) -> Dict[str, List[str]]:
        """Check for missing using statements and dependencies"""
        missing_deps = {}
        
        for project in self.target_projects:
            project_path = self.target_base / project
            missing_deps[project] = []
            
            if not project_path.exists():
                continue
            
            for cs_file in project_path.rglob("*.cs"):
                try:
                    with open(cs_file, 'r', encoding='utf-8') as f:
                        content = f.read()
                    
                    # Look for references to other ExxerAI projects without using statements
                    exxer_refs = re.findall(r'ExxerAI\.(\w+)', content)
                    using_statements = re.findall(r'using\s+ExxerAI\.(\w+)', content)
                    
                    # Find missing using statements
                    missing = set(exxer_refs) - set(using_statements) - {project.split('.')[-1]}
                    if missing:
                        missing_deps[project].extend(list(missing))
                
                except Exception as e:
                    self.logger.warning(f"Could not analyze {cs_file}: {e}")
        
        return missing_deps
    
    def validate_build_configuration(self) -> Dict[str, bool]:
        """Validate build configuration for all projects"""
        results = {}
        
        for project in self.target_projects:
            project_file = self.target_base / project / f"{project}.csproj"
            results[project] = {
                "compiles": False,
                "has_errors": False,
                "warnings_count": 0,
                "build_output": ""
            }
            
            if not project_file.exists():
                continue
            
            try:
                # Try to build the individual project
                result = subprocess.run(
                    ["dotnet", "build", str(project_file), "--no-restore", "--verbosity", "minimal"],
                    cwd=self.base_path,
                    capture_output=True,
                    text=True,
                    timeout=120
                )
                
                results[project]["compiles"] = result.returncode == 0
                results[project]["has_errors"] = result.returncode != 0
                results[project]["build_output"] = result.stderr if result.stderr else result.stdout
                
                # Count warnings
                if result.stdout:
                    warning_count = result.stdout.count("warning")
                    results[project]["warnings_count"] = warning_count
                
            except subprocess.TimeoutExpired:
                results[project]["has_errors"] = True
                results[project]["build_output"] = "Build timeout"
            except Exception as e:
                results[project]["has_errors"] = True
                results[project]["build_output"] = str(e)
        
        return results
    
    def generate_migration_health_report(self) -> Dict:
        """Generate comprehensive migration health report"""
        self.logger.info("Generating migration health report...")
        
        report = {
            "timestamp": str(Path.cwd()),
            "project_structure": self.validate_project_structure(),
            "circular_dependencies": self.analyze_circular_dependencies(),
            "namespace_consistency": self.validate_namespace_consistency(),
            "missing_dependencies": self.check_missing_dependencies(),
            "build_validation": self.validate_build_configuration()
        }
        
        # Calculate overall health score
        total_projects = len(self.target_projects)
        
        structure_score = sum(1 for p in report["project_structure"].values() if p["structure_valid"]) / total_projects
        
        circular_score = 1.0 - (len([p for p, deps in report["circular_dependencies"].items() if deps]) / total_projects)
        
        namespace_scores = [p["consistency_score"] for p in report["namespace_consistency"].values() if p["total_files"] > 0]
        namespace_score = sum(namespace_scores) / len(namespace_scores) if namespace_scores else 0.0
        
        build_score = sum(1 for p in report["build_validation"].values() if p["compiles"]) / total_projects
        
        report["health_metrics"] = {
            "structure_score": structure_score,
            "circular_dependency_score": circular_score,
            "namespace_consistency_score": namespace_score,
            "build_success_score": build_score,
            "overall_health": (structure_score + circular_score + namespace_score + build_score) / 4
        }
        
        return report
    
    def identify_critical_issues(self, report: Dict) -> List[Dict]:
        """Identify critical issues that need immediate attention"""
        issues = []
        
        # Structure issues
        for project, structure in report["project_structure"].items():
            if not structure["structure_valid"]:
                issues.append({
                    "severity": "HIGH",
                    "type": "Structure",
                    "project": project,
                    "description": f"Project {project} has invalid structure",
                    "action": "Create proper project structure and .csproj file"
                })
        
        # Circular dependencies
        for project, deps in report["circular_dependencies"].items():
            if deps:
                issues.append({
                    "severity": "CRITICAL",
                    "type": "Circular Dependency",
                    "project": project,
                    "description": f"Circular dependency with: {', '.join(deps)}",
                    "action": "Refactor to remove circular dependencies using interfaces"
                })
        
        # Build failures
        for project, build_info in report["build_validation"].items():
            if build_info["has_errors"]:
                issues.append({
                    "severity": "HIGH",
                    "type": "Build Failure",
                    "project": project,
                    "description": f"Project fails to compile",
                    "action": f"Fix build errors: {build_info['build_output'][:100]}..."
                })
        
        # Namespace inconsistencies
        for project, namespace_info in report["namespace_consistency"].items():
            if namespace_info["total_files"] > 0 and namespace_info["consistency_score"] < 0.8:
                issues.append({
                    "severity": "MEDIUM",
                    "type": "Namespace Inconsistency",
                    "project": project,
                    "description": f"Only {namespace_info['consistency_score']:.1%} of files have correct namespace",
                    "action": "Update namespace declarations in C# files"
                })
        
        return sorted(issues, key=lambda x: {"CRITICAL": 3, "HIGH": 2, "MEDIUM": 1, "LOW": 0}[x["severity"]], reverse=True)
    
    def generate_action_plan(self, issues: List[Dict]) -> List[Dict]:
        """Generate action plan to resolve identified issues"""
        action_plan = []
        
        # Group issues by type and priority
        issue_groups = {}
        for issue in issues:
            key = (issue["severity"], issue["type"])
            if key not in issue_groups:
                issue_groups[key] = []
            issue_groups[key].append(issue)
        
        for (severity, issue_type), grouped_issues in issue_groups.items():
            action_plan.append({
                "priority": severity,
                "category": issue_type,
                "affected_projects": [issue["project"] for issue in grouped_issues],
                "description": f"Resolve {issue_type.lower()} issues in {len(grouped_issues)} projects",
                "actions": list(set(issue["action"] for issue in grouped_issues)),
                "estimated_time": self.estimate_fix_time(issue_type, len(grouped_issues))
            })
        
        return action_plan
    
    def estimate_fix_time(self, issue_type: str, count: int) -> str:
        """Estimate time to fix issues"""
        time_per_issue = {
            "Structure": "30 minutes",
            "Circular Dependency": "2 hours", 
            "Build Failure": "1 hour",
            "Namespace Inconsistency": "15 minutes"
        }
        
        base_time = time_per_issue.get(issue_type, "1 hour")
        if count > 1:
            return f"{base_time} × {count} projects"
        return base_time


def main():
    """Main execution function"""
    import argparse
    
    parser = argparse.ArgumentParser(description="ExxerAI Infrastructure Migration Validator")
    parser.add_argument("--base-path", required=True, help="Base path to ExxerAI project")
    parser.add_argument("--output", help="Output file for validation report")
    
    args = parser.parse_args()
    
    validator = MigrationValidator(args.base_path)
    
    # Generate health report
    report = validator.generate_migration_health_report()
    
    # Identify issues
    issues = validator.identify_critical_issues(report)
    
    # Generate action plan
    action_plan = validator.generate_action_plan(issues)
    
    # Combine into final report
    final_report = {
        "health_report": report,
        "critical_issues": issues,
        "action_plan": action_plan,
        "summary": {
            "overall_health": report["health_metrics"]["overall_health"],
            "critical_issues_count": len([i for i in issues if i["severity"] == "CRITICAL"]),
            "high_issues_count": len([i for i in issues if i["severity"] == "HIGH"]),
            "migration_ready": report["health_metrics"]["overall_health"] > 0.8 and 
                             len([i for i in issues if i["severity"] in ["CRITICAL", "HIGH"]]) == 0
        }
    }
    
    # Output report
    if args.output:
        output_path = Path(args.output)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(final_report, f, indent=2)
        
        print(f"Validation report generated: {output_path}")
    else:
        print(json.dumps(final_report, indent=2))
    
    # Print summary
    print(f"\nMigration Health Summary:")
    print(f"Overall Health: {final_report['summary']['overall_health']:.1%}")
    print(f"Critical Issues: {final_report['summary']['critical_issues_count']}")
    print(f"High Priority Issues: {final_report['summary']['high_issues_count']}")
    print(f"Migration Ready: {'YES' if final_report['summary']['migration_ready'] else 'NO'}")


if __name__ == "__main__":
    main()