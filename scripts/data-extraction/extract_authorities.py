#!/usr/bin/env python3
"""
Entity Extraction Pipeline - Idempotent & Incremental

Extracts Mexican legal entities from PDF documents using:
- Regex pattern matching
- Multi-model LLM extraction (Ollama)
- Polynomial-enhanced OCR for scanned documents

Features:
- Idempotent: Safe to run multiple times
- Incremental: Only processes new or changed files
- Change detection via SHA256 + filesize + content signature
"""

import os
import sys
import json
import re
import argparse
import hashlib
from datetime import datetime
from typing import List, Dict, Any, Set, Optional
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path
import PyPDF2
import requests
import pytesseract
from pdf2image import convert_from_path
from PIL import Image, ImageFilter, ImageEnhance
import cv2
import numpy as np

# Add scripts path for production_filter_inference
SCRIPTS_PATH = os.path.join(os.path.dirname(__file__), "Prisma", "scripts")
sys.path.insert(0, SCRIPTS_PATH)

# Polynomial model path
POLYNOMIAL_MODEL_PATH = os.path.join(os.path.dirname(__file__), "Prisma", "Fixtures", "polynomial_model_v2.json")

# --- Configuration ---
# Directory where your PDF files are located.
PDF_DIRECTORY = "/home/abel/projects/Prisma/ExxerCube.Prisma/Prisma/Entidades Legales/Sources"

# Output file where the final structured JSON data will be saved.
OUTPUT_FILE = "extracted_authorities.json"

# Processing state file - tracks processed files and their signatures
STATE_FILE = "extraction_state.json"

# Ollama API endpoint.
OLLAMA_ENDPOINT = "http://localhost:11434/api/generate"


# =============================================================================
# IDEMPOTENT STATE MANAGEMENT
# =============================================================================

def calculate_file_signature(filepath: str) -> Dict[str, Any]:
    """
    Calculate a unique signature for a file based on:
    - SHA256 hash of full content
    - File size in bytes
    - First 1KB content hash (quick change detection)
    - Last modification time

    Returns a signature dict that can be compared for changes.
    """
    path = Path(filepath)
    if not path.exists():
        return None

    stat = path.stat()
    file_size = stat.st_size
    mtime = stat.st_mtime

    # Calculate SHA256 of full file
    sha256_hash = hashlib.sha256()
    head_hash = hashlib.md5()

    with open(filepath, 'rb') as f:
        # Read first 1KB for quick signature
        head_data = f.read(1024)
        head_hash.update(head_data)
        sha256_hash.update(head_data)

        # Continue reading for full hash
        while chunk := f.read(8192):
            sha256_hash.update(chunk)

    return {
        "sha256": sha256_hash.hexdigest(),
        "head_md5": head_hash.hexdigest(),
        "size_bytes": file_size,
        "mtime": mtime,
        "computed_at": datetime.now().isoformat()
    }


def load_processing_state(state_file: str = STATE_FILE) -> Dict[str, Any]:
    """Load the processing state from disk."""
    if not os.path.exists(state_file):
        return {
            "version": "1.0",
            "created_at": datetime.now().isoformat(),
            "last_run": None,
            "processed_files": {},
            "total_entities_extracted": 0
        }

    with open(state_file, 'r', encoding='utf-8') as f:
        return json.load(f)


def save_processing_state(state: Dict[str, Any], state_file: str = STATE_FILE):
    """Save the processing state to disk."""
    state["last_run"] = datetime.now().isoformat()
    with open(state_file, 'w', encoding='utf-8') as f:
        json.dump(state, f, indent=2, ensure_ascii=False)


