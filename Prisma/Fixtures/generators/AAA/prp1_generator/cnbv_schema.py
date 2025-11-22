r"""
CNBV XML Schema - Extracted from real PRP1 samples.

This module defines the exact XML structure used by CNBV (Comisión Nacional Bancaria y de Valores)
for regulatory requests in the SIARA system.

Schema extracted from: F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Fixtures\PRP1\
"""

from __future__ import annotations

import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional


# CNBV XML Namespace
CNBV_NAMESPACE = "http://www.cnbv.gob.mx"
XSI_NAMESPACE = "http://www.w3.org/2001/XMLSchema-instance"
XSD_NAMESPACE = "http://www.w3.org/2001/XMLSchema"


@dataclass
class SolicitudPartes:
    """Represents a party in the request (SolicitudPartes)."""
    ParteId: str = "1"
    Caracter: str = ""  # e.g., "Patrón Determinado", "Contribuyente Auditado"
    Persona: str = "Moral"  # "Moral" or "Fisica"
    Paterno: str = ""
    Materno: str = ""
    Nombre: str = ""
    Rfc: str = "             "  # 13 spaces if empty


@dataclass
class PersonasSolicitud:
    """Represents detailed person information in SolicitudEspecifica."""
    PersonaId: str = "1"
    Caracter: str = ""  # e.g., "Patrón Determinado", "Tercero vinculado fiscalmente"
    Persona: str = "Moral"  # "Moral" or "Fisica"
    Paterno: str = ""
    Materno: str = ""
    Nombre: str = ""
    Rfc: str = ""
    Relacion: str = ""
    Domicilio: str = ""
    Complementarios: str = ""


@dataclass
class SolicitudEspecifica:
    """Represents the specific request details."""
    SolicitudEspecificaId: str = "1"
    InstruccionesCuentasPorConocer: str = ""
    PersonasSolicitud: PersonasSolicitud = field(default_factory=PersonasSolicitud)


@dataclass
class CNBVExpediente:
    """
    Represents a complete CNBV Expediente (case file).

    This structure matches the actual CNBV XML schema used in SIARA.
    All field names must match exactly as they appear in real documents.
    """

    # CNBV Metadata
    Cnbv_NumeroOficio: str = ""  # e.g., "222/AAA/-4444444444/2025"
    Cnbv_NumeroExpediente: str = ""  # e.g., "A/AS1-1111-222222-AAA    " (note: may have trailing spaces)
    Cnbv_SolicitudSiara: str = ""  # e.g., "AGAFADAFSON2/2025/000084"
    Cnbv_Folio: str = ""  # e.g., "6789"
    Cnbv_OficioYear: str = ""  # e.g., "2025"
    Cnbv_AreaClave: str = ""  # "1" (HACENDARIO), "3" (ASEGURAMIENTO), "5" (INFORMACION)
    Cnbv_AreaDescripcion: str = ""  # "ASEGURAMIENTO", "HACENDARIO", "INFORMACION", "JUDICIAL"
    Cnbv_FechaPublicacion: str = ""  # ISO format: "2025-06-05"
    Cnbv_DiasPlazo: str = ""  # e.g., "7"

    # Authority Information
    AutoridadNombre: str = ""  # Name of requesting authority
    AutoridadEspecificaNombre: Optional[str] = None  # Optional specific authority name
    NombreSolicitante: Optional[str] = None  # Usually nil/empty

    # References
    Referencia: str = "                         "  # Usually 25 spaces
    Referencia1: str = "                         "  # Usually 25 spaces
    Referencia2: str = ""  # e.g., "IMSSCOB/40/01/001283/2025"

    # Flags
    TieneAseguramiento: bool = False  # "true" or "false"

    # Nested structures
    SolicitudPartes: SolicitudPartes = field(default_factory=SolicitudPartes)
    SolicitudEspecifica: SolicitudEspecifica = field(default_factory=SolicitudEspecifica)


# Area codes mapping
AREA_CODES = {
    "1": "HACENDARIO",
    "3": "ASEGURAMIENTO",
    "5": "INFORMACION",
    "6": "JUDICIAL",
}


# Character types (Caracter) for parties
CARACTER_TYPES = [
    "Patrón Determinado",
    "Contribuyente Auditado",
    "Tercero vinculado fiscalmente",
    "Demandado",
    "Demandante",
    "Tercero",
    "Persona de Interés",
]


# Person types
PERSONA_TYPES = ["Moral", "Fisica"]


