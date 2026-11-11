"""
Image Degradation Script for OCR Performance Testing

Generates realistic degraded versions of document scans to test OCR robustness.
Simulates real-world scanner artifacts and document handling issues.

Quality Levels:
- Q1_Poor: Light degradation (slight blur, minor noise, small rotation)
- Q2_MediumPoor: Moderate degradation (blur, noise, JPEG compression)
- Q3_Low: Heavy degradation (strong blur, salt-pepper noise, contrast issues)
- Q4_VeryLow: Extreme but human-readable (maximum artifacts, heavy compression)

Usage:
    python degrade_images_for_ocr_testing.py
"""

from PIL import Image, ImageFilter, ImageEnhance, ImageDraw
import random
import os
from pathlib import Path
from typing import Tuple
import numpy as np


class DegradationProfile:
    """Configuration for a specific degradation level."""

    def __init__(
        self,
        name: str,
        blur_radius: float,
        noise_intensity: float,
        rotation_angle: float,
        contrast_factor: float,
        brightness_factor: float,
        jpeg_quality: int,
        salt_pepper_amount: float,
        add_scan_lines: bool
    ):
        self.name = name
        self.blur_radius = blur_radius
        self.noise_intensity = noise_intensity
        self.rotation_angle = rotation_angle
        self.contrast_factor = contrast_factor
        self.brightness_factor = brightness_factor
        self.jpeg_quality = jpeg_quality
        self.salt_pepper_amount = salt_pepper_amount
        self.add_scan_lines = add_scan_lines


# Define the 4 quality profiles
QUALITY_PROFILES = {
    'Q1_Poor': DegradationProfile(
        name='Q1_Poor',
        blur_radius=0.5,
        noise_intensity=0.02,
        rotation_angle=0.5,
        contrast_factor=0.95,
        brightness_factor=0.98,
        jpeg_quality=90,
        salt_pepper_amount=0.0,
        add_scan_lines=False
    ),
    'Q2_MediumPoor': DegradationProfile(
        name='Q2_MediumPoor',
        blur_radius=1.2,
        noise_intensity=0.05,
        rotation_angle=1.5,
        contrast_factor=0.85,
        brightness_factor=0.92,
        jpeg_quality=70,
        salt_pepper_amount=0.001,
        add_scan_lines=True
    ),
    'Q3_Low': DegradationProfile(
        name='Q3_Low',
        blur_radius=2.0,
        noise_intensity=0.10,
        rotation_angle=2.5,
        contrast_factor=0.75,
        brightness_factor=0.85,
        jpeg_quality=50,
        salt_pepper_amount=0.003,
        add_scan_lines=True
    ),
    'Q4_VeryLow': DegradationProfile(
        name='Q4_VeryLow',
        blur_radius=3.0,
        noise_intensity=0.15,
        rotation_angle=3.5,
        contrast_factor=0.65,
        brightness_factor=0.80,
        jpeg_quality=35,
        salt_pepper_amount=0.006,
        add_scan_lines=True
    )
}


def add_gaussian_noise(image: Image.Image, intensity: float) -> Image.Image:
    """Add Gaussian noise to simulate sensor noise."""
    if intensity <= 0:
        return image

    img_array = np.array(image, dtype=np.float32)
    noise = np.random.normal(0, intensity * 255, img_array.shape)
    noisy_img = np.clip(img_array + noise, 0, 255).astype(np.uint8)

    return Image.fromarray(noisy_img)


def add_salt_pepper_noise(image: Image.Image, amount: float) -> Image.Image:
    """Add salt-and-pepper noise (random black/white pixels)."""
    if amount <= 0:
        return image

    img_array = np.array(image)

    # Salt (white pixels)
    num_salt = int(amount * img_array.size * 0.5)
    coords = [np.random.randint(0, i - 1, num_salt) for i in img_array.shape]
    img_array[coords[0], coords[1]] = 255

    # Pepper (black pixels)
    num_pepper = int(amount * img_array.size * 0.5)
    coords = [np.random.randint(0, i - 1, num_pepper) for i in img_array.shape]
    img_array[coords[0], coords[1]] = 0

    return Image.fromarray(img_array)


def add_scan_lines(image: Image.Image) -> Image.Image:
    """Add horizontal scan lines to simulate scanner artifacts."""
    draw = ImageDraw.Draw(image)
    width, height = image.size

    # Add subtle horizontal lines every 10-15 pixels
    for y in range(0, height, random.randint(10, 15)):
        # Very faint lines
        opacity = random.randint(200, 240)
        draw.line([(0, y), (width, y)], fill=(opacity, opacity, opacity), width=1)

    return image


def apply_jpeg_compression(image: Image.Image, quality: int) -> Image.Image:
    """Apply JPEG compression artifacts."""
    import io

    # Save to bytes with specified quality
    buffer = io.BytesIO()
    image.save(buffer, format='JPEG', quality=quality)
    buffer.seek(0)

    # Reload the compressed image
    return Image.open(buffer)


