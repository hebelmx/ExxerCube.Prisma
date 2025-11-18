#!/usr/bin/env python3
"""
Update solution file for Phase 1 renamed projects
"""

import re
from pathlib import Path


def update_solution_file():
    """Update solution file with new project references"""

    sln_file = Path("F:/Dynamic/ExxerAi/ExxerAI/code/src/ExxerAI.sln")

    if not sln_file.exists():
        print("⚠️  Solution file not found")
        return

    print("📋 Updating solution file...")

    with open(sln_file, 'r', encoding='utf-8') as f:
        content = f.read()

    # Define all the renames
    renames = {
        "ExxerAI.Analytics.Integration.Test": "ExxerAI.Signal.Integration.Test",
        "ExxerAI.Authentication.Integration.Test": "ExxerAI.Sentinel.Integration.Test",
        "ExxerAI.EnhancedRag.Integration.Test": "ExxerAI.Cortex.Integration.Test",
        "ExxerAI.Cache.Integration.Test": "ExxerAI.Datastream.Integration.Test",
        "ExxerAI.Database.Integration.Test": "ExxerAI.Datastream.Integration.Test",
        "ExxerAI.GoogleDriveM2M.Integration.Test": "ExxerAI.Gatekeeper.Integration.Test",
        "ExxerAI.Infrastructure.GoogleDriveM2M.Integration.Test": "ExxerAI.Gatekeeper.Integration.Test",
    }

    # Track which project GUIDs to remove (for merges)
    projects_to_remove = []

    # First pass: collect GUIDs of merged projects
    for old_name in renames.keys():
        # Find the project GUID for old projects
        pattern = rf'Project\("{{[^}}]+}}"\) = "{re.escape(old_name)}", "[^"]*", "(\{{[^}}]+\}})"'
        match = re.search(pattern, content)
        if match:
            guid = match.group(1)
            new_name = renames[old_name]

            # If this is a merge (multiple projects map to same new name),
            # we need to remove all but the first occurrence
            existing_pattern = rf'Project\("{{[^}}]+}}"\) = "{re.escape(new_name)}"'
            if re.search(existing_pattern, content) and old_name != list(renames.keys())[list(renames.values()).index(new_name)]:
                # This is a duplicate that should be removed
                projects_to_remove.append((old_name, guid))

    print(f"Found {len(projects_to_remove)} duplicate projects to remove")

    # Update project references
    for old_name, new_name in renames.items():
        # Skip if this is a duplicate we'll remove
        if any(old_name == p[0] for p in projects_to_remove):
            continue

        # Update project path
        content = re.sub(
            rf'tests\\05IntegrationTests\\{re.escape(old_name)}\\{re.escape(old_name)}\.csproj',
            f'tests\\\\05IntegrationTests\\\\{new_name}\\\\{new_name}.csproj',
            content
        )

        # Update project name in solution
        content = re.sub(
            rf'= "{re.escape(old_name)}", "tests\\05IntegrationTests\\{re.escape(old_name)}',
            f'= "{new_name}", "tests\\\\05IntegrationTests\\\\{new_name}',
            content
        )

    # Remove duplicate project entries
    for old_name, guid in projects_to_remove:
        # Remove the Project(...) line
        pattern = rf'Project\("[^"]+"\) = "{re.escape(old_name)}", "[^"]*", "{re.escape(guid)}"\s*EndProject\s*\n'
        content = re.sub(pattern, '', content, flags=re.MULTILINE)

        # Remove from GlobalSection entries
        guid_pattern = rf'\s*{re.escape(guid)}[^\n]*\n'
        content = re.sub(guid_pattern, '', content)

    with open(sln_file, 'w', encoding='utf-8') as f:
        f.write(content)

    print(f"✅ Updated solution file with {len(renames)} renames")
    print(f"✅ Removed {len(projects_to_remove)} duplicate entries")


def main():
    print("=" * 100)
    print("🎯 UPDATE SOLUTION FILE FOR PHASE 1")
    print("=" * 100)
    print()

    update_solution_file()

    print()
    print("=" * 100)
    print("✅ Solution file updated!")
    print("=" * 100)


if __name__ == "__main__":
    main()
