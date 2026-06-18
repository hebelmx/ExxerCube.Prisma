"""LLM client for generating legal narrative text using Ollama."""

import json
import requests
from typing import Dict, List, Optional
from dataclasses import dataclass


@dataclass
class LLMConfig:
    """Configuration for LLM client."""
    base_url: str = "http://localhost:11434"
    model: str = "llama2"
    temperature: float = 0.7
    max_tokens: int = 500
    timeout: int = 30


class OllamaClient:
    """Client for interacting with Ollama API for text generation."""

    def __init__(self, config: Optional[LLMConfig] = None):
        """Initialize Ollama client.

        Args:
            config: LLM configuration (uses defaults if None)
        """
        self.config = config or LLMConfig()

    def is_available(self) -> bool:
        """Check if Ollama is available and running.

        Returns:
            True if Ollama is accessible, False otherwise
        """
        try:
            response = requests.get(f"{self.config.base_url}/api/tags", timeout=5)
            return response.status_code == 200
        except:
            return False

    def list_models(self) -> List[str]:
        """List available Ollama models.

        Returns:
            List of model names
        """
        try:
            response = requests.get(f"{self.config.base_url}/api/tags", timeout=5)
            if response.status_code == 200:
                data = response.json()
                return [model['name'] for model in data.get('models', [])]
            return []
        except:
            return []

    def generate(self, prompt: str, system_prompt: Optional[str] = None) -> str:
        """Generate text using Ollama.

        Args:
            prompt: User prompt
            system_prompt: Optional system prompt to guide behavior

        Returns:
            Generated text
        """
        url = f"{self.config.base_url}/api/generate"

        payload = {
            "model": self.config.model,
            "prompt": prompt,
            "stream": False,
            "options": {
                "temperature": self.config.temperature,
                "num_predict": self.config.max_tokens,
            }
        }

        if system_prompt:
            payload["system"] = system_prompt

        try:
            response = requests.post(
                url,
                json=payload,
                timeout=self.config.timeout
            )

            if response.status_code == 200:
                result = response.json()
                return result.get('response', '').strip()
            else:
                raise RuntimeError(f"Ollama API error: {response.status_code} - {response.text}")

        except requests.exceptions.Timeout:
            raise RuntimeError(f"Ollama timeout after {self.config.timeout} seconds")
        except Exception as e:
            raise RuntimeError(f"Ollama generation error: {e}")


