"""Variation engine for generating credible document variations."""

import random
from typing import Dict, List, Optional
from enum import Enum


class DocumentPersona(Enum):
    """Different bureaucratic personas for document generation."""
    FORMAL_METICULOUS = "formal_meticulous"      # Very detailed, formal language
    RUSHED_PRACTICAL = "rushed_practical"         # Brief, to the point
    VERBOSE_ELABORATE = "verbose_elaborate"       # Long-winded, overly detailed
    TECHNICAL_PRECISE = "technical_precise"       # Technical legal language
    CASUAL_INFORMAL = "casual_informal"           # Less formal, more conversational


class NarrativeStyle(Enum):
    """Different narrative styles for legal text."""
    CHRONOLOGICAL = "chronological"    # Events in time order
    LEGAL_FIRST = "legal_first"        # Legal framework first, then facts
    FACT_BASED = "fact_based"          # Facts first, then legal reasoning
    FORMAL_ACADEMIC = "formal_academic" # Academic legal style


class VariationEngine:
    """Engine for generating credible document variations."""

    # Different ways to phrase common legal concepts
    PHRASE_VARIATIONS = {
        'solicitar': ['requerir', 'pedir', 'demandar', 'solicitar'],
        'información': ['información', 'datos', 'documentación', 'antecedentes'],
        'proporcionar': ['proporcionar', 'entregar', 'suministrar', 'facilitar', 'hacer llegar'],
        'con fundamento en': ['con fundamento en', 'con base en', 'de conformidad con', 'en virtud de'],
        'derivado de': ['derivado de', 'como resultado de', 'en consecuencia de', 'producto de'],
        'se requiere': ['se requiere', 'es necesario', 'se solicita', 'se necesita'],
        'en virtud de': ['en virtud de', 'por medio de', 'mediante', 'a través de'],
        'la presente': ['la presente', 'este oficio', 'el presente documento', 'esta solicitud'],
    }

    # Sentence structure templates
    SENTENCE_STRUCTURES = {
        'simple': "{subject} {verb} {object}.",
        'compound': "{subject} {verb} {object}, y {additional_clause}.",
        'complex': "Dado que {premise}, {subject} {verb} {object}.",
        'formal': "Es menester que {subject} {verb} {object} conforme a derecho.",
    }

    # Different opening styles for sections
    SECTION_OPENINGS = {
        'direct': "Se {verb} la siguiente información:",
        'formal': "Por medio del presente, se {verb} respetuosamente:",
        'legal': "Con fundamento en las disposiciones aplicables, se {verb}:",
        'contextual': "Derivado de las investigaciones en curso, se {verb}:",
    }

    def __init__(self, seed: Optional[int] = None):
        """Initialize variation engine.

        Args:
            seed: Random seed for reproducibility
        """
        if seed:
            random.seed(seed)

    def get_persona_description(self, persona: DocumentPersona) -> Dict[str, str]:
        """Get LLM system prompt for specific persona.

        Args:
            persona: Document persona

        Returns:
            Dictionary with persona description and style guides
        """
        personas = {
            DocumentPersona.FORMAL_METICULOUS: {
                'description': 'Eres un funcionario público meticuloso y muy formal. Escribes con gran detalle, usando lenguaje legal preciso. Cada punto debe estar claramente fundamentado.',
                'style': 'formal, detallado, con múltiples referencias legales',
                'sentence_length': 'largas (25-35 palabras)',
                'vocabulary': 'técnico-legal avanzado',
            },
            DocumentPersona.RUSHED_PRACTICAL: {
                'description': 'Eres un funcionario con mucho trabajo que escribe de manera práctica y directa. Vas al grano sin rodeos innecesarios.',
                'style': 'directo, conciso, práctico',
                'sentence_length': 'cortas (10-15 palabras)',
                'vocabulary': 'simple pero correcto',
            },
            DocumentPersona.VERBOSE_ELABORATE: {
                'description': 'Eres un funcionario que le gusta elaborar extensamente. Usas múltiples sinónimos, redundancias y explicaciones detalladas.',
                'style': 'verboso, elaborado, con muchas aclaraciones',
                'sentence_length': 'muy largas (35-50 palabras)',
                'vocabulary': 'rico en sinónimos y perífrasis',
            },
            DocumentPersona.TECHNICAL_PRECISE: {
                'description': 'Eres un abogado técnico que usa lenguaje legal preciso. Referencias exactas a artículos, fracciones e incisos.',
                'style': 'técnico, preciso, con citas legales exactas',
                'sentence_length': 'medias (15-25 palabras)',
                'vocabulary': 'terminología legal específica',
            },
            DocumentPersona.CASUAL_INFORMAL: {
                'description': 'Eres un funcionario que escribe de manera más informal (pero correcta). Lenguaje accesible sin perder la formalidad mínima requerida.',
                'style': 'menos formal, más accesible',
                'sentence_length': 'medias (12-20 palabras)',
                'vocabulary': 'estándar, evitando tecnicismos excesivos',
            },
        }

        return personas.get(persona, personas[DocumentPersona.FORMAL_METICULOUS])

    def apply_phrase_variations(self, text: str) -> str:
        """Apply random phrase variations to text.

        Args:
            text: Original text

        Returns:
            Text with varied phrases
        """
        result = text

        for original, variations in self.PHRASE_VARIATIONS.items():
            if original.lower() in result.lower():
                # Choose a random variation
                variation = random.choice(variations)

                # Replace with case matching
                import re
                pattern = re.compile(re.escape(original), re.IGNORECASE)

                def replace_with_case(match):
                    matched = match.group(0)
                    if matched.isupper():
                        return variation.upper()
                    elif matched[0].isupper():
                        return variation.capitalize()
                    else:
                        return variation.lower()

                # Replace first occurrence only to maintain some consistency
                result = pattern.sub(replace_with_case, result, count=1)

        return result

    def vary_section_order(self, data: Dict, style: NarrativeStyle) -> Dict:
        """Vary the emphasis and order of sections based on narrative style.

        Args:
            data: Document data
            style: Narrative style to apply

        Returns:
            Modified data with style-specific emphasis
        """
        # Add metadata to guide template rendering
        data['narrative_style'] = style.value

        if style == NarrativeStyle.LEGAL_FIRST:
            # Emphasize legal framework first
            data['section_order'] = ['facultades', 'fundamento', 'motivacion', 'instrucciones']
            data['emphasis'] = 'legal'

        elif style == NarrativeStyle.FACT_BASED:
            # Emphasize facts and motivation first
            data['section_order'] = ['motivacion', 'origen', 'fundamento', 'instrucciones']
            data['emphasis'] = 'factual'

        elif style == NarrativeStyle.CHRONOLOGICAL:
            # Follow chronological order
            data['section_order'] = ['antecedentes', 'motivacion', 'fundamento', 'instrucciones']
            data['emphasis'] = 'chronological'

        elif style == NarrativeStyle.FORMAL_ACADEMIC:
            # Very structured, academic approach
            data['section_order'] = ['fundamento', 'facultades', 'motivacion', 'instrucciones']
            data['emphasis'] = 'academic'

        return data

    def generate_varied_opening(self, req_type: str, persona: DocumentPersona) -> str:
        """Generate varied opening text for document.

        Args:
            req_type: Requirement type
            persona: Document persona

        Returns:
            Opening text
        """
        openings = {
            'fiscal': [
                "En ejercicio de las facultades conferidas a esta autoridad fiscal,",
                "Por medio del presente oficio y con fundamento en la normativa aplicable,",
                "Derivado del procedimiento de fiscalización en curso,",
                "En cumplimiento a las disposiciones fiscales vigentes,",
            ],
            'judicial': [
                "En cumplimiento a la orden judicial emitida,",
                "Por mandato de la autoridad jurisdiccional competente,",
                "En atención al oficio judicial de referencia,",
                "Dando cumplimiento a lo ordenado por el Juez,",
            ],
            'pld': [
                "En el marco de las investigaciones por operaciones sospechosas,",
                "Derivado de los análisis de inteligencia financiera,",
                "Con motivo de la detección de operaciones irregulares,",
                "En seguimiento a reportes de operaciones inusuales,",
            ],
        }

        opening_list = openings.get(req_type, openings['fiscal'])

        # Choose based on persona
        if persona == DocumentPersona.RUSHED_PRACTICAL:
            return random.choice(opening_list[:2])  # Prefer shorter openings
        elif persona == DocumentPersona.VERBOSE_ELABORATE:
            # Return longer version
            base = random.choice(opening_list)
            return f"{base} y considerando las disposiciones aplicables en la materia,"
        else:
            return random.choice(opening_list)

    def generate_varied_closing(self, persona: DocumentPersona) -> str:
        """Generate varied closing text.

        Args:
            persona: Document persona

        Returns:
            Closing text
        """
        closings = {
            DocumentPersona.FORMAL_METICULOUS: [
                "Sin otro particular por el momento, quedo de usted con la más distinguida consideración.",
                "En espera de su amable respuesta en los términos solicitados, reciba un cordial saludo.",
            ],
            DocumentPersona.RUSHED_PRACTICAL: [
                "En espera de respuesta.",
                "Favor de atender a la brevedad.",
            ],
            DocumentPersona.VERBOSE_ELABORATE: [
                "Sin más por el momento y en espera de contar con su valiosa colaboración en el cumplimiento de las disposiciones legales aplicables, aprovecho la ocasión para enviarle un cordial saludo.",
            ],
            DocumentPersona.TECHNICAL_PRECISE: [
                "Se reitera la obligación legal de dar respuesta en el plazo establecido.",
                "El cumplimiento de lo solicitado es de carácter obligatorio conforme a derecho.",
            ],
            DocumentPersona.CASUAL_INFORMAL: [
                "Agradezco su atención.",
                "Quedo al pendiente de su respuesta.",
            ],
        }

        return random.choice(closings.get(persona, closings[DocumentPersona.FORMAL_METICULOUS]))

    def vary_amount_presentation(self, amount: float) -> Dict[str, str]:
        """Generate varied ways to present monetary amounts.

        Args:
            amount: Monetary amount

        Returns:
            Dictionary with different presentations
        """
        variations = []

        # Format variations
        variations.append(f"${amount:,.2f} MN")
        variations.append(f"${amount:,.2f} M.N.")
        variations.append(f"${amount:,.2f} (Moneda Nacional)")
        variations.append(f"la cantidad de ${amount:,.2f}")
        variations.append(f"un monto de ${amount:,.2f} MN")

        return {
            'short': random.choice(variations[:2]),
            'long': random.choice(variations[2:]),
            'varied': random.choice(variations),
        }

    def vary_date_format(self, date_str: str) -> str:
        """Generate varied date formats.

        Args:
            date_str: Date string in DD/MM/YYYY format

        Returns:
            Varied date format
        """
        if '/' not in date_str:
            return date_str

        formats = [
            date_str,  # 21/11/2025
            date_str.replace('/', '-'),  # 21-11-2025
            # Could add more like "21 de noviembre de 2025" with date parsing
        ]

        return random.choice(formats)

    def select_random_persona(self) -> DocumentPersona:
        """Select random document persona.

        Returns:
            Random persona
        """
        return random.choice(list(DocumentPersona))

    def select_random_narrative_style(self) -> NarrativeStyle:
        """Select random narrative style.

        Returns:
            Random narrative style
        """
        return random.choice(list(NarrativeStyle))

    def get_variation_summary(self, persona: DocumentPersona,
                             narrative: NarrativeStyle) -> Dict:
        """Get summary of applied variations.

        Args:
            persona: Selected persona
            narrative: Selected narrative style

        Returns:
            Summary dictionary
        """
        return {
            'persona': persona.value,
            'narrative_style': narrative.value,
            'persona_description': self.get_persona_description(persona),
        }
