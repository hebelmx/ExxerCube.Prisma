#!/usr/bin/env python3
import os
import sys
import re
import json
import torch
from typing import List, Optional, Union
from pydantic import BaseModel, Field, ValidationError
from PIL import Image
# --- device & dtype selection ---
# --- Suppress CUDA warnings ---
import warnings
warnings.filterwarnings("ignore", message="CUDA initialization: Unexpected error from cudaGetDeviceCount()")

# --- Handle deprecated video processor config ---
if os.path.exists("preprocessor.json") and not os.path.exists("video_preprocessor.json"):
    os.rename("preprocessor.json", "video_preprocessor.json")
    print("[INFO] Renamed deprecated 'preprocessor.json' to 'video_preprocessor.json'", file=sys.stderr)
has_cuda = torch.cuda.is_available()

# --- Robust CUDA support check ---
def is_cuda_supported():
    if not torch.cuda.is_available():
        return False
    try:
        count = torch._C._cuda_getDeviceCount()
        if count == 0:
            return False
        # Check device capability (Ampere+ for bf16)
        major = torch.cuda.get_device_capability()[0]
        return major >= 6  # Pascal+ (GTX 10xx and newer)
    except Exception:
        return False

has_cuda = is_cuda_supported()
device = torch.device("cuda" if has_cuda else "cpu")

# Older GPUs (e.g., GTX 1060) cannot do bfloat16, prefer float16 if CUDA; else float32
if has_cuda:
    major = torch.cuda.get_device_capability()[0]
    dtype = torch.bfloat16 if major >= 8 else torch.float16
else:
    dtype = torch.float32

from transformers import (
    AutoProcessor,
    AutoModelForImageTextToText,
)

# Attention impl: use flash_attn_2 only if available (Ampere+ with proper builds)
attn_impl = None
if has_cuda:
    try:
        # torch >= 2.1 has SDPA; flash_attn_2 requires specific CUDA + wheels
        import flash_attn  # noqa: F401
        # Prefer flash_attention_2 only on Ampere+; else SDPA
        attn_impl = "flash_attention_2" if major >= 8 else "sdpa"
    except Exception:
        attn_impl = "sdpa"  # safe default
else:
    attn_impl = None

print(f"[INFO] device={device}, dtype={dtype}, attn_impl={attn_impl}", file=sys.stderr)

#DumyPrisma1.png
# -------------------------------
# Pydantic Schema Definition
# -------------------------------
class RequerimientoDetalle(BaseModel):
    descripcion: Optional[str] = Field(default="unknown")
    monto: Optional[float] = Field(default=None)
    moneda: Optional[str] = Field(default="unknown")
    activoVirtual: Optional[str] = Field(default="unknown")

class Requerimiento(BaseModel):
    fecha: Optional[str] = Field(default="unknown")
    autoridadEmisora: Optional[str] = Field(default="unknown")
    expediente: Optional[str] = Field(default="unknown")
    tipoRequerimiento: Optional[str] = Field(default="unknown")
    subtipoRequerimiento: Optional[str] = Field(default="unknown")
    fundamentoLegal: Optional[str] = Field(default="unknown")
    motivacion: Optional[str] = Field(default="unknown")
    partes: Optional[List[str]] = Field(default_factory=list)
    detalle: Optional[RequerimientoDetalle] = Field(default_factory=RequerimientoDetalle)

# -------------------------------
# Model Setup (SmolVLM2)
# -------------------------------
# Small, GPU-friendly VLM:
MODEL_ID = os.getenv("SMOLVLM_ID", "HuggingFaceTB/SmolVLM2-2.2B-Instruct")

# (Optional) use bfloat16 if supported
dtype = torch.bfloat16 if torch.cuda.is_available() else torch.float32
device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

# Load processor and model
processor = AutoProcessor.from_pretrained(MODEL_ID)
model = AutoModelForImageTextToText.from_pretrained(
    MODEL_ID,
    torch_dtype=dtype,
        _attn_implementation=attn_impl,
).to(device).eval()

# -------------------------------
# Prompt Template
# -------------------------------
SYSTEM_INSTRUCTIONS = (
    "You are an information extraction assistant for Spanish legal documents. "
    "Extract structured fields. If a field is missing or ambiguous, set it to 'unknown'. "
    "Only output a single valid JSON object and nothing else."
)

