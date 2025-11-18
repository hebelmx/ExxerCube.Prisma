#!/usr/bin/env python3
"""
Detailed failure analysis for SRP refactoring test results.
Extracts specific error patterns and root causes from test logs.
"""

import re
import json
from collections import defaultdict, Counter
from dataclasses import dataclass
from typing import List, Dict, Optional

@dataclass
class FailureDetails:
    test_name: str
    scenario: str
    duration: str
    original_success: bool
    refactored_success: bool
    original_errors: str
    refactored_errors: str
    root_cause: str

def parse_test_errors(file_path: str) -> List[FailureDetails]:
    """Extract detailed failure information from test log."""
    failures = []
    
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Pattern for failed test headers
    failed_pattern = r'failed (.+?)\((.+?)\) \((.+?)\)'
    comparison_pattern = r'Handlers produced different results for scenario: (.+?)\.\s*Original: Success=(\w+), Errors=(.+?)\s*Refactored: Success=(\w+), Errors=(.+?)(?:\s+at|$)'
    
    lines = content.split('\n')
    i = 0
    
    while i < len(lines):
        line = lines[i].strip()
        
        # Look for failed test
        failed_match = re.search(failed_pattern, line)
        if failed_match:
            test_name = failed_match.group(1)
            params = failed_match.group(2)
            duration = failed_match.group(3)
            
            # Look ahead for comparison details
            comparison_found = False
            scenario = "Unknown"
            orig_success = False
            ref_success = False
            orig_errors = ""
            ref_errors = ""
            
            # First try to extract scenario from parameters
            if "scenario:" in params:
                scenario_match = re.search(r'scenario: "([^"]+)"', params)
                if scenario_match:
                    scenario = scenario_match.group(1)
            
            j = i + 1
            while j < min(i + 100, len(lines)):  # Look within next 100 lines
                line_text = lines[j].strip()
                
                # Look for the comparison pattern first
                comparison_match = re.search(comparison_pattern, line_text)
                if comparison_match:
                    scenario = comparison_match.group(1)
                    orig_success = comparison_match.group(2) == 'True'
                    orig_errors = comparison_match.group(3).strip()
                    ref_success = comparison_match.group(4) == 'True'
                    ref_errors = comparison_match.group(5).strip()
                    comparison_found = True
                    break
                
                # Alternative: look for individual result lines
                if "Original: Success=" in line_text:
                    orig_match = re.search(r'Original: Success=(\w+), Errors=(.+?)(?:\s|$)', line_text)
                    if orig_match:
                        orig_success = orig_match.group(1) == 'True'
                        orig_errors = orig_match.group(2).strip()
                
                if "Refactored: Success=" in line_text:
                    ref_match = re.search(r'Refactored: Success=(\w+), Errors=(.+?)(?:\s|$)', line_text)
                    if ref_match:
                        ref_success = ref_match.group(1) == 'True'
                        ref_errors = ref_match.group(2).strip()
                        if orig_errors:  # If we have both, we're done
                            comparison_found = True
                            break
                
                j += 1
            
            if comparison_found or orig_errors or ref_errors:
                # Determine root cause
                root_cause = determine_root_cause(orig_success, ref_success, orig_errors, ref_errors)
                
                failures.append(FailureDetails(
                    test_name=test_name,
                    scenario=scenario,
                    duration=duration,
                    original_success=orig_success,
                    refactored_success=ref_success,
                    original_errors=orig_errors,
                    refactored_errors=ref_errors,
                    root_cause=root_cause
                ))
            else:
                # If no comparison found, it's a different type of failure
                failures.append(FailureDetails(
                    test_name=test_name,
                    scenario="Unknown",
                    duration=duration,
                    original_success=False,
                    refactored_success=False,
                    original_errors="N/A",
                    refactored_errors="N/A",
                    root_cause="Non-comparison failure"
                ))
        
        i += 1
    
    return failures

def determine_root_cause(orig_success: bool, ref_success: bool, orig_errors: str, ref_errors: str) -> str:
    """Determine the root cause of test failure."""
    if orig_success and not ref_success:
        if "Product already exists" in ref_errors:
            return "SRP handler detects existing products (state pollution)"
        elif "Customer not found" in ref_errors:
            return "SRP handler validation stricter"
        elif "Line not found" in ref_errors:
            return "SRP handler validation stricter"
        else:
            return "SRP handler failing where original succeeds"
    elif not orig_success and ref_success:
        return "SRP handler succeeding where original fails"
    elif not orig_success and not ref_success:
        if orig_errors != ref_errors:
            return "Different error messages"
        else:
            return "Both failing with same errors"
    else:
        return "Unknown difference in success cases"

