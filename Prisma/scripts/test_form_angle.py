#!/usr/bin/env python3
"""Test form angle detection on Q2 images."""

import cv2
import numpy as np
from PIL import Image
from pathlib import Path

def detect_form_angle(cv2_image):
    """Detect rotation angle from form structure."""
    # Edge detection
    edges = cv2.Canny(cv2_image, 50, 150, apertureSize=3)

    # Hough Line Transform
    lines = cv2.HoughLinesP(
        edges,
        rho=1,
        theta=np.pi / 180,
        threshold=100,
        minLineLength=100,
        maxLineGap=10
    )

    if lines is None or len(lines) < 10:
        return None, 0, 0

    angles = []
    for line in lines:
        x1, y1, x2, y2 = line[0]
        angle = np.degrees(np.arctan2(y2 - y1, x2 - x1))
        if angle < -45:
            angle = 90 + angle
        elif angle > 45:
            angle = angle - 90
        angles.append(angle)

    median_angle = np.median(angles)
    std_angle = np.std(angles)

    return len(lines), median_angle, std_angle


# Test on all Q2 images
test_images = [
    "222AAA-44444444442025_page-0001.jpg",
    "333BBB-44444444442025_page1.png",
    "333ccc-6666666662025_page1.png",
    "555CCC-66666662025_page1.png"
]

base_path = Path("../Fixtures/PRP1_Degraded/Q2_MediumPoor")

print("="*80)
print("FORM STRUCTURE ANGLE DETECTION TEST - Q2 Images")
print("="*80)

for img_name in test_images:
    img_path = base_path / img_name
    if not img_path.exists():
        print(f"\n{img_name}: NOT FOUND")
        continue

    img = Image.open(img_path)
    if img.mode != 'L':
        img = img.convert('L')
    cv2_img = np.array(img)

    line_count, median, std = detect_form_angle(cv2_img)

    print(f"\n{img_name}:")
    if line_count is None:
        print(f"  ❌ Insufficient lines detected")
    else:
        print(f"  Lines: {line_count}")
        print(f"  Median angle: {median:.3f}°")
        print(f"  Std deviation: {std:.3f}°")

        # Decision
        if abs(median) > 5.0:
            print(f"  ⚠ Angle too large - perspective suspected")
        elif std > 3.0:
            print(f"  ⚠ High variance - perspective suspected")
        elif abs(median) < 0.5:
            print(f"  ✓ Negligible angle")
        else:
            print(f"  ✅ SAFE to correct - angle {median:.2f}°")

print(f"\n{'='*80}")
