#!/usr/bin/env python3
"""
GOT-OCR2 Wrapper for CSnakes Integration
Provides type-safe Python functions callable from C# via CSnakes

This module wraps GOT-OCR2 (General OCR Theory 2.0) model to implement
the IOcrExecutor interface contract from C#.
"""

import io
import os
import sys
import warnings
from typing import Optional, Tuple, List

# Suppress warnings for cleaner output
warnings.filterwarnings("ignore")

# Remove current directory from sys.path to prevent torch import conflicts
# Keep original path for restoration
_original_sys_path = sys.path.copy()
_module_dir = os.path.dirname(os.path.abspath(__file__))
if _module_dir in sys.path:
    sys.path.remove(_module_dir)
if '' in sys.path:
    sys.path.remove('')

# Delay imports to avoid path issues - will import in load_model()
# Core libraries will be imported when needed

# -------------------------------
# Device Configuration
# -------------------------------
def is_cuda_supported() -> bool:
    """Check if CUDA is available and working"""
    import torch
    if not torch.cuda.is_available():
        return False
    try:
        torch.cuda.current_device()
        torch.cuda.get_device_name(0)
        return True
    except Exception:
        return False

# Model configuration
MODEL_ID = os.getenv("GOT_OCR2_MODEL_ID", "stepfun-ai/GOT-OCR-2.0-hf")

# Global model and processor (lazy loaded)
_model = None
_processor = None
_model_loaded = False
_device_config_initialized = False
HAS_CUDA = True
DEVICE = "cuda"
DTYPE = None

# -------------------------------
# Model Loading
# -------------------------------
def load_model():
    """
    Load GOT-OCR2 model and processor (cached after first load)

    Returns:
        Tuple of (model, processor)
    """
    global _model, _processor, _model_loaded, _device_config_initialized, HAS_CUDA, DEVICE, DTYPE

    print("[DEBUG] load_model() called")
    print(f"[DEBUG] _model_loaded: {_model_loaded}")
    print(f"[DEBUG] _model is None: {_model is None}")
    print(f"[DEBUG] _processor is None: {_processor is None}")

    if _model_loaded and _model is not None and _processor is not None:
        print("[DEBUG] Returning cached model")
        return _model, _processor

    try:
        print("[DEBUG] Starting model load process...")
        print(f"[DEBUG] sys.path: {sys.path[:3]}...")  # First 3 entries
        print(f"[DEBUG] Current working directory: {os.getcwd()}")

        # Import libraries here (sys.path cleaned at module level)
        print("[DEBUG] Importing torch...")
        import torch
        print(f"[DEBUG] torch imported successfully, version: {torch.__version__}")

        print("[DEBUG] Importing transformers...")
        import transformers
        print(f"[DEBUG] transformers imported successfully, version: {transformers.__version__}")

        print("[DEBUG] Getting AutoProcessor from transformers...")
        AutoProcessor = transformers.AutoProcessor
        print(f"[DEBUG] AutoProcessor type: {type(AutoProcessor)}")

        print("[DEBUG] Getting AutoModelForImageTextToText from transformers...")
        AutoModelForImageTextToText = transformers.AutoModelForImageTextToText
        print(f"[DEBUG] AutoModelForImageTextToText type: {type(AutoModelForImageTextToText)}")

        # Initialize device config if not done
        if not _device_config_initialized:
            print("[DEBUG] Initializing device config...")
            HAS_CUDA = is_cuda_supported()
            DEVICE = "cuda" if HAS_CUDA else "cpu"
            DTYPE = torch.bfloat16 if HAS_CUDA else torch.float32
            _device_config_initialized = True
            print(f"[DEBUG] Device config initialized: CUDA={HAS_CUDA}, DEVICE={DEVICE}, DTYPE={DTYPE}")

        print(f"[INFO] Loading GOT-OCR2 model: {MODEL_ID}")
        print(f"[INFO] Device: {DEVICE}, dtype: {DTYPE}")

        _model = AutoModelForImageTextToText.from_pretrained(
            MODEL_ID,
            device_map=DEVICE,
            torch_dtype=DTYPE,
            trust_remote_code=True
        )

        _processor = AutoProcessor.from_pretrained(
            MODEL_ID,
            use_fast=True,
            trust_remote_code=True
        )

        _model_loaded = True
        print(f"[SUCCESS] GOT-OCR2 loaded successfully on {DEVICE}")

        return _model, _processor

    except Exception as e:
        import traceback
        print(f"[ERROR] Failed to load GOT-OCR2!")
        print(f"[ERROR] Exception type: {type(e).__name__}")
        print(f"[ERROR] Exception message: {str(e)}")
        print(f"[ERROR] Exception args: {e.args}")
        print(f"[ERROR] Traceback:")
        traceback.print_exc()
        print(f"[ERROR] Global state at failure:")
        print(f"[ERROR]   _model_loaded: {_model_loaded}")
        print(f"[ERROR]   _device_config_initialized: {_device_config_initialized}")
        print(f"[ERROR]   HAS_CUDA: {HAS_CUDA}")
        print(f"[ERROR]   DEVICE: {DEVICE}")
        print(f"[ERROR]   DTYPE: {DTYPE}")
        raise

