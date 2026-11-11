#!/usr/bin/env python3
"""
Safe Test Timeout Addition Script
Adds [Fact(Timeout = 30_000)] to tests missing timeout attributes
Supports dry-run mode for safety validation
"""

import os
import re
import argparse
from pathlib import Path
from typing import List, Tuple, Dict
import json
from datetime import datetime

class TestTimeoutAdder:
    def __init__(self, dry_run: bool = True, timeout_ms: int = 30_000):
        self.dry_run = dry_run
        self.timeout_ms = timeout_ms
        self.changes = []
        self.stats = {
            'files_processed': 0,
            'tests_found': 0,
            'tests_with_timeout': 0,
            'tests_needing_timeout': 0,
            'changes_applied': 0
        }
    
    def find_test_files(self, base_path: str) -> List[Path]:
        """Find all C# test files in the specified path"""
        base = Path(base_path)
        test_files = []
        
        # Look for test files
        for pattern in ['*Tests.cs', '*Test.cs', '*TestCase.cs']:
            test_files.extend(base.glob(f'**/{pattern}'))
        
        return test_files
    
    def analyze_test_file(self, file_path: Path) -> Dict:
        """Analyze a test file for timeout patterns"""
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()
        except Exception as e:
            return {'error': str(e)}
        
        analysis = {
            'file': str(file_path),
            'tests': [],
            'has_using_xunit': 'using Xunit' in content,
            'total_tests': 0,
            'tests_with_timeout': 0,
            'tests_needing_timeout': 0
        }
        
        # Find test methods with their attributes
        test_pattern = r'(\s*(?:\[[\w\s,()=._"]*\]\s*)*)\s*public\s+(?:async\s+)?(?:Task|void)\s+(\w+)\s*\('
        matches = re.finditer(test_pattern, content, re.MULTILINE)
        
        for match in matches:
            attributes = match.group(1)
            method_name = match.group(2)
            
            # Check if this looks like a test method
            is_test = any(attr in attributes for attr in ['[Fact', '[Theory', '[Test'])
            
            if is_test:
                analysis['total_tests'] += 1
                has_timeout = 'Timeout' in attributes
                
                test_info = {
                    'name': method_name,
                    'attributes': attributes.strip(),
                    'has_timeout': has_timeout,
                    'line_start': content[:match.start()].count('\n') + 1
                }
                
                analysis['tests'].append(test_info)
                
                if has_timeout:
                    analysis['tests_with_timeout'] += 1
                else:
                    analysis['tests_needing_timeout'] += 1
        
        return analysis
    
    def generate_timeout_attribute(self, existing_attributes: str) -> str:
        """Generate the appropriate timeout attribute"""
        if '[Fact' in existing_attributes and 'Timeout' not in existing_attributes:
            # Replace [Fact] with [Fact(Timeout = 30_000)]
            return re.sub(r'\[Fact\]', f'[Fact(Timeout = {self.timeout_ms})]', existing_attributes)
        elif '[Theory' in existing_attributes and 'Timeout' not in existing_attributes:
            # Replace [Theory] with [Theory(Timeout = 30_000)]
            return re.sub(r'\[Theory\]', f'[Theory(Timeout = {self.timeout_ms})]', existing_attributes)
        else:
            return existing_attributes
    
    def apply_timeouts_to_file(self, file_path: Path, analysis: Dict) -> Dict:
        """Apply timeout attributes to a file"""
        if analysis.get('error') or analysis['tests_needing_timeout'] == 0:
            return {'applied': 0, 'skipped': True}
        
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()
        except Exception as e:
            return {'error': str(e)}
        
        original_content = content
        changes_made = 0
        
        # Process each test that needs timeout
        for test in analysis['tests']:
            if not test['has_timeout']:
                old_attrs = test['attributes']
                new_attrs = self.generate_timeout_attribute(old_attrs)
                
                if new_attrs != old_attrs:
                    # Add author comment
                    comment = f" // Added timeout to prevent hanging - Author: Claude"
                    new_attrs_with_comment = new_attrs + comment
                    
                    content = content.replace(old_attrs, new_attrs_with_comment, 1)
                    changes_made += 1
                    
                    self.changes.append({
                        'file': str(file_path),
                        'test': test['name'],
                        'old': old_attrs,
                        'new': new_attrs_with_comment
                    })
        
        # Write file if not dry run
        if not self.dry_run and changes_made > 0:
            try:
                with open(file_path, 'w', encoding='utf-8') as f:
                    f.write(content)
            except Exception as e:
                return {'error': f'Failed to write file: {e}'}
        
        return {'applied': changes_made, 'skipped': False}
    
    def process_directory(self, base_path: str) -> Dict:
        """Process all test files in a directory"""
        test_files = self.find_test_files(base_path)
        
        print(f"Found {len(test_files)} test files to process")
        
        for file_path in test_files:
            print(f"Processing: {file_path}")
            
            # Analyze file
            analysis = self.analyze_test_file(file_path)
            if analysis.get('error'):
                print(f"  Error: {analysis['error']}")
                continue
            
            self.stats['files_processed'] += 1
            self.stats['tests_found'] += analysis['total_tests']
            self.stats['tests_with_timeout'] += analysis['tests_with_timeout']
            self.stats['tests_needing_timeout'] += analysis['tests_needing_timeout']
            
            print(f"  Tests: {analysis['total_tests']}, With timeout: {analysis['tests_with_timeout']}, Need timeout: {analysis['tests_needing_timeout']}")
            
            # Apply timeouts
            if analysis['tests_needing_timeout'] > 0:
                result = self.apply_timeouts_to_file(file_path, analysis)
                if result.get('error'):
                    print(f"  Error applying timeouts: {result['error']}")
                else:
                    applied = result.get('applied', 0)
                    self.stats['changes_applied'] += applied
                    if applied > 0:
                        mode = "DRY RUN" if self.dry_run else "APPLIED"
                        print(f"  {mode}: Added timeout to {applied} tests")
        
        return self.stats
    
    def save_report(self, output_path: str):
        """Save detailed report of changes"""
        report = {
            'timestamp': datetime.now().isoformat(),
            'dry_run': self.dry_run,
            'timeout_ms': self.timeout_ms,
            'statistics': self.stats,
            'changes': self.changes
        }
        
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2, ensure_ascii=False)
        
        print(f"Report saved to: {output_path}")