def file_needs_processing(filepath: str, state: Dict[str, Any]) -> tuple[bool, str]:
    """
    Check if a file needs to be processed based on its signature.

    Returns:
        (needs_processing: bool, reason: str)
    """
    filename = os.path.basename(filepath)
    current_sig = calculate_file_signature(filepath)

    if current_sig is None:
        return False, "file_not_found"

    processed = state.get("processed_files", {})

    if filename not in processed:
        return True, "new_file"

    prev_sig = processed[filename].get("signature", {})

    # Check if file has changed
    if prev_sig.get("sha256") != current_sig["sha256"]:
        return True, "content_changed"

    if prev_sig.get("size_bytes") != current_sig["size_bytes"]:
        return True, "size_changed"

    # File unchanged
    return False, "unchanged"


def mark_file_processed(
    state: Dict[str, Any],
    filepath: str,
    entities_count: int,
    extraction_result: Dict[str, Any]
) -> None:
    """Mark a file as successfully processed in the state."""
    filename = os.path.basename(filepath)
    signature = calculate_file_signature(filepath)

    state["processed_files"][filename] = {
        "filepath": filepath,
        "signature": signature,
        "processed_at": datetime.now().isoformat(),
        "entities_extracted": entities_count,
        "extraction_stats": extraction_result.get("stats", {})
    }


def get_incremental_summary(state: Dict[str, Any]) -> str:
    """Get a summary of the current processing state."""
    processed = state.get("processed_files", {})
    total_files = len(processed)
    total_entities = sum(p.get("entities_extracted", 0) for p in processed.values())
    last_run = state.get("last_run", "Never")

    return f"""
Processing State Summary:
  - Total files processed: {total_files}
  - Total entities extracted: {total_entities}
  - Last run: {last_run}
"""

# --- Regex Patterns for Entity Extraction ---
ENTITY_PATTERNS = [
    # Juzgados
    (r'JUZGADO\s+(?:DE\s+)?(?:CONTROL|CIVIL|PENAL|FAMILIAR|LABORAL|MERCANTIL|ADMINISTRATIVO)[\w\s,\-áéíóúñÁÉÍÓÚÑ]+(?:DE\s+[\w\s]+)?', 'Juzgado'),
    (r'JUZGADO\s+(?:PRIMERO|SEGUNDO|TERCERO|CUARTO|QUINTO|SEXTO|SÉPTIMO|OCTAVO|NOVENO|DÉCIMO|\d+[°º]?)\s+[\w\s,\-áéíóúñÁÉÍÓÚÑ]+', 'Juzgado'),
    # Tribunales
    (r'TRIBUNAL\s+(?:COLEGIADO|UNITARIO|ELECTORAL|SUPERIOR|FEDERAL|LABORAL)[\w\s,\-áéíóúñÁÉÍÓÚÑ]+(?:CIRCUITO|REGIÓN)?[\w\s]*', 'Tribunal'),
    (r'TRIBUNAL\s+DE\s+(?:ENJUICIAMIENTO|JUSTICIA|ALZADA)[\w\s,\-áéíóúñÁÉÍÓÚÑ]*', 'Tribunal'),
    # Secretarías
    (r'SECRETARÍA\s+(?:DE\s+)?[\w\s,\-áéíóúñÁÉÍÓÚÑ]+(?:FEDERAL|ESTATAL|GENERAL)?', 'Secretaría'),
    # Consejos
    (r'CONSEJO\s+(?:DE\s+)?(?:LA\s+)?(?:JUDICATURA|FEDERAL|ESTATAL)[\w\s,\-áéíóúñÁÉÍÓÚÑ]*', 'Consejo'),
    # Direcciones
    (r'DIRECCIÓN\s+GENERAL\s+(?:DE\s+)?[\w\s,\-áéíóúñÁÉÍÓÚÑ]+', 'Dirección'),
    # Comisiones
    (r'COMISIÓN\s+(?:NACIONAL|FEDERAL|ESTATAL)?[\w\s,\-áéíóúñÁÉÍÓÚÑ]+', 'Comisión'),
    # Fiscalías
    (r'FISCALÍA\s+(?:GENERAL|ESPECIALIZADA)?[\w\s,\-áéíóúñÁÉÍÓÚÑ]+', 'Fiscalía'),
    # Procuradurías
    (r'PROCURADURÍA\s+(?:GENERAL|FEDERAL)?[\w\s,\-áéíóúñÁÉÍÓÚÑ]+', 'Procuraduría'),
    # Suprema Corte
    (r'SUPREMA\s+CORTE\s+DE\s+JUSTICIA[\w\s,\-áéíóúñÁÉÍÓÚÑ]*', 'Tribunal'),
    # Salas
    (r'SALA\s+(?:SUPERIOR|REGIONAL|PENAL|CIVIL|FAMILIAR)[\w\s,\-áéíóúñÁÉÍÓÚÑ]*', 'Sala'),
    # Plenos
    (r'PLENO\s+(?:DEL?\s+)?(?:CIRCUITO|CONSEJO)?[\w\s,\-áéíóúñÁÉÍÓÚÑ]*', 'Pleno'),
    # Centros de justicia
    (r'CENTRO\s+(?:DE\s+)?(?:JUSTICIA|NACIONAL)[\w\s,\-áéíóúñÁÉÍÓÚÑ]+', 'Centro'),
]


