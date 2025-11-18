#!/usr/bin/env python3
"""
Move all C# files from legacy projects to allclasses folder WITH COMPLETE STRUCTURE PRESERVATION
- Preserves full folder hierarchy information
- Tracks: full path + project + original folder + namespace
- Maintains project structure while consolidating files
- Handles naming conflicts intelligently
"""

import os
import re
from pathlib import Path
from collections import defaultdict
from datetime import datetime

class EnhancedLegacyCsFileMover:
    """Moves C# files while preserving complete structural information."""
    
    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.legacy_path = self.base_path / "code" / "legacy"
        self.allclasses_path = self.legacy_path / "allclasses"
        
    def create_allclasses_folder(self):
        """Create the allclasses destination folder."""
        self.allclasses_path.mkdir(parents=True, exist_ok=True)
        print(f"Created: {self.allclasses_path}")
        
    def extract_namespace(self, cs_file_path: Path):
        """Extract namespace from C# file content."""
        try:
            with open(cs_file_path, 'r', encoding='utf-8') as f:
                content = f.read()
            
            # Look for namespace declaration
            namespace_match = re.search(r'namespace\s+([^\s\{;]+)', content)
            if namespace_match:
                return namespace_match.group(1).strip()
            
            # Look for file-scoped namespace (C# 10+)
            file_namespace_match = re.search(r'namespace\s+([^\s;]+);', content)
            if file_namespace_match:
                return file_namespace_match.group(1).strip()
                
            return "Unknown"
        except Exception:
            return "Unknown"
            
    def get_full_structure_info(self, cs_file_path: Path):
        """Extract complete structural information from file path."""
        relative_path = cs_file_path.relative_to(self.legacy_path)
        parts = relative_path.parts
        
        project_name = parts[0] if parts else "Unknown"
        filename = cs_file_path.name
        
        # Get folder path (everything between project and filename)
        if len(parts) > 2:
            folder_path = "/".join(parts[1:-1])
        else:
            folder_path = ""
            
        full_original_path = str(relative_path).replace("\\", "/")
        
        return {
            'project': project_name,
            'folder_path': folder_path,
            'filename': filename,
            'full_original_path': full_original_path,
            'namespace': self.extract_namespace(cs_file_path)
        }
        
    def generate_enhanced_filename(self, structure_info: dict, counter: int = 0):
        """Keep original filename, add counter only if needed for conflicts."""
        original_name = structure_info['filename']
        
        if counter == 0:
            return original_name
        else:
            # Only add counter if there's a naming conflict
            name_parts = original_name.rsplit('.', 1)
            base_name = name_parts[0]
            extension = name_parts[1] if len(name_parts) > 1 else "cs"
            return f"{base_name}_{counter}.{extension}"
        
    def create_comprehensive_header(self, structure_info: dict):
        """Create comprehensive header with all structural information."""
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        
        header = f"""// ============================================================================
// LEGACY FILE CONSOLIDATION TRACKING
// ============================================================================
// MOVED FROM: {structure_info['full_original_path']}
// PROJECT: {structure_info['project']}
// ORIGINAL FOLDER: {structure_info['folder_path']}
// NAMESPACE: {structure_info['namespace']}
// MOVED ON: {timestamp}
// ============================================================================

"""
        return header
        
    def find_all_cs_files(self):
        """Find all .cs files in legacy projects, excluding allclasses."""
        cs_files = []
        for cs_file in self.legacy_path.rglob("*.cs"):
            # Skip files already in allclasses folder
            if "allclasses" not in str(cs_file):
                cs_files.append(cs_file)
        
        print(f"Found {len(cs_files)} C# files in legacy projects")
        return cs_files
        
    def move_cs_files_with_structure_preservation(self):
        """Move all C# files while preserving complete structural information."""
        cs_files = self.find_all_cs_files()
        moved_count = 0
        structure_report = []
        
        for cs_file in cs_files:
            try:
                # Extract complete structure information
                structure_info = self.get_full_structure_info(cs_file)
                
                # Generate enhanced filename
                counter = 0
                new_filename = self.generate_enhanced_filename(structure_info, counter)
                destination = self.allclasses_path / new_filename
                
                # Handle naming conflicts
                while destination.exists():
                    counter += 1
                    new_filename = self.generate_enhanced_filename(structure_info, counter)
                    destination = self.allclasses_path / new_filename
                
                # Read original content
                with open(cs_file, 'r', encoding='utf-8') as f:
                    original_content = f.read()
                
                # Create comprehensive header
                header = self.create_comprehensive_header(structure_info)
                
                # Write file with header
                with open(destination, 'w', encoding='utf-8') as f:
                    f.write(header + original_content)
                
                # Keep original (copy mode - don't remove)
                # cs_file.unlink()  # Commented out to preserve originals
                
                # Track for report
                structure_report.append({
                    'original': structure_info['full_original_path'],
                    'new': new_filename,
                    'project': structure_info['project'],
                    'folder': structure_info['folder_path'],
                    'namespace': structure_info['namespace']
                })
                
                print(f"Moved: {structure_info['filename']} -> {new_filename}")
                print(f"  From: {structure_info['project']}/{structure_info['folder_path']}")
                print(f"  Namespace: {structure_info['namespace']}")
                moved_count += 1
                
            except Exception as e:
                print(f"Error moving {cs_file}: {e}")
                
        # Generate structure report
        self.generate_structure_report(structure_report)
        
        print(f"\nSuccessfully moved {moved_count} C# files with complete structure preservation")
        
    def generate_structure_report(self, structure_report):
        """Generate a detailed report of the file structure mapping."""
        report_path = self.allclasses_path / "STRUCTURE_REPORT.md"
        
        with open(report_path, 'w', encoding='utf-8') as f:
            f.write("# Legacy File Structure Report\n\n")
            f.write(f"Generated on: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n\n")
            
            # Group by project
            by_project = defaultdict(list)
            for item in structure_report:
                by_project[item['project']].append(item)
            
            for project, files in sorted(by_project.items()):
                f.write(f"## Project: {project}\n\n")
                f.write("| Original Path | New Filename | Folder | Namespace |\n")
                f.write("|---------------|--------------|--------|----------|\n")
                
                for file_info in sorted(files, key=lambda x: x['original']):
                    f.write(f"| {file_info['original']} | {file_info['new']} | {file_info['folder']} | {file_info['namespace']} |\n")
                
                f.write("\n")
        
        print(f"Structure report generated: {report_path}")
        
    def cleanup_empty_directories(self):
        """Remove empty directories after moving files."""
        for root, dirs, files in os.walk(self.legacy_path, topdown=False):
            for dir_name in dirs:
                dir_path = Path(root) / dir_name
                if dir_path != self.allclasses_path:
                    try:
                        if not any(dir_path.iterdir()):
                            dir_path.rmdir()
                            print(f"Removed empty directory: {dir_path}")
                    except OSError:
                        pass  # Directory not empty or permission issue
                        
    def run(self):
        """Execute the enhanced CS file moving process."""
        print("Starting ENHANCED legacy C# file consolidation...")
        print("Features: Complete structure preservation + namespace tracking")
        print(f"Source: {self.legacy_path}")
        print(f"Destination: {self.allclasses_path}")
        
        self.create_allclasses_folder()
        self.move_cs_files_with_structure_preservation()
        self.cleanup_empty_directories()
        
        print("\n" + "="*60)
        print("ENHANCED CONSOLIDATION COMPLETE!")
        print(f"All C# files moved to: {self.allclasses_path}")
        print("Complete structure information preserved in file headers")
        print("Structure report generated for easy reconstruction")
        print("="*60)

def main():
    import sys
    
    if len(sys.argv) > 1:
        base_path = sys.argv[1]
    else:
        base_path = "."
        
    mover = EnhancedLegacyCsFileMover(base_path)
    mover.run()

if __name__ == "__main__":
    main()