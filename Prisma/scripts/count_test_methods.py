#!/usr/bin/env python3
"""
Count test methods across all test projects.
Provides detailed breakdown by project and file.

Author: Claude Code Agent
Date: 2025-01-15
"""

import os
import re
import json
from pathlib import Path
from typing import Dict, Set, List
from collections import defaultdict
from datetime import datetime


class TestMethodCounter:
    """Counts test methods across test projects."""
    
    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.src_path = self.base_path / "Prisma" / "Code" / "Src" / "CSharp"
        
        # Test method patterns - match both Fact and Theory attributes
        self.fact_pattern = r'\[Fact[^\]]*\]'
        self.theory_pattern = r'\[Theory[^\]]*\]'
        # Match test methods with Fact or Theory attributes (handles various method signatures)
        self.test_method_pattern = r'\[(?:Fact|Theory)[^\]]*\]\s*(?:public\s+)?(?:async\s+)?(?:Task\s+)?(?:void\s+)?(\w+)\s*\('
        # Also match test methods that might have attributes on separate lines
        self.test_method_multiline_pattern = r'\[(?:Fact|Theory)[^\]]*\]\s*(?:.*\n)*?\s*(?:public\s+)?(?:async\s+)?(?:Task\s+)?(?:void\s+)?(\w+)\s*\('
        
        # Results storage
        self.project_counts = defaultdict(lambda: {"files": 0, "methods": 0, "file_details": []})
        
    def count_test_methods(self, file_path: Path) -> int:
        """Count test methods in a C# test file."""
        try:
            content = file_path.read_text(encoding='utf-8')
            
            # Skip Python interop tests (being dropped)
            if 'PythonInteropServiceTests' in file_path.name or 'PrismaOcrWrapperAdapter' in content:
                return 0
            
            # Find all test methods - try both patterns
            matches = list(re.finditer(self.test_method_pattern, content, re.MULTILINE | re.DOTALL))
            matches2 = list(re.finditer(self.test_method_multiline_pattern, content, re.MULTILINE | re.DOTALL))
            
            # Combine and deduplicate by method name
            method_names = set()
            for match in matches:
                method_names.add(match.group(1))
            for match in matches2:
                method_names.add(match.group(1))
            
            return len(method_names)
        except Exception as e:
            print(f"Error reading {file_path}: {e}")
            return 0
    
    def scan_project(self, project_path: Path, project_name: str):
        """Scan a test project and count test methods."""
        if not project_path.exists():
            return
        
        file_count = 0
        method_count = 0
        file_details = []
        
        for cs_file in project_path.rglob("*.cs"):
            # Skip bin/obj directories
            if any(part in ['bin', 'obj', '.vs', '.git'] for part in cs_file.parts):
                continue
                
            try:
                content = cs_file.read_text(encoding='utf-8')
                if re.search(self.fact_pattern, content) or re.search(self.theory_pattern, content):
                    methods = self.count_test_methods(cs_file)
                    if methods > 0:
                        file_count += 1
                        method_count += methods
                        rel_path = cs_file.relative_to(self.src_path)
                        file_details.append({
                            "file": str(rel_path).replace('\\', '/'),
                            "methods": methods
                        })
            except Exception as e:
                print(f"Error scanning {cs_file}: {e}")
        
        self.project_counts[project_name] = {
            "files": file_count,
            "methods": method_count,
            "file_details": sorted(file_details, key=lambda x: x["file"])
        }
    
    def analyze(self):
        """Run the analysis."""
        print("=" * 80)
        print("Test Method Count Analysis")
        print("=" * 80)
        
        # Scan all test projects
        projects = [
            ("ExxerCube.Prisma.Tests", self.src_path / "Tests"),
            ("Tests.Application", self.src_path / "Tests.Application"),
            ("Tests.Domain", self.src_path / "Tests.Domain"),
            ("Tests.Domain.Interfaces", self.src_path / "Tests.Domain.Interfaces"),
            ("Tests.Infrastructure.Extraction", self.src_path / "Tests.Infrastructure.Extraction"),
            ("Tests.Infrastructure.Classification", self.src_path / "Tests.Infrastructure.Classification"),
            ("Tests.Infrastructure.Database", self.src_path / "Tests.Infrastructure.Database"),
            ("Tests.Infrastructure.Export", self.src_path / "Tests.Infrastructure.Export"),
            ("Tests.Infrastructure.FileSystem", self.src_path / "Tests.Infrastructure.FileSystem"),
            ("Tests.Infrastructure.FileStorage", self.src_path / "Tests.Infrastructure.FileStorage"),
            ("Tests.Infrastructure.Python", self.src_path / "Tests.Infrastructure.Python"),
            ("Tests.System", self.src_path / "Tests.System"),
            ("Tests.EndToEnd", self.src_path / "Tests.EndToEnd"),
            ("Tests.Architecture", self.src_path / "Tests.Architecture"),
            ("Tests.UI", self.src_path / "Tests.UI"),
        ]
        
        print(f"\nScanning test projects:")
        for project_name, project_path in projects:
            if project_path.exists():
                self.scan_project(project_path, project_name)
                counts = self.project_counts[project_name]
                print(f"  {project_name}: {counts['methods']} methods in {counts['files']} files")
            else:
                print(f"  {project_name}: Project not found")
        
        # Calculate totals
        original_count = self.project_counts.get("ExxerCube.Prisma.Tests", {}).get("methods", 0)
        split_projects = {k: v for k, v in self.project_counts.items() if k != "ExxerCube.Prisma.Tests"}
        split_total = sum(counts["methods"] for counts in split_projects.values())
        total_files = sum(counts["files"] for counts in self.project_counts.values())
        total_methods = sum(counts["methods"] for counts in self.project_counts.values())
        
        # Generate report
        report = {
            "analysis_date": datetime.now().isoformat(),
            "projects": dict(self.project_counts),
            "summary": {
                "original_project_methods": original_count,
                "split_projects_methods": split_total,
                "total_files": total_files,
                "total_methods": total_methods,
                "project_count": len(self.project_counts)
            }
        }
        
        # Save report
        output_file = self.base_path / "Prisma" / "scripts" / "test_method_counts.json"
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2, ensure_ascii=False)
        
        print(f"\n" + "=" * 80)
        print("SUMMARY")
        print("=" * 80)
        print(f"Original Tests project: {original_count} methods")
        print(f"Split test projects: {split_total} methods")
        print(f"Total across all projects: {total_methods} methods")
        print(f"Total test files: {total_files}")
        print(f"\nReport saved to: {output_file}")
        
        return report


def main():
    import argparse
    
    parser = argparse.ArgumentParser(description="Count test methods across all test projects")
    parser.add_argument("--base-path", type=str, default=".", help="Base path to ExxerCube.Prisma repository")
    parser.add_argument("--output", type=str, default=None, help="Output JSON file path (default: scripts/test_method_counts.json)")
    
    args = parser.parse_args()
    
    counter = TestMethodCounter(args.base_path)
    report = counter.analyze()
    
    if args.output:
        output_path = Path(args.output)
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2, ensure_ascii=False)
        print(f"\nReport also saved to: {output_path}")


if __name__ == "__main__":
    main()

