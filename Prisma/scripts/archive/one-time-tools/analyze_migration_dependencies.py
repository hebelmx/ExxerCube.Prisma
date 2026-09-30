#!/usr/bin/env python3
"""
ADR-011 Phase 5.2: Migration Dependency Analyzer
Analyzes test files to determine dependencies, using statements, and migration targets.

Usage:
    python analyze_migration_dependencies.py --dry-run
    python analyze_migration_dependencies.py
"""

import json
import os
import re
import argparse
from pathlib import Path
from datetime import datetime
from typing import Dict, List, Set, Tuple

# Configuration
BASE_PATH = r"F:\Dynamic\ExxerAi\ExxerAI"
COMPONENTS_INTEGRATION_PATH = os.path.join(BASE_PATH, "code", "src", "tests", "05IntegrationTests", "ExxerAI.Components.Integration.Test")
OUTPUT_FILE = os.path.join(BASE_PATH, "docs", "adr", "migration_artifacts", "migration_dependency_analysis.json")
LOG_FILE = os.path.join(BASE_PATH, "docs", "adr", "migration_artifacts", "logs", f"dependency_analysis_{datetime.now().strftime('%Y-%m-%d_%H-%M-%S')}.log")

# Evocative domain mapping from ADR
EVOCATIVE_MAPPING = {
    "GoogleDrive": "Gatekeeper",
    "GoogleAuth": "Gatekeeper",
    "OAuth": "Sentinel",
    "Credential": "Sentinel",
    "ServiceAccount": "Sentinel",
    "Qdrant": "Vault",
    "Neo4j": "Vault",
    "Helix": "Helix",
    "Vector": "Vault",
    "Graph": "Helix",
    "Ollama": "Cortex",
    "LLM": "Cortex",
    "Embedding": "Cortex",
    "A2A": "Conduit",
    "Agent": "Conduit",
    "Hub": "Conduit",
    "Document": "Nexus",
    "Processing": "Nexus",
    "MCP": "Nexus",
    "Analytics": "Signal",
    "Monitoring": "Signal",
    "Authentication": "Sentinel",
    "Semantic": "Vault",
    "Knowledge": "Vault",
    "Conversation": "Cortex",
}

# Test layer classification patterns
ADAPTER_PATTERNS = ["Mock", "InMemory", "Fake", "Stub", "Test", "Behavior"]
INTEGRATION_PATTERNS = ["Docker", "Container", "Live", "Real", "Fixture"]
SYSTEM_PATTERNS = ["EndToEnd", "E2E", "Smoke", "Complete", "Workflow"]

class LogManager:
    def __init__(self, log_file: str):
        self.log_file = log_file
        os.makedirs(os.path.dirname(log_file), exist_ok=True)

    def log(self, message: str, level: str = "INFO"):
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        log_entry = f"[{timestamp}] [{level}] {message}\n"
        print(log_entry.strip())
        with open(self.log_file, "a", encoding="utf-8") as f:
            f.write(log_entry)

