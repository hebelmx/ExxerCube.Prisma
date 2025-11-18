#!/usr/bin/env python3
"""
Select 100 random PDF documents from KpiExxerpro/Fixture using sigmoid-based distribution.
Ensures realistic sample distribution from small to large PDF files.

Usage:
    python select_pdf_fixtures.py
"""

import os
import sys
import shutil
import random
import json
from pathlib import Path
from typing import List, Dict, Tuple
import math

def sigmoid(x: float) -> float:
    """Sigmoid function: 1 / (1 + e^-x)"""
    return 1 / (1 + math.exp(-x))

def inverse_sigmoid_random(min_val: float, max_val: float) -> float:
    """
    Generate random value using inverse sigmoid distribution.
    This creates a distribution that favors mid-range values while still
    allowing both small and large values.

    Args:
        min_val: Minimum value
        max_val: Maximum value

    Returns:
        Random value in range [min_val, max_val] with sigmoid distribution
    """
    # Generate random value in [-6, 6] range (sigmoid sensitive range)
    # Then apply sigmoid to get [0, 1] with nice distribution
    x = random.uniform(-6, 6)
    sigmoid_val = sigmoid(x)

    # Map [0, 1] to [min_val, max_val]
    return min_val + (sigmoid_val * (max_val - min_val))

def find_pdf_documents(root_path: str) -> List[Tuple[str, int]]:
    """
    Find all PDF documents (.pdf) recursively.

    Args:
        root_path: Root directory to search

    Returns:
        List of (file_path, file_size) tuples
    """
    documents = []

    print(f"🔍 Scanning {root_path} for PDF documents (.pdf)...")

    for root, dirs, files in os.walk(root_path):
        for file in files:
            file_path = os.path.join(root, file)
            ext = os.path.splitext(file)[1].lower()

            if ext == '.pdf':
                try:
                    file_size = os.path.getsize(file_path)
                    # Filter files between 1 byte and 100MB
                    if 1 <= file_size <= 100 * 1024 * 1024:
                        documents.append((file_path, file_size))
                except OSError as e:
                    print(f"⚠️  Warning: Could not access {file_path}: {e}")

    return documents

def categorize_by_size(documents: List[Tuple[str, int]]) -> Dict[str, List[Tuple[str, int]]]:
    """
    Categorize documents by size ranges for better distribution.

    Args:
        documents: List of (file_path, file_size) tuples

    Returns:
        Dictionary of size category -> list of documents
    """
    categories = {
        'tiny': [],      # < 10 KB
        'small': [],     # 10 KB - 100 KB
        'medium': [],    # 100 KB - 1 MB
        'large': [],     # 1 MB - 10 MB
        'xlarge': []     # > 10 MB
    }

    for doc_path, doc_size in documents:
        if doc_size < 10 * 1024:
            categories['tiny'].append((doc_path, doc_size))
        elif doc_size < 100 * 1024:
            categories['small'].append((doc_path, doc_size))
        elif doc_size < 1 * 1024 * 1024:
            categories['medium'].append((doc_path, doc_size))
        elif doc_size < 10 * 1024 * 1024:
            categories['large'].append((doc_path, doc_size))
        else:
            categories['xlarge'].append((doc_path, doc_size))

    return categories

def select_documents_with_distribution(
    documents: List[Tuple[str, int]],
    target_count: int = 100
) -> List[Tuple[str, int, str]]:
    """
    Select target_count documents using sigmoid-based random distribution.

    Args:
        documents: List of (file_path, file_size) tuples
        target_count: Number of documents to select (default: 100)

    Returns:
        List of (file_path, file_size, category) tuples
    """
    if len(documents) < target_count:
        print(f"⚠️  Warning: Only {len(documents)} documents available, less than target {target_count}")
        target_count = len(documents)

    # Categorize documents
    categories = categorize_by_size(documents)

    # Calculate distribution using sigmoid-weighted random selection
    # We want a nice bell curve favoring medium-sized files
    selected = []

    # Distribution targets (percentages)
    distribution = {
        'tiny': 0.10,    # 10%
        'small': 0.25,   # 25%
        'medium': 0.35,  # 35%
        'large': 0.20,   # 20%
        'xlarge': 0.10   # 10%
    }

    for category, percentage in distribution.items():
        available = categories[category]
        if not available:
            continue

        # Calculate how many to select from this category
        count = min(int(target_count * percentage), len(available))

        # Randomly select from this category
        selected_from_category = random.sample(available, count)

        # Add category label
        for doc_path, doc_size in selected_from_category:
            selected.append((doc_path, doc_size, category))

    # If we haven't reached target count, randomly fill from remaining
    if len(selected) < target_count:
        remaining_needed = target_count - len(selected)
        all_remaining = [doc for doc in documents if doc not in [s[:2] for s in selected]]

        if all_remaining:
            additional = random.sample(
                all_remaining,
                min(remaining_needed, len(all_remaining))
            )
            for doc_path, doc_size in additional:
                # Determine category
                for cat, docs in categories.items():
                    if (doc_path, doc_size) in docs:
                        selected.append((doc_path, doc_size, cat))
                        break

    return selected

