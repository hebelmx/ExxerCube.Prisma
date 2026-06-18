"""Context sampling utilities using entities and PRP1 fixture summaries."""

from __future__ import annotations

import random
from dataclasses import dataclass, field
from datetime import datetime, timedelta
from typing import Any, Dict, Iterable, List, Optional, Sequence, Tuple


SCHEMA_TYPES = [
    "Embargo Precautorio",
    "Embargo Ejecutivo",
    "Aseguramiento de Cuentas",
    "Congelamiento de Activos",
    "Inmovilización de Valores",
    "Retención de Fondos",
    "Bloqueo de Cuentas",
    "Suspensión de Operaciones",
    "Información Financiera",
    "Estados de Cuenta",
    "Movimientos Bancarios",
    "Transferencias Internacionales",
    "Depósitos en Garantía",
    "Liberación de Fondos",
    "Levantamiento de Embargo",
]

CANONICAL_TYPE_MAP = {
    "aseguramiento": "Aseguramiento de Cuentas",
    "aseguramiento de cuentas": "Aseguramiento de Cuentas",
    "bloqueo": "Bloqueo de Cuentas",
    "inmovilización": "Inmovilización de Valores",
    "inmovilizacion": "Inmovilización de Valores",
    "información": "Información Financiera",
    "informacion": "Información Financiera",
    "transferencia": "Transferencias Internacionales",
    "documentación": "Información Financiera",
    "documentacion": "Información Financiera",
}


def _canonical_requirement_type(label: Optional[str]) -> str:
    if not label:
        return "Información Financiera"
    normalized = label.strip()
    if not normalized:
        return "Información Financiera"
    lower = normalized.lower()
    for schema_value in SCHEMA_TYPES:
        if lower == schema_value.lower():
            return schema_value
    for key, target in CANONICAL_TYPE_MAP.items():
        if key in lower:
            return target
    return "Información Financiera"


@dataclass
class RequirementProfile:
    """Represents a requirement archetype extracted from PRP1 fixtures."""

    identifier: str
    authority: str
    requirement_kind: str
    subtype: Optional[str] = None
    prompt_hints: Sequence[str] = field(default_factory=list)
    mandatory_fields: Sequence[str] = field(default_factory=list)
    sla_days: Tuple[int, int] = (3, 10)
    aseguramiento_required: bool = False
    error_bias: Sequence[str] = field(default_factory=list)


def _profiles_from_summary(summary: Optional[Dict[str, Any]]) -> List[RequirementProfile]:
    """Convert parsed PRP1 summary JSON into RequirementProfile objects."""
    if not summary:
        return []

    profiles: List[RequirementProfile] = []
    for item in summary.get("requirement_profiles", []):
        profiles.append(
            RequirementProfile(
                identifier=item.get("id") or item.get("name", "perfil"),
                authority=item.get("authority", "Autoridad Desconocida"),
                requirement_kind=_canonical_requirement_type(item.get("requirement_type")),
                subtype=item.get("subtype"),
                prompt_hints=item.get("hints", []),
                mandatory_fields=item.get("mandatory_fields", []),
                sla_days=tuple(item.get("sla_days", (3, 10))),
                aseguramiento_required=item.get("aseguramiento", False),
                error_bias=item.get("error_bias", []),
            )
        )
    return profiles


