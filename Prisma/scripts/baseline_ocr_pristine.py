#!/usr/bin/env python3
"""
Baseline OCR for pristine documents.
Gets confidence scores and OCR text for the 4 source documents.
"""

import subprocess
import json
from pathlib import Path
from PIL import Image
import cv2
import numpy as np

# Pristine document paths
BASE_PATH = Path(__file__).parent.parent / "Fixtures" / "PRP1"
DOCUMENTS = [
    "222AAA-44444444442025_page-1.png",  # Converted from PDF to PNG
    "333BBB-44444444442025_page1.png",
    "333ccc-6666666662025_page1.png",
    "555CCC-66666662025_page-0001.png",
]


def run_tesseract_with_confidence(image_path: Path) -> dict:
    """Run Tesseract and get both text and confidence."""
    # Get OCR text
    result_text = subprocess.run(
        ["tesseract", str(image_path), "stdout", "-l", "spa", "--psm", "6"],
        capture_output=True, text=True, timeout=60
    )
    ocr_text = result_text.stdout

    # Get confidence via TSV output
    result_tsv = subprocess.run(
        ["tesseract", str(image_path), "stdout", "-l", "spa", "--psm", "6", "tsv"],
        capture_output=True, text=True, timeout=60
    )

    # Parse TSV for confidence (conf is float like 96.580193)
    confidences = []
    for line in result_tsv.stdout.strip().split('\n')[1:]:  # Skip header
        parts = line.split('\t')
        if len(parts) >= 12:  # level, page_num, ... conf, text
            conf_str = parts[10]
            try:
                conf = float(conf_str)
                if conf > 0:  # Only count valid confidences (skip -1)
                    confidences.append(conf)
            except ValueError:
                pass

    mean_conf = sum(confidences) / len(confidences) if confidences else 0

    return {
        "text": ocr_text,
        "mean_confidence": round(mean_conf, 2),
        "word_count": len(confidences),
        "min_confidence": min(confidences) if confidences else 0,
        "max_confidence": max(confidences) if confidences else 0,
        "char_count": len(ocr_text),
    }


def analyze_image_properties(image_path: Path) -> dict:
    """Analyze image properties for clustering."""
    img = cv2.imread(str(image_path), cv2.IMREAD_GRAYSCALE)
    if img is None:
        return {}

    # Blur score (Laplacian variance - higher = sharper)
    blur_score = cv2.Laplacian(img, cv2.CV_64F).var()

    # Noise estimation (high-frequency via Laplacian)
    noise_estimate = np.std(cv2.Laplacian(img, cv2.CV_64F))

    # Contrast (standard deviation)
    contrast = np.std(img)

    # Brightness (mean)
    brightness = np.mean(img)

    # Edge density (Canny)
    edges = cv2.Canny(img, 50, 150)
    edge_density = np.sum(edges > 0) / edges.size

    # FFT analysis
    f = np.fft.fft2(img)
    fshift = np.fft.fftshift(f)
    magnitude = np.abs(fshift)

    h, w = magnitude.shape
    center_y, center_x = h // 2, w // 2

    # Low freq (center) vs high freq (edges)
    low_freq_mask = np.zeros_like(magnitude)
    cv2.circle(low_freq_mask, (center_x, center_y), min(h, w) // 8, 1, -1)

    low_freq_energy = np.sum(magnitude * low_freq_mask)
    high_freq_energy = np.sum(magnitude * (1 - low_freq_mask))
    total_energy = low_freq_energy + high_freq_energy

    high_freq_pct = high_freq_energy / total_energy * 100 if total_energy > 0 else 0

    return {
        "blur_score": round(blur_score, 2),
        "noise_estimate": round(noise_estimate, 2),
        "contrast": round(contrast, 2),
        "brightness": round(brightness, 2),
        "edge_density": round(edge_density * 100, 2),  # percentage
        "high_freq_pct": round(high_freq_pct, 2),
        "dimensions": f"{img.shape[1]}x{img.shape[0]}",
    }


def main():
    print("=" * 70)
    print("BASELINE OCR FOR PRISTINE DOCUMENTS")
    print("=" * 70)
    print()

    results = {}

    for doc_name in DOCUMENTS:
        doc_path = BASE_PATH / doc_name
        if not doc_path.exists():
            print(f"WARNING: {doc_name} not found!")
            continue

        print(f"Processing: {doc_name}")
        print("-" * 50)

        # Get OCR results
        ocr_result = run_tesseract_with_confidence(doc_path)

        # Get image properties
        img_props = analyze_image_properties(doc_path)

        # Store results
        doc_id = doc_name.split("-")[0]  # e.g., "222AAA"
        results[doc_id] = {
            "filename": doc_name,
            "path": str(doc_path),
            "ocr": ocr_result,
            "image_properties": img_props,
        }

        print(f"  Confidence: {ocr_result['mean_confidence']:.1f}% (words: {ocr_result['word_count']})")
        print(f"  Char count: {ocr_result['char_count']}")
        print(f"  Blur score: {img_props.get('blur_score', 'N/A')}")
        print(f"  Contrast:   {img_props.get('contrast', 'N/A')}")
        print(f"  Brightness: {img_props.get('brightness', 'N/A')}")
        print(f"  Edge density: {img_props.get('edge_density', 'N/A')}%")
        print(f"  High freq:  {img_props.get('high_freq_pct', 'N/A')}%")
        print()

    # Save results
    output_path = BASE_PATH.parent / "pristine_baseline_ocr.json"
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(results, f, indent=2, ensure_ascii=False)

    print("=" * 70)
    print(f"Results saved to: {output_path}")
    print("=" * 70)

    # Summary table
    print()
    print("SUMMARY TABLE")
    print("-" * 70)
    print(f"{'Document':<12} {'Confidence':<12} {'Blur':<10} {'Contrast':<10} {'Edge%':<10}")
    print("-" * 70)
    for doc_id, data in results.items():
        conf = data['ocr']['mean_confidence']
        blur = data['image_properties'].get('blur_score', 0)
        contrast = data['image_properties'].get('contrast', 0)
        edge = data['image_properties'].get('edge_density', 0)
        print(f"{doc_id:<12} {conf:<12.1f} {blur:<10.1f} {contrast:<10.1f} {edge:<10.2f}")

    return results


if __name__ == "__main__":
    main()
