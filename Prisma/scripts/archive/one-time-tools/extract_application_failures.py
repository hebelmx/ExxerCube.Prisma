#!/usr/bin/env python3
"""
Extract failing Application test names and group them by common patterns for batch fixing.
"""
import re
import sys
from collections import defaultdict

def extract_failing_tests_from_output(test_output):
    """Extract failing test information from dotnet test output"""
    failing_tests = []
    test_errors = {}
    
    # Find failed test patterns in test output
    lines = test_output.split('\n')
    
    for i, line in enumerate(lines):
        # Look for Failed test patterns
        if 'Failed:' in line or 'Error Message:' in line or line.strip().startswith('ExxerAI.Application.Tests'):
            # Try to extract test name
            test_match = re.search(r'(ExxerAI\.Application\.Tests\.[^\s\[]+)', line)
            if test_match:
                test_name = test_match.group(1)
                failing_tests.append(test_name)
                
                # Look for error details in subsequent lines
                error_lines = []
                for j in range(i+1, min(i+5, len(lines))):
                    if lines[j].strip() and not lines[j].startswith(' '):
                        break
                    error_lines.append(lines[j].strip())
                
                if error_lines:
                    test_errors[test_name] = ' '.join(error_lines)
    
    return list(set(failing_tests)), test_errors

def categorize_by_common_patterns(test_names):
    """Group tests by common root causes for batch fixing"""
    categories = defaultdict(list)
    
    for test in test_names:
        # Extract patterns for clustering
        test_lower = test.lower()
        
        if 'conceptextractionengine' in test_lower:
            if 'enhanced' in test_lower or 'extract' in test_lower:
                categories['ConceptExtractionEngine - Enhanced Tests'].append(test)
            else:
                categories['ConceptExtractionEngine - General'].append(test)
        elif 'documentprocessingengine' in test_lower:
            categories['DocumentProcessingEngine'].append(test)
        elif 'logginginterceptor' in test_lower:
            categories['LoggingInterceptor'].append(test)
        elif 'healthcheck' in test_lower:
            categories['HealthCheck'].append(test)
        elif 'workflow' in test_lower:
            categories['Workflow Services'].append(test)
        elif 'humanreview' in test_lower:
            categories['Human Review'].append(test)
        else:
            # Try to extract service name
            parts = test.split('.')
            for part in parts:
                if part.endswith('Tests') or part.endswith('Service'):
                    categories[f'Other - {part}'].append(test)
                    break
            else:
                categories['Uncategorized'].append(test)
    
    return categories

def suggest_batch_fixes(categories, test_errors):
    """Suggest batches for systematic fixing"""
    print("\n" + "="*80)
    print("🎯 BATCH FIXING RECOMMENDATIONS")
    print("="*80)
    
    batch_suggestions = []
    
    # Prioritize categories by size and complexity
    for category, tests in sorted(categories.items(), key=lambda x: len(x[1]), reverse=True):
        if len(tests) >= 3:  # Batches of 3+ for efficiency
            batch_suggestions.append({
                'category': category,
                'tests': tests,
                'count': len(tests),
                'priority': 'HIGH' if 'ConceptExtractionEngine' in category else 'MEDIUM'
            })
    
    # Print batch recommendations
    for i, batch in enumerate(batch_suggestions[:4], 1):  # Top 4 batches
        print(f"\n🔨 BATCH {i}: {batch['category']} ({batch['count']} tests) - {batch['priority']} PRIORITY")
        print("-" * 60)
        
        for test in batch['tests'][:5]:  # Show first 5 tests
            test_short = test.split('.')[-1]  # Just the method name
            print(f"  • {test_short}")
            
            if test in test_errors:
                error = test_errors[test][:100] + "..." if len(test_errors[test]) > 100 else test_errors[test]
                print(f"    ⚠️  {error}")
        
        if len(batch['tests']) > 5:
            print(f"  ... and {len(batch['tests']) - 5} more tests")
        
        # Suggest root cause
        if 'ConceptExtractionEngine' in batch['category']:
            print(f"  🎯 LIKELY ROOT CAUSE: ConceptExtractionEngine logic issues")
            print(f"  📁 FILE: code/src/Core/ExxerAI.Application/Services/ConceptExtractionEngine.cs")
        elif 'DocumentProcessingEngine' in batch['category']:
            print(f"  🎯 LIKELY ROOT CAUSE: DocumentProcessingEngine mock setup issues")
            print(f"  📁 FILE: code/src/tests/ExxerAI.Application.Tests/Services/DocumentServices/")
    
    return batch_suggestions

if __name__ == "__main__":
    # This script expects test output to be piped to it or provided as argument
    if len(sys.argv) > 1:
        test_output = sys.argv[1]
    else:
        print("Please provide test output as argument or pipe it to this script")
        print("Usage: python extract_application_failures.py \"<test_output>\"")
        print("   Or: dotnet test | python extract_application_failures.py")
        sys.exit(1)
    
    print("🔍 ANALYZING APPLICATION TEST FAILURES")
    print("="*80)
    
    failing_tests, test_errors = extract_failing_tests_from_output(test_output)
    
    if not failing_tests:
        print("❌ No failing tests found in output")
        sys.exit(0)
    
    print(f"📊 Total failing tests found: {len(failing_tests)}")
    
    categories = categorize_by_common_patterns(failing_tests)
    
    print(f"\n📋 Tests categorized into {len(categories)} groups:")
    for category, tests in sorted(categories.items(), key=lambda x: len(x[1]), reverse=True):
        print(f"  • {category}: {len(tests)} tests")
    
    # Generate batch fixing suggestions
    batch_suggestions = suggest_batch_fixes(categories, test_errors)
    
    print(f"\n✅ Generated {len(batch_suggestions)} batch recommendations for systematic fixing")
    print("🚀 Start with BATCH 1 for maximum impact!")