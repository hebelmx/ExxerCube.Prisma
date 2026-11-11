#!/usr/bin/env python3
"""
Create Large BM25 Performance Test Corpus

Extracts text from 250+ documents for performance/stress testing.
Designed to run overnight or on-demand for benchmarking.

Usage:
    python scripts/create_bm25_performance_corpus.py
    python scripts/create_bm25_performance_corpus.py --count 500
    python scripts/create_bm25_performance_corpus.py --dry-run

Author: Claude Code Agent
Date: 2025-11-10
"""

import json
import random
import hashlib
import statistics
from pathlib import Path
from typing import List, Dict, Any
from dataclasses import dataclass, asdict
from datetime import datetime
import os

# Try to import PDF extraction
try:
    import PyPDF2
    HAS_PYPDF2 = True
except ImportError:
    HAS_PYPDF2 = False
    print("⚠️  PyPDF2 not installed - install with: pip install PyPDF2")


@dataclass
class PerformanceDocument:
    """Full document for performance testing."""
    document_id: str
    title: str
    content: str  # Full text or up to 1000 words
    metadata: Dict[str, Any]
    word_count: int
    char_count: int


class PerformanceCorpusBuilder:
    """Builds large corpus for performance testing."""

    VALID_EXTENSIONS = {'.pdf'}
    MAX_WORDS_PER_DOC = 1000

    def __init__(self, source_dir: Path, target_count: int = 250, seed: int = 42):
        """Initialize builder."""
        self.source_dir = source_dir
        self.target_count = target_count
        self.seed = seed
        self.all_docs: List[Path] = []
        self.corpus_docs: List[PerformanceDocument] = []

        random.seed(seed)

    def scan_documents(self) -> None:
        """Scan for documents."""
        print(f"🔍 Scanning {self.source_dir} recursively...")

        for root, _, files in os.walk(self.source_dir):
            for filename in files:
                if Path(filename).suffix.lower() in self.VALID_EXTENSIONS:
                    filepath = Path(root) / filename
                    try:
                        size = filepath.stat().st_size
                        # Small to medium documents (1KB - 10MB)
                        if 1024 < size < 10 * 1024 * 1024:
                            self.all_docs.append(filepath)
                    except Exception:
                        pass

        print(f"✅ Found {len(self.all_docs)} documents")

    def select_sample(self) -> List[Path]:
        """Select sample using 1-sigma distribution."""
        if not self.all_docs:
            return []

        sizes = [doc.stat().st_size for doc in self.all_docs]
        mean = statistics.mean(sizes)
        std = statistics.stdev(sizes) if len(sizes) > 1 else 0

        print(f"\n📊 Distribution: mean={mean/1024:.1f}KB, σ={std/1024:.1f}KB")

        # 1-sigma range
        lower = max(0, mean - std)
        upper = mean + std

        in_range = [d for d in self.all_docs if lower <= d.stat().st_size <= upper]
        print(f"🎯 1-sigma range: {len(in_range)} documents")

        if not in_range:
            in_range = self.all_docs

        sample_size = min(self.target_count, len(in_range))
        selected = random.sample(in_range, sample_size)
        selected.sort(key=lambda x: x.stat().st_size)

        print(f"✅ Selected {len(selected)} documents")
        return selected

    def extract_pdf_text(self, pdf_path: Path, max_words: int) -> str:
        """Extract text from PDF."""
        if not HAS_PYPDF2:
            return f"[PDF content placeholder: {pdf_path.stem}]"

        try:
            with open(pdf_path, 'rb') as f:
                reader = PyPDF2.PdfReader(f)
                text_parts = []
                word_count = 0

                for page in reader.pages:
                    if word_count >= max_words:
                        break

                    page_text = page.extract_text()
                    if page_text:
                        words = page_text.split()
                        remaining = max_words - word_count
                        text_parts.extend(words[:remaining])
                        word_count += len(words[:remaining])

                return ' '.join(text_parts) if text_parts else "[Empty PDF]"

        except Exception as e:
            return f"[Error: {str(e)[:100]}]"

    def build_corpus(self, selected: List[Path]) -> None:
        """Build corpus from selected documents."""
        print(f"\n📝 Processing {len(selected)} documents...")

        for idx, doc_path in enumerate(selected, 1):
            if idx % 50 == 0:
                print(f"   {idx}/{len(selected)} processed...")

            doc_id = f"perf_{idx:04d}"

            # Extract text
            content = self.extract_pdf_text(doc_path, self.MAX_WORDS_PER_DOC)

            # Skip if too short
            word_count = len(content.split())
            if word_count < 10:
                continue

            # Generate title
            title = doc_path.stem.replace('_', ' ').replace('-', ' ').title()

            # Metadata
            with open(doc_path, 'rb') as f:
                file_hash = hashlib.sha256(f.read()).hexdigest()[:16]

            metadata = {
                "source_file": doc_path.name,
                "source_path": str(doc_path.relative_to(self.source_dir)),
                "file_size": doc_path.stat().st_size,
                "hash": file_hash,
                "index": idx
            }

            doc = PerformanceDocument(
                document_id=doc_id,
                title=title,
                content=content,
                metadata=metadata,
                word_count=word_count,
                char_count=len(content)
            )

            self.corpus_docs.append(doc)

        print(f"✅ Created {len(self.corpus_docs)} corpus entries")

    def save_corpus(self, output_path: Path, dry_run: bool = False) -> None:
        """Save corpus to JSON."""
        total_words = sum(d.word_count for d in self.corpus_docs)
        total_chars = sum(d.char_count for d in self.corpus_docs)

        corpus_data = {
            "version": "1.0.0",
            "type": "performance_test_corpus",
            "created": datetime.now().isoformat(),
            "seed": self.seed,
            "statistics": {
                "total_documents": len(self.corpus_docs),
                "total_words": total_words,
                "total_characters": total_chars,
                "avg_words_per_doc": total_words / len(self.corpus_docs) if self.corpus_docs else 0,
                "max_words_per_doc": self.MAX_WORDS_PER_DOC
            },
            "documents": [asdict(d) for d in self.corpus_docs],
            "test_scenarios": [
                {
                    "scenario_id": "stress_001",
                    "description": "High volume search (1000 queries)",
                    "query_count": 1000,
                    "concurrent_users": 10
                },
                {
                    "scenario_id": "load_001",
                    "description": "Sustained load (100 queries/sec for 60s)",
                    "duration_seconds": 60,
                    "queries_per_second": 100
                }
            ]
        }

        if dry_run:
            est_size_mb = len(json.dumps(corpus_data)) / (1024 * 1024)
            print(f"\n[DRY RUN] Would save to: {output_path}")
            print(f"[DRY RUN] Estimated size: {est_size_mb:.1f} MB")
            return

        print(f"\n💾 Saving to {output_path}...")
        output_path.parent.mkdir(parents=True, exist_ok=True)

        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(corpus_data, f, ensure_ascii=False, indent=2)

        size_mb = output_path.stat().st_size / (1024 * 1024)
        print(f"✅ Saved ({size_mb:.1f} MB)")


