"""
Generate Degradation Spectrum for Cluster-Based Strategy

Creates intermediate quality levels between Pristine (Q0) and Q2 to build
a comprehensive degradation spectrum for document clustering and filter optimization.

Quality Spectrum:
- Q0_Pristine: Original document (no degradation)
- Q0.5_VeryGood: Minimal degradation (between pristine and Q1)
- Q1_Poor: Light degradation (existing from original script)
- Q1.5_Medium: Moderate degradation (between Q1 and Q2)
- Q2_MediumPoor: Heavy degradation (existing from original script)

This spectrum enables:
1. Performance matrix: measure OCR quality degradation curve
2. Document clustering: group by degradation sensitivity
3. Specialized filter optimization: target specific quality ranges

Usage:
    python generate_degradation_spectrum.py
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


def interpolate_profile(profile1: DegradationProfile, profile2: DegradationProfile,
                        alpha: float, name: str) -> DegradationProfile:
    """
    Linearly interpolate between two degradation profiles.

    Args:
        profile1: First profile (less degraded)
        profile2: Second profile (more degraded)
        alpha: Interpolation factor (0.0 = profile1, 1.0 = profile2)
        name: Name for the new profile

    Returns:
        Interpolated DegradationProfile
    """
    return DegradationProfile(
        name=name,
        blur_radius=profile1.blur_radius * (1 - alpha) + profile2.blur_radius * alpha,
        noise_intensity=profile1.noise_intensity * (1 - alpha) + profile2.noise_intensity * alpha,
        rotation_angle=profile1.rotation_angle * (1 - alpha) + profile2.rotation_angle * alpha,
        contrast_factor=profile1.contrast_factor * (1 - alpha) + profile2.contrast_factor * alpha,
        brightness_factor=profile1.brightness_factor * (1 - alpha) + profile2.brightness_factor * alpha,
        jpeg_quality=int(profile1.jpeg_quality * (1 - alpha) + profile2.jpeg_quality * alpha),
        salt_pepper_amount=profile1.salt_pepper_amount * (1 - alpha) + profile2.salt_pepper_amount * alpha,
        add_scan_lines=profile2.add_scan_lines  # Binary decision - use more degraded setting
    )


# Define anchor profiles (Q0, Q1, Q2)
Q0_PRISTINE = DegradationProfile(
    name='Q0_Pristine',
    blur_radius=0.0,
    noise_intensity=0.0,
    rotation_angle=0.0,
    contrast_factor=1.0,
    brightness_factor=1.0,
    jpeg_quality=100,
    salt_pepper_amount=0.0,
    add_scan_lines=False
)

Q1_POOR = DegradationProfile(
    name='Q1_Poor',
    blur_radius=0.5,
    noise_intensity=0.02,
    rotation_angle=0.5,
    contrast_factor=0.95,
    brightness_factor=0.98,
    jpeg_quality=90,
    salt_pepper_amount=0.0,
    add_scan_lines=False
)

Q2_MEDIUMPOOR = DegradationProfile(
    name='Q2_MediumPoor',
    blur_radius=1.2,
    noise_intensity=0.05,
    rotation_angle=1.5,
    contrast_factor=0.85,
    brightness_factor=0.92,
    jpeg_quality=70,
    salt_pepper_amount=0.001,
    add_scan_lines=True
)

# Generate intermediate profiles
Q05_VERYGOOD = interpolate_profile(Q0_PRISTINE, Q1_POOR, 0.5, 'Q05_VeryGood')
Q15_MEDIUM = interpolate_profile(Q1_POOR, Q2_MEDIUMPOOR, 0.5, 'Q15_Medium')

# Complete spectrum (ordered from best to worst)
SPECTRUM_PROFILES = {
    'Q0_Pristine': Q0_PRISTINE,
    'Q05_VeryGood': Q05_VERYGOOD,
    'Q1_Poor': Q1_POOR,
    'Q15_Medium': Q15_MEDIUM,
    'Q2_MediumPoor': Q2_MEDIUMPOOR
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

    # Q0_Pristine: return copy without any degradation
    if profile.name == 'Q0_Pristine':
        return image.copy()

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


def print_spectrum_summary():
    """Print summary of degradation spectrum."""
    print()
    print("=" * 80)
    print("DEGRADATION SPECTRUM SUMMARY")
    print("=" * 80)
    print()
    print(f"{'Level':<15} {'Blur':<8} {'Noise':<8} {'Contrast':<10} {'JPEG':<6} {'Scan Lines'}")
    print("-" * 80)

    for name, profile in SPECTRUM_PROFILES.items():
        scan_lines_str = "Yes" if profile.add_scan_lines else "No"
        print(f"{name:<15} "
              f"{profile.blur_radius:<8.2f} "
              f"{profile.noise_intensity:<8.3f} "
              f"{profile.contrast_factor:<10.2f} "
              f"{profile.jpeg_quality:<6} "
              f"{scan_lines_str}")

    print("=" * 80)
    print()


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
    print(f"Degradation Spectrum Generation")
    print(f"=" * 80)
    print(f"Input directory: {input_dir}")
    print(f"Output directory: {output_base_dir}")
    print(f"Images to process: {len(image_files)}")
    print(f"Quality levels: {len(profiles)}")
    print(f"Total outputs: {len(image_files) * len(profiles)}")
    print(f"=" * 80)
    print()

    print_spectrum_summary()

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

    print("=" * 80)
    print("✓ Degradation spectrum generation complete!")
    print(f"Generated {len(image_files) * len(profiles)} images across {len(profiles)} quality levels")
    print("=" * 80)


def main():
    """Main execution function."""

    # Configuration
    script_dir = Path(__file__).parent
    project_root = script_dir.parent

    input_dir = project_root / "Fixtures" / "PRP1"
    output_base_dir = project_root / "Fixtures" / "PRP1_Spectrum"

    # Images to process (page 1 only - most text content)
    image_files = [
        "222AAA-44444444442025_page-0001.jpg",
        "333BBB-44444444442025_page1.png",
        "333ccc-6666666662025_page1.png",
        "555CCC-66666662025_page1.png"
    ]

    # Process images with spectrum profiles
    process_images(
        input_dir=input_dir,
        output_base_dir=output_base_dir,
        image_files=image_files,
        profiles=SPECTRUM_PROFILES,
        seed=42  # Reproducible degradation
    )

    print()
    print("NEXT STEPS:")
    print("1. Run OCR on all spectrum images to build performance matrix")
    print("2. Analyze degradation sensitivity per document")
    print("3. Cluster documents by sensitivity patterns")
    print("4. Launch specialized NSGA-II for each cluster")


if __name__ == "__main__":
    main()
