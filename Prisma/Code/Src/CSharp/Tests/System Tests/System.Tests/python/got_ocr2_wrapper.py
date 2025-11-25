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
import logging
from typing import Optional, Tuple, List

# Suppress warnings for cleaner output
warnings.filterwarnings("ignore")

# Configure Python logging (works in CSnakes unlike print())
# Also write to a file for debugging
import tempfile
_log_file = os.path.join(tempfile.gettempdir(), 'got_ocr2_debug.log')
logging.basicConfig(
    level=logging.DEBUG,
    format='[%(asctime)s] [%(levelname)s] %(message)s',
    handlers=[
        logging.StreamHandler(),  # Still try stderr
        logging.FileHandler(_log_file, mode='a')  # Also write to file
    ]
)
logger = logging.getLogger(__name__)
logger.info(f"GOT-OCR2 wrapper initialized. Debug log: {_log_file}")

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

def select_optimal_device(batch_size: int = 1) -> Tuple[str, any]:
    """
    Select optimal device based on batch size and strategy

    Args:
        batch_size: Number of images to process

    Returns:
        Tuple of (device, dtype)

    Strategy:
    - "auto": GPU for batch_size >= GPU_BATCH_THRESHOLD, else CPU
    - "cuda": GPU if available, else CPU
    - "cpu": Always CPU
    - "force_cuda": Always GPU (for testing)
    """
    import torch

    has_cuda = is_cuda_supported()

    if DEVICE_STRATEGY == "cpu":
        return "cpu", torch.float32

    if DEVICE_STRATEGY == "force_cuda":
        if has_cuda:
            return "cuda", torch.bfloat16
        else:
            logger.warning("force_cuda requested but CUDA not available, falling back to CPU")
            return "cpu", torch.float32

    if DEVICE_STRATEGY == "cuda":
        if has_cuda:
            return "cuda", torch.bfloat16
        else:
            return "cpu", torch.float32

    # "auto" strategy (default)
    if has_cuda and batch_size >= GPU_BATCH_THRESHOLD:
        logger.info(f"Using GPU for batch_size={batch_size} (threshold={GPU_BATCH_THRESHOLD})")
        return "cuda", torch.bfloat16
    else:
        reason = f"batch_size={batch_size} < threshold={GPU_BATCH_THRESHOLD}" if has_cuda else "CUDA not available"
        logger.info(f"Using CPU ({reason})")
        return "cpu", torch.float32

# Model configuration
MODEL_ID = os.getenv("GOT_OCR2_MODEL_ID", "stepfun-ai/GOT-OCR-2.0-hf")

# Device selection strategy
# - "auto": Use GPU for batch_size >= threshold, CPU otherwise (smart default)
# - "cuda": Always use GPU if available
# - "cpu": Always use CPU
# - "force_cuda": Force CUDA even for single images (for testing)
DEVICE_STRATEGY = os.getenv("GOT_OCR2_DEVICE_STRATEGY", "auto")
GPU_BATCH_THRESHOLD = int(os.getenv("GOT_OCR2_GPU_BATCH_THRESHOLD", "4"))  # Use GPU for 4+ images

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

    logger.debug("load_model() called")
    logger.debug(f"_model_loaded: {_model_loaded}")
    logger.debug(f"_model is None: {_model is None}")
    logger.debug(f"_processor is None: {_processor is None}")

    if _model_loaded and _model is not None and _processor is not None:
        logger.debug("Returning cached model")
        return _model, _processor

    try:
        logger.debug("Starting model load process...")
        logger.debug(f"sys.path: {sys.path[:3]}...")  # First 3 entries
        logger.debug(f"Current working directory: {os.getcwd()}")

        # Import libraries here (sys.path cleaned at module level)
        logger.debug("Importing torch...")
        import torch
        logger.debug(f"torch imported successfully, version: {torch.__version__}")

        logger.debug("Importing transformers...")
        import transformers
        logger.debug(f"transformers imported successfully, version: {transformers.__version__}")

        logger.debug("Getting AutoProcessor from transformers...")
        AutoProcessor = transformers.AutoProcessor
        logger.debug(f"AutoProcessor type: {type(AutoProcessor)}")

        logger.debug("Getting AutoModelForImageTextToText from transformers...")
        AutoModelForImageTextToText = transformers.AutoModelForImageTextToText
        logger.debug(f"AutoModelForImageTextToText type: {type(AutoModelForImageTextToText)}")

        # Initialize device config if not done
        if not _device_config_initialized:
            logger.debug("Initializing device config...")
            HAS_CUDA = is_cuda_supported()
            DEVICE = "cuda" if HAS_CUDA else "cpu"
            DTYPE = torch.bfloat16 if HAS_CUDA else torch.float32
            _device_config_initialized = True
            logger.debug(f"Device config initialized: CUDA={HAS_CUDA}, DEVICE={DEVICE}, DTYPE={DTYPE}")

        logger.info(f"Loading GOT-OCR2 model: {MODEL_ID}")
        logger.info(f"Device: {DEVICE}, dtype: {DTYPE}")

        _model = AutoModelForImageTextToText.from_pretrained(
            MODEL_ID,
            device_map=DEVICE,
            dtype=DTYPE,
            trust_remote_code=True
        )

        _processor = AutoProcessor.from_pretrained(
            MODEL_ID,
            use_fast=True,
            trust_remote_code=True
        )

        _model_loaded = True
        logger.info(f"GOT-OCR2 loaded successfully on {DEVICE}")

        return _model, _processor

    except Exception as e:
        import traceback
        logger.error(f"Failed to load GOT-OCR2!")
        logger.error(f"Exception type: {type(e).__name__}")
        logger.error(f"Exception message: {str(e)}")
        logger.error(f"Exception args: {e.args}")
        logger.error(f"Traceback:")
        traceback.print_exc()
        logger.error(f"Global state at failure:")
        logger.error(f"  _model_loaded: {_model_loaded}")
        logger.error(f"  _device_config_initialized: {_device_config_initialized}")
        logger.error(f"  HAS_CUDA: {HAS_CUDA}")
        logger.error(f"  DEVICE: {DEVICE}")
        logger.error(f"  DTYPE: {DTYPE}")
        raise

