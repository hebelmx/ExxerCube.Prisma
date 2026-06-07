#!/usr/bin/env python3
"""
Cleanup script for extracted entities using fuzzy matching.
Deduplicates, normalizes, and cleans entity names.
"""

import json
import re
import argparse
from typing import List, Dict, Any, Tuple
from rapidfuzz import fuzz, process
from collections import defaultdict


# --- Configuration ---
INPUT_FILE = "extracted_authorities.json"
OUTPUT_FILE = "extracted_authorities_clean.json"

# Fuzzy match threshold (0-100). Higher = stricter matching
SIMILARITY_THRESHOLD = 85

# Maximum entity name length (truncate garbage)
MAX_NAME_LENGTH = 120

# Minimum entity name length
MIN_NAME_LENGTH = 15


def clean_entity_name(name: str) -> str:
    """Clean and normalize an entity name."""
    if not name:
        return ""

    # Remove extra whitespace
    name = ' '.join(name.split())

    # Remove trailing garbage (addresses, numbers, etc.)
    # Stop at common address indicators
    stop_patterns = [
        r'\s+(?:Av\.|Calle|Carretera|Fracción|Plaza|s/n|No\.|Núm\.|Cerro|Adolfo|Independencia)',
        r'\s+\d{4,}',  # Zip codes or long numbers
        r'\s+[a-z]{2,}\s*$',  # Trailing lowercase words (likely addresses)
        r',\s*\d+\s*$',  # Trailing numbers after comma
        r'\s+JUZGADO\s+',  # Multiple entities concatenated
        r'\s+TRIBUNAL\s+',  # Multiple entities concatenated
    ]

    for pattern in stop_patterns:
        match = re.search(pattern, name, re.IGNORECASE)
        if match:
            name = name[:match.start()].strip()

    # Remove trailing punctuation and numbers
    name = re.sub(r'[\s,\.\-:;]+$', '', name)
    name = re.sub(r'\s+\d+$', '', name)

    # Normalize common variations
    name = re.sub(r'\s+', ' ', name)

    # Remove text in parentheses at the end if it's just abbreviations
    name = re.sub(r'\s*\([A-Z]{2,6}\)\s*$', '', name)

    # Truncate at reasonable boundary if still too long
    if len(name) > MAX_NAME_LENGTH:
        # Try to cut at a natural boundary
        for boundary in [' CON ', ' DEL ', ' DE LA ', ' EN EL ', ' EN ']:
            parts = name.split(boundary)
            if len(parts) > 1:
                candidate = boundary.join(parts[:2])
                if len(candidate) <= MAX_NAME_LENGTH:
                    name = candidate
                    break
        # Last resort: hard truncate
        if len(name) > MAX_NAME_LENGTH:
            name = name[:MAX_NAME_LENGTH].rsplit(' ', 1)[0]

    return name.strip()


def normalize_for_comparison(name: str) -> str:
    """Normalize name for fuzzy comparison."""
    name = name.upper()
    # Remove accents for comparison
    replacements = {
        'Á': 'A', 'É': 'E', 'Í': 'I', 'Ó': 'O', 'Ú': 'U',
        'Ñ': 'N', 'Ü': 'U'
    }
    for old, new in replacements.items():
        name = name.replace(old, new)
    # Remove punctuation
    name = re.sub(r'[^\w\s]', '', name)
    # Normalize whitespace
    name = ' '.join(name.split())
    return name


def select_best_name(names: List[str]) -> str:
    """Select the best/canonical name from a group of similar names."""
    if not names:
        return ""
    if len(names) == 1:
        return names[0]

    # Prefer:
    # 1. Proper case over ALL CAPS
    # 2. Longer names (more complete)
    # 3. Names without trailing garbage

    scored = []
    for name in names:
        score = 0
        # Penalize ALL CAPS
        if name.isupper():
            score -= 10
        # Prefer mixed case
        if any(c.islower() for c in name) and any(c.isupper() for c in name):
            score += 5
        # Prefer reasonable length
        if 20 <= len(name) <= 80:
            score += 10
        elif len(name) > 80:
            score -= 5
        # Penalize trailing numbers
        if re.search(r'\d+$', name):
            score -= 5
        # Bonus for completeness indicators
        if 'CON RESIDENCIA' in name.upper() or 'DEL DISTRITO' in name.upper():
            score += 3

        scored.append((score, len(name), name))

    # Sort by score desc, then length desc
    scored.sort(key=lambda x: (-x[0], -x[1]))
    return scored[0][2]


