"""Tests for the package crawler query helpers."""

import json
from pathlib import Path

import pytest

from scripts.package_crawler import collector, query


@pytest.fixture(scope="module")
def data_root() -> Path:
    return Path(__file__).parent / "data"


@pytest.fixture
def package_index(tmp_path, data_root: Path) -> Path:
    output_root = tmp_path / "externals"
    packages_lock = data_root / "packages.lock.json"
    index = collector.collect_packages(
        packages_lock_paths=[packages_lock],
        directory_props_paths=[data_root / "Directory.Packages.props"],
        output_root=output_root,
        local_feed=data_root / "local_feed",
        github_mirror=data_root / "github_mirror",
    )
    index_path = output_root / "index.json"
    index_path.write_text(json.dumps(index, indent=2))
    return index_path


def test_find_package_returns_matching_entry(package_index: Path) -> None:
    index_data = json.loads(package_index.read_text())
    entry = query.find_package(index_data=index_data, package_id="Contoso.Logging")
    assert entry is not None
    assert entry["version"] == "1.2.3"


def test_find_package_filters_version(package_index: Path) -> None:
    index_data = json.loads(package_index.read_text())
    entry = query.find_package(
        index_data=index_data,
        package_id="Contoso.Core",
        version="2.0.0",
    )
    assert entry["packageId"] == "Contoso.Core"
