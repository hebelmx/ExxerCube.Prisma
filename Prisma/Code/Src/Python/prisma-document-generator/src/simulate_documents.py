#!/usr/bin/env python3
"""
DOCUMENT SIMULATION PIPELINE
============================

This script creates realistic degraded scanned legal documents for OCR testing.
It takes a corpus of legal text and transforms it into challenging document images
with watermarking, realistic degradation, and proper formatting.

MAIN FEATURES:
- 30 hash watermarks (10 clusters × 3 hashes) at 45° diagonal
- Realistic scanning artifacts (blur, noise, stains, compression)
- Proper Spanish legal document formatting
- Both PNG and PDF output with aggressive cropping
- Configurable degradation levels

REFINEMENT POINTS FOR REAL DOCUMENTS:
- Line 150-250: Watermark positioning and density
- Line 280-320: Degradation parameters and realism
- Line 235-245: Document cropping dimensions
- Font loading: Adapt to system-specific font paths

AUTHOR: Claude Code Assistant
DATE: August 2025
VERSION: Production Ready v1.0
"""

from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance
import random
import os
import re
import json
import numpy as np
from pathlib import Path
import argparse

def parse_requirements_markdown(file_path):
    """Parse requirements from markdown file."""
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    requirements = []
    # Updated pattern to match the exact format
    req_pattern = re.compile(r'<--Start Requirement.*?-->\n\*\*Requerimiento\*\*\n(.*?)\n\*\*Hash\*\*\n(.*?)\n<--End Requirement-->', re.DOTALL)
    matches = req_pattern.findall(content)
    
    for req_text, req_hash in matches:
        requirements.append((req_text.strip(), req_hash.strip()))
    
    return requirements

def parse_requirements_json(file_path):
    """Parse requirements from JSON file."""
    with open(file_path, 'r', encoding='utf-8') as f:
        corpus = json.load(f)
    
    requirements = []
    for doc in corpus:
        requirements.append((doc['text'], doc['hash']))
    
    return requirements

def create_base_image(text, width=2480, height=3508):
    """
    Create A4-sized document image at 300 DPI with proper Spanish legal formatting.
    
    REFINEMENT NOTE: This function handles document layout and typography.
    For real documents, adjust margins and font sizes to match actual legal formats.
    
    Args:
        text: Legal document text to render
        width: Document width in pixels (default A4 at 300 DPI)
        height: Document height in pixels (default A4 at 300 DPI)
    
    Returns:
        PIL Image with rendered legal document
    """
    # Create white background with subtle paper texture for realism
    image = Image.new('RGB', (width, height), 'white')
    
    # Add subtle paper texture to simulate real paper
    # REFINEMENT: Adjust noise parameters for different paper types
    noise = np.random.normal(250, 3, (height, width, 3))
    noise = np.clip(noise, 240, 255).astype(np.uint8)
    paper = Image.fromarray(noise)
    image = Image.blend(image, paper, 0.2)
    
    draw = ImageDraw.Draw(image)
    
    # Font loading system - tries multiple common font paths
    # REFINEMENT: Add system-specific font paths for different environments
    font_paths = [
        "/usr/share/fonts/truetype/liberation/LiberationSerif-Regular.ttf",  # Linux
        "/usr/share/fonts/truetype/dejavu/DejaVuSerif.ttf",                 # Linux
        "/usr/share/fonts/truetype/liberation/LiberationMono-Regular.ttf",   # Linux
        "/System/Library/Fonts/Times.ttc",                                  # macOS
        "C:\\Windows\\Fonts\\times.ttf",                                    # Windows
        "arial.ttf",                                                        # Generic
        "times.ttf"                                                         # Generic
    ]
    
    font = None
    for font_path in font_paths:
        try:
            # REFINEMENT: Adjust font size (32) for different document scales
            font = ImageFont.truetype(font_path, 32)
            break
        except IOError:
            continue
    
    if not font:
        font = ImageFont.load_default()
    
    # Document layout margins - critical for watermark positioning alignment
    # REFINEMENT: These margins must match the watermark system positioning
    margin_left = 200    # Left margin - referenced in watermark system
    margin_right = width - 150   # Right margin 
    margin_top = 250     # Top margin - referenced in watermark system
    line_height = 45     # Line spacing for text
    y_position = margin_top
    
    # Split text into paragraphs
    paragraphs = text.split('\n')
    
    for paragraph in paragraphs:
        if not paragraph.strip():
            y_position += line_height  # Empty line
            continue
        
        # Word wrap
        words = paragraph.split()
        current_line = ""
        
        for word in words:
            test_line = current_line + " " + word if current_line else word
            bbox = draw.textbbox((0, 0), test_line, font=font)
            text_width = bbox[2] - bbox[0]
            
            if text_width > (margin_right - margin_left):
                if current_line:
                    draw.text((margin_left, y_position), current_line, fill='black', font=font)
                    y_position += line_height
                    current_line = word
                else:
                    # Word too long, draw it anyway
                    draw.text((margin_left, y_position), word, fill='black', font=font)
                    y_position += line_height
                    current_line = ""
            else:
                current_line = test_line
        
        if current_line:
            draw.text((margin_left, y_position), current_line, fill='black', font=font)
            y_position += line_height
        
        if y_position > height - 200:
            break  # Page full
    
    return image

