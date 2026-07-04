"""Shared substitution for leaking `{{key}}` template tokens.

The legal Motivación templates in ``core/legal_catalog.py``
(``generate_motivacion_template``) embed literal ``{{PlaceholderName}}``
tokens (e.g. ``{{NumeroExpediente}}``) that were never substituted before
being written into the generated document body. That made gold fields such
as ``Cnbv_NumeroExpediente`` recoverable ONLY from the XML companion file --
never from the PDF/DOCX/MD/HTML body text an OCR/extraction pipeline actually
reads. This module fixes that: it substitutes every known placeholder with
its real value from the generator's own `data` dict, and fails loudly
(``KeyError``) rather than silently leaving a literal ``{{...}}`` token in
the rendered document.
"""

import re
from typing import Dict, List, Optional

# Regex for a literal `{{Name}}` template token.
_PLACEHOLDER_RE = re.compile(r'\{\{(\w+)\}\}')


# Maps the literal `{{PlaceholderName}}` token (as it appears inside the
# Motivación templates in legal_catalog.py) to the key holding its real,
# already-computed value inside the `data` dict assembled by
# `main_generator.CNBVFixtureGenerator._generate_requirement_data`.
#
# NOTE: `JuzgadoNombre` and `Ejercicio` have no other field in `data` --
# `_generate_requirement_data` synthesizes them specifically so this map
# never has to fall back to leaving the literal placeholder in place:
#   - JuzgadoNombre <- the REQUESTING authority's own official name
#     (`authority_data['nombre']`). NOTE this is a different concept from
#     `AutoridadNombre`, which is hardcoded to the CNBV (the *receiving*
#     regulator) by deliberate design elsewhere in this file -- see the
#     comment on `AutoridadNombre` in `_generate_requirement_data`. Using
#     "Juzgado" for a non-judicial requesting authority is a naming
#     imprecision inherited from the original template wording; flagged as
#     a known caveat, not silently hidden.
#   - Ejercicio <- reuses `Cnbv_OficioYear` (current year) as a stand-in for
#     "ejercicio fiscal" (fiscal year); the generator does not model a
#     distinct fiscal-year concept.
MOTIVACION_PLACEHOLDER_MAP: Dict[str, str] = {
    'NumeroExpediente': 'Cnbv_NumeroExpediente',
    'FechaDiligencia': 'FechaDiligencia',
    'JuzgadoNombre': 'JuzgadoNombre',
    'AutoridadNombre': 'AutoridadNombre',
    'MontoCredito': 'MontoCredito',
    'PersonaNombre': 'Persona_Nombre',
    'Ejercicio': 'Ejercicio',
}


def find_placeholders(text: str) -> List[str]:
    """Return the literal `{{Name}}` placeholder names present in `text` (in order, with duplicates)."""
    return _PLACEHOLDER_RE.findall(text)


def substitute_placeholders(text: str, data: Dict,
                             placeholder_map: Optional[Dict[str, str]] = None) -> str:
    """Replace every `{{Name}}` token in `text` with its real value from `data`.

    Args:
        text: Raw text that may contain literal `{{Name}}` tokens.
        data: The assembled document data dict (gold values).
        placeholder_map: Maps `{{Name}}` token name -> key in `data`.
            Defaults to `MOTIVACION_PLACEHOLDER_MAP`.

    Returns:
        `text` with every placeholder replaced by `str(data[data_key])`.
        A no-op (returns `text` unchanged) if it contains no `{{...}}` tokens.

    Raises:
        KeyError: if a placeholder in `text` has no entry in `placeholder_map`,
            or its mapped `data` key is missing/empty. This is intentional --
            printing a literal, un-substituted `{{...}}` token into a
            generated document is exactly the defect this module exists to
            eliminate, so we fail loudly instead of leaking the template
            token silently.
    """
    if placeholder_map is None:
        placeholder_map = MOTIVACION_PLACEHOLDER_MAP

    result = text
    for name in find_placeholders(text):
        token = "{{" + name + "}}"
        if name not in placeholder_map:
            raise KeyError(
                f"Template placeholder '{token}' has no entry in placeholder_map; "
                f"map it to a data key or remove it from the template -- it must "
                f"never print literally into a generated document."
            )

        data_key = placeholder_map[name]
        value = data.get(data_key)
        if value in (None, ''):
            raise KeyError(
                f"Template placeholder '{token}' maps to data['{data_key}'] which is "
                f"missing/empty; cannot substitute without leaking the literal token "
                f"'{token}' into the rendered document."
            )

        result = result.replace(token, str(value))

    return result