def extract_entities_with_regex(text: str) -> List[Dict[str, Any]]:
    """Extract entities using regex patterns."""
    entities = []
    seen: Set[str] = set()

    for pattern, tipo in ENTITY_PATTERNS:
        matches = re.findall(pattern, text, re.IGNORECASE | re.MULTILINE)
        for match in matches:
            # Clean up the match
            name = ' '.join(match.split())  # Normalize whitespace
            name = name.strip('., ')

            # Skip if too short or already seen
            if len(name) < 10 or name.upper() in seen:
                continue

            seen.add(name.upper())
            entities.append({
                "nombre": name,
                "tipo": tipo,
                "jurisdiccion": extract_jurisdiction(name),
                "source": "regex"
            })

    return entities


def extract_jurisdiction(name: str) -> str:
    """Extract jurisdiction from entity name."""
    name_upper = name.upper()

    # Federal
    if 'FEDERAL' in name_upper or 'CIRCUITO' in name_upper:
        return 'Federal'

    # States
    states = [
        'CIUDAD DE MÉXICO', 'ESTADO DE MÉXICO', 'JALISCO', 'NUEVO LEÓN',
        'PUEBLA', 'VERACRUZ', 'CHIHUAHUA', 'SONORA', 'COAHUILA', 'TABASCO',
        'MICHOACÁN', 'OAXACA', 'YUCATÁN', 'GUERRERO', 'CHIAPAS', 'QUERÉTARO',
        'MORELOS', 'TLAXCALA', 'HIDALGO', 'AGUASCALIENTES', 'ZACATECAS',
        'NAYARIT', 'DURANGO', 'COLIMA', 'CAMPECHE', 'QUINTANA ROO',
        'BAJA CALIFORNIA', 'TAMAULIPAS', 'GUANAJUATO', 'SAN LUIS POTOSÍ',
        'SINALOA', 'CHALCO', 'TOLUCA', 'TEXCOCO', 'ECATEPEC', 'NEZAHUALCÓYOTL'
    ]

    for state in states:
        if state in name_upper:
            return state.title()

    return None


# --- Polynomial OCR Enhancement ---

def load_polynomial_model():
    """Load the polynomial filter model."""
    if not os.path.exists(POLYNOMIAL_MODEL_PATH):
        print(f"[Warning] Polynomial model not found at {POLYNOMIAL_MODEL_PATH}")
        return None
    with open(POLYNOMIAL_MODEL_PATH) as f:
        return json.load(f)


