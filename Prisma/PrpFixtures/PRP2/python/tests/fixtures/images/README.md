# VEC Statement Test Images

Image fixtures organized by category for visual compliance testing.

## Directory Structure

```
images/
├── card_images/                    # Card images (REQ-027)
│   ├── IMAGEN_TAREJETA_Imagen1.jpeg
│   ├── IMAGEN_TAREJETA_Picture2.jpeg
│   └── ...
├── important_messages/             # Important messages (REQ-030)
│   ├── IMAGEN_MENSAJES_IMPORTANTES_Imagen1.emf
│   ├── IMAGEN_MENSAJES_IMPORTANTES_Imagen4.jpeg
│   └── ...
├── marketing_images/               # Marketing images (REQ-047)
│   ├── IMAGENES_Imagen3.png
│   ├── IMAGENES_Imagen49.png
│   └── ...
├── mandatory_legends/              # Mandatory legends (REQ-046)
│   ├── Leyendas_Obligatorios_table1.png
│   ├── Leyendas_Obligatorios_table2.png
│   └── ...
└── variants/                       # Test variants
    ├── correct/                    # Correct images (valid tests)
    ├── incorrect/                  # Incorrect images (invalid tests)
    └── corrupted/                  # Corrupted images (error tests)
```

## Image Categories

### Card Images (REQ-027)
Card images that must correspond to product type:
- Vista card image
- Recompra card image
- CEDE card image
- etc.

### Important Messages (REQ-030)
Important message banners that must match product catalog.

### Marketing Images (REQ-047)
Marketing promotional images that must appear in correct sequence.

### Mandatory Legends (REQ-046)
Regulatory mandatory legends that must be included.

## Usage in Tests

```python
from pathlib import Path

# Load correct card image
card_image = Path("images/card_images/IMAGEN_TAREJETA_Imagen1.jpeg")

# Use in test case
test_case = {
    "use_correct_logo": True,
    "card_image_path": str(card_image),
}
```

## Test Variants

### Correct Images
For valid test cases where all visual compliance checks pass.

### Incorrect Images
For invalid test cases where visual compliance should fail:
- Wrong product card image
- Missing bank logo
- Incorrect important messages

### Corrupted Images
For error handling test cases:
- Corrupted JPEG files
- Invalid image formats
- Truncated images
