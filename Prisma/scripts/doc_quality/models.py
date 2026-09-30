"""Shared dataclasses for the doc-quality pipeline."""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional


@dataclass
class XmlDoc:
    """Structured representation of XML documentation comments."""

    raw: str = ""
    summary: str = ""
    returns: str = ""
    remarks: str = ""
    params: Dict[str, str] = field(default_factory=dict)
    typeparams: Dict[str, str] = field(default_factory=dict)
    seealso: List[str] = field(default_factory=list)

    def to_dict(self) -> Optional[Dict[str, object]]:
        """Convert into a JSON-ready dictionary."""
        if not self.raw.strip():
            return None
        return {
            "raw": self.raw,
            "summary": self.summary,
            "returns": self.returns,
            "remarks": self.remarks,
            "params": self.params,
            "typeparam": self.typeparams,
            "seealso": self.seealso,
        }

    def has_summary(self) -> bool:
        return bool(self.summary.strip())

    def has_remarks(self) -> bool:
        return bool(self.remarks.strip())

    def has_returns(self) -> bool:
        return bool(self.returns.strip())

    def has_params(self) -> bool:
        return bool(self.params)


@dataclass
class MemberInfo:
    """Represents a public/protected member declared inside a type."""

    name: str
    kind: str
    accessibility: str
    line: int
    signature: str
    xml_doc: XmlDoc = field(default_factory=XmlDoc)
    parameters: List[str] = field(default_factory=list)
    parameter_details: List[Dict[str, str]] = field(default_factory=list)
    return_type: Optional[str] = None

    def to_dict(self) -> Dict[str, object]:
        """Flatten to a serializable shape."""
        return {
            "name": self.name,
            "kind": self.kind,
            "accessibility": self.accessibility,
            "line": self.line,
            "signature": self.signature,
            "parameters": self.parameters,
            "parameterDetails": self.parameter_details,
            "returnType": self.return_type,
            "xmlDoc": self.xml_doc.to_dict(),
            "hasSummary": self.xml_doc.has_summary(),
            "hasParams": self.xml_doc.has_params(),
        }


@dataclass
class TypeInfo:
    """Represents a discovered type declaration."""

    name: str
    namespace: str
    kind: str
    accessibility: str
    line: int
    file_path: Path
    project: str
    layer: str
    xml_doc: XmlDoc = field(default_factory=XmlDoc)
    attributes: List[str] = field(default_factory=list)
    members: List[MemberInfo] = field(default_factory=list)
    end_line: int = 0
    bases: List[str] = field(default_factory=list)

    def type_id(self) -> str:
        return f"{self.namespace}.{self.name}" if self.namespace else self.name

    def relative_file(self, root: Path) -> str:
        return str(self.file_path.relative_to(root).as_posix())

    def to_catalog_entry(self, root: Path, doc_hash: Optional[str]) -> Dict[str, object]:
        """Return a JSON-ready dictionary for the type catalog."""
        xml_doc = self.xml_doc
        return {
            "typeId": self.type_id(),
            "name": self.name,
            "namespace": self.namespace,
            "project": self.project,
            "layer": self.layer,
            "file": self.relative_file(root),
            "line": self.line,
            "endLine": self.end_line,
            "kind": self.kind,
            "accessibility": self.accessibility,
            "attributes": self.attributes,
            "bases": self.bases,
            "xmlDoc": xml_doc.to_dict(),
            "xmlDocHash": doc_hash,
            "hasSummary": xml_doc.has_summary(),
            "hasRemarks": xml_doc.has_remarks(),
            "hasReturns": xml_doc.has_returns(),
            "hasParams": xml_doc.has_params(),
            "memberCount": len(self.members),
            "members": [member.to_dict() for member in self.members],
        }
