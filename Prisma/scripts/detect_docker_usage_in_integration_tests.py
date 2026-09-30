#!/usr/bin/env python3
"""
Detect which projects in 05IntegrationTests use Docker containers
Per ADR-011:
- WITH Docker (1 real dependency) → Stay in 05IntegrationTests, rename to evocative
- WITHOUT Docker → Move to 04AdapterTests
"""

import re
from pathlib import Path
from collections import defaultdict


def detect_docker_usage(project_dir: Path) -> dict:
    """Detect if project uses Docker containers"""

    uses_docker = False
    docker_indicators = []

    # Check all CS files
    cs_files = list(project_dir.rglob("*.cs"))

    for cs_file in cs_files:
        try:
            with open(cs_file, 'r', encoding='utf-8') as f:
                content = f.read()

            # Look for Docker/container indicators
            if re.search(r'(TestContainers|DotNet\.Testcontainers)', content):
                uses_docker = True
                docker_indicators.append("TestContainers package")

            if re.search(r'(ContainerFixture|DockerFixture)', content):
                uses_docker = True
                docker_indicators.append("Container fixtures")

            if re.search(r'\.WithImage\(|\.Build\(\)|\.StartAsync\(', content):
                uses_docker = True
                docker_indicators.append("Container API calls")

            if 'docker-compose' in content.lower():
                uses_docker = True
                docker_indicators.append("docker-compose reference")

        except:
            continue

    # Check for docker-compose files
    if (project_dir / "docker-compose.yml").exists() or (project_dir / "docker-compose.yaml").exists():
        uses_docker = True
        docker_indicators.append("docker-compose.yml file")

    return {
        'uses_docker': uses_docker,
        'indicators': list(set(docker_indicators)),
        'cs_file_count': len(cs_files)
    }


def main():
    print("🔍 Detecting Docker Usage in 05IntegrationTests")
    print("Per ADR-011: WITH Docker → Stay, WITHOUT Docker → Move to 04AdapterTests")
    print("=" * 100)
    print()

    integration_dir = Path("code/src/tests/05IntegrationTests")

    if not integration_dir.exists():
        print(f"❌ Directory not found: {integration_dir}")
        return

    # Find all projects
    projects = sorted([p.parent for p in integration_dir.rglob("*.csproj")])

    docker_projects = []
    non_docker_projects = []

    for project_dir in projects:
        project_name = project_dir.name
        analysis = detect_docker_usage(project_dir)

        if analysis['uses_docker']:
            docker_projects.append({
                'name': project_name,
                'indicators': analysis['indicators'],
                'files': analysis['cs_file_count']
            })
        else:
            non_docker_projects.append({
                'name': project_name,
                'files': analysis['cs_file_count']
            })

    # Report
    print("=" * 100)
    print("📊 CLASSIFICATION RESULTS")
    print("=" * 100)
    print()
    print(f"🐳 WITH Docker:     {len(docker_projects)} projects (STAY in 05IntegrationTests)")
    print(f"📦 WITHOUT Docker:  {len(non_docker_projects)} projects (MOVE to 04AdapterTests)")
    print()

    if docker_projects:
        print("=" * 100)
        print("🐳 PROJECTS WITH DOCKER (Stay in 05IntegrationTests)")
        print("=" * 100)
        print()
        for proj in docker_projects:
            print(f"✅ {proj['name']}")
            print(f"   Docker indicators: {', '.join(proj['indicators'])}")
            print(f"   Files: {proj['files']}")
            print()

    if non_docker_projects:
        print("=" * 100)
        print("📦 PROJECTS WITHOUT DOCKER (Move to 04AdapterTests)")
        print("=" * 100)
        print()
        for proj in non_docker_projects:
            print(f"⚠️  {proj['name']}")
            print(f"   Files: {proj['files']}")
            print()

    # Migration recommendations
    print("=" * 100)
    print("🎯 MIGRATION PLAN")
    print("=" * 100)
    print()

    if docker_projects:
        print("STEP 1: Rename Docker projects to evocative names (KEEP in 05IntegrationTests)")
        for proj in docker_projects:
            evocative_name = proj['name']
            if 'Analytics' in evocative_name:
                evocative_name = "ExxerAI.Signal.Integration.Test"
            elif 'Authentication' in evocative_name:
                evocative_name = "ExxerAI.Sentinel.Integration.Test"
            elif 'Cache' in evocative_name:
                evocative_name = "ExxerAI.Datastream.Integration.Test"
            elif 'Database' in evocative_name:
                evocative_name = "ExxerAI.Datastream.Integration.Test"
            elif 'Components' in evocative_name:
                evocative_name = "BREAK UP: Conduit, Vault, Cortex, Sentinel, etc."
            elif 'EnhancedRag' in evocative_name:
                evocative_name = "ExxerAI.Cortex.Integration.Test"
            elif 'GoogleDrive' in evocative_name:
                evocative_name = "ExxerAI.Gatekeeper.Integration.Test"
            elif 'Nexus' in evocative_name:
                evocative_name = "✅ Already evocative"

            print(f"   🐳 {proj['name']}")
            print(f"      → {evocative_name}")
        print()

    if non_docker_projects:
        print("STEP 2: Move non-Docker projects to 04AdapterTests (and rename)")
        for proj in non_docker_projects:
            adapter_name = proj['name'].replace('.Integration.Test', '.Adapter.Test')
            if 'Analytics' in adapter_name:
                adapter_name = "ExxerAI.Signal.Adapter.Test"
            elif 'Authentication' in adapter_name:
                adapter_name = "ExxerAI.Sentinel.Adapter.Test"
            elif 'Cache' in adapter_name:
                adapter_name = "ExxerAI.Datastream.Adapter.Test"
            elif 'Database' in adapter_name:
                adapter_name = "ExxerAI.Datastream.Adapter.Test"
            elif 'GoogleDrive' in adapter_name:
                adapter_name = "ExxerAI.Gatekeeper.Adapter.Test"

            print(f"   📦 {proj['name']}")
            print(f"      → 04AdapterTests/{adapter_name}")
        print()

    print("=" * 100)


if __name__ == "__main__":
    main()