def get_model_info() -> str:
    """
    Get information about the loaded model

    Returns:
        String with model information
    """
    cuda_status = "available" if is_cuda_supported() else "not available"
    return f"GOT-OCR2 ({MODEL_ID}) | Strategy: {DEVICE_STRATEGY} | CUDA: {cuda_status} | Threshold: {GPU_BATCH_THRESHOLD}"

# -------------------------------
# OCR Execution Functions
# -------------------------------
def execute_ocr(
    image_bytes: bytes,
    language: str = "spa",
    confidence_threshold: float = 0.7,
    batch_size: int = 1
) -> Tuple[str, float, float, List[float], str]:
    """
    Execute OCR on image bytes using GOT-OCR2

    This function is designed to be called from C# via CSnakes and matches
    the IOcrExecutor interface contract.

    Args:
        image_bytes: Raw image data as bytes (from C# byte[])
        language: Primary language code (e.g., "spa", "eng")
        confidence_threshold: Confidence threshold (0.0 to 1.0)
        batch_size: Number of images in batch (for device selection)

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

        # Select optimal device for this operation
        device, dtype = select_optimal_device(batch_size)

        # Load model (cached after first call)
        model, processor = load_model()

        # Move model to selected device if needed
        if model.device.type != device:
            logger.info(f"Moving model from {model.device.type} to {device}")
            model = model.to(device)
            if dtype:
                model = model.to(dtype)

        # Convert bytes to PIL Image
        # Support both PDF and image formats
        try:
            # Try as direct image first
            image = Image.open(io.BytesIO(image_bytes)).convert("RGB")
        except Exception:
            # If that fails, try as PDF using PyMuPDF
            import fitz  # PyMuPDF
            logger.debug("Direct image loading failed, attempting PDF conversion")

            pdf_doc = fitz.open(stream=image_bytes, filetype="pdf")
            if len(pdf_doc) == 0:
                raise ValueError("PDF has no pages")

            # Convert first page to image
            page = pdf_doc[0]
            pix = page.get_pixmap(dpi=300)  # High DPI for better OCR
            img_bytes = pix.tobytes("png")
            image = Image.open(io.BytesIO(img_bytes)).convert("RGB")
            pdf_doc.close()
            logger.debug(f"Converted PDF page to image: {image.size}")

        # Process image with GOT-OCR2 processor
        inputs = processor(image, return_tensors="pt").to(device)

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
        import traceback
        error_msg = f"OCR execution failed: {str(e)}"
        logger.error(f"{error_msg}")
        logger.error(f"Exception type: {type(e).__name__}")
        logger.error(f"Traceback:")
        traceback.print_exc()
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
        logger.error(f"File not found: {file_path}")
        return ("", 0.0, 0.0, [0.0], language)
    except Exception as e:
        logger.error(f"Failed to read file: {e}")
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
    logger.debug("health_check() called")
    try:
        logger.debug("Calling load_model() from health_check...")
        result = load_model()
        logger.debug(f"load_model() returned: {type(result)}")
        logger.debug("Health check PASSED")
        return True
    except Exception as e:
        import traceback
        logger.error(f"Health check failed!")
        logger.error(f"Exception type: {type(e).__name__}")
        logger.error(f"Exception message: {str(e)}")
        logger.error(f"Traceback:")
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
