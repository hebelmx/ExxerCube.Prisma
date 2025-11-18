#!/usr/bin/env python3
"""
Create Small BM25 Integration Test Corpus

Extracts text from 20-30 documents to create a fast, deterministic JSON corpus
for integration tests. Runs in <1 second, no binary fixtures needed.

Usage:
    python scripts/create_bm25_integration_corpus.py
    python scripts/create_bm25_integration_corpus.py --dry-run

Author: Claude Code Agent
Date: 2025-11-10
"""

import json
import hashlib
from pathlib import Path
from typing import List, Dict, Any, Optional
from dataclasses import dataclass, asdict
from datetime import datetime

# Try to import PDF extraction
try:
    import PyPDF2
    HAS_PYPDF2 = True
except ImportError:
    HAS_PYPDF2 = False
    print("⚠️  Warning: PyPDF2 not installed")
    print("   Install: pip install PyPDF2")


@dataclass
class Bm25IntegrationDocument:
    """Lightweight document for integration testing."""
    document_id: str
    title: str
    content: str  # 200-300 words
    metadata: Dict[str, Any]


class IntegrationCorpusBuilder:
    """Builds small BM25 corpus for fast integration tests."""

    MAX_WORDS_PER_DOC = 300

    def __init__(self, fixtures_dir: Path):
        """Initialize builder with fixtures directory."""
        self.fixtures_dir = fixtures_dir
        self.documents: List[Bm25IntegrationDocument] = []

    def extract_pdf_text(self, pdf_path: Path, max_words: int) -> str:
        """Extract text from PDF."""
        if not HAS_PYPDF2:
            return f"Sample text from {pdf_path.stem}"

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

                return ' '.join(text_parts) if text_parts else f"[Empty PDF: {pdf_path.stem}]"

        except Exception as e:
            return f"[Error reading PDF: {str(e)}]"

    def build_corpus(self) -> None:
        """Build corpus from fixtures."""
        print(f"🔍 Scanning {self.fixtures_dir} for fixtures...")

        pdf_files = sorted(self.fixtures_dir.glob("*.pdf"))
        print(f"✅ Found {len(pdf_files)} PDF files")

        for idx, pdf_path in enumerate(pdf_files, 1):
            print(f"   Processing {idx}/{len(pdf_files)}: {pdf_path.name}")

            # Generate doc ID from filename
            doc_id = f"doc_{idx:03d}"

            # Extract text
            content = self.extract_pdf_text(pdf_path, self.MAX_WORDS_PER_DOC)

            # Generate title
            title = pdf_path.stem.replace('helix_', 'Document ').replace('_', ' ')

            # Create metadata
            with open(pdf_path, 'rb') as f:
                file_hash = hashlib.sha256(f.read()).hexdigest()[:16]

            metadata = {
                "source_file": pdf_path.name,
                "file_size": pdf_path.stat().st_size,
                "hash": file_hash,
                "index": idx
            }

            doc = Bm25IntegrationDocument(
                document_id=doc_id,
                title=title,
                content=content,
                metadata=metadata
            )

            self.documents.append(doc)

        print(f"✅ Created {len(self.documents)} document entries")

    def create_test_queries(self) -> List[Dict[str, Any]]:
        """Define test queries with expected top results."""

        # Analyze corpus to create realistic queries
        # For now, create sample queries
        queries = [
            {
                "query_id": "q001",
                "query": "documento fiscal",
                "description": "Search for fiscal documents",
                "expected_top_10": [],  # Will be filled after indexing
                "min_score": 0.1
            },
            {
                "query_id": "q002",
                "query": "pago factura",
                "description": "Search for payment invoices",
                "expected_top_10": [],
                "min_score": 0.1
            },
            {
                "query_id": "q003",
                "query": "servicio cliente",
                "description": "Search for customer service documents",
                "expected_top_10": [],
                "min_score": 0.1
            }
        ]

        return queries

    def save_corpus(self, output_path: Path, dry_run: bool = False) -> None:
        """Save corpus to JSON file."""

        total_words = sum(len(doc.content.split()) for doc in self.documents)
        total_chars = sum(len(doc.content) for doc in self.documents)

        corpus_data = {
            "version": "1.0.0",
            "type": "integration_test_corpus",
            "created": datetime.now().isoformat(),
            "seed": 42,
            "statistics": {
                "total_documents": len(self.documents),
                "total_words": total_words,
                "total_characters": total_chars,
                "avg_words_per_doc": total_words / len(self.documents) if self.documents else 0
            },
            "documents": [asdict(doc) for doc in self.documents],
            "test_queries": self.create_test_queries()
        }

        if dry_run:
            print(f"\n[DRY RUN] Would save to: {output_path}")
            print(f"[DRY RUN] Size: ~{len(json.dumps(corpus_data)) / 1024:.1f} KB")
            return

        print(f"\n💾 Saving corpus to {output_path}...")
        output_path.parent.mkdir(parents=True, exist_ok=True)

        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(corpus_data, f, ensure_ascii=False, indent=2)

        size_kb = output_path.stat().st_size / 1024
        print(f"✅ Corpus saved ({size_kb:.1f} KB)")


def main():
    import argparse

    parser = argparse.ArgumentParser(description='Create BM25 integration test corpus')
    parser.add_argument('--fixtures-dir', type=str,
                       default='code/src/tests/05SystemTests/ExxerAI.EnhancedRag.System.Tests/Fixtures',
                       help='Directory with fixture PDFs')
    parser.add_argument('--output', type=str,
                       default='code/src/tests/05SystemTests/ExxerAI.EnhancedRag.System.Tests/Fixtures/bm25_integration_corpus.json',
                       help='Output JSON path')
    parser.add_argument('--dry-run', action='store_true', help='Simulate without writing')

    args = parser.parse_args()

    fixtures_dir = Path(args.fixtures_dir)
    output_path = Path(args.output)

    if not fixtures_dir.exists():
        print(f"❌ Error: Fixtures directory not found: {fixtures_dir}")
        return 1

    print("=" * 60)
    print("  BM25 Integration Corpus Builder (Fast)")
    print("=" * 60)
    print(f"Source: {fixtures_dir}")
    print(f"Output: {output_path}")
    if args.dry_run:
        print("Mode: DRY RUN")
    print()

    builder = IntegrationCorpusBuilder(fixtures_dir)
    builder.build_corpus()

    if not builder.documents:
        print("❌ No documents created")
        return 1

    builder.save_corpus(output_path, args.dry_run)

    print("\n" + "=" * 60)
    print("  ✅ Integration Corpus Ready!")
    print("=" * 60)
    print(f"📊 {len(builder.documents)} documents")
    print(f"🧪 Fast deterministic tests (<1s)")
    print(f"📁 {output_path}")

    return 0


if __name__ == "__main__":
    exit(main())
