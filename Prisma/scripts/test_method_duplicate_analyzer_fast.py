#!/usr/bin/env python3
"""
Ultra-Fast Test Method Duplicate Analyzer
Optimized version using:
1. Hash-based exact matching
2. N-gram similarity for fuzzy matching
3. Batch processing with progress reporting
4. Early termination strategies
"""

import json
import re
from pathlib import Path
from typing import Dict, List, Set, Tuple
from datetime import datetime
from collections import defaultdict
import hashlib

class FastTestMethodDuplicateAnalyzer:
    """Ultra-fast analyzer optimized for large datasets."""
    
    def __init__(self, backup_methods_file: str):
        self.backup_methods_file = backup_methods_file
        self.src_tests_path = Path("code/src/tests")
        
        # Load backup test methods analysis
        with open(backup_methods_file, 'r') as f:
            self.backup_data = json.load(f)
            
        # Extract backup test methods
        self.backup_methods = self._extract_backup_methods()
        
        # Similarity thresholds (more aggressive)
        self.exact_match_threshold = 1.0
        self.high_similarity_threshold = 0.90
        self.code_drift_threshold = 0.75
        self.different_threshold = 0.60
        
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
    
    def normalize_method_name(self, method_name: str) -> str:
        """Normalize method name for better comparison."""
        # Remove common test prefixes/suffixes
        normalized = method_name
        normalized = re.sub(r'^Test_?', '', normalized, flags=re.IGNORECASE)
        normalized = re.sub(r'_?Test$', '', normalized, flags=re.IGNORECASE)
        normalized = re.sub(r'Async$', '', normalized, flags=re.IGNORECASE)
        
        # Normalize underscores and casing patterns
        normalized = re.sub(r'_+', '_', normalized)
        normalized = normalized.strip('_').lower()
        
        return normalized
    
    def create_ngrams(self, text: str, n: int = 3) -> Set[str]:
        """Create n-grams for similarity comparison."""
        if len(text) < n:
            return {text}
        return {text[i:i+n] for i in range(len(text) - n + 1)}
    
    def ngram_similarity(self, s1: str, s2: str) -> float:
        """Calculate similarity using n-gram approach (faster than Levenshtein)."""
        if not s1 or not s2:
            return 0.0
        if s1 == s2:
            return 1.0
            
        ngrams1 = self.create_ngrams(s1, 3)
        ngrams2 = self.create_ngrams(s2, 3)
        
        if not ngrams1 or not ngrams2:
            return 0.0
            
        intersection = len(ngrams1 & ngrams2)
        union = len(ngrams1 | ngrams2)
        
        return intersection / union if union > 0 else 0.0
    
    def scan_current_test_methods(self) -> Dict[str, List[Dict]]:
        """Scan current src/tests for test methods."""
        current_methods = {}
        
        if not self.src_tests_path.exists():
            print(f"Warning: {self.src_tests_path} does not exist")
            return current_methods
            
        print("Scanning current test files...")
        test_files = list(self.src_tests_path.rglob("*.cs"))
        print(f"Found {len(test_files)} test files to scan")
        
        for i, test_file in enumerate(test_files):
            if i % 10 == 0:
                print(f"Scanned {i}/{len(test_files)} files")
                
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
                
                # Extract class name and namespace quickly
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
                
        print(f"Scan complete: found {len(current_methods)} unique normalized method names")
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
    
    def analyze_method_duplicates_fast(self) -> Dict:
        """Ultra-fast analysis using hash-based matching and n-grams."""
        print("Starting ultra-fast method duplicate analysis...")
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
        
        # Create hash-based lookup for instant exact matching
        current_hash_lookup = {}
        current_normalized_lookup = {}
        
        for current_name, instances in current_methods.items():
            # Hash for exact matching
            method_hash = hashlib.md5(current_name.encode()).hexdigest()
            current_hash_lookup[method_hash] = (current_name, instances)
            
            # Normalized for case-insensitive matching
            current_normalized_lookup[current_name.lower()] = (current_name, instances)
        
        print(f"Analyzing {len(self.backup_methods)} backup methods...")
        
        processed = 0
        batch_size = 100
        
        for backup_name, backup_instances in self.backup_methods.items():
            processed += 1
            if processed % batch_size == 0:
                print(f"Progress: {processed}/{len(self.backup_methods)} methods ({100*processed//len(self.backup_methods)}%)")
            
            # Step 1: Hash-based exact match (fastest)
            backup_hash = hashlib.md5(backup_name.encode()).hexdigest()
            if backup_hash in current_hash_lookup:
                current_name, current_instances = current_hash_lookup[backup_hash]
                match_info = self._create_exact_match_info(
                    backup_name, backup_instances, current_name, current_instances, 1.0
                )
                analysis['exact_matches'].append(match_info)
                analysis['statistics']['exact_matches'] += 1
                continue
            
            # Step 2: Normalized exact match (case-insensitive)
            backup_lower = backup_name.lower()
            if backup_lower in current_normalized_lookup:
                current_name, current_instances = current_normalized_lookup[backup_lower]
                match_info = self._create_exact_match_info(
                    backup_name, backup_instances, current_name, current_instances, 0.99
                )
                analysis['exact_matches'].append(match_info)
                analysis['statistics']['exact_matches'] += 1
                continue
            
            # Step 3: N-gram similarity for fuzzy matching (only if needed)
            best_similarity = 0.0
            best_match = None
            
            # Only check methods with similar length (optimization)
            backup_len = len(backup_name)
            candidates = [
                (name, instances) for name, instances in current_methods.items()
                if abs(len(name) - backup_len) <= max(3, backup_len * 0.3)
            ]
            
            # Limit candidates for performance
            if len(candidates) > 50:
                candidates = candidates[:50]
            
            for current_name, current_instances in candidates:
                similarity = self.ngram_similarity(backup_name, current_name)
                if similarity > best_similarity:
                    best_similarity = similarity
                    best_match = (current_name, current_instances)
                    
                # Early termination for high similarity
                if similarity > 0.95:
                    break
            
            # Classify the result
            if best_match and best_similarity >= self.high_similarity_threshold:
                current_name, current_instances = best_match
                match_info = self._create_exact_match_info(
                    backup_name, backup_instances, current_name, current_instances, best_similarity
                )
                analysis['exact_matches'].append(match_info)
                analysis['statistics']['high_similarity'] += 1
                
            elif best_match and best_similarity >= self.code_drift_threshold:
                current_name, current_instances = best_match
                drift_info = self._create_code_drift_info(
                    backup_name, backup_instances, [(current_name, best_similarity)], current_methods
                )
                analysis['code_drift_candidates'].append(drift_info)
                analysis['statistics']['code_drift'] += 1
                
            else:
                # Unique method - recovery candidate
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
                    'instances': current_methods[name] if name in current_methods else []
                }
                for name, score in similar_methods
            ],
            'backup_instances': backup_instances,
            'recommendation': 'REVIEW - Possible code drift, compare implementations',
            'backup_projects': list(set(inst['project'] for inst in backup_instances))
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
            'analyzer_version': '2.0.0_ultra_fast',
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
            
        print(f"Ultra-fast method-level duplicate analysis saved to: {output_file}")
        return analysis
    
    def print_summary(self, analysis: Dict):
        """Print detailed summary of method-level analysis."""
        print("\n" + "="*70)
        print("ULTRA-FAST TEST METHOD DUPLICATE ANALYSIS")
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
            if drift['similar_methods']:
                similar = drift['similar_methods'][0]
                print(f"   🔄 {drift['backup_method']} ≈ {similar['method_name']} (similarity: {similar['similarity_score']:.2f})")
        
        print("="*70)

def main():
    import sys
    
    # Use the most recent test methods analysis file
    methods_file = "test_methods_analysis_20251031_103108.json"
    if len(sys.argv) > 1:
        methods_file = sys.argv[1]
    
    analyzer = FastTestMethodDuplicateAnalyzer(methods_file)
    analysis = analyzer.analyze_method_duplicates_fast()
    
    # Generate output filename
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    output_file = f"method_duplicate_analysis_fast_{timestamp}.json"
    
    analyzer.generate_report(analysis, output_file)
    analyzer.print_summary(analysis)

if __name__ == "__main__":
    main()