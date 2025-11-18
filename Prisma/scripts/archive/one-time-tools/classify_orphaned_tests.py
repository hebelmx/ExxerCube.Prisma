#!/usr/bin/env python3
"""
Systematically Classify Orphaned Losetests Based on Evocative Architecture
===========================================================================

PURPOSE: Maps 20 orphaned test files to proper evocative infrastructure destinations
         following ADR-007 Rhomboid Testing Architecture principles.

ARCHITECTURE FOUNDATION (ADR-007):
- 🧠 Cortex: AI/LLM intelligent reasoning brain
- 🚪 Gatekeeper: External API integration guardian
- ⚡ Nexus: Document processing transformation
- 📡 Conduit: Messaging and agent communication
- 📊 Signal: Monitoring and system heartbeat
- 🌊 Datastream: Data flow and persistence foundation
- 🏛️ Vault: Vector/graph semantic memory storage
- 🛡️ Sentinel: Authentication and security protection

TEST LAYERS (Rhomboid):
- 03UnitTests: Fast feedback, in-memory, doubles
- 04AdapterTests: Adapter layer testing
- 05IntegrationTests: Live containers, happy paths
- 06SystemTests: E2E scenarios, real-world journeys

SAFETY: Read-only analysis - generates recommendations only.
"""

import json
from pathlib import Path
from typing import Dict, List
from dataclasses import dataclass, asdict
import argparse


@dataclass
class TestClassification:
    """Classification for an orphaned test file."""
    filename: str
    namespace: str
    evocative_domain: str
    test_layer: str
    destination_project: str
    rationale: str
    confidence: str  # 'high', 'medium', 'low'


