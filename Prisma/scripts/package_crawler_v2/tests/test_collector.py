"""Tests for package crawler v2 collector."""

import json
import zipfile
from pathlib import Path

import pytest

from scripts.package_crawler_v2 import collector


@pytest.fixture(scope="module")
def data_root() -> Path:
    return Path(__file__).parent / "data"


def _build_nupkg_sources(data_root: Path, tmp_path: Path) -> Path:
    source_root = data_root / "package_sources"
    cache_root = tmp_path / "nupkgs"
    for package_id_dir in source_root.iterdir():
        if not package_id_dir.is_dir():
            continue
        package_id = package_id_dir.name
        for version_dir in package_id_dir.iterdir():
            if not version_dir.is_dir():
                continue
            version = version_dir.name
            target_dir = cache_root / package_id / version
            target_dir.mkdir(parents=True, exist_ok=True)
            nupkg_path = target_dir / f"{package_id}.{version}.nupkg"
            with zipfile.ZipFile(nupkg_path, "w") as archive:
                for file in version_dir.rglob("*"):
                    if file.is_file():
                        archive.write(file, arcname=file.relative_to(version_dir))
    return cache_root


def test_collector_v2_extracts_docs_and_api(tmp_path, data_root: Path) -> None:
    nupkg_cache = _build_nupkg_sources(data_root, tmp_path)
    output_root = tmp_path / "externals_v2"
    cache_dir = tmp_path / "cache"
    fake_ilspy = Path(__file__).parent / "bin" / "fake_ilspy.py"
    fake_docfx = Path(__file__).parent / "bin" / "fake_docfx.py"

    index = collector.collect_packages_v2(
        packages_lock_paths=[data_root / "packages.lock.json"],
        directory_props_paths=[data_root / "Directory.Packages.props"],
        output_root=output_root,
        cache_dir=cache_dir,
        download_cmd=["python", "-c", "import sys; sys.exit('unexpected download')"],
        ilspy_cmd=["python", str(fake_ilspy)],
        docfx_cmd=["python", str(fake_docfx)],
        local_nupkg_dir=nupkg_cache,
    )

    assert index["packageCount"] == 2
    logging_entry = next(entry for entry in index["packages"] if entry["packageId"] == "Contoso.Logging")
    assert logging_entry["artifacts"]["docs"]
    assert logging_entry["artifacts"]["samples"]
    assert logging_entry["artifacts"]["apiIlspy"]
    assert logging_entry["artifacts"]["docfxApi"]
    assert logging_entry["isDefaultProject"] is False

    analyzer_entry = next(entry for entry in index["packages"] if entry["packageId"] == "Contoso.Logging.Analyzers")
    assert analyzer_entry["version"] == "0.9.0"
    assert analyzer_entry["isDefaultProject"] is True

    readme_path = Path(logging_entry["artifacts"]["docs"][0])
    assert readme_path.read_text(encoding="utf-8").startswith("# Contoso Logging")


def test_collector_v2_writes_index(tmp_path, data_root: Path) -> None:
    nupkg_cache = _build_nupkg_sources(data_root, tmp_path)
    output_root = tmp_path / "externals_v2"
    cache_dir = tmp_path / "cache"
    fake_ilspy = Path(__file__).parent / "bin" / "fake_ilspy.py"

    collector.collect_packages_v2(
        packages_lock_paths=[data_root / "packages.lock.json"],
        directory_props_paths=None,
        output_root=output_root,
        cache_dir=cache_dir,
        download_cmd=["python", "-c", "import sys; sys.exit('unexpected download')"],
        ilspy_cmd=["python", str(fake_ilspy)],
        docfx_cmd=None,
        local_nupkg_dir=nupkg_cache,
    )

    index_path = output_root / "index.json"
    assert index_path.exists()
    data = json.loads(index_path.read_text(encoding="utf-8"))
    assert data["packageCount"] == 1


def test_collector_v2_filters_specific_packages(tmp_path, data_root: Path) -> None:
    nupkg_cache = _build_nupkg_sources(data_root, tmp_path)
    output_root = tmp_path / "externals_v2"
    cache_dir = tmp_path / "cache"
    fake_ilspy = Path(__file__).parent / "bin" / "fake_ilspy.py"

    # Initial full crawl
    collector.collect_packages_v2(
        packages_lock_paths=[data_root / "packages.lock.json"],
        directory_props_paths=[data_root / "Directory.Packages.props"],
        output_root=output_root,
        cache_dir=cache_dir,
        download_cmd=["python", "-c", "import sys; sys.exit('unexpected download')"],
        ilspy_cmd=["python", str(fake_ilspy)],
        docfx_cmd=None,
        local_nupkg_dir=nupkg_cache,
    )

    # Second crawl limited to a single package
    index = collector.collect_packages_v2(
        packages_lock_paths=[data_root / "packages.lock.json"],
        directory_props_paths=[data_root / "Directory.Packages.props"],
        output_root=output_root,
        cache_dir=cache_dir,
        download_cmd=["python", "-c", "import sys; sys.exit('unexpected download')"],
        ilspy_cmd=["python", str(fake_ilspy)],
        docfx_cmd=None,
        local_nupkg_dir=nupkg_cache,
        only_packages=["Contoso.Logging"],
    )

    assert index["packageCount"] == 2
    package_ids = {entry["packageId"] for entry in index["packages"]}
    assert package_ids == {"Contoso.Logging", "Contoso.Logging.Analyzers"}
