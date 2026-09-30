from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance
import random
import os
import re

def parse_requirements(file_path):
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    requirements = []
    
    # Split content by "## Ejemplo" to separate different examples
    examples = re.split(r'## Ejemplo \d+:', content)
    
    for i, example in enumerate(examples[1:], 1):  # Skip first empty part
        if not example.strip():
            continue
            
        # Extract the title and content
        lines = example.strip().split('\n')
        title = lines[0].strip()
        
        # Get the full content of this example
        doc_text = example.strip()
        
        # Generate a hash for this document
        import hashlib
        hash_line = hashlib.md5(doc_text.encode()).hexdigest().upper()
        
        requirements.append((doc_text, hash_line))
        
    return requirements

def create_base_image(text, width=800, height=1000):
    image = Image.new('RGB', (width, height), 'white')
    draw = ImageDraw.Draw(image)
    try:
        font = ImageFont.truetype("arial.ttf", 15)
    except IOError:
        font = ImageFont.load_default()
    
    draw.text((50, 50), text, fill='black', font=font)
    return image

def add_watermark(image, text):
    watermark = Image.new('RGBA', image.size, (255, 255, 255, 0))
    draw = ImageDraw.Draw(watermark)
    try:
        font = ImageFont.truetype("arial.ttf", 50)
    except IOError:
        font = ImageFont.load_default()

    bbox = draw.textbbox((0, 0), text, font=font)
    text_width = bbox[2] - bbox[0]
    text_height = bbox[3] - bbox[1]
    x = (image.width - text_width) / 2
    y = (image.height - text_height) / 2
    
    draw.text((x, y), text, font=font, fill=(255, 0, 0, 128))
    watermark = watermark.rotate(45, expand=False)
    
    img_rgba = image.convert("RGBA")
    img_watermarked = Image.alpha_composite(img_rgba, watermark)
    
    return img_watermarked.convert("RGB")

def apply_deterioration(image):
    # Apply a random selection of deterioration effects
    
    # Noise
    if random.random() > 0.5:
        noise = Image.new('L', image.size, 0)
        draw = ImageDraw.Draw(noise)
        for _ in range(int(image.width * image.height * 0.1)):
            x = random.randint(0, image.width - 1)
            y = random.randint(0, image.height - 1)
            draw.point((x, y), random.randint(0, 255))
        noise = noise.filter(ImageFilter.GaussianBlur(1.5))
        image = Image.blend(image, Image.merge('RGB', (noise, noise, noise)), 0.1)

    # Blur
    if random.random() > 0.5:
        image = image.filter(ImageFilter.GaussianBlur(radius=random.uniform(0.5, 1.5)))

    # Contrast
    if random.random() > 0.5:
        enhancer = ImageEnhance.Contrast(image)
        image = enhancer.enhance(random.uniform(0.7, 1.3))

    # Brightness
    if random.random() > 0.5:
        enhancer = ImageEnhance.Brightness(image)
        image = enhancer.enhance(random.uniform(0.8, 1.2))
        
    # Rotation
    if random.random() > 0.5:
        image = image.rotate(random.uniform(-1, 1), expand=True, fillcolor='white')

    return image

def main():
    input_file = 'fictitious_requerimientos_raw.md'
    output_dir = 'Fixtures'
    
    if not os.path.exists(output_dir):
        os.makedirs(output_dir)
        
    requirements = parse_requirements(input_file)
    
    for i, (req_text, req_hash) in enumerate(requirements):
        base_image = create_base_image(req_text)
        watermarked_image = add_watermark(base_image, req_hash)
        deteriorated_image = apply_deterioration(watermarked_image)
        
        output_filename = f"Fixtures{i+1:03d}"
        png_path = os.path.join(output_dir, f"{output_filename}.png")
        pdf_path = os.path.join(output_dir, f"{output_filename}.pdf")
        
        deteriorated_image.save(png_path, 'PNG')
        deteriorated_image.save(pdf_path, 'PDF', resolution=100.0)
        
        print(f"Generated {png_path} and {pdf_path}")

if __name__ == "__main__":
    main()
