#!/usr/bin/env python3
"""
Analyze 05IntegrationTests folder against Evocative Architecture
Identifies which projects align with evocative naming and which don't
"""

from pathlib import Path

# Evocative architecture components
EVOCATIVE_COMPONENTS = {
    'Axis': '🏗️ Structural backbone',
    'Datastream': '🌊 Data flow',
    'Cortex': '🧠 AI brain',
    'Gatekeeper': '🚪 External guardian',
    'Vault': '🏛️ Semantic memory',
    'Sentinel': '🛡️ Security',
    'Conduit': '📡 Communication',
    'Nexus': '⚡ Document processing',
    'Chronos': '⏰ Scheduling',
    'Signal': '📊 Monitoring',
    'Helix': '🧬 Knowledge graph',
    'Nebula': '🌌 Experimental',
    'Wisdom': '🦉 Governance',
}

def main():
    print("🔍 Analyzing 05IntegrationTests against Evocative Architecture")
    print("=" * 100)
    print()

    integration_tests_dir = Path("code/src/tests/05IntegrationTests")

    if not integration_tests_dir.exists():
        print(f"❌ Directory not found: {integration_tests_dir}")
        return

    # Find all test projects
    projects = sorted([p.parent for p in integration_tests_dir.rglob("*.csproj")])

    evocative_projects = []
    non_evocative_projects = []

    for project_dir in projects:
        project_name = project_dir.name

        # Check if project name contains any evocative component
        is_evocative = any(component in project_name for component in EVOCATIVE_COMPONENTS.keys())

        if is_evocative:
            evocative_projects.append(project_name)
        else:
            non_evocative_projects.append(project_name)

    print(f"Total Integration Test Projects: {len(projects)}")
    print(f"✅ Evocative: {len(evocative_projects)}")
    print(f"❌ Non-Evocative: {len(non_evocative_projects)}")
    print()

    if evocative_projects:
        print("=" * 100)
        print("✅ EVOCATIVE ARCHITECTURE PROJECTS (Aligned)")
        print("=" * 100)
        print()
        for proj in evocative_projects:
            # Find which component
            for component, desc in EVOCATIVE_COMPONENTS.items():
                if component in proj:
                    print(f"   {desc} {proj}")
                    break
        print()

    if non_evocative_projects:
        print("=" * 100)
        print("❌ NON-EVOCATIVE PROJECTS (Need Migration/Review)")
        print("=" * 100)
        print()
        for proj in non_evocative_projects:
            print(f"   • {proj}")
        print()

        print("=" * 100)
        print("💡 MIGRATION RECOMMENDATIONS")
        print("=" * 100)
        print()

        for proj in non_evocative_projects:
            if 'Analytics' in proj:
                print(f"   📦 {proj}")
                print(f"      → Consider: ExxerAI.Signal.IntegrationTest (📊 Monitoring)")
                print()
            elif 'Authentication' in proj:
                print(f"   📦 {proj}")
                print(f"      → Migrate to: ExxerAI.Sentinel.IntegrationTest (🛡️ Security)")
                print()
            elif 'Cache' in proj:
                print(f"   📦 {proj}")
                print(f"      → Migrate to: ExxerAI.Datastream.IntegrationTest (🌊 Data flow)")
                print()
            elif 'Components' in proj:
                print(f"   📦 {proj} ⚠️ CRITICAL - Contains 27 old integration tests")
                print(f"      → This is a FRAGMENT of old ExxerAI.IntegrationTests (32.5%)")
                print(f"      → Needs analysis and migration to evocative projects:")
                print(f"         • A2A tests → ExxerAI.Conduit.IntegrationTest (📡)")
                print(f"         • Container tests → Infrastructure tests")
                print(f"         • Auth tests → ExxerAI.Sentinel.IntegrationTest (🛡️)")
                print(f"         • Knowledge Store → ExxerAI.Vault.IntegrationTest (🏛️)")
                print(f"         • LLM tests → ExxerAI.Cortex.IntegrationTest (🧠)")
                print()
            elif 'Database' in proj:
                print(f"   📦 {proj}")
                print(f"      → Migrate to: ExxerAI.Datastream.IntegrationTest (🌊 Data persistence)")
                print()
            elif 'GoogleDrive' in proj:
                print(f"   📦 {proj}")
                print(f"      → Migrate to: ExxerAI.Gatekeeper.IntegrationTest (🚪 External systems)")
                print()

    print("=" * 100)
    print("📊 SUMMARY")
    print("=" * 100)
    print()
    print(f"Total: {len(projects)} integration test projects")
    print(f"✅ Aligned: {len(evocative_projects)} projects")
    print(f"❌ Non-aligned: {len(non_evocative_projects)} projects")
    print()

    if non_evocative_projects:
        print("⚠️  ACTION REQUIRED:")
        print("   1. Analyze Components.Integration.Test (contains old integration test fragment)")
        print("   2. Migrate non-evocative projects to appropriate evocative projects")
        print("   3. Update solution to reflect evocative architecture")
        print()
    else:
        print("✅ All integration tests aligned with evocative architecture!")
        print()

    print("=" * 100)


if __name__ == "__main__":
    main()
