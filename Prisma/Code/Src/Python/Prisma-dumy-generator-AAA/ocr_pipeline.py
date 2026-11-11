#!/usr/bin/env python3
# Fast OCR Pipeline for Spanish Documents with Red Diagonal Watermarks
# -------------------------------------------------------------------
# - Removes/attenuates red watermark text
# - Deskews, binarizes, and OCRs with Tesseract (spa)
# - Extracts sections: "CAUSA QUE MOTIVA EL REQUERIMIENTO" and "ACCIÓN SOLICITADA"
# - Normalizes dates and amounts
# - Outputs TXT + JSON with confidences
#
# Usage:
#   python ocr_pipeline.py --input /path/to/image_or_folder --outdir ./out
#
# Dependencies (pip):
#   pip install opencv-python pillow pytesseract numpy unidecode
#   # Optional: Install tesseract-ocr and Spanish language pack in your OS.
#   # Ubuntu: sudo apt-get install tesseract-ocr tesseract-ocr-spa
#   # Windows: install Tesseract and add tesseract.exe to PATH; download spa traineddata.
#
# Notes:
#   - If spa is not available, it will fallback to eng automatically.
#   - For batch folders, files with extensions (png, jpg, jpeg, tiff, bmp, pdf*) are processed.
#   - PDF support requires `pip install pdf2image` and poppler installed.

import argparse
import json
import os
import re
import sys
from dataclasses import dataclass, asdict
from typing import List, Optional

import cv2
import numpy as np
from PIL import Image
from unidecode import unidecode

try:
    import pytesseract
    from pytesseract import Output as TessOutput
except Exception as e:
    print("ERROR: pytesseract not available:", e, file=sys.stderr)
    sys.exit(2)

try:
    from pdf2image import convert_from_path  # optional
    PDF2IMAGE = True
except Exception:
    PDF2IMAGE = False

SUPPORTED_EXTS = {".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".pdf"}

@dataclass
class OCRResult:
    text: str
    confidence_avg: float
    confidence_median: float
    confidences: List[float]

@dataclass
class ExtractedFields:
    expediente: Optional[str]
    causa: Optional[str]
    accion_solicitada: Optional[str]
    fechas: List[str]
    montos: List[dict]
    confianza_ocr: float

HEADER_ALIASES_CAUSA = [
    "CAUSA QUE MOTIVA EL REQUERIMIENTO",
    "CAUSA QUE MOTIVA EL REQUERIMIENTO.",
    "CAUSA DEL REQUERIMIENTO",
    "CAUSA QUE MOTIVA",
]

HEADER_ALIASES_ACCION = [
    "ACCIÓN SOLICITADA",
    "ACCION SOLICITADA",
    "ACCIÓN REQUERIDA",
    "ACCION REQUERIDA",
    "PETICIÓN",
    "PETICION",
    "REQUERIMIENTO",
]

MONTHS = {
    "enero": "01", "febrero": "02", "marzo": "03", "abril": "04",
    "mayo": "05", "junio": "06", "julio": "07", "agosto": "08",
    "septiembre": "09", "setiembre": "09", "octubre": "10",
    "noviembre": "11", "diciembre": "12"
}

def is_pdf(path: str) -> bool:
    return os.path.splitext(path)[1].lower() == ".pdf"

def list_inputs(path: str) -> List[str]:
    if os.path.isdir(path):
        files = []
        for root, _, filenames in os.walk(path):
            for fn in filenames:
                ext = os.path.splitext(fn)[1].lower()
                if ext in SUPPORTED_EXTS:
                    files.append(os.path.join(root, fn))
        return sorted(files)
    else:
        return [path]

def load_images(path: str) -> List[np.ndarray]:
    """Load image(s) from path. For PDFs, convert pages to images if available."""
    if is_pdf(path):
        if not PDF2IMAGE:
            raise RuntimeError("PDF support requires pdf2image + poppler installed.")
        images: List[Image.Image] = convert_from_path(path, dpi=300)
        return [cv2.cvtColor(np.array(im), cv2.COLOR_RGB2BGR) for im in images]
    img = cv2.imread(path, cv2.IMREAD_COLOR)
    if img is None:
        raise RuntimeError(f"Could not read image: {path}")
    return [img]

def remove_red_watermark(bgr: np.ndarray) -> np.ndarray:
    """Suppress red regions (watermark) and inpaint lightly."""
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    H, S, V = hsv[:,:,0], hsv[:,:,1], hsv[:,:,2]
    # thresholds tuned for vivid red marks
    mask1 = (H < 10) & (S > 80) & (V > 80)
    mask2 = (H > 170) & (S > 80) & (V > 80)
    mask = (mask1 | mask2).astype(np.uint8) * 255

    # grow slightly to cover thin strokes
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3,3))
    mask = cv2.dilate(mask, kernel, iterations=1)

    # Inpaint to fill red marks using nearby background
    cleaned = cv2.inpaint(bgr, mask, 3, cv2.INPAINT_TELEA)
    return cleaned

