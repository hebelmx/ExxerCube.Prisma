#!/usr/bin/env python3
"""
Test Duplicate vs Recovery Candidate Analyzer
Compares current src/tests with backup metadata to identify:
1. Duplicated tests (same class name, likely same functionality)
2. Recovery candidates (unique tests that should be restored)
"""

import json
from pathlib import Path
from typing import Dict, List, Set, Tuple
from datetime import datetime

class TestDuplicateAnalyzer:
    """Analyzes test duplication and recovery candidates."""
    
    def __init__(self, backup_analysis_file: str):
        self.backup_analysis_file = backup_analysis_file
        self.src_tests_path = Path("code/src/tests")
        
        # Load backup analysis
        with open(backup_analysis_file, 'r') as f:
            self.backup_data = json.load(f)
            
        # Extract backup test classes
        self.backup_tests = self._extract_backup_tests()
        
    def _extract_backup_tests(self) -> Dict[str, List[Dict]]:
        """Extract test classes from backup analysis grouped by class name."""
        backup_tests = {}
        
        for cls in self.backup_data['classes']:
            if self._is_test_class(cls):
                class_name = cls['className']
                if class_name not in backup_tests:
                    backup_tests[class_name] = []
                backup_tests[class_name].append(cls)
                
        return backup_tests
    
    def _is_test_class(self, cls: Dict) -> bool:
        """Determine if a class is a test class."""
        indicators = [
            'test' in cls['className'].lower(),
            'test' in cls['fileName'].lower(),
            'tests' in cls['fullPath'].lower(),
            cls['fileName'].endswith('Tests.cs'),
            cls['fileName'].endswith('Test.cs'),
            'xunit' in str(cls['dependencies']).lower(),
            'nsubstitute' in str(cls['dependencies']).lower(),
            'shouldly' in str(cls['dependencies']).lower()
        ]
        return any(indicators)
    
    def scan_current_tests(self) -> Dict[str, List[Dict]]:
        """Scan current src/tests directory for test classes."""
        current_tests = {}
        
        if not self.src_tests_path.exists():
            print(f"Warning: {self.src_tests_path} does not exist")
            return current_tests
            
        for test_file in self.src_tests_path.rglob("*.cs"):
            try:
                with open(test_file, 'r', encoding='utf-8') as f:
                    content = f.read()
                    
                # Extract class names from current test files
                import re
                class_pattern = r'(?:public|internal|private|protected)?\s*(?:static|abstract|sealed)?\s*class\s+(\w+)'
                matches = re.findall(class_pattern, content, re.MULTILINE | re.IGNORECASE)
                
                for class_name in matches:
                    if class_name not in current_tests:
                        current_tests[class_name] = []
                        
                    # Create minimal class info for current tests
                    class_info = {
                        'className': class_name,
                        'fileName': test_file.name,
                        'fullPath': str(test_file).replace('\\', '/'),
                        'project': self._extract_current_project(test_file),
                        'namespace': self._extract_namespace_from_content(content)
                    }
                    current_tests[class_name].append(class_info)
                    
            except Exception as e:
                print(f"Error processing {test_file}: {e}")
                
        return current_tests
    
    def _extract_current_project(self, test_file: Path) -> str:
        """Extract project name from current test file path."""
        try:
            rel_path = test_file.relative_to(self.src_tests_path)
            return rel_path.parts[0] if rel_path.parts else "Unknown"
        except:
            return "Unknown"
    
    def _extract_namespace_from_content(self, content: str) -> str:
        """Extract namespace from file content."""
        import re
        namespace_match = re.search(r'namespace\s+([^\s\{;]+)', content)
        if namespace_match:
            return namespace_match.group(1).strip()
        return "Unknown"
    
    def analyze_duplicates_and_candidates(self) -> Dict:
        """Main analysis: identify duplicates vs recovery candidates."""
        current_tests = self.scan_current_tests()
        
        analysis = {
            'duplicates': [],
            'recovery_candidates': [],
            'statistics': {
                'backup_test_classes': len(self.backup_tests),
                'current_test_classes': len(current_tests),
                'duplicate_count': 0,
                'candidate_count': 0
            }
        }
        
        # Check each backup test class
        for backup_class_name, backup_instances in self.backup_tests.items():
            if backup_class_name in current_tests:
                # Potential duplicate - analyze further
                current_instances = current_tests[backup_class_name]
                duplicate_info = self._analyze_potential_duplicate(
                    backup_class_name, backup_instances, current_instances
                )
                analysis['duplicates'].append(duplicate_info)
                analysis['statistics']['duplicate_count'] += 1
            else:
                # Recovery candidate - unique test class
                candidate_info = self._create_recovery_candidate(backup_class_name, backup_instances)
                analysis['recovery_candidates'].append(candidate_info)
                analysis['statistics']['candidate_count'] += 1
        
        return analysis
    
    def _analyze_potential_duplicate(self, class_name: str, backup_instances: List[Dict], 
                                   current_instances: List[Dict]) -> Dict:
        """Analyze if classes are true duplicates or different implementations."""
        
        # Compare namespaces, projects, and contexts
        backup_namespaces = {inst['namespace'] for inst in backup_instances}
        current_namespaces = {inst['namespace'] for inst in current_instances}
        
        backup_projects = {inst.get('project', 'Unknown') for inst in backup_instances}
        current_projects = {inst.get('project', 'Unknown') for inst in current_instances}
        
        # Determine likelihood of being true duplicate
        namespace_overlap = bool(backup_namespaces & current_namespaces)
        project_similarity = bool(backup_projects & current_projects)
        
        confidence_score = 0
        if namespace_overlap:
            confidence_score += 0.6
        if project_similarity:
            confidence_score += 0.3
        if class_name.endswith('Test') or class_name.endswith('Tests'):
            confidence_score += 0.1
            
        duplicate_info = {
            'className': class_name,
            'confidence_duplicate': confidence_score,
            'is_likely_duplicate': confidence_score > 0.7,
            'backup_instances': backup_instances,
            'current_instances': current_instances,
            'backup_namespaces': list(backup_namespaces),
            'current_namespaces': list(current_namespaces),
            'backup_projects': list(backup_projects),
            'current_projects': list(current_projects),
            'recommendation': self._get_duplicate_recommendation(confidence_score)
        }
        
        return duplicate_info
    
    def _create_recovery_candidate(self, class_name: str, backup_instances: List[Dict]) -> Dict:
        """Create recovery candidate information."""
        # Analyze value of recovery
        recovery_score = 0
        
        # Score based on test indicators
        if any('test' in inst['fileName'].lower() for inst in backup_instances):
            recovery_score += 0.3
        if any('xunit' in str(inst.get('dependencies', [])).lower() for inst in backup_instances):
            recovery_score += 0.2
        if len(backup_instances) > 1:
            recovery_score += 0.2  # Multiple instances = likely valuable
        if any('integration' in inst['fullPath'].lower() for inst in backup_instances):
            recovery_score += 0.3  # Integration tests are valuable
            
        candidate_info = {
            'className': class_name,
            'recovery_value_score': recovery_score,
            'is_high_value': recovery_score > 0.6,
            'backup_instances': backup_instances,
            'backup_projects': list({inst.get('project', 'Unknown') for inst in backup_instances}),
            'backup_namespaces': list({inst['namespace'] for inst in backup_instances}),
            'recommendation': self._get_recovery_recommendation(recovery_score, backup_instances)
        }
        
        return candidate_info
    
    def _get_duplicate_recommendation(self, confidence_score: float) -> str:
        """Get recommendation for duplicate handling."""
        if confidence_score > 0.8:
            return "SKIP - Very likely duplicate, keep current version"
        elif confidence_score > 0.6:
            return "REVIEW - Likely duplicate, but compare implementations"
        else:
            return "CONSIDER - May be different implementations, worth reviewing"
    
    def _get_recovery_recommendation(self, recovery_score: float, instances: List[Dict]) -> str:
        """Get recommendation for recovery candidate."""
        if recovery_score > 0.7:
            return "HIGH PRIORITY - Valuable test, should recover"
        elif recovery_score > 0.5:
            return "MEDIUM PRIORITY - Useful test, consider recovery"
        elif any('integration' in inst['fullPath'].lower() for inst in instances):
            return "INTEGRATION TEST - Consider for integration coverage"
        else:
            return "LOW PRIORITY - Basic test, recover if needed"
    
    def generate_report(self, analysis: Dict, output_file: str):
        """Generate comprehensive duplicate/recovery analysis report."""
        
        # Add metadata
        analysis['metadata'] = {
            'generated_on': datetime.now().isoformat(),
            'backup_analysis_file': self.backup_analysis_file,
            'analyzer_version': '1.0.0'
        }
        
        # Add summary statistics
        analysis['summary'] = {
            'total_backup_tests': analysis['statistics']['backup_test_classes'],
            'total_current_tests': analysis['statistics']['current_test_classes'],
            'duplicates_found': analysis['statistics']['duplicate_count'],
            'recovery_candidates': analysis['statistics']['candidate_count'],
            'high_priority_candidates': len([c for c in analysis['recovery_candidates'] if c['is_high_value']]),
            'likely_duplicates': len([d for d in analysis['duplicates'] if d['is_likely_duplicate']])
        }
        
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(analysis, f, indent=2, ensure_ascii=False)
            
        print(f"Duplicate/Recovery analysis saved to: {output_file}")
        return analysis
    
    def print_summary(self, analysis: Dict):
        """Print summary of analysis results."""
        print("\n" + "="*60)
        print("TEST DUPLICATE vs RECOVERY ANALYSIS SUMMARY")
        print("="*60)
        
        summary = analysis['summary']
        print(f"📊 Backup Tests Analyzed: {summary['total_backup_tests']}")
        print(f"📊 Current Tests Found: {summary['total_current_tests']}")
        print(f"🔄 Potential Duplicates: {summary['duplicates_found']}")
        print(f"   └─ Likely Duplicates: {summary['likely_duplicates']}")
        print(f"💎 Recovery Candidates: {summary['recovery_candidates']}")
        print(f"   └─ High Priority: {summary['high_priority_candidates']}")
        
        print(f"\n🎯 RECOMMENDATIONS:")
        high_priority = [c for c in analysis['recovery_candidates'] if c['is_high_value']]
        for candidate in high_priority[:5]:  # Show top 5
            print(f"   💎 {candidate['className']} - {candidate['recommendation']}")
            
        print(f"\n⚠️ DUPLICATES TO REVIEW:")
        review_duplicates = [d for d in analysis['duplicates'] if not d['is_likely_duplicate']]
        for duplicate in review_duplicates[:5]:  # Show top 5
            print(f"   🔄 {duplicate['className']} - {duplicate['recommendation']}")

def main():
    import sys
    
    # Use the most recent backup analysis file
    backup_file = "cs_class_analysis_20251031_101417.json"
    if len(sys.argv) > 1:
        backup_file = sys.argv[1]
    
    analyzer = TestDuplicateAnalyzer(backup_file)
    analysis = analyzer.analyze_duplicates_and_candidates()
    
    # Generate output filename
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    output_file = f"test_duplicate_analysis_{timestamp}.json"
    
    analyzer.generate_report(analysis, output_file)
    analyzer.print_summary(analysis)

if __name__ == "__main__":
    main()