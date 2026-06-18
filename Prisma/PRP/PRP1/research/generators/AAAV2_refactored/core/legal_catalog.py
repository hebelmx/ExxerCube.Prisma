"""Catalog of Mexican legal articles and references for CNBV requirements."""

import random
from typing import List, Dict, Optional


class LegalArticleCatalog:
    """Catalog of Mexican banking/legal articles organized by requirement type."""

    # Legal articles organized by requirement type
    ARTICLES = {
        'fiscal': [
            'Art. 42 del Código Fiscal de la Federación',
            'Artículos 145, 151, 152, 153, 154 del Código Fiscal de la Federación',
            'Art. 16 de la Constitución Política de los Estados Unidos Mexicanos',
            'Art. 3 fracción I y 45 de la Ley Orgánica de la Administración Pública Federal',
            'Artículos 5, 9, 250 fracciones VII, XX y XXVI de la Ley del Seguro Social',
            'Art. 160 del Código Fiscal de la Federación',
            'Art. 287 y 291 de la Ley del Seguro Social',
        ],
        'judicial': [
            'Art. 142 de la Ley de Instituciones de Crédito',
            'Art. 16 Constitución Política de los Estados Unidos Mexicanos',
            'Art. 40 del Código Nacional de Procedimientos Penales',
            'Art. 192 de la Ley del Mercado de Valores',
            'Artículos 16 y 20 Constitucional',
            'Art. 181 del Código Nacional de Procedimientos Penales',
        ],
        'pld': [
            'Art. 400 bis del Código Penal Federal',
            'LFPIORPI artículos 17, 18 y 23',
            'Art. 139 Ter del Código Penal Federal',
            'Ley Federal contra la Delincuencia Organizada artículos 2 y 11',
            'Art. 53 de la Ley de Instituciones de Crédito',
        ],
        'aseguramiento': [
            'Art. 160 del Código Fiscal de la Federación',
            'Art. 40 del Código Nacional de Procedimientos Penales',
            'Art. 181 del Código Nacional de Procedimientos Penales',
            'Art. 142 de la Ley de Instituciones de Crédito',
            'Art. 16 Constitucional',
        ],
        'informacion': [
            'Art. 142 de la Ley de Instituciones de Crédito',
            'Art. 117 de la Ley de Instituciones de Crédito',
            'Art. 46 de la Ley de Instituciones de Crédito',
            'Disposiciones de carácter general aplicables a las instituciones de crédito',
        ]
    }

    # Authority faculties by type
    FACULTADES = {
        'IMSS': [
            'Esta oficina para cobros, órgano integrante del Instituto Mexicano del Seguro Social con fundamento en los artículos 251 fracción VII, XXV y XXXVII, 251-A de la Ley del Seguro Social',
            'Instituto Mexicano del Seguro Social con fundamento en los artículos 270, 287 y 291 de la Ley del Seguro Social',
        ],
        'SAT': [
            'Administración General de Auditoría Fiscal Federal con fundamento en los artículos 16 constitucional, 1, 2 y 42 del Código Fiscal de la Federación',
            'Servicio de Administración Tributaria con fundamento en el artículo 42 del Código Fiscal de la Federación',
        ],
        'FGR': [
            'Fiscalía General de la República en ejercicio de las facultades conferidas por los artículos 21 y 102 Constitucional',
            'Subprocuraduría Especializada en Investigación de Delincuencia Organizada con fundamento en el artículo 2 de la LFCDO',
        ],
        'SEIDO': [
            'Subprocuraduría Especializada en Investigación de Delincuencia Organizada con fundamento en el artículo 2 de la LFCDO',
            'SEIDO en ejercicio de las facultades previstas en la Ley Federal contra la Delincuencia Organizada',
        ],
        'UIF': [
            'Unidad de Inteligencia Financiera en ejercicio de las facultades conferidas por el artículo 15 de la LFPIORPI',
            'Secretaría de Hacienda y Crédito Público a través de la UIF con fundamento en el artículo 15 de la LFPIORPI',
        ],
        'PJF': [
            'Juzgado Federal en ejercicio de las facultades conferidas por el artículo 16 Constitucional',
            'Poder Judicial de la Federación con fundamento en los artículos 94 a 107 Constitucional',
        ],
        'INFONAVIT': [
            'Instituto del Fondo Nacional de la Vivienda para los Trabajadores con fundamento en el artículo 30 de la Ley del INFONAVIT',
        ],
        'SHCP': [
            'Secretaría de Hacienda y Crédito Público con fundamento en el artículo 31 de la Ley Orgánica de la Administración Pública Federal',
        ],
        'CONDUSEF': [
            'Comisión Nacional para la Protección y Defensa de los Usuarios de Servicios Financieros con fundamento en el artículo 11 de la Ley de la CONDUSEF',
        ],
    }

    # Requirement type descriptions
    REQUIREMENT_TYPES = {
        'fiscal': 'Requerimiento Hacendario',
        'judicial': 'Orden Judicial',
        'pld': 'Prevención de Lavado de Dinero',
        'aseguramiento': 'Aseguramiento Precautorio',
        'informacion': 'Solicitud de Información',
    }

    def __init__(self):
        """Initialize legal catalog."""
        pass

    def get_articles_for_requirement(self, req_type: str, count: int = 3) -> List[str]:
        """Get random legal articles appropriate for requirement type.

        Args:
            req_type: Type of requirement (fiscal, judicial, pld, aseguramiento, informacion)
            count: Number of articles to return

        Returns:
            List of legal article references
        """
        if req_type not in self.ARTICLES:
            req_type = 'informacion'  # Default fallback

        articles = self.ARTICLES[req_type]
        selected_count = min(count, len(articles))

        return random.sample(articles, selected_count)

    def get_facultades(self, authority_siglas: str) -> str:
        """Get authority faculties text.

        Args:
            authority_siglas: Authority abbreviation (SAT, FGR, UIF, PJF)

        Returns:
            Faculty text string
        """
        if authority_siglas not in self.FACULTADES:
            authority_siglas = 'SAT'  # Default

        return random.choice(self.FACULTADES[authority_siglas])

    def get_requirement_type_name(self, req_type: str) -> str:
        """Get human-readable requirement type name.

        Args:
            req_type: Requirement type code

        Returns:
            Human-readable type name
        """
        return self.REQUIREMENT_TYPES.get(req_type, 'Requerimiento General')

    def generate_fundamento_legal(self, req_type: str) -> str:
        """Generate complete legal foundation text for requirement.

        Args:
            req_type: Type of requirement

        Returns:
            Complete fundamento legal text
        """
        articles = self.get_articles_for_requirement(req_type, count=4)

        # Join articles with commas and final 'y'
        if len(articles) == 1:
            articles_text = articles[0]
        elif len(articles) == 2:
            articles_text = f"{articles[0]} y {articles[1]}"
        else:
            articles_text = ", ".join(articles[:-1]) + f" y {articles[-1]}"

        return f"Con fundamento en {articles_text}, se solicita la siguiente información."

    def generate_motivacion_template(self, req_type: str) -> Dict[str, str]:
        """Generate motivation text template based on requirement type.

        Args:
            req_type: Type of requirement

        Returns:
            Dictionary with motivation template parts
        """
        templates = {
            'fiscal': {
                'intro': 'Mediante diligencia de fecha {{FechaDiligencia}} se practicó dentro del procedimiento administrativo de ejecución',
                'accion': 'embargo sobre los depósitos bancarios en cuentas a nombre del contribuyente',
                'objetivo': 'para la recuperación de los créditos fiscales a favor de {{AutoridadNombre}}',
            },
            'judicial': {
                'intro': 'En cumplimiento a la orden judicial emitida por {{JuzgadoNombre}} con fecha {{FechaDiligencia}}',
                'accion': 'se solicita información sobre cuentas bancarias y movimientos',
                'objetivo': 'relacionados con la averiguación previa {{NumeroExpediente}}',
            },
            'pld': {
                'intro': 'En el marco de las investigaciones relacionadas con posibles operaciones con recursos de procedencia ilícita',
                'accion': 'se requiere información completa de cuentas y operaciones',
                'objetivo': 'para determinar el origen, destino y monto de los recursos investigados',
            },
            'aseguramiento': {
                'intro': 'Con el propósito de asegurar el interés fiscal de la Federación',
                'accion': 'se solicita el aseguramiento precautorio de las cuentas bancarias',
                'objetivo': 'hasta por el monto de {{MontoCredito}} más los accesorios legales que se generen',
            },
            'informacion': {
                'intro': 'En ejercicio de las facultades de comprobación conferidas por la ley',
                'accion': 'se requiere información bancaria del contribuyente {{PersonaNombre}}',
                'objetivo': 'para verificar el cumplimiento de las obligaciones fiscales correspondientes al ejercicio fiscal {{Ejercicio}}',
            },
        }

        return templates.get(req_type, templates['informacion'])

    def generate_instrucciones_cuentas(self, req_type: str) -> str:
        """Generate instructions for account information.

        Args:
            req_type: Type of requirement

        Returns:
            Instructions text
        """
        common_instructions = [
            "Proporcionar información de todas las cuentas bancarias",
            "Incluir números de cuenta, saldos y movimientos",
            "Indicar titulares y co-titulares de las cuentas",
            "Especificar fechas de apertura y cierre de cuentas",
            "Detallar operaciones superiores a $100,000.00 MN",
        ]

        specific_instructions = {
            'aseguramiento': [
                "Proceder al aseguramiento inmediato de las cuentas identificadas",
                "Impedir cualquier disposición de los recursos",
                "Notificar el monto total asegurado",
            ],
            'pld': [
                "Identificar operaciones inusuales o irregulares",
                "Reportar transferencias internacionales",
                "Incluir información de beneficiarios finales",
            ],
        }

        instructions = common_instructions.copy()

        if req_type in specific_instructions:
            instructions.extend(specific_instructions[req_type])

        # Shuffle for variation
        random.shuffle(instructions)

        return "\n".join(f"- {instr}" for instr in instructions[:5])

    def get_sectores_bancarios(self) -> List[str]:
        """Get list of banking sectors that must respond.

        Returns:
            List of banking sector names
        """
        return [
            "Sector Instituciones de Banca Múltiple",
            "Sector Instituciones de Banca de Desarrollo",
            "Sector Casas de Bolsa",
            "Sector Sociedades Financieras Populares",
            "Sector Sociedades Cooperativas de Ahorro y Préstamo",
        ]

    def get_random_sectores(self, count: int = 3) -> str:
        """Get random banking sectors formatted as text.

        Args:
            count: Number of sectors to include

        Returns:
            Formatted sectors text
        """
        sectores = self.get_sectores_bancarios()
        selected = random.sample(sectores, min(count, len(sectores)))

        return "\n".join(selected)