class LegalTextGenerator:
    """Generate legal narrative text for CNBV requirements using LLM."""

    # System prompt for Spanish legal text generation
    SYSTEM_PROMPT = """Eres un asistente especializado en redacción de documentos legales mexicanos.
Tu tarea es generar texto legal realista en español mexicano para requerimientos bancarios.

Características del texto que debes generar:
- Idioma: español mexicano formal
- Tono: burocrático y legal
- Estilo: formal, con referencias a leyes mexicanas
- Incluye nombres de leyes, artículos y dependencias gubernamentales mexicanas
- Usa terminología legal bancaria mexicana
- El texto debe ser coherente pero puede tener pequeños errores tipográficos naturales

NO uses inglés. TODO debe estar en español."""

    def __init__(self, ollama_client: Optional[OllamaClient] = None):
        """Initialize legal text generator.

        Args:
            ollama_client: Ollama client instance (creates default if None)
        """
        self.ollama = ollama_client or OllamaClient()

    def generate_facultades(self, authority_data: Dict, persona_prompt: str = None) -> str:
        """Generate 'Facultades de la Autoridad' text.

        Args:
            authority_data: Dictionary with authority information
            persona_prompt: Optional persona-specific system prompt

        Returns:
            Generated legal text for authority faculties
        """
        prompt = f"""Genera un párrafo de 100-150 palabras en español describiendo las facultades legales de la autoridad "{authority_data.get('nombre', 'Autoridad')}".

Tipo de autoridad: {authority_data.get('tipo', 'fiscal')}
Área específica: {authority_data.get('area', '')}

El texto debe:
- Mencionar artículos de leyes mexicanas relevantes
- Usar lenguaje legal formal mexicano
- Incluir referencias a facultades de investigación, auditoría o revisión
- Ser coherente y realista

Genera SOLO el párrafo, sin título."""

        system = persona_prompt if persona_prompt else self.SYSTEM_PROMPT
        return self.ollama.generate(prompt, system_prompt=system)

    def generate_motivacion(self, req_type: str, context: Dict) -> str:
        """Generate 'Motivación del Requerimiento' text.

        Args:
            req_type: Type of requirement (fiscal, judicial, pld, etc.)
            context: Context data (person name, dates, amounts, etc.)

        Returns:
            Generated motivation text
        """
        req_type_names = {
            'fiscal': 'requerimiento fiscal',
            'judicial': 'orden judicial',
            'pld': 'investigación de prevención de lavado de dinero',
            'aseguramiento': 'aseguramiento precautorio',
            'informacion': 'solicitud de información',
        }

        tipo_texto = req_type_names.get(req_type, 'requerimiento')

        prompt = f"""Genera un párrafo de 150-200 palabras en español explicando la motivación de un {tipo_texto}.

Contexto:
- Persona investigada: {context.get('persona_nombre', 'Contribuyente')}
- Fecha de diligencia: {context.get('fecha', '01/01/2025')}
- Monto involucrado: {context.get('monto', '$1,000,000.00')}
- Número de expediente: {context.get('expediente', 'EXP-1234-2025')}

El texto debe:
- Explicar por qué se requiere la información bancaria
- Mencionar el procedimiento administrativo o judicial
- Incluir referencias temporales y montos
- Usar lenguaje legal formal mexicano
- Ser coherente con el tipo de requerimiento

Genera SOLO el párrafo, sin título."""

        return self.ollama.generate(prompt, system_prompt=self.SYSTEM_PROMPT)

    def generate_instrucciones(self, req_type: str, sectores: List[str]) -> str:
        """Generate 'Instrucciones sobre las cuentas por conocer' text.

        Args:
            req_type: Type of requirement
            sectores: List of banking sectors

        Returns:
            Generated instructions text
        """
        sectores_text = ", ".join(sectores)

        prompt = f"""Genera instrucciones detalladas en español (150-200 palabras) sobre qué información bancaria se requiere.

Tipo de requerimiento: {req_type}
Sectores bancarios: {sectores_text}

Las instrucciones deben:
- Especificar qué datos de cuentas bancarias se necesitan
- Mencionar números de cuenta, saldos, movimientos, titulares
- Incluir rangos de fechas y montos mínimos
- Especificar el formato de entrega de información
- Mencionar plazos de respuesta
- Usar lenguaje legal formal mexicano

Genera un párrafo corrido o una lista detallada."""

        return self.ollama.generate(prompt, system_prompt=self.SYSTEM_PROMPT)

    def generate_antecedentes(self, context: Dict) -> str:
        """Generate 'Antecedentes' background text.

        Args:
            context: Context information

        Returns:
            Generated background text
        """
        prompt = f"""Genera un párrafo de 100-150 palabras en español describiendo los antecedentes de un caso.

Contexto:
- Tipo de caso: {context.get('tipo', 'fiscal')}
- Sujetos involucrados: {context.get('sujetos', 'Contribuyente')}
- Periodo investigado: {context.get('periodo', '2024-2025')}

El texto debe:
- Explicar brevemente el origen del caso
- Mencionar auditorías, revisiones o investigaciones previas
- Incluir fechas y referencias a procedimientos
- Usar lenguaje legal formal mexicano

Genera SOLO el párrafo, sin título."""

        return self.ollama.generate(prompt, system_prompt=self.SYSTEM_PROMPT)

    def generate_complete_narrative(self, req_data: Dict) -> Dict[str, str]:
        """Generate all narrative sections for a requirement.

        Args:
            req_data: Complete requirement data

        Returns:
            Dictionary with generated text sections
        """
        generated = {}

        # Generate facultades
        if 'authority' in req_data:
            generated['FacultadesTexto'] = self.generate_facultades(req_data['authority'])

        # Generate motivacion
        context = {
            'persona_nombre': req_data.get('Persona_Nombre', ''),
            'fecha': req_data.get('FechaDiligencia', ''),
            'monto': req_data.get('MontoCredito', ''),
            'expediente': req_data.get('Cnbv_NumeroExpediente', ''),
        }
        generated['MotivacionTexto'] = self.generate_motivacion(
            req_data.get('tipo', 'fiscal'),
            context
        )

        # Generate instrucciones
        sectores = req_data.get('SectoresBancarios', '').split('\n')
        generated['InstruccionesCuentasPorConocer'] = self.generate_instrucciones(
            req_data.get('tipo', 'fiscal'),
            sectores
        )

        return generated

    def enhance_with_variations(self, text: str) -> str:
        """Create slight variations of text for uniqueness.

        Args:
            text: Original text

        Returns:
            Varied text
        """
        prompt = f"""Reescribe el siguiente texto legal en español manteniendo el mismo significado pero con ligeras variaciones en la redacción:

{text}

Cambios permitidos:
- Reordenar algunas frases
- Usar sinónimos legales
- Cambiar conectores (sin embargo, no obstante, por tanto)
- Mantener el tono formal y legal

Genera SOLO el texto reescrito, sin comentarios adicionales."""

        return self.ollama.generate(prompt, system_prompt=self.SYSTEM_PROMPT)
