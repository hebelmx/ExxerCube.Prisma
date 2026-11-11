#!/usr/bin/env python3
"""
True Duplicate Detection via Similarity Scoring
Uses Levenshtein distance to find real duplicates (≥88% similarity)

EXCLUDES: ExxerAI.Integration.Test (pyramidal source - intentional duplication)

Detects:
1. CRITICAL: Same project, different namespace (copy/paste)
2. HIGH: Cross-project, same test strategy (Docker vs Non-Docker)
3. Naturally filters: Cross-layer differences (Domain vs Adapter vs Docker)
"""
import argparse
import json
import re
from collections import defaultdict
from dataclasses import dataclass, asdict
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Set, Tuple, Optional


@dataclass
class TestMethod:
    """Single test method"""
    name: str
    has_fact: bool
    has_theory: bool


@dataclass
class TestClass:
    """Test class with methods"""
    class_name: str
    namespace: str
    project: str
    file_path: str
    methods: List[TestMethod]
    is_docker_test: bool

    def method_names(self) -> List[str]:
        """Get sorted list of method names"""
        return sorted([m.name for m in self.methods])


@dataclass
class DuplicateMatch:
    """Duplicate test class match"""
    class1: TestClass
    class2: TestClass
    class_similarity: float
    method_similarity: float
    combined_similarity: float
    matching_methods: List[Tuple[str, str, float]]  # (method1, method2, similarity)
    duplicate_type: str  # INTRA_PROJECT, CROSS_PROJECT_DOCKER, CROSS_PROJECT_NON_DOCKER
    priority: str  # CRITICAL, HIGH, MEDIUM, LOW


