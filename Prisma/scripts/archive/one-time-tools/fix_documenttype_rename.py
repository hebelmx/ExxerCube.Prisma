#!/usr/bin/env python3
"""
DocumentType to EAIDocumentType Rename Script
Safely replaces DocumentType with EAIDocumentType in affected files from CS0246.txt
"""

import os
import re
from pathlib import Path
from typing import Set, List
import subprocess

def extract_files_from_errors(error_file: str) -> Set[str]:
    """Extract unique file paths containing DocumentType errors."""
    files = set()
    
    try:
        with open(error_file, 'r', encoding='utf-8') as f:
            for line in f:
                # Skip header line
                if line.startswith('Severity'):
                    continue
                    
                parts = line.strip().split('\t')
                if len(parts) >= 5 and 'DocumentType' in line:
                    file_path = parts[4]  # File column
                    files.add(file_path)
                    
    except FileNotFoundError:
        print(f"Error file not found: {error_file}")
        return set()
    
    return files

def git_add_commit(base_path: str, message: str) -> bool:
    """Create a safety git commit."""
    try:
        os.chdir(base_path)
        subprocess.run(['git', 'add', '.'], check=True, capture_output=True)
        subprocess.run(['git', 'commit', '-m', message], check=True, capture_output=True)
        print(f"✅ Created safety commit: {message}")
        return True
    except subprocess.CalledProcessError as e:
        print(f"⚠️ Git commit failed: {e}")
        return False

def replace_documenttype_in_file(file_path: str, dry_run: bool = True) -> int:
    """Replace DocumentType with EAIDocumentType in a single file."""
    if not os.path.exists(file_path):
        print(f"❌ File not found: {file_path}")
        return 0
    
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # Count occurrences before replacement
        original_count = len(re.findall(r'\bDocumentType\b', content))
        
        if original_count == 0:
            return 0
        
        # Replace DocumentType with EAIDocumentType (word boundary to avoid partial matches)
        new_content = re.sub(r'\bDocumentType\b', 'EAIDocumentType', content)
        
        if dry_run:
            print(f"📝 DRY RUN - Would replace {original_count} occurrences in: {os.path.basename(file_path)}")
        else:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(new_content)
            print(f"✅ Replaced {original_count} occurrences in: {os.path.basename(file_path)}")
        
        return original_count
        
    except Exception as e:
        print(f"❌ Error processing {file_path}: {e}")
        return 0

def main():
    base_path = "F:/Dynamic/ExxerAi/ExxerAI"
    error_file = os.path.join(base_path, "Errors", "CS0246.txt")
    
    print("🔍 Extracting files with DocumentType errors...")
    affected_files = extract_files_from_errors(error_file)
    
    if not affected_files:
        print("❌ No files found with DocumentType errors")
        return
    
    print(f"📋 Found {len(affected_files)} files with DocumentType errors:")
    for file_path in sorted(affected_files):
        print(f"   • {os.path.basename(file_path)}")
    
    print("\n🧪 DRY RUN - Checking what would be replaced...")
    total_replacements = 0
    
    for file_path in sorted(affected_files):
        count = replace_documenttype_in_file(file_path, dry_run=True)
        total_replacements += count
    
    print(f"\n📊 DRY RUN SUMMARY:")
    print(f"   • Files to modify: {len(affected_files)}")
    print(f"   • Total replacements: {total_replacements}")
    
    # Auto-apply changes (no interactive input needed)
    print("\n✅ Auto-applying changes (80 DocumentType errors to fix)...")
    
    # Create safety commit
    print("\n🔒 Creating safety commit...")
    if not git_add_commit(base_path, "Safety commit before DocumentType to EAIDocumentType rename"):
        print("⚠️ Warning: Could not create safety commit, but proceeding...")
    
    # Apply changes
    print("\n🔧 Applying changes...")
    actual_replacements = 0
    
    for file_path in sorted(affected_files):
        count = replace_documenttype_in_file(file_path, dry_run=False)
        actual_replacements += count
    
    print(f"\n✅ COMPLETED:")
    print(f"   • Files modified: {len(affected_files)}")
    print(f"   • Total replacements: {actual_replacements}")
    print(f"   • DocumentType → EAIDocumentType")
    
    print("\n📝 Next steps:")
    print("   1. Run 'dotnet build' to verify fixes")
    print("   2. Commit the changes if successful")

if __name__ == "__main__":
    main()