def main():
    parser = argparse.ArgumentParser(description='Add timeout attributes to test methods')
    parser.add_argument('path', help='Path to test directory')
    parser.add_argument('--timeout', type=int, default=30000, help='Timeout in milliseconds (default: 30000)')
    parser.add_argument('--dry-run', action='store_true', default=True, help='Dry run mode (default: true)')
    parser.add_argument('--execute', action='store_true', help='Actually apply changes (disables dry-run)')
    parser.add_argument('--report', default='timeout_report.json', help='Report output file')
    
    args = parser.parse_args()
    
    # Safety: dry-run is default unless --execute is specified
    dry_run = not args.execute
    
    if not dry_run:
        print("⚠️  EXECUTING MODE: Changes will be applied to files!")
        response = input("Are you sure? Type 'yes' to continue: ")
        if response.lower() != 'yes':
            print("Cancelled.")
            return
    else:
        print("🔍 DRY RUN MODE: No files will be modified")
    
    adder = TestTimeoutAdder(dry_run=dry_run, timeout_ms=args.timeout)
    stats = adder.process_directory(args.path)
    
    print("\n📊 SUMMARY:")
    print(f"Files processed: {stats['files_processed']}")
    print(f"Total tests found: {stats['tests_found']}")
    print(f"Tests with timeout: {stats['tests_with_timeout']}")
    print(f"Tests needing timeout: {stats['tests_needing_timeout']}")
    print(f"Changes {'applied' if not dry_run else 'planned'}: {stats['changes_applied']}")
    
    # Save report
    adder.save_report(args.report)
    
    if dry_run and stats['changes_applied'] > 0:
        print(f"\n💡 To apply changes, run with --execute flag")

if __name__ == '__main__':
    main()