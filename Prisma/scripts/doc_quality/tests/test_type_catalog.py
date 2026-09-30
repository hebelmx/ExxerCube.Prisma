"""Tests for the type catalog builder."""

from pathlib import Path

import pytest

from scripts.doc_quality import type_catalog


@pytest.fixture(scope="module")
def sample_repo_root() -> Path:
    """Path to the synthetic repository used for tests."""
    return Path(__file__).parent / "data" / "sample_repo"


def test_build_type_catalog_captures_domain_types(sample_repo_root: Path) -> None:
    """Domain-first scan should capture SampleEntity metadata."""
    result = type_catalog.build_type_catalog(root=sample_repo_root)

    assert result["totalTypes"] == 2
    type_map = {entry["typeId"]: entry for entry in result["types"]}
    sample_type = type_map["ExxerAI.Domain.Sample.Entities.SampleEntity"]
    assert sample_type["layer"] == "Domain"
    assert sample_type["project"].endswith("ExxerAI.Domain.Sample.csproj")
    assert sample_type["xmlDoc"]["summary"].startswith(
        "Represents a simple document ingested"
    )
    assert sample_type["hasRemarks"] is True


def test_build_type_catalog_can_include_non_domain_projects(sample_repo_root: Path) -> None:
    """Explicit include glob should surface non-Domain types as well."""
    result = type_catalog.build_type_catalog(
        root=sample_repo_root, include_patterns=["**/*.cs"], domain_first=False
    )

    type_ids = {entry["typeId"] for entry in result["types"]}
    assert "ExxerAI.Application.Sample.Services.SampleConsumer" in type_ids
