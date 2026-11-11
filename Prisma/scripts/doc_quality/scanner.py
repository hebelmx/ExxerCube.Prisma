"""File discovery and lightweight C# parsing utilities."""

from __future__ import annotations

import fnmatch
import hashlib
import re
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Tuple
from xml.etree import ElementTree

from .models import MemberInfo, TypeInfo, XmlDoc

DEFAULT_INCLUDE_PATTERNS = ["code/src/**/*Domain*/**/*.cs"]
DEFAULT_EXCLUDE_PATTERNS = ["**/bin/**", "**/obj/**", "**/artifacts/**", "**/.git/**"]


def normalize_rel_path(path: Path, root: Path) -> str:
    """Return a posix style relative path string."""
    return str(path.relative_to(root).as_posix())


def discover_cs_files(
    root: Path,
    include_patterns: Optional[Sequence[str]] = None,
    exclude_patterns: Optional[Sequence[str]] = None,
    domain_first: bool = True,
) -> List[Path]:
    """Discover .cs files under the repository honoring include/exclude globs."""

    if include_patterns:
        patterns = include_patterns
    elif domain_first:
        patterns = DEFAULT_INCLUDE_PATTERNS
    else:
        patterns = ["code/src/**/*.cs"]

    excludes = list(DEFAULT_EXCLUDE_PATTERNS)
    if exclude_patterns:
        excludes.extend(exclude_patterns)

    discovered: List[Path] = []
    seen = set()
    for pattern in patterns:
        for path in root.glob(pattern):
            if not path.is_file():
                continue
            rel = normalize_rel_path(path, root)
            if any(fnmatch.fnmatch(rel, ex) for ex in excludes):
                continue
            if rel in seen:
                continue
            seen.add(rel)
            discovered.append(path)
    return sorted(discovered)


class ProjectLocator:
    """Locate the nearest .csproj for a given file."""

    def __init__(self, root: Path) -> None:
        self.root = root
        self._cache: Dict[Path, str] = {}

    def find(self, file_path: Path) -> str:
        """Return the relative path to the owning .csproj or 'Unknown'."""
        if file_path in self._cache:
            return self._cache[file_path]

        current = file_path.parent
        while True:
            candidates = list(current.glob("*.csproj"))
            if candidates:
                rel = normalize_rel_path(candidates[0], self.root)
                self._cache[file_path] = rel
                return rel
            if current == self.root:
                break
            try:
                current = current.parent
                current.relative_to(self.root)
            except ValueError:
                break

        self._cache[file_path] = "Unknown"
        return "Unknown"


LAYER_KEYWORDS = [
    ("domain", "Domain"),
    ("tests", "Tests"),
    ("application", "Application"),
    ("infrastructure", "Infrastructure"),
    ("core", "Core"),
    ("api", "Gatekeeper"),
    ("axis", "Axis"),
    ("cortex", "Cortex"),
    ("nexus", "Nexus"),
    ("cli", "Sentinel"),
]


def infer_layer(file_path: Path) -> str:
    """Infer the evocative layer from the path heuristically."""
    lowered = [part.lower() for part in file_path.parts]
    for keyword, layer in LAYER_KEYWORDS:
        if any(keyword in part for part in lowered):
            return layer
    return "Unknown"


TYPE_PATTERN = re.compile(
    r"\b(?P<kind>class|interface|struct|record|enum)\s+(?P<name>[A-Za-z_][\w<>]*)"
)


def extract_base_types(line: str) -> List[str]:
    if ":" not in line:
        return []
    after = line.split(":", 1)[1]
    after = after.split("{", 1)[0]
    fragments = [fragment.strip() for fragment in after.split(",")]
    return [fragment for fragment in fragments if fragment]


def detect_accessibility(line: str) -> str:
    """Extract the access modifier from the declaration line."""
    lowered = line.lower()
    if "public" in lowered:
        return "public"
    if "protected internal" in lowered:
        return "protected internal"
    if "protected" in lowered:
        return "protected"
    if "internal" in lowered:
        return "internal"
    if "private" in lowered:
        return "private"
    return "internal"


def clean_doc_line(line: str) -> str:
    """Remove the leading XML doc slashes and whitespace."""
    if line.startswith("///"):
        return line[3:].lstrip()
    return line


def parse_param_names(param_block: str) -> List[str]:
    """Parse a parameter list and return parameter names."""
    params: List[str] = []
    current: List[str] = []
    depth = 0
    for char in param_block:
        if char == "<":
            depth += 1
            current.append(char)
        elif char == ">":
            depth = max(0, depth - 1)
            current.append(char)
        elif char == "," and depth == 0:
            token = "".join(current).strip()
            current = []
            name = extract_param_name(token)
            if name:
                params.append(name)
        else:
            current.append(char)

    token = "".join(current).strip()
    name = extract_param_name(token)
    if name:
        params.append(name)
    return params


def extract_param_name(token: str) -> Optional[str]:
    """Extract the identifier from a parameter token."""
    if not token:
        return None
    token = token.split("=")[0].strip()
    if not token:
        return None
    parts = token.split()
    if not parts:
        return None
    return parts[-1].strip()