# EXCLUDED project (pyramidal source)
EXCLUDED_PROJECTS = {
    # No projects currently excluded
    # Add project names here if needed to exclude from duplicate detection
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description='Detect true duplicate tests using similarity scoring (≥88%)',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Run similarity-based duplicate detection
  python %(prog)s --root code/src/tests --threshold 0.88

  # Generate JSON for automation
  python %(prog)s --output-json docs/TrueDuplicates.json

  # Focus on specific type
  python %(prog)s --type intra-project
"""
    )

    parser.add_argument('--root', type=str, default='code/src/tests',
                        help='Root directory for test files (default: code/src/tests)')
    parser.add_argument('--threshold', type=float, default=0.88,
                        help='Similarity threshold (default: 0.88 = 88%%)')
    parser.add_argument('--output', type=str, default='docs/TrueDuplicatesReport.txt',
                        help='Output text report file')
    parser.add_argument('--output-json', type=str, default='docs/TrueDuplicates.json',
                        help='Output JSON file for automation')
    parser.add_argument('--type', type=str, choices=['all', 'intra-project', 'cross-project'],
                        default='all', help='Type of duplicates to detect')

    return parser.parse_args()


def levenshtein_distance(s1: str, s2: str) -> int:
    """Calculate Levenshtein distance between two strings"""
    if len(s1) < len(s2):
        return levenshtein_distance(s2, s1)

    if len(s2) == 0:
        return len(s1)

    previous_row = range(len(s2) + 1)
    for i, c1 in enumerate(s1):
        current_row = [i + 1]
        for j, c2 in enumerate(s2):
            # Cost of insertions, deletions, or substitutions
            insertions = previous_row[j + 1] + 1
            deletions = current_row[j] + 1
            substitutions = previous_row[j] + (c1 != c2)
            current_row.append(min(insertions, deletions, substitutions))
        previous_row = current_row

    return previous_row[-1]


def levenshtein_ratio(s1: str, s2: str) -> float:
    """Calculate similarity ratio (0.0 to 1.0) using Levenshtein distance"""
    if not s1 or not s2:
        return 0.0

    distance = levenshtein_distance(s1.lower(), s2.lower())
    max_len = max(len(s1), len(s2))

    return 1.0 - (distance / max_len)


def is_docker_test_class(file_path: Path, content: str) -> bool:
    """Check if test class uses Docker containers (Testcontainers)"""

    # Check for Testcontainers usage patterns
    docker_indicators = [
        r'IAsyncLifetime',
        r'DockerContainer',
        r'PostgreSqlContainer',
        r'QdrantContainer',
        r'Neo4jContainer',
        r'RedisContainer',
        r'MongoDbContainer',
        r'TestcontainersContainer',
    ]

    for indicator in docker_indicators:
        if re.search(indicator, content):
            return True

    return False


def extract_test_classes(root_path: Path) -> List[TestClass]:
    """Scan and extract all test classes with methods"""
    test_classes = []

    for cs_file in root_path.rglob('*.cs'):
        try:
            # Determine project
            parts = cs_file.parts
            project = 'Unknown'
            for part in parts:
                if part.endswith('.Test') or part.endswith('Tests'):
                    project = part
                    break

            # Skip excluded projects
            if project in EXCLUDED_PROJECTS:
                continue

            with open(cs_file, 'r', encoding='utf-8') as f:
                content = f.read()

            # Extract namespace
            namespace_match = re.search(r'namespace\s+([\w\.]+)', content)
            namespace = namespace_match.group(1) if namespace_match else 'Unknown'

            # NEW APPROACH: First check if file contains [Fact or [Theory attributes
            # This is the definitive way to identify xUnit test files
            if '[Fact' not in content and '[Theory' not in content:
                continue  # Not a test file, skip it

            # Find ALL public classes (not just those ending in Test/Tests)
            # We already know this file has test attributes, so any public class could be a test class
            class_pattern = r'public\s+(?:sealed\s+)?class\s+(\w+)\s*(?::|$|<|\()'
            all_class_names = re.findall(class_pattern, content, re.MULTILINE)

            if not all_class_names:
                continue

            # Check if Docker test
            is_docker = is_docker_test_class(cs_file, content)

            # Extract test methods for each class
            for class_name in all_class_names:
                # Find all methods in this class
                # Simple extraction - find methods with [Fact] or [Theory]
                method_pattern = r'\[(?:Fact|Theory)(?:\([^\)]*\))?\]\s*(?:public|private|protected|internal)?\s*(?:async\s+)?(?:Task|void)\s+(\w+)\s*\('

                methods = []
                for match in re.finditer(method_pattern, content, re.MULTILINE):
                    method_name = match.group(1)

                    # Check which attribute
                    context_start = max(0, match.start() - 100)
                    context = content[context_start:match.start()]

                    has_fact = '[Fact]' in context or '[Fact(' in context
                    has_theory = '[Theory]' in context or '[Theory(' in context

                    methods.append(TestMethod(
                        name=method_name,
                        has_fact=has_fact,
                        has_theory=has_theory
                    ))

                if methods:  # Only add if has test methods
                    test_classes.append(TestClass(
                        class_name=class_name,
                        namespace=namespace,
                        project=project,
                        file_path=str(cs_file),
                        methods=methods,
                        is_docker_test=is_docker
                    ))

        except Exception as e:
            print(f"Warning: Could not process {cs_file}: {e}")
            continue

    return test_classes


def calculate_method_similarity(methods1: List[str], methods2: List[str]) -> Tuple[float, List[Tuple[str, str, float]]]:
    """Calculate similarity between two method lists

    Returns: (similarity_score, matching_methods)
    """
    if not methods1 or not methods2:
        return 0.0, []

    matching_methods = []
    total_matches = 0

    for m1 in methods1:
        best_match = None
        best_score = 0.0

        for m2 in methods2:
            score = levenshtein_ratio(m1, m2)
            if score > best_score:
                best_score = score
                best_match = m2

        if best_score >= 0.88:  # Threshold for individual method match
            total_matches += 1
            matching_methods.append((m1, best_match, best_score))

    total_methods = max(len(methods1), len(methods2))
    similarity = total_matches / total_methods if total_methods > 0 else 0.0

    return similarity, matching_methods


def find_duplicates(
    test_classes: List[TestClass],
    threshold: float,
    duplicate_type: str = 'all'
) -> List[DuplicateMatch]:
    """Find duplicate test classes based on similarity

    OPTIMIZED: Uses hash table to group by class name first,
    reducing O(n²) to O(n*m) where m = avg classes with same name
    """

    duplicates = []

    # OPTIMIZATION: Build hash table - group by exact class name
    class_name_groups = defaultdict(list)
    for test_class in test_classes:
        class_name_groups[test_class.class_name].append(test_class)

    print(f"   Hash table built: {len(class_name_groups)} unique class names")

    # Only compare classes with the SAME class name
    # (True duplicates almost always have identical class names)
    for class_name, group in class_name_groups.items():
        if len(group) < 2:
            continue  # No duplicates possible

        # Compare all pairs within this group
        for i, class1 in enumerate(group):
            for j, class2 in enumerate(group):
                if i >= j:  # Avoid comparing same class and duplicates
                    continue

                # Determine duplicate type
                same_project = class1.project == class2.project
                same_namespace = class1.namespace == class2.namespace
                both_docker = class1.is_docker_test and class2.is_docker_test
                both_non_docker = not class1.is_docker_test and not class2.is_docker_test

                # Skip if same namespace (exact same location)
                if same_project and same_namespace:
                    continue

                # Filter by requested type
                if duplicate_type == 'intra-project' and not same_project:
                    continue
                if duplicate_type == 'cross-project' and same_project:
                    continue

                # Class names are identical (from hash table), so similarity = 1.0
                class_sim = 1.0

                # Calculate method similarity
                method_sim, matching_methods = calculate_method_similarity(
                    class1.method_names(),
                    class2.method_names()
                )

                # Combined similarity
                combined_sim = (class_sim + method_sim) / 2

                # Check threshold
                if combined_sim >= threshold:
                    # Determine duplicate category
                    if same_project:
                        dup_type = "INTRA_PROJECT"
                        priority = "CRITICAL"
                    elif both_docker:
                        dup_type = "CROSS_PROJECT_DOCKER"
                        priority = "HIGH"
                    elif both_non_docker:
                        dup_type = "CROSS_PROJECT_NON_DOCKER"
                        priority = "MEDIUM"
                    else:
                        dup_type = "CROSS_STRATEGY"
                        priority = "LOW"

                    duplicates.append(DuplicateMatch(
                        class1=class1,
                        class2=class2,
                        class_similarity=class_sim,
                        method_similarity=method_sim,
                        combined_similarity=combined_sim,
                        matching_methods=matching_methods,
                        duplicate_type=dup_type,
                        priority=priority
                    ))

    # Sort by priority and similarity
    priority_order = {"CRITICAL": 1, "HIGH": 2, "MEDIUM": 3, "LOW": 4}
    duplicates.sort(key=lambda d: (priority_order[d.priority], -d.combined_similarity))

    return duplicates


def generate_text_report(duplicates: List[DuplicateMatch], test_classes: List[TestClass], output_file: Path):
    """Generate human-readable text report"""

    output_lines = []

    def out(line=''):
        output_lines.append(line)
        print(line)

    out("=" * 100)
    out("TRUE DUPLICATE DETECTION - SIMILARITY-BASED ANALYSIS")
    out("Threshold: ≥88% similarity (Levenshtein distance)")
    out("=" * 100)
    out()

    # Statistics
    docker_projects = set(tc.project for tc in test_classes if tc.is_docker_test)
    non_docker_projects = set(tc.project for tc in test_classes if not tc.is_docker_test)

    out(f"Projects Analyzed: {len(set(tc.project for tc in test_classes))}")
    out(f"  Docker-based: {len(docker_projects)}")
    out(f"  Non-Docker: {len(non_docker_projects)}")
    out(f"  Excluded: {', '.join(EXCLUDED_PROJECTS)}")
    out()
    out(f"Total Test Classes: {len(test_classes)}")
    out(f"Duplicates Found: {len(duplicates)}")
    out()

    # Group by type
    critical = [d for d in duplicates if d.priority == "CRITICAL"]
    high = [d for d in duplicates if d.priority == "HIGH"]
    medium = [d for d in duplicates if d.priority == "MEDIUM"]
    low = [d for d in duplicates if d.priority == "LOW"]

    out(f"  🔴 CRITICAL (same project, different namespace): {len(critical)}")
    out(f"  🟠 HIGH (cross-project, same strategy): {len(high)}")
    out(f"  🟡 MEDIUM (cross-project, mixed): {len(medium)}")
    out(f"  🟢 LOW (cross-strategy): {len(low)}")
    out()

    # CRITICAL section
    if critical:
        out("=" * 100)
        out("🔴 CRITICAL - Same Project, Different Namespace (Copy/Paste Duplication)")
        out("=" * 100)
        out()

        for dup in critical:
            out(f"Class: {dup.class1.class_name}")
            out(f"Similarity: {dup.combined_similarity:.1%} (class: {dup.class_similarity:.1%}, methods: {dup.method_similarity:.1%})")
            out(f"Project: {dup.class1.project}")
            out()
            out(f"  Copy 1: {dup.class1.namespace}")
            out(f"          {dup.class1.file_path}")
            out(f"          Methods: {len(dup.class1.methods)}")
            out()
            out(f"  Copy 2: {dup.class2.namespace}")
            out(f"          {dup.class2.file_path}")
            out(f"          Methods: {len(dup.class2.methods)}")
            out()
            out(f"  Matching Methods: {len(dup.matching_methods)}/{max(len(dup.class1.methods), len(dup.class2.methods))}")
            for m1, m2, sim in dup.matching_methods[:5]:  # Show first 5
                out(f"    - {m1} ≈ {m2} ({sim:.1%})")
            if len(dup.matching_methods) > 5:
                out(f"    ... and {len(dup.matching_methods) - 5} more")
            out()
            out(f"  💡 RECOMMENDATION: DELETE one copy (likely copy/paste duplication)")
            out()
            out("-" * 100)
            out()

    # HIGH section
    if high:
        out("=" * 100)
        out("🟠 HIGH - Cross-Project, Same Strategy")
        out("=" * 100)
        out()

        for dup in high:
            out(f"Class: {dup.class1.class_name}")
            out(f"Similarity: {dup.combined_similarity:.1%}")
            out(f"Strategy: {'Docker-based' if dup.class1.is_docker_test else 'Non-Docker'}")
            out()
            out(f"  Project 1: {dup.class1.project}")
            out(f"             {dup.class1.file_path}")
            out()
            out(f"  Project 2: {dup.class2.project}")
            out(f"             {dup.class2.file_path}")
            out()
            out(f"  Matching Methods: {len(dup.matching_methods)}/{max(len(dup.class1.methods), len(dup.class2.methods))}")
            out()
            out(f"  💡 RECOMMENDATION: Review - may be testing same functionality twice")
            out()
            out("-" * 100)
            out()

    # Summary
    out("=" * 100)
    out("SUMMARY")
    out("=" * 100)
    out()
    out(f"Total Duplicates: {len(duplicates)}")
    out(f"  CRITICAL (immediate action): {len(critical)}")
    out(f"  HIGH (review needed): {len(high)}")
    out(f"  MEDIUM/LOW (informational): {len(medium) + len(low)}")
    out()
    out("=" * 100)

    # Write to file
    with open(output_file, 'w', encoding='utf-8') as f:
        f.write('\n'.join(output_lines))

    print(f"\n📄 Report saved to {output_file}")


def generate_json_output(duplicates: List[DuplicateMatch], test_classes: List[TestClass], output_file: Path):
    """Generate JSON output for automation (deletion script)"""

    report = {
        "generated_date": datetime.now().isoformat(),
        "threshold": 0.88,
        "excluded_projects": list(EXCLUDED_PROJECTS),
        "statistics": {
            "total_projects": len(set(tc.project for tc in test_classes)),
            "total_test_classes": len(test_classes),
            "docker_projects": len(set(tc.project for tc in test_classes if tc.is_docker_test)),
            "non_docker_projects": len(set(tc.project for tc in test_classes if not tc.is_docker_test)),
            "total_duplicates": len(duplicates),
            "critical_duplicates": sum(1 for d in duplicates if d.priority == "CRITICAL"),
            "high_duplicates": sum(1 for d in duplicates if d.priority == "HIGH"),
        },
        "duplicates": []
    }

    for dup in duplicates:
        duplicate_entry = {
            "priority": dup.priority,
            "duplicate_type": dup.duplicate_type,
            "combined_similarity": round(dup.combined_similarity, 3),
            "class_similarity": round(dup.class_similarity, 3),
            "method_similarity": round(dup.method_similarity, 3),
            "class1": {
                "class_name": dup.class1.class_name,
                "namespace": dup.class1.namespace,
                "project": dup.class1.project,
                "file_path": dup.class1.file_path,
                "method_count": len(dup.class1.methods),
                "is_docker_test": dup.class1.is_docker_test,
            },
            "class2": {
                "class_name": dup.class2.class_name,
                "namespace": dup.class2.namespace,
                "project": dup.class2.project,
                "file_path": dup.class2.file_path,
                "method_count": len(dup.class2.methods),
                "is_docker_test": dup.class2.is_docker_test,
            },
            "matching_methods": [
                {
                    "method1": m1,
                    "method2": m2,
                    "similarity": round(sim, 3)
                }
                for m1, m2, sim in dup.matching_methods
            ],
            # Metadata for deletion automation
            "automation": {
                "can_auto_delete": dup.priority == "CRITICAL",  # Only auto-delete CRITICAL
                "requires_review": dup.priority in ["HIGH", "MEDIUM"],
                "suggested_action": "DELETE_NEWER" if dup.priority == "CRITICAL" else "MANUAL_REVIEW",
                "deletion_candidate": dup.class2.file_path,  # Suggest deleting class2 (arbitrary choice)
                "keep_candidate": dup.class1.file_path,
            }
        }

        report["duplicates"].append(duplicate_entry)

    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(report, f, indent=2)

    print(f"📄 JSON output saved to {output_file}")
    print(f"   Structure ready for automation script")


def main():
    args = parse_args()

    print(f"🔍 Scanning test files in {args.root}...")
    print(f"   Excluding: {', '.join(EXCLUDED_PROJECTS)}")
    print(f"   Similarity threshold: {args.threshold:.0%}")
    print()

    # Extract test classes
    root_path = Path(args.root)
    test_classes = extract_test_classes(root_path)

    print(f"   Found {len(test_classes)} test classes")
    docker_count = sum(1 for tc in test_classes if tc.is_docker_test)
    print(f"   Docker-based: {docker_count}")
    print(f"   Non-Docker: {len(test_classes) - docker_count}")
    print()

    # Find duplicates
    print(f"🤖 Finding duplicates (≥{args.threshold:.0%} similarity)...")
    duplicates = find_duplicates(test_classes, args.threshold, args.type)
    print(f"   Found {len(duplicates)} duplicate matches")
    print()

    # Generate reports
    print(f"📊 Generating text report...")
    generate_text_report(duplicates, test_classes, Path(args.output))

    print(f"\n📄 Generating JSON output for automation...")
    generate_json_output(duplicates, test_classes, Path(args.output_json))

    print("\n✅ Analysis complete!")
    print(f"\nNext steps:")
    print(f"  1. Review report: {args.output}")
    print(f"  2. Use JSON for automation: {args.output_json}")


if __name__ == '__main__':
    main()
