#!/usr/bin/env python3
"""
Find potential XUnit1051 issues by scanning code directly
"""

import os
import re
from pathlib import Path

def scan_cs_files():
    """Scan C# files for patterns that might trigger XUnit1051"""
    patterns_found = []
    
    # Find all .cs files in test directories
    test_dirs = [
        "Src/Tests"
    ]
    
    cs_files = []
    for test_dir in test_dirs:
        if os.path.exists(test_dir):
            for root, dirs, files in os.walk(test_dir):
                for file in files:
                    if file.endswith('.cs'):
                        cs_files.append(os.path.join(root, file))
    
    print(f"Scanning {len(cs_files)} C# test files...")
    
    # Patterns that commonly trigger XUnit1051
    problematic_patterns = [
        (r'CancellationToken\.None', 'CancellationToken.None usage'),
        (r'new\s+CancellationToken\s*\(\s*\)', 'new CancellationToken() usage'),
        (r'default\(CancellationToken\)', 'default(CancellationToken) usage'),
        (r'await\s+\w+.*Async\s*\([^)]*\)\s*;', 'await async calls (potential token needed)'),
        (r'\.WaitAsync\s*\(\s*\)', 'WaitAsync without token'),
        (r'\.ConfigureAwait\s*\(\s*false\s*\)', 'ConfigureAwait patterns'),
    ]
    
    for file_path in cs_files:
        try:
            with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
                content = f.read()
                lines = content.split('\n')
            
            # Check for test methods (methods that might need XUnit1051 fixes)
            is_test_file = '[Fact]' in content or '[Theory]' in content
            
            if is_test_file:
                for i, line in enumerate(lines, 1):
                    for pattern, description in problematic_patterns:
                        if re.search(pattern, line, re.IGNORECASE):
                            patterns_found.append({
                                'file': file_path,
                                'line': i,
                                'pattern': description,
                                'code': line.strip()
                            })
                            
        except Exception as e:
            print(f"Error reading {file_path}: {e}")
    
    return patterns_found

def main():
    print("Scanning for potential XUnit1051 issues...")
    print("=" * 60)
    
    patterns = scan_cs_files()
    
    if not patterns:
        print("No potential XUnit1051 issues found!")
        print("\nThis could mean:")
        print("1. XUnit analyzers are not enabled")
        print("2. Code doesn't use async patterns that trigger XUnit1051")
        print("3. Issues have already been fixed")
        return
    
    # Group by pattern type
    by_pattern = {}
    for item in patterns:
        pattern_type = item['pattern']
        if pattern_type not in by_pattern:
            by_pattern[pattern_type] = []
        by_pattern[pattern_type].append(item)
    
    print(f"Found {len(patterns)} potential issues:")
    print()
    
    for pattern_type, items in by_pattern.items():
        print(f"{pattern_type}: {len(items)} occurrences")
        
        # Show first few examples
        for item in items[:3]:
            print(f"  {item['file']}:{item['line']}")
            print(f"    {item['code']}")
        
        if len(items) > 3:
            print(f"  ... and {len(items) - 3} more")
        print()

if __name__ == "__main__":
    main()