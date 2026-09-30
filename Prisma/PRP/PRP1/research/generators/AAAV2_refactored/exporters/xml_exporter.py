"""XML exporter for CNBV documents using CNBV schema."""

import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Dict
from xml.dom import minidom


class XMLExporter:
    """Export CNBV documents as XML format following CNBV schema."""

    NAMESPACE = "http://www.cnbv.gob.mx"

    def __init__(self):
        """Initialize XML exporter."""
        pass

    def export(self, data: Dict, output_path: Path) -> Path:
        """Export data to XML file.

        Args:
            data: Dictionary with document data
            output_path: Path for output XML file

        Returns:
            Path to generated XML file
        """
        # Create root element with namespace
        root = ET.Element('SolicitudRequerimiento', attrib={
            'xmlns': self.NAMESPACE
        })

        # Add identification fields
        self._add_element(root, 'Cnbv_SolicitudSiara', data.get('Cnbv_SolicitudSiara', ''))
        self._add_element(root, 'Cnbv_NumeroOficio', data.get('Cnbv_NumeroOficio', ''))
        self._add_element(root, 'Cnbv_OficioYear', data.get('Cnbv_OficioYear', ''))
        self._add_element(root, 'Cnbv_AreaDescripcion', data.get('Cnbv_AreaDescripcion', ''))
        self._add_element(root, 'Cnbv_Folio', data.get('Cnbv_Folio', ''))

        # Additional CNBV fields
        self._add_element(root, 'Cnbv_NumeroExpediente', data.get('Cnbv_NumeroExpediente', ''))
        self._add_element(root, 'Cnbv_FechaPublicacion', data.get('Cnbv_FechaPublicacion', ''))
        self._add_element(root, 'Cnbv_DiasPlazo', data.get('Cnbv_DiasPlazo', ''))

        # Authority
        self._add_element(root, 'AutoridadNombre', data.get('AutoridadNombre', ''))
        self._add_element(root, 'NombreSolicitante', data.get('NombreSolicitante', ''))

        # References
        self._add_element(root, 'Referencia', data.get('Referencia', ''))
        self._add_element(root, 'Referencia1', data.get('Referencia1', ''))
        self._add_element(root, 'Referencia2', data.get('Referencia2', ''))

        # Aseguramiento
        tiene_aseguramiento = 'true' if data.get('TieneAseguramiento', 'No') == 'Sí' else 'false'
        self._add_element(root, 'TieneAseguramiento', tiene_aseguramiento)

        # Partes (first item)
        if data.get('SolicitudPartes_Nombre'):
            partes = ET.SubElement(root, 'SolicitudPartes')
            self._add_element(partes, 'Nombre', data.get('SolicitudPartes_Nombre', ''))
            self._add_element(partes, 'Caracter', data.get('SolicitudPartes_Caracter', ''))

        # Instrucciones
        self._add_element(root, 'InstruccionesCuentasPorConocer',
                         data.get('InstruccionesCuentasPorConocer', ''))

        # Personas
        if data.get('Persona_Nombre'):
            personas = ET.SubElement(root, 'PersonasSolicitud')
            self._add_element(personas, 'Nombre', data.get('Persona_Nombre', ''))
            self._add_element(personas, 'Rfc', data.get('Persona_Rfc', ''))
            self._add_element(personas, 'Caracter', data.get('Persona_Caracter', ''))
            self._add_element(personas, 'Domicilio', data.get('Persona_Domicilio', ''))
            self._add_element(personas, 'Complementarios', data.get('Persona_Complementarios', ''))

        # Create formatted XML string
        xml_string = self._prettify(root)

        # Write to file
        output_path.parent.mkdir(parents=True, exist_ok=True)
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write(xml_string)

        return output_path

    def _add_element(self, parent: ET.Element, tag: str, text: str) -> ET.Element:
        """Add child element with text.

        Args:
            parent: Parent element
            tag: Element tag name
            text: Element text content

        Returns:
            Created element
        """
        elem = ET.SubElement(parent, tag)
        elem.text = str(text) if text else ''
        return elem

    def _prettify(self, elem: ET.Element) -> str:
        """Return a pretty-printed XML string.

        Args:
            elem: Root element

        Returns:
            Formatted XML string
        """
        rough_string = ET.tostring(elem, encoding='utf-8')
        reparsed = minidom.parseString(rough_string)
        return reparsed.toprettyxml(indent="  ", encoding='utf-8').decode('utf-8')

    def validate_against_schema(self, xml_path: Path, schema_path: Path = None) -> bool:
        """Validate XML against CNBV schema (if lxml available).

        Args:
            xml_path: Path to XML file
            schema_path: Optional path to XSD schema file

        Returns:
            True if valid, False otherwise
        """
        try:
            from lxml import etree

            # Parse XML
            with open(xml_path, 'rb') as f:
                xml_doc = etree.parse(f)

            # If schema provided, validate
            if schema_path and schema_path.exists():
                with open(schema_path, 'rb') as f:
                    schema_doc = etree.parse(f)
                    schema = etree.XMLSchema(schema_doc)

                return schema.validate(xml_doc)

            return True  # No schema to validate against

        except ImportError:
            # lxml not available, skip validation
            return True

        except Exception as e:
            print(f"Validation error: {e}")
            return False
