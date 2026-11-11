#!/usr/bin/env python3
"""
Test Method Duplicate Detector for Prisma
Detects duplicate test methods within the codebase
Adapted from ExxerAI test_method_duplicate_analyzer_fast.py
"""

import json
import re
from pathlib import Path
from typing import Dict, List, Set
from datetime import datetime
from collections import defaultdict
import hashlib

class PrismaTestDuplicateDetector:
    """Detects duplicate test methods in Prisma codebase."""

    def __init__(self, base_path: str = "Code/Src/CSharp"):
        self.base_path = Path(base_path)

        # Similarity thresholds
        self.exact_match_threshold = 1.0
        self.high_similarity_threshold = 0.90
        self.suspicious_threshold = 0.75

    def normalize_method_name(self, method_name: str) -> str:
        """Normalize method name for comparison."""
        normalized = method_name
        # Remove common test prefixes/suffixes
        normalized = re.sub(r'^Test_?', '', normalized, flags=re.IGNORECASE)
        normalized = re.sub(r'_?Test$', '', normalized, flags=re.IGNORECASE)
        normalized = re.sub(r'Async$', '', normalized, flags=re.IGNORECASE)

        # Normalize underscores and casing
        normalized = re.sub(r'_+', '_', normalized)
        normalized = normalized.strip('_').lower()

        return normalized

    def create_ngrams(self, text: str, n: int = 3) -> Set[str]:
        """Create n-grams for similarity comparison."""
        if len(text) < n:
            return {text}
        return {text[i:i+n] for i in range(len(text) - n + 1)}

    def ngram_similarity(self, s1: str, s2: str) -> float:
        """Calculate similarity using n-gram approach."""
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

    def scan_test_methods(self) -> Dict[str, List[Dict]]:
        """Scan all test files for test methods."""
        all_methods = {}

        print(f"Scanning test files in {self.base_path}...")
        test_files = list(self.base_path.rglob("*Tests.cs"))
        print(f"Found {len(test_files)} test files")

        for i, test_file in enumerate(test_files):
            if i % 10 == 0 and i > 0:
                print(f"Scanned {i}/{len(test_files)} files")

            try:
                with open(test_file, 'r', encoding='utf-8') as f:
                    content = f.read()

                # Extract test methods
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

                project = self._extract_project(test_file)

                for method_name in found_methods:
                    normalized_name = self.normalize_method_name(method_name)

                    method_info = {
                        'originalName': method_name,
                        'normalizedName': normalized_name,
                        'className': class_name,
                        'project': project,
                        'namespace': namespace,
                        'fullPath': str(test_file).replace('\\', '/')
                    }

                    if normalized_name not in all_methods:
                        all_methods[normalized_name] = []
                    all_methods[normalized_name].append(method_info)

            except Exception as e:
                print(f"Error processing {test_file}: {e}")

        print(f"Scan complete: found {len(all_methods)} unique normalized method names")
        return all_methods

    def _extract_project(self, test_file: Path) -> str:
        """Extract project name from test file path."""
        try:
            parts = test_file.parts
            for i, part in enumerate(parts):
                if part == "CSharp" and i + 1 < len(parts):
                    return parts[i + 1]
            return "Unknown"
        except:
            return "Unknown"

    def detect_duplicates(self) -> Dict:
        """Detect duplicate test methods."""
        print("Starting duplicate detection...")
        all_methods = self.scan_test_methods()

        analysis = {
            'exact_duplicates': [],
            'high_similarity_duplicates': [],
            'suspicious_similarities': [],
            'statistics': {
                'total_unique_normalized': len(all_methods),
                'exact_duplicates': 0,
                'high_similarity': 0,
                'suspicious': 0,
                'total_methods': sum(len(instances) for instances in all_methods.values())
            }
        }

        # Find exact duplicates (same normalized name, multiple instances)
        for normalized_name, instances in all_methods.items():
            if len(instances) > 1:
                analysis['exact_duplicates'].append({
                    'normalizedName': normalized_name,
                    'instanceCount': len(instances),
                    'instances': instances,
                    'recommendation': 'REVIEW - Same method name in multiple locations'
                })
                analysis['statistics']['exact_duplicates'] += 1

        # Find similar method names (fuzzy matching)
        processed_pairs = set()
        method_list = list(all_methods.items())

        print(f"Comparing {len(method_list)} methods for similarity...")

        for i, (name1, instances1) in enumerate(method_list):
            if i % 100 == 0 and i > 0:
                print(f"Compared {i}/{len(method_list)} methods")

            for name2, instances2 in method_list[i+1:]:
                # Skip if same name
                if name1 == name2:
                    continue

                # Skip if already processed
                pair_key = tuple(sorted([name1, name2]))
                if pair_key in processed_pairs:
                    continue
                processed_pairs.add(pair_key)

                # Check similarity
                similarity = self.ngram_similarity(name1, name2)

                if similarity >= self.high_similarity_threshold:
                    analysis['high_similarity_duplicates'].append({
                        'method1': name1,
                        'method2': name2,
                        'similarity': similarity,
                        'instances1': instances1,
                        'instances2': instances2,
                        'recommendation': 'REVIEW - Highly similar method names, possible duplication'
                    })
                    analysis['statistics']['high_similarity'] += 1

                elif similarity >= self.suspicious_threshold:
                    analysis['suspicious_similarities'].append({
                        'method1': name1,
                        'method2': name2,
                        'similarity': similarity,
                        'instances1': instances1,
                        'instances2': instances2,
                        'recommendation': 'CHECK - Similar method names, may be related'
                    })
                    analysis['statistics']['suspicious'] += 1

        return analysis

    def generate_report(self, analysis: Dict, output_file: str):
        """Generate duplicate detection report."""
        analysis['metadata'] = {
            'generated_on': datetime.now().isoformat(),
            'base_path': str(self.base_path),
            'analyzer_version': '1.0.0_prisma',
            'thresholds': {
                'exact_match': self.exact_match_threshold,
                'high_similarity': self.high_similarity_threshold,
                'suspicious': self.suspicious_threshold
            }
        }

        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(analysis, f, indent=2, ensure_ascii=False)

        print(f"\nDuplicate analysis saved to: {output_file}")
        return analysis

    def print_summary(self, analysis: Dict):
        """Print summary of duplicate detection."""
        print("\n" + "="*70)
        print("PRISMA TEST METHOD DUPLICATE DETECTION")
        print("="*70)

        stats = analysis['statistics']
        print(f"📊 Total Test Methods Found: {stats['total_methods']}")
        print(f"📊 Unique Normalized Names: {stats['total_unique_normalized']}")
        print(f"⚠️  Exact Duplicates: {stats['exact_duplicates']}")
        print(f"🔄 High Similarity: {stats['high_similarity']}")
        print(f"❓ Suspicious Similarities: {stats['suspicious']}")

        if analysis['exact_duplicates']:
            print(f"\n⚠️  TOP EXACT DUPLICATES:")
            for dup in analysis['exact_duplicates'][:10]:
                print(f"   🔴 {dup['normalizedName']} ({dup['instanceCount']} instances)")
                for inst in dup['instances'][:3]:
                    print(f"      - {inst['className']} in {inst['project']}")

        if analysis['high_similarity_duplicates']:
            print(f"\n🔄 HIGH SIMILARITY PAIRS:")
            for sim in analysis['high_similarity_duplicates'][:10]:
                print(f"   🟡 {sim['method1']} ≈ {sim['method2']} (similarity: {sim['similarity']:.2f})")

        print("="*70)

def main():
    import sys

    base_path = "Code/Src/CSharp" if len(sys.argv) < 2 else sys.argv[1]

    detector = PrismaTestDuplicateDetector(base_path)
    analysis = detector.detect_duplicates()

    # Generate output filename
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    output_file = f"test_duplicate_analysis_{timestamp}.json"

    detector.generate_report(analysis, output_file)
    detector.print_summary(analysis)

if __name__ == "__main__":
    main()
