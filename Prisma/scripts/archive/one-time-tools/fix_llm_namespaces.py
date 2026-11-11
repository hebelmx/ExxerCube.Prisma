#!/usr/bin/env python3
"""
Fix namespace issues in LLM files - change ExxerAi to ExxerAI
"""

import os
from pathlib import Path

def fix_namespace_in_file(file_path: Path):
    """Fix namespace in a single file."""
    try:
        content = file_path.read_text(encoding='utf-8')
        
        # Replace the namespace
        fixed_content = content.replace('namespace ExxerAi.Axioms.Models.LLM', 'namespace ExxerAI.Axioms.Models.LLM')
        
        if fixed_content != content:
            file_path.write_text(fixed_content, encoding='utf-8')
            print(f"Fixed: {file_path.name}")
            return True
        return False
    except Exception as e:
        print(f"Error fixing {file_path}: {e}")
        return False

def main():
    base_path = Path("F:/Dynamic/ExxerAi/ExxerAI")
    llm_path = base_path / "code/src/Core/ExxerAi.Axioms/Models/LLM"
    
    if not llm_path.exists():
        print(f"LLM directory not found: {llm_path}")
        return
    
    fixed_count = 0
    for cs_file in llm_path.glob("*.cs"):
        if fix_namespace_in_file(cs_file):
            fixed_count += 1
    
    print(f"\nFixed {fixed_count} files")

if __name__ == "__main__":
    main()