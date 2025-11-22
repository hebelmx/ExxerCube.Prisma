#!/usr/bin/env python3
"""
Simple smoke test for got_ocr2_wrapper without loading the full model
"""

import sys
from pathlib import Path

# Test imports
try:
    from got_ocr2_wrapper import (
        get_version,
        get_model_info,
        calculate_confidence_heuristic,
        is_cuda_supported,
        DEVICE
    )
    print("✓ All imports successful")
except ImportError as e:
    print(f"✗ Import failed: {e}")
    sys.exit(1)

# Test basic functions
print("\n=== Basic Function Tests ===")

print(f"Version: {get_version()}")
print(f"Model Info: {get_model_info()}")
print(f"CUDA Supported: {is_cuda_supported()}")
print(f"Device: {DEVICE}")

# Test confidence heuristic
print("\n=== Confidence Heuristic Tests ===")

test_cases = [
    ("", 0.0),
    ("Hello", None),
    ("Este es un documento de prueba en español", None),
    ("This is a test document in English", None),
]

for text, expected in test_cases:
    confidence = calculate_confidence_heuristic(text, 0.7)
    print(f"Text: '{text[:30]}...' -> Confidence: {confidence:.2f}")
    if expected is not None:
        assert confidence == expected, f"Expected {expected}, got {confidence}"

print("\n✓ All smoke tests passed!")
print("\nNote: Full model tests require downloading GOT-OCR2 (~5GB)")
print("Set SKIP_MODEL_TESTS=1 to run pytest without model download")