def deskew(gray: np.ndarray) -> np.ndarray:
    """Estimate skew angle and rotate."""
    thr = cv2.threshold(gray, 0, 255, cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU)[1]
    coords = np.column_stack(np.where(thr > 0))
    if coords.size == 0:
        return gray
    rect = cv2.minAreaRect(coords)
    angle = rect[-1]
    if angle < -45:
        angle = 90 + angle
    (h, w) = gray.shape[:2]
    M = cv2.getRotationMatrix2D((w // 2, h // 2), angle, 1.0)
    rotated = cv2.warpAffine(gray, M, (w, h), flags=cv2.INTER_CUBIC, borderMode=cv2.BORDER_REPLICATE)
    return rotated

def sauvola_threshold(gray: np.ndarray) -> np.ndarray:
    """Adaptive threshold (Sauvola/Niblack-like); fallback to Gaussian if ximgproc absent."""
    try:
        bin_img = cv2.ximgproc.niBlackThreshold(gray, 255, cv2.THRESH_BINARY, 41, -0.2)
    except Exception:
        bin_img = cv2.adaptiveThreshold(gray, 255, cv2.ADAPTIVE_THRESH_GAUSSIAN_C,
                                        cv2.THRESH_BINARY, 41, 11)
    return bin_img

def preprocess(bgr: np.ndarray) -> np.ndarray:
    cleaned = remove_red_watermark(bgr)
    gray = cv2.cvtColor(cleaned, cv2.COLOR_BGR2GRAY)
    gray = deskew(gray)
    bin_img = sauvola_threshold(gray)
    bin_inv = cv2.bitwise_not(bin_img)
    return bin_inv

def run_tesseract(img: np.ndarray, prefer_spa: bool = True) -> OCRResult:
    lang = "spa" if prefer_spa else "eng"
    config = "--oem 1 --psm 6"
    try:
        data = pytesseract.image_to_data(img, lang=lang, config=config, output_type=TessOutput.DICT)
    except pytesseract.TesseractError:
        data = pytesseract.image_to_data(img, lang="eng", config=config, output_type=TessOutput.DICT)

    words, confs = [], []
    for t, c in zip(data.get("text", []), data.get("conf", [])):
        if t is None:
            continue
        try:
            c = float(c)
        except Exception:
            c = -1.0
        if t.strip():
            words.append(t)
            if c >= 0:
                confs.append(c)
    text = " ".join(words)
    conf_arr = np.array(confs) if confs else np.array([0.0])
    return OCRResult(text=text,
                     confidence_avg=float(np.mean(conf_arr)),
                     confidence_median=float(np.median(conf_arr)),
                     confidences=confs)

def normalize_text(s: str) -> str:
    s = re.sub(r"\s+([,.;:])", r"\1", s)
    s = re.sub(r"\s{2,}", " ", s)
    return s.strip()

def find_section(text: str, start_aliases: List[str], end_aliases: List[str]) -> Optional[str]:
    norm = unidecode(text.upper())
    starts = [unidecode(a.upper()) for a in start_aliases]
    ends = [unidecode(a.upper()) for a in end_aliases]

    start_idx = -1
    best = None
    for alias in starts:
        i = norm.find(alias)
        if i != -1 and (best is None or i < best):
            best = i
            start_idx = i
    if start_idx == -1:
        return None

    next_end = len(norm)
    for alias in ends:
        j = norm.find(alias, start_idx + 1)
        if j != -1:
            next_end = min(next_end, j)
    return text[start_idx:next_end].strip()

def extract_fields(text: str, conf: float) -> ExtractedFields:
    exp = None
    m = re.search(r"\b(expediente|exp\.?|exped\.)\s*[:#]?\s*([A-Za-z0-9\-\/\.]+)", text, flags=re.IGNORECASE)
    if m:
        exp = m.group(2)

    causa = find_section(text, HEADER_ALIASES_CAUSA, HEADER_ALIASES_ACCION)
    accion = find_section(text, HEADER_ALIASES_ACCION, [])

    fechas = []
    for d in re.finditer(r"(\d{1,2})\s+de\s+([A-Za-zñÑáéíóúÁÉÍÓÚ]+)\s+de\s+(\d{4})", text, flags=re.IGNORECASE):
        dd, mm_name, yyyy = d.group(1), d.group(2).lower(), d.group(3)
        mm = MONTHS.get(unidecode(mm_name), None)
        if mm:
            fechas.append(f"{int(yyyy):04d}-{mm}-{int(dd):02d}")

    montos = []
    for m in re.finditer(r"\$[\s]*([0-9]{1,3}(?:[.,][0-9]{3})*|[0-9]+)(?:[.,]([0-9]{2}))?", text):
        whole = m.group(1).replace(".", "").replace(",", "")
        cents = m.group(2) or "00"
        try:
            val = float(f"{int(whole)}.{cents}")
            montos.append({"moneda": "MXN", "valor": val})
        except Exception:
            pass

    return ExtractedFields(
        expediente=exp,
        causa=normalize_text(causa) if causa else None,
        accion_solicitada=normalize_text(accion) if accion else None,
        fechas=sorted(set(fechas)),
        montos=montos,
        confianza_ocr=round(conf/100.0, 3)
    )

def save_outputs(base_out: str, text: str, fields: ExtractedFields):
    os.makedirs(os.path.dirname(base_out), exist_ok=True)
    with open(base_out + ".txt", "w", encoding="utf-8") as f:
        f.write(text)
    with open(base_out + ".json", "w", encoding="utf-8") as f:
        json.dump(asdict(fields), f, ensure_ascii=False, indent=2)

def process_file(path: str, outdir: str, verbose: bool = False) -> List[str]:
    pages = load_images(path)
    outputs = []
    for i, bgr in enumerate(pages):
        pre = preprocess(bgr)
        ocr = run_tesseract(pre, prefer_spa=True)
        fields = extract_fields(ocr.text, ocr.confidence_avg)
        stem = os.path.splitext(os.path.basename(path))[0]
        suffix = f"_p{i+1}" if len(pages) > 1 else ""
        base_out = os.path.join(outdir, f"{stem}{suffix}")
        save_outputs(base_out, ocr.text, fields)
        outputs.append(base_out)
        if verbose:
            print(f"[OK] {path} page {i+1}: avg_conf={ocr.confidence_avg:.1f}, median={ocr.confidence_median:.1f}")
            if not fields.causa or not fields.accion_solicitada:
                print("  [WARN] Sections not fully detected; consider adjusting header aliases.", file=sys.stderr)
    return outputs

def main():
    ap = argparse.ArgumentParser(description="Fast OCR pipeline for Spanish legal docs with red watermark removal.")
    ap.add_argument("--input", required=True, help="Path to image/PDF or folder")
    ap.add_argument("--outdir", required=True, help="Output directory for TXT/JSON")
    ap.add_argument("--verbose", action="store_true", help="Verbose logs")
    args = ap.parse_args()

    inputs = list_inputs(args.input)
    if not inputs:
        print("No inputs found.", file=sys.stderr)
        sys.exit(1)

    os.makedirs(args.outdir, exist_ok=True)
    for p in inputs:
        try:
            process_file(p, args.outdir, verbose=args.verbose)
        except Exception as e:
            print(f"[ERROR] {p}: {e}", file=sys.stderr)

if __name__ == "__main__":
    main()
