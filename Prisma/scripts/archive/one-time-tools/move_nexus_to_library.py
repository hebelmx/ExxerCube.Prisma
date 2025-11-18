#!/usr/bin/env python3
"""
Move Nexus files to Nexus.Library for shell pattern implementation
"""

import os
import shutil
from datetime import datetime

def move_files_to_library():
    source_dir = r"F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Nexus"
    target_dir = r"F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Nexus.Library"
    
    # Files to keep in the original project
    keep_files = {
        "Program.cs",
        "ExxerAI.Nexus.csproj",
        "README.md",
        ".mcp"  # MCP directory
    }
    
    moved_count = 0
    skipped_count = 0
    
    print(f"\n{'=' * 80}")
    print(f"MOVING NEXUS FILES TO LIBRARY")
    print(f"Started: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")
    
    # Process all files and directories
    for root, dirs, files in os.walk(source_dir):
        # Calculate relative path
        rel_path = os.path.relpath(root, source_dir)
        
        # Skip .mcp directory
        if ".mcp" in dirs:
            dirs.remove(".mcp")
        
        # Create target directory
        if rel_path != ".":
            target_subdir = os.path.join(target_dir, rel_path)
            os.makedirs(target_subdir, exist_ok=True)
        
        # Move files
        for file in files:
            if file in keep_files:
                print(f"  ⏭️  Keeping: {file}")
                skipped_count += 1
                continue
                
            source_file = os.path.join(root, file)
            
            if rel_path == ".":
                target_file = os.path.join(target_dir, file)
            else:
                target_file = os.path.join(target_dir, rel_path, file)
            
            try:
                # Move the file
                shutil.move(source_file, target_file)
                moved_count += 1
                print(f"  ✓ Moved: {os.path.relpath(source_file, source_dir)} → {os.path.relpath(target_file, target_dir)}")
            except Exception as e:
                print(f"  ✗ Error moving {file}: {e}")
    
    # Clean up empty directories in source
    for root, dirs, files in os.walk(source_dir, topdown=False):
        for dir in dirs:
            dir_path = os.path.join(root, dir)
            try:
                if not os.listdir(dir_path):  # Directory is empty
                    os.rmdir(dir_path)
                    print(f"  🗑️  Removed empty directory: {os.path.relpath(dir_path, source_dir)}")
            except:
                pass
    
    print(f"\n{'=' * 80}")
    print(f"SUMMARY: Moved {moved_count} files, kept {skipped_count} files")
    print(f"Completed: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")

if __name__ == "__main__":
    move_files_to_library()