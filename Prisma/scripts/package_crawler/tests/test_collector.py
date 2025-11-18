"""Tests for the NuGet/GitHub collector."""

import json
from pathlib import Path

import pytest

from scripts.package_crawler import collector


@pytest.fixture(scope="module")
def data_root() -> Path:
    return Path(__file__).parent / "data"


def test_collector_builds_package_artifacts(tmp_path, data_root: Path) -> None:
    packages_lock = data_root / "packages.lock.json"
    output_root = tmp_path / "externals"

    index = collector.collect_packages(
        packages_lock_paths=[packages_lock],
        directory_props_paths=[data_root / "Directory.Packages.props"],
        output_root=output_root,
        local_feed=data_root / "local_feed",
        github_mirror=data_root / "github_mirror",
    )

    assert index["packageCount"] == 3
    logging_entry = next(
        entry for entry in index["packages"] if entry["packageId"] == "Contoso.Logging"
    )
    assert logging_entry["repository"]["url"] == "https://github.com/contoso/logging"

    package_dir = output_root / "Contoso.Logging" / "1.2.3"
    assert (package_dir / "metadata.json").exists()
    assert (package_dir / "docs" / "README.md").exists()
    assert (package_dir / "docs" / "usage.md").exists()
    assert (package_dir / "samples" / "SampleApp.cs").exists()

    api_surface = json.loads((package_dir / "api_surface.json").read_text())
    type_ids = {entry["typeId"] for entry in api_surface["types"]}
    assert "Contoso.Logging.Logger" in type_ids


def test_collector_handles_transitive_packages(tmp_path, data_root: Path) -> None:
    packages_lock = data_root / "packages.lock.json"
    output_root = tmp_path / "externals"

    index = collector.collect_packages(
        packages_lock_paths=[packages_lock],
        directory_props_paths=[data_root / "Directory.Packages.props"],
        output_root=output_root,
        local_feed=data_root / "local_feed",
        github_mirror=data_root / "github_mirror",
    )

    core_entry = next(
        entry for entry in index["packages"] if entry["packageId"] == "Contoso.Core"
    )
    assert core_entry["version"] == "2.0.0"
    assert (output_root / "Contoso.Core" / "2.0.0" / "metadata.json").exists()

    analyzer_entry = next(
        entry
        for entry in index["packages"]
        if entry["packageId"] == "Contoso.Logging.Analyzers"
    )
    assert analyzer_entry["version"] == "0.9.0"