def copy_selected_documents(
    selected: List[Tuple[str, int, str]],
    output_dir: str
) -> Dict:
    """
    Copy selected documents to output directory and generate metadata.

    Args:
        selected: List of (file_path, file_size, category) tuples
        output_dir: Destination directory

    Returns:
        Metadata dictionary
    """
    # Create output directory
    os.makedirs(output_dir, exist_ok=True)

    # Clear existing files
    for existing_file in os.listdir(output_dir):
        file_path = os.path.join(output_dir, existing_file)
        if os.path.isfile(file_path) and existing_file.endswith('.pdf'):
            os.remove(file_path)

    metadata = {
        'total_documents': len(selected),
        'documents': [],
        'statistics': {
            'tiny': 0,
            'small': 0,
            'medium': 0,
            'large': 0,
            'xlarge': 0,
            'total_bytes': 0,
            'min_size': float('inf'),
            'max_size': 0,
            'extensions': {}
        }
    }

    print(f"\n📋 Copying {len(selected)} documents to {output_dir}...")

    for idx, (src_path, file_size, category) in enumerate(selected, 1):
        # Generate unique filename
        ext = os.path.splitext(src_path)[1]
        dest_filename = f"test_{idx:03d}_{category}_{file_size}{ext}"
        dest_path = os.path.join(output_dir, dest_filename)

        # Copy file
        try:
            shutil.copy2(src_path, dest_path)

            # Update metadata
            doc_info = {
                'index': idx,
                'filename': dest_filename,
                'original_path': src_path,
                'size_bytes': file_size,
                'size_category': category,
                'extension': ext
            }
            metadata['documents'].append(doc_info)

            # Update statistics
            metadata['statistics'][category] += 1
            metadata['statistics']['total_bytes'] += file_size
            metadata['statistics']['min_size'] = min(metadata['statistics']['min_size'], file_size)
            metadata['statistics']['max_size'] = max(metadata['statistics']['max_size'], file_size)

            ext_count = metadata['statistics']['extensions'].get(ext, 0)
            metadata['statistics']['extensions'][ext] = ext_count + 1

            print(f"  ✅ {idx:3d}. {dest_filename} ({file_size:,} bytes, {category})")

        except Exception as e:
            print(f"  ❌ {idx:3d}. Failed to copy {src_path}: {e}")

    # Calculate average
    if metadata['documents']:
        metadata['statistics']['avg_size'] = metadata['statistics']['total_bytes'] / len(metadata['documents'])

    return metadata

def main():
    """Main execution function."""
    print("=" * 80)
    print("📚 PDF Test Fixture Generator")
    print("=" * 80)

    # Paths
    script_dir = Path(__file__).parent
    test_project_root = script_dir.parent
    fixtures_source = Path("F:/Dynamic/ExxerAi/ExxerAI/KpiExxerpro/Fixture")
    fixtures_dest = test_project_root / "Fixtures"
    metadata_file = fixtures_dest / "fixtures_metadata.json"

    # Validate source exists
    if not fixtures_source.exists():
        print(f"❌ Source directory not found: {fixtures_source}")
        sys.exit(1)

    # Find all PDF documents
    all_documents = find_pdf_documents(str(fixtures_source))

    if not all_documents:
        print(f"❌ No PDF documents found in {fixtures_source}")
        sys.exit(1)

    print(f"\n📊 Found {len(all_documents)} PDF documents")

    # Categorize and display distribution
    categories = categorize_by_size(all_documents)
    print(f"\n📈 Size Distribution:")
    for category, docs in categories.items():
        if docs:
            sizes = [size for _, size in docs]
            print(f"  {category.upper():8s}: {len(docs):4d} files "
                  f"(min: {min(sizes):>10,} bytes, max: {max(sizes):>10,} bytes)")

    # Select documents with distribution
    selected_docs = select_documents_with_distribution(all_documents, target_count=100)

    print(f"\n🎯 Selected {len(selected_docs)} documents using sigmoid distribution")

    # Display selection distribution
    selection_dist = {}
    for _, _, category in selected_docs:
        selection_dist[category] = selection_dist.get(category, 0) + 1

    print(f"\n📊 Selection Distribution:")
    for category, count in sorted(selection_dist.items()):
        percentage = (count / len(selected_docs)) * 100
        print(f"  {category.upper():8s}: {count:3d} files ({percentage:5.1f}%)")

    # Copy documents and generate metadata
    metadata = copy_selected_documents(selected_docs, str(fixtures_dest))

    # Save metadata
    with open(metadata_file, 'w', encoding='utf-8') as f:
        json.dump(metadata, f, indent=2)

    print(f"\n💾 Metadata saved to {metadata_file}")

    # Display final statistics
    stats = metadata['statistics']
    print(f"\n📊 Final Statistics:")
    print(f"  Total Documents: {metadata['total_documents']}")
    print(f"  Total Size: {stats['total_bytes']:,} bytes ({stats['total_bytes'] / 1024 / 1024:.2f} MB)")
    print(f"  Average Size: {stats['avg_size']:,.0f} bytes ({stats['avg_size'] / 1024:.2f} KB)")
    print(f"  Min Size: {stats['min_size']:,} bytes")
    print(f"  Max Size: {stats['max_size']:,} bytes ({stats['max_size'] / 1024 / 1024:.2f} MB)")

    print(f"\n📄 Extensions:")
    for ext, count in sorted(stats['extensions'].items()):
        percentage = (count / metadata['total_documents']) * 100
        print(f"  {ext}: {count:3d} files ({percentage:5.1f}%)")

    print(f"\n✅ Successfully created test fixtures!")
    print(f"   Location: {fixtures_dest}")
    print(f"   Ready for integration testing with PdfPig!")
    print("=" * 80)

if __name__ == '__main__':
    main()