def create_cnbv_xml(expediente: CNBVExpediente) -> ET.ElementTree:
    """
    Create CNBV-compliant XML from an Expediente dataclass.

    Args:
        expediente: CNBVExpediente dataclass with all required fields

    Returns:
        ElementTree with proper CNBV namespace and structure
    """
    # Register namespaces
    ET.register_namespace('', CNBV_NAMESPACE)
    ET.register_namespace('xsi', XSI_NAMESPACE)
    ET.register_namespace('xsd', XSD_NAMESPACE)

    # Create root element with namespaces
    root = ET.Element(
        f"{{{CNBV_NAMESPACE}}}Expediente",
        {
            f"{{{XSI_NAMESPACE}}}schemaLocation": f"{CNBV_NAMESPACE}",
            f"{{{XSI_NAMESPACE}}}type": "Expediente",
        }
    )

    # Add CNBV metadata fields
    _add_element(root, "Cnbv_NumeroOficio", expediente.Cnbv_NumeroOficio)
    _add_element(root, "Cnbv_NumeroExpediente", expediente.Cnbv_NumeroExpediente)
    _add_element(root, "Cnbv_SolicitudSiara", expediente.Cnbv_SolicitudSiara)
    _add_element(root, "Cnbv_Folio", expediente.Cnbv_Folio)
    _add_element(root, "Cnbv_OficioYear", expediente.Cnbv_OficioYear)
    _add_element(root, "Cnbv_AreaClave", expediente.Cnbv_AreaClave)
    _add_element(root, "Cnbv_AreaDescripcion", expediente.Cnbv_AreaDescripcion)
    _add_element(root, "Cnbv_FechaPublicacion", expediente.Cnbv_FechaPublicacion)
    _add_element(root, "Cnbv_DiasPlazo", expediente.Cnbv_DiasPlazo)

    # Add authority information
    _add_element(root, "AutoridadNombre", expediente.AutoridadNombre)

    if expediente.AutoridadEspecificaNombre:
        _add_element(root, "AutoridadEspecificaNombre", expediente.AutoridadEspecificaNombre)

    # NombreSolicitante - usually nil
    if expediente.NombreSolicitante is None:
        nombre_solicitante = ET.SubElement(root, f"{{{CNBV_NAMESPACE}}}NombreSolicitante")
        nombre_solicitante.set(f"{{{XSI_NAMESPACE}}}nil", "true")
    else:
        _add_element(root, "NombreSolicitante", expediente.NombreSolicitante)

    # Add references
    _add_element(root, "Referencia", expediente.Referencia)
    _add_element(root, "Referencia1", expediente.Referencia1)
    _add_element(root, "Referencia2", expediente.Referencia2)

    # Add flags
    _add_element(root, "TieneAseguramiento", "true" if expediente.TieneAseguramiento else "false")

    # Add SolicitudPartes
    solicitud_partes = ET.SubElement(root, f"{{{CNBV_NAMESPACE}}}SolicitudPartes")
    _add_element(solicitud_partes, "ParteId", expediente.SolicitudPartes.ParteId)
    _add_element(solicitud_partes, "Caracter", expediente.SolicitudPartes.Caracter)
    _add_element(solicitud_partes, "Persona", expediente.SolicitudPartes.Persona)
    _add_element(solicitud_partes, "Paterno", expediente.SolicitudPartes.Paterno)
    _add_element(solicitud_partes, "Materno", expediente.SolicitudPartes.Materno)
    _add_element(solicitud_partes, "Nombre", expediente.SolicitudPartes.Nombre)
    _add_element(solicitud_partes, "Rfc", expediente.SolicitudPartes.Rfc)

    # Add SolicitudEspecifica
    solicitud_especifica = ET.SubElement(root, f"{{{CNBV_NAMESPACE}}}SolicitudEspecifica")
    _add_element(
        solicitud_especifica,
        "SolicitudEspecificaId",
        expediente.SolicitudEspecifica.SolicitudEspecificaId
    )
    _add_element(
        solicitud_especifica,
        "InstruccionesCuentasPorConocer",
        expediente.SolicitudEspecifica.InstruccionesCuentasPorConocer
    )

    # Add PersonasSolicitud
    personas_solicitud = ET.SubElement(solicitud_especifica, f"{{{CNBV_NAMESPACE}}}PersonasSolicitud")
    persona = expediente.SolicitudEspecifica.PersonasSolicitud
    _add_element(personas_solicitud, "PersonaId", persona.PersonaId)
    _add_element(personas_solicitud, "Caracter", persona.Caracter)
    _add_element(personas_solicitud, "Persona", persona.Persona)
    _add_element(personas_solicitud, "Paterno", persona.Paterno)
    _add_element(personas_solicitud, "Materno", persona.Materno)
    _add_element(personas_solicitud, "Nombre", persona.Nombre)
    _add_element(personas_solicitud, "Rfc", persona.Rfc)
    _add_element(personas_solicitud, "Relacion", persona.Relacion)
    _add_element(personas_solicitud, "Domicilio", persona.Domicilio)
    _add_element(personas_solicitud, "Complementarios", persona.Complementarios)

    return ET.ElementTree(root)


def _add_element(parent: ET.Element, tag: str, text: str) -> ET.Element:
    """Helper to add an element with proper namespace."""
    elem = ET.SubElement(parent, f"{{{CNBV_NAMESPACE}}}{tag}")
    if text:
        elem.text = str(text)
    return elem


