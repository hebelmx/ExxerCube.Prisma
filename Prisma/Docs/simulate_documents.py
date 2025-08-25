from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance
import random
import os
import re

def parse_requirements(file_path):
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    requirements = []
    req_pattern = re.compile(r'<--Start Requirment--->\n\*\*requirment\*\*\n(.*?)\n\*\*Hash\*\*\n(.*?)\n<--End Requirment--->', re.DOTALL)
    matches = req_pattern.findall(content)
    
    for req_text, req_hash in matches:
        requirements.append((req_text.strip(), req_hash.strip()))
        
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
    # Create a transparent layer for the watermark
    watermark_layer = Image.new('RGBA', image.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(watermark_layer)
    
    try:
        font = ImageFont.truetype("arial.ttf", 50)
    except IOError:
        font = ImageFont.load_default()

    # Get text size
    bbox = draw.textbbox((0, 0), text, font=font)
    text_width = bbox[2] - bbox[0]
    text_height = bbox[3] - bbox[1]

    # Create a separate image for the text
    text_image = Image.new('RGBA', (text_width, text_height), (0, 0, 0, 0))
    text_draw = ImageDraw.Draw(text_image)
    text_draw.text((0, 0), text, font=font, fill=(255, 0, 0, 128))

    # Rotate the text image
    rotated_text = text_image.rotate(45, expand=True, resample=Image.BICUBIC)

    # Paste the rotated text onto the watermark layer
    x = (image.width - rotated_text.width) // 2
    y = (image.height - rotated_text.height) // 2
    watermark_layer.paste(rotated_text, (x, y), rotated_text)

    # Composite the watermark with the original image
    img_rgba = image.convert("RGBA")
    img_watermarked = Image.alpha_composite(img_rgba, watermark_layer)
    
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
    input_file = 'corpus_requerimientos.md'
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
