#!/usr/bin/env python3
"""
Extract failing test names from the Aggregation.BoundedTests log file.
The log file has a unique encoding where characters are spaced with spaces.
"""
import re
import sys
from collections import defaultdict

def normalize_spaced_text(text):
    """Convert spaced text like 'f a i l e d' to 'failed'"""
    # Remove extra spaces between single characters
    normalized = re.sub(r'(\b\w)\s+(?=\w\s)', r'\1', text)
    # Clean up remaining multiple spaces
    normalized = re.sub(r'\s+', ' ', normalized)
    return normalized

def extract_failing_tests(log_file_path):
    """Extract failing test information from the log file"""
    failing_tests = []
    test_errors = {}
    
    try:
        with open(log_file_path, 'r', encoding='utf-8', errors='ignore') as f:
            content = f.read()
            
        # Normalize the spaced text
        normalized_content = normalize_spaced_text(content)ij
        
        # Find all failed test patterns
        # Pattern: "failed IndTrace.Aggregation.BoundedTests...." 
        failed_pattern = r'failed\s+(IndTrace\.Aggregation\.BoundedTests\.[^\s]+\.[^\s]+)\s*\([^)]*\)'
        matches = re.findall(failed_pattern, normalized_content, re.IGNORECASE)
        
        for match in matches:
            failing_tests.append(match)
            
        # Also look for ShouldAssertException patterns to get error details
        error_pattern = r'(IndTrace\.Aggregation\.BoundedTests\.[^\s]+\.[^\s]+).*?ShouldAssertException\s*:\s*([^\n]+)'
        error_matches = re.findall(error_pattern, normalized_content, re.DOTALL | re.IGNORECASE)
        
        for test_name, error_msg in error_matches:
            test_errors[test_name] = error_msg.strip()
            
    except Exception as e:
        print(f"Error reading log file: {e}")
        return [], {}
        
    return failing_tests, test_errors

def categorize_tests(test_names):
    """Categorize tests by namespace/class"""
    categories = defaultdict(list)
    
    for test in test_names:
        # Extract the category from the test name
        # Format: IndTrace.Aggregation.BoundedTests.Category.SubCategory.TestClass.TestMethod
        parts = test.split('.')
        if len(parts) >= 5:
            category = parts[3]  # The main category (e.g., WorkFlows, Products, Registers)
            subcategory = parts[4] if len(parts) > 4 else ""
            test_class = parts[5] if len(parts) > 5 else ""
            
            categories[f"{category}.{subcategory}"].append({
                'full_name': test,
                'class': test_class,
                'method': parts[-1] if len(parts) > 6 else ""
            })
    
    return categories

def main():
    log_file = r"F:\Dynamic\IndTraceV2025\Src\Tests\Core\Aggregation.BoundedTests\bin\Debug\net10.0\TestResults\IndTrace.Aggregation.BoundedTests_net10.0_x64.log"
    
    print("[*] Analyzing Aggregation.BoundedTests failures...")
    print("=" * 60)
    
    failing_tests, test_errors = extract_failing_tests(log_file)
    
    if not failing_tests:
        print("[X] No failing tests found or unable to parse log file")
        return
        
    print(f"[+] Total failing tests found: {len(failing_tests)}")
    print()
    
    # Categorize the failures
    categories = categorize_tests(failing_tests)
    
    print("[+] Failing tests by category:")
    print("=" * 60)
    
    total_shown = 0
    for category, tests in sorted(categories.items()):
        print(f"\n[*] {category} ({len(tests)} failures)")
        print("-" * 40)
        
        for test_info in tests:
            test_name = test_info['full_name']
            print(f"  [X] {test_info['class']}.{test_info['method']}")
            
            # Show error if available
            if test_name in test_errors:
                error = test_errors[test_name][:100] + "..." if len(test_errors[test_name]) > 100 else test_errors[test_name]
                print(f"     [!] {error}")
                
            total_shown += 1
    
    print(f"\n[+] Summary:")
    print(f"  - Total failures: {len(failing_tests)}")
    print(f"  - Categories: {len(categories)}")
    print(f"  - Most affected: {max(categories.items(), key=lambda x: len(x[1]))[0]} ({max(len(tests) for tests in categories.values())} failures)")
    
    # Look for common error patterns
    print(f"\n[*] Common error patterns:")
    error_types = defaultdict(int)
    for error in test_errors.values():
        if "result.IsSuccess should be True but was False" in error:
            error_types["Result.IsSuccess = False"] += 1
        elif "ShouldAssertException" in error:
            error_types["Assertion failure"] += 1
        else:
            error_types["Other"] += 1
            
    for error_type, count in error_types.items():
        print(f"  - {error_type}: {count} occurrences")

if __name__ == "__main__":
    main()