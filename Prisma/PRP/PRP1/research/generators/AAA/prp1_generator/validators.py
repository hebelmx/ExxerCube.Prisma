"""Metadata validation: JSON schema + PRP1-derived rules."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Dict, Iterable, Optional

import jsonschema


class ValidationError(RuntimeError):
    """Raised when metadata fails validation."""


@dataclass
class MetadataValidator:
    schema: Dict[str, Any]
    summary: Optional[Dict[str, Any]] = None

    def __post_init__(self) -> None:
        self.validator = jsonschema.Draft7Validator(self.schema)

    def validate(self, metadata: Dict[str, Any], profile_id: Optional[str] = None) -> None:
        errors = sorted(self.validator.iter_errors(metadata), key=lambda e: e.path)
        if errors:
            raise ValidationError("; ".join(err.message for err in errors))

        # Enforce additional fields derived from PRP1 profiles
        required_fields = self._required_fields(profile_id)
        for field in required_fields:
            if field not in metadata:
                raise ValidationError(f"Metadata missing field '{field}' for profile {profile_id}")

        plazo = metadata.get("plazoDias")
        if plazo is not None and int(plazo) <= 0:
            raise ValidationError("plazoDias debe ser mayor a cero")

        partes = metadata.get("partes")
        if isinstance(partes, Iterable) and not list(partes):
            raise ValidationError("partes debe contener al menos un elemento")

    def _required_fields(self, profile_id: Optional[str]) -> Iterable[str]:
        if not profile_id or not self.summary:
            return []
        for profile in self.summary.get("requirement_profiles", []):
            if profile.get("id") == profile_id or profile.get("name") == profile_id:
                return profile.get("mandatory_fields", [])
        return []