def analyze_patterns(failures: List[FailureDetails]) -> Dict:
    """Analyze failure patterns and provide insights."""
    root_causes = Counter([f.root_cause for f in failures])
    scenarios = Counter([f.scenario for f in failures])
    
    # Group by test class
    test_classes = defaultdict(list)
    for f in failures:
        class_name = f.test_name.split('.')[-2] if '.' in f.test_name else "Unknown"
        test_classes[class_name].append(f)
    
    return {
        "total_failures": len(failures),
        "root_causes": dict(root_causes),
        "scenarios": dict(scenarios),
        "test_classes": {k: len(v) for k, v in test_classes.items()},
        "failures_by_class": {k: [f.test_name.split('.')[-1] for f in v] for k, v in test_classes.items()}
    }

def generate_fix_recommendations(failures: List[FailureDetails]) -> List[str]:
    """Generate specific fix recommendations."""
    recommendations = []
    
    state_pollution_count = sum(1 for f in failures if "state pollution" in f.root_cause)
    if state_pollution_count > 0:
        recommendations.append(f"🔴 CRITICAL: {state_pollution_count} tests failing due to state pollution - products being created in first run, causing 'Product already exists' in subsequent runs")
        recommendations.append("   - Solution: Add test isolation/cleanup between test runs")
        recommendations.append("   - Consider using unique product names per test iteration")
    
    validation_count = sum(1 for f in failures if "validation stricter" in f.root_cause)
    if validation_count > 0:
        recommendations.append(f"🟡 VALIDATION: {validation_count} tests failing due to stricter SRP validation")
        recommendations.append("   - Solution: Update test expectations to match new validation behavior")
    
    return recommendations

def main():
    print("=== SRP REFACTORING DETAILED FAILURE ANALYSIS ===")
    print("=" * 60)
    
    try:
        failures = parse_test_errors(r'F:\Dynamic\IndTraceV2025\Src\Tests\Core\Aggregation.BoundedTests\test_errors.txt')
        
        if not failures:
            print("No detailed failures found in log.")
            return
        
        analysis = analyze_patterns(failures)
        
        print(f"Total detailed failures analyzed: {analysis['total_failures']}")
        print()
        
        print("ROOT CAUSE BREAKDOWN:")
        print("-" * 40)
        for cause, count in analysis['root_causes'].items():
            print(f"  {cause}: {count}")
        print()
        
        print("SCENARIO BREAKDOWN:")
        print("-" * 40)
        for scenario, count in analysis['scenarios'].items():
            print(f"  {scenario}: {count}")
        print()
        
        print("TEST CLASS BREAKDOWN:")
        print("-" * 40)
        for test_class, count in analysis['test_classes'].items():
            print(f"  {test_class}: {count} failures")
        print()
        
        print("DETAILED FAILURES:")
        print("-" * 40)
        for i, failure in enumerate(failures, 1):
            print(f"{i}. {failure.test_name.split('.')[-1]}")
            print(f"   Scenario: {failure.scenario}")
            print(f"   Original: Success={failure.original_success}, Errors='{failure.original_errors}'")
            print(f"   Refactored: Success={failure.refactored_success}, Errors='{failure.refactored_errors}'")
            print(f"   Root Cause: {failure.root_cause}")
            print(f"   Duration: {failure.duration}")
            print()
        
        recommendations = generate_fix_recommendations(failures)
        if recommendations:
            print("FIX RECOMMENDATIONS:")
            print("-" * 40)
            for rec in recommendations:
                print(rec)
            print()
        
        # Save detailed analysis to JSON
        output_data = {
            "analysis": analysis,
            "failures": [
                {
                    "test_name": f.test_name,
                    "scenario": f.scenario,
                    "duration": f.duration,
                    "original_success": f.original_success,
                    "refactored_success": f.refactored_success,
                    "original_errors": f.original_errors,
                    "refactored_errors": f.refactored_errors,
                    "root_cause": f.root_cause
                }
                for f in failures
            ],
            "recommendations": recommendations
        }
        
        with open(r'F:\Dynamic\IndTraceV2025\detailed_failure_analysis.json', 'w') as f:
            json.dump(output_data, f, indent=2)
        
        print("Detailed analysis saved to: detailed_failure_analysis.json")
        
    except Exception as e:
        print(f"Error analyzing failures: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    main()