class ContextSampler:
    """Generates metadata payloads consistent with schema + PRP1 fixtures."""

    def __init__(
        self,
        entities: Dict[str, Sequence[Any]],
        summary: Optional[Dict[str, Any]],
        seed: Optional[int] = None,
    ) -> None:
        self.entities = entities
        self.random = random.Random(seed)
        self.profiles = _profiles_from_summary(summary)

    def _rand(self, seq: Sequence[Any]) -> Any:
        return self.random.choice(seq)

    def _random_date(self) -> str:
        start = datetime.now() - timedelta(days=400)
        end = datetime.now()
        random_days = self.random.randint(0, (end - start).days)
        return (start + timedelta(days=random_days)).strftime("%Y-%m-%d")

    def _build_profile_context(self, batch: Optional[str]) -> RequirementProfile:
        if self.profiles:
            if batch:
                filtered = [p for p in self.profiles if batch.lower() in p.identifier.lower()]
                if filtered:
                    return self.random.choice(filtered)
            return self.random.choice(self.profiles)
        # default lightweight rule-set when no profiles available
        return RequirementProfile(
            identifier="default",
            authority=self._rand(self.entities.get("autoridades", ["Autoridad"])),
            requirement_kind=self._rand(self.entities.get("tipos_requerimiento", ["Información Financiera"])),
        )

    def _inject_noise(self, text: str, profile: RequirementProfile) -> str:
        """Insert small human-like mistakes without breaking meaning."""
        if not text:
            return text
        tokens = text.split()
        if len(tokens) < 5:
            return text
        mistakes = profile.error_bias or ["omision", "abreviac."]
        if self.random.random() < 0.35:
            idx = self.random.randint(0, len(tokens) - 1)
            tokens[idx] = tokens[idx].replace("s", "z", 1) if "s" in tokens[idx] else tokens[idx] + ","
        if self.random.random() < 0.25:
            tokens.insert(
                self.random.randint(1, len(tokens) - 2),
                mistakes[self.random.randrange(len(mistakes))],
            )
        return " ".join(tokens)

    def sample(self, batch: Optional[str] = None) -> tuple[Dict[str, Any], RequirementProfile]:
        profile = self._build_profile_context(batch)

        monto = None
        moneda = self._rand(self.entities.get("monedas", ["MXN"]))
        if profile.requirement_kind.lower() in {"embargo precautorio", "embargo ejecutivo", "aseguramiento de cuentas", "retención de fondos"}:
            monto = float(self._rand(self.entities.get("montos_comunes", [500000])))

        partes_pool = self.entities.get("nombres_personas", []) + self.entities.get("empresas", [])
        partes = self.random.sample(partes_pool, k=min(len(partes_pool), self.random.randint(1, 3))) if partes_pool else []

        descripcion = (
            f"Se requiere a {self._rand(self.entities.get('entidades_financieras', ['Institución']))} "
            f"detallar operaciones relacionadas con {', '.join(partes[:2])}. "
            f"El requerimiento se emite dentro del expediente {self._rand(self.entities.get('numeros_expediente', ['0000/2024']))} "
            f"por la autoridad {profile.authority}."
        )
        descripcion = self._inject_noise(descripcion, profile)

        detalle = {
            "descripcion": descripcion,
            "monto": monto,
            "moneda": moneda,
        }
        if monto is None:
            detalle.pop("monto")

        metadata: Dict[str, Any] = {
            "fecha": self._random_date(),
            "autoridadEmisora": profile.authority,
            "expediente": self._rand(self.entities.get("numeros_expediente", ["0000/2024"])),
            "tipoRequerimiento": profile.requirement_kind,
            "subtipoRequerimiento": profile.subtype or self._rand(self.entities.get("subtipos_requerimiento", ["Cuentas de Cheques"])),
            "fundamentoLegal": self._rand(self.entities.get("fundamentos_legales", ["Artículo 115 LIC"])),
            "motivacion": self._rand(self.entities.get("motivaciones", ["Juicio Ordinario Mercantil"])),
            "partes": partes,
            "detalle": detalle,
        }

        if profile.aseguramiento_required:
            metadata["aseguramiento"] = True

        sla_days = self.random.randint(*profile.sla_days)
        metadata["plazoDias"] = sla_days
        metadata["plazoDescripcion"] = self._inject_noise(
            f"Se otorga un plazo improrrogable de {sla_days} días hábiles.",
            profile,
        )

        hints: Iterable[str] = profile.prompt_hints or []
        metadata["promptHints"] = list(hints)
        metadata["profileId"] = profile.identifier
        return metadata, profile
