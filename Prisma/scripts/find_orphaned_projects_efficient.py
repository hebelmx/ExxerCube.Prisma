#!/usr/bin/env python3
"""
Efficient Orphaned Project Finder
Avoids token overload by using Python for all operations
"""

import os
import re
from pathlib import Path

def find_orphaned_projects(base_path="."):
    """Find orphaned projects efficiently"""
    
    # 1. Extract solution projects (with paths)
    sln_file = Path(base_path) / "code" / "src" / "ExxerAI.sln"
    solution_projects = set()
    
    try:
        with open(sln_file, 'r', encoding='utf-8') as f:
            content = f.read()
            # Extract project paths from solution
            matches = re.findall(r'"([^"]*\.csproj)"', content)
            for match in matches:
                # Convert to directory path
                proj_dir = Path(match).parent
                solution_projects.add(str(proj_dir).replace('\\', '/'))
    except Exception as e:
        print(f"Error reading solution: {e}")
        return
    
    # 2. Find all actual project directories
    src_path = Path(base_path) / "code" / "src"
    actual_projects = set()
    
    for csproj in src_path.rglob("*.csproj"):
        # Get relative path from src
        rel_path = csproj.relative_to(src_path).parent
        actual_projects.add(str(rel_path).replace('\\', '/'))
    
    # 3. Find orphaned projects
    orphaned = actual_projects - solution_projects
    
    # 4. Output results efficiently
    print(f"SOLUTION PROJECTS: {len(solution_projects)}")
    print(f"ACTUAL PROJECTS: {len(actual_projects)}")
    print(f"ORPHANED PROJECTS: {len(orphaned)}")
    
    if len(orphaned) < 15:
        print("⚠️ WARNING: Less than 15 orphaned projects - something may be wrong")
    elif len(orphaned) > 22:
        print("🛑 ABORT: More than 22 orphaned projects - need investigation")
        return None
    else:
        print("✅ SUCCESS: Found expected number of orphaned projects")
    
    # 5. Save orphaned project paths
    with open('orphaned_projects_full_paths.txt', 'w') as f:
        for project in sorted(orphaned):
            f.write(f"{project}\n")
    
    print(f"\nOrphaned projects saved to: orphaned_projects_full_paths.txt")
    return sorted(orphaned)

if __name__ == "__main__":
    find_orphaned_projects()