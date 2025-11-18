#!/usr/bin/env python3
import subprocess
import re
from collections import defaultdict

def main():
    print("=== AGGREGATION.BOUNDEDTESTS FAILURE ANALYSIS ===")
    print("=" * 60)
    
    # Use strings command to extract failing tests
    cmd = [
        'strings', 
        'Src/Tests/Core/Aggregation.BoundedTests/bin/Debug/net10.0/TestResults/IndTrace.Aggregation.BoundedTests_net10.0_x64.log'
    ]
    
    try:
        result = subprocess.run(cmd, capture_output=True, text=True, cwd='F:/Dynamic/IndTraceV2025')
        if result.returncode != 0:
            print(f"Error running strings: {result.stderr}")
            return
            
        lines = result.stdout.splitlines()
        failing_tests = [line for line in lines if 'failed' in line.lower() and 'IndTrace.Aggregation.BoundedTests' in line]
        
        print(f"Total failing tests found: {len(failing_tests)}")
        print("=" * 60)
        
        # Categorize by namespace/area
        categories = defaultdict(list)
        detailed_failures = []
        matched_count = 0
        
        for test_line in failing_tests:
            # Extract test name and timing
            match = re.match(r'failed\s+(IndTrace\.Aggregation\.BoundedTests\.([^.]+)\.([^.]+)\.([^.]+)\.([^(]+))', test_line, re.IGNORECASE)
            if match:
                matched_count += 1
                full_name = match.group(1)
                area = match.group(2)  # e.g., Products, WorkFlows, etc.
                type_dir = match.group(3)  # Commands, Queries, etc.
                test_class = match.group(4)
                test_method = match.group(5)
                
                category_key = f"{area}.{type_dir}"
                categories[category_key].append({
                    'full_name': full_name,
                    'test_class': test_class,
                    'test_method': test_method,
                    'raw_line': test_line
                })
            else:
                print(f"DEBUG: Unmatched failed line: {test_line}")
        
        # Print categorized results
        print("\nFAILURES BY CATEGORY:")
        print("-" * 40)
        
        for category, tests in sorted(categories.items()):
            print(f"\n[{category}] - {len(tests)} failures:")
            for test in tests:
                duration_match = re.search(r'\(([^)]+)\)$', test['raw_line'])
                duration = duration_match.group(1) if duration_match else "N/A"
                print(f"  - {test['test_class']}.{test['test_method']} ({duration})")
        
        # Summary statistics
        print(f"\n" + "=" * 60)
        print("SUMMARY STATISTICS:")
        print(f"  Total failures: {matched_count}")  # Use actual test failures, not raw failed lines
        print(f"  Categories affected: {len(categories)}")
        
        # Most affected areas
        category_counts = [(cat, len(tests)) for cat, tests in categories.items()]
        category_counts.sort(key=lambda x: x[1], reverse=True)
        
        print(f"\nMOST AFFECTED AREAS:")
        for cat, count in category_counts[:5]:
            print(f"  {cat}: {count} failures")
        
        # Look for common patterns
        print(f"\nCOMMON FAILURE PATTERNS:")
        method_patterns = defaultdict(int)
        for category, tests in categories.items():
            for test in tests:
                method_patterns[test['test_method']] += 1
        
        common_methods = [(method, count) for method, count in method_patterns.items() if count > 1]
        common_methods.sort(key=lambda x: x[1], reverse=True)
        
        for method, count in common_methods[:5]:
            print(f"  {method}: {count} occurrences")
            
        print(f"\n" + "=" * 60)
        
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    main()