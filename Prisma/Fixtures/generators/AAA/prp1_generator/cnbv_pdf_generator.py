"""
CNBV PDF Generator - Pixel-Perfect Replication

Generates PDFs that visually match real CNBV documents from SIARA system.
Uses exact layout specifications extracted from real samples.
"""

from __future__ import annotations

import os
from pathlib import Path
from typing import Optional

from reportlab.lib import colors
from reportlab.lib.pagesizes import LETTER
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import inch
from reportlab.pdfgen import canvas as canvas_module
from reportlab.platypus import (
    Image as RLImage,
    PageBreak,
    Paragraph,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)

from .cnbv_schema import CNBVExpediente


class CNBVPDFGenerator:
    r"""
    Generates CNBV-compliant PDFs matching real SIARA document layout.

    Based on analysis of real documents from:
    F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Fixtures\PRP1\
    """

    def __init__(self, logo_path: Optional[Path] = None):
        """
        Initialize PDF generator.

        Args:
            logo_path: Path to LogoMexico.jpg (optional, will search if not provided)
        """
        self.logo_path = logo_path or self._find_logo()
        self.styles = self._create_styles()

    def _find_logo(self) -> Optional[Path]:
        """Find LogoMexico.jpg in common locations."""
        search_paths = [
            Path("Prisma/Fixtures/PRP1/LogoMexico.jpg"),
            Path("Prisma/Code/Src/CSharp/Python/LogoMexico.jpg"),
            Path("LogoMexico.jpg"),
            Path("generators/AAA/LogoMexico.jpg"),
        ]

        for path in search_paths:
            if path.exists():
                return path.resolve()

        return None

    def _create_styles(self) -> dict:
        """Create ReportLab styles matching CNBV documents."""
        base_styles = getSampleStyleSheet()

        # Header style - VICEPRESIDENCIA...
        header_style = ParagraphStyle(
            "CNBVHeader",
            parent=base_styles["Normal"],
            fontSize=10,
            leading=12,
            alignment=1,  # Center
            fontName="Helvetica-Bold",
            textColor=colors.black,
        )

        # Subheader style - Dirección General...
        subheader_style = ParagraphStyle(
            "CNBVSubHeader",
            parent=base_styles["Normal"],
            fontSize=9,
            leading=11,
            alignment=1,  # Center
            fontName="Helvetica",
            textColor=colors.black,
        )

        # Body style
        body_style = ParagraphStyle(
            "CNBVBody",
            parent=base_styles["Normal"],
            fontSize=10,
            leading=14,
            alignment=4,  # Justify
            fontName="Helvetica",
            textColor=colors.black,
        )

        # Small style for footer notes
        small_style = ParagraphStyle(
            "CNBVSmall",
            parent=base_styles["Normal"],
            fontSize=8,
            leading=10,
            fontName="Helvetica",
            textColor=colors.black,
        )

        # Bold style for labels
        bold_style = ParagraphStyle(
            "CNBVBold",
            parent=base_styles["Normal"],
            fontSize=10,
            leading=12,
            fontName="Helvetica-Bold",
            textColor=colors.black,
        )

        return {
            "header": header_style,
            "subheader": subheader_style,
            "body": body_style,
            "small": small_style,
            "bold": bold_style,
        }

    def generate_pdf(
        self,
        expediente: CNBVExpediente,
        output_path: Path,
        authority_reference: Optional[str] = None,
    ) -> Path:
        """
        Generate CNBV PDF from expediente data.

        Args:
            expediente: CNBVExpediente with all required data
            output_path: Where to save the PDF
            authority_reference: Optional reference from originating authority

        Returns:
            Path to generated PDF
        """
        output_path.parent.mkdir(parents=True, exist_ok=True)

        doc = SimpleDocTemplate(
            str(output_path),
            pagesize=LETTER,
            leftMargin=0.8 * inch,
            rightMargin=0.8 * inch,
            topMargin=0.7 * inch,
            bottomMargin=0.7 * inch,
        )

        story = []

        # 1. Logo header (5 logos side by side)
        story.extend(self._build_logo_header())
        story.append(Spacer(1, 0.15 * inch))

        # 2. CNBV Header block
        story.extend(self._build_header_block())
        story.append(Spacer(1, 0.1 * inch))

        # 3. Document metadata code (CNBV.4S.1,214-1,...)
        story.append(self._build_metadata_code(expediente))
        story.append(Spacer(1, 0.1 * inch))

        # 4. Recipient address
        story.extend(self._build_address_block())
        story.append(Spacer(1, 0.15 * inch))

        # 5. Date and oficio numbers
        story.extend(self._build_oficio_metadata(expediente))
        story.append(Spacer(1, 0.15 * inch))

        # 6. Subject line (ASUNTO:)
        story.extend(self._build_subject_line(expediente))
        story.append(Spacer(1, 0.15 * inch))

        # 7. Attention block
        story.extend(self._build_attention_block())
        story.append(Spacer(1, 0.15 * inch))

        # 8. Legal foundation paragraph
        story.extend(self._build_legal_foundation(expediente, authority_reference))
        story.append(Spacer(1, 0.1 * inch))

        # 9. Information request paragraph
        story.extend(self._build_information_request())
        story.append(Spacer(1, 0.1 * inch))

        # 10. Deadline paragraph
        story.extend(self._build_deadline_paragraph(expediente))
        story.append(Spacer(1, 0.15 * inch))

        # 11. Closing
        story.extend(self._build_closing())
        story.append(Spacer(1, 0.2 * inch))

        # 12. Signature block
        story.extend(self._build_signature_block(expediente))
        story.append(Spacer(1, 0.15 * inch))

        # 13. Footer note
        story.extend(self._build_footer_note())

        # Build PDF
        doc.build(story)

        return output_path

    def _build_logo_header(self) -> list:
        """Build header with 5 LogoMexico.jpg images side by side."""
        if not self.logo_path or not self.logo_path.exists():
            # Fallback to text if logo not found
            return [
                Paragraph(
                    "GOBIERNO DE MÉXICO" * 5,
                    self.styles["small"]
                )
            ]

        # Create 5 logo images
        logo_width = 1.1 * inch
        logo_height = 0.45 * inch

        logos = [
            RLImage(str(self.logo_path), width=logo_width, height=logo_height)
            for _ in range(5)
        ]

        # Create table with 5 columns
        logo_table = Table(
            [logos],
            colWidths=[logo_width] * 5,
            hAlign="CENTER"
        )

        return [logo_table]

    def _build_header_block(self) -> list:
        """Build CNBV header text block."""
        return [
            Paragraph(
                "VICEPRESIDENCIA DE SUPERVISIÓN DE PROCESOS PREVENTIVOS",
                self.styles["header"]
            ),
            Paragraph(
                "Dirección General de Atención a Autoridades",
                self.styles["subheader"]
            ),
            Paragraph(
                'COORDINACIÓN DE ATENCIÓN A AUTORIDADES "A"',
                self.styles["subheader"]
            ),
        ]

    def _build_metadata_code(self, expediente: CNBVExpediente) -> Paragraph:
        """Build metadata code line (CNBV.4S.1,214-1,...)."""
        # Extract date from Cnbv_FechaPublicacion
        fecha = expediente.Cnbv_FechaPublicacion or "01/01/2025"
        # Convert from ISO to DD/MM/YYYY
        if "-" in fecha:
            parts = fecha.split("-")
            fecha = f"{parts[2]}/{parts[1]}/{parts[0]}"

        # Get area code for the metadata
        area = expediente.Cnbv_AreaClave or "1"

        code = f'CNBV.4S.1,214-{area},"{fecha}",<2>'

        return Paragraph(code, self.styles["small"])

    def _build_address_block(self) -> list:
        """Build recipient address block."""
        # This would be the bank's address
        # For now, use placeholder matching real samples
        return [
            Paragraph("MXNALBCO", self.styles["body"]),
            Paragraph("01 ENERO PISO 1000 PONIENTE", self.styles["body"]),
            Paragraph("06000 CIUDAD DE MÉXICO", self.styles["body"]),
        ]

    def _build_oficio_metadata(self, expediente: CNBVExpediente) -> list:
        """Build date, oficio number, folio, and registro."""
        # Parse date for display
        fecha = expediente.Cnbv_FechaPublicacion or "2025-01-01"
        if "-" in fecha:
            from datetime import datetime
            fecha_obj = datetime.strptime(fecha, "%Y-%m-%d")
            fecha_text = fecha_obj.strftime("%d de %B de %Y")
            # Replace English months with Spanish
            meses = {
                "January": "Enero", "February": "Febrero", "March": "Marzo",
                "April": "Abril", "May": "Mayo", "June": "Junio",
                "July": "Julio", "August": "Agosto", "September": "Septiembre",
                "October": "Octubre", "November": "Noviembre", "December": "Diciembre"
            }
            for en, es in meses.items():
                fecha_text = fecha_text.replace(en, es)
        else:
            fecha_text = fecha

        return [
            Paragraph(f"Ciudad de México, a {fecha_text}.", self.styles["body"]),
            Spacer(1, 0.05 * inch),
            Paragraph(f"Oficio Núm.: {expediente.Cnbv_NumeroOficio}", self.styles["body"]),
            Paragraph(f"Folio   Núm.: {expediente.Cnbv_NumeroExpediente}", self.styles["body"]),
            Paragraph(
                f"Registro: {expediente.Cnbv_Folio} Año : {expediente.Cnbv_OficioYear}",
                self.styles["body"]
            ),
        ]

    def _build_subject_line(self, expediente: CNBVExpediente) -> list:
        """Build ASUNTO line."""
        authority = expediente.AutoridadNombre or "LA AUTORIDAD"

        if expediente.TieneAseguramiento:
            subject = f"ASUNTO: Para su atención, se remite oficio girado por"
        else:
            subject = f"ASUNTO: Se requiere información."

        return [
            Paragraph(f"<b>{subject}</b>", self.styles["body"]),
            Paragraph(authority.upper(), self.styles["bold"]),
        ]

    def _build_attention_block(self) -> list:
        """Build At'n: block."""
        return [
            Paragraph("At'n :", self.styles["body"]),
            Paragraph("PEPE TOÑO PALOMA FLORES", self.styles["body"]),
            Paragraph("DIRECTOR", self.styles["body"]),
        ]

    def _build_legal_foundation(
        self,
        expediente: CNBVExpediente,
        authority_reference: Optional[str]
    ) -> list:
        """Build legal foundation paragraph."""
        authority = expediente.AutoridadNombre or "LA AUTORIDAD"
        ref = authority_reference or expediente.Referencia2 or expediente.Cnbv_SolicitudSiara

        # Parse date from Cnbv_FechaPublicacion
        fecha = expediente.Cnbv_FechaPublicacion or "01 de Enero de 2025"

        text = (
            f"Con fundamento en lo dispuesto por los artículos 142, segundo y tercer párrafo s , "
            f"de la Ley de Instituciones de Crédito, 19 de la Ley de la Comisión Nacional Bancaria "
            f"y de Valores, y 49, fracciones I y III , del Reglamento Interior de la Comisión "
            f"Nacional Bancaria y de Valores, por conducto de esta Comisión se remite el o ficio "
            f"No. {ref} del {fecha} , emitido por {authority} , a efecto de que se sirva atender "
            f"de inmediato la solicitud contenida en el mismo, conforme a derecho."
        )

        # Add intentional spacing error: "párrafo s" instead of "párrafos"
        # Add spacing error: "o ficio" instead of "oficio"

        return [Paragraph(text, self.styles["body"])]

    def _build_information_request(self) -> list:
        """Build information request paragraph."""
        text = (
            "Asimismo, le agradeceré que la información y documentación que se sirva proporcionar "
            "al respecto sea en forma COMPLETA y LEGIBLE, o en su caso, indi car las razones por "
            "las cuales no es enviada de esa manera ."
        )

        # Intentional error: "indi car" instead of "indicar"

        return [Paragraph(text, self.styles["body"])]

    def _build_deadline_paragraph(self, expediente: CNBVExpediente) -> list:
        """Build deadline paragraph."""
        dias = expediente.Cnbv_DiasPlazo or "10"

        # Convert number to text
        dias_text = {
            "1": "UN", "3": "TRES", "5": "CINCO", "7": "SIETE", "10": "DIEZ",
            "15": "QUINCE", "20": "VEINTE", "30": "TREINTA"
        }.get(dias, dias)

        text = (
            f"Se le concede a esa Entidad Financiera , un plazo de {dias_text} DIA(S) HABIL(ES), "
            f"contado(s) a partir del día hábil siguiente a l que surta sus efectos la notificación "
            f"del presente, para proporcionar la respuesta al presente oficio y la información y "
            f"documentación correspondiente, por los medios electrónicos establecidos, así como de "
            f"manera física en los casos que proceda, en las oficinas de esta Comisión sitas en "
            f"Av. Insurgentes Sur No. 1971, Plaza Inn, Torre Sur, Nivel Avenida, Col. Guadalupe Inn, "
            f"Alcaldía Álvaro Obregón, C.P. 01020, Ciudad de México , apercibiéndole que, en caso de "
            f"incumplimiento, se hará acreedora a la imposición de la sanción establecida en los "
            f"ordenamientos legales aplicables."
        )

        # Intentional errors: "a l" instead of "al"

        return [Paragraph(text, self.styles["body"])]

    def _build_closing(self) -> list:
        """Build closing paragraph."""
        return [
            Paragraph("Sin otro particular, le envío un saludo.", self.styles["body"]),
            Paragraph("Atentamente .", self.styles["body"]),
        ]

    def _build_signature_block(self, expediente: CNBVExpediente) -> list:
        """Build signature block."""
        # Determine coordinator based on area
        area_clave = expediente.Cnbv_AreaClave or "1"
        coordinador_letter = {"1": "C", "3": "A", "5": "B", "6": "A"}.get(area_clave, "A")

        return [
            Paragraph("DR. OCAMPO DE ZARAGOZA X", self.styles["bold"]),
            Paragraph("COORDINADOR DE ATENCIÓN", self.styles["body"]),
            Paragraph(f'A AUTORIDADES "{coordinador_letter}"', self.styles["body"]),
            Spacer(1, 0.05 * inch),
            Paragraph(expediente.Cnbv_FechaPublicacion or "01/01/2025", self.styles["body"]),
            Paragraph("COORDINACIÓN DE ATENCIÓN A", self.styles["body"]),
            Paragraph(f'AUTORIDADES "{coordinador_letter}"', self.styles["body"]),
        ]

    def _build_footer_note(self) -> list:
        """Build footer note."""
        return [
            Paragraph(
                "<b>NOTA:</b> En su contestación, sírvase citar número de oficio y expediente.",
                self.styles["small"]
            ),
            Paragraph("PHM", self.styles["small"]),
        ]


def xml_to_pdf(xml_path: Path, output_path: Path, logo_path: Optional[Path] = None) -> Path:
    """
    Convert CNBV XML to PDF.

    Args:
        xml_path: Path to XML file
        output_path: Where to save PDF
        logo_path: Optional path to LogoMexico.jpg

    Returns:
        Path to generated PDF
    """
    from .cnbv_schema import parse_cnbv_xml

    # Parse XML
    expediente = parse_cnbv_xml(str(xml_path))

    # Generate PDF
    generator = CNBVPDFGenerator(logo_path=logo_path)
    return generator.generate_pdf(expediente, output_path)


if __name__ == "__main__":
    # Test with real sample
    import sys

    if len(sys.argv) < 2:
        print("Usage: python cnbv_pdf_generator.py <xml_file> [output_pdf]")
        sys.exit(1)

    xml_file = Path(sys.argv[1])
    output_file = Path(sys.argv[2]) if len(sys.argv) > 2 else xml_file.with_suffix(".generated.pdf")

    print(f"Converting {xml_file} to {output_file}...")
    result = xml_to_pdf(xml_file, output_file)
    print(f"Generated: {result}")
