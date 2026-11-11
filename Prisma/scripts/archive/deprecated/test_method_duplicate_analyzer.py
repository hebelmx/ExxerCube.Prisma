#!/usr/bin/env python3
"""
Test Method Duplicate Analyzer with Levenshtein Distance
Compares test methods using similarity scoring to detect:
1. Exact duplicates
2. Code drift (similar methods with small changes)
3. Unique recovery candidates

Uses Levenshtein distance for fuzzy matching to catch renamed/modified tests.
"""

import json
import re
from pathlib import Path
from typing import Dict, List, Set, Tuple
from datetime import datetime

class TestMethodDuplicateAnalyzer:
    """Analyzes test method duplication using Levenshtein distance."""
    
    def __init__(self, backup_methods_file: str):
        self.backup_methods_file = backup_methods_file
        self.src_tests_path = Path("code/src/tests")
        
        # Load backup test methods analysis
        with open(backup_methods_file, 'r') as f:
            self.backup_data = json.load(f)
            
        # Extract backup test methods
        self.backup_methods = self._extract_backup_methods()
        
        # Similarity thresholds
        self.exact_match_threshold = 1.0
        self.high_similarity_threshold = 0.85
        self.code_drift_threshold = 0.70
        self.different_threshold = 0.50
        
    def levenshtein_distance(self, s1: str, s2: str) -> int:
        """Calculate Levenshtein distance between two strings."""
        if len(s1) < len(s2):
            return self.levenshtein_distance(s2, s1)

        if len(s2) == 0:
            return len(s1)

        previous_row = list(range(len(s2) + 1))
        for i, c1 in enumerate(s1):
            current_row = [i + 1]
            for j, c2 in enumerate(s2):
                insertions = previous_row[j + 1] + 1
                deletions = current_row[j] + 1
                substitutions = previous_row[j] + (c1 != c2)
                current_row.append(min(insertions, deletions, substitutions))
            previous_row = current_row
        
        return previous_row[-1]
    
    def similarity_score(self, s1: str, s2: str) -> float:
        """Calculate similarity score (0-1) using Levenshtein distance."""
        if not s1 or not s2:
            return 0.0
            
        max_len = max(len(s1), len(s2))
        if max_len == 0:
            return 1.0
            
        distance = self.levenshtein_distance(s1.lower(), s2.lower())
        return 1.0 - (distance / max_len)
    
    def normalize_method_name(self, method_name: str) -> str:
        """Normalize method name for better comparison."""
        # Remove common test prefixes/suffixes
        normalized = method_name
        normalized = re.sub(r'^Test_?', '', normalized, flags=re.IGNORECASE)
        normalized = re.sub(r'_?Test$', '', normalized, flags=re.IGNORECASE)
        normalized = re.sub(r'Async$', '', normalized, flags=re.IGNORECASE)
        
        # Normalize underscores and casing patterns
        normalized = re.sub(r'_+', '_', normalized)
        normalized = normalized.strip('_')
        
        return normalized
    
    def _extract_backup_methods(self) -> Dict[str, List[Dict]]:
        """Extract test methods from backup analysis grouped by normalized name."""
        backup_methods = {}
        
        for test_class in self.backup_data.get('test_classes', []):
            for method in test_class.get('testMethods', []):
                method_name = method['methodName']
                normalized_name = self.normalize_method_name(method_name)
                
                method_info = {
                    'originalName': method_name,
                    'normalizedName': normalized_name,
                    'className': test_class['className'],
                    'project': test_class['project'],
                    'namespace': test_class['namespace'],
                    'fullPath': test_class['fullPath'],
                    'testPattern': method.get('testPattern', 'Unknown')
                }
                
                if normalized_name not in backup_methods:
                    backup_methods[normalized_name] = []
                backup_methods[normalized_name].append(method_info)
                
        return backup_methods
    
    def scan_current_test_methods(self) -> Dict[str, List[Dict]]:
        """Scan current src/tests for test methods."""
        current_methods = {}
        
        if not self.src_tests_path.exists():
            print(f"Warning: {self.src_tests_path} does not exist")
            return current_methods
            
        for test_file in self.src_tests_path.rglob("*.cs"):
            try:
                with open(test_file, 'r', encoding='utf-8') as f:
                    content = f.read()
                    
                # Extract test methods quickly
                method_patterns = [
                    r'\[(?:Fact|Theory|Test|TestMethod|TestCase)[^\]]*\]\s*(?:public|private|protected|internal)?\s*(?:async\s+)?(?:Task|void|\w+)\s+(\w+)',
                    r'(?:public|private|protected|internal)?\s*(?:async\s+)?(?:Task|void|\w+)\s+(\w+_Should_\w+|\w+_When_\w+|\w+Test\w*)\s*\('
                ]
                
                found_methods = set()
                for pattern in method_patterns:
                    for match in re.finditer(pattern, content, re.IGNORECASE | re.MULTILINE):
                        method_name = match.group(1)
                        found_methods.add(method_name)
                
                # Extract class name and namespace
                class_match = re.search(r'class\s+(\w+)', content, re.IGNORECASE)
                class_name = class_match.group(1) if class_match else test_file.stem
                
                namespace_match = re.search(r'namespace\s+([^\s\{;]+)', content)
                namespace = namespace_match.group(1) if namespace_match else "Unknown"
                
                project = self._extract_current_project(test_file)
                
                for method_name in found_methods:
                    normalized_name = self.normalize_method_name(method_name)
                    
                    method_info = {
                        'originalName': method_name,
                        'normalizedName': normalized_name,
                        'className': class_name,
                        'project': project,
                        'namespace': namespace,
                        'fullPath': str(test_file).replace('\\', '/'),
                        'testPattern': self._classify_test_pattern(method_name)
                    }
                    
                    if normalized_name not in current_methods:
                        current_methods[normalized_name] = []
                    current_methods[normalized_name].append(method_info)
                    
            except Exception as e:
                print(f"Error processing {test_file}: {e}")
                
        return current_methods
    
    def _extract_current_project(self, test_file: Path) -> str:
        """Extract project name from current test file path."""
        try:
            rel_path = test_file.relative_to(self.src_tests_path)
            return rel_path.parts[0] if rel_path.parts else "Unknown"
        except:
            return "Unknown"
    
    def _classify_test_pattern(self, method_name: str) -> str:
        """Classify test method pattern."""
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
    
    def find_similar_methods(self, backup_method_name: str, current_methods: Dict) -> List[Tuple[str, float]]:
        """Find similar methods using Levenshtein distance."""
        similarities = []
        
        for current_name in current_methods.keys():
            similarity = self.similarity_score(backup_method_name, current_name)
            if similarity >= self.different_threshold:
                similarities.append((current_name, similarity))
        
        # Sort by similarity (highest first)
        similarities.sort(key=lambda x: x[1], reverse=True)
        return similarities
    
    def _find_similar_methods_optimized(self, backup_method_name: str, current_methods: Dict) -> List[Tuple[str, float]]:
        """Optimized similarity search with early termination."""
        similarities = []
        backup_normalized = self.normalize_method_name(backup_method_name)
        
        # Quick length-based filtering first
        backup_len = len(backup_normalized)
        candidates = []
        
        for current_name in current_methods.keys():
            current_normalized = self.normalize_method_name(current_name)
            current_len = len(current_normalized)
            
            # Skip if length difference is too large (optimization)
            length_ratio = min(backup_len, current_len) / max(backup_len, current_len)
            if length_ratio < 0.5:  # Skip if lengths are very different
                continue
                
            candidates.append((current_name, current_normalized))
        
        # Now calculate similarity only for promising candidates
        for current_name, current_normalized in candidates:
            similarity = self.similarity_score(backup_normalized, current_normalized)
            if similarity >= self.different_threshold:
                similarities.append((current_name, similarity))
                
            # Early termination if we find exact match
            if similarity >= 0.95:
                break
        
        # Sort by similarity (highest first) and return top 5
        similarities.sort(key=lambda x: x[1], reverse=True)
        return similarities[:5]
    
    def analyze_method_duplicates_and_drift(self) -> Dict:
        """Optimized analysis: identify exact matches, code drift, and unique methods."""
        print("Scanning current test methods...")
        current_methods = self.scan_current_test_methods()
        
        analysis = {
            'exact_matches': [],
            'code_drift_candidates': [],
            'unique_backup_methods': [],
            'statistics': {
                'backup_methods': len(self.backup_methods),
                'current_methods': len(current_methods),
                'exact_matches': 0,
                'high_similarity': 0,
                'code_drift': 0,
                'unique_methods': 0
            }
        }
        
        # Create normalized lookup for faster exact matching
        current_normalized = {name.lower(): name for name in current_methods.keys()}
        
        total_backup = len(self.backup_methods)
        processed = 0
        
        print(f"Analyzing {total_backup} backup methods...")
        
        # Process backup methods in batches for progress tracking
        batch_size = 50
        for i in range(0, total_backup, batch_size):
            batch_items = list(self.backup_methods.items())[i:i+batch_size]
            
            for backup_name, backup_instances in batch_items:
                processed += 1
                if processed % 100 == 0:
                    print(f"Progress: {processed}/{total_backup} methods processed")
                
                # Fast exact match check first
                backup_normalized = backup_name.lower()
                if backup_normalized in current_normalized:
                    current_name = current_normalized[backup_normalized]
                    current_instances = current_methods[current_name]
                    
                    match_info = self._create_exact_match_info(
                        backup_name, backup_instances, current_name, current_instances, 1.0
                    )
                    analysis['exact_matches'].append(match_info)
                    analysis['statistics']['exact_matches'] += 1
                    continue
                
                # Find similar methods for non-exact matches
                similar_methods = self._find_similar_methods_optimized(backup_name, current_methods)
                
                if not similar_methods:
                    # Unique method - recovery candidate
                    unique_info = self._create_unique_method_info(backup_name, backup_instances)
                    analysis['unique_backup_methods'].append(unique_info)
                    analysis['statistics']['unique_methods'] += 1
                    
                else:
                    best_match_name, best_similarity = similar_methods[0]
                    current_instances = current_methods[best_match_name]
                    
                    if best_similarity >= self.high_similarity_threshold:
                        # High similarity - likely same method
                        match_info = self._create_exact_match_info(
                            backup_name, backup_instances, best_match_name, current_instances, best_similarity
                        )
                        analysis['exact_matches'].append(match_info)
                        analysis['statistics']['high_similarity'] += 1
                        
                    elif best_similarity >= self.code_drift_threshold:
                        # Code drift - similar but changed
                        drift_info = self._create_code_drift_info(
                            backup_name, backup_instances, similar_methods[:3], current_methods
                        )
                        analysis['code_drift_candidates'].append(drift_info)
                        analysis['statistics']['code_drift'] += 1
                        
                    else:
                        # Different enough to be unique
                        unique_info = self._create_unique_method_info(backup_name, backup_instances)
                        analysis['unique_backup_methods'].append(unique_info)
                        analysis['statistics']['unique_methods'] += 1
        
        print(f"Analysis complete: {processed} methods processed")
        return analysis
    
    def _create_exact_match_info(self, backup_name: str, backup_instances: List[Dict], 
                                current_name: str, current_instances: List[Dict], similarity: float) -> Dict:
        """Create exact match information."""
        return {
            'backup_method': backup_name,
            'current_method': current_name,
            'similarity_score': similarity,
            'match_type': 'exact' if similarity >= self.exact_match_threshold else 'high_similarity',
            'backup_instances': backup_instances,
            'current_instances': current_instances,
            'recommendation': 'SKIP - Method exists in current codebase',
            'backup_projects': list(set(inst['project'] for inst in backup_instances)),
            'current_projects': list(set(inst['project'] for inst in current_instances))
        }
    
    def _create_code_drift_info(self, backup_name: str, backup_instances: List[Dict], 
                               similar_methods: List[Tuple[str, float]], current_methods: Dict) -> Dict:
        """Create code drift information."""
        return {
            'backup_method': backup_name,
            'similar_methods': [
                {
                    'method_name': name,
                    'similarity_score': score,
                    'instances': current_methods[name]
                }
                for name, score in similar_methods
            ],
            'backup_instances': backup_instances,
            'recommendation': 'REVIEW - Possible code drift, compare implementations',
            'backup_projects': list(set(inst['project'] for inst in backup_instances)),
            'drift_analysis': self._analyze_drift_patterns(backup_name, similar_methods)
        }
    
    def _create_unique_method_info(self, backup_name: str, backup_instances: List[Dict]) -> Dict:
        """Create unique method information."""
        # Calculate recovery priority
        priority_score = 0
        
        # Score based on test patterns
        for instance in backup_instances:
            pattern = instance.get('testPattern', 'Unknown')
            if pattern in ['BDD_Should', 'BDD_When', 'BDD_Given']:
                priority_score += 0.3
            elif 'Integration' in instance['fullPath']:
                priority_score += 0.4
            elif 'System' in instance['fullPath']:
                priority_score += 0.3
        
        # Score based on method name patterns
        name_lower = backup_name.lower()
        if any(keyword in name_lower for keyword in ['integration', 'e2e', 'system', 'performance']):
            priority_score += 0.4
        elif any(keyword in name_lower for keyword in ['should', 'when', 'given']):
            priority_score += 0.2
            
        return {
            'backup_method': backup_name,
            'backup_instances': backup_instances,
            'recovery_priority_score': min(priority_score, 1.0),
            'is_high_priority': priority_score > 0.6,
            'backup_projects': list(set(inst['project'] for inst in backup_instances)),
            'recommendation': self._get_recovery_recommendation(priority_score, backup_instances)
        }
    
    def _analyze_drift_patterns(self, backup_name: str, similar_methods: List[Tuple[str, float]]) -> Dict:
        """Analyze patterns in code drift."""
        patterns = {
            'possible_rename': False,
            'parameter_changes': False,
            'pattern_evolution': False,
            'async_conversion': False
        }
        
        for current_name, similarity in similar_methods:
            if similarity > 0.8:
                # Check for common drift patterns
                if 'async' in current_name.lower() and 'async' not in backup_name.lower():
                    patterns['async_conversion'] = True
                if len(current_name) != len(backup_name):
                    patterns['parameter_changes'] = True
                if backup_name.replace('_', '').lower() in current_name.replace('_', '').lower():
                    patterns['possible_rename'] = True
        
        return patterns
    
    def _get_recovery_recommendation(self, priority_score: float, instances: List[Dict]) -> str:
        """Get recommendation for recovery."""
        if priority_score > 0.7:
            return "HIGH PRIORITY - Critical test, should recover"
        elif priority_score > 0.5:
            return "MEDIUM PRIORITY - Valuable test, consider recovery"
        elif any('integration' in inst['fullPath'].lower() for inst in instances):
            return "INTEGRATION - Consider for coverage gaps"
        else:
            return "LOW PRIORITY - Standard test, recover if needed"
    
    def generate_report(self, analysis: Dict, output_file: str):
        """Generate comprehensive method-level duplicate analysis report."""
        
        # Add metadata
        analysis['metadata'] = {
            'generated_on': datetime.now().isoformat(),
            'backup_methods_file': self.backup_methods_file,
            'analyzer_version': '1.0.0_levenshtein',
            'similarity_thresholds': {
                'exact_match': self.exact_match_threshold,
                'high_similarity': self.high_similarity_threshold,
                'code_drift': self.code_drift_threshold,
                'different': self.different_threshold
            }
        }
        
        # Add summary
        stats = analysis['statistics']
        analysis['summary'] = {
            'total_backup_methods': stats['backup_methods'],
            'total_current_methods': stats['current_methods'],
            'exact_matches': stats['exact_matches'],
            'high_similarity_matches': stats['high_similarity'],
            'code_drift_candidates': stats['code_drift'],
            'unique_recovery_candidates': stats['unique_methods'],
            'high_priority_recoveries': len([m for m in analysis['unique_backup_methods'] if m['is_high_priority']])
        }
        
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(analysis, f, indent=2, ensure_ascii=False)
            
        print(f"Method-level duplicate analysis saved to: {output_file}")
        return analysis
    
    def print_summary(self, analysis: Dict):
        """Print detailed summary of method-level analysis."""
        print("\n" + "="*70)
        print("TEST METHOD DUPLICATE ANALYSIS with LEVENSHTEIN DISTANCE")
        print("="*70)
        
        summary = analysis['summary']
        print(f"📊 Backup Methods Analyzed: {summary['total_backup_methods']}")
        print(f"📊 Current Methods Found: {summary['total_current_methods']}")
        print(f"✅ Exact Matches: {summary['exact_matches']}")
        print(f"🔄 High Similarity: {summary['high_similarity_matches']}")
        print(f"🌊 Code Drift Candidates: {summary['code_drift_candidates']}")
        print(f"💎 Unique Recovery Candidates: {summary['unique_recovery_candidates']}")
        print(f"🎯 High Priority Recoveries: {summary['high_priority_recoveries']}")
        
        print(f"\n🎯 TOP RECOVERY CANDIDATES:")
        high_priority = [m for m in analysis['unique_backup_methods'] if m['is_high_priority']]
        for candidate in high_priority[:10]:
            print(f"   💎 {candidate['backup_method']} - {candidate['recommendation']}")
            
        print(f"\n🌊 CODE DRIFT DETECTED:")
        for drift in analysis['code_drift_candidates'][:5]:
            similar = drift['similar_methods'][0]
            print(f"   🔄 {drift['backup_method']} ≈ {similar['method_name']} (similarity: {similar['similarity_score']:.2f})")
        
        print("="*70)

def main():
    import sys
    
    # Use the most recent test methods analysis file
    methods_file = "test_methods_analysis_20251031_103108.json"
    if len(sys.argv) > 1:
        methods_file = sys.argv[1]
    
    analyzer = TestMethodDuplicateAnalyzer(methods_file)
    analysis = analyzer.analyze_method_duplicates_and_drift()
    
    # Generate output filename
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    output_file = f"method_duplicate_analysis_{timestamp}.json"
    
    analyzer.generate_report(analysis, output_file)
    analyzer.print_summary(analysis)

if __name__ == "__main__":
    main()