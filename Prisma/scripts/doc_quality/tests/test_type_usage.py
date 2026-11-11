"""Tests for the type usage graph."""

from pathlib import Path

import pytest

from scripts.doc_quality import type_catalog, type_usage


@pytest.fixture(scope="module")
def sample_repo_root() -> Path:
    return Path(__file__).parent / "data" / "sample_repo"


@pytest.fixture(scope="module")
def full_catalog(sample_repo_root: Path):
    """Build a catalog that covers Domain + Application for usage analysis."""
    return type_catalog.build_type_catalog(
        root=sample_repo_root, include_patterns=["**/*.cs"], domain_first=False
    )


def test_usage_graph_detects_consumer_relationships(
    sample_repo_root: Path, full_catalog
) -> None:
    """SampleConsumer should appear as a consumer of SampleEntity."""
    result = type_usage.build_type_usage(
        root=sample_repo_root,
        catalog_data=full_catalog,
        include_patterns=["**/*.cs"],
        domain_first=False,
    )

    assert result["totalTargets"] == 3  # SampleEntity + SampleConsumer + interface

    sample_entity_usage = next(
        usage for usage in result["usages"] if usage["targetType"].endswith("SampleEntity")
    )
    relations = {(item["relation"], item["consumerType"]) for item in sample_entity_usage["consumers"]}

    assert ("parameter", "ExxerAI.Application.Sample.Services.SampleConsumer") in relations
    assert ("property", "ExxerAI.Application.Sample.Services.SampleConsumer") in relations
    assert ("instantiation", "ExxerAI.Application.Sample.Services.SampleConsumer") in relations
