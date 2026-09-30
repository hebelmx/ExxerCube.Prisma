"""NuGet/GitHub package collector."""

from __future__ import annotations

import argparse
import json
import shutil
import tempfile
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, List, Optional, Sequence
from zipfile import ZipFile

from scripts.doc_quality.scanner import CSharpFileParser, ProjectLocator

NUGET_FLAT_CONTAINER = "https://api.nuget.org/v3-flatcontainer"


def _ensure_dir(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)


def _lower_id(package_id: str) -> str:
    return package_id.lower()


def _parse_packages_lock(lock_path: Path) -> Dict[str, str]:
    data = json.loads(lock_path.read_text(encoding="utf-8"))
    packages: Dict[str, str] = {}

    def _walk(deps: Dict[str, dict]) -> None:
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

    dependencies = data.get("dependencies", {})
    for framework_deps in dependencies.values():
        _walk(framework_deps)

    return packages


def _collect_packages_from_locks(paths: Sequence[Path]) -> Dict[str, str]:
    aggregated: Dict[str, str] = {}
    for path in paths:
        if not path.exists():
            continue
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
        if not path.exists():
            continue
        aggregated.update(_parse_directory_packages_props(path))
    return aggregated


def _load_local_metadata(
    package_id: str, version: str, local_feed: Optional[Path]
) -> Optional[dict]:
    if not local_feed:
        return None
    candidate = (
        local_feed
        / _lower_id(package_id)
        / version
        / "metadata.json"
    )
    if candidate.exists():
        return json.loads(candidate.read_text(encoding="utf-8"))
    return None


def _fetch_nuspec_metadata(package_id: str, version: str) -> dict:
    package_lower = _lower_id(package_id)
    url = f"{NUGET_FLAT_CONTAINER}/{package_lower}/{version}/{package_lower}.nuspec"
    with urllib.request.urlopen(url) as response:  # nosec B310
        xml_content = response.read().decode("utf-8")
    root = ET.fromstring(xml_content)
    metadata = root.find("{http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd}metadata")
    if metadata is None:
        metadata = root.find("metadata")
    def _text(tag: str) -> Optional[str]:
        node = metadata.find(tag) if metadata is not None else None
        return node.text.strip() if node is not None and node.text else None

    repository_node = metadata.find("repository") if metadata is not None else None
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
        "packageId": package_id,
        "version": version,
        "description": _text("description") or "",
        "projectUrl": _text("projectUrl"),
        "licenseUrl": _text("licenseUrl"),
        "repository": repository,
        "tags": tags,
    }


def _resolve_metadata(
    package_id: str,
    version: str,
    *,
    local_feed: Optional[Path],
) -> dict:
    metadata = _load_local_metadata(package_id, version, local_feed)
    if metadata:
        return metadata
    try:
        return _fetch_nuspec_metadata(package_id, version)
    except Exception as exc:  # pragma: no cover (network errors)
        return {
            "packageId": package_id,
            "version": version,
            "description": "",
            "projectUrl": None,
            "licenseUrl": None,
            "repository": None,
            "tags": [],
            "error": f"Failed to fetch metadata: {exc}",
        }


def _repo_mirror_path(repo_url: str, mirror_root: Optional[Path]) -> Optional[Path]:
    if not mirror_root:
        return None
    parsed = urllib.parse.urlparse(repo_url)
    parts = Path(parsed.path.strip("/")).parts
    if len(parts) < 2:
        return None
    owner, repo = parts[0], parts[1].replace(".git", "")
    candidate = mirror_root / owner.lower() / repo.lower()
    return candidate if candidate.exists() else None


def _download_github_repo(repo_url: str, branch: Optional[str]) -> Path:
    parsed = urllib.parse.urlparse(repo_url)
    parts = Path(parsed.path.strip("/")).parts
    if len(parts) < 2:
        raise ValueError(f"Cannot parse GitHub repo from {repo_url}")
    owner, repo = parts[0], parts[1].replace(".git", "")
    branch_name = branch or "main"
    zip_url = f"https://codeload.github.com/{owner}/{repo}/zip/refs/heads/{branch_name}"
    with urllib.request.urlopen(zip_url) as response:  # nosec B310
        data = response.read()
    temp_dir = Path(tempfile.mkdtemp(prefix="pkg_repo_"))
    zip_path = temp_dir / f"{repo}.zip"
    zip_path.write_bytes(data)
    with ZipFile(zip_path) as archive:
        archive.extractall(temp_dir)
    extracted_dirs = [child for child in temp_dir.iterdir() if child.is_dir()]
    if not extracted_dirs:
        raise RuntimeError(f"Failed to extract repository {repo_url}")
    return extracted_dirs[0]


def _copy_if_exists(source: Path, destination: Path) -> Optional[Path]:
    if not source.exists():
        return None
    _ensure_dir(destination.parent)
    shutil.copy2(source, destination)
    return destination


def _copy_docs(repo_dir: Path, package_dir: Path) -> List[str]:
    docs_dir = package_dir / "docs"
    copied: List[str] = []
    readme = repo_dir / "README.md"
    readme_target = docs_dir / "README.md"
    copied_path = _copy_if_exists(readme, readme_target)
    if copied_path:
        copied.append(str(copied_path))

    for path in repo_dir.glob("docs/**/*.md"):
        target = docs_dir / path.relative_to(repo_dir / "docs")
        copied_path = _copy_if_exists(path, target)
        if copied_path:
            copied.append(str(copied_path))
    return copied