def degrade_image(image: Image.Image, profile: DegradationProfile, seed: int) -> Image.Image:
    """
    Apply degradation effects to an image according to the profile.

    Args:
        image: Input PIL Image
        profile: DegradationProfile with degradation parameters
        seed: Random seed for reproducibility

    Returns:
        Degraded PIL Image
    """
    random.seed(seed)
    np.random.seed(seed)

    degraded = image.copy()

    # 1. Gaussian Blur (scanner focus issues)
    if profile.blur_radius > 0:
        degraded = degraded.filter(ImageFilter.GaussianBlur(radius=profile.blur_radius))

    # 2. Gaussian Noise (sensor noise)
    degraded = add_gaussian_noise(degraded, profile.noise_intensity)

    # 3. Salt-and-Pepper Noise (dust, scratches)
    degraded = add_salt_pepper_noise(degraded, profile.salt_pepper_amount)

    # 4. Contrast Adjustment
    if profile.contrast_factor != 1.0:
        enhancer = ImageEnhance.Contrast(degraded)
        degraded = enhancer.enhance(profile.contrast_factor)

    # 5. Brightness Adjustment
    if profile.brightness_factor != 1.0:
        enhancer = ImageEnhance.Brightness(degraded)
        degraded = enhancer.enhance(profile.brightness_factor)

    # 6. Rotation (document skew)
    if profile.rotation_angle != 0:
        degraded = degraded.rotate(
            profile.rotation_angle,
            resample=Image.BICUBIC,
            expand=False,
            fillcolor='white'
        )

    # 7. Scan Lines
    if profile.add_scan_lines:
        degraded = add_scan_lines(degraded)

    # 8. JPEG Compression (last step to compound artifacts)
    if profile.jpeg_quality < 100:
        degraded = apply_jpeg_compression(degraded, profile.jpeg_quality)

    return degraded


def process_images(
    input_dir: Path,
    output_base_dir: Path,
    image_files: list[str],
    profiles: dict[str, DegradationProfile],
    seed: int = 42
):
    """
    Process multiple images with all degradation profiles.

    Args:
        input_dir: Directory containing original images
        output_base_dir: Base directory for degraded outputs
        image_files: List of image filenames to process
        profiles: Dictionary of degradation profiles
        seed: Random seed base (will vary per image)
    """
    print(f"Image Degradation for OCR Testing")
    print(f"=" * 60)
    print(f"Input directory: {input_dir}")
    print(f"Output directory: {output_base_dir}")
    print(f"Images to process: {len(image_files)}")
    print(f"Quality profiles: {len(profiles)}")
    print(f"Total outputs: {len(image_files) * len(profiles)}")
    print(f"=" * 60)
    print()

    # Create output directories
    for profile_name in profiles.keys():
        profile_dir = output_base_dir / profile_name
        profile_dir.mkdir(parents=True, exist_ok=True)
        print(f"Created directory: {profile_dir}")

    print()

    # Process each image
    for idx, image_file in enumerate(image_files):
        input_path = input_dir / image_file

        if not input_path.exists():
            print(f"WARNING: Image not found: {input_path}")
            continue

        print(f"[{idx + 1}/{len(image_files)}] Processing: {image_file}")

        # Load original image
        try:
            original = Image.open(input_path)
            if original.mode != 'RGB':
                original = original.convert('RGB')
        except Exception as e:
            print(f"  ERROR: Failed to load image: {e}")
            continue

        # Apply each degradation profile
        for profile_name, profile in profiles.items():
            try:
                # Use different seed for each image to vary artifacts
                image_seed = seed + idx

                # Apply degradation
                degraded = degrade_image(original, profile, image_seed)

                # Save degraded image
                output_path = output_base_dir / profile_name / image_file

                # Preserve original format (jpg/png)
                if output_path.suffix.lower() in ['.jpg', '.jpeg']:
                    degraded.save(output_path, 'JPEG', quality=profile.jpeg_quality)
                else:
                    degraded.save(output_path, 'PNG')

                print(f"  ✓ {profile_name}: {output_path.name}")

            except Exception as e:
                print(f"  ERROR: Failed to process {profile_name}: {e}")

        print()

    print("=" * 60)
    print("✓ Image degradation complete!")
    print(f"Generated {len(image_files) * len(profiles)} degraded images")
    print("=" * 60)


def main():
    """Main execution function."""

    # Configuration
    script_dir = Path(__file__).parent
    project_root = script_dir.parent

    input_dir = project_root / "Fixtures" / "PRP1"
    output_base_dir = project_root / "Fixtures" / "PRP1_Degraded"

    # Images to process (page 1 only - most text content)
    image_files = [
        "222AAA-44444444442025_page-0001.jpg",
        "333BBB-44444444442025_page1.png",
        "333ccc-6666666662025_page1.png",
        "555CCC-66666662025_page1.png"
    ]

    # Process images
    process_images(
        input_dir=input_dir,
        output_base_dir=output_base_dir,
        image_files=image_files,
        profiles=QUALITY_PROFILES,
        seed=42  # Reproducible degradation
    )


if __name__ == "__main__":
    main()