def extract_image_properties(image: Image.Image) -> Dict[str, float]:
    """Extract properties used for polynomial filter prediction."""
    img_array = np.array(image)

    # Convert to grayscale if needed
    if len(img_array.shape) == 3:
        gray = cv2.cvtColor(img_array, cv2.COLOR_RGB2GRAY)
    else:
        gray = img_array

    # Blur score (Laplacian variance - higher = sharper)
    laplacian = cv2.Laplacian(gray, cv2.CV_64F)
    blur_score = laplacian.var()

    # Contrast (std dev of intensity)
    contrast = float(gray.std())

    # Noise estimate (high-frequency energy via Laplacian)
    noise_estimate = float(np.abs(laplacian).mean())

    # Edge density (Canny edge pixel ratio)
    edges = cv2.Canny(gray, 100, 200)
    edge_density = float(np.sum(edges > 0) / edges.size)

    return {
        "blur_score": blur_score,
        "contrast": contrast,
        "noise_estimate": noise_estimate,
        "edge_density": edge_density,
    }


def predict_filter_params(properties: Dict[str, float], model: Dict) -> Dict:
    """Predict filter parameters using polynomial model."""
    from sklearn.preprocessing import PolynomialFeatures

    FEATURE_NAMES = ["blur_score", "contrast", "noise_estimate", "edge_density"]
    FILTER_PARAMS = ["contrast", "brightness", "sharpness", "unsharp_radius", "unsharp_percent"]

    features = np.array([[properties[f] for f in FEATURE_NAMES]])

    params = {}
    for param_name in FILTER_PARAMS:
        model_data = model["individual_models"][param_name]

        # Normalize features
        mean = np.array(model_data["scaler_mean"])
        scale = np.array(model_data["scaler_scale"])
        features_scaled = (features - mean) / scale

        # Create polynomial features
        poly = PolynomialFeatures(degree=model_data["poly_degree"], include_bias=True)
        features_poly = poly.fit_transform(features_scaled)

        # Predict
        coef = np.array(model_data["coefficients"])
        intercept = model_data["intercept"]
        pred = float((np.dot(features_poly, coef) + intercept)[0])

        # Clamp to reasonable bounds
        if param_name == "contrast":
            pred = max(0.5, min(2.0, pred))
        elif param_name == "brightness":
            pred = max(0.8, min(1.3, pred))
        elif param_name == "sharpness":
            pred = max(0.5, min(3.0, pred))
        elif param_name == "unsharp_radius":
            pred = max(0.0, min(5.0, pred))
        elif param_name == "unsharp_percent":
            pred = max(0, min(250, int(pred)))

        params[param_name] = pred

    return params


def enhance_image_for_ocr(image: Image.Image, model: Dict = None) -> Image.Image:
    """Apply polynomial-predicted filter enhancement for better OCR."""
    if model is None:
        model = load_polynomial_model()
        if model is None:
            return image  # No enhancement

    try:
        # Extract image properties
        properties = extract_image_properties(image)

        # Predict optimal filter parameters
        params = predict_filter_params(properties, model)

        # Apply filters
        img = image.copy()
        if img.mode != 'RGB':
            img = img.convert('RGB')

        # Apply brightness
        if params.get("brightness", 1.0) != 1.0:
            img = ImageEnhance.Brightness(img).enhance(params["brightness"])

        # Apply contrast
        if params.get("contrast", 1.0) != 1.0:
            img = ImageEnhance.Contrast(img).enhance(params["contrast"])

        # Apply sharpness
        if params.get("sharpness", 1.0) != 1.0:
            img = ImageEnhance.Sharpness(img).enhance(params["sharpness"])

        # Apply unsharp mask
        unsharp_radius = params.get("unsharp_radius", 0)
        unsharp_percent = params.get("unsharp_percent", 0)
        if unsharp_radius > 0.5 and unsharp_percent > 50:
            img = img.filter(ImageFilter.UnsharpMask(
                radius=unsharp_radius,
                percent=int(unsharp_percent),
                threshold=2
            ))

        return img
    except Exception as e:
        print(f"[Warning] Enhancement failed: {e}")
        return image


# --- Core Functions ---

