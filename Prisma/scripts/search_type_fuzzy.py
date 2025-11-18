#!/usr/bin/env python3
"""
Fuzzy Type Search - Find types by name with Levenshtein distance matching
Quickly find type metadata without exact name knowledge.

Features:
- Fuzzy matching using Levenshtein distance
- Returns type name, project, namespace, file location
- Supports partial matches and typo tolerance
- Fast in-memory search using cached JSON

Usage:
    python scripts/search_type_fuzzy.py "DocumentNotifcation" --max-distance 3
    python scripts/search_type_fuzzy.py "IDocNotif" --limit 5
    python scripts/search_type_fuzzy.py "NPOIAdapter" --exact

Author: Claude Code Agent
Date: 2025-11-08
"""

import json
import argparse
from pathlib import Path
from typing import Dict, List, Tuple
from datetime import datetime
import sys


def levenshtein_distance(s1: str, s2: str) -> int:
    """
    Calculate Levenshtein distance between two strings.
    Returns the minimum number of single-character edits (insertions, deletions, substitutions).

    Uses dynamic programming for O(m*n) complexity.
    """
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


def case_insensitive_contains(search: str, target: str) -> bool:
    """Check if search string is contained in target (case-insensitive)."""
    return search.lower() in target.lower()


class FuzzyTypeSearcher:
    """Fuzzy search for types in ExxerAI codebase using cached JSON."""

    def __init__(self, json_file: str = None):
        """Initialize with type database JSON file."""
        if json_file is None:
            # Default to latest scan file
            json_file = self._find_latest_json()

        self.json_file = Path(json_file)

        if not self.json_file.exists():
            raise FileNotFoundError(f"Type database not found: {json_file}\nRun: python scripts/scan_exxerai_types.py")

        print(f"Loading type database: {self.json_file.name}")
        with open(self.json_file, 'r', encoding='utf-8') as f:
            self.data = json.load(f)

        self.all_types = self.data.get('all_types', {})
        print(f"Loaded {len(self.all_types)} types from database\n")

    def _find_latest_json(self) -> Path:
        """Find the canonical type database file."""
        scripts_dir = Path(__file__).parent

        # Priority 1: Use the canonical latest file (updated incrementally)
        latest_file = scripts_dir / "exxerai_types_latest.json"
        if latest_file.exists():
            return latest_file

        # Priority 2: Fallback to default name
        default_file = scripts_dir / "exxerai_types.json"
        if default_file.exists():
            return default_file

        # Priority 3: Find most recent timestamped file (legacy/backup)
        json_files = list(scripts_dir.glob("exxerai_types_*.json"))
        if json_files:
            return max(json_files, key=lambda p: p.stat().st_mtime)

        # No database found
        raise FileNotFoundError(
            f"No type database found in {scripts_dir}\n"
            f"Expected: exxerai_types_latest.json\n"
            f"Run: python scripts/scan_exxerai_types.py --base-path . --output scripts/exxerai_types_latest.json"
        )

    def search_exact(self, type_name: str) -> List[Dict]:
        """Search for exact type name matches (case-insensitive)."""
        results = []

        for full_name, metadata in self.all_types.items():
            if metadata['name'].lower() == type_name.lower():
                results.append({
                    'match_type': 'exact',
                    'distance': 0,
                    'score': 100.0,
                    **metadata
                })

        return results

    def search_fuzzy(self, search_term: str, max_distance: int = 3, limit: int = 10) -> List[Dict]:
        """
        Search for types using fuzzy matching with Levenshtein distance.

        Args:
            search_term: The type name to search for
            max_distance: Maximum Levenshtein distance allowed
            limit: Maximum number of results to return

        Returns:
            List of matching types sorted by relevance (distance, then alphabetically)
        """
        results = []

        for full_name, metadata in self.all_types.items():
            type_name = metadata['name']

            # Calculate Levenshtein distance
            distance = levenshtein_distance(search_term.lower(), type_name.lower())

            # Skip if distance too large
            if distance > max_distance:
                # But check for substring match (partial match)
                if case_insensitive_contains(search_term, type_name):
                    distance = len(type_name) - len(search_term)  # Penalty for length difference
                else:
                    continue

            # Calculate similarity score (0-100)
            max_len = max(len(search_term), len(type_name))
            similarity = ((max_len - distance) / max_len) * 100

            # Determine match type
            if distance == 0:
                match_type = 'exact'
            elif case_insensitive_contains(search_term, type_name):
                match_type = 'substring'
            else:
                match_type = 'fuzzy'

            results.append({
                'match_type': match_type,
                'distance': distance,
                'score': round(similarity, 2),
                **metadata
            })

        # Sort by distance (ascending), then score (descending), then name
        results.sort(key=lambda x: (x['distance'], -x['score'], x['name']))

        return results[:limit]

    def search_by_project(self, project_name: str) -> List[Dict]:
        """Search for all types in a specific project."""
        results = []

        for full_name, metadata in self.all_types.items():
            if project_name.lower() in metadata['project'].lower():
                results.append(metadata)

        results.sort(key=lambda x: x['name'])
        return results

    def search_by_namespace(self, namespace: str) -> List[Dict]:
        """Search for all types in a specific namespace."""
        results = []

        for full_name, metadata in self.all_types.items():
            if namespace.lower() in metadata['namespace'].lower():
                results.append(metadata)

        results.sort(key=lambda x: x['name'])
        return results

    def print_results(self, results: List[Dict], verbose: bool = False):
        """Print search results in a formatted table."""
        if not results:
            print("❌ No matches found")
            return

        print(f"✅ Found {len(results)} matches:\n")

        for i, result in enumerate(results, 1):
            match_type = result.get('match_type', 'match')
            distance = result.get('distance', 0)
            score = result.get('score', 100.0)

            # Match type emoji
            emoji = {
                'exact': '🎯',
                'substring': '🔍',
                'fuzzy': '🌟'
            }.get(match_type, '✓')

            print(f"{emoji} [{i}] {result['name']} (Score: {score}%)")
            print(f"    Project:   {result['project']}")
            print(f"    Namespace: {result['namespace']}")
            print(f"    Kind:      {result['kind']}")

            if verbose:
                print(f"    File:      {result['file']}")
                if distance > 0:
                    print(f"    Distance:  {distance}")

            print()


