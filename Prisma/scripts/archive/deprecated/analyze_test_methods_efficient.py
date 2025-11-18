#!/usr/bin/env python3
"""
Efficient Test Methods Analyzer
Focuses specifically on test methods for faster analysis
"""

import os
import re
import json
from pathlib import Path
from typing import Dict, List
from datetime import datetime

class EfficientTestMethodAnalyzer:
    """Fast analyzer focused on test methods only."""
    
    def __init__(self):
        self.backup_legacy_path = Path("F:/Dynamic/ExxerAi/ExxerAI/backups/legacy")
        
    def extract_test_methods_fast(self, content: str, file_path: Path) -> Dict:
        """Fast extraction of test methods only."""
        
        # Quick test class detection
        is_test_file = (
            'test' in file_path.name.lower() or
            'tests' in str(file_path).lower() or
            re.search(r'\[Fact\]|\[Theory\]|\[Test\]', content, re.IGNORECASE)
        )
        
        if not is_test_file:
            return None
            
        # Extract class name quickly
        class_match = re.search(r'class\s+(\w+)', content, re.IGNORECASE)
        class_name = class_match.group(1) if class_match else file_path.stem
        
        # Extract namespace
        namespace_match = re.search(r'namespace\s+([^\s\{;]+)', content)
        namespace = namespace_match.group(1) if namespace_match else "Unknown"
        
        # Extract test methods quickly
        test_methods = []
        
        # Pattern for test methods with attributes
        test_pattern = r'\[(?:Fact|Theory|Test|TestMethod|TestCase)[^\]]*\]\s*(?:public|private|protected|internal)?\s*(?:async\s+)?(?:Task|void|\w+)\s+(\w+)'
        
        for match in re.finditer(test_pattern, content, re.IGNORECASE | re.MULTILINE):
            method_name = match.group(1)
            test_methods.append({
                'methodName': method_name,
                'isTestMethod': True,
                'testPattern': self.classify_test_pattern(method_name)
            })
        
        # Additional pattern-based detection
        method_pattern = r'(?:public|private|protected|internal)?\s*(?:async\s+)?(?:Task|void|\w+)\s+(\w+_Should_\w+|\w+_When_\w+|\w+Test\w*)\s*\('
        
        for match in re.finditer(method_pattern, content, re.IGNORECASE | re.MULTILINE):
            method_name = match.group(1)
            if not any(tm['methodName'] == method_name for tm in test_methods):
                test_methods.append({
                    'methodName': method_name,
                    'isTestMethod': True,
                    'testPattern': self.classify_test_pattern(method_name)
                })
        
        project_name = self.get_project_name_fast(file_path)
        
        return {
            'className': class_name,
            'project': project_name,
            'fullPath': str(file_path).replace('\\', '/'),
            'fileName': file_path.name,
            'namespace': namespace,
            'testMethods': test_methods,
            'testMethodCount': len(test_methods),
            'isTestClass': len(test_methods) > 0
        }
    
    def classify_test_pattern(self, method_name: str) -> str:
        """Classify test method pattern quickly."""
        name_lower = method_name.lower()
        
        if '_should_' in name_lower:
            return 'BDD_Should'
        elif '_when_' in name_lower:
            return 'BDD_When'
        elif '_given_' in name_lower:
            return 'BDD_Given'
        elif 'test' in name_lower:
            return 'Traditional_Test'
        elif 'can' in name_lower:
            return 'Behavior_Can'
        else:
            return 'Other'
    
    def get_project_name_fast(self, file_path: Path) -> str:
        """Fast project name extraction."""
        path_str = str(file_path)
        if 'backups' in path_str and 'legacy' in path_str:
            try:
                parts = path_str.split('/')
                legacy_idx = parts.index('legacy')
                if legacy_idx + 1 < len(parts):
                    return parts[legacy_idx + 1]
            except:
                pass
        return "Unknown"
    
    def analyze_fast(self) -> List[Dict]:
        """Fast analysis of test methods only."""
        test_classes = []
        
        print(f"Fast scanning: {self.backup_legacy_path}")
        
        for cs_file in self.backup_legacy_path.rglob("*.cs"):
            try:
                with open(cs_file, 'r', encoding='utf-8') as f:
                    content = f.read()
                    
                test_class_info = self.extract_test_methods_fast(content, cs_file)
                if test_class_info and test_class_info['testMethodCount'] > 0:
                    test_classes.append(test_class_info)
                    
            except Exception as e:
                print(f"Error processing {cs_file}: {e}")
        
        return test_classes
    
    def generate_report(self, test_classes: List[Dict]) -> Dict:
        """Generate focused test methods report."""
        
        total_methods = sum(tc['testMethodCount'] for tc in test_classes)
        
        # Statistics
        stats = {
            'total_test_classes': len(test_classes),
            'total_test_methods': total_methods,
            'by_project': {},
            'by_pattern': {},
            'methods_by_project': {}
        }
        
        for tc in test_classes:
            project = tc['project']
            stats['by_project'][project] = stats['by_project'].get(project, 0) + 1
            
            if project not in stats['methods_by_project']:
                stats['methods_by_project'][project] = 0
            stats['methods_by_project'][project] += tc['testMethodCount']
            
            for method in tc['testMethods']:
                pattern = method['testPattern']
                stats['by_pattern'][pattern] = stats['by_pattern'].get(pattern, 0) + 1
        
        report = {
            'metadata': {
                'generated_on': datetime.now().isoformat(),
                'analyzer_version': '1.0.0_fast_test_methods'
            },
            'statistics': stats,
            'test_classes': test_classes
        }
        
        return report
    
    def run(self):
        """Execute fast test method analysis."""
        print("Starting FAST test method analysis...")
        
        test_classes = self.analyze_fast()
        report = self.generate_report(test_classes)
        
        # Save report
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        output_file = f"test_methods_analysis_{timestamp}.json"
        
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2, ensure_ascii=False)
        
        stats = report['statistics']
        print("\n" + "="*50)
        print("FAST TEST METHOD ANALYSIS COMPLETE!")
        print(f"Test classes found: {stats['total_test_classes']}")
        print(f"Test methods found: {stats['total_test_methods']}")
        print(f"Projects with tests: {len(stats['by_project'])}")
        print(f"Output: {output_file}")
        
        # Show top projects
        print("\nTOP PROJECTS BY TEST METHODS:")
        for project, count in sorted(stats['methods_by_project'].items(), key=lambda x: x[1], reverse=True)[:5]:
            print(f"  {project}: {count} test methods")
        
        print("="*50)
        return output_file

def main():
    analyzer = EfficientTestMethodAnalyzer()
    analyzer.run()

if __name__ == "__main__":
    main()