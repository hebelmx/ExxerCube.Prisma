"""Tests for the doc linter heuristics."""

from pathlib import Path

import pytest

from scripts.doc_quality import doc_linter, type_catalog, type_usage


@pytest.fixture(scope="module")
def sample_repo_root() -> Path:
    return Path(__file__).parent / "data" / "sample_repo"


@pytest.fixture(scope="module")
def catalog_and_usage(sample_repo_root: Path):
    catalog = type_catalog.build_type_catalog(
        root=sample_repo_root, include_patterns=["**/*.cs"], domain_first=False
    )
    usage = type_usage.build_type_usage(
        root=sample_repo_root,
        catalog_data=catalog,
        include_patterns=["**/*.cs"],
        domain_first=False,
    )
    return catalog, usage


def test_doc_linter_flags_missing_param_docs(catalog_and_usage, sample_repo_root: Path) -> None:
    catalog, usage = catalog_and_usage
    report = doc_linter.build_doc_quality_report(
        root=sample_repo_root, catalog_data=catalog, usage_data=usage
    )

    rules = {(issue["rule"], issue.get("member")) for issue in report["issues"]}
    assert ("MissingParamDoc", "Process") in rules
    assert ("SummaryMeaningless", "Process") in rules

    process_issue = next(
        issue
        for issue in report["issues"]
        if issue["rule"] == "MissingParamDoc" and issue.get("member") == "Process"
    )
    assert process_issue["context"]["typeImportance"] == 3
