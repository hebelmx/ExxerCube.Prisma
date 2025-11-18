"""Tests for dependency graph queries."""

from pathlib import Path

import pytest

from scripts.dependency_graph import builder, query


@pytest.fixture(scope="module")
def sample_repo_root() -> Path:
    return Path(__file__).parent.parent.parent / "doc_quality" / "tests" / "data" / "sample_repo"


@pytest.fixture(scope="module")
def sample_graph(sample_repo_root: Path):
    return builder.build_dependency_graph(
        root=sample_repo_root, include_patterns=["**/*.cs"], domain_first=False
    )


def test_find_dependents_returns_constructor_consumers(sample_graph: dict) -> None:
    dependents = query.find_dependents(
        graph_data=sample_graph,
        target_id="ExxerAI.Domain.Sample.Entities.SampleEntity",
    )

    assert "ExxerAI.Application.Sample.Services.SampleConsumer" in dependents


def test_find_dependents_can_filter_relations(sample_graph: dict) -> None:
    dependents = query.find_dependents(
        graph_data=sample_graph,
        target_id="ExxerAI.Domain.Sample.Entities.SampleEntity",
        relations={"property_dependency"},
    )

    assert dependents == ["ExxerAI.Application.Sample.Services.SampleConsumer"]
