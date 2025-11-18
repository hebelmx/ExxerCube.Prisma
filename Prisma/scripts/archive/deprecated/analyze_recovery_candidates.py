#!/usr/bin/env python3
"""
Recovery Candidates Detailed Analysis
Breaks down what the 625 "unique recovery candidates" actually are
"""

import json
from collections import defaultdict, Counter
from typing import Dict, List

def analyze_recovery_candidates():
    """Analyze what the recovery candidates actually represent."""
    
    with open('method_duplicate_analysis_fast_20251031_104145.json', 'r') as f:
        data = json.load(f)
    
    unique_methods = data['unique_backup_methods']
    high_priority = [m for m in unique_methods if m['is_high_priority']]
    
    print("🔍 DETAILED RECOVERY CANDIDATES ANALYSIS")
    print("=" * 60)
    print(f"Total Recovery Candidates: {len(unique_methods)}")
    print(f"High Priority: {len(high_priority)}")
    print()
    
    # 1. Analysis by Test Classes
    print("📋 1. BREAKDOWN BY TEST CLASSES")
    print("-" * 40)
    
    class_groups = defaultdict(list)
    for method in unique_methods:
        for instance in method['backup_instances']:
            class_name = instance['className']
            class_groups[class_name].append(method['backup_method'])
    
    print(f"Number of distinct test classes: {len(class_groups)}")
    print(f"Average methods per class: {len(unique_methods) / len(class_groups):.1f}")
    print()
    
    # Show classes with most methods
    print("Top 10 classes by method count:")
    for class_name, methods in sorted(class_groups.items(), key=lambda x: len(x[1]), reverse=True)[:10]:
        print(f"  {class_name}: {len(methods)} methods")
    print()
    
    # 2. Analysis by Project/Namespace
    print("📁 2. BREAKDOWN BY PROJECT/NAMESPACE")
    print("-" * 40)
    
    project_groups = defaultdict(list)
    namespace_groups = defaultdict(list)
    
    for method in unique_methods:
        for instance in method['backup_instances']:
            project = instance.get('project', 'Unknown')
            namespace = instance.get('namespace', 'Unknown')
            project_groups[project].append(method['backup_method'])
            namespace_groups[namespace].append(method['backup_method'])
    
    print("By Project:")
    for project, methods in sorted(project_groups.items(), key=lambda x: len(x[1]), reverse=True)[:10]:
        print(f"  {project}: {len(methods)} methods")
    print()
    
    print("By Namespace (top 10):")
    for namespace, methods in sorted(namespace_groups.items(), key=lambda x: len(x[1]), reverse=True)[:10]:
        print(f"  {namespace}: {len(methods)} methods")
    print()
    
    # 3. Analysis by Test Patterns
    print("🎭 3. BREAKDOWN BY TEST PATTERNS")
    print("-" * 40)
    
    pattern_counter = Counter()
    for method in unique_methods:
        for instance in method['backup_instances']:
            pattern = instance.get('testPattern', 'Unknown')
            pattern_counter[pattern] += 1
    
    print("Test patterns distribution:")
    for pattern, count in pattern_counter.most_common():
        percentage = (count / len(unique_methods)) * 100
        print(f"  {pattern}: {count} methods ({percentage:.1f}%)")
    print()
    
    # 4. Keyword Analysis
    print("🔍 4. METHOD NAME KEYWORD ANALYSIS")
    print("-" * 40)
    
    keywords = defaultdict(int)
    for method in unique_methods:
        method_name = method['backup_method'].lower()
        
        # Extract meaningful keywords
        key_terms = [
            'test', 'should', 'when', 'given', 'async', 'integration', 'system', 
            'performance', 'unit', 'service', 'repository', 'controller', 'api',
            'database', 'entity', 'model', 'create', 'update', 'delete', 'get',
            'validate', 'process', 'handle', 'execute', 'calculate', 'generate',
            'parse', 'convert', 'transform', 'analyze', 'search', 'filter'
        ]
        
        for term in key_terms:
            if term in method_name:
                keywords[term] += 1
    
    print("Top keywords in method names:")
    for keyword, count in sorted(keywords.items(), key=lambda x: x[1], reverse=True)[:15]:
        percentage = (count / len(unique_methods)) * 100
        print(f"  '{keyword}': {count} methods ({percentage:.1f}%)")
    print()
    
    # 5. High Priority Analysis
    print("🎯 5. HIGH PRIORITY BREAKDOWN")
    print("-" * 40)
    
    hp_class_groups = defaultdict(list)
    hp_pattern_counter = Counter()
    
    for method in high_priority:
        for instance in method['backup_instances']:
            class_name = instance['className']
            pattern = instance.get('testPattern', 'Unknown')
            hp_class_groups[class_name].append(method['backup_method'])
            hp_pattern_counter[pattern] += 1
    
    print(f"High priority methods come from {len(hp_class_groups)} test classes:")
    for class_name, methods in sorted(hp_class_groups.items(), key=lambda x: len(x[1]), reverse=True)[:10]:
        print(f"  {class_name}: {len(methods)} high-priority methods")
    print()
    
    print("High priority by test pattern:")
    for pattern, count in hp_pattern_counter.most_common():
        percentage = (count / len(high_priority)) * 100
        print(f"  {pattern}: {count} methods ({percentage:.1f}%)")
    print()
    
    # 6. Specific Examples
    print("📝 6. SAMPLE METHOD NAMES BY CATEGORY")
    print("-" * 40)
    
    # Integration tests
    integration_methods = [
        m['backup_method'] for m in unique_methods 
        if 'integration' in m['backup_method'].lower()
    ]
    
    print(f"Integration Tests ({len(integration_methods)} found):")
    for method in integration_methods[:5]:
        print(f"  - {method}")
    print()
    
    # System tests
    system_methods = [
        m['backup_method'] for m in unique_methods 
        if 'system' in m['backup_method'].lower()
    ]
    
    print(f"System Tests ({len(system_methods)} found):")
    for method in system_methods[:5]:
        print(f"  - {method}")
    print()
    
    # BDD tests
    bdd_methods = []
    for method in unique_methods:
        for instance in method['backup_instances']:
            if instance.get('testPattern') in ['BDD_Should', 'BDD_When', 'BDD_Given']:
                bdd_methods.append(method['backup_method'])
                break
    
    print(f"BDD Tests ({len(bdd_methods)} found):")
    for method in bdd_methods[:5]:
        print(f"  - {method}")
    print()
    
    # 7. Potential Consolidation Analysis
    print("🔄 7. CONSOLIDATION OPPORTUNITIES")
    print("-" * 40)
    
    # Look for similar method name patterns
    method_prefixes = defaultdict(list)
    for method in unique_methods:
        method_name = method['backup_method']
        # Extract prefix (first part before underscore or camelCase)
        prefix = method_name.split('_')[0] if '_' in method_name else method_name[:20]
        method_prefixes[prefix.lower()].append(method_name)
    
    # Find groups that might be variations of the same test
    large_groups = {k: v for k, v in method_prefixes.items() if len(v) > 5}
    
    print(f"Found {len(large_groups)} method prefix groups with >5 methods each:")
    for prefix, methods in sorted(large_groups.items(), key=lambda x: len(x[1]), reverse=True)[:10]:
        print(f"  '{prefix}*': {len(methods)} methods")
        for method in methods[:3]:  # Show first 3 examples
            print(f"    - {method}")
        if len(methods) > 3:
            print(f"    ... and {len(methods) - 3} more")
        print()
    
    # Summary
    print("📊 SUMMARY & RECOMMENDATIONS")
    print("=" * 60)
    print(f"✅ {len(unique_methods)} recovery candidates represent individual TEST METHODS")
    print(f"✅ These come from {len(class_groups)} distinct TEST CLASSES")
    print(f"✅ High priority: {len(high_priority)} methods from {len(hp_class_groups)} classes")
    print()
    print("🎯 SUGGESTED APPROACH:")
    print("1. Focus on HIGH PRIORITY methods first (102 methods)")
    print("2. Prioritize by class - recover entire test classes rather than individual methods")
    print("3. Start with Integration/System tests for maximum coverage impact")
    print("4. Consider consolidating similar method patterns")
    print()

if __name__ == "__main__":
    analyze_recovery_candidates()