JSON_SPEC = {
    "fecha": "... (YYYY-MM-DD if known, else 'unknown')",
    "autoridadEmisora": "...",
    "expediente": "...",
    "tipoRequerimiento": "...",
    "subtipoRequerimiento": "...",
    "fundamentoLegal": "...",
    "motivacion": "...",
    "partes": ["..."],  # list of strings like full names (or empty if unknown)
    "detalle": {
        "descripcion": "...",
        "monto": None,
        "moneda": "MXN or 'unknown'",
        "activoVirtual": "..."
    }
}

USER_PROMPT = (
    "Read this Spanish legal document and extract: fecha (date), autoridad emisora (issuing authority), "
    "expediente (case number), and tipo de requerimiento (type of requirement). "
    "Output as valid JSON only. Example: {'fecha': '2024-01-15', 'autoridadEmisora': 'CONDUSEF'}"
)

# -------------------------------
# Extraction Utility
# -------------------------------
def _parse_json_strict_or_relaxed(text: str) -> dict:
    """
    Try strict JSON parse first.
    If that fails, try to find the largest JSON object substring and parse that.
    """
    # Strict
    try:
        return json.loads(text)
    except Exception:
        pass

    # Relaxed: find outermost JSON object
    start = text.find("{")
    end = text.rfind("}")
    if start != -1 and end != -1 and end > start:
        candidate = text[start : end + 1]
        # replace single-quotes with double quotes carefully if needed
        # but avoid breaking numbers/null/true/false
        # heuristic: only replace quotes around keys
        try:
            # Quick attempt as-is
            return json.loads(candidate)
        except Exception:
            # Heuristic normalization – last resort
            normalized = re.sub(r"(?<!\\)'", '"', candidate)
            return json.loads(normalized)

    # Give up
    raise json.JSONDecodeError("No JSON object could be decoded", text, 0)

# -------------------------------
# Inference Wrapper
# -------------------------------
def extract_requerimiento_from_image(image_path: str, max_new_tokens: int = 512) -> Union[Requerimiento, str]:
    image = Image.open(image_path).convert("RGB")

    # Build chat messages per HF docs; pass PIL image directly
    messages = [
        {"role": "system", "content": [{"type": "text", "text": SYSTEM_INSTRUCTIONS}]},
        {
            "role": "user",
            "content": [
                {"type": "image", "image": image},
                {"type": "text", "text": USER_PROMPT},
            ],
        },
    ]

    # Apply chat template
    inputs = processor.apply_chat_template(
        messages,
        add_generation_prompt=True,
        tokenize=True,
        return_dict=True,
        return_tensors="pt",
    )

    # Move tensors and image features to device/dtype
    inputs = {
        k: (
            v.to(device, dtype=dtype) if torch.is_tensor(v) and v.dtype.is_floating_point
            else v.to(device) if torch.is_tensor(v)
            else v
        )
        for k, v in inputs.items()
    }

    with torch.no_grad():
        generated_ids = model.generate(
            **inputs,
            do_sample=False,              # deterministic for stability
            max_new_tokens=max_new_tokens
        )

    result_text = processor.batch_decode(generated_ids, skip_special_tokens=True)[0].strip()

    # Robust JSON parsing
    try:
        data = _parse_json_strict_or_relaxed(result_text)
        validated = Requerimiento(**data)
        return validated
    except (json.JSONDecodeError, ValidationError) as e:
        return f"Error parsing model output: {e}\nRaw output:\n{result_text}"

# -------------------------------
# CLI Entry Point
# -------------------------------
if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(
        description="Extract structured legal requerimiento from image using SmolVLM2-2.2B-Instruct"
    )
    parser.add_argument("--image", required=False, default="/home/abel/projects/Prisma/ExxerCube.Prisma/Prisma/Docs/DumyPrisma1.png", help="Path to input image (default: DumyPrisma1.png)")
    parser.add_argument("--max_new_tokens", type=int, default=512, help="Max new tokens to generate")
    args = parser.parse_args()

    result = extract_requerimiento_from_image(args.image, max_new_tokens=args.max_new_tokens)
    if isinstance(result, Requerimiento):
        print(result.model_dump_json(indent=2))
    else:
        print(result)