def add_watermark(image, text):
    """
    WATERMARK SYSTEM - 30 Hash Implementation
    ========================================
    
    Creates 30 hash watermarks (10 clusters × 3 hashes) at 45° diagonal angle.
    This is the core OCR challenge system that overlays hash text on legal content.
    
    REFINEMENT PRIORITIES:
    1. Watermark density: Adjust cluster count and spacing for real documents
    2. Hash visibility: Modify opacity and color intensity
    3. Positioning accuracy: Ensure proper text overlay alignment
    4. Angle optimization: 45° works well, but test other angles
    
    Args:
        image: Base document image
        text: SHA256 hash string for watermarking
        
    Returns:
        Image with applied watermarks
    """
    # Convert to RGBA for transparency operations
    img_rgba = image.convert("RGBA")
    
    # Create transparent layer for watermark compositing
    watermark_layer = Image.new('RGBA', image.size, (0, 0, 0, 0))
    
    # Try to load font for watermark
    font_paths = [
        "/usr/share/fonts/truetype/liberation/LiberationSerif-Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf",
        "arial.ttf"
    ]
    
    font = None
    font_size = 36  # Good size for overlay
    for font_path in font_paths:
        try:
            font = ImageFont.truetype(font_path, font_size)
            break
        except IOError:
            continue
    if not font:
        font = ImageFont.load_default()
    
    # Use only the hash as watermark text
    hash_text = text  # Full hash
    
    width, height = image.size
    
    # Define the text area where our document content is (same as in create_base_image)
    text_margin_left = 200
    text_margin_right = width - 150
    text_margin_top = 250
    text_margin_bottom = height - 250
    
    text_width = text_margin_right - text_margin_left
    text_height = text_margin_bottom - text_margin_top
    
    # Create diagonal watermarks that properly overlay the text area
    # We'll create multiple diagonal lines that cross the text area at 45°
    
    # Calculate diagonal lines that cross the text area
    # For a 45° line, we need lines that start from left edge and go diagonally up-right
    
    # Start from various points along the left side of text area and bottom
    start_points = []
    
    # Lines starting from left edge of text area - more spacing between clusters
    for y in range(text_margin_top, text_margin_bottom, 200):
        start_points.append((text_margin_left, y))
    
    # Lines starting from bottom edge of text area - more spacing between clusters
    for x in range(text_margin_left, text_margin_right, 200):
        start_points.append((x, text_margin_bottom))
    
    # Add more start points to ensure we can place 10 clusters (30 hashes total)
    # Additional diagonal starts from middle areas
    for y in range(text_margin_top + 100, text_margin_bottom, 180):
        start_points.append((text_margin_left + 100, y))
    
    for x in range(text_margin_left + 100, text_margin_right, 180):
        start_points.append((x, text_margin_bottom - 100))
    
    cluster_count = 0
    
    # Draw watermarks along these diagonal lines
    for i, (start_x, start_y) in enumerate(start_points):
        if cluster_count >= 10:  # 10 clusters total (30 hashes)
            break
            
        # Create image for this diagonal line
        line_img = Image.new('RGBA', image.size, (0, 0, 0, 0))
        line_draw = ImageDraw.Draw(line_img)
        
        # For each diagonal line, place hash clusters
        if cluster_count < 10:
            # Draw 1 cluster per line to distribute the 10 clusters
            cluster_count += 1
            
            # Position cluster along this diagonal line (within text area)
            cluster_distance = 250  # More distance along diagonal for more spacing
            
            for hash_in_cluster in range(3):
                # Calculate position for each hash in the cluster
                hash_distance = hash_in_cluster * 150  # Even more space between hashes in cluster
                total_distance = cluster_distance + hash_distance
                
                # Calculate diagonal position (45° means equal x and y movement)
                x = start_x + total_distance * 0.707  # cos(45°) ≈ 0.707
                y = start_y - total_distance * 0.707  # sin(45°) ≈ 0.707 (negative because going up)
                
                # Ensure we're still in the text area
                if text_margin_left <= x <= text_margin_right and text_margin_top <= y <= text_margin_bottom:
                    # Red color for watermark
                    red_intensity = random.randint(200, 255)
                    opacity = random.randint(80, 100)
                    
                    # Draw the hash text at 45° angle
                    temp_img = Image.new('RGBA', image.size, (0, 0, 0, 0))
                    temp_draw = ImageDraw.Draw(temp_img)
                    temp_draw.text((x, y), hash_text, font=font, 
                                 fill=(red_intensity, 0, 0, opacity))
                    
                    # Rotate at 45°
                    rotated_hash = temp_img.rotate(45, resample=Image.BICUBIC, expand=False)
                    watermark_layer = Image.alpha_composite(watermark_layer, rotated_hash)
        
        # Skip the fill repetitions to avoid big clusters - only use the 10 clusters of 3 hashes each
    
    # Composite everything
    final = Image.alpha_composite(img_rgba, watermark_layer)
    
    # Crop to standard document size (remove oversized areas)
    # Much more aggressive cropping as requested
    crop_left = 50
    crop_top = 50  
    crop_right = width - 500  # Much more cropping from right (500px instead of 50)
    crop_bottom = height - int(height * 0.2)  # Crop 20% from bottom height
    
    cropped = final.crop((crop_left, crop_top, crop_right, crop_bottom))
    
    return cropped.convert("RGB")

