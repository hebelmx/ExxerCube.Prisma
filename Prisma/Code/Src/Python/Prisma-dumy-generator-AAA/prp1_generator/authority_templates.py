"""Authority-specific layout templates for realistic fixtures."""

from __future__ import annotations

from dataclasses import dataclass
from typing import List, Optional


@dataclass
class AuthorityTemplate:
    identifier: str
    matches: List[str]
    title_lines: List[str]
    subtitle: str
    footer: str
    seal_text: str
    watermark: str


TEMPLATES: List[AuthorityTemplate] = [
    AuthorityTemplate(
        identifier="cnbv",
        matches=["CNBV", "Comisión Nacional Bancaria", "Comision Nacional Bancaria"],
        title_lines=[
            "COMISIÓN NACIONAL BANCARIA Y DE VALORES",
            "DIRECCIÓN GENERAL DE SUPERVISIÓN FINANCIERA",
        ],
        subtitle="Oficio de requerimiento en términos de la Ley de Instituciones de Crédito.",
        footer="Siara | Unidad de Inteligencia Financiera | Contacto: cnv.gob.mx/autoridades",
        seal_text="SELLO CNBV",
        watermark="CNBV - CONFIDENCIAL",
    ),
    AuthorityTemplate(
        identifier="imss",
        matches=["IMSS", "Instituto Mexicano del Seguro Social"],
        title_lines=[
            "INSTITUTO MEXICANO DEL SEGURO SOCIAL",
            "SUBDELEGACIÓN DE FISCALIZACIÓN",
        ],
        subtitle="Requerimiento para aseguramiento de cuentas y movimientos.",
        footer="IMSS | Coordinación de Recaudación Fiscal | Línea 800 623 2323",
        seal_text="IMSS",
        watermark="IMSS",
    ),
    AuthorityTemplate(
        identifier="tribunal",
        matches=["Juzgado", "Tribunal", "Sala", "Poder Judicial", "Distrito"],
        title_lines=[
            "PODER JUDICIAL DE LA FEDERACIÓN",
            "JUZGADO / TRIBUNAL COMPETENTE",
        ],
        subtitle="Orden emitida dentro del expediente indicado.",
        footer="Secretaría de Acuerdos | Actuario Responsable",
        seal_text="SELLO JUDICIAL",
        watermark="PODER JUDICIAL",
    ),
    AuthorityTemplate(
        identifier="uif",
        matches=["UIF", "Unidad de Inteligencia Financiera", "Fiscalía"],
        title_lines=[
            "UNIDAD DE INTELIGENCIA FINANCIERA",
            "SECRETARÍA DE HACIENDA Y CRÉDITO PÚBLICO",
        ],
        subtitle="Requerimiento derivado de investigaciones por operaciones inusuales.",
        footer="UIF | Prevención de Operaciones con Recursos de Procedencia Ilícita",
        seal_text="UIF",
        watermark="UIF - RESERVADO",
    ),
]


def match_template(authority_name: str) -> AuthorityTemplate:
    normalized = (authority_name or "").lower()
    for template in TEMPLATES:
        if any(keyword.lower() in normalized for keyword in template.matches):
            return template
    return AuthorityTemplate(
        identifier="generic",
        matches=[],
        title_lines=["AUTORIDAD ADMINISTRATIVA", "REQUERIMIENTO FORMAL"],
        subtitle="Documento oficial generado en el sistema PRP1.",
        footer="Sistema PRP1 | Uso exclusivo de prueba",
        seal_text="OFICIAL",
        watermark="CONFIDENCIAL",
    )
