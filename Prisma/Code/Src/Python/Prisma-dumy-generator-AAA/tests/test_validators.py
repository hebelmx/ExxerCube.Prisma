import sys
from pathlib import Path

import pytest

sys.path.append(str(Path(__file__).resolve().parents[1]))

from prp1_generator.validators import MetadataValidator, ValidationError


def test_validator_enforces_schema_and_required_fields():
    schema = {
        "type": "object",
        "required": ["fecha"],
        "properties": {"fecha": {"type": "string"}},
    }
    summary = {"requirement_profiles": [{"id": "perfil", "mandatory_fields": ["plazoDias"]}]}
    validator = MetadataValidator(schema, summary)
    metadata = {"fecha": "2024-05-06", "plazoDias": 5}
    validator.validate(metadata, "perfil")  # no exception


def test_validator_raises_on_missing_field():
    schema = {"type": "object", "properties": {}}
    summary = {"requirement_profiles": [{"id": "perfil", "mandatory_fields": ["plazoDias"]}]}
    validator = MetadataValidator(schema, summary)
    with pytest.raises(ValidationError):
        validator.validate({}, "perfil")