class OrphanedTestClassifier:
    """Classifies orphaned tests based on evocative architecture."""

    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.losetests_dir = self.base_path / "code" / "src" / "Losetests"

        # Classification rules based on namespace, filename patterns, and domain knowledge
        self.classification_rules = self._build_classification_rules()

    def _build_classification_rules(self) -> List[Dict]:
        """
        Build systematic classification rules based on evocative architecture.

        Each rule contains:
        - Pattern: How to identify this test
        - Domain: Which evocative project it belongs to
        - Layer: Which test layer (Unit/Adapter/Integration/System)
        - Destination: Exact project name
        """
        return [
            # ===============================================================
            # 📡 CONDUIT - Agent Communication & Orchestration
            # ===============================================================
            {
                "pattern": "AgentSwarmManager",
                "namespace_contains": "Orchestration.Services",
                "evocative_domain": "Conduit",
                "test_layer": "Integration",
                "destination": "05IntegrationTests/ExxerAI.Conduit.Integration.Tests",
                "rationale": "Tests agent swarm orchestration with real event broadcasting - core Conduit responsibility",
                "confidence": "high"
            },
            {
                "pattern": "EIAAgentService",
                "namespace_contains": "Orchestration.Services",
                "evocative_domain": "Conduit",
                "test_layer": "Integration",
                "destination": "05IntegrationTests/ExxerAI.Conduit.Integration.Tests",
                "rationale": "Tests EIA agent service with repository patterns - agent lifecycle management in Conduit",
                "confidence": "high"
            },
            {
                "pattern": "HealthMonitor",
                "namespace_contains": "Orchestration.Services",
                "evocative_domain": "Conduit",
                "test_layer": "Integration",
                "destination": "05IntegrationTests/ExxerAI.Conduit.Integration.Tests",
                "rationale": "Tests agent health monitoring and message routing - Conduit communication responsibility",
                "confidence": "high"
            },

            # ===============================================================
            # 🚪 GATEKEEPER - External API Integrations
            # ===============================================================
            {
                "pattern": "GoogleAuthentication",
                "namespace_contains": "Adapters.GoogleDrive",
                "evocative_domain": "Gatekeeper",
                "test_layer": "Adapter",
                "destination": "04AdapterTests/ExxerAI.Gatekeeper.Adapter.Tests",
                "rationale": "Tests Google Drive authentication adapter - Gatekeeper guards external API access",
                "confidence": "high"
            },
            {
                "pattern": "GoogleDrivePerformance",
                "namespace_contains": "Adapters.GoogleDrive",
                "evocative_domain": "Gatekeeper",
                "test_layer": "Integration",
                "destination": "05IntegrationTests/ExxerAI.Gatekeeper.Integration.Tests",
                "rationale": "Performance tests for Google Drive integration - Gatekeeper external service testing",
                "confidence": "high"
            },
            {
                "pattern": "GoogleDriveStorage",
                "namespace_contains": "Adapters.GoogleDrive",
                "evocative_domain": "Gatekeeper",
                "test_layer": "Adapter",
                "destination": "04AdapterTests/ExxerAI.Gatekeeper.Adapter.Tests",
                "rationale": "Tests Google Drive storage adapter - Gatekeeper external storage integration",
                "confidence": "high"
            },

            # ===============================================================
            # ⚡ NEXUS - Document Processing & Transformation
            # ===============================================================
            {
                "pattern": "NPOIAdapter",
                "namespace_contains": "Adapters.DocumentProcessing",
                "evocative_domain": "Nexus",
                "test_layer": "Adapter",
                "destination": "04AdapterTests/ExxerAI.Nexus.Adapter.Tests",
                "rationale": "Tests NPOI adapter for Office document processing - Nexus document transformation",
                "confidence": "high"
            },
            {
                "pattern": "OpenXmlAdapter",
                "namespace_contains": "Adapters.DocumentProcessing",
                "evocative_domain": "Nexus",
                "test_layer": "Adapter",
                "destination": "04AdapterTests/ExxerAI.Nexus.Adapter.Tests",
                "rationale": "Tests OpenXML adapter for modern Office formats - Nexus document processing",
                "confidence": "high"
            },
            {
                "pattern": "PdfPigAdapter",
                "namespace_contains": "Adapters.DocumentProcessing",
                "evocative_domain": "Nexus",
                "test_layer": "Adapter",
                "destination": "04AdapterTests/ExxerAI.Nexus.Adapter.Tests",
                "rationale": "Tests PdfPig adapter for PDF processing - Nexus document extraction",
                "confidence": "high"
            },
            {
                "pattern": "OCRProcessing",
                "namespace_contains": "TechStack",
                "evocative_domain": "Nexus",
                "test_layer": "Integration",
                "destination": "05IntegrationTests/ExxerAI.Nexus.Integration.Tests",
                "rationale": "Tests OCR processing service with real workflows - Nexus document intelligence pipeline",
                "confidence": "high"
            },
            {
                "pattern": "OCRProcessingService",
                "namespace_contains": "TechStack",
                "evocative_domain": "Nexus",
                "test_layer": "Integration",
                "destination": "05IntegrationTests/ExxerAI.Nexus.Integration.Tests",
                "rationale": "Tests OCR service integration - Nexus text extraction from images",
                "confidence": "high"
            },
            {
                "pattern": "ImageProcessing",
                "namespace_contains": "Services",
                "evocative_domain": "Nexus",
                "test_layer": "Integration",
                "destination": "05IntegrationTests/ExxerAI.Nexus.Integration.Tests",
                "rationale": "Tests image processing service - Nexus visual document processing",
                "confidence": "high"
            },

            # ===============================================================
            # 📊 SIGNAL - Monitoring & System Health
            # ===============================================================
            {
                "pattern": "ServiceMonitoring",
                "namespace_contains": "Services",
                "evocative_domain": "Signal",
                "test_layer": "Integration",
                "destination": "05IntegrationTests/ExxerAI.Signal.Integration.Tests",
                "rationale": "Tests service monitoring functionality - Signal system heartbeat and observability",
                "confidence": "high"
            },

            # ===============================================================
            # 🎯 CUBEXPLORER - Business Domain (Specialized Application)
            # ===============================================================
            {
                "pattern": "ArtifactLoader",
                "namespace_contains": "CubeXplorer.Services",
                "evocative_domain": "CubeXplorer",
                "test_layer": "Application",
                "destination": "01Application/ExxerAI.Application.CubeXplorer.Tests",
                "rationale": "Tests CubeX artifact loading - business domain application logic",
                "confidence": "high"
            },
            {
                "pattern": "CubeXDocumentParser",
                "namespace_contains": "CubeXplorer.Services",
                "evocative_domain": "CubeXplorer",
                "test_layer": "Application",
                "destination": "01Application/ExxerAI.Application.CubeXplorer.Tests",
                "rationale": "Tests CubeX-specific document parsing - business domain logic",
                "confidence": "high"
            },
            {
                "pattern": "RuleParserEngine",
                "namespace_contains": "CubeXplorer.Services",
                "evocative_domain": "CubeXplorer",
                "test_layer": "Application",
                "destination": "01Application/ExxerAI.Application.CubeXplorer.Tests",
                "rationale": "Tests rule parsing engine for CubeX - business domain rule processing",
                "confidence": "high"
            },
            {
                "pattern": "TimbradoApiClient",
                "namespace_contains": "CubeXplorer.Services",
                "evocative_domain": "CubeXplorer",
                "test_layer": "Application",
                "destination": "01Application/ExxerAI.Application.CubeXplorer.Tests",
                "rationale": "Tests Timbrado API client for Mexican tax compliance - CubeX business requirement",
                "confidence": "high"
            },

            # ===============================================================
            # 🌊 DATASTREAM - Storage & Persistence (Fallback)
            # ===============================================================
            {
                "pattern": "FileSystemDocumentStorage",
                "namespace_contains": "Storage",
                "evocative_domain": "Datastream",
                "test_layer": "Integration",
                "destination": "05IntegrationTests/ExxerAI.Datastream.Integration.Tests",
                "rationale": "Tests file system storage service - Datastream persistence responsibility",
                "confidence": "medium"
            },

            # ===============================================================
            # 🔧 SPECIAL CASES
            # ===============================================================
            {
                "pattern": "SelfRegisteredExtensions",
                "namespace_contains": "Domain.Nexus",
                "evocative_domain": "Nexus",
                "test_layer": "Domain",
                "destination": "00Domain/ExxerAI.Domain.Nexus.Tests",
                "rationale": "Extension class for Nexus domain - belongs in domain layer",
                "confidence": "high"
            },
            {
                "pattern": "SmokeTestFixture",
                "namespace_contains": "Components.IntegrationTests",
                "evocative_domain": "Components",
                "test_layer": "Integration",
                "destination": "05IntegrationTests/ExxerAI.Components.Integration.Tests/EndToEnd",
                "rationale": "Smoke test fixture for component integration - already exists with 84.5% similarity",
                "confidence": "medium"
            },
        ]

    def classify_file(self, filename: str) -> TestClassification:
        """Classify a single test file based on pattern matching."""

        # Try to read the file to get namespace
        file_path = self.losetests_dir / filename
        namespace = ""

        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                for line in f:
                    if line.strip().startswith("namespace"):
                        namespace = line.strip().replace("namespace", "").replace(";", "").strip()
                        break
        except Exception as e:
            print(f"⚠️  Could not read {filename}: {e}")

        # Match against classification rules
        for rule in self.classification_rules:
            if rule["pattern"] in filename and rule["namespace_contains"] in namespace:
                return TestClassification(
                    filename=filename,
                    namespace=namespace,
                    evocative_domain=rule["evocative_domain"],
                    test_layer=rule["test_layer"],
                    destination_project=rule["destination"],
                    rationale=rule["rationale"],
                    confidence=rule["confidence"]
                )

        # No match found - return unknown
        return TestClassification(
            filename=filename,
            namespace=namespace,
            evocative_domain="Unknown",
            test_layer="Unknown",
            destination_project="NEEDS_MANUAL_REVIEW",
            rationale="No matching classification rule found",
            confidence="low"
        )

    def classify_all(self) -> List[TestClassification]:
        """Classify all files in Losetests directory."""
        classifications = []

        if not self.losetests_dir.exists():
            print(f"❌ ERROR: Losetests directory not found: {self.losetests_dir}")
            return classifications

        test_files = sorted([f.name for f in self.losetests_dir.iterdir() if f.is_file() and f.suffix == '.cs'])

        for filename in test_files:
            classification = self.classify_file(filename)
            classifications.append(classification)

        return classifications

    def generate_report(self, classifications: List[TestClassification]) -> Dict:
        """Generate comprehensive classification report."""

        # Group by evocative domain
        by_domain = {}
        for c in classifications:
            if c.evocative_domain not in by_domain:
                by_domain[c.evocative_domain] = []
            by_domain[c.evocative_domain].append(asdict(c))

        # Group by confidence
        high_confidence = [c for c in classifications if c.confidence == 'high']
        medium_confidence = [c for c in classifications if c.confidence == 'medium']
        low_confidence = [c for c in classifications if c.confidence == 'low']

        # Generate summary
        summary = {
            'total_files': len(classifications),
            'high_confidence': len(high_confidence),
            'medium_confidence': len(medium_confidence),
            'low_confidence': len(low_confidence),
            'by_domain_count': {domain: len(files) for domain, files in by_domain.items()},
            'by_test_layer_count': {}
        }

        # Count by test layer
        for c in classifications:
            layer = c.test_layer
            summary['by_test_layer_count'][layer] = summary['by_test_layer_count'].get(layer, 0) + 1

        return {
            'summary': summary,
            'by_domain': by_domain,
            'high_confidence': [asdict(c) for c in high_confidence],
            'medium_confidence': [asdict(c) for c in medium_confidence],
            'low_confidence': [asdict(c) for c in low_confidence],
            'all_classifications': [asdict(c) for c in classifications]
        }

    def print_report(self, report: Dict):
        """Print classification report to console."""
        summary = report['summary']

        print("\n" + "="*80)
        print("🎯 ORPHANED TEST CLASSIFICATION REPORT")
        print("   Based on ADR-007 Evocative Architecture & Rhomboid Testing")
        print("="*80)

        print(f"\n📊 SUMMARY:")
        print(f"   Total files:        {summary['total_files']}")
        print(f"   ✅ High confidence:  {summary['high_confidence']}")
        print(f"   ⚠️  Medium confidence: {summary['medium_confidence']}")
        print(f"   ❌ Low confidence:   {summary['low_confidence']}")

        print(f"\n🎭 BY EVOCATIVE DOMAIN:")
        for domain, count in sorted(summary['by_domain_count'].items()):
            icon = self._get_domain_icon(domain)
            print(f"   {icon} {domain:15} → {count:2} file(s)")

        print(f"\n🧪 BY TEST LAYER:")
        for layer, count in sorted(summary['by_test_layer_count'].items()):
            print(f"   {layer:15} → {count:2} file(s)")

        # Detailed classifications by domain
        print("\n" + "="*80)
        print("📋 DETAILED CLASSIFICATION BY EVOCATIVE DOMAIN")
        print("="*80)

        for domain, files in sorted(report['by_domain'].items()):
            icon = self._get_domain_icon(domain)
            print(f"\n{icon} {domain.upper()} ({len(files)} files)")
            print("-" * 80)

            for file_info in sorted(files, key=lambda x: x['filename']):
                confidence_icon = "✅" if file_info['confidence'] == 'high' else "⚠️" if file_info['confidence'] == 'medium' else "❌"
                print(f"\n   {confidence_icon} {file_info['filename']}")
                print(f"      Layer:       {file_info['test_layer']}")
                print(f"      Destination: {file_info['destination_project']}")
                print(f"      Rationale:   {file_info['rationale']}")

        print("\n" + "="*80)
        print("💡 NEXT STEPS")
        print("="*80)
        print(f"1. Review {summary['high_confidence']} high-confidence classifications (ready to relocate)")
        print(f"2. Validate {summary['medium_confidence']} medium-confidence classifications (minor adjustments)")
        print(f"3. Manually classify {summary['low_confidence']} low-confidence files")
        print("\n")

    def _get_domain_icon(self, domain: str) -> str:
        """Get emoji icon for evocative domain."""
        icons = {
            'Conduit': '📡',
            'Gatekeeper': '🚪',
            'Nexus': '⚡',
            'Signal': '📊',
            'Cortex': '🧠',
            'Vault': '🏛️',
            'Datastream': '🌊',
            'Sentinel': '🛡️',
            'CubeXplorer': '🎯',
            'Components': '🔧',
            'Unknown': '❓'
        }
        return icons.get(domain, '📄')

    def save_report(self, report: Dict, output_file: str):
        """Save classification report to JSON."""
        output_path = self.base_path / output_file

        try:
            with open(output_path, 'w', encoding='utf-8') as f:
                json.dump(report, f, indent=2)
            print(f"✅ Classification report saved to: {output_path}")
        except Exception as e:
            print(f"❌ Error saving report: {e}")


def main():
    parser = argparse.ArgumentParser(
        description='Classify orphaned Losetests based on evocative architecture',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python scripts/classify_orphaned_tests.py --base-path "F:/Dynamic/ExxerAi/ExxerAI"

This script analyzes test files and recommends proper destinations based on ADR-007.
        """
    )

    parser.add_argument(
        '--base-path',
        type=str,
        required=True,
        help='Base path to ExxerAI repository'
    )

    parser.add_argument(
        '--output',
        type=str,
        default='orphaned_tests_classification.json',
        help='Output JSON report filename'
    )

    args = parser.parse_args()

    # Run classification
    classifier = OrphanedTestClassifier(args.base_path)
    classifications = classifier.classify_all()
    report = classifier.generate_report(classifications)

    classifier.print_report(report)
    classifier.save_report(report, args.output)

    return 0


if __name__ == '__main__':
    exit(main())
