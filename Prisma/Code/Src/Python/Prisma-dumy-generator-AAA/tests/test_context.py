import sys
from pathlib import Path

import pytest

sys.path.append(str(Path(__file__).resolve().parents[1]))

from prp1_generator.context import ContextSampler


def build_sampler():
    entities = {
        "autoridades": ["CNBV"],
        "tipos_requerimiento": ["Bloqueo de Cuentas"],
        "subtipos_requerimiento": ["Cuentas de Cheques"],
        "fundamentos_legales": ["Artículo 115 de la LIC"],
        "motivaciones": ["Juicio Ordinario Mercantil"],
        "nombres_personas": ["Juan Pérez", "María Gómez"],
        "empresas": ["Empresa Ficticia"],
        "entidades_financieras": ["Banco Demo"],
        "numeros_expediente": ["123/2024"],
        "montos_comunes": [100000],
        "monedas": ["MXN"],
    }
    summary = {
        "requirement_profiles": [
            {
                "id": "CNBV_Bloqueo",
                "authority": "CNBV",
                "requirement_type": "Bloqueo de Cuentas",
                "mandatory_fields": ["plazoDias"],
                "sla_days": [5, 5],
                "aseguramiento": True,
            }
        ]
    }
    return ContextSampler(entities, summary, seed=1234)


def test_sampler_returns_consistent_metadata():
    sampler = build_sampler()
    metadata, profile = sampler.sample()
    assert metadata["autoridadEmisora"] == "CNBV"
    assert metadata["tipoRequerimiento"] == "Bloqueo de Cuentas"
    assert profile.identifier == "CNBV_Bloqueo"
    assert "plazoDias" in metadata


def test_sampler_injects_profile_id():
    sampler = build_sampler()
    metadata, _ = sampler.sample()
    assert metadata["profileId"] == "CNBV_Bloqueo"