def parse_xml_doc(lines: List[str]) -> XmlDoc:
    """Parse XML documentation comment lines."""
    if not lines:
        return XmlDoc()

    raw = "\n".join(lines)
    cleaned = "\n".join(clean_doc_line(line) for line in lines)
    doc = XmlDoc(raw=raw)

    try:
        root = ElementTree.fromstring(f"<root>{cleaned}</root>")
    except ElementTree.ParseError:
        doc.summary = cleaned.strip()
        return doc

    summary = root.findtext("summary", default="").strip()
    doc.summary = " ".join(summary.split())
    doc.returns = " ".join(root.findtext("returns", default="").split())
    doc.remarks = " ".join(root.findtext("remarks", default="").split())

    for param in root.findall("param"):
        name = param.attrib.get("name", "")
        if name:
            doc.params[name] = " ".join(param.text.split()) if param.text else ""
    for param in root.findall("typeparam"):
        name = param.attrib.get("name", "")
        if name:
            doc.typeparams[name] = " ".join(param.text.split()) if param.text else ""

    for see in root.findall("seealso"):
        cref = see.attrib.get("cref")
        if cref:
            doc.seealso.append(cref)

    return doc


class CSharpFileParser:
    """Very lightweight parser for C# types and members."""

    def __init__(self, root: Path, file_path: Path, project: str, layer: str) -> None:
        self.root = root
        self.file_path = file_path
        self.project = project
        self.layer = layer

    def parse(self, text: Optional[str] = None) -> Tuple[List[TypeInfo], str]:
        """Parse the file, returning the discovered types and the file hash."""
        if text is None:
            text = self.file_path.read_text(encoding="utf-8")
        file_hash = hashlib.sha1(text.encode("utf-8")).hexdigest()
        lines = text.splitlines()

        namespace = ""
        types: List[TypeInfo] = []
        contexts: List[Tuple[TypeInfo, int]] = []
        pending_doc: List[str] = []
        pending_attributes: List[str] = []

        for idx, raw_line in enumerate(lines, start=1):
            stripped = raw_line.strip()

            if stripped.startswith("///"):
                pending_doc.append(stripped)
                continue

            if stripped.startswith("[") and not stripped.endswith("];"):
                pending_attributes.append(stripped)
                continue

            if stripped.startswith("namespace "):
                ns = (
                    stripped[len("namespace ") :]
                    .replace("{", "")
                    .replace(";", "")
                    .strip()
                )
                namespace = ns
                pending_doc = []
                continue

            type_match = TYPE_PATTERN.search(stripped)
            if type_match:
                type_info = TypeInfo(
                    name=type_match.group("name").rstrip(":"),
                    namespace=namespace,
                    kind=type_match.group("kind"),
                    accessibility=detect_accessibility(stripped),
                    line=idx,
                    file_path=self.file_path,
                    project=self.project,
                    layer=self.layer,
                    xml_doc=parse_xml_doc(pending_doc),
                    attributes=list(pending_attributes),
                    bases=extract_base_types(stripped),
                )
                pending_doc = []
                pending_attributes = []
                contexts.append((type_info, 0))
                types.append(type_info)

            current_context = contexts[-1][0] if contexts and contexts[-1][1] > 0 else None
            if current_context:
                member = self._try_parse_member(
                    stripped, idx, current_context, pending_doc
                )
                if member:
                    current_context.members.append(member)
                    pending_doc = []

            contexts = self._apply_braces(stripped, contexts, idx)

        line_count = len(lines) or 1
        for type_info, brace_count in contexts:
            if type_info.end_line == 0:
                type_info.end_line = line_count

        return types, file_hash

    def _apply_braces(
        self, line: str, contexts: List[Tuple[TypeInfo, int]], line_number: int
    ) -> List[Tuple[TypeInfo, int]]:
        """Update context brace counts based on braces found on the line."""
        updated = contexts
        for char in line:
            if char == "{":
                if updated:
                    type_info, count = updated[-1]
                    updated[-1] = (type_info, count + 1)
            elif char == "}":
                if updated:
                    type_info, count = updated[-1]
                    count -= 1
                    if count <= 0:
                        type_info.end_line = line_number
                        updated = updated[:-1]
                    else:
                        updated[-1] = (type_info, count)
        return updated

    def _try_parse_member(
        self,
        stripped: str,
        line_number: int,
        parent_type: TypeInfo,
        pending_doc: List[str],
    ) -> Optional[MemberInfo]:
        """Attempt to parse a member declaration from the provided line."""
        lowered = stripped.lower()
        if not (
            lowered.startswith("public")
            or lowered.startswith("protected")
            or lowered.startswith("internal protected")
        ):
            return None

        signature = stripped
        accessibility = detect_accessibility(stripped)

        if "(" in stripped and ")" in stripped:
            before_paren, after_paren = stripped.split("(", 1)
            params_part, _, remainder = after_paren.partition(")")
            name_token = before_paren.strip().split()[-1]
            kind = "method"
            if name_token == parent_type.name:
                kind = "constructor"
            parameters = parse_param_names(params_part)
            member = MemberInfo(
                name=name_token,
                kind=kind,
                accessibility=accessibility,
                line=line_number,
                signature=signature,
                xml_doc=parse_xml_doc(pending_doc),
                parameters=parameters,
            )
            return member

        if "{" in stripped and ("get;" in stripped or "set;" in stripped):
            name_token = stripped.split("{", 1)[0].split()[-1]
            member = MemberInfo(
                name=name_token.strip().rstrip(";"),
                kind="property",
                accessibility=accessibility,
                line=line_number,
                signature=signature,
                xml_doc=parse_xml_doc(pending_doc),
            )
            return member

        if stripped.endswith(";"):
            name_token = stripped.rstrip(";").split()[-1]
            member = MemberInfo(
                name=name_token.strip(),
                kind="field",
                accessibility=accessibility,
                line=line_number,
                signature=signature,
                xml_doc=parse_xml_doc(pending_doc),
            )
            return member

        return None
