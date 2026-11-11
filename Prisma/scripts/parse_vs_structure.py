#!/usr/bin/env python3
"""
Parse Visual Studio .sln file to show the folder structure as it appears in VS.
"""

import re
from pathlib import Path
from typing import Dict, List, Tuple

def parse_sln_structure(sln_path: str):
    """Parse .sln file and display the Visual Studio folder structure."""

    with open(sln_path, 'r', encoding='utf-8-sig') as f:
        content = f.read()

    # Extract projects (both folders and actual projects)
    # Format: Project("{GUID}") = "Name", "Path", "{ProjectGUID}"
    project_pattern = r'Project\("\{([^}]+)\}"\)\s*=\s*"([^"]+)",\s*"([^"]+)",\s*"\{([^}]+)\}"'
    projects = re.findall(project_pattern, content)

    # Build GUID to name mapping
    guid_to_info = {}
    folder_guid = "2150E333-8FDC-42A3-9474-1A3956D46DE8"  # Solution folder GUID

    for type_guid, name, path, proj_guid in projects:
        is_folder = (type_guid.upper() == folder_guid.upper())
        guid_to_info[proj_guid.upper()] = {
            'name': name,
            'path': path,
            'is_folder': is_folder,
            'type_guid': type_guid
        }

    # Extract nested relationships
    # Format: {ChildGUID} = {ParentGUID}
    nested_section = re.search(r'GlobalSection\(NestedProjects\).*?EndGlobalSection', content, re.DOTALL)

    parent_map = {}  # child_guid -> parent_guid

    if nested_section:
        nested_content = nested_section.group(0)
        nested_pattern = r'\{([^}]+)\}\s*=\s*\{([^}]+)\}'
        nested_pairs = re.findall(nested_pattern, nested_content)

        for child_guid, parent_guid in nested_pairs:
            parent_map[child_guid.upper()] = parent_guid.upper()

    # Build tree structure
    root_items = []
    children_map = {}  # parent_guid -> [child_guids]

    for guid in guid_to_info.keys():
        if guid in parent_map:
            parent_guid = parent_map[guid]
            if parent_guid not in children_map:
                children_map[parent_guid] = []
            children_map[parent_guid].append(guid)
        else:
            root_items.append(guid)

    # Print tree
    print("="*80)
    print("VISUAL STUDIO SOLUTION STRUCTURE")
    print("="*80)
    print(f"Solution: {Path(sln_path).name}")
    print("")

    def print_tree(guid, indent=0):
        """Recursively print tree structure."""
        info = guid_to_info.get(guid)
        if not info:
            return

        prefix = "  " * indent
        icon = "📁" if info['is_folder'] else "📄"

        # Clean up the name
        name = info['name']

        # For projects, show relative path
        if not info['is_folder'] and info['path'] not in [name, ""]:
            path_suffix = f" ({Path(info['path']).parent})" if Path(info['path']).parent != Path('.') else ""
            print(f"{prefix}{icon} {name}{path_suffix}")
        else:
            print(f"{prefix}{icon} {name}")

        # Print children
        if guid in children_map:
            # Sort children: folders first, then projects
            children = children_map[guid]
            children_sorted = sorted(children, key=lambda g: (
                not guid_to_info.get(g, {}).get('is_folder', False),
                guid_to_info.get(g, {}).get('name', '').lower()
            ))

            for child_guid in children_sorted:
                print_tree(child_guid, indent + 1)

    # Sort root items: folders first, then projects
    root_items_sorted = sorted(root_items, key=lambda g: (
        not guid_to_info.get(g, {}).get('is_folder', False),
        guid_to_info.get(g, {}).get('name', '').lower()
    ))

    for root_guid in root_items_sorted:
        print_tree(root_guid)

    print("")
    print("="*80)
    print(f"Total Projects: {len([g for g, i in guid_to_info.items() if not i['is_folder']])}")
    print(f"Total Folders: {len([g for g, i in guid_to_info.items() if i['is_folder']])}")
    print("="*80)


def main():
    import sys

    sln_path = "Code/Src/CSharp/ExxerCube.Prisma.sln" if len(sys.argv) < 2 else sys.argv[1]

    if not Path(sln_path).exists():
        print(f"Error: Solution file not found: {sln_path}")
        return 1

    parse_sln_structure(sln_path)
    return 0


if __name__ == "__main__":
    exit(main())