def extract_text_from_pdf(pdf_path: str, use_ocr: bool = False) -> str:
    """
    Extracts all text from a given PDF file.
    Falls back to OCR (Tesseract) if text extraction yields no content.

    Args:
        pdf_path: Path to the PDF file
        use_ocr: If True, always use OCR instead of text extraction
    """
    text = ""
    filename = os.path.basename(pdf_path)

    # Try PyPDF2 text extraction first (unless OCR is forced)
    if not use_ocr:
        try:
            with open(pdf_path, "rb") as file:
                pdf_reader = PyPDF2.PdfReader(file)
                for page in pdf_reader.pages:
                    text += page.extract_text() or ""
            if text.strip():
                print(f"[PyPDF2] Extracted text from: {filename}")
                return text
        except Exception as e:
            print(f"[PyPDF2] Error reading {filename}: {e}")

    # Fallback to OCR with Tesseract + Polynomial Enhancement
    print(f"[Tesseract+Poly] Running enhanced OCR on: {filename}")
    try:
        # Load polynomial model once
        poly_model = load_polynomial_model()
        if poly_model:
            print(f"  [Polynomial] Model loaded - applying 18.4% OCR improvement filters")

        images = convert_from_path(pdf_path, dpi=300)
        ocr_texts = []
        for i, image in enumerate(images):
            # Apply polynomial enhancement before OCR
            if poly_model:
                enhanced_image = enhance_image_for_ocr(image, poly_model)
            else:
                enhanced_image = image

            page_text = pytesseract.image_to_string(enhanced_image, lang='spa+eng')
            ocr_texts.append(page_text)
            print(f"  - Page {i+1}/{len(images)} processed")
        text = "\n".join(ocr_texts)
        print(f"[Tesseract+Poly] Successfully extracted {len(text)} chars from {filename}")
    except Exception as e:
        print(f"[Tesseract+Poly] OCR failed for {filename}: {e}")

    return text

def extract_structured_data_with_ollama(text_content: str, model: str) -> Dict[str, Any]:
    """
    Calls a local Ollama container to extract structured data from text.
    Chunks long text to avoid timeout issues.

    Args:
        text_content: The raw text extracted from a PDF.
        model: The name of the Ollama model to use.

    Returns:
        A dictionary with the structured data, or an error dictionary.
    """
    # Chunk long text into smaller pieces for better LLM processing
    CHUNK_SIZE = 3000
    MAX_CHUNKS = 4
    all_entities = []

    chunks = [text_content[i:i+CHUNK_SIZE] for i in range(0, min(len(text_content), CHUNK_SIZE * MAX_CHUNKS), CHUNK_SIZE)]

    for chunk_idx, chunk in enumerate(chunks):
        prompt = f"""Extract Mexican legal entities from this Spanish text. Return JSON only.

Types: Juzgado, Tribunal, Secretaría, Dirección, Consejo, Comisión, Fiscalía, Sala, Pleno, Centro

Format: {{"entidades": [{{"nombre": "NAME", "tipo": "TYPE"}}]}}

Text:
{chunk}
"""
        try:
            response = requests.post(
                OLLAMA_ENDPOINT,
                json={
                    "model": model,
                    "prompt": prompt,
                    "format": "json",
                    "stream": False
                },
                timeout=60
            )
            response.raise_for_status()

            response_json_str = response.json().get("response", "{}")
            chunk_data = json.loads(response_json_str)
            chunk_entities = chunk_data.get("entidades", [])
            all_entities.extend(chunk_entities)

        except Exception as e:
            print(f"    [Chunk {chunk_idx+1}/{len(chunks)}] Error: {e}")
            continue

    print(f"Successfully extracted {len(all_entities)} entities from {len(chunks)} chunks using Ollama.")
    return {"entidades": all_entities}

def save_to_json(data: List[Dict[str, Any]], output_path: str):
    """
    Saves the provided data to a JSON file.
    """
    try:
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=4, ensure_ascii=False)
        print(f"\nSuccessfully saved data for {len(data)} files to {output_path}")
    except Exception as e:
        print(f"An error occurred while saving the JSON file: {e}")


