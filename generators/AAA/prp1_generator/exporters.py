"""Exporters for corpora and auditing."""

from __future__ import annotations

import json
import os
from dataclasses import dataclass
from hashlib import sha256
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional


@dataclass
class CorpusRecord:
    """Serializable representation of a generated requerimiento."""

    record_id: str
    text: str
    metadata: Dict[str, Any]
    sections: Dict[str, str]
    fixtures: Dict[str, str]
    prompt: Optional[str] = None
    model: Optional[str] = None

    @property
    def hash(self) -> str:
        return sha256(self.text.encode("utf-8")).hexdigest()


class CorpusExporter:
    """Writes corpus outputs to JSON + Markdown so downstream tooling stays compatible."""

    def __init__(self, json_path: Path) -> None:
        self.json_path = json_path
        self.md_path = json_path.with_suffix(".md")
        self.json_path.parent.mkdir(parents=True, exist_ok=True)

    def write(self, records: Iterable[CorpusRecord]) -> None:
        serializable: List[Dict[str, Any]] = []
        for record in records:
            payload = {
                "id": record.record_id,
                "text": record.text,
                "hash": record.hash,
                "metadata": record.metadata,
                "sections": record.sections,
                "fixtures": record.fixtures,
                "model": record.model,
            }
            serializable.append(payload)
        self.json_path.write_text(json.dumps(serializable, ensure_ascii=False, indent=2), encoding="utf-8")
        self._write_markdown(serializable)

    def _write_markdown(self, documents: List[Dict[str, Any]]) -> None:
        lines = ["# Corpus de Requerimientos Legales", ""]
        for doc in documents:
            lines.append(f"<--Start Requirement {doc['id']}-->")
            lines.append("**Requerimiento**")
            lines.append(doc["text"])
            lines.append("")
            lines.append("**Hash**")
            lines.append(doc["hash"])
            lines.append("")
            lines.append("**Metadata (JSON)**")
            lines.append("```json")
            lines.append(json.dumps(doc["metadata"], ensure_ascii=False, indent=2))
            lines.append("```")
            lines.append("<--End Requirement-->")
            lines.append("")
        self.md_path.write_text("\n".join(lines), encoding="utf-8")


class AuditLogger:
    """Append-only JSONL log with provenance info."""

    def __init__(self, path: Optional[Path]) -> None:
        self.path = path
        if self.path:
            self.path.parent.mkdir(parents=True, exist_ok=True)

    def log(self, entry: Dict[str, Any]) -> None:
        if not self.path:
            return
        with self.path.open("a", encoding="utf-8") as fh:
            fh.write(json.dumps(entry, ensure_ascii=False) + os.linesep)
