#!/usr/bin/env python3
"""
Detailed inspection of Docker usage in 05IntegrationTests
Shows actual code snippets where Docker is detected
"""

import re
from pathlib import Path


def inspect_project_for_docker(project_dir: Path) -> dict:
    """Detailed Docker inspection with code snippets"""

    docker_evidence = {
        'project_name': project_dir.name,
        'has_docker_compose': False,
        'has_testcontainers': False,
        'has_container_fixtures': False,
        'code_snippets': [],
        'file_list': []
    }

    # Check for docker-compose files
    for compose_file in ['docker-compose.yml', 'docker-compose.yaml']:
        if (project_dir / compose_file).exists():
            docker_evidence['has_docker_compose'] = True
            docker_evidence['file_list'].append(compose_file)

    # Check all CS files
    cs_files = list(project_dir.rglob("*.cs"))

    for cs_file in cs_files:
        try:
            with open(cs_file, 'r', encoding='utf-8') as f:
                lines = f.readlines()

            for i, line in enumerate(lines, 1):
                # TestContainers package
                if re.search(r'(TestContainers|DotNet\.Testcontainers)', line, re.IGNORECASE):
                    docker_evidence['has_testcontainers'] = True
                    docker_evidence['code_snippets'].append({
                        'file': str(cs_file.relative_to(project_dir)),
                        'line': i,
                        'code': line.strip()
                    })

                # Container fixtures
                if re.search(r'(ContainerFixture|DockerFixture)', line):
                    docker_evidence['has_container_fixtures'] = True
                    docker_evidence['code_snippets'].append({
                        'file': str(cs_file.relative_to(project_dir)),
                        'line': i,
                        'code': line.strip()
                    })

                # Container API calls
                if re.search(r'\.(WithImage|Build|StartAsync|StopAsync)\(', line):
                    docker_evidence['code_snippets'].append({
                        'file': str(cs_file.relative_to(project_dir)),
                        'line': i,
                        'code': line.strip()
                    })

        except:
            continue

    return docker_evidence


def main():
    print("🔍 DETAILED Docker Inspection - 05IntegrationTests")
    print("=" * 100)
    print()

    integration_dir = Path("code/src/tests/05IntegrationTests")

    if not integration_dir.exists():
        print(f"❌ Directory not found: {integration_dir}")
        return

    # Find all projects
    projects = sorted([p.parent for p in integration_dir.rglob("*.csproj")])

    has_docker = []
    no_docker = []

    for project_dir in projects:
        evidence = inspect_project_for_docker(project_dir)

        has_any_docker = (
            evidence['has_docker_compose'] or
            evidence['has_testcontainers'] or
            evidence['has_container_fixtures'] or
            len(evidence['code_snippets']) > 0
        )

        if has_any_docker:
            has_docker.append(evidence)
        else:
            no_docker.append(evidence)

    # Report projects WITH Docker
    if has_docker:
        print("=" * 100)
        print(f"🐳 PROJECTS WITH DOCKER EVIDENCE ({len(has_docker)} projects)")
        print("=" * 100)
        print()

        for evidence in has_docker:
            print(f"📦 {evidence['project_name']}")
            print("-" * 100)

            if evidence['has_docker_compose']:
                print(f"   ✅ docker-compose files: {', '.join(evidence['file_list'])}")

            if evidence['has_testcontainers']:
                print(f"   ✅ Uses TestContainers package")

            if evidence['has_container_fixtures']:
                print(f"   ✅ Has container fixtures")

            if evidence['code_snippets']:
                print(f"   📝 Code evidence ({len(evidence['code_snippets'])} instances):")
                # Show first 5 snippets
                for snippet in evidence['code_snippets'][:5]:
                    print(f"      {snippet['file']}:{snippet['line']}")
                    print(f"      → {snippet['code']}")
                if len(evidence['code_snippets']) > 5:
                    print(f"      ... and {len(evidence['code_snippets']) - 5} more instances")

            print()

    # Report projects WITHOUT Docker
    if no_docker:
        print("=" * 100)
        print(f"📦 PROJECTS WITHOUT DOCKER ({len(no_docker)} projects)")
        print("=" * 100)
        print()

        for evidence in no_docker:
            print(f"   • {evidence['project_name']}")
        print()

    # Summary
    print("=" * 100)
    print("📊 SUMMARY")
    print("=" * 100)
    print()
    print(f"🐳 WITH Docker:    {len(has_docker)} projects → STAY in 05IntegrationTests")
    print(f"📦 WITHOUT Docker: {len(no_docker)} projects → MOVE to 04AdapterTests")
    print()

    if has_docker:
        print("Projects to rename in 05IntegrationTests:")
        for evidence in has_docker:
            print(f"   • {evidence['project_name']}")
    print()

    if no_docker:
        print("Projects to move to 04AdapterTests:")
        for evidence in no_docker:
            print(f"   • {evidence['project_name']}")
    print()

    print("=" * 100)


if __name__ == "__main__":
    main()