def deduplicate_entities(entities: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
    """Remove duplicate entities based on normalized name."""
    seen: Set[str] = set()
    unique = []

    for entity in entities:
        name = entity.get("nombre", "").strip().upper()
        # Normalize: remove extra spaces, common variations
        name_normalized = ' '.join(name.split())

        if name_normalized and name_normalized not in seen and len(name_normalized) > 8:
            seen.add(name_normalized)
            unique.append(entity)

    return unique


def process_file_with_model(text_content: str, model: str, filename: str) -> Dict[str, Any]:
    """Process a single file with a specific model."""
    print(f"  [{model}] Processing {filename}...")
    result = extract_structured_data_with_ollama(text_content, model)
    entities = result.get("entidades", [])
    for e in entities:
        e["source"] = f"llm:{model}"
    return {"model": model, "entities": entities}


def extract_all_entities(text_content: str, filename: str, models: List[str], use_regex: bool = True) -> Dict[str, Any]:
    """
    Extract entities using multiple methods in parallel:
    - Multiple LLM models
    - Regex patterns
    Then combine and deduplicate results.
    """
    all_entities = []

    # 1. Regex extraction (fast, runs first)
    if use_regex:
        print(f"  [regex] Extracting from {filename}...")
        regex_entities = extract_entities_with_regex(text_content)
        print(f"  [regex] Found {len(regex_entities)} entities")
        all_entities.extend(regex_entities)

    # 2. Multi-model LLM extraction in parallel
    if models:
        with ThreadPoolExecutor(max_workers=len(models)) as executor:
            futures = {
                executor.submit(process_file_with_model, text_content, model, filename): model
                for model in models
            }

            for future in as_completed(futures):
                model = futures[future]
                try:
                    result = future.result()
                    entities = result.get("entities", [])
                    print(f"  [{model}] Found {len(entities)} entities")
                    all_entities.extend(entities)
                except Exception as e:
                    print(f"  [{model}] Error: {e}")

    # 3. Deduplicate
    unique_entities = deduplicate_entities(all_entities)
    print(f"  [TOTAL] {len(all_entities)} raw -> {len(unique_entities)} unique entities")

    return {
        "source_file": filename,
        "entidades": unique_entities,
        "stats": {
            "raw_count": len(all_entities),
            "unique_count": len(unique_entities),
            "methods_used": ["regex"] + models if use_regex else models
        }
    }


# =============================================================================
# INCREMENTAL OUTPUT MANAGEMENT
# =============================================================================

def load_existing_results(output_file: str = OUTPUT_FILE) -> List[Dict[str, Any]]:
    """Load existing extraction results from the output file."""
    if not os.path.exists(output_file):
        return []

    try:
        with open(output_file, 'r', encoding='utf-8') as f:
            data = json.load(f)
            return data if isinstance(data, list) else []
    except (json.JSONDecodeError, Exception) as e:
        print(f"[Warning] Could not load existing results: {e}")
        return []


def merge_results(
    existing: List[Dict[str, Any]],
    new_results: List[Dict[str, Any]],
    reprocessed_files: Set[str]
) -> List[Dict[str, Any]]:
    """
    Merge new extraction results with existing ones.

    - Keeps existing results for unchanged files
    - Replaces results for reprocessed files
    - Adds results for new files
    """
    # Index existing results by source file
    existing_by_file = {r.get("source_file"): r for r in existing}

    # Remove reprocessed files from existing (they'll be replaced)
    for filename in reprocessed_files:
        existing_by_file.pop(filename, None)

    # Add new results
    for result in new_results:
        existing_by_file[result.get("source_file")] = result

    return list(existing_by_file.values())


# --- Main Execution ---

def main():
    """
    Main function to run the script.
    Supports idempotent and incremental processing.
    """
    parser = argparse.ArgumentParser(
        description="Extract legal authority information from PDF documents using multiple methods.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Normal incremental run (only process new/changed files)
  python extract_authorities.py --model llama3:8b

  # Force reprocess all files
  python extract_authorities.py --force

  # Force reprocess specific file
  python extract_authorities.py --reprocess "SAT_Reglamento.pdf"

  # Show processing status without running
  python extract_authorities.py --status

  # Reset state and start fresh
  python extract_authorities.py --reset
"""
    )
    parser.add_argument(
        "--model",
        type=str,
        default="llama3:8b",
        help="Comma-separated list of Ollama models (e.g., 'llama3:8b,phi4,qwen3:8b'). Default: 'llama3:8b'."
    )
    parser.add_argument(
        "--skip-llm",
        action="store_true",
        help="If set, skips the Ollama processing and only uses regex extraction."
    )
    parser.add_argument(
        "--ocr",
        action="store_true",
        help="Force OCR (Tesseract) for all PDFs, even if they contain text."
    )
    parser.add_argument(
        "--no-regex",
        action="store_true",
        help="Disable regex-based extraction."
    )
    # Incremental processing flags
    parser.add_argument(
        "--force",
        action="store_true",
        help="Force reprocess all files, ignoring the processing state."
    )
    parser.add_argument(
        "--reprocess",
        type=str,
        metavar="FILENAME",
        help="Force reprocess a specific file by name (e.g., 'SAT_Reglamento.pdf')."
    )
    parser.add_argument(
        "--status",
        action="store_true",
        help="Show processing status and exit without running extraction."
    )
    parser.add_argument(
        "--reset",
        action="store_true",
        help="Reset the processing state (mark all files as unprocessed)."
    )
    parser.add_argument(
        "--state-file",
        type=str,
        default=STATE_FILE,
        help=f"Path to the state file. Default: '{STATE_FILE}'."
    )
    parser.add_argument(
        "--output",
        type=str,
        default=OUTPUT_FILE,
        help=f"Path to the output file. Default: '{OUTPUT_FILE}'."
    )
    args = parser.parse_args()

    # Load or initialize processing state
    state = load_processing_state(args.state_file)

    # Handle --status flag
    if args.status:
        print(get_incremental_summary(state))
        processed = state.get("processed_files", {})
        if processed:
            print("Processed files:")
            for fname, info in sorted(processed.items()):
                entities = info.get("entities_extracted", 0)
                processed_at = info.get("processed_at", "Unknown")
                print(f"  - {fname}: {entities} entities (processed: {processed_at})")
        return

    # Handle --reset flag
    if args.reset:
        print("Resetting processing state...")
        state = {
            "version": "1.0",
            "created_at": datetime.now().isoformat(),
            "last_run": None,
            "processed_files": {},
            "total_entities_extracted": 0
        }
        save_processing_state(state, args.state_file)
        print(f"State reset. State file: {args.state_file}")
        return

    # Parse models
    models = [m.strip() for m in args.model.split(",")] if not args.skip_llm else []

    print(f"\n{'='*60}")
    print(f"Entity Extraction - Idempotent & Incremental Mode")
    print(f"{'='*60}")
    print(f"Models: {models if models else 'None (regex only)'}")
    print(f"Regex: {'Enabled' if not args.no_regex else 'Disabled'}")
    print(f"OCR: {'Forced' if args.ocr else 'Auto-fallback'}")
    print(f"Mode: {'FORCE ALL' if args.force else 'Incremental'}")
    if args.reprocess:
        print(f"Reprocessing: {args.reprocess}")
    print(f"State file: {args.state_file}")
    print(f"{'='*60}")
    print(get_incremental_summary(state))

    # --- Step 1: Find PDFs to process ---
    if not os.path.isdir(PDF_DIRECTORY):
        print(f"Error: Source directory not found at '{PDF_DIRECTORY}'")
        return

    pdf_files = [f for f in os.listdir(PDF_DIRECTORY) if f.lower().endswith(".pdf")]

    if not pdf_files:
        print(f"No PDF files found in '{PDF_DIRECTORY}'. Exiting.")
        return

    # Determine which files need processing
    files_to_process = []
    skipped_files = []
    reprocessed_files = set()

    for filename in pdf_files:
        pdf_path = os.path.join(PDF_DIRECTORY, filename)

        # Check if specific file reprocessing requested
        if args.reprocess:
            if filename == args.reprocess or args.reprocess in filename:
                files_to_process.append((filename, pdf_path, "forced_reprocess"))
                reprocessed_files.add(filename)
            else:
                skipped_files.append((filename, "not_targeted"))
            continue

        # Check if force processing all
        if args.force:
            files_to_process.append((filename, pdf_path, "forced"))
            reprocessed_files.add(filename)
            continue

        # Normal incremental check
        needs_processing, reason = file_needs_processing(pdf_path, state)

        if needs_processing:
            files_to_process.append((filename, pdf_path, reason))
            if reason in ("content_changed", "size_changed"):
                reprocessed_files.add(filename)
        else:
            skipped_files.append((filename, reason))

    # Report what will be processed
    print(f"\n--- File Analysis ---")
    print(f"Total PDF files found: {len(pdf_files)}")
    print(f"Files to process: {len(files_to_process)}")
    print(f"Files skipped (unchanged): {len(skipped_files)}")

    if files_to_process:
        print(f"\nWill process:")
        for fname, _, reason in files_to_process:
            print(f"  - {fname} ({reason})")

    if not files_to_process:
        print(f"\n[OK] All files already processed. Nothing to do.")
        print(f"     Use --force to reprocess all, or --reprocess <filename> for specific file.")
        return

    # --- Step 2: Process files ---
    new_results = []

    for filename, pdf_path, reason in files_to_process:
        print(f"\n>>> Processing: {filename} ({reason})")

        # First try regular text extraction
        text_content = extract_text_from_pdf(pdf_path, use_ocr=args.ocr)

        # If no text found, automatically fallback to OCR
        if not text_content.strip():
            print(f"[Auto-OCR] No text found, trying OCR for {filename}...")
            text_content = extract_text_from_pdf(pdf_path, use_ocr=True)

        if not text_content.strip():
            print(f"Skipping {filename} - no text extracted even with OCR.")
            continue

        # Extract entities with all methods
        result = extract_all_entities(
            text_content,
            filename,
            models=models,
            use_regex=not args.no_regex
        )

        new_results.append(result)

        # Update state immediately after successful processing
        entities_count = result.get("stats", {}).get("unique_count", 0)
        mark_file_processed(state, pdf_path, entities_count, result)
        save_processing_state(state, args.state_file)
        print(f"  [State] Saved processing state for {filename}")

    # --- Step 3: Merge with existing results and save ---
    existing_results = load_existing_results(args.output)
    merged_results = merge_results(existing_results, new_results, reprocessed_files)

    save_to_json(merged_results, args.output)

    # Update total in state
    state["total_entities_extracted"] = sum(
        r.get("stats", {}).get("unique_count", 0) for r in merged_results
    )
    save_processing_state(state, args.state_file)

    # Print summary
    new_entities = sum(r.get("stats", {}).get("unique_count", 0) for r in new_results)
    total_entities = sum(r.get("stats", {}).get("unique_count", 0) for r in merged_results)

    print(f"\n{'='*60}")
    print(f"SUMMARY")
    print(f"{'='*60}")
    print(f"Files processed this run: {len(new_results)}")
    print(f"Entities extracted this run: {new_entities}")
    print(f"Total files in catalog: {len(merged_results)}")
    print(f"Total entities in catalog: {total_entities}")
    print(f"\nProcessed this run:")
    for r in new_results:
        print(f"  - {r['source_file']}: {r.get('stats', {}).get('unique_count', 0)} entities")
    print(f"\nState saved to: {args.state_file}")
    print(f"Results saved to: {args.output}")

if __name__ == "__main__":
    main()

