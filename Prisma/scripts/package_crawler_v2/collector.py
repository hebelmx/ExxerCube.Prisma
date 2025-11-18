"""NuGet package crawler v2 (dotnet nuget download + ILSpy + DocFX)."""

from __future__ import annotations

import argparse
import json
import os
import shlex
import shutil
import subprocess
import tempfile
import zipfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence
import xml.etree.ElementTree as ET


DEFAULT_OUTPUT = Path("artifacts/externals_v2")
DEFAULT_CACHE = Path(".nupkg-cache")
DEFAULT_DOWNLOAD_CMD = ["dotnet", "nuget", "download"]


def _ensure_dir(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)


def _parse_packages_lock(lock_path: Path) -> Dict[str, str]:
    data = json.loads(lock_path.read_text(encoding="utf-8"))
    packages: Dict[str, str] = {}

    def _walk(deps: Dict[str, object]) -> None:
        for name, info in deps.items():
            if isinstance(info, str):
                version = info
                nested = None
            else:
                version = info.get("resolved") or info.get("version")
                nested = info.get("dependencies")
            if version:
                packages.setdefault(name, version)
            if isinstance(nested, dict):
                _walk(nested)

    for framework_deps in data.get("dependencies", {}).values():
        if isinstance(framework_deps, dict):
            _walk(framework_deps)
    return packages


def _collect_packages_from_locks(paths: Sequence[Path]) -> Dict[str, str]:
    aggregated: Dict[str, str] = {}
    for path in paths:
        if path.exists():
            aggregated.update(_parse_packages_lock(path))
    return aggregated


def _parse_directory_packages_props(props_path: Path) -> Dict[str, str]:
    text = props_path.read_text(encoding="utf-8")
    root = ET.fromstring(text)
    packages: Dict[str, str] = {}
    for item_group in root.findall("ItemGroup"):
        for package in item_group.findall("PackageVersion"):
            package_id = package.attrib.get("Include")
            version = package.attrib.get("Version")
            if package_id and version:
                packages.setdefault(package_id, version)
    return packages


def _collect_packages_from_props(paths: Sequence[Path]) -> Dict[str, str]:
    aggregated: Dict[str, str] = {}
    for path in paths:
        if path.exists():
            aggregated.update(_parse_directory_packages_props(path))
    return aggregated


def _shlex_or_none(value: Optional[str]) -> Optional[List[str]]:
    if not value:
        return None
    return shlex.split(value)