def parse_cnbv_xml(xml_path: str) -> CNBVExpediente:
    """
    Parse a CNBV XML file into a CNBVExpediente dataclass.

    Args:
        xml_path: Path to the XML file

    Returns:
        CNBVExpediente with all parsed fields
    """
    tree = ET.parse(xml_path)
    root = tree.getroot()

    # Helper to get text from element
    def get_text(tag: str, default: str = "") -> str:
        elem = root.find(f"{{{CNBV_NAMESPACE}}}{tag}")
        if elem is not None and elem.text:
            return elem.text
        return default

    # Parse SolicitudPartes
    sp_elem = root.find(f"{{{CNBV_NAMESPACE}}}SolicitudPartes")
    solicitud_partes = SolicitudPartes()
    if sp_elem is not None:
        solicitud_partes.ParteId = get_text_from_elem(sp_elem, "ParteId", "1")
        solicitud_partes.Caracter = get_text_from_elem(sp_elem, "Caracter", "")
        solicitud_partes.Persona = get_text_from_elem(sp_elem, "Persona", "Moral")
        solicitud_partes.Paterno = get_text_from_elem(sp_elem, "Paterno", "")
        solicitud_partes.Materno = get_text_from_elem(sp_elem, "Materno", "")
        solicitud_partes.Nombre = get_text_from_elem(sp_elem, "Nombre", "")
        solicitud_partes.Rfc = get_text_from_elem(sp_elem, "Rfc", "             ")

    # Parse SolicitudEspecifica
    se_elem = root.find(f"{{{CNBV_NAMESPACE}}}SolicitudEspecifica")
    solicitud_especifica = SolicitudEspecifica()
    if se_elem is not None:
        solicitud_especifica.SolicitudEspecificaId = get_text_from_elem(se_elem, "SolicitudEspecificaId", "1")
        solicitud_especifica.InstruccionesCuentasPorConocer = get_text_from_elem(
            se_elem, "InstruccionesCuentasPorConocer", ""
        )

        # Parse PersonasSolicitud
        ps_elem = se_elem.find(f"{{{CNBV_NAMESPACE}}}PersonasSolicitud")
        if ps_elem is not None:
            persona = PersonasSolicitud()
            persona.PersonaId = get_text_from_elem(ps_elem, "PersonaId", "1")
            persona.Caracter = get_text_from_elem(ps_elem, "Caracter", "")
            persona.Persona = get_text_from_elem(ps_elem, "Persona", "Moral")
            persona.Paterno = get_text_from_elem(ps_elem, "Paterno", "")
            persona.Materno = get_text_from_elem(ps_elem, "Materno", "")
            persona.Nombre = get_text_from_elem(ps_elem, "Nombre", "")
            persona.Rfc = get_text_from_elem(ps_elem, "Rfc", "")
            persona.Relacion = get_text_from_elem(ps_elem, "Relacion", "")
            persona.Domicilio = get_text_from_elem(ps_elem, "Domicilio", "")
            persona.Complementarios = get_text_from_elem(ps_elem, "Complementarios", "")
            solicitud_especifica.PersonasSolicitud = persona

    # Parse NombreSolicitante (check for nil)
    ns_elem = root.find(f"{{{CNBV_NAMESPACE}}}NombreSolicitante")
    nombre_solicitante = None
    if ns_elem is not None:
        if ns_elem.get(f"{{{XSI_NAMESPACE}}}nil") != "true":
            nombre_solicitante = ns_elem.text or ""

    # Build expediente
    return CNBVExpediente(
        Cnbv_NumeroOficio=get_text("Cnbv_NumeroOficio"),
        Cnbv_NumeroExpediente=get_text("Cnbv_NumeroExpediente"),
        Cnbv_SolicitudSiara=get_text("Cnbv_SolicitudSiara"),
        Cnbv_Folio=get_text("Cnbv_Folio"),
        Cnbv_OficioYear=get_text("Cnbv_OficioYear"),
        Cnbv_AreaClave=get_text("Cnbv_AreaClave"),
        Cnbv_AreaDescripcion=get_text("Cnbv_AreaDescripcion"),
        Cnbv_FechaPublicacion=get_text("Cnbv_FechaPublicacion"),
        Cnbv_DiasPlazo=get_text("Cnbv_DiasPlazo"),
        AutoridadNombre=get_text("AutoridadNombre"),
        AutoridadEspecificaNombre=get_text("AutoridadEspecificaNombre") or None,
        NombreSolicitante=nombre_solicitante,
        Referencia=get_text("Referencia", "                         "),
        Referencia1=get_text("Referencia1", "                         "),
        Referencia2=get_text("Referencia2"),
        TieneAseguramiento=get_text("TieneAseguramiento") == "true",
        SolicitudPartes=solicitud_partes,
        SolicitudEspecifica=solicitud_especifica,
    )


def get_text_from_elem(parent: ET.Element, tag: str, default: str = "") -> str:
    """Helper to get text from a child element."""
    elem = parent.find(f"{{{CNBV_NAMESPACE}}}{tag}")
    if elem is not None and elem.text:
        return elem.text
    return default