def get_model_info() -> str:
    """
    Get information about the loaded model

    Returns:
        String with model information
    """
    return f"GOT-OCR2 ({MODEL_ID}) on {DEVICE} with dtype {DTYPE}"

# -------------------------------
# OCR Execution Functions
# -------------------------------
def execute_ocr(
    image_bytes: bytes,
    language: str = "spa",
    confidence_threshold: float = 0.7
) -> Tuple[str, float, float, List[float], str]:
    """
    Execute OCR on image bytes using GOT-OCR2

    This function is designed to be called from C# via CSnakes and matches
    the IOcrExecutor interface contract.

    Args:
        image_bytes: Raw image data as bytes (from C# byte[])
        language: Primary language code (e.g., "spa", "eng")
        confidence_threshold: Confidence threshold (0.0 to 1.0)

    Returns:
        Tuple containing:
        - text (str): Extracted text from OCR
        - confidence_avg (float): Average confidence score (0-100)
        - confidence_median (float): Median confidence score (0-100)
        - confidences (List[float]): List of per-word confidence scores
        - language_used (str): Language used for OCR

    Example:
        >>> with open("document.pdf", "rb") as f:
        ...     image_data = f.read()
        >>> text, avg, median, scores, lang = execute_ocr(image_data, "spa", 0.7)
    """
    try:
        # Import libraries here (sys.path cleaned at module level)
        import torch
        from PIL import Image

        # Load model (cached after first call)
        model, processor = load_model()

        # Convert bytes to PIL Image
        image = Image.open(io.BytesIO(image_bytes)).convert("RGB")

        # Process image with GOT-OCR2 processor
        inputs = processor(image, return_tensors="pt").to(DEVICE)

        # Generate OCR output
        with torch.no_grad():
            generate_ids = model.generate(
                **inputs,
                do_sample=False,
                tokenizer=processor.tokenizer,
                stop_strings="<|im_end|>",
                max_new_tokens=4096,
            )

        # Decode the generated text
        extracted_text = processor.decode(
            generate_ids[0, inputs["input_ids"].shape[1]:],
            skip_special_tokens=True
        )

        # Clean up text
        extracted_text = extracted_text.strip() if extracted_text else ""

        # Calculate confidence metrics
        # Note: GOT-OCR2 doesn't provide per-word confidence scores like Tesseract
        # We use a heuristic based on text length and quality
        confidence_score = calculate_confidence_heuristic(extracted_text, confidence_threshold)

        # For compatibility with IOcrExecutor interface, we return the same confidence
        # for avg and median since we don't have per-word scores
        confidence_avg = confidence_score
        confidence_median = confidence_score

        # Return a single confidence score in the list (no per-word scores available)
        confidences = [confidence_score]

        return (
            extracted_text,
            confidence_avg,
            confidence_median,
            confidences,
            language
        )

    except Exception as e:
        error_msg = f"OCR execution failed: {str(e)}"
        print(f"[ERROR] {error_msg}")
        # Return empty result with zero confidence on error
        return ("", 0.0, 0.0, [0.0], language)

