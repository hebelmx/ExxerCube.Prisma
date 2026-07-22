#!/usr/bin/env python3
"""
build_corpus_index.py — RC1.S1 corpus-index builder.

Reads the anonymized VEC real-corpus staging tree (out of repo, default
``~/Downloads/vec-corpus-staging/``) plus the source manifest
(``~/Downloads/statement-manifest.json``) and writes ``corpus-index.json``
INTO the staging tree (never into the repo).

The index uses NEUTRAL account labels (A/B/C) in the ``id`` field only.
``relativePath`` is relative to the staging root and may contain the real
staging folder names (they carry a real account's last-4 digits as a
folder-name suffix) — that is safe because the index itself never enters git
(see scripts/veriqan-corpus/README.md, "Hard PII rules"). This script must
never hardcode or print one of those folder names' digit suffixes outside of
values read from the staging tree at run time.

Usage:
    python3 build_corpus_index.py
    python3 build_corpus_index.py --staging-root ~/Downloads/vec-corpus-staging \
        --manifest ~/Downloads/statement-manifest.json \
        --out ~/Downloads/vec-corpus-staging/corpus-index.json

No network calls, no external dependencies (stdlib only).
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path

SCHEMA_VERSION = 1

# Staging folder name -> (neutral account label, product enum)
ACCOUNT_FOLDER_PREFIXES: dict[str, tuple[str, str]] = {
    "account-A-priority": ("A", "checking"),
    "account-B-visa": ("B", "credit_card"),  # prefix match (folder carries real last-4 suffix)
    "account-C-mc": ("C", "credit_card"),
}

PERIOD_PATTERN = re.compile(r"^(\d{4}-\d{2})\.pdf$")
DEFECT_PATTERN = re.compile(r"^([A-Za-z0-9\-]+)\.pdf$")


def sha256_of(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def resolve_account(folder_name: str) -> tuple[str, str] | None:
    for prefix, mapping in ACCOUNT_FOLDER_PREFIXES.items():
        if folder_name == prefix or folder_name.startswith(prefix + "-"):
            return mapping
    return None


def build_index(staging_root: Path, manifest_path: Path | None) -> dict:
    entries: list[dict] = []

    # Sanity cross-reference against the source manifest, if present — does NOT
    # feed any field into the index (keeps the index minimal / PII-lean even
    # though it lives out of repo).
    expected_accounts: dict[str, list[str]] = {}
    if manifest_path is not None and manifest_path.exists():
        with manifest_path.open("r", encoding="utf-8") as f:
            manifest = json.load(f)
        for label, acc in manifest.get("summary", {}).get("accounts", {}).items():
            months = acc.get("months", [])
            # label is like "account-A-priority" in this manifest's own schema
            expected_accounts[label] = months

    for child in sorted(staging_root.iterdir()):
        if not child.is_dir():
            continue

        if child.name == "defects":
            for pdf_path in sorted(child.glob("*.pdf")):
                m = DEFECT_PATTERN.match(pdf_path.name)
                if not m:
                    continue
                defect_kind = m.group(1)
                entries.append(
                    {
                        "id": f"defect-{defect_kind}",
                        "relativePath": f"defects/{pdf_path.name}",
                        "sha256": sha256_of(pdf_path),
                        "product": "defect",
                        "defectKind": defect_kind,
                    }
                )
            continue

        resolved = resolve_account(child.name)
        if resolved is None:
            print(f"WARNING: unrecognized staging folder '{child.name}' — skipped", file=sys.stderr)
            continue

        account_label, product = resolved
        for pdf_path in sorted(child.glob("*.pdf")):
            m = PERIOD_PATTERN.match(pdf_path.name)
            if not m:
                print(f"WARNING: unrecognized file '{pdf_path}' in {child.name} — skipped", file=sys.stderr)
                continue
            period = m.group(1)
            entries.append(
                {
                    "id": f"{account_label}-{period}",
                    "relativePath": f"{child.name}/{pdf_path.name}",
                    "sha256": sha256_of(pdf_path),
                    "product": product,
                    "period": period,
                    "accountLabel": account_label,
                }
            )

    return {
        "schemaVersion": SCHEMA_VERSION,
        "generatedFrom": str(staging_root),
        "entryCount": len(entries),
        "files": entries,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--staging-root",
        type=Path,
        default=Path.home() / "Downloads" / "vec-corpus-staging",
        help="Directory containing account-*/defects PDF subfolders (out of repo).",
    )
    parser.add_argument(
        "--manifest",
        type=Path,
        default=Path.home() / "Downloads" / "statement-manifest.json",
        help="Source manifest JSON used only for a sanity cross-reference (optional).",
    )
    parser.add_argument(
        "--out",
        type=Path,
        default=None,
        help="Output path for corpus-index.json (default: <staging-root>/corpus-index.json).",
    )
    args = parser.parse_args()

    staging_root: Path = args.staging_root.expanduser().resolve()
    if not staging_root.is_dir():
        print(f"ERROR: staging root not found: {staging_root}", file=sys.stderr)
        return 1

    manifest_path = args.manifest.expanduser().resolve() if args.manifest else None
    out_path = (args.out or (staging_root / "corpus-index.json")).expanduser().resolve()

    # Refuse to write the index anywhere inside a git repository — belt-and-suspenders
    # against accidentally landing it in-repo.
    for parent in out_path.parents:
        if (parent / ".git").exists():
            print(
                f"ERROR: refusing to write corpus-index.json under a git repo root: {parent}",
                file=sys.stderr,
            )
            return 1

    index = build_index(staging_root, manifest_path)

    out_path.parent.mkdir(parents=True, exist_ok=True)
    with out_path.open("w", encoding="utf-8") as f:
        json.dump(index, f, indent=2, sort_keys=False)
        f.write("\n")

    print(f"Wrote {index['entryCount']} entries to {out_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
