#!/usr/bin/env python3
"""
Protocol-Compliant Timeout Addition Script
Applies timeout attributes to tests without timeouts according to CodeAnalysysForFailingTestV2.md
ONLY PERMITTED CHANGE per protocol requirements
"""

import os
import re
import sys
from pathlib import Path
from typing import List, Dict

def find_test_files(base_path: str) -> List[Path]:
    """Find all C# test files in the specified path"""
    base = Path(base_path)
    test_files = []
    
    # Look for test files
    for pattern in ['*Tests.cs', '*Test.cs', '*TestCase.cs']:
        test_files.extend(base.glob(f'**/{pattern}'))
    
    return test_files

def add_timeouts_to_file(file_path: Path) -> Dict:
    """Add timeout attributes to tests missing them"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
    except Exception as e:
        return {'error': str(e), 'changes': 0}
    
    original_content = content
    changes_made = 0
    
    # Find [Fact] attributes without Timeout
    fact_pattern = r'\[Fact\](?!\(Timeout)'
    matches = list(re.finditer(fact_pattern, content))
    
    # Replace from end to beginning to avoid offset issues
    for match in reversed(matches):
        old_attr = match.group(0)
        new_attr = '[Fact(Timeout = 30_000)] // Added timeout to prevent hanging - Author: Claude'
        content = content[:match.start()] + new_attr + content[match.end():]
        changes_made += 1
    
    # Find [Theory] attributes without Timeout
    theory_pattern = r'\[Theory\](?!\(Timeout)'
    matches = list(re.finditer(theory_pattern, content))
    
    # Replace from end to beginning to avoid offset issues
    for match in reversed(matches):
        old_attr = match.group(0)
        new_attr = '[Theory(Timeout = 30_000)] // Added timeout to prevent hanging - Author: Claude'
        content = content[:match.start()] + new_attr + content[match.end():]
        changes_made += 1
    
    # Write file if changes were made
    if changes_made > 0:
        try:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(content)
        except Exception as e:
            return {'error': f'Failed to write file: {e}', 'changes': 0}
    
    return {'changes': changes_made}

def main():
    if len(sys.argv) != 2:
        print("Usage: python apply_timeouts.py <test_directory>")
        sys.exit(1)
    
    test_directory = sys.argv[1]
    print(f"PROTOCOL COMPLIANCE: Adding timeouts per CodeAnalysysForFailingTestV2.md")
    print(f"Target Directory: {test_directory}")
    
    test_files = find_test_files(test_directory)
    print(f"Found {len(test_files)} test files to process")
    
    total_changes = 0
    files_modified = 0
    
    for file_path in test_files:
        result = add_timeouts_to_file(file_path)
        
        if result.get('error'):
            print(f"ERROR processing {file_path}: {result['error']}")
            continue
        
        changes = result.get('changes', 0)
        if changes > 0:
            print(f"SUCCESS {file_path.name}: Added timeout to {changes} tests")
            total_changes += changes
            files_modified += 1
        else:
            print(f"SKIP {file_path.name}: No changes needed")
    
    print(f"\nSUMMARY:")
    print(f"Files modified: {files_modified}")
    print(f"Total timeout attributes added: {total_changes}")
    print(f"PROTOCOL COMPLIANCE: Only timeout additions made per requirements")

if __name__ == '__main__':
    main()