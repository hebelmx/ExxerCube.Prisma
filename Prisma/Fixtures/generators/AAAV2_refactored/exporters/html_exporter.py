"""HTML exporter using Jinja2 templates with proven AAAV2 styling."""

import base64
import hashlib
from pathlib import Path
from typing import Dict, Optional
from jinja2 import Environment, FileSystemLoader, Template


class HTMLExporter:
    """Export CNBV documents as styled HTML using Jinja2 templates."""

    def __init__(self, template_dir: Optional[Path] = None, logo_path: Optional[Path] = None):
        """Initialize HTML exporter.

        Args:
            template_dir: Directory containing Jinja2 templates
            logo_path: Path to logo image file
        """
        if template_dir is None:
            template_dir = Path(__file__).parent.parent / 'templates'

        self.template_dir = template_dir
        self.logo_path = logo_path

        # Setup Jinja2 environment
        self.env = Environment(
            loader=FileSystemLoader(str(template_dir)),
            autoescape=True,
            trim_blocks=True,
            lstrip_blocks=True
        )

        # Load logo as base64 if provided
        self.logo_base64 = self._load_logo()

    def _load_logo(self) -> str:
        """Load logo image and encode as base64.

        Returns:
            Base64 encoded logo string
        """
        if self.logo_path and self.logo_path.exists():
            with open(self.logo_path, 'rb') as f:
                return base64.b64encode(f.read()).decode('utf-8')
        return ""

    def _generate_watermark(self, folio: str) -> str:
        """Generate scrambled watermark from folio.

        Args:
            folio: Document folio/ID

        Returns:
            Watermark text
        """
        hash_obj = hashlib.sha256(folio.encode())
        hash_hex = hash_obj.hexdigest()[:16].upper()

        parts = folio.split('/')
        if len(parts) >= 3:
            return f"{parts[0][:4]}-{hash_hex[:8]}-{parts[2]}"
        else:
            return f"{folio}-{hash_hex[:8]}"

    def export(self, data: Dict, output_path: Path, template_name: str = 'cnbv_requirement.html') -> Path:
        """Export data to HTML file.

        Args:
            data: Dictionary with document data
            output_path: Path for output HTML file
            template_name: Name of Jinja2 template to use

        Returns:
            Path to generated HTML file
        """
        # Generate watermark
        watermark = self._generate_watermark(data.get('Cnbv_SolicitudSiara', 'DOCUMENTO'))

        # Prepare template context
        context = {
            'data': data,
            'watermark': watermark,
            'logo_base64': self.logo_base64,
        }

        # Render template
        template = self.env.get_template(template_name)
        html_content = template.render(**context)

        # Write to file
        output_path.parent.mkdir(parents=True, exist_ok=True)
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write(html_content)

        return output_path

    def render_to_string(self, data: Dict, template_name: str = 'cnbv_requirement.html') -> str:
        """Render template to HTML string without writing file.

        Args:
            data: Dictionary with document data
            template_name: Name of Jinja2 template to use

        Returns:
            Rendered HTML string
        """
        watermark = self._generate_watermark(data.get('Cnbv_SolicitudSiara', 'DOCUMENTO'))

        context = {
            'data': data,
            'watermark': watermark,
            'logo_base64': self.logo_base64,
        }

        template = self.env.get_template(template_name)
        return template.render(**context)

    def get_proven_css(self) -> str:
        """Get proven CSS from AAAV2 implementation.

        Returns:
            CSS string with all proven styles
        """
        return """
        @page {
            margin: 40px;
        }

        body {
            font-family: Arial, Helvetica, sans-serif;
            font-size: 11pt;
            line-height: 1.4;
            color: #000;
            margin: 0;
            padding: 20px;
        }

        .header {
            text-align: center;
            margin-bottom: 20px;
        }

        .header-logo {
            width: 60px;
            height: auto;
            margin: 0 5px;
            display: inline-block;
        }

        h1 {
            font-size: 11pt;
            font-weight: normal;
            margin: 5px 0;
            text-align: left;
        }

        .id-box {
            border: 2px solid #000;
            padding: 8px 15px;
            float: right;
            margin: 10px 0 20px 20px;
            font-size: 10pt;
            text-align: center;
        }

        .section-header {
            border: 1px solid #000;
            padding: 5px 10px;
            background-color: #f5f5f5;
            font-weight: bold;
            margin: 15px 0 10px 0;
            text-align: center;
        }

        .subsection-header {
            border: 1px solid #000;
            padding: 5px 10px;
            background-color: #f9f9f9;
            margin: 10px 0;
            text-align: center;
        }

        .two-column-table {
            width: 100%;
            border: 1px solid #000;
            border-collapse: collapse;
            margin: 15px 0;
        }

        .two-column-table td {
            border: 1px solid #000;
            padding: 15px;
            vertical-align: top;
            width: 50%;
        }

        table {
            width: 100%;
            border-collapse: collapse;
            margin: 15px 0;
            font-size: 10pt;
        }

        table, th, td {
            border: 1px solid #000;
        }

        th, td {
            padding: 8px;
            text-align: left;
        }

        th {
            background-color: #f2f2f2;
            font-weight: bold;
        }

        hr {
            border: none;
            border-top: 1px solid #ccc;
            margin: 20px 0;
        }

        p {
            margin: 10px 0;
            text-align: justify;
        }

        strong {
            font-weight: bold;
        }

        .signature {
            text-align: center;
            margin-top: 80px;
            page-break-inside: avoid;
        }

        /* Watermark - Diagonal text across page in red */
        .watermark {
            position: fixed;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%) rotate(-45deg);
            font-size: 80pt;
            font-weight: bold;
            color: rgba(255, 0, 0, 0.12);
            z-index: 9999;
            white-space: nowrap;
            pointer-events: none;
            font-family: 'Courier New', monospace;
            letter-spacing: 8px;
            text-shadow: 0 0 2px rgba(255, 0, 0, 0.1);
        }

        @media print {
            .watermark {
                position: fixed;
                color: rgba(255, 0, 0, 0.12);
            }
        }
        """
