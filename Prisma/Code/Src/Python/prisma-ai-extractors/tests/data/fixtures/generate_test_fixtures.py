"""
Generate test fixtures for comprehensive testing.
"""

import json
import random
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from datetime import datetime, timedelta
import numpy as np


class TestFixtureGenerator:
    """Generate test fixtures for document extraction testing."""
    
    def __init__(self, output_dir: Path):
        self.output_dir = Path(output_dir)
        self.output_dir.mkdir(parents=True, exist_ok=True)
        
        # Spanish legal authorities
        self.authorities = [
            "Comisión Nacional Bancaria y de Valores",
            "CONDUSEF",
            "Secretaría de Hacienda y Crédito Público",
            "Banco de México",
            "Comisión Nacional del Sistema de Ahorro para el Retiro",
            "Instituto para la Protección al Ahorro Bancario",
            "Tribunal Federal de Justicia Administrativa"
        ]
        
        # Requirement types
        self.requirement_types = [
            "Requerimiento de Información",
            "Solicitud de Documentos",
            "Embargo Bancario",
            "Aseguramiento de Bienes",
            "Notificación de Multa",
            "Requerimiento de Pago",
            "Solicitud de Colaboración"
        ]
        
        # Legal foundations
        self.legal_foundations = [
            "Artículo 142 de la Ley de Instituciones de Crédito",
            "Artículo 95 de la Ley del Banco de México",
            "Artículo 21 de la Ley para Regular las Sociedades de Información Crediticia",
            "Artículo 117 de la Ley de la Comisión Nacional Bancaria y de Valores",
            "Código Fiscal de la Federación Artículo 42",
            "Ley Federal de Protección de Datos Personales Artículo 3"
        ]
    
    def generate_legal_document_data(self, variant: str = "standard") -> dict:
        """Generate realistic legal document data."""
        # Generate date within last 2 years
        start_date = datetime.now() - timedelta(days=730)
        random_days = random.randint(0, 730)
        doc_date = start_date + timedelta(days=random_days)
        
        # Generate case number
        year = doc_date.year
        case_number = f"{random.choice(['CNBV', 'CONDUSEF', 'SHCP'])}/EXP/{year}/{random.randint(1, 9999):04d}"
        
        base_data = {
            "fecha": doc_date.strftime("%Y-%m-%d"),
            "autoridadEmisora": random.choice(self.authorities),
            "expediente": case_number,
            "tipoRequerimiento": random.choice(self.requirement_types),
            "subtipoRequerimiento": self._generate_subtype(),
            "fundamentoLegal": random.choice(self.legal_foundations),
            "motivacion": self._generate_motivation(),
            "partes": self._generate_parties(),
            "detalle": self._generate_detail_data(variant)
        }
        
        # Add variant-specific modifications
        if variant == "minimal":
            # Remove optional fields
            base_data = {k: v for k, v in base_data.items() 
                        if k in ["fecha", "autoridadEmisora", "expediente"]}
        
        elif variant == "complex":
            # Add additional fields
            base_data.update({
                "numeroOficio": f"OF/{random.randint(1000, 9999)}/{year}",
                "fechaNotificacion": (doc_date + timedelta(days=random.randint(1, 30))).strftime("%Y-%m-%d"),
                "plazoRespuesta": random.choice(["10 días hábiles", "15 días hábiles", "30 días naturales"]),
                "contacto": {
                    "nombre": self._generate_contact_name(),
                    "cargo": random.choice(["Director", "Subdirector", "Jefe de Departamento"]),
                    "telefono": self._generate_phone(),
                    "email": self._generate_email()
                },
                "anexos": self._generate_attachments()
            })
        
        elif variant == "malformed":
            # Introduce common data issues
            if random.random() < 0.3:
                base_data["fecha"] = doc_date.strftime("%d/%m/%Y")  # Different format
            if random.random() < 0.2:
                base_data["autoridadEmisora"] = base_data["autoridadEmisora"].upper()
            if random.random() < 0.1:
                base_data["expediente"] = base_data["expediente"].replace("/", "-")
        
        return base_data
    
    def _generate_subtype(self) -> str:
        """Generate document subtypes."""
        subtypes = [
            "Movimientos Bancarios", "Estados Financieros", "Documentación Legal",
            "Información Crediticia", "Datos Personales", "Operaciones Sospechosas",
            "Cumplimiento Normativo", "Auditoría Interna"
        ]
        return random.choice(subtypes)
    
    def _generate_motivation(self) -> str:
        """Generate legal motivation text."""
        motivations = [
            "Investigación por posibles irregularidades en operaciones financieras",
            "Cumplimiento de disposiciones en materia de prevención de lavado de dinero",
            "Verificación del cumplimiento de obligaciones fiscales",
            "Atención a queja presentada por usuario de servicios financieros",
            "Supervisión del cumplimiento de la normatividad aplicable",
            "Proceso de fiscalización de operaciones reportables"
        ]
        return random.choice(motivations)
    
    def _generate_parties(self) -> list:
        """Generate involved parties."""
        first_names = ["Juan", "María", "Carlos", "Ana", "Luis", "Carmen", "José", "Isabel"]
        last_names = ["García", "Rodríguez", "López", "Martínez", "González", "Pérez", "Sánchez", "Ramírez"]
        
        parties = []
        num_parties = random.randint(1, 4)
        
        for _ in range(num_parties):
            if random.random() < 0.7:  # 70% individuals
                name = f"{random.choice(first_names)} {random.choice(last_names)} {random.choice(last_names)}"
                parties.append(name)
            else:  # 30% companies
                company_types = ["S.A.", "S.A. de C.V.", "S.C.", "A.C."]
                company_names = ["Banco Nacional", "Financiera", "Inversiones", "Grupo Financiero"]
                company = f"{random.choice(company_names)} {random.choice(['del Centro', 'de México', 'Internacional', 'Especializado'])} {random.choice(company_types)}"
                parties.append(company)
        
        return parties
    
    def _generate_detail_data(self, variant: str) -> dict:
        """Generate detail section data."""
        base_detail = {
            "descripcion": self._generate_description(),
            "monto": round(random.uniform(10000, 10000000), 2) if random.random() < 0.8 else None,
            "moneda": random.choice(["MXN", "USD", "EUR"]),
            "activoVirtual": "N/A"
        }
        
        if variant == "complex":
            base_detail.update({
                "periodo": {
                    "fechaInicio": (datetime.now() - timedelta(days=365)).strftime("%Y-%m-%d"),
                    "fechaFin": datetime.now().strftime("%Y-%m-%d")
                },
                "cuentas": [f"012180{random.randint(100000000, 999999999)}" for _ in range(random.randint(1, 3))],
                "documentosRequeridos": [
                    "Estados de cuenta bancarios",
                    "Comprobantes de ingresos",
                    "Declaraciones fiscales",
                    "Contratos de crédito"
                ][:random.randint(1, 4)]
            })
        
        return base_detail
    
    def _generate_contact_name(self) -> str:
        """Generate contact names."""
        titles = ["Lic.", "Dr.", "Mtro.", "Ing."]
        first_names = ["Roberto", "Patricia", "Fernando", "Claudia", "Ricardo", "Mónica"]
        last_names = ["Hernández", "Morales", "Jiménez", "Vargas", "Castro", "Ortega"]
        
        return f"{random.choice(titles)} {random.choice(first_names)} {random.choice(last_names)}"
    
    def _generate_phone(self) -> str:
        """Generate Mexican phone numbers."""
        area_codes = ["55", "81", "33", "222", "228", "999"]
        return f"+52-{random.choice(area_codes)}-{random.randint(1000000, 9999999)}"
    
    def _generate_email(self) -> str:
        """Generate institutional email addresses."""
        domains = ["cnbv.gob.mx", "condusef.gob.mx", "hacienda.gob.mx", "banxico.org.mx"]
        names = ["contacto", "atencion", "info", "notificaciones", "tramites"]
        return f"{random.choice(names)}@{random.choice(domains)}"
    
    def _generate_description(self) -> str:
        """Generate requirement descriptions."""
        descriptions = [
            "Solicitud de información sobre movimientos bancarios del periodo indicado",
            "Requerimiento de documentación para proceso de supervisión",
            "Información necesaria para investigación de operaciones irregulares",
            "Documentos requeridos para cumplimiento de normatividad vigente",
            "Datos solicitados para proceso de fiscalización",
            "Información para atención de queja de usuario de servicios financieros"
        ]
        return random.choice(descriptions)
    
    def _generate_attachments(self) -> list:
        """Generate attachment references."""
        attachments = [
            "Formato de solicitud de información",
            "Lista de documentos requeridos",
            "Marco legal aplicable",
            "Instructivo de llenado",
            "Acuse de recibo"
        ]
        return random.sample(attachments, random.randint(1, 3))
    
    def generate_document_image(
        self, 
        data: dict, 
        width: int = 1200, 
        height: int = 1600,
        add_noise: bool = False,
        add_watermarks: bool = False,
        quality: str = "high"
    ) -> Image.Image:
        """Generate realistic document image from data."""
        
        # Create base image
        image = Image.new('RGB', (width, height), color='white')
        draw = ImageDraw.Draw(image)
        
        # Try to load fonts
        try:
            header_font = ImageFont.truetype("/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf", 28)
            title_font = ImageFont.truetype("/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf", 22)
            body_font = ImageFont.truetype("/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf", 18)
            small_font = ImageFont.truetype("/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf", 14)
        except (OSError, IOError):
            # Fallback to default fonts
            header_font = ImageFont.load_default()
            title_font = ImageFont.load_default()
            body_font = ImageFont.load_default()
            small_font = ImageFont.load_default()
        
        # Document header with logo area
        draw.rectangle([(50, 50), (width-50, 180)], fill='lightgray', outline='black', width=2)
        
        # Authority name
        authority_text = data.get('autoridadEmisora', 'AUTORIDAD EMISORA')
        draw.text((70, 80), authority_text, fill='black', font=header_font)
        
        # Document title
        doc_type = data.get('tipoRequerimiento', 'REQUERIMIENTO DE INFORMACIÓN')
        draw.text((70, 120), doc_type, fill='black', font=title_font)
        
        # Document body
        y_offset = 220
        line_height = 35
        
        # Date
        fecha = data.get('fecha', 'unknown')
        draw.text((70, y_offset), f"FECHA: {fecha}", fill='black', font=body_font)
        y_offset += line_height
        
        # Case number
        expediente = data.get('expediente', 'unknown')
        draw.text((70, y_offset), f"EXPEDIENTE: {expediente}", fill='black', font=body_font)
        y_offset += line_height
        
        # Spacing\n        y_offset += 20\n        \n        # Legal foundation\n        fundamento = data.get('fundamentoLegal', '')\n        if fundamento:\n            draw.text((70, y_offset), \"FUNDAMENTO LEGAL:\", fill='black', font=title_font)\n            y_offset += line_height\n            \n            # Wrap long text\n            words = fundamento.split()\n            line = \"\"\n            for word in words:\n                test_line = line + word + \" \"\n                if len(test_line) * 10 > width - 140:  # Approximate text width\n                    draw.text((90, y_offset), line, fill='black', font=body_font)\n                    y_offset += line_height - 5\n                    line = word + \" \"\n                else:\n                    line = test_line\n            \n            if line:\n                draw.text((90, y_offset), line, fill='black', font=body_font)\n                y_offset += line_height\n        \n        # Parties\n        partes = data.get('partes', [])\n        if partes:\n            y_offset += 20\n            draw.text((70, y_offset), \"PARTES INVOLUCRADAS:\", fill='black', font=title_font)\n            y_offset += line_height\n            \n            for parte in partes:\n                draw.text((90, y_offset), f\"• {parte}\", fill='black', font=body_font)\n                y_offset += line_height - 5\n        \n        # Detail section\n        detalle = data.get('detalle', {})\n        if detalle:\n            y_offset += 20\n            draw.text((70, y_offset), \"DETALLE DEL REQUERIMIENTO:\", fill='black', font=title_font)\n            y_offset += line_height\n            \n            descripcion = detalle.get('descripcion', '')\n            if descripcion:\n                # Wrap description text\n                words = descripcion.split()\n                line = \"\"\n                for word in words:\n                    test_line = line + word + \" \"\n                    if len(test_line) * 8 > width - 160:\n                        draw.text((90, y_offset), line, fill='black', font=body_font)\n                        y_offset += line_height - 8\n                        line = word + \" \"\n                    else:\n                        line = test_line\n                \n                if line:\n                    draw.text((90, y_offset), line, fill='black', font=body_font)\n                    y_offset += line_height\n            \n            # Amount\n            monto = detalle.get('monto')\n            moneda = detalle.get('moneda', 'MXN')\n            if monto:\n                draw.text((90, y_offset), f\"MONTO: ${monto:,.2f} {moneda}\", fill='black', font=body_font)\n                y_offset += line_height\n        \n        # Footer with date and signature area\n        if y_offset < height - 200:\n            y_offset = height - 200\n        \n        draw.text((70, y_offset), \"ATENTAMENTE\", fill='black', font=body_font)\n        y_offset += 60\n        \n        # Signature line\n        draw.line([(70, y_offset), (400, y_offset)], fill='black', width=1)\n        y_offset += 20\n        \n        draw.text((70, y_offset), \"NOMBRE Y FIRMA DEL FUNCIONARIO\", fill='black', font=small_font)\n        \n        # Add noise if requested\n        if add_noise:\n            self._add_noise(image, intensity=0.1 if quality == \"high\" else 0.3)\n        \n        # Add watermarks if requested\n        if add_watermarks:\n            self._add_watermarks(image, draw)\n        \n        # Simulate different quality levels\n        if quality == \"low\":\n            # Reduce quality by resizing down and up\n            temp_size = (width//2, height//2)\n            image = image.resize(temp_size, Image.Resampling.LANCZOS)\n            image = image.resize((width, height), Image.Resampling.LANCZOS)\n            self._add_noise(image, intensity=0.2)\n        \n        elif quality == \"medium\":\n            self._add_noise(image, intensity=0.05)\n        \n        return image\n    \n    def _add_noise(self, image: Image.Image, intensity: float = 0.1):\n        \"\"\"Add noise to image to simulate scan artifacts.\"\"\"\n        pixels = np.array(image)\n        noise = np.random.randint(-int(255*intensity), int(255*intensity), pixels.shape)\n        pixels = np.clip(pixels.astype(int) + noise, 0, 255)\n        \n        # Apply noise\n        noisy_image = Image.fromarray(pixels.astype('uint8'))\n        image.paste(noisy_image)\n    \n    def _add_watermarks(self, image: Image.Image, draw: ImageDraw.Draw):\n        \"\"\"Add watermarks to simulate real documents.\"\"\"\n        width, height = image.size\n        \n        # Add diagonal watermark\n        try:\n            watermark_font = ImageFont.truetype(\"/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf\", 60)\n        except (OSError, IOError):\n            watermark_font = ImageFont.load_default()\n        \n        # Create transparent watermark\n        watermark_img = Image.new('RGBA', (width, height), (255, 255, 255, 0))\n        watermark_draw = ImageDraw.Draw(watermark_img)\n        \n        # Add diagonal text watermarks\n        for i in range(0, width, 300):\n            for j in range(0, height, 200):\n                watermark_draw.text(\n                    (i, j), \n                    \"COPIA\", \n                    fill=(200, 200, 200, 60), \n                    font=watermark_font\n                )\n        \n        # Composite watermark onto image\n        image.paste(Image.alpha_composite(image.convert('RGBA'), watermark_img).convert('RGB'))\n    \n    def generate_test_dataset(self, num_documents: int = 100) -> list:\n        \"\"\"Generate a complete test dataset.\"\"\"\n        dataset = []\n        \n        variants = [\"standard\", \"minimal\", \"complex\", \"malformed\"]\n        qualities = [\"high\", \"medium\", \"low\"]\n        \n        for i in range(num_documents):\n            variant = random.choice(variants)\n            quality = random.choice(qualities)\n            \n            # Generate document data\n            doc_data = self.generate_legal_document_data(variant)\n            \n            # Generate image\n            add_noise = random.random() < 0.3\n            add_watermarks = random.random() < 0.4\n            \n            doc_image = self.generate_document_image(\n                doc_data,\n                width=random.randint(1000, 1400),\n                height=random.randint(1200, 1800),\n                add_noise=add_noise,\n                add_watermarks=add_watermarks,\n                quality=quality\n            )\n            \n            # Save files\n            image_filename = f\"doc_{i:04d}_{variant}_{quality}.png\"\n            data_filename = f\"doc_{i:04d}_{variant}_{quality}.json\"\n            \n            image_path = self.output_dir / \"images\" / image_filename\n            data_path = self.output_dir / \"ground_truth\" / data_filename\n            \n            # Ensure directories exist\n            image_path.parent.mkdir(parents=True, exist_ok=True)\n            data_path.parent.mkdir(parents=True, exist_ok=True)\n            \n            # Save files\n            doc_image.save(image_path, \"PNG\", optimize=True)\n            \n            with open(data_path, 'w', encoding='utf-8') as f:\n                json.dump(doc_data, f, indent=2, ensure_ascii=False)\n            \n            dataset.append({\n                'id': i,\n                'variant': variant,\n                'quality': quality,\n                'image_path': str(image_path),\n                'data_path': str(data_path),\n                'metadata': {\n                    'has_noise': add_noise,\n                    'has_watermarks': add_watermarks,\n                    'size': doc_image.size\n                }\n            })\n        \n        # Save dataset index\n        index_path = self.output_dir / \"dataset_index.json\"\n        with open(index_path, 'w', encoding='utf-8') as f:\n            json.dump({\n                'total_documents': num_documents,\n                'generated_at': datetime.now().isoformat(),\n                'variants': variants,\n                'qualities': qualities,\n                'documents': dataset\n            }, f, indent=2, ensure_ascii=False)\n        \n        print(f\"Generated {num_documents} test documents in {self.output_dir}\")\n        print(f\"Dataset index saved to {index_path}\")\n        \n        return dataset\n\n\ndef main():\n    \"\"\"Generate test fixtures.\"\"\"\n    output_dir = Path(__file__).parent\n    generator = TestFixtureGenerator(output_dir)\n    \n    # Generate different sized datasets\n    print(\"Generating test fixtures...\")\n    \n    # Small dataset for quick tests\n    generator.generate_test_dataset(20)\n    \n    # Generate specific test cases\n    test_cases = [\n        (\"edge_case_minimal\", \"minimal\", \"high\"),\n        (\"edge_case_complex\", \"complex\", \"high\"),\n        (\"edge_case_low_quality\", \"standard\", \"low\"),\n        (\"edge_case_watermarked\", \"standard\", \"medium\"),\n    ]\n    \n    for case_name, variant, quality in test_cases:\n        doc_data = generator.generate_legal_document_data(variant)\n        doc_image = generator.generate_document_image(\n            doc_data,\n            add_noise=(quality == \"low\"),\n            add_watermarks=(case_name == \"edge_case_watermarked\"),\n            quality=quality\n        )\n        \n        # Save edge case\n        image_path = generator.output_dir / \"images\" / f\"{case_name}.png\"\n        data_path = generator.output_dir / \"ground_truth\" / f\"{case_name}.json\"\n        \n        doc_image.save(image_path, \"PNG\")\n        with open(data_path, 'w', encoding='utf-8') as f:\n            json.dump(doc_data, f, indent=2, ensure_ascii=False)\n    \n    print(\"Test fixture generation complete!\")\n\n\nif __name__ == \"__main__\":\n    main()"