"""Markdown exporter for CNBV documents."""

from pathlib import Path
from typing import Dict


class MarkdownExporter:
    """Export CNBV documents as Markdown format."""

    def __init__(self):
        """Initialize Markdown exporter."""
        pass

    def export(self, data: Dict, output_path: Path, template_path: Path = None) -> Path:
        """Export data to Markdown file.

        Args:
            data: Dictionary with document data
            output_path: Path for output MD file
            template_path: Optional path to markdown template

        Returns:
            Path to generated MD file
        """
        if template_path and template_path.exists():
            # Use template
            with open(template_path, 'r', encoding='utf-8') as f:
                template = f.read()

            # Replace placeholders
            markdown_content = self._apply_template(template, data)
        else:
            # Generate from scratch
            markdown_content = self._generate_markdown(data)

        # Write to file
        output_path.parent.mkdir(parents=True, exist_ok=True)
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write(markdown_content)

        return output_path

    def _apply_template(self, template: str, data: Dict) -> str:
        """Apply data to template by replacing {{placeholders}}.

        Args:
            template: Template string
            data: Data dictionary

        Returns:
            Rendered markdown
        """
        result = template
        for key, value in data.items():
            placeholder = "{{" + key + "}}"
            result = result.replace(placeholder, str(value))

        return result

    def _generate_markdown(self, data: Dict) -> str:
        """Generate markdown from data (no template).

        Args:
            data: Data dictionary

        Returns:
            Generated markdown string
        """
        md_lines = []

        # Header
        md_lines.append("Administración General de Auditoría Fiscal Federal")
        md_lines.append(data.get('UnidadSolicitante', ''))
        md_lines.append("")

        # ID box
        md_lines.append(f"**No. De Identificación del Requerimiento**")
        md_lines.append(f"**{data.get('Cnbv_SolicitudSiara', 'N/A')}**")
        md_lines.append("")
        md_lines.append("---")
        md_lines.append("")

        # Recipient
        md_lines.append(f"**{data.get('Destinatario_Nombre', '')}**")
        md_lines.append(data.get('Destinatario_Cargo', ''))
        md_lines.append(data.get('Destinatario_Institucion', ''))
        md_lines.append(data.get('Destinatario_Direccion', ''))
        md_lines.append("**P r e s e n t e**")
        md_lines.append("")

        # Datos generales del solicitante
        md_lines.append("## Datos generales del solicitante")
        md_lines.append("")
        md_lines.append("| Solicitante | Detalles |")
        md_lines.append("|-------------|----------|")
        md_lines.append(f"| Unidad | {data.get('UnidadSolicitante', '')} |")
        md_lines.append(f"| Domicilio | {data.get('DomicilioSolicitante', '')} |")
        md_lines.append(f"| Servidor Público | {data.get('ServidorPublico_Nombre', '')} |")
        md_lines.append(f"| Cargo | {data.get('ServidorPublico_Cargo', '')} |")
        md_lines.append(f"| Teléfono | {data.get('ServidorPublico_Telefono', '')} |")
        md_lines.append(f"| Correo | {data.get('ServidorPublico_Correo', '')} |")
        md_lines.append("")

        # Facultades
        md_lines.append("## Facultades de la Autoridad")
        md_lines.append("")
        md_lines.append(data.get('FacultadesTexto', ''))
        md_lines.append("")

        # Fundamento
        md_lines.append("## Fundamento del Requerimiento")
        md_lines.append("")
        md_lines.append(data.get('FundamentoTexto', ''))
        md_lines.append("")

        # Motivación
        md_lines.append("## Motivación del requerimiento")
        md_lines.append("")
        md_lines.append(data.get('MotivacionTexto', ''))
        md_lines.append("")
        md_lines.append(data.get('MontoTexto', ''))
        md_lines.append("")

        # Origen
        md_lines.append("## Origen del requerimiento")
        md_lines.append("")
        md_lines.append(f"**¿Contiene aseguramiento?** {data.get('TieneAseguramiento', 'No')}")
        md_lines.append(f"**No de oficio:** {data.get('NoOficioRevision', 'N/A')}")
        md_lines.append(f"**Monto:** {data.get('MontoCredito', 'N/A')}")
        md_lines.append(f"**Créditos:** {data.get('CreditosFiscales', 'N/A')}")
        md_lines.append(f"**Periodos:** {data.get('Periodos', 'N/A')}")
        md_lines.append("")

        # Personas
        md_lines.append("## Personas de quien se requiere información")
        md_lines.append("")
        md_lines.append("| Nombre | RFC | Carácter | Dirección | Complementarios |")
        md_lines.append("|--------|-----|----------|-----------|-----------------|")
        md_lines.append(f"| {data.get('Persona_Nombre', '')} | {data.get('Persona_Rfc', '')} | {data.get('Persona_Caracter', '')} | {data.get('Persona_Domicilio', '')} | {data.get('Persona_Complementarios', '')} |")
        md_lines.append("")

        # Sectores bancarios
        md_lines.append("## Cuentas por conocer")
        md_lines.append("")
        md_lines.append(data.get('SectoresBancarios', ''))
        md_lines.append("")

        # Instrucciones
        md_lines.append("## Instrucciones sobre las cuentas por conocer")
        md_lines.append("")
        md_lines.append(data.get('InstruccionesCuentasPorConocer', ''))
        md_lines.append("")

        # Closing
        closing = f"Derivado de lo anterior solicitó a la comisión nacional bancaria y de valores sea atendido el presente requerimiento y gestionado por medio de del sistema de atención de requerimientos autoridad (SIARA) contando con el folio {data.get('Cnbv_SolicitudSiara', 'N/A')}"
        md_lines.append(closing)
        md_lines.append("")

        # Signature
        md_lines.append("---")
        md_lines.append("")
        md_lines.append("_" * 40)
        md_lines.append("")
        md_lines.append(f"**{data.get('ServidorPublico_Nombre', '')}**")
        md_lines.append("")
        md_lines.append(f"**{data.get('ServidorPublico_Cargo', '')}**")

        return "\n".join(md_lines)