def calculate_confidence_heuristic(text: str, threshold: float) -> float:
    """
    Calculate confidence score heuristic based on extracted text quality

    Args:
        text: Extracted text
        threshold: Confidence threshold

    Returns:
        Confidence score (0-100)
    """
    if not text:
        return 0.0

    # Basic heuristics for confidence estimation
    # 1. Text length (longer is generally better, up to a point)
    length_score = min(len(text) / 1000.0, 1.0) * 30.0

    # 2. Ratio of alphanumeric characters (higher is better)
    alnum_count = sum(c.isalnum() for c in text)
    alnum_ratio = alnum_count / len(text) if len(text) > 0 else 0.0
    alnum_score = alnum_ratio * 40.0

    # 3. Presence of common Spanish/English words (basic check)
    common_words = ["de", "la", "el", "en", "the", "and", "of", "to"]
    text_lower = text.lower()
    word_match_score = sum(1 for word in common_words if word in text_lower)
    word_match_score = min(word_match_score / len(common_words), 1.0) * 30.0

    # Combine scores
    total_score = length_score + alnum_score + word_match_score

    # Ensure score is within 0-100 range
    total_score = max(0.0, min(100.0, total_score))

    return total_score

def execute_ocr_from_file(
    file_path: str,
    language: str = "spa",
    confidence_threshold: float = 0.7
) -> Tuple[str, float, float, List[float], str]:
    """
    Execute OCR on an image file (convenience function for testing)

    Args:
        file_path: Path to image file
        language: Primary language code
        confidence_threshold: Confidence threshold

    Returns:
        Same tuple as execute_ocr()
    """
    try:
        with open(file_path, "rb") as f:
            image_bytes = f.read()
        return execute_ocr(image_bytes, language, confidence_threshold)
    except FileNotFoundError:
        print(f"[ERROR] File not found: {file_path}")
        return ("", 0.0, 0.0, [0.0], language)
    except Exception as e:
        print(f"[ERROR] Failed to read file: {e}")
        return ("", 0.0, 0.0, [0.0], language)

# -------------------------------
# Module Info and Health Check
# -------------------------------
def get_version() -> str:
    """Get module version"""
    return "1.0.0"

def health_check() -> bool:
    """
    Perform health check to verify model can be loaded

    Returns:
        True if model loads successfully, False otherwise
    """
    print("[DEBUG] health_check() called")
    try:
        print("[DEBUG] Calling load_model() from health_check...")
        result = load_model()
        print(f"[DEBUG] load_model() returned: {type(result)}")
        print("[DEBUG] Health check PASSED")
        return True
    except Exception as e:
        import traceback
        print(f"[ERROR] Health check failed!")
        print(f"[ERROR] Exception type: {type(e).__name__}")
        print(f"[ERROR] Exception message: {str(e)}")
        print(f"[ERROR] Traceback:")
        traceback.print_exc()
        return False

# -------------------------------
# CLI Testing (Optional)
# -------------------------------
if __name__ == "__main__":
    import argparse
    import json

    parser = argparse.ArgumentParser(description="GOT-OCR2 Wrapper Test")
    parser.add_argument("--image", help="Path to image file")
    parser.add_argument("--health", action="store_true", help="Run health check")
    parser.add_argument("--info", action="store_true", help="Show model info")

    args = parser.parse_args()

    if args.health:
        is_healthy = health_check()
        print(f"Health check: {'PASS' if is_healthy else 'FAIL'}")

    elif args.info:
        print(f"Version: {get_version()}")
        print(f"Model: {get_model_info()}")

    elif args.image:
        print(f"Processing: {args.image}")
        text, avg, median, confidences, lang = execute_ocr_from_file(args.image)

        result = {
            "text": text[:200] + "..." if len(text) > 200 else text,
            "confidence_avg": avg,
            "confidence_median": median,
            "confidence_count": len(confidences),
            "language_used": lang,
            "text_length": len(text)
        }

        print(json.dumps(result, indent=2, ensure_ascii=False))

    else:
        parser.print_help()
