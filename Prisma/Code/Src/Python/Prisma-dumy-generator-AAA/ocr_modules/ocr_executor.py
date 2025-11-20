"""
OCR execution module using Tesseract.
Single Responsibility: Execute OCR and return structured results.
"""
import numpy as np
from typing import Dict, List, Tuple, Optional
import pytesseract
from pytesseract import Output
from .models import OCRConfig, OCRResult


def parse_tesseract_output(tesseract_data: Dict) -> Tuple[str, List[float]]:
    """
    Parse Tesseract output to extract text and confidences.
    Pure function for data transformation.
    
    Args:
        tesseract_data: Raw output from Tesseract
        
    Returns:
        Tuple of (combined text, list of word confidences)
    """
    words = []
    confidences = []
    
    texts = tesseract_data.get("text", [])
    confs = tesseract_data.get("conf", [])
    
    for text, conf in zip(texts, confs):
        if text is None:
            continue
            
        text = str(text).strip()
        if not text:
            continue
            
        words.append(text)
        
        try:
            conf_value = float(conf)
            if conf_value >= 0:  # Tesseract uses -1 for invalid confidence
                confidences.append(conf_value)
        except (ValueError, TypeError):
            pass
    
    combined_text = " ".join(words)
    return combined_text, confidences


def calculate_confidence_metrics(confidences: List[float]) -> Tuple[float, float]:
    """
    Calculate average and median confidence scores.
    Pure function for statistical calculations.
    
    Args:
        confidences: List of confidence values
        
    Returns:
        Tuple of (average confidence, median confidence)
    """
    if not confidences:
        return 0.0, 0.0
    
    conf_array = np.array(confidences)
    return float(np.mean(conf_array)), float(np.median(conf_array))


def build_tesseract_config(ocr_config: OCRConfig) -> str:
    """
    Build Tesseract configuration string.
    Pure function for config generation.
    
    Args:
        ocr_config: OCR configuration parameters
        
    Returns:
        Tesseract config string
    """
    return f"--oem {ocr_config.oem} --psm {ocr_config.psm}"


def run_tesseract_with_fallback(image: np.ndarray, 
                               primary_lang: str,
                               fallback_lang: str,
                               config_string: str) -> Tuple[Dict, str]:
    """
    Run Tesseract with automatic language fallback.
    
    Args:
        image: Input image
        primary_lang: Primary language to try
        fallback_lang: Fallback language if primary fails
        config_string: Tesseract configuration
        
    Returns:
        Tuple of (tesseract output dict, language used)
    """
    try:
        data = pytesseract.image_to_data(
            image, 
            lang=primary_lang, 
            config=config_string, 
            output_type=Output.DICT
        )
        return data, primary_lang
    except pytesseract.TesseractError:
        # Fallback to secondary language
        data = pytesseract.image_to_data(
            image, 
            lang=fallback_lang, 
            config=config_string, 
            output_type=Output.DICT
        )
        return data, fallback_lang


def execute_ocr(image: np.ndarray, config: OCRConfig) -> OCRResult:
    """
    Execute OCR on an image and return structured results.
    Pure function: deterministic output for given input.
    
    Args:
        image: Input image (should be preprocessed/binary)
        config: OCR configuration
        
    Returns:
        OCR result with text and confidence metrics
        
    Raises:
        RuntimeError: If Tesseract is not available or fails
    """
    # Build configuration
    config_string = build_tesseract_config(config)
    
    # Run OCR with fallback
    try:
        tesseract_data, language_used = run_tesseract_with_fallback(
            image,
            config.language,
            config.fallback_language,
            config_string
        )
    except Exception as e:
        raise RuntimeError(f"Tesseract execution failed: {str(e)}")
    
    # Parse output
    text, confidences = parse_tesseract_output(tesseract_data)
    
    # Calculate metrics
    avg_conf, median_conf = calculate_confidence_metrics(confidences)
    
    return OCRResult(
        text=text,
        confidence_avg=avg_conf,
        confidence_median=median_conf,
        confidences=confidences,
        language_used=language_used
    )


def check_tesseract_availability() -> bool:
    """
    Check if Tesseract is available and properly configured.
    
    Returns:
        True if Tesseract is available
    """
    try:
        pytesseract.get_tesseract_version()
        return True
    except Exception:
        return False


def get_available_languages() -> List[str]:
    """
    Get list of available Tesseract languages.
    
    Returns:
        List of language codes
    """
    try:
        return pytesseract.get_languages()
    except Exception:
        return []