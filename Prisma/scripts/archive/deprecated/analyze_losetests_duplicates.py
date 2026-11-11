#!/usr/bin/env python3
"""
Analyze Losetests Directory for Duplicate Test Files
====================================================

PURPOSE: Identifies test files in Losetests that already exist in the proper test directory structure.

SAFETY: Read-only analysis - makes NO modifications to files.

USAGE:
    python scripts/analyze_losetests_duplicates.py --base-path "F:/Dynamic/ExxerAi/ExxerAI"

OUTPUT:
    - Console report showing duplicates, unique files, and similar files
    - JSON report: losetests_duplicate_analysis.json
"""

import os
import json
import hashlib
import difflib
from pathlib import Path
from typing import Dict, List, Tuple, Set
from dataclasses import dataclass, asdict
import argparse


@dataclass
class FileComparison:
    """Represents a comparison between a Losetests file and a test directory file."""
    losetests_file: str
    match_type: str  # 'exact', 'similar', 'unique'
    matching_files: List[str]
    similarity_score: float
    file_size: int
    content_hash: str


class LosetestsAnalyzer:
    """Analyzes Losetests directory for duplicate test files."""

    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.losetests_dir = self.base_path / "code" / "src" / "Losetests"
        self.tests_dir = self.base_path / "code" / "src" / "tests"
        self.results: List[FileComparison] = []

    def calculate_file_hash(self, file_path: Path) -> str:
        """Calculate MD5 hash of file content."""
        try:
            with open(file_path, 'rb') as f:
                return hashlib.md5(f.read()).hexdigest()
        except Exception as e:
            print(f"⚠️  Error calculating hash for {file_path}: {e}")
            return ""

    def calculate_similarity(self, content1: str, content2: str) -> float:
        """Calculate similarity ratio between two file contents."""
        try:
            return difflib.SequenceMatcher(None, content1, content2).ratio()
        except Exception:
            return 0.0

    def find_matching_files(self, filename: str) -> List[Path]:
        """Find all files with the same name in the tests directory."""
        matching_files = []

        if not self.tests_dir.exists():
            print(f"⚠️  Tests directory not found: {self.tests_dir}")
            return matching_files

        for test_file in self.tests_dir.rglob(filename):
            if test_file.is_file():
                matching_files.append(test_file)

        return matching_files

    def compare_file(self, losetests_file: Path) -> FileComparison:
        """Compare a Losetests file against the test directory structure."""
        filename = losetests_file.name
        file_size = losetests_file.stat().st_size
        content_hash = self.calculate_file_hash(losetests_file)

        # Find files with matching names
        matching_files = self.find_matching_files(filename)

        if not matching_files:
            return FileComparison(
                losetests_file=str(losetests_file.relative_to(self.base_path)),
                match_type='unique',
                matching_files=[],
                similarity_score=0.0,
                file_size=file_size,
                content_hash=content_hash
            )

        # Read source file content
        try:
            with open(losetests_file, 'r', encoding='utf-8') as f:
                source_content = f.read()
        except Exception as e:
            print(f"⚠️  Error reading {losetests_file}: {e}")
            return FileComparison(
                losetests_file=str(losetests_file.relative_to(self.base_path)),
                match_type='error',
                matching_files=[],
                similarity_score=0.0,
                file_size=file_size,
                content_hash=content_hash
            )

        # Compare with each matching file
        best_match_score = 0.0
        exact_matches = []
        similar_matches = []

        for match_file in matching_files:
            match_hash = self.calculate_file_hash(match_file)

            # Check for exact hash match
            if match_hash == content_hash:
                exact_matches.append(str(match_file.relative_to(self.base_path)))
                best_match_score = 1.0
            else:
                # Calculate similarity
                try:
                    with open(match_file, 'r', encoding='utf-8') as f:
                        match_content = f.read()

                    similarity = self.calculate_similarity(source_content, match_content)

                    if similarity > best_match_score:
                        best_match_score = similarity

                    if similarity >= 0.8:  # 80% similarity threshold
                        similar_matches.append(str(match_file.relative_to(self.base_path)))

                except Exception as e:
                    print(f"⚠️  Error comparing with {match_file}: {e}")

        # Determine match type
        if exact_matches:
            match_type = 'exact'
            all_matches = exact_matches
        elif similar_matches:
            match_type = 'similar'
            all_matches = similar_matches
        else:
            match_type = 'name_only'
            all_matches = [str(f.relative_to(self.base_path)) for f in matching_files]

        return FileComparison(
            losetests_file=str(losetests_file.relative_to(self.base_path)),
            match_type=match_type,
            matching_files=all_matches,
            similarity_score=best_match_score,
            file_size=file_size,
            content_hash=content_hash
        )

    def analyze(self) -> Dict:
        """Perform full analysis of Losetests directory."""
        print("\n" + "="*80)
        print("🔍 LOSETESTS DUPLICATE ANALYSIS")
        print("="*80)

        if not self.losetests_dir.exists():
            print(f"\n❌ ERROR: Losetests directory not found: {self.losetests_dir}")
            return {}

        if not self.tests_dir.exists():
            print(f"\n❌ ERROR: Tests directory not found: {self.tests_dir}")
            return {}

        print(f"\n📂 Scanning: {self.losetests_dir}")
        print(f"📂 Comparing against: {self.tests_dir}")

        # Get all files in Losetests
        losetests_files = [f for f in self.losetests_dir.iterdir() if f.is_file()]

        print(f"\n📊 Found {len(losetests_files)} files in Losetests")
        print("\n⏳ Analyzing files...\n")

        # Analyze each file
        for losetests_file in sorted(losetests_files):
            print(f"   Analyzing: {losetests_file.name}")
            comparison = self.compare_file(losetests_file)
            self.results.append(comparison)

        # Generate summary
        exact_matches = [r for r in self.results if r.match_type == 'exact']
        similar_matches = [r for r in self.results if r.match_type == 'similar']
        name_only_matches = [r for r in self.results if r.match_type == 'name_only']
        unique_files = [r for r in self.results if r.match_type == 'unique']

        summary = {
            'total_files': len(losetests_files),
            'exact_duplicates': len(exact_matches),
            'similar_files': len(similar_matches),
            'name_only_matches': len(name_only_matches),
            'unique_files': len(unique_files),
            'losetests_directory': str(self.losetests_dir.relative_to(self.base_path)),
            'tests_directory': str(self.tests_dir.relative_to(self.base_path))
        }

        report = {
            'summary': summary,
            'exact_duplicates': [asdict(r) for r in exact_matches],
            'similar_files': [asdict(r) for r in similar_matches],
            'name_only_matches': [asdict(r) for r in name_only_matches],
            'unique_files': [asdict(r) for r in unique_files]
        }

        return report

    def print_report(self, report: Dict):
        """Print analysis report to console."""
        if not report:
            return

        summary = report['summary']

        print("\n" + "="*80)
        print("📊 ANALYSIS SUMMARY")
        print("="*80)
        print(f"Total files in Losetests:  {summary['total_files']}")
        print(f"  ✅ Exact duplicates:      {summary['exact_duplicates']}")
        print(f"  ⚠️  Similar files:         {summary['similar_files']}")
        print(f"  📝 Name-only matches:     {summary['name_only_matches']}")
        print(f"  🆕 Unique files:          {summary['unique_files']}")

        # Exact duplicates
        if report['exact_duplicates']:
            print("\n" + "="*80)
            print("✅ EXACT DUPLICATES (Can be safely removed from Losetests)")
            print("="*80)
            for dup in report['exact_duplicates']:
                print(f"\n📄 {Path(dup['losetests_file']).name}")
                print(f"   Losetests: {dup['losetests_file']}")
                print(f"   Matches:")
                for match in dup['matching_files']:
                    print(f"      → {match}")

        # Similar files
        if report['similar_files']:
            print("\n" + "="*80)
            print("⚠️  SIMILAR FILES (Review before removing)")
            print("="*80)
            for sim in report['similar_files']:
                print(f"\n📄 {Path(sim['losetests_file']).name}")
                print(f"   Similarity: {sim['similarity_score']:.1%}")
                print(f"   Losetests: {sim['losetests_file']}")
                print(f"   Matches:")
                for match in sim['matching_files']:
                    print(f"      → {match}")

        # Name-only matches
        if report['name_only_matches']:
            print("\n" + "="*80)
            print("📝 NAME-ONLY MATCHES (Same filename, different content)")
            print("="*80)
            for match in report['name_only_matches']:
                print(f"\n📄 {Path(match['losetests_file']).name}")
                print(f"   Similarity: {match['similarity_score']:.1%}")
                print(f"   Losetests: {match['losetests_file']}")
                print(f"   Found in:")
                for found in match['matching_files']:
                    print(f"      → {found}")

        # Unique files
        if report['unique_files']:
            print("\n" + "="*80)
            print("🆕 UNIQUE FILES (Not found in test directories)")
            print("="*80)
            for unique in report['unique_files']:
                print(f"   • {Path(unique['losetests_file']).name}")

        print("\n" + "="*80)
        print("💡 RECOMMENDATIONS")
        print("="*80)
        print(f"1. ✅ Safe to delete: {summary['exact_duplicates']} exact duplicates")
        print(f"2. ⚠️  Review carefully: {summary['similar_files']} similar files")
        print(f"3. 📝 Manual review needed: {summary['name_only_matches']} name-only matches")
        print(f"4. 🆕 Possibly new tests: {summary['unique_files']} unique files")
        print("\n")

    def save_report(self, report: Dict, output_file: str):
        """Save analysis report to JSON file."""
        output_path = self.base_path / output_file

        try:
            with open(output_path, 'w', encoding='utf-8') as f:
                json.dump(report, f, indent=2)
            print(f"✅ Report saved to: {output_path}")
        except Exception as e:
            print(f"❌ Error saving report: {e}")


def main():
    parser = argparse.ArgumentParser(
        description='Analyze Losetests directory for duplicate test files',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python scripts/analyze_losetests_duplicates.py --base-path "F:/Dynamic/ExxerAi/ExxerAI"

This script is SAFE - it only reads files and generates reports.
        """
    )

    parser.add_argument(
        '--base-path',
        type=str,
        required=True,
        help='Base path to ExxerAI repository'
    )

    parser.add_argument(
        '--output',
        type=str,
        default='losetests_duplicate_analysis.json',
        help='Output JSON report filename (default: losetests_duplicate_analysis.json)'
    )

    args = parser.parse_args()

    # Run analysis
    analyzer = LosetestsAnalyzer(args.base_path)
    report = analyzer.analyze()

    if report:
        analyzer.print_report(report)
        analyzer.save_report(report, args.output)

    return 0


if __name__ == '__main__':
    exit(main())