def fuzzy_deduplicate(entities: List[Dict[str, Any]], threshold: int = SIMILARITY_THRESHOLD) -> List[Dict[str, Any]]:
    """Deduplicate entities using fuzzy matching."""
    if not entities:
        return []

    # Group by tipo first (only compare within same type)
    by_type = defaultdict(list)
    for e in entities:
        by_type[e.get('tipo', 'Unknown')].append(e)

    unique_entities = []

    for tipo, type_entities in by_type.items():
        # Build list of normalized names for comparison
        names_normalized = [(normalize_for_comparison(e.get('nombre', '')), e) for e in type_entities]

        # Track which entities have been merged
        merged = set()

        for i, (norm_name_i, entity_i) in enumerate(names_normalized):
            if i in merged:
                continue

            # Find all similar entities
            similar_group = [entity_i]
            similar_names = [entity_i.get('nombre', '')]

            for j, (norm_name_j, entity_j) in enumerate(names_normalized[i+1:], start=i+1):
                if j in merged:
                    continue

                # Compare using token_sort_ratio (handles word order differences)
                similarity = fuzz.token_sort_ratio(norm_name_i, norm_name_j)

                if similarity >= threshold:
                    similar_group.append(entity_j)
                    similar_names.append(entity_j.get('nombre', ''))
                    merged.add(j)

            # Select best name from the group
            best_name = select_best_name(similar_names)

            # Merge jurisdictions (keep most specific)
            jurisdictions = [e.get('jurisdiccion') for e in similar_group if e.get('jurisdiccion')]
            best_jurisdiction = jurisdictions[0] if jurisdictions else None

            # Merge sources
            sources = list(set(e.get('source', 'unknown') for e in similar_group))

            unique_entities.append({
                'nombre': best_name,
                'tipo': tipo,
                'jurisdiccion': best_jurisdiction,
                'source': sources[0] if len(sources) == 1 else sources,
                'merged_count': len(similar_group)
            })

    return unique_entities


def filter_entities(entities: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
    """Filter out invalid or garbage entities."""
    filtered = []

    for e in entities:
        name = e.get('nombre', '')

        # Skip empty or too short
        if len(name) < MIN_NAME_LENGTH:
            continue

        # Skip if mostly numbers
        alpha_chars = sum(1 for c in name if c.isalpha())
        if alpha_chars < len(name) * 0.5:
            continue

        # Skip if it's just a header/title
        skip_patterns = [
            r'^NOTA\s+INFORMATIVA',
            r'^DISTRIBUCIÓN\s+DE',
            r'^FUENTE:',
            r'^\d+[°º]?\s*CIRCUITO\s*$',
        ]
        if any(re.match(p, name, re.IGNORECASE) for p in skip_patterns):
            continue

        filtered.append(e)

    return filtered


def process_file(input_path: str, output_path: str, threshold: int = SIMILARITY_THRESHOLD):
    """Process the extracted entities file."""
    print(f"\n{'='*60}")
    print("Entity Cleanup - Fuzzy Deduplication")
    print(f"{'='*60}")
    print(f"Input: {input_path}")
    print(f"Similarity threshold: {threshold}%")
    print(f"{'='*60}\n")

    # Load data
    with open(input_path, 'r', encoding='utf-8') as f:
        data = json.load(f)

    total_before = 0
    total_after = 0

    for doc in data:
        filename = doc.get('source_file', 'unknown')
        entities = doc.get('entidades', [])
        count_before = len(entities)
        total_before += count_before

        print(f">>> Processing: {filename}")
        print(f"    Before: {count_before} entities")

        # Step 1: Clean names
        for e in entities:
            e['nombre'] = clean_entity_name(e.get('nombre', ''))

        # Step 2: Filter garbage
        entities = filter_entities(entities)
        print(f"    After filtering: {len(entities)} entities")

        # Step 3: Fuzzy deduplicate
        entities = fuzzy_deduplicate(entities, threshold)
        count_after = len(entities)
        total_after += count_after

        print(f"    After dedup: {count_after} entities")
        print(f"    Reduction: {count_before - count_after} ({(count_before - count_after) / max(count_before, 1) * 100:.1f}%)")

        # Update document
        doc['entidades'] = entities
        doc['stats'] = {
            'original_count': count_before,
            'clean_count': count_after,
            'reduction_pct': round((count_before - count_after) / max(count_before, 1) * 100, 1)
        }

    # Save cleaned data
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)

    # Summary
    print(f"\n{'='*60}")
    print("SUMMARY")
    print(f"{'='*60}")
    print(f"Total before: {total_before}")
    print(f"Total after:  {total_after}")
    print(f"Reduction:    {total_before - total_after} ({(total_before - total_after) / max(total_before, 1) * 100:.1f}%)")
    print(f"\nOutput saved to: {output_path}")


def main():
    parser = argparse.ArgumentParser(description="Clean and deduplicate extracted entities using fuzzy matching.")
    parser.add_argument('--input', type=str, default=INPUT_FILE, help=f"Input JSON file (default: {INPUT_FILE})")
    parser.add_argument('--output', type=str, default=OUTPUT_FILE, help=f"Output JSON file (default: {OUTPUT_FILE})")
    parser.add_argument('--threshold', type=int, default=SIMILARITY_THRESHOLD,
                        help=f"Similarity threshold 0-100 (default: {SIMILARITY_THRESHOLD})")

    args = parser.parse_args()
    process_file(args.input, args.output, args.threshold)


if __name__ == "__main__":
    main()
