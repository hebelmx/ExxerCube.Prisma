"""Query helpers for package crawler v2 artifacts."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Optional, Sequence


def find_package(*, index_data: dict, package_id: str, version: Optional[str] = None) -> Optional[dict]:
    """Return the matching package entry from the v2 index."""
    for entry in index_data.get("packages", []):
        if entry.get("packageId") != package_id:
            continue
        if version and entry.get("version") != version:
            continue
        return entry
    return None


def _parse_args(argv: Optional[Sequence[str]] = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Query v2 package artifacts.")
    parser.add_argument("--index", required=True, type=Path, help="Path to artifacts/externals_v2/index.json")
    parser.add_argument("--package", dest="package_id", help="Package ID to inspect.")
    parser.add_argument("--version", help="Optional version filter.")
    parser.add_argument("--all", action="store_true", help="Return the full index instead of default-project packages.")
    return parser.parse_args(argv)


def main(argv: Optional[Sequence[str]] = None) -> int:
    args = _parse_args(argv)
    index_data = json.loads(args.index.read_text(encoding="utf-8"))
    if args.package_id:
        entry = find_package(index_data=index_data, package_id=args.package_id, version=args.version)
        if entry:
            print(json.dumps(entry, indent=2))
        else:
            print("Package not found")
    else:
        packages = index_data.get("packages", [])
        if not args.all:
            defaults = [entry for entry in packages if entry.get("isDefaultProject")]
            if defaults:
                print(json.dumps(defaults, indent=2))
                return 0
        print(json.dumps(packages, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
