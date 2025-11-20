"""Fallback template rendering when Ollama is unavailable."""

from __future__ import annotations

import random
from string import Template
from typing import Dict, Sequence


class FallbackTemplateRenderer:
    """Renders deterministic templates when LLM output is not available."""

    def __init__(self, templates: Sequence[str], seed: int = 0) -> None:
        self.templates = templates or [
            (
                "OFICIO ${expediente}\\n"
                "Autoridad: ${autoridadEmisora}\\n"
                "Se requiere a ${entidad_financiera:-Institución Financiera} "
                "remitir detalles de ${tipoRequerimiento} respecto a ${partes}. "
                "Plazo concedido: ${plazoDias} días."
            )
        ]
        self.random = random.Random(seed)

    def render(self, metadata: Dict[str, object]) -> str:
        template_text = self.random.choice(self.templates)
        template = Template(template_text)
        safe_mapping = {k: v for k, v in metadata.items()}
        detalle = metadata.get("detalle") or {}
        if isinstance(detalle, dict):
            safe_mapping.update({f"detalle_{k}": v for k, v in detalle.items()})
        return template.safe_substitute(safe_mapping)