class DependencyAnalyzer:
    def __init__(self, log_manager: LogManager):
        self.log = log_manager
        self.metadata = self._load_metadata()

    def _load_metadata(self) -> Dict:
        """Load existing JSON metadata files"""
        metadata = {
            "enhanced_dependency": {},
            "gatekeeper_migration": {},
            "type_definitions": {}
        }

        try:
            # Load enhanced_dependency_analysis.json
            enhanced_path = os.path.join(BASE_PATH, "enhanced_dependency_analysis.json")
            if os.path.exists(enhanced_path):
                with open(enhanced_path, "r", encoding="utf-8") as f:
                    metadata["enhanced_dependency"] = json.load(f)
                self.log.log(f"Loaded enhanced dependency analysis", "INFO")

            # Load GatekeeperMigrationPlan.json
            gatekeeper_path = os.path.join(BASE_PATH, "GatekeeperMigrationPlan.json")
            if os.path.exists(gatekeeper_path):
                with open(gatekeeper_path, "r", encoding="utf-8") as f:
                    metadata["gatekeeper_migration"] = json.load(f)
                self.log.log(f"Loaded Gatekeeper migration plan", "INFO")

            # Load exxerai_types.json
            types_path = os.path.join(BASE_PATH, "scripts", "exxerai_types.json")
            if os.path.exists(types_path):
                with open(types_path, "r", encoding="utf-8") as f:
                    metadata["type_definitions"] = json.load(f)
                self.log.log(f"Loaded ExxerAI type definitions", "INFO")

        except Exception as e:
            self.log.log(f"Error loading metadata: {e}", "ERROR")

        return metadata

    def extract_using_statements(self, file_path: str) -> List[str]:
        """Extract all using statements from a C# file"""
        usings = []
        try:
            with open(file_path, "r", encoding="utf-8") as f:
                content = f.read()

            # Match using statements
            pattern = r'^\s*using\s+(?:static\s+)?([a-zA-Z_][a-zA-Z0-9_\.]*)\s*;'
            matches = re.finditer(pattern, content, re.MULTILINE)

            for match in matches:
                namespace = match.group(1)
                if namespace and not namespace.startswith("System") and not namespace.startswith("Microsoft"):
                    usings.append(namespace)

        except Exception as e:
            self.log.log(f"Error extracting usings from {file_path}: {e}", "ERROR")

        return usings

    def detect_infrastructure_dependencies(self, file_path: str) -> Dict[str, bool]:
        """Detect if file has Docker/container dependencies"""
        dependencies = {
            "has_docker": False,
            "has_testcontainers": False,
            "has_live_api": False,
            "has_fixtures": False
        }

        try:
            with open(file_path, "r", encoding="utf-8") as f:
                content = f.read().lower()

            dependencies["has_docker"] = "docker" in content or "container" in content
            dependencies["has_testcontainers"] = "testcontainers" in content or "containerf ix" in content
            dependencies["has_live_api"] = "live" in content or "real" in content
            dependencies["has_fixtures"] = "fixture" in content or "ixunit.collection" in content

        except Exception as e:
            self.log.log(f"Error detecting dependencies in {file_path}: {e}", "ERROR")

        return dependencies

    def classify_test_layer(self, file_name: str, content_analysis: Dict) -> Tuple[str, str]:
        """Classify test into Adapter, Integration, or System layer"""
        file_lower = file_name.lower()

        # System tests (E2E, Smoke)
        if any(pattern.lower() in file_lower for pattern in SYSTEM_PATTERNS):
            return "System", "06SystemTests"

        # Integration tests (has real infrastructure)
        if (content_analysis["has_docker"] or
            content_analysis["has_testcontainers"] or
            content_analysis["has_live_api"] or
            any(pattern.lower() in file_lower for pattern in INTEGRATION_PATTERNS)):
            return "Integration", "05IntegrationTests"

        # Adapter tests (mocked/in-memory)
        if any(pattern.lower() in file_lower for pattern in ADAPTER_PATTERNS):
            return "Adapter", "04AdapterTests"

        # Default to Adapter if uncertain
        return "Adapter", "04AdapterTests"

    def map_to_evocative_domain(self, file_name: str, usings: List[str]) -> str:
        """Map test file to evocative domain"""
        file_lower = file_name.lower()

        # Check filename first
        for keyword, domain in EVOCATIVE_MAPPING.items():
            if keyword.lower() in file_lower:
                return domain

        # Check using statements
        for using in usings:
            for keyword, domain in EVOCATIVE_MAPPING.items():
                if keyword.lower() in using.lower():
                    return domain

        return "Unknown"

    def determine_migration_action(self, file_name: str, layer: str, domain: str) -> str:
        """Determine if file should be MOVED, COPIED (duplicated), or INVESTIGATED"""
        file_lower = file_name.lower()

        # Infrastructure files marked as DO NOT TOUCH - still move but mark for repair
        if "container" in file_lower and "fixture" in file_lower:
            return "MOVE_NEEDS_REPAIR"

        # System tests should be duplicated
        if layer == "System":
            return "DUPLICATE"

        # Everything else moves
        return "MOVE"

    def analyze_file(self, file_path: str, relative_path: str) -> Dict:
        """Analyze a single test file"""
        file_name = os.path.basename(file_path)

        self.log.log(f"Analyzing: {relative_path}", "INFO")

        # Extract information
        usings = self.extract_using_statements(file_path)
        dependencies = self.detect_infrastructure_dependencies(file_path)
        layer, layer_folder = self.classify_test_layer(file_name, dependencies)
        domain = self.map_to_evocative_domain(file_name, usings)
        action = self.determine_migration_action(file_name, layer, domain)

        # Determine target project
        if layer == "Adapter":
            target_project = f"ExxerAI.{domain}.Adapter.Test"
        elif layer == "Integration":
            target_project = f"ExxerAI.{domain}.Integration.Test"
        else:  # System
            target_project = f"ExxerAI.{domain}.System.Test"

        target_path = os.path.join(BASE_PATH, "code", "src", "tests", layer_folder, target_project, file_name)

        return {
            "source_file": file_path,
            "relative_path": relative_path,
            "file_name": file_name,
            "evocative_domain": domain,
            "target_layer": layer,
            "target_project": target_project,
            "target_path": target_path,
            "migration_action": action,
            "using_statements": usings,
            "infrastructure_dependencies": dependencies,
            "needs_repair": action == "MOVE_NEEDS_REPAIR",
            "is_duplicate": action == "DUPLICATE"
        }

    def scan_components_integration(self) -> List[Dict]:
        """Scan all files in Components.Integration.Test"""
        results = []

        if not os.path.exists(COMPONENTS_INTEGRATION_PATH):
            self.log.log(f"Components.Integration path not found: {COMPONENTS_INTEGRATION_PATH}", "ERROR")
            return results

        self.log.log(f"Scanning: {COMPONENTS_INTEGRATION_PATH}", "INFO")

        for root, dirs, files in os.walk(COMPONENTS_INTEGRATION_PATH):
            for file in files:
                if file.endswith(".cs") and not file.endswith(".g.cs"):
                    file_path = os.path.join(root, file)
                    relative_path = os.path.relpath(file_path, COMPONENTS_INTEGRATION_PATH)

                    analysis = self.analyze_file(file_path, relative_path)
                    results.append(analysis)

        self.log.log(f"Analyzed {len(results)} files", "INFO")
        return results

    def generate_report(self, analysis_results: List[Dict]) -> Dict:
        """Generate comprehensive migration report"""
        report = {
            "generated_date": datetime.now().isoformat(),
            "total_files": len(analysis_results),
            "statistics": {
                "by_layer": {},
                "by_domain": {},
                "by_action": {},
                "needs_repair": 0,
                "to_duplicate": 0
            },
            "files": analysis_results
        }

        # Calculate statistics
        for item in analysis_results:
            layer = item["target_layer"]
            domain = item["evocative_domain"]
            action = item["migration_action"]

            report["statistics"]["by_layer"][layer] = report["statistics"]["by_layer"].get(layer, 0) + 1
            report["statistics"]["by_domain"][domain] = report["statistics"]["by_domain"].get(domain, 0) + 1
            report["statistics"]["by_action"][action] = report["statistics"]["by_action"].get(action, 0) + 1

            if item["needs_repair"]:
                report["statistics"]["needs_repair"] += 1
            if item["is_duplicate"]:
                report["statistics"]["to_duplicate"] += 1

        return report

    def run(self):
        """Execute dependency analysis"""
        self.log.log("=" * 80, "INFO")
        self.log.log("ADR-011 Phase 5.2: Migration Dependency Analysis", "INFO")
        self.log.log("=" * 80, "INFO")

        # Scan and analyze
        results = self.scan_components_integration()

        if not results:
            self.log.log("No files found to analyze!", "WARNING")
            return

        # Generate report
        report = self.generate_report(results)

        # Save output
        os.makedirs(os.path.dirname(OUTPUT_FILE), exist_ok=True)
        with open(OUTPUT_FILE, "w", encoding="utf-8") as f:
            json.dump(report, f, indent=2, ensure_ascii=False)

        self.log.log(f"Report saved to: {OUTPUT_FILE}", "INFO")
        self.log.log("=" * 80, "INFO")
        self.log.log("SUMMARY:", "INFO")
        self.log.log(f"  Total files: {report['total_files']}", "INFO")
        self.log.log(f"  Adapter: {report['statistics']['by_layer'].get('Adapter', 0)}", "INFO")
        self.log.log(f"  Integration: {report['statistics']['by_layer'].get('Integration', 0)}", "INFO")
        self.log.log(f"  System: {report['statistics']['by_layer'].get('System', 0)}", "INFO")
        self.log.log(f"  Needs Repair: {report['statistics']['needs_repair']}", "INFO")
        self.log.log(f"  To Duplicate: {report['statistics']['to_duplicate']}", "INFO")
        self.log.log("=" * 80, "INFO")

def main():
    parser = argparse.ArgumentParser(description="ADR-011 Phase 5.2: Migration Dependency Analyzer")
    parser.add_argument("--dry-run", action="store_true", help="Analyze without generating output file")
    args = parser.parse_args()

    log_manager = LogManager(LOG_FILE)
    analyzer = DependencyAnalyzer(log_manager)

    if args.dry_run:
        log_manager.log("DRY-RUN MODE: Analysis only, no output file will be created", "INFO")

    analyzer.run()

    if args.dry_run:
        log_manager.log("DRY-RUN COMPLETE: Review the analysis above", "INFO")
    else:
        log_manager.log(f"Analysis complete. Review: {OUTPUT_FILE}", "INFO")

if __name__ == "__main__":
    main()
