#!/usr/bin/env python3
"""
Complete Phase 1 merges manually (avoiding permission issues)
"""

import shutil
import re
from pathlib import Path


def update_namespace_in_file(file_path: Path, old_ns: str, new_ns: str):
    """Update namespace declarations in a C# file"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()

        # Update namespace declarations
        content = re.sub(
            rf'^namespace\s+{re.escape(old_ns)}\s*;',
            f'namespace {new_ns};',
            content,
            flags=re.MULTILINE
        )
        content = re.sub(
            rf'namespace\s+{re.escape(old_ns)}\s*{{',
            f'namespace {new_ns} {{',
            content
        )

        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(content)

    except Exception as e:
        print(f"⚠️  Error updating {file_path}: {e}")


def merge_projects(base_path: Path, source_names: list, target_name: str):
    """Merge multiple projects into one"""
    integration_dir = base_path / "code/src/tests/05IntegrationTests"
    target_dir = integration_dir / target_name

    print(f"\n🔀 MERGE: {', '.join(source_names)} → {target_name}")
    print("-" * 100)

    # Create target directory
    target_dir.mkdir(exist_ok=True)
    print(f"✅ Created {target_name} directory")

    # Copy .csproj from first source
    first_source = integration_dir / source_names[0]
    first_csproj = first_source / f"{source_names[0]}.csproj"

    if first_csproj.exists():
        target_csproj = target_dir / f"{target_name}.csproj"
        shutil.copy2(first_csproj, target_csproj)
        update_namespace_in_file(target_csproj, source_names[0], target_name)
        print(f"✅ Created {target_name}.csproj from {source_names[0]}")

    # Copy all CS files from all sources
    for source_name in source_names:
        source_dir = integration_dir / source_name
        if not source_dir.exists():
            print(f"⚠️  {source_name} does not exist, skipping...")
            continue

        cs_files = list(source_dir.rglob("*.cs"))

        for cs_file in cs_files:
            relative_path = cs_file.relative_to(source_dir)
            target_file = target_dir / relative_path

            target_file.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(cs_file, target_file)
            # Update namespace
            update_namespace_in_file(target_file, source_name, target_name)

        print(f"✅ Copied {len(cs_files)} files from {source_name}")

    # Delete old directories
    for source_name in source_names:
        source_dir = integration_dir / source_name
        if source_dir.exists():
            shutil.rmtree(source_dir)
            print(f"✅ Deleted old directory: {source_name}")


def main():
    base_path = Path("F:/Dynamic/ExxerAi/ExxerAI")

    print("=" * 100)
    print("🎯 COMPLETE PHASE 1 MERGES")
    print("=" * 100)

    # Merge: Cache + Database → Datastream
    merge_projects(
        base_path,
        ["ExxerAI.Cache.Integration.Test", "ExxerAI.Database.Integration.Test"],
        "ExxerAI.Datastream.Integration.Test"
    )

    # Merge: GoogleDrive projects → Gatekeeper
    merge_projects(
        base_path,
        ["ExxerAI.GoogleDriveM2M.Integration.Test", "ExxerAI.Infrastructure.GoogleDriveM2M.Integration.Test"],
        "ExxerAI.Gatekeeper.Integration.Test"
    )

    print()
    print("=" * 100)
    print("✅ Phase 1 merges completed!")
    print("=" * 100)
    print()
    print("Resulting structure:")
    print("  1. ExxerAI.Signal.Integration.Test 📊")
    print("  2. ExxerAI.Sentinel.Integration.Test 🛡️")
    print("  3. ExxerAI.Cortex.Integration.Test 🧠")
    print("  4. ExxerAI.Datastream.Integration.Test 🌊")
    print("  5. ExxerAI.Gatekeeper.Integration.Test 🚪")
    print("  6. ExxerAI.Components.Integration.Test (will break up in Phase 6)")
    print("  7. ExxerAI.Nexus.Integration.Test ⚡")
    print()


if __name__ == "__main__":
    main()