def _copy_samples(repo_dir: Path, package_dir: Path) -> List[str]:
    samples_dir = package_dir / "samples"
    copied: List[str] = []
    for path in repo_dir.glob("samples/**/*"):
        if path.is_file():
            target = samples_dir / path.relative_to(repo_dir / "samples")
            copied_path = _copy_if_exists(path, target)
            if copied_path:
                copied.append(str(copied_path))
    return copied


def _analyze_api_surface(repo_dir: Path, package_dir: Path) -> Path:
    type_entries: List[dict] = []
    locator = ProjectLocator(repo_dir)
    for cs_file in repo_dir.rglob("*.cs"):
        project = locator.find(cs_file)
        parser = CSharpFileParser(repo_dir, cs_file, project, "External")
        types, _ = parser.parse()
        for type_info in types:
            entry = {
                "typeId": type_info.type_id(),
                "name": type_info.name,
                "namespace": type_info.namespace,
                "file": type_info.relative_file(repo_dir),
                "kind": type_info.kind,
            }
            type_entries.append(entry)
    api_surface = {
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "types": type_entries,
        "typeCount": len(type_entries),
    }
    path = package_dir / "api_surface.json"
    path.write_text(json.dumps(api_surface, indent=2), encoding="utf-8")
    return path


def _process_package(
    package_id: str,
    version: str,
    *,
    output_root: Path,
    local_feed: Optional[Path],
    github_mirror: Optional[Path],
) -> dict:
    package_dir = output_root / package_id / version
    _ensure_dir(package_dir)

    metadata = _resolve_metadata(package_id, version, local_feed=local_feed)
    metadata_path = package_dir / "metadata.json"
    metadata_path.write_text(json.dumps(metadata, indent=2), encoding="utf-8")

    repo_url = (metadata.get("repository") or {}).get("url")
    docs_files: List[str] = []
    sample_files: List[str] = []
    api_surface_path: Optional[Path] = None
    repo_dir: Optional[Path] = None

    if repo_url:
        repo_dir = _repo_mirror_path(repo_url, github_mirror)
        if not repo_dir:
            branch = (metadata.get("repository") or {}).get("branch")
            try:
                repo_dir = _download_github_repo(repo_url, branch)
            except Exception:
                repo_dir = None

    if repo_dir:
        docs_files = _copy_docs(repo_dir, package_dir)
        sample_files = _copy_samples(repo_dir, package_dir)
        api_surface_path = _analyze_api_surface(repo_dir, package_dir)

    entry = {
        "packageId": package_id,
        "version": version,
        "description": metadata.get("description", ""),
        "projectUrl": metadata.get("projectUrl"),
        "licenseUrl": metadata.get("licenseUrl"),
        "repository": metadata.get("repository"),
        "tags": metadata.get("tags", []),
        "artifacts": {
            "root": str(package_dir),
            "metadata": str(metadata_path),
            "apiSurface": str(api_surface_path) if api_surface_path else None,
            "docs": docs_files,
            "samples": sample_files,
        },
    }
    return entry


def collect_packages(
    *,
    packages_lock_paths: Sequence[Path],
    directory_props_paths: Optional[Sequence[Path]] = None,
    output_root: Path,
    local_feed: Optional[Path] = None,
    github_mirror: Optional[Path] = None,
) -> dict:
    """Collect NuGet package metadata and repository assets."""
    output_root = output_root.resolve()
    _ensure_dir(output_root)
    packages = _collect_packages_from_locks(packages_lock_paths)
    if directory_props_paths:
        packages.update(_collect_packages_from_props(directory_props_paths))
    entries: List[dict] = []
    for package_id, version in sorted(packages.items()):
        entries.append(
            _process_package(
                package_id,
                version,
                output_root=output_root,
                local_feed=local_feed,
                github_mirror=github_mirror,
            )
        )

    index = {
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "packageCount": len(entries),
        "packages": entries,
    }
    index_path = output_root / "index.json"
    index_path.write_text(json.dumps(index, indent=2), encoding="utf-8")
    return index


def _parse_args(argv: Optional[Sequence[str]] = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Collect NuGet/GitHub package metadata.")
    parser.add_argument(
        "--packages-lock",
        action="append",
        dest="packages_lock",
        help="Path to packages.lock.json (can repeat).",
    )
    parser.add_argument(
        "--directory-props",
        action="append",
        dest="directory_props",
        help="Path to Directory.Packages.props (can repeat).",
    )
    parser.add_argument(
        "--output",
        dest="output",
        type=Path,
        default=Path("artifacts/externals"),
        help="Output root directory.",
    )
    parser.add_argument(
        "--local-feed",
        dest="local_feed",
        type=Path,
        help="Optional local feed directory with cached metadata JSON.",
    )
    parser.add_argument(
        "--github-mirror",
        dest="github_mirror",
        type=Path,
        help="Optional local mirror of GitHub repositories.",
    )
    return parser.parse_args(argv)


def main(argv: Optional[Sequence[str]] = None) -> int:
    args = _parse_args(argv)
    lock_paths = [Path(p) for p in args.packages_lock] if args.packages_lock else []
    props_paths = [Path(p) for p in args.directory_props] if args.directory_props else []
    collect_packages(
        packages_lock_paths=lock_paths,
        directory_props_paths=props_paths,
        output_root=args.output,
        local_feed=args.local_feed,
        github_mirror=args.github_mirror,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
