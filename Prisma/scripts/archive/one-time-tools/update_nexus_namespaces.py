#!/usr/bin/env python3
"""
Update namespaces from ExxerAI.Nexus to ExxerAI.Nexus.Library
"""

import os
import re
from datetime import datetime

def update_namespaces_in_file(file_path):
    """Update namespaces in a single file"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        original_content = content
        
        # Update namespace declarations
        content = re.sub(r'namespace\s+ExxerAI\.Nexus(?!\.Library)', 'namespace ExxerAI.Nexus.Library', content)
        
        # Update using statements
        content = re.sub(r'using\s+ExxerAI\.Nexus(?!\.Library)', 'using ExxerAI.Nexus.Library', content)
        
        # Update global using statements
        content = re.sub(r'global\s+using\s+ExxerAI\.Nexus(?!\.Library)', 'global using ExxerAI.Nexus.Library', content)
        
        if content != original_content:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(content)
            return True
        return False
    except Exception as e:
        print(f"  ✗ Error updating {file_path}: {e}")
        return False

def update_all_namespaces():
    """Update namespaces in all files in the Library project"""
    target_dir = r"F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Nexus.Library"
    
    updated_count = 0
    total_count = 0
    
    print(f"\n{'=' * 80}")
    print(f"UPDATING NAMESPACES TO ExxerAI.Nexus.Library")
    print(f"Started: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")
    
    # Process all C# files
    for root, dirs, files in os.walk(target_dir):
        for file in files:
            if file.endswith('.cs'):
                total_count += 1
                file_path = os.path.join(root, file)
                
                if update_namespaces_in_file(file_path):
                    updated_count += 1
                    print(f"  ✓ Updated: {os.path.relpath(file_path, target_dir)}")
                else:
                    print(f"  - No changes: {os.path.relpath(file_path, target_dir)}")
    
    print(f"\n{'=' * 80}")
    print(f"SUMMARY: Updated {updated_count} of {total_count} files")
    print(f"Completed: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")

if __name__ == "__main__":
    update_all_namespaces()