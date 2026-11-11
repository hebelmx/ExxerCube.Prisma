#!/usr/bin/env python3
"""
Resolve class duplications by keeping lower abstraction level versions
Priority: Domain > Application > Infrastructure (Axioms, etc.)
"""

import os
import re
import json
from pathlib import Path
from datetime import datetime

# Load type dictionary
with open('exxerai_types.json', 'r') as f:
    type_dict = json.load(f)

# Find all duplicates
duplicates = {}
for full_name, info in type_dict['types'].items():
    class_name = info['name']
    if class_name not in duplicates:
        duplicates[class_name] = []
    duplicates[class_name].append({
        'full_name': full_name,
        'namespace': info['namespace'],
        'file': info.get('file', ''),
        'project': info.get('project', '')
    })

# Filter to only actual duplicates
duplicates = {k: v for k, v in duplicates.items() if len(v) > 1}

print(f"\n{'=' * 80}")
print(f"CLASS DUPLICATE RESOLUTION")
print(f"Started: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
print(f"{'=' * 80}\n")

print(f"Found {len(duplicates)} duplicate class names:\n")

# Define abstraction levels (lower number = higher priority)
def get_abstraction_level(namespace):
    if 'Domain' in namespace:
        return 1
    elif 'Application' in namespace:
        return 2
    elif 'Axioms' in namespace:
        return 3
    elif 'Vault' in namespace:  # Vault is semantic/vector storage - infrastructure
        return 4
    elif 'Nexus' in namespace:  # Nexus is document processing - infrastructure  
        return 5
    else:
        return 10

# Resolution plan
resolution_plan = {}
files_to_delete = []
namespace_replacements = {}

for class_name, locations in duplicates.items():
    # Sort by abstraction level
    sorted_locs = sorted(locations, key=lambda x: get_abstraction_level(x['namespace']))
    
    print(f"{class_name}:")
    for loc in sorted_locs:
        level = get_abstraction_level(loc['namespace'])
        print(f"  {level}. {loc['namespace']} - {loc['file']}")
    
    # Keep the one with lowest abstraction level
    keep = sorted_locs[0]
    delete = sorted_locs[1:]
    
    print(f"  → Keep: {keep['namespace']}")
    print(f"  → Delete: {[d['namespace'] for d in delete]}")
    print()
    
    # Track files to delete
    for d in delete:
        if d['file']:
            file_path = os.path.join(r"F:\Dynamic\ExxerAi\ExxerAI", d['file'].replace('\\', os.sep))
            files_to_delete.append(file_path)
            # Create namespace replacement mapping
            namespace_replacements[d['full_name']] = keep['full_name']

# Write namespace replacements for updating references
print(f"\nNamespace replacements needed ({len(namespace_replacements)}):")
for old, new in namespace_replacements.items():
    print(f"  {old} → {new}")

# Save resolution plan
resolution = {
    'timestamp': datetime.now().isoformat(),
    'duplicates_found': len(duplicates),
    'files_to_delete': files_to_delete,
    'namespace_replacements': namespace_replacements
}

with open('duplicate_resolution_plan.json', 'w') as f:
    json.dump(resolution, f, indent=2)

print(f"\nResolution plan saved to: duplicate_resolution_plan.json")

# Create deletion script
delete_script = f"""#!/usr/bin/env python3
# Auto-generated script to delete duplicate class files
# Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}

import os
import shutil
from datetime import datetime

files_to_delete = {json.dumps(files_to_delete, indent=2)}

backup_dir = f"backup_duplicates_{{datetime.now().strftime('%Y%m%d_%H%M%S')}}"
os.makedirs(backup_dir, exist_ok=True)

print(f"Backing up {{len(files_to_delete)}} files to {{backup_dir}}")

for file in files_to_delete:
    if os.path.exists(file):
        # Backup first
        backup_path = os.path.join(backup_dir, os.path.basename(file))
        shutil.copy2(file, backup_path)
        print(f"  Backed up: {{os.path.basename(file)}}")
        
        # Then delete
        os.remove(file)
        print(f"  Deleted: {{file}}")
    else:
        print(f"  Not found: {{file}}")

print(f"\\nDeleted {{len(files_to_delete)}} duplicate files")
print(f"Backups saved to: {{backup_dir}}")
"""

with open('delete_duplicate_classes.py', 'w') as f:
    f.write(delete_script)

print(f"\nDeletion script created: delete_duplicate_classes.py")

# Create update references script
update_script = f"""#!/usr/bin/env python3
# Auto-generated script to update references to deleted duplicate classes
# Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}

import os
import re
from pathlib import Path

namespace_replacements = {json.dumps(namespace_replacements, indent=2)}

# Find all C# files
cs_files = []
for root, dirs, files in os.walk(r"F:\\Dynamic\\ExxerAi\\ExxerAI\\code\\src"):
    for file in files:
        if file.endswith('.cs'):
            cs_files.append(os.path.join(root, file))

print(f"Found {{len(cs_files)}} C# files to check")

updated_count = 0
for file_path in cs_files:
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        modified = False
        for old_ns, new_ns in namespace_replacements.items():
            if old_ns in content:
                # Replace full namespace references
                content = content.replace(old_ns, new_ns)
                modified = True
        
        if modified:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(content)
            updated_count += 1
            print(f"  Updated: {{os.path.basename(file_path)}}")
    
    except Exception as e:
        print(f"  Error updating {{file_path}}: {{e}}")

print(f"\\nUpdated {{updated_count}} files with new namespaces")
"""

with open('update_duplicate_references.py', 'w') as f:
    f.write(update_script)

print(f"Reference update script created: update_duplicate_references.py")
print(f"\n{'=' * 80}")
print("NEXT STEPS:")
print("1. Review the duplicate_resolution_plan.json")
print("2. Run: python delete_duplicate_classes.py")
print("3. Run: python update_duplicate_references.py")
print("4. Rebuild the solution")
print(f"{'=' * 80}\n")