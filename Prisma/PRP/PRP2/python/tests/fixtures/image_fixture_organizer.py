"""
Image fixture organizer for VEC statement testing.

Organizes images from Check+list+demo+v2+Iqubica_images/ into categorized structure
for use in visual compliance testing.
"""

import shutil
from pathlib import Path
from typing import Dict, List


class ImageFixtureOrganizer:
    """Organize test images by category for validation testing."""

    # Image categories from validation checklist
    CATEGORIES = {
        "card_images": [
            "IMAGEN_TAREJETA_Imagen1.jpeg",
            "IMAGEN_TAREJETA_Picture2.jpeg",
            "image1.jpeg",  # Additional card image
            "image2.jpeg",  # Additional card image
        ],
        "important_messages": [
            "IMAGEN_MENSAJES_IMPORTANTES_Imagen1.emf",
            "IMAGEN_MENSAJES_IMPORTANTES_Imagen4.jpeg",
            "image3.emf",  # Additional message image
            "image4.jpeg",  # Additional message image
        ],
        "marketing_images": [
            "IMAGENES_Imagen3.png",
            "IMAGENES_Imagen49.png",
            "image7.png",  # Additional marketing image
            "image8.png",  # Additional marketing image
        ],
        "mandatory_legends": [
            "Leyendas_Obligatorios_table1.png",
            "Leyendas_Obligatorios_table2.png",
            "image5.png",  # Additional legend table
            "image6.png",  # Additional legend table
        ],
    }

    def __init__(self, source_dir: Path, target_dir: Path):
        """
        Initialize image fixture organizer.

        Args:
            source_dir: Source directory with images (Check+list+demo+v2+Iqubica_images/)
            target_dir: Target directory for organized fixtures
        """
        self.source_dir = Path(source_dir)
        self.target_dir = Path(target_dir)

    def organize_images(self) -> Dict[str, List[Path]]:
        """
        Organize images into categorized directories.

        Returns:
            Dictionary mapping categories to organized image paths
        """
        organized_images = {}

        for category, image_files in self.CATEGORIES.items():
            category_dir = self.target_dir / category
            category_dir.mkdir(parents=True, exist_ok=True)

            organized_images[category] = []

            for image_file in image_files:
                source_path = self.source_dir / image_file

                if source_path.exists():
                    target_path = category_dir / image_file
                    shutil.copy2(source_path, target_path)
                    organized_images[category].append(target_path)
                    print(f"✓ Copied {image_file} → {category}/")
                else:
                    print(f"✗ Not found: {image_file}")

        return organized_images

    def create_test_variants(self):
        """
        Create test variants for visual compliance testing.

        Creates:
        - correct/ - Correct images for valid test cases
        - incorrect/ - Incorrect images for invalid test cases
        - corrupted/ - Corrupted images for error handling tests
        """
        # Create variant directories
        variants_dir = self.target_dir / "variants"

        # Correct images (symlinks or copies of originals)
        correct_dir = variants_dir / "correct"
        correct_dir.mkdir(parents=True, exist_ok=True)

        # Incorrect images (modified for testing)
        incorrect_dir = variants_dir / "incorrect"
        incorrect_dir.mkdir(parents=True, exist_ok=True)

        # Corrupted images (for error handling)
        corrupted_dir = variants_dir / "corrupted"
        corrupted_dir.mkdir(parents=True, exist_ok=True)

        print(f"\nCreated variant directories:")
        print(f"  - {correct_dir}")
        print(f"  - {incorrect_dir}")
        print(f"  - {corrupted_dir}")

    def generate_image_manifest(self) -> Dict:
        """
        Generate manifest of available images for test cases.

        Returns:
            Dictionary with image manifest
        """
        manifest = {
            "version": "1.0",
            "categories": {},
        }

        for category, image_files in self.CATEGORIES.items():
            category_images = []

            for image_file in image_files:
                source_path = self.source_dir / image_file

                if source_path.exists():
                    category_images.append({
                        "filename": image_file,
                        "size_bytes": source_path.stat().st_size,
                        "format": source_path.suffix[1:].upper(),
                        "available": True,
                    })
                else:
                    category_images.append({
                        "filename": image_file,
                        "available": False,
                    })

            manifest["categories"][category] = {
                "total_images": len(category_images),
                "available_images": sum(1 for img in category_images if img.get("available")),
                "images": category_images,
            }

        return manifest

    def create_readme(self):
        """Create README for image fixtures."""
        readme_content = """# VEC Statement Test Images

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
"""

        readme_path = self.target_dir / "README.md"
        readme_path.write_text(readme_content, encoding="utf-8")
        print(f"\n✓ Created README: {readme_path}")


def main():
    """Main entry point for image fixture organization."""
    # Source directory (Check+list+demo+v2+Iqubica_images/)
    source_dir = Path(__file__).parent.parent.parent.parent / "Check+list+demo+v2+Iqubica_images"

    # Target directory (tests/fixtures/images/)
    target_dir = Path(__file__).parent / "images"

    print("=== VEC Image Fixture Organizer ===\n")
    print(f"Source: {source_dir}")
    print(f"Target: {target_dir}\n")

    organizer = ImageFixtureOrganizer(source_dir, target_dir)

    # Organize images
    print("Organizing images by category...\n")
    organized_images = organizer.organize_images()

    # Create test variants
    print("\nCreating test variants...")
    organizer.create_test_variants()

    # Generate manifest
    print("\nGenerating image manifest...")
    manifest = organizer.generate_image_manifest()

    # Save manifest
    import json
    manifest_path = target_dir / "image_manifest.json"
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
    print(f"✓ Saved manifest: {manifest_path}")

    # Create README
    organizer.create_readme()

    # Print summary
    print("\n=== Summary ===")
    for category, images in organized_images.items():
        print(f"{category}: {len(images)} images")

    print(f"\nTotal images organized: {sum(len(images) for images in organized_images.values())}")


if __name__ == "__main__":
    main()
