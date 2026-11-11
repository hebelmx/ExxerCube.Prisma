#!/usr/bin/env python3
"""
Classify tests in 05IntegrationTests as true Integration vs System tests
Based on ADR-011 definitions:
- Integration: 1 real Docker service + mocked dependencies
- System: Full Docker stack with real data OR real services
"""

import re
from pathlib import Path
from dataclasses import dataclass
from typing import List, Set


@dataclass
class TestFileAnalysis:
    """Analysis of a test file"""
    file_path: str
    project_name: str
    has_docker_fixture: bool
    has_mock_setup: bool
    has_real_data_setup: bool
    test_type: str  # 'integration', 'system', 'unknown'


def analyze_test_file(file_path: Path, project_root: Path) -> TestFileAnalysis:
    """Analyze a single test file to determine test type"""

    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
    except:
        return None

    project_name = file_path.relative_to(project_root).parts[0]

    # Look for indicators
    has_docker_fixture = bool(re.search(r'(ContainerFixture|DockerFixture|TestContainers)', content, re.IGNORECASE))
    has_mock_setup = bool(re.search(r'(Substitute\.For|NSubstitute|\.Returns\(|Mock<)', content))
    has_real_data_setup = bool(re.search(r'(SeedData|LoadTestData|RealData|ActualData)', content, re.IGNORECASE))

    # Classify based on ADR-011
    if has_docker_fixture and has_mock_setup:
        test_type = 'integration'  # Docker + mocks = Integration
    elif has_docker_fixture and has_real_data_setup:
        test_type = 'system'  # Docker + real data = System
    elif 'ContainerVerification' in content or 'HealthCheck' in content:
        test_type = 'system'  # Container verification is system-level
    elif 'EndToEnd' in str(file_path) or 'E2E' in str(file_path):
        test_type = 'system'  # E2E is always system
    else:
        test_type = 'unknown'

    return TestFileAnalysis(
        file_path=str(file_path.relative_to(project_root)),
        project_name=project_name,
        has_docker_fixture=has_docker_fixture,
        has_mock_setup=has_mock_setup,
        has_real_data_setup=has_real_data_setup,
        test_type=test_type
    )


def main():
    """Main execution"""
    print("🔍 Classifying Integration vs System Tests per ADR-011")
    print("=" * 100)
    print()

    integration_dir = Path("code/src/tests/05IntegrationTests")

    if not integration_dir.exists():
        print(f"❌ Directory not found: {integration_dir}")
        return

    # Analyze all CS files
    cs_files = list(integration_dir.rglob("*.cs"))

    integration_tests = []
    system_tests = []
    unknown_tests = []

    print(f"📂 Analyzing {len(cs_files)} test files...")
    print()

    for cs_file in cs_files:
        if 'GlobalUsings' in cs_file.name or 'AssemblyInfo' in cs_file.name:
            continue

        analysis = analyze_test_file(cs_file, integration_dir)
        if not analysis:
            continue

        if analysis.test_type == 'integration':
            integration_tests.append(analysis)
        elif analysis.test_type == 'system':
            system_tests.append(analysis)
        else:
            unknown_tests.append(analysis)

    # Group by project
    integration_by_project = {}
    system_by_project = {}
    unknown_by_project = {}

    for test in integration_tests:
        integration_by_project.setdefault(test.project_name, []).append(test)

    for test in system_tests:
        system_by_project.setdefault(test.project_name, []).append(test)

    for test in unknown_tests:
        unknown_by_project.setdefault(test.project_name, []).append(test)

    # Report
    print("=" * 100)
    print("📊 CLASSIFICATION RESULTS")
    print("=" * 100)
    print()
    print(f"✅ Integration Tests: {len(integration_tests)} files")
    print(f"🔧 System Tests:      {len(system_tests)} files")
    print(f"❓ Unknown/Needs Review: {len(unknown_tests)} files")
    print()

    if integration_tests:
        print("=" * 100)
        print("✅ INTEGRATION TESTS (1 Docker + Mocks - Correctly placed)")
        print("=" * 100)
        print()
        for project in sorted(integration_by_project.keys()):
            print(f"📦 {project}: {len(integration_by_project[project])} files")
        print()

    if system_tests:
        print("=" * 100)
        print("🔧 SYSTEM TESTS (Full stack - SHOULD BE IN 06SystemTests!)")
        print("=" * 100)
        print()
        for project in sorted(system_by_project.keys()):
            print(f"⚠️  {project}: {len(system_by_project[project])} files")
            for test in system_by_project[project][:5]:  # Show first 5
                print(f"      • {test.file_path}")
        print()

    if unknown_tests:
        print("=" * 100)
        print("❓ UNKNOWN/NEEDS MANUAL REVIEW")
        print("=" * 100)
        print()
        for project in sorted(unknown_by_project.keys()):
            print(f"❓ {project}: {len(unknown_by_project[project])} files")
        print()

    # Migration plan
    print("=" * 100)
    print("📋 MIGRATION PLAN")
    print("=" * 100)
    print()

    print("STEP 1: Keep Integration Tests in 05IntegrationTests (but rename to evocative)")
    for project in sorted(integration_by_project.keys()):
        print(f"   ✅ {project} → Rename to evocative (see earlier mapping)")
    print()

    print("STEP 2: Move System Tests to 06SystemTests (and rename to evocative)")
    for project in sorted(system_by_project.keys()):
        print(f"   🔧 {project} → 06SystemTests/ExxerAI.{project}.System.Test")
    print()

    print("STEP 3: Review Unknown tests manually")
    for project in sorted(unknown_by_project.keys()):
        print(f"   ❓ {project} → Manual review needed")
    print()

    print("=" * 100)


if __name__ == "__main__":
    main()