def main():
    parser = argparse.ArgumentParser(
        description='Fuzzy search for types in ExxerAI codebase',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Fuzzy search with typo tolerance
  python scripts/search_type_fuzzy.py "DocumentNotifcation" --max-distance 3

  # Find types with partial name
  python scripts/search_type_fuzzy.py "IDocNotif" --limit 5

  # Exact match only
  python scripts/search_type_fuzzy.py "NPOIAdapter" --exact

  # Search by project
  python scripts/search_type_fuzzy.py --project "ExxerAI.Application"

  # Search by namespace
  python scripts/search_type_fuzzy.py --namespace "ExxerAI.Application.Interfaces"

  # Verbose output with file paths
  python scripts/search_type_fuzzy.py "DocumentAsset" --verbose
        """
    )

    parser.add_argument('search_term', nargs='?', help='Type name to search for')
    parser.add_argument('--json', help='Path to type database JSON file (default: auto-detect latest)')
    parser.add_argument('--max-distance', type=int, default=3,
                       help='Maximum Levenshtein distance for fuzzy matching (default: 3)')
    parser.add_argument('--limit', type=int, default=10,
                       help='Maximum number of results to return (default: 10)')
    parser.add_argument('--exact', action='store_true',
                       help='Only exact matches (case-insensitive)')
    parser.add_argument('--project', help='Search for all types in a project')
    parser.add_argument('--namespace', help='Search for all types in a namespace')
    parser.add_argument('--verbose', '-v', action='store_true',
                       help='Show detailed information including file paths')

    args = parser.parse_args()

    # Validate arguments
    if not args.search_term and not args.project and not args.namespace:
        parser.error("Must provide search_term, --project, or --namespace")

    try:
        searcher = FuzzyTypeSearcher(args.json)

        # Execute appropriate search
        if args.project:
            results = searcher.search_by_project(args.project)
        elif args.namespace:
            results = searcher.search_by_namespace(args.namespace)
        elif args.exact:
            results = searcher.search_exact(args.search_term)
        else:
            results = searcher.search_fuzzy(args.search_term, args.max_distance, args.limit)

        searcher.print_results(results, args.verbose)

        # Exit code based on results
        sys.exit(0 if results else 1)

    except FileNotFoundError as e:
        print(f"❌ Error: {e}")
        print("\n💡 Solution: Run the type scanner first:")
        print("   python scripts/scan_exxerai_types.py --base-path . --output scripts/exxerai_types.json")
        sys.exit(1)
    except Exception as e:
        print(f"❌ Unexpected error: {e}")
        sys.exit(1)


if __name__ == "__main__":
    main()
