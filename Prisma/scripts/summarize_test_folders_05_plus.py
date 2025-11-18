#!/usr/bin/env python3
"""
Summarize test folder structure 05+ to understand current vs ADR-011 target
"""

from pathlib import Path
from collections import defaultdict

def main():
    print("🔍 Current Test Folder Structure (05+) vs ADR-011")
    print("=" * 100)
    print()

    tests_dir = Path("code/src/tests")

    # Get all numbered folders 05+
    folders = sorted([f for f in tests_dir.iterdir() if f.is_dir() and f.name[0:2].isdigit() and int(f.name[0:2]) >= 5])

    print("📂 CURRENT STATE:")
    print("=" * 100)
    for folder in folders:
        projects = list(folder.rglob("*.csproj"))
        print(f"\n{folder.name}/ ({len(projects)} projects)")
        if len(projects) <= 10:
            for proj in projects:
                print(f"   • {proj.parent.name}")
        else:
            for proj in projects[:5]:
                print(f"   • {proj.parent.name}")
            print(f"   ... and {len(projects) - 5} more")

    print("\n" + "=" * 100)
    print("📋 ADR-011 TARGET:")
    print("=" * 100)
    print("""
04AdapterTests/     ← Test doubles, mocks, in-memory (NO real services)
   • ExxerAI.Adapters.Conduit.Test
   • ExxerAI.Adapters.Vault.Test
   • ExxerAI.Adapters.Cortex.Test
   • ExxerAI.Adapters.Gatekeeper.Test
   • ExxerAI.Adapters.Datastream.Test

05SystemTests/      ← REAL external dependencies (1 Docker + real service)
   • ExxerAI.Conduit.Integration.Test (REAL agent communication)
   • ExxerAI.Vault.Integration.Test (REAL Qdrant + Neo4j)
   • ExxerAI.Cortex.Integration.Test (REAL Ollama/OpenAI)
   • ExxerAI.Gatekeeper.Integration.Test (REAL GoogleDrive)
   • ExxerAI.Datastream.Integration.Test (REAL PostgreSQL)
   • ExxerAI.Sentinel.Integration.Test (REAL auth services)
   • ExxerAI.Signal.Integration.Test (REAL analytics)
   • ExxerAI.Nexus.Integration.Test (REAL document processing)

06SystemTests/      ← End-to-end with ALL real dependencies
    """)

    print("=" * 100)
    print("⚠️  CONFUSION POINTS:")
    print("=" * 100)
    print("""
1. Current folder "05IntegrationTests" should be "05SystemTests" per ADR-011
2. Multiple folders share same numbers (05CrossCutting + 05IntegrationTests)
3. Non-evocative project names in 05IntegrationTests folder
4. Projects scattered across 05CrossCutting, 06Communication, 07Infrastructure
    """)

    print("=" * 100)
    print("🎯 RECOMMENDED INCREMENTAL APPROACH:")
    print("=" * 100)
    print("""
STEP 1: Consolidate evocative integration tests
   Move from:
   - 05CrossCutting/ExxerAI.Nexus.IntegrationTest
   - 06Communication/ExxerAI.Conduit.IntegrationTest
   - 07Infrastructure/ExxerAI.Signal.IntegrationTest
   To: 05SystemTests/ (keeping project names)

STEP 2: Migrate 05IntegrationTests non-evocative projects
   Rename and move to 05SystemTests/:
   - Analytics.Integration.Test → ExxerAI.Signal.Integration.Test
   - Authentication.Integration.Test → ExxerAI.Sentinel.Integration.Test
   - Cache.Integration.Test → ExxerAI.Datastream.Integration.Test
   - Database.Integration.Test → ExxerAI.Datastream.Integration.Test (merge)
   - Components.Integration.Test → BREAK UP into evocative projects
   - GoogleDrive projects → ExxerAI.Gatekeeper.Integration.Test
   - EnhancedRag → ExxerAI.Cortex.Integration.Test
   - Nexus.Integration.Test → Already exists, move to 05SystemTests

STEP 3: Delete empty 05IntegrationTests folder

STEP 4: Clean up old numbered folders (05CrossCutting, 06Communication, etc)
    """)
    print("=" * 100)


if __name__ == "__main__":
    main()
