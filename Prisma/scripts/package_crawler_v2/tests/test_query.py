"""Tests for package crawler v2 query helpers."""

import json
import zipfile
from pathlib import Path

import pytest

from scripts.package_crawler_v2 import collector, query


@pytest.fixture(scope="module")
def data_root() -> Path:
    return Path(__file__).parent / "data"


def _prepare_index(tmp_path: Path, data_root: Path) -> Path:
    from .test_collector import _build_nupkg_sources

    nupkg_cache = _build_nupkg_sources(data_root, tmp_path)
    output_root = tmp_path / "externals_v2"
    cache_dir = tmp_path / "cache"
    collector.collect_packages_v2(
        packages_lock_paths=[data_root / "packages.lock.json"],
        directory_props_paths=[data_root / "Directory.Packages.props"],
        output_root=output_root,
        cache_dir=cache_dir,
        download_cmd=["python", "-c", "import sys; sys.exit('unexpected download')"],
        ilspy_cmd=None,
        docfx_cmd=None,
        local_nupkg_dir=nupkg_cache,
    )
    return output_root / "index.json"


def test_query_finds_package(tmp_path, data_root: Path) -> None:
    index_path = _prepare_index(tmp_path, data_root)
    index_data = json.loads(index_path.read_text(encoding="utf-8"))
    entry = query.find_package(index_data=index_data, package_id="Contoso.Logging")
    assert entry is not None
    assert entry["version"] == "1.2.3"


def test_query_filters_version(tmp_path, data_root: Path) -> None:
    index_path = _prepare_index(tmp_path, data_root)
    index_data = json.loads(index_path.read_text(encoding="utf-8"))
    entry = query.find_package(index_data=index_data, package_id="Contoso.Logging.Analyzers", version="0.9.0")
    assert entry is not None


def test_query_defaults_to_props_packages(tmp_path, data_root: Path) -> None:
    index_path = _prepare_index(tmp_path, data_root)
    index_data = json.loads(index_path.read_text(encoding="utf-8"))
    default_entries = [entry for entry in index_data["packages"] if entry.get("isDefaultProject")]
    assert default_entries
