"""Configuration helpers for the PRP1 generator."""

from __future__ import annotations

import json
import os
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Dict, Optional, Sequence


def _resolve_default(path: str) -> Path:
    """Return repo-relative path for convenience."""
    return Path(path).resolve()


@dataclass
class OutputOptions:
    """CLI-tunable options controlling side effects."""

    fixtures_output: Optional[Path] = None
    fixtures_format: str = "png"
    audit_log: Optional[Path] = None
    debug: bool = False

    def normalized_format(self) -> str:
        fmt = (self.fixtures_format or "png").lower()
        if fmt not in {"png", "pdf", "both"}:
            return "png"
        return fmt


@dataclass
class GeneratorConfig:
    """In-memory configuration for the generation pipeline."""

    num_records: int = 1
    seed: Optional[int] = None
    ollama_model: str = "llama3.2:latest"
    ollama_url: str = field(
        default_factory=lambda: os.getenv("OLLAMA_BASE_URL", "http://localhost:11434")
    )
    entities: Dict[str, Sequence[Any]] = field(default_factory=dict)
    prompt_template: str = ""
    fallback_templates: Sequence[str] = field(default_factory=list)
    schema: Dict[str, Any] = field(default_factory=dict)
    summary: Optional[Dict[str, Any]] = None
    fixtures_root: Path = field(
        default_factory=lambda: _resolve_default("Prisma/Fixtures/PRP1")
    )
    output: OutputOptions = field(default_factory=OutputOptions)
    batch: Optional[str] = None
    debug_prompts: bool = False

    def ensure_paths(self) -> None:
        """Create directories that are expected to exist."""
        if self.output.fixtures_output:
            self.output.fixtures_output.mkdir(parents=True, exist_ok=True)
        if self.output.audit_log:
            self.output.audit_log.parent.mkdir(parents=True, exist_ok=True)


def _read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def _load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def load_config_from_args(args: Any) -> GeneratorConfig:
    """Construct GeneratorConfig from argparse Namespace."""

    base_dir = Path("Prisma/Code/Src/CSharp/Python").resolve()
    entities_path = base_dir / "entities.json"
    prompt_path = base_dir / "prompt_template.txt"
    fallback_path = base_dir / "fictitious_requerimientos_raw.md"
    schema_path = base_dir / "requerimientos_schema.json"
    if args.prp1_summary:
        summary_path = Path(args.prp1_summary).resolve()
    else:
        summary_path = Path("Prisma/Fixtures/PRP1/prp1_summary.json").resolve()
        if not summary_path.exists():
            summary_path = None

    entities = _load_json(entities_path)
    prompt_template = _read_text(prompt_path)

    fallback_templates: Sequence[str]
    if fallback_path.exists():
        # Each example block becomes a fallback template
        raw = _read_text(fallback_path)
        fallback_templates = [
            block.strip()
            for block in raw.split("## Ejemplo")
            if block.strip()
        ]
    else:
        fallback_templates = [
            (
                "OFICIO {expediente} - {autoridadEmisora}\\n"
                "Se requiere a {entidad_financiera} entregar información financiera "
                "relacionada con {partes}. Plazo: {plazo_dias} días."
            )
        ]

    summary = _load_json(summary_path) if summary_path and summary_path.exists() else None
    schema = _load_json(schema_path)
    output = OutputOptions(
        fixtures_output=Path(args.fixtures_output).resolve()
        if args.fixtures_output
        else None,
        fixtures_format=args.fixtures_format,
        audit_log=Path(args.audit_log).resolve() if args.audit_log else None,
        debug=args.debug,
    )

    cfg = GeneratorConfig(
        num_records=args.num,
        seed=args.seed,
        ollama_model=args.ollama_model,
        ollama_url=args.ollama_url,
        entities=entities,
        prompt_template=prompt_template,
        fallback_templates=fallback_templates,
        summary=summary,
        schema=schema,
        output=output,
        batch=args.batch,
        debug_prompts=args.debug_prompts,
    )
    cfg.ensure_paths()
    return cfg