def _download_nupkg(
    package_id: str,
    version: str,
    cache_dir: Path,
    *,
    download_cmd: Sequence[str],
    local_nupkg_dir: Optional[Path],
) -> Path:
    cache_dir = cache_dir.resolve()
    _ensure_dir(cache_dir)
    target = cache_dir / f"{package_id}.{version}.nupkg"
    if target.exists():
        return target

    if local_nupkg_dir:
        candidate = (
            local_nupkg_dir
            / package_id
            / version
            / f"{package_id}.{version}.nupkg"
        )
        if candidate.exists():
            shutil.copy2(candidate, target)
            return target

    cmd = list(download_cmd) + [
        package_id,
        "--version",
        version,
        "--outputdirectory",
        str(cache_dir),
    ]
    try:
        subprocess.run(cmd, check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    except subprocess.CalledProcessError as exc:
        raise RuntimeError(
            f"Failed to download {package_id}/{version} via {' '.join(download_cmd)}: {exc.stderr.decode(errors='ignore')}"
        ) from exc
    if not target.exists():
        raise RuntimeError(f"Expected nupkg {target} was not created.")
    return target


def _extract_nupkg(nupkg_path: Path, extract_root: Path) -> Path:
    destination = extract_root / nupkg_path.stem
    if destination.exists():
        return destination
    _ensure_dir(destination)
    with zipfile.ZipFile(nupkg_path, "r") as archive:
        archive.extractall(destination)
    return destination


def _parse_nuspec(extracted_dir: Path) -> dict:
    nuspecs = list(extracted_dir.glob("*.nuspec"))
    if not nuspecs:
        return {
            "packageId": extracted_dir.name,
            "version": "",
            "description": "",
            "projectUrl": None,
            "licenseUrl": None,
            "repository": None,
            "tags": [],
        }
    tree = ET.parse(nuspecs[0])
    ns = {"ns": "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"}
    metadata = tree.find("ns:metadata", ns)
    if metadata is None:
        metadata = tree.find("metadata")

    def _text(tag: str) -> Optional[str]:
        node = metadata.find(f"ns:{tag}", ns) if metadata is not None else None
        if node is None and metadata is not None:
            node = metadata.find(tag)
        if node is not None and node.text:
            return node.text.strip()
        return None

    repository_node = (
        metadata.find("ns:repository", ns)
        if metadata is not None
        else None
    )
    if repository_node is None and metadata is not None:
        repository_node = metadata.find("repository")
    repository = (
        {
            "type": repository_node.attrib.get("type"),
            "url": repository_node.attrib.get("url"),
            "branch": repository_node.attrib.get("branch"),
        }
        if repository_node is not None
        else None
    )

    tags_text = _text("tags") or ""
    tags = [tag.strip() for tag in tags_text.split() if tag.strip()]

    return {
        "packageId": _text("id") or extracted_dir.name,
        "version": _text("version") or "",
        "description": _text("description") or "",
        "projectUrl": _text("projectUrl"),
        "licenseUrl": _text("licenseUrl"),
        "repository": repository,
        "tags": tags,
    }


def _copy_tree_if_exists(source: Path, destination: Path) -> List[str]:
    if not source.exists():
        return []
    copied: List[str] = []
    for path in source.rglob("*"):
        if path.is_file():
            relative = path.relative_to(source)
            target = destination / relative
            _ensure_dir(target.parent)
            shutil.copy2(path, target)
            copied.append(str(target))
    return copied


def _copy_docs(extracted_dir: Path, package_dir: Path) -> List[str]:
    docs_dir = package_dir / "docs"
    copied: List[str] = []
    readme_candidates = [
        extracted_dir / "README.md",
        extracted_dir / "readme.md",
        extracted_dir / "README.txt",
    ]
    for candidate in readme_candidates:
        if candidate.exists():
            target = docs_dir / candidate.name
            _ensure_dir(target.parent)
            shutil.copy2(candidate, target)
            copied.append(str(target))
            break
    docs_folder = extracted_dir / "docs"
    copied.extend(_copy_tree_if_exists(docs_folder, docs_dir))
    return copied


def _copy_samples(extracted_dir: Path, package_dir: Path) -> List[str]:
    samples_dir = package_dir / "samples"
    return _copy_tree_if_exists(extracted_dir / "samples", samples_dir)


def _list_library_dlls(extracted_dir: Path) -> List[Path]:
    libs: List[Path] = []
    for dll in extracted_dir.glob("lib/**/*.dll"):
        libs.append(dll)
    return libs


def _detect_command(explicit: Optional[str], env_var: str, default: Optional[str]) -> Optional[List[str]]:
    if explicit:
        return _shlex_or_none(explicit)
    env_value = os.environ.get(env_var)
    if env_value:
        return _shlex_or_none(env_value)
    if default:
        found = shutil.which(default)
        if found:
            return [found]
    return None


def _run_ilspy(dlls: List[Path], package_dir: Path, ilspy_cmd: Optional[Sequence[str]]) -> List[str]:
    if not dlls or not ilspy_cmd:
        return []
    outputs: List[str] = []
    api_dir = package_dir / "api_ilspy"
    _ensure_dir(api_dir)
    for dll in dlls:
        output_file = api_dir / f"{dll.stem}.txt"
        cmd = list(ilspy_cmd) + [str(dll), str(output_file)]
        try:
            subprocess.run(cmd, check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            outputs.append(str(output_file))
        except FileNotFoundError:
            break
        except subprocess.CalledProcessError:
            continue
    return outputs


def _run_docfx_metadata(extracted_dir: Path, package_dir: Path, docfx_cmd: Optional[Sequence[str]]) -> Optional[str]:
    if not docfx_cmd:
        return None
    docfx_work = package_dir / ".docfx"
    _ensure_dir(docfx_work)
    docfx_json = docfx_work / "docfx.json"
    docfx_output = package_dir / "docfx_api"
    dll_patterns = []
    for dll in extracted_dir.glob("lib/**/*.dll"):
        relative = dll.relative_to(extracted_dir).as_posix()
        dll_patterns.append(relative)
    if not dll_patterns:
        return None
    docfx_config = {
        "metadata": [
            {
                "src": [
                    {
                        "files": dll_patterns,
                        "cwd": ".",
                    }
                ],
                "dest": str(docfx_output.relative_to(package_dir)),
            }
        ]
    }
    docfx_json.write_text(json.dumps(docfx_config, indent=2), encoding="utf-8")
    cmd = list(docfx_cmd) + ["metadata", str(docfx_json)]
    try:
        subprocess.run(
            cmd,
            check=True,
            cwd=package_dir,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
    except FileNotFoundError:
        return None
    except subprocess.CalledProcessError:
        return None
    return str(docfx_output)


def collect_packages_v2(
    *,
    packages_lock_paths: Sequence[Path],
    directory_props_paths: Optional[Sequence[Path]],
    output_root: Path,
    cache_dir: Path,
    download_cmd: Sequence[str],
    ilspy_cmd: Optional[Sequence[str]],
    docfx_cmd: Optional[Sequence[str]],
    local_nupkg_dir: Optional[Path] = None,
    only_packages: Optional[Sequence[str]] = None,
) -> dict:
    output_root = output_root.resolve()
    cache_dir = cache_dir.resolve()
    _ensure_dir(output_root)
    _ensure_dir(cache_dir)

    packages = _collect_packages_from_locks(packages_lock_paths)
    default_packages: Set[str] = set()
    if directory_props_paths:
        props_packages = _collect_packages_from_props(directory_props_paths)
        default_packages = set(props_packages.keys())
        packages.update(props_packages)
    all_packages = dict(packages)

    only_set = set(only_packages) if only_packages else None
    if only_set is not None:
        packages = {pkg: ver for pkg, ver in packages.items() if pkg in only_set}

    index_path = output_root / "index.json"
    existing_entries: Dict[str, dict] = {}
    if index_path.exists():
        previous = json.loads(index_path.read_text(encoding="utf-8"))
        for entry in previous.get("packages", []):
            existing_entries[entry.get("packageId")] = entry

    if not packages:
        if index_path.exists():
            return json.loads(index_path.read_text(encoding="utf-8"))
        empty_index = {
            "generatedAt": datetime.now(timezone.utc).isoformat(),
            "packageCount": 0,
            "packages": [],
        }
        index_path.write_text(json.dumps(empty_index, indent=2), encoding="utf-8")
        return empty_index

    entries: List[dict] = []
    extract_root = cache_dir / "extracted"
    for package_id, version in sorted(packages.items()):
        try:
            nupkg_path = _download_nupkg(
                package_id,
                version,
                cache_dir,
                download_cmd=download_cmd,
                local_nupkg_dir=local_nupkg_dir,
            )
        except RuntimeError as exc:
            entries.append(
                {
                    "packageId": package_id,
                    "version": version,
                    "error": str(exc),
                }
            )
            continue

        extracted_dir = _extract_nupkg(nupkg_path, extract_root)
        package_dir = output_root / package_id / version
        _ensure_dir(package_dir)

        metadata = _parse_nuspec(extracted_dir)
        docs = _copy_docs(extracted_dir, package_dir)
        samples = _copy_samples(extracted_dir, package_dir)
        dlls = _list_library_dlls(extracted_dir)
        ilspy_outputs = _run_ilspy(dlls, package_dir, ilspy_cmd)
        docfx_output = _run_docfx_metadata(extracted_dir, package_dir, docfx_cmd)

        entry = {
            "packageId": metadata.get("packageId", package_id),
            "version": metadata.get("version", version),
            "description": metadata.get("description", ""),
            "projectUrl": metadata.get("projectUrl"),
            "licenseUrl": metadata.get("licenseUrl"),
            "repository": metadata.get("repository"),
            "tags": metadata.get("tags", []),
            "isDefaultProject": package_id in default_packages,
            "artifacts": {
                "root": str(package_dir),
                "nupkg": str(nupkg_path),
                "docs": docs,
                "samples": samples,
                "apiIlspy": ilspy_outputs,
                "docfxApi": docfx_output,
            },
        }
        entries.append(entry)

    entry_map: Dict[str, dict]
    if only_set is None:
        entry_map = {entry["packageId"]: entry for entry in entries}
    else:
        entry_map = existing_entries
        for entry in entries:
            entry_map[entry["packageId"]] = entry

    # Drop packages that are no longer referenced.
    full_reference_ids = set(all_packages)
    for package_id in list(entry_map.keys()):
        if package_id not in full_reference_ids:
            entry_map.pop(package_id, None)

    sorted_ids = sorted(entry_map)
    final_entries = [entry_map[pkg_id] for pkg_id in sorted_ids]
    index = {
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "packageCount": len(final_entries),
        "packages": final_entries,
    }
    index_path.write_text(json.dumps(index, indent=2), encoding="utf-8")
    return index


def _parse_args(argv: Optional[Sequence[str]] = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="NuGet crawler v2")
    parser.add_argument(
        "--packages-lock",
        action="append",
        dest="packages_lock",
        help="Path to packages.lock.json (can repeat)",
    )
    parser.add_argument(
        "--directory-props",
        action="append",
        dest="directory_props",
        help="Path to Directory.Packages.props (can repeat)",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=DEFAULT_OUTPUT,
        help="Artifacts output root",
    )
    parser.add_argument(
        "--cache",
        type=Path,
        default=DEFAULT_CACHE,
        help="NuGet cache directory",
    )
    parser.add_argument(
        "--download-cmd",
        dest="download_cmd",
        help="Override download command (default dotnet nuget download)",
    )
    parser.add_argument(
        "--ilspy-cmd",
        dest="ilspy_cmd",
        help="Command used to invoke ILSpy (default env PACKAGE_CRAWLER_V2_ILSPY or ilspycmd if found)",
    )
    parser.add_argument(
        "--docfx-cmd",
        dest="docfx_cmd",
        help="Command used to invoke DocFX (default env PACKAGE_CRAWLER_V2_DOCFX or docfx if found)",
    )
    parser.add_argument(
        "--local-nupkg-dir",
        dest="local_nupkg_dir",
        type=Path,
        help="Optional local directory containing <id>/<version>/<id>.<version>.nupkg (test hook)",
    )
    parser.add_argument(
        "--package",
        dest="packages",
        action="append",
        help="Limit crawl to specific package id(s).",
    )
    return parser.parse_args(argv)


def main(argv: Optional[Sequence[str]] = None) -> int:
    args = _parse_args(argv)
    lock_paths = [Path(p) for p in args.packages_lock] if args.packages_lock else []
    props_paths = [Path(p) for p in args.directory_props] if args.directory_props else None
    download_cmd = _shlex_or_none(args.download_cmd) or DEFAULT_DOWNLOAD_CMD
    ilspy_cmd = _detect_command(args.ilspy_cmd, "PACKAGE_CRAWLER_V2_ILSPY", "ilspycmd")
    docfx_cmd = _detect_command(args.docfx_cmd, "PACKAGE_CRAWLER_V2_DOCFX", "docfx")
    collect_packages_v2(
        packages_lock_paths=lock_paths,
        directory_props_paths=props_paths,
        output_root=args.output,
        cache_dir=args.cache,
        download_cmd=download_cmd,
        ilspy_cmd=ilspy_cmd,
        docfx_cmd=docfx_cmd,
        local_nupkg_dir=args.local_nupkg_dir,
        only_packages=args.packages,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
