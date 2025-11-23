#!/usr/bin/env python3
"""
FastAPI server for GOT-OCR2 OCR service
Provides type-safe HTTP API for OCR operations
"""

import base64
import io
import warnings
from typing import List

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field
import torch
from PIL import Image

# Suppress warnings
warnings.filterwarnings("ignore")

# GOT-OCR2 imports
try:
    from transformers import AutoProcessor, AutoModelForImageTextToText
except ImportError as e:
    print(f"Error: transformers not installed. Run: pip install transformers")
    raise

# -------------------------------
# Pydantic Models
# -------------------------------
class OCRRequest(BaseModel):
    """Request model for OCR operation"""
    image_base64: str = Field(..., description="Base64-encoded image data")
    language: str = Field(default="spa", description="Language code (spa, eng, etc.)")
    confidence_threshold: float = Field(default=0.7, ge=0.0, le=1.0)

class OCRResponse(BaseModel):
    """Response model for OCR operation"""
    text: str = Field(..., description="Extracted text")
    confidence_avg: float = Field(..., description="Average confidence score (0-100)")
    confidence_median: float = Field(..., description="Median confidence score (0-100)")
    confidences: List[float] = Field(..., description="List of confidence scores")
    language_used: str = Field(..., description="Language used for OCR")

class HealthResponse(BaseModel):
    """Health check response"""
    status: str
    model_loaded: bool
    device: str
    version: str

# -------------------------------
# Device Configuration
# -------------------------------
def is_cuda_supported() -> bool:
    if not torch.cuda.is_available():
        return False
    try:
        torch.cuda.current_device()
        return True
    except Exception:
        return False

HAS_CUDA = is_cuda_supported()
DEVICE = "cuda" if HAS_CUDA else "cpu"
DTYPE = torch.bfloat16 if HAS_CUDA else torch.float32
MODEL_ID = "stepfun-ai/GOT-OCR-2.0-hf"

# Global model and processor
_model: AutoModelForImageTextToText | None = None
_processor: AutoProcessor | None = None
_model_loaded = False

# -------------------------------
# FastAPI App
# -------------------------------
app = FastAPI(
    title="GOT-OCR2 Service",
    description="OCR service using GOT-OCR-2.0 model",
    version="1.0.0"
)

# -------------------------------
# Model Loading
# -------------------------------
def load_model():
    """Load GOT-OCR2 model (lazy loading)"""
    global _model, _processor, _model_loaded

    if _model_loaded and _model is not None and _processor is not None:
        return _model, _processor

    print(f"[INFO] Loading GOT-OCR2 model: {MODEL_ID}")
    print(f"[INFO] Device: {DEVICE}, dtype: {DTYPE}")

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
    print(f"[SUCCESS] GOT-OCR2 loaded on {DEVICE}")

    return _model, _processor

def calculate_confidence_heuristic(text: str, threshold: float) -> float:
    """Calculate confidence heuristic based on text quality"""
    if not text:
        return 0.0

    # Length score (up to 30 points)
    length_score = min(len(text) / 1000.0, 1.0) * 30.0

    # Alphanumeric ratio (up to 40 points)
    alnum_count = sum(c.isalnum() for c in text)
    alnum_ratio = alnum_count / len(text) if len(text) > 0 else 0.0
    alnum_score = alnum_ratio * 40.0

    # Common words (up to 30 points)
    common_words = ["de", "la", "el", "en", "the", "and", "of", "to"]
    text_lower = text.lower()
    word_match_score = sum(1 for word in common_words if word in text_lower)
    word_match_score = min(word_match_score / len(common_words), 1.0) * 30.0

    total_score = length_score + alnum_score + word_match_score
    return max(0.0, min(100.0, total_score))

# -------------------------------
# API Endpoints
# -------------------------------
@app.get("/health", response_model=HealthResponse)
async def health_check():
    """Health check endpoint"""
    return HealthResponse(
        status="healthy",
        model_loaded=_model_loaded,
        device=DEVICE,
        version="1.0.0"
    )

@app.post("/ocr", response_model=OCRResponse)
async def execute_ocr(request: OCRRequest):
    """
    Execute OCR on base64-encoded image

    Args:
        request: OCRRequest with base64 image data

    Returns:
        OCRResponse with extracted text and confidence scores
    """
    try:
        # Load model
        model, processor = load_model()

        # Decode base64 image
        try:
            image_bytes = base64.b64decode(request.image_base64)
            image = Image.open(io.BytesIO(image_bytes)).convert("RGB")
        except Exception as e:
            raise HTTPException(status_code=400, detail=f"Invalid image data: {str(e)}")

        # Process with GOT-OCR2
        inputs = processor(image, return_tensors="pt").to(DEVICE)

        with torch.no_grad():
            generate_ids = model.generate(
                **inputs,
                do_sample=False,
                tokenizer=processor.tokenizer,
                stop_strings="<|im_end|>",
                max_new_tokens=4096,
            )

        # Decode text
        extracted_text = processor.decode(
            generate_ids[0, inputs["input_ids"].shape[1]:],
            skip_special_tokens=True
        )
        extracted_text = extracted_text.strip() if extracted_text else ""

        # Calculate confidence
        confidence_score = calculate_confidence_heuristic(extracted_text, request.confidence_threshold)

        return OCRResponse(
            text=extracted_text,
            confidence_avg=confidence_score,
            confidence_median=confidence_score,
            confidences=[confidence_score],
            language_used=request.language
        )

    except Exception as e:
        raise HTTPException(status_code=500, detail=f"OCR failed: {str(e)}")

# -------------------------------
# Startup
# -------------------------------
if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