def main():
    import argparse

    parser = argparse.ArgumentParser(description='Create BM25 performance test corpus')
    parser.add_argument('--source-dir', type=str, default='KpiExxerpro/Fixture',
                       help='Source directory')
    parser.add_argument('--output', type=str,
                       default='code/src/tests/05SystemTests/ExxerAI.EnhancedRag.System.Tests/Fixtures/bm25_performance_corpus.json',
                       help='Output JSON')
    parser.add_argument('--count', type=int, default=250, help='Document count')
    parser.add_argument('--seed', type=int, default=42, help='Random seed')
    parser.add_argument('--dry-run', action='store_true', help='Simulate')

    args = parser.parse_args()

    source_dir = Path(args.source_dir)
    if not source_dir.exists():
        print(f"❌ Source not found: {source_dir}")
        return 1

    print("=" * 60)
    print("  BM25 Performance Corpus Builder (Overnight)")
    print("=" * 60)
    print(f"Source: {source_dir}")
    print(f"Output: {args.output}")
    print(f"Count: {args.count}")
    print(f"Seed: {args.seed}")
    if args.dry_run:
        print("Mode: DRY RUN")
    print()

    builder = PerformanceCorpusBuilder(source_dir, args.count, args.seed)
    builder.scan_documents()

    if not builder.all_docs:
        print("❌ No documents found")
        return 1

    selected = builder.select_sample()
    if not selected:
        print("❌ No selection made")
        return 1

    builder.build_corpus(selected)
    if not builder.corpus_docs:
        print("❌ No corpus created")
        return 1

    builder.save_corpus(Path(args.output), args.dry_run)

    print("\n" + "=" * 60)
    print("  ✅ Performance Corpus Ready!")
    print("=" * 60)
    print(f"📊 {len(builder.corpus_docs)} documents")
    print(f"🧪 For nightly/on-demand performance tests")

    return 0


if __name__ == "__main__":
    exit(main())
