#!/usr/bin/env python3
"""
Sample KPI Fixture Documents for Helix OCR Tests

Walks through KpiExxerpro/Fixture directory structure, analyzes document sizes,
and selects a representative sample using 1-sigma distribution (around 20MB target).

Strategy:
- Scan all PDF, PNG, JPG, JPEG, TIFF files recursively
- Calculate mean and std deviation of file sizes
- Select files within 1 standard deviation of mean (±1σ)
- Target: ~40 files, ~20MB total
- Rename files with sequential numbering for test clarity
- Copy to Helix test Fixtures directory

Usage:
    python scripts/sample_kpi_fixtures.py
    python scripts/sample_kpi_fixtures.py --target-count 50 --target-size-mb 25
    python scripts/sample_kpi_fixtures.py --dry-run

Author: Claude Code Agent
Date: 2025-11-09
"""

import os
import shutil
import random
import argparse
from pathlib import Path
from typing import List, Tuple
from dataclasses import dataclass
import statistics

@dataclass
class DocumentFile:
    """Represents a document file with metadata."""
    path: Path
    size_bytes: int
    extension: str

    @property
    def size_mb(self) -> float:
        return self.size_bytes / (1024 * 1024)


class KPIFixtureSampler:
    """Samples documents from KPI repository for test fixtures."""

    # Document extensions to include
    VALID_EXTENSIONS = {'.pdf', '.png', '.jpg', '.jpeg', '.tiff', '.tif'}

    def __init__(self, source_dir: Path, target_dir: Path):
        """Initialize sampler with source and target directories."""
        self.source_dir = source_dir
        self.target_dir = target_dir
        self.documents: List[DocumentFile] = []

    def scan_documents(self) -> None:
        """Scan source directory recursively for valid documents."""
        print(f"🔍 Scanning {self.source_dir} for documents...")

        for root, _, files in os.walk(self.source_dir):
            for filename in files:
                ext = Path(filename).suffix.lower()
                if ext in self.VALID_EXTENSIONS:
                    filepath = Path(root) / filename
                    try:
                        size = filepath.stat().st_size
                        # Skip empty files and extremely large files (>50MB)
                        if 0 < size < 50 * 1024 * 1024:
                            self.documents.append(DocumentFile(
                                path=filepath,
                                size_bytes=size,
                                extension=ext
                            ))
                    except Exception as e:
                        print(f"⚠️  Warning: Could not stat {filepath}: {e}")

        print(f"✅ Found {len(self.documents)} valid documents")

    def analyze_size_distribution(self) -> Tuple[float, float]:
        """Calculate mean and standard deviation of file sizes."""
        if not self.documents:
            return 0.0, 0.0

        sizes = [doc.size_bytes for doc in self.documents]
        mean_size = statistics.mean(sizes)
        std_dev = statistics.stdev(sizes) if len(sizes) > 1 else 0

        print(f"\n📊 Size Distribution:")
        print(f"   Total documents: {len(self.documents)}")
        print(f"   Mean size: {mean_size / 1024 / 1024:.2f} MB")
        print(f"   Std deviation: {std_dev / 1024 / 1024:.2f} MB")
        print(f"   Min size: {min(sizes) / 1024:.2f} KB")
        print(f"   Max size: {max(sizes) / 1024 / 1024:.2f} MB")

        return mean_size, std_dev

    def select_sample(self, target_count: int = 40, target_size_mb: float = 20.0) -> List[DocumentFile]:
        """
        Select sample of documents using 1-sigma distribution.

        Args:
            target_count: Target number of files to select
            target_size_mb: Target total size in megabytes

        Returns:
            List of selected DocumentFile objects
        """
        if not self.documents:
            print("❌ No documents to sample from")
            return []

        mean_size, std_dev = self.analyze_size_distribution()

        # Filter documents within 1 standard deviation of mean
        lower_bound = max(0, mean_size - std_dev)
        upper_bound = mean_size + std_dev

        in_range = [
            doc for doc in self.documents
            if lower_bound <= doc.size_bytes <= upper_bound
        ]

        print(f"\n🎯 1-Sigma Selection (±{std_dev / 1024 / 1024:.2f} MB):")
        print(f"   Documents in range: {len(in_range)}")
        print(f"   Size range: {lower_bound / 1024:.2f} KB - {upper_bound / 1024 / 1024:.2f} MB")

        if not in_range:
            print("⚠️  No documents in 1-sigma range, using all documents")
            in_range = self.documents

        # Randomly sample target_count documents
        sample_size = min(target_count, len(in_range))
        selected = random.sample(in_range, sample_size)

        # Sort by extension then size for consistent numbering
        selected.sort(key=lambda x: (x.extension, x.size_bytes))

        total_size_mb = sum(doc.size_mb for doc in selected)

        print(f"\n✅ Selected {len(selected)} documents:")
        print(f"   Total size: {total_size_mb:.2f} MB")
        print(f"   Target size: {target_size_mb:.2f} MB")
        print(f"   Difference: {total_size_mb - target_size_mb:+.2f} MB")

        # Show breakdown by extension
        by_ext = {}
        for doc in selected:
            by_ext[doc.extension] = by_ext.get(doc.extension, 0) + 1

        print(f"\n📋 Breakdown by extension:")
        for ext, count in sorted(by_ext.items()):
            print(f"   {ext}: {count} files")

        return selected

    def copy_sample(self, selected: List[DocumentFile], dry_run: bool = False) -> None:
        """
        Copy selected documents to target directory with sequential naming.

        Args:
            selected: List of DocumentFile objects to copy
            dry_run: If True, only simulate copying
        """
        if not selected:
            print("❌ No documents to copy")
            return

        # Create target directory
        if not dry_run:
            self.target_dir.mkdir(parents=True, exist_ok=True)

        print(f"\n📁 {'[DRY RUN] ' if dry_run else ''}Copying to {self.target_dir}...")

        # Group by extension for sequential numbering
        by_ext = {}
        for doc in selected:
            if doc.extension not in by_ext:
                by_ext[doc.extension] = []
            by_ext[doc.extension].append(doc)

        copied_count = 0
        total_size = 0

        for ext, docs in sorted(by_ext.items()):
            for idx, doc in enumerate(docs, 1):
                # Generate new filename: helix_001.pdf, helix_002.png, etc.
                new_filename = f"helix_{copied_count + 1:03d}{ext}"
                target_path = self.target_dir / new_filename

                if not dry_run:
                    try:
                        shutil.copy2(doc.path, target_path)
                        print(f"   ✓ {new_filename} ({doc.size_mb:.2f} MB)")
                    except Exception as e:
                        print(f"   ✗ Failed to copy {doc.path.name}: {e}")
                        continue
                else:
                    print(f"   → {new_filename} ({doc.size_mb:.2f} MB) ← {doc.path.name}")

                copied_count += 1
                total_size += doc.size_bytes

        print(f"\n{'[DRY RUN] ' if dry_run else ''}✅ Copied {copied_count} files ({total_size / 1024 / 1024:.2f} MB)")


