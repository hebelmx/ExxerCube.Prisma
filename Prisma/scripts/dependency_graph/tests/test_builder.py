"""Tests for the dependency graph builder."""

from pathlib import Path

import pytest

from scripts.dependency_graph import builder


@pytest.fixture(scope="module")
def sample_repo_root() -> Path:
    return Path(__file__).parent.parent.parent / "doc_quality" / "tests" / "data" / "sample_repo"


def test_builder_creates_constructor_and_interface_edges(sample_repo_root: Path) -> None:
    """SampleConsumer should produce constructor + interface edges."""
    graph = builder.build_dependency_graph(
        root=sample_repo_root, include_patterns=["**/*.cs"], domain_first=False
    )

    node_ids = {node["id"] for node in graph["nodes"]}
    assert "ExxerAI.Application.Sample.Services.SampleConsumer" in node_ids
    assert "ExxerAI.Domain.Sample.Entities.SampleEntity" in node_ids
    assert "ExxerAI.Domain.Sample.Entities.ISampleProcessor" in node_ids

    edges = {(edge["source"], edge["target"], edge["relation"]) for edge in graph["edges"]}

    assert (
        "ExxerAI.Application.Sample.Services.SampleConsumer",
        "ExxerAI.Domain.Sample.Entities.SampleEntity",
        "constructor_parameter",
    ) in edges
    assert (
        "ExxerAI.Application.Sample.Services.SampleConsumer",
        "ExxerAI.Domain.Sample.Entities.ISampleProcessor",
        "implements_interface",
    ) in edges


def test_builder_identifies_property_dependency(sample_repo_root: Path) -> None:
    graph = builder.build_dependency_graph(
        root=sample_repo_root, include_patterns=["**/*.cs"], domain_first=False
    )

    property_edges = [
        edge
        for edge in graph["edges"]
        if edge["relation"] == "property_dependency"
        and edge["source"] == "ExxerAI.Application.Sample.Services.SampleConsumer"
    ]

    assert property_edges, "Expected a property dependency edge"
    assert property_edges[0]["target"] == "ExxerAI.Domain.Sample.Entities.SampleEntity"