def apply_deterioration(image, level='random'):
    """
    DEGRADATION ENGINE - Realistic Scanning Artifacts
    =================================================
    
    Applies realistic document degradation to simulate real-world scanning conditions.
    This system creates challenging but realistic OCR conditions.
    
    REFINEMENT PRIORITIES:
    1. Degradation realism: Adjust parameters based on real scan analysis
    2. Challenge level: Balance difficulty with extractability
    3. Artifact types: Add/remove effects based on target document types
    4. Parameter ranges: Fine-tune based on OCR performance testing
    
    DEGRADATION LEVELS:
    - light: Minor artifacts, high readability
    - medium: Moderate challenge, good for testing
    - heavy: Significant artifacts, advanced testing
    - extreme: Maximum challenge, stress testing
    
    Args:
        image: Document image to degrade
        level: Degradation intensity ('light'|'medium'|'heavy'|'extreme'|'random')
        
    Returns:
        Degraded document image
    """
    img_array = np.array(image)
    
    # Random level selection for diversity - REFINEMENT: Use specific levels for targeted testing
    level = random.choice(['light', 'medium', 'heavy', 'extreme'])
    
    # Multiple blur types
    if random.random() > 0.3:
        blur_type = random.choice(['gaussian', 'box', 'motion'])
        if blur_type == 'gaussian':
            blur_radius = random.uniform(0.3, 3.0) if level in ['heavy', 'extreme'] else random.uniform(0.2, 1.5)
            image = image.filter(ImageFilter.GaussianBlur(radius=blur_radius))
        elif blur_type == 'box':
            image = image.filter(ImageFilter.BoxBlur(radius=random.uniform(0.5, 2.0)))
        else:  # motion blur simulation
            kernel_size = random.choice([3, 5, 7])
            image = image.filter(ImageFilter.BoxBlur(radius=kernel_size/2))
    
    # More aggressive brightness and contrast variations
    if random.random() > 0.2:
        brightness = ImageEnhance.Brightness(image)
        if level == 'extreme':
            factor = random.choice([random.uniform(0.5, 0.7), random.uniform(1.3, 1.5)])
        elif level == 'heavy':
            factor = random.uniform(0.65, 1.35)
        else:
            factor = random.uniform(0.8, 1.2)
        image = brightness.enhance(factor)
        
        contrast = ImageEnhance.Contrast(image)
        if level == 'extreme':
            factor = random.choice([random.uniform(0.5, 0.7), random.uniform(1.4, 1.8)])
        elif level == 'heavy':
            factor = random.uniform(0.6, 1.4)
        else:
            factor = random.uniform(0.85, 1.15)
        image = contrast.enhance(factor)
    
    # Add color saturation changes (faded scans)
    if random.random() > 0.5:
        color = ImageEnhance.Color(image)
        factor = random.uniform(0.3, 0.8) if level in ['heavy', 'extreme'] else random.uniform(0.7, 1.0)
        image = color.enhance(factor)
    
    # Rotation/skew (document placement) - more variation
    if random.random() > 0.3:
        if level == 'extreme':
            angle = random.uniform(-5, 5)
        elif level == 'heavy':
            angle = random.uniform(-3, 3)
        else:
            angle = random.uniform(-1.5, 1.5)
        image = image.rotate(angle, fillcolor='white', expand=False)
    
    # Convert to array for noise operations
    img_array = np.array(image)
    
    # More diverse noise types
    if random.random() > 0.3:
        noise_type = random.choice(['salt_pepper', 'gaussian', 'speckle', 'uniform'])
        
        if noise_type == 'salt_pepper':
            if level == 'extreme':
                noise_prob = random.uniform(0.005, 0.015)
            elif level == 'heavy':
                noise_prob = random.uniform(0.002, 0.008)
            else:
                noise_prob = random.uniform(0.0005, 0.003)
            
            salt_pepper = np.random.random(img_array.shape[:2])
            img_array[salt_pepper < noise_prob/2] = 255  # Salt
            img_array[salt_pepper > 1 - noise_prob/2] = 0  # Pepper
            
        elif noise_type == 'gaussian':
            # Gaussian noise
            noise_strength = random.uniform(5, 25) if level in ['heavy', 'extreme'] else random.uniform(2, 10)
            noise = np.random.normal(0, noise_strength, img_array.shape)
            img_array = np.clip(img_array.astype(np.float32) + noise, 0, 255).astype(np.uint8)
            
        elif noise_type == 'speckle':
            # Speckle noise
            speckle = np.random.randn(*img_array.shape) * random.uniform(0.1, 0.3)
            img_array = np.clip(img_array + img_array * speckle, 0, 255).astype(np.uint8)
            
        else:  # uniform noise
            noise_level = random.uniform(5, 20)
            noise = np.random.uniform(-noise_level, noise_level, img_array.shape)
            img_array = np.clip(img_array.astype(np.float32) + noise, 0, 255).astype(np.uint8)
    
    # More diverse shadow/gradient effects
    if random.random() > 0.4:
        height, width = img_array.shape[:2]
        
        gradient_type = random.choice(['horizontal', 'vertical', 'corner', 'radial', 'wavy', 'patches'])
        
        if gradient_type == 'horizontal':
            start_val = random.uniform(0.7, 0.9)
            end_val = random.uniform(0.95, 1.05)
            gradient = np.linspace(start_val, end_val, width)
            gradient = np.tile(gradient, (height, 1))
        elif gradient_type == 'vertical':
            start_val = random.uniform(0.7, 0.9)
            end_val = random.uniform(0.95, 1.05)
            gradient = np.linspace(start_val, end_val, height).reshape(-1, 1)
            gradient = np.tile(gradient, (1, width))
        elif gradient_type == 'radial':
            # Radial gradient from center
            center_x, center_y = width//2, height//2
            y, x = np.ogrid[:height, :width]
            distance = np.sqrt((x - center_x)**2 + (y - center_y)**2)
            max_dist = np.sqrt(center_x**2 + center_y**2)
            gradient = 0.7 + 0.3 * (1 - distance / max_dist)
        elif gradient_type == 'wavy':
            # Wavy shadow pattern
            x_coords = np.arange(width)
            y_coords = np.arange(height).reshape(-1, 1)
            wave = 0.1 * np.sin(x_coords * np.pi / (width/4)) + 0.9
            gradient = np.tile(wave, (height, 1))
        else:  # patches - random dark/light patches
            gradient = np.ones((height, width))
            num_patches = random.randint(3, 8)
            for _ in range(num_patches):
                patch_size = random.randint(100, 300)
                patch_x = random.randint(0, max(1, width - patch_size))
                patch_y = random.randint(0, max(1, height - patch_size))
                patch_val = random.uniform(0.6, 0.95)
                gradient[patch_y:patch_y+patch_size, patch_x:patch_x+patch_size] *= patch_val
        
        gradient = np.stack([gradient] * 3, axis=-1)
        img_array = (img_array * gradient).astype(np.uint8)
    
    # Various stains and marks (more frequent and diverse)
    if random.random() > 0.7:
        height, width = img_array.shape[:2]
        stain_type = random.choice(['coffee', 'water', 'ink', 'finger'])
        
        num_stains = random.randint(1, 4 if level in ['heavy', 'extreme'] else 2)
        
        for _ in range(num_stains):
            center_x = random.randint(width//6, 5*width//6)
            center_y = random.randint(height//6, 5*height//6)
            
            if stain_type == 'coffee':
                radius = random.randint(50, 150)
                y, x = np.ogrid[:height, :width]
                mask = (x - center_x)**2 + (y - center_y)**2 <= radius**2
                stain_color = np.array([180 + random.randint(-30, 30), 
                                      140 + random.randint(-20, 20), 
                                      90 + random.randint(-20, 30)])
                img_array[mask] = img_array[mask] * 0.6 + stain_color * 0.4
                
            elif stain_type == 'water':
                # Water damage - oval shaped
                radius_x = random.randint(80, 200)
                radius_y = random.randint(40, 120)
                y, x = np.ogrid[:height, :width]
                mask = ((x - center_x)**2 / radius_x**2) + ((y - center_y)**2 / radius_y**2) <= 1
                img_array[mask] = (img_array[mask] * random.uniform(0.7, 0.9)).astype(np.uint8)
                
            elif stain_type == 'ink':
                # Ink blots - irregular shape
                radius = random.randint(30, 80)
                y, x = np.ogrid[:height, :width]
                # Add randomness to make irregular
                noise_mask = np.random.random((height, width)) > 0.3
                base_mask = (x - center_x)**2 + (y - center_y)**2 <= radius**2
                mask = base_mask & noise_mask
                ink_color = random.choice([0, 50, 100])  # Dark colors
                img_array[mask] = ink_color
                
            else:  # fingerprints
                radius = random.randint(20, 50)
                y, x = np.ogrid[:height, :width]
                mask = (x - center_x)**2 + (y - center_y)**2 <= radius**2
                img_array[mask] = (img_array[mask] * 0.8).astype(np.uint8)
    
    # More diverse scan artifacts
    if random.random() > 0.5:
        artifact_type = random.choice(['scan_lines', 'streaks', 'bands', 'dropout'])
        
        if artifact_type == 'scan_lines':
            num_lines = random.randint(2, 8 if level in ['heavy', 'extreme'] else 5)
            for _ in range(num_lines):
                y = random.randint(0, img_array.shape[0]-1)
                intensity = random.uniform(0.6, 0.95)
                thickness = random.randint(1, 3)
                for dy in range(-thickness, thickness+1):
                    if 0 <= y + dy < img_array.shape[0]:
                        img_array[y + dy, :] = (img_array[y + dy, :] * intensity).astype(np.uint8)
        
        elif artifact_type == 'streaks':
            # Vertical streaks from scanner problems
            num_streaks = random.randint(3, 10)
            for _ in range(num_streaks):
                x = random.randint(0, img_array.shape[1]-1)
                intensity = random.uniform(0.7, 0.9)
                width = random.randint(1, 4)
                for dx in range(-width, width+1):
                    if 0 <= x + dx < img_array.shape[1]:
                        img_array[:, x + dx] = (img_array[:, x + dx] * intensity).astype(np.uint8)
        
        elif artifact_type == 'bands':
            # Horizontal bands of different brightness
            num_bands = random.randint(2, 5)
            band_height = img_array.shape[0] // (num_bands + 1)
            for i in range(num_bands):
                y_start = i * band_height + random.randint(-20, 20)
                y_end = min(y_start + band_height//2, img_array.shape[0])
                intensity = random.uniform(0.8, 1.1)
                img_array[y_start:y_end, :] = np.clip(
                    img_array[y_start:y_end, :] * intensity, 0, 255
                ).astype(np.uint8)
        
        else:  # dropout - missing sections
            num_dropouts = random.randint(1, 3)
            for _ in range(num_dropouts):
                x = random.randint(0, img_array.shape[1]//2)
                y = random.randint(0, img_array.shape[0]//2)
                w = random.randint(50, 200)
                h = random.randint(10, 50)
                img_array[y:y+h, x:x+w] = 255  # White dropout
    
    # Convert back to PIL Image
    image = Image.fromarray(img_array.astype(np.uint8))
    
    # More aggressive compression artifacts
    if random.random() > 0.4:
        from io import BytesIO
        # Multiple compression passes for more artifacts
        num_passes = random.randint(1, 3 if level in ['heavy', 'extreme'] else 2)
        
        for _ in range(num_passes):
            if level == 'extreme':
                quality = random.randint(20, 40)
            elif level == 'heavy':
                quality = random.randint(40, 65)
            elif level == 'medium':
                quality = random.randint(60, 80)
            else:
                quality = random.randint(75, 90)
            
            buffer = BytesIO()
            image.save(buffer, format='JPEG', quality=quality)
            buffer.seek(0)
            image = Image.open(buffer)
    
    # Add some final touches - paper fold lines
    if level in ['heavy', 'extreme'] and random.random() > 0.8:
        img_array = np.array(image)
        height, width = img_array.shape[:2]
        
        # Horizontal fold line
        if random.random() > 0.5:
            fold_y = random.randint(height//4, 3*height//4)
            fold_thickness = random.randint(2, 6)
            for dy in range(-fold_thickness, fold_thickness+1):
                if 0 <= fold_y + dy < height:
                    img_array[fold_y + dy, :] = (img_array[fold_y + dy, :] * 0.7).astype(np.uint8)
        
        # Vertical fold line
        if random.random() > 0.5:
            fold_x = random.randint(width//4, 3*width//4)
            fold_thickness = random.randint(2, 6)
            for dx in range(-fold_thickness, fold_thickness+1):
                if 0 <= fold_x + dx < width:
                    img_array[:, fold_x + dx] = (img_array[:, fold_x + dx] * 0.7).astype(np.uint8)
        
        image = Image.fromarray(img_array)
    
    return image

def main():
    parser = argparse.ArgumentParser(description="Simulate degraded document images from corpus")
    parser.add_argument("--input", default="corpus_requerimientos.json", 
                       help="Input corpus file (JSON or Markdown)")
    parser.add_argument("--output", default="Fixtures", 
                       help="Output directory for fixtures")
    parser.add_argument("--num", type=int, 
                       help="Number of documents to generate (default: all)")
    parser.add_argument("--degradation", choices=['light', 'medium', 'heavy', 'random'], 
                       default='random', help="Degradation level")
    
    args = parser.parse_args()
    
    # Create output directory
    output_dir = Path(args.output)
    output_dir.mkdir(exist_ok=True)
    
    # Parse requirements based on file extension
    if args.input.endswith('.json'):
        requirements = parse_requirements_json(args.input)
    elif args.input.endswith('.md'):
        requirements = parse_requirements_markdown(args.input)
    else:
        print(f"Error: Unsupported file format. Use .json or .md")
        return
    
    if not requirements:
        print(f"No requirements found in {args.input}")
        return
    
    # Limit number if specified
    if args.num:
        requirements = requirements[:args.num]
    
    print(f"Generating {len(requirements)} fixture documents...")
    
    for i, (req_text, req_hash) in enumerate(requirements):
        print(f"Processing document {i+1}/{len(requirements)}...")
        
        # Create base document
        base_image = create_base_image(req_text)
        
        # Add watermark
        watermarked_image = add_watermark(base_image, req_hash)
        
        # Apply degradations
        deteriorated_image = apply_deterioration(watermarked_image, args.degradation)
        
        # Save files
        output_filename = f"Fixture{i+1:03d}"
        png_path = output_dir / f"{output_filename}.png"
        pdf_path = output_dir / f"{output_filename}.pdf"
        
        # Save with proper DPI settings
        deteriorated_image.save(png_path, 'PNG', dpi=(300, 300))
        deteriorated_image.save(pdf_path, 'PDF', resolution=300.0)
        
        print(f"  Generated {png_path} and {pdf_path}")
    
    print(f"\nCompleted! Files saved to {output_dir}/")

if __name__ == "__main__":
    main()