def main():
    parser = argparse.ArgumentParser(
        description='Sample KPI fixture documents for Helix OCR tests',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Default: Select ~40 files, ~20MB total
  python scripts/sample_kpi_fixtures.py

  # Custom target count and size
  python scripts/sample_kpi_fixtures.py --target-count 50 --target-size-mb 25

  # Dry run (show what would be copied without copying)
  python scripts/sample_kpi_fixtures.py --dry-run

  # Custom source directory
  python scripts/sample_kpi_fixtures.py --source-dir "path/to/documents"
        """
    )

    parser.add_argument(
        '--source-dir',
        type=str,
        default='KpiExxerpro/Fixture',
        help='Source directory containing documents (default: KpiExxerpro/Fixture)'
    )

    parser.add_argument(
        '--target-dir',
        type=str,
        default='code/src/tests/04AdapterTests/ExxerAI.Helix.Adapter.Tests/Fixtures',
        help='Target directory for test fixtures (default: Helix.Adapter.Tests/Fixtures)'
    )

    parser.add_argument(
        '--target-count',
        type=int,
        default=40,
        help='Target number of files to select (default: 40)'
    )

    parser.add_argument(
        '--target-size-mb',
        type=float,
        default=20.0,
        help='Target total size in megabytes (default: 20.0)'
    )

    parser.add_argument(
        '--dry-run',
        action='store_true',
        help='Simulate copying without actually copying files'
    )

    parser.add_argument(
        '--seed',
        type=int,
        default=None,
        help='Random seed for reproducible sampling'
    )

    args = parser.parse_args()

    # Set random seed for reproducibility
    if args.seed is not None:
        random.seed(args.seed)
        print(f"🎲 Using random seed: {args.seed}")

    # Convert to Path objects
    source_dir = Path(args.source_dir)
    target_dir = Path(args.target_dir)

    # Validate source directory
    if not source_dir.exists():
        print(f"❌ Error: Source directory not found: {source_dir}")
        print(f"   Current directory: {Path.cwd()}")
        return 1

    print("========================================")
    print("  KPI Fixture Sampler for Helix Tests")
    print("========================================")
    print(f"Source: {source_dir}")
    print(f"Target: {target_dir}")
    print(f"Target count: {args.target_count} files")
    print(f"Target size: {args.target_size_mb} MB")
    if args.dry_run:
        print("Mode: DRY RUN (simulation only)")
    print()

    # Create sampler and run
    sampler = KPIFixtureSampler(source_dir, target_dir)

    # Scan documents
    sampler.scan_documents()

    if not sampler.documents:
        print("❌ No documents found in source directory")
        return 1

    # Select sample
    selected = sampler.select_sample(
        target_count=args.target_count,
        target_size_mb=args.target_size_mb
    )

    if not selected:
        print("❌ No documents selected")
        return 1

    # Copy sample
    sampler.copy_sample(selected, dry_run=args.dry_run)

    print("\n========================================")
    print("  ✅ Sampling Complete!")
    print("========================================")

    if args.dry_run:
        print("\n💡 Tip: Remove --dry-run flag to actually copy files")
    else:
        print(f"\n📂 Files copied to: {target_dir}")
        print("🧪 Ready for Helix OCR integration tests!")

    return 0


if __name__ == "__main__":
    exit(main())
