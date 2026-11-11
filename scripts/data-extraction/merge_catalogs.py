#!/usr/bin/env python3
"""
Merge all entity catalogs into a consolidated master catalog.
Also fixes type classification for entities marked as "Unknown".
"""

import json
import re
from pathlib import Path
from typing import Dict, List, Any
from rapidfuzz import fuzz
from collections import defaultdict

# Input files
INPUT_FILES = [
    "phase1_pjf_extracted.json",          # Phase 1: PJF Courts
    "extracted_authorities_clean.json",   # Phase 2: Fiscalías, FGR, UIF
    "sat_reglamento_extracted.json",      # SAT Reglamento
    "Prisma/Entidades Legales/Sources/SAT_Estructura_Manual.json",  # Manual SAT structure
]

# Previous extraction (Phase 1: PJF courts)
PHASE1_BACKUP = "Prisma/Entidades Legales"  # Original extraction location

OUTPUT_FILE = "consolidated_entity_catalog.json"

# Type classification rules
TYPE_PATTERNS = {
    "Fiscalía": [r"fiscal[ií]a", r"fiscal general"],
    "Procuraduría": [r"procuradur[ií]a"],
    "Juzgado": [r"juzgado"],
    "Tribunal": [r"tribunal"],
    "Secretaría": [r"secretar[ií]a"],
    "Dirección": [r"direcci[oó]n"],
    "Administración": [r"administraci[oó]n"],
    "Consejo": [r"consejo"],
    "Comisión": [r"comisi[oó]n"],
    "Delegación": [r"delegaci[oó]n"],
    "Subdelegación": [r"subdelegaci[oó]n"],
    "Agencia": [r"agencia"],
    "Unidad": [r"unidad"],
    "Sala": [r"sala\s"],
    "Pleno": [r"pleno\s"],
    "Centro": [r"centro\s"],
    "Coordinación": [r"coordinaci[oó]n"],
    "Subadministración": [r"subadministraci[oó]n"],
    "Instituto": [r"instituto"],
    "Órgano": [r"[oó]rgano"],
}


def classify_entity_type(nombre: str) -> str:
    """Classify entity type based on name patterns."""
    nombre_lower = nombre.lower()

    for entity_type, patterns in TYPE_PATTERNS.items():
        for pattern in patterns:
            if re.search(pattern, nombre_lower):
                return entity_type

    return "Otro"


def normalize_entity(entity: Dict) -> Dict:
    """Normalize and clean entity data."""
    nombre = entity.get("nombre", "").strip()

    # Fix type if Unknown or missing
    tipo = entity.get("tipo", "Unknown")
    if tipo in ["Unknown", "unknown", "", None]:
        tipo = classify_entity_type(nombre)

    # Clean nombre - remove trailing garbage
    nombre = re.sub(r'\s+', ' ', nombre)
    nombre = re.sub(r'\s*[,\.]+$', '', nombre)

    # Extract jurisdiction if present in name
    jurisdiccion = entity.get("jurisdiccion")
    if not jurisdiccion:
        if "federal" in nombre.lower():
            jurisdiccion = "Federal"
        elif "ciudad de méxico" in nombre.lower() or "cdmx" in nombre.lower():
            jurisdiccion = "Ciudad de México"

    return {
        "nombre": nombre,
        "tipo": tipo,
        "jurisdiccion": jurisdiccion,
        "nombre_corto": entity.get("nombre_corto"),
        "source": entity.get("source", "unknown"),
        "competencia": entity.get("competencia", []),
    }


def deduplicate_global(entities: List[Dict], threshold: int = 85) -> List[Dict]:
    """Global deduplication across all sources."""
    if not entities:
        return []

    # Group by type for faster comparison
    by_type = defaultdict(list)
    for e in entities:
        by_type[e.get("tipo", "Otro")].append(e)

    unique = []

    for tipo, type_entities in by_type.items():
        seen_names = set()

        for entity in type_entities:
            nombre = entity.get("nombre", "").upper()
            nombre_normalized = re.sub(r'[^\w\s]', '', nombre)
            nombre_normalized = ' '.join(nombre_normalized.split())

            # Check if similar exists
            is_duplicate = False
            for seen in seen_names:
                if fuzz.token_sort_ratio(nombre_normalized, seen) >= threshold:
                    is_duplicate = True
                    break

            if not is_duplicate and len(nombre_normalized) > 10:
                seen_names.add(nombre_normalized)
                unique.append(entity)

    return unique


def load_json_file(filepath: str) -> List[Dict]:
    """Load entities from a JSON file."""
    path = Path(filepath)
    if not path.exists():
        print(f"  [Warning] File not found: {filepath}")
        return []

    with open(path, 'r', encoding='utf-8') as f:
        data = json.load(f)

    # Handle different formats
    if isinstance(data, list):
        # List of documents
        entities = []
        for doc in data:
            if isinstance(doc, dict):
                entities.extend(doc.get("entidades", []))
        return entities
    elif isinstance(data, dict):
        return data.get("entidades", [])

    return []


def main():
    print("=" * 60)
    print("Entity Catalog Consolidation")
    print("=" * 60)

    all_entities = []

    # Load from all input files
    for filepath in INPUT_FILES:
        print(f"\nLoading: {filepath}")
        entities = load_json_file(filepath)
        print(f"  Found {len(entities)} entities")

        # Add source tracking
        for e in entities:
            if "source" not in e:
                e["source"] = filepath

        all_entities.extend(entities)

    print(f"\n>>> Total raw entities: {len(all_entities)}")

    # Normalize all entities
    print("\nNormalizing entities...")
    normalized = [normalize_entity(e) for e in all_entities]

    # Count type fixes
    unknown_fixed = sum(1 for e in normalized if e["tipo"] != "Unknown")
    print(f"  Classified {unknown_fixed} entity types")

    # Global deduplication
    print("\nDeduplicating globally...")
    unique = deduplicate_global(normalized, threshold=85)
    print(f"  Unique entities: {len(unique)}")

    # Group by type for summary
    by_type = defaultdict(list)
    for e in unique:
        by_type[e["tipo"]].append(e)

    # Build output catalog
    catalog = {
        "catalog_name": "autoridades_mexicanas_consolidado",
        "version": "2025-11-30",
        "description": "Consolidated catalog of Mexican authorities for Prisma system",
        "sources": INPUT_FILES,
        "stats": {
            "total_entities": len(unique),
            "by_type": {k: len(v) for k, v in sorted(by_type.items(), key=lambda x: -len(x[1]))}
        },
        "entities": unique
    }

    # Save
    with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
        json.dump(catalog, f, indent=2, ensure_ascii=False)

    print(f"\n{'=' * 60}")
    print("SUMMARY")
    print("=" * 60)
    print(f"Total unique entities: {len(unique)}")
    print(f"\nBy type:")
    for tipo, entities in sorted(by_type.items(), key=lambda x: -len(x[1])):
        print(f"  {tipo}: {len(entities)}")
    print(f"\nOutput saved to: {OUTPUT_FILE}")


if __name__ == "__main__":
    main()
