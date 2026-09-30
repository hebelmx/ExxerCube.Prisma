#!/usr/bin/env python3
"""Entry point for the refactored PRP1-style generator."""

from __future__ import annotations

import argparse
import json
import logging
from datetime import datetime
from pathlib import Path
from typing import Dict, Tuple

from tqdm import tqdm

from prp1_generator import (
    AuditLogger,
    ContextSampler,
    CorpusExporter,
    CorpusRecord,
    FallbackTemplateRenderer,
    FixtureRenderer,
    MetadataValidator,
    OllamaClient,
    OllamaError,
    load_config_from_args,
)

LOGGER = logging.getLogger("prp1_generator")


def build_sections(metadata: Dict[str, object]) -> Dict[str, str]:
    """Derive structural sections similar to PRP1 layout."""
    autoridad = (
        f"{metadata.get('autoridadEmisora', 'Autoridad')} requiere diligenciar el expediente "
        f"{metadata.get('expediente', '')} relativo a {', '.join(metadata.get('partes', [])[:2])}."
    )
    instrucciones = (
        f"Con fundamento en {metadata.get('fundamentoLegal', 'artículos aplicables')}, "
        f"se ordena ejecutar {metadata.get('tipoRequerimiento')} respecto de "
        f"{metadata.get('subtipoRequerimiento')} en un plazo de {metadata.get('plazoDias', 5)} días."
    )
    apercibimiento = (
        "Bajo apercibimiento de imponer multas y remitir antecedentes a la autoridad competente "
        "en caso de desacato."
    )
    firmas = (
        "Lic. María del Rosario Toriz – Secretaria de Acuerdos\n"
        "Lic. Carlos Alberto Ugalde – Actuario Judicial"
    )
    return {
        "autoridad": autoridad,
        "instrucciones": instrucciones,
        "apercibimiento": apercibimiento,
        "firmas": firmas,
    }


def run_generation(args: argparse.Namespace) -> None:
    config = load_config_from_args(args)
    sampler = ContextSampler(config.entities, config.summary, config.seed)
    validator = MetadataValidator(config.schema, config.summary)
    fallback_renderer = FallbackTemplateRenderer(
        config.fallback_templates,
        seed=config.seed or 0,
    )
    fixtures = FixtureRenderer(
        config.output.fixtures_output,
        config.output.normalized_format(),
        seed=config.seed,
    )
    exporter = CorpusExporter(Path(args.output).resolve())
    audit = AuditLogger(config.output.audit_log)

    records = []
    client = OllamaClient(config.ollama_url, config.ollama_model)

    progress_log = Path(args.fixtures_output or "Prp1Dummy") / "job_progress.log"
    if progress_log.parent:
        progress_log.parent.mkdir(parents=True, exist_ok=True)

    def log_message(msg: str) -> None:
        LOGGER.info(msg)
        timestamped = f"[{datetime.now().isoformat()}] {msg}\n"
        progress_log.write_text(progress_log.read_text() + timestamped if progress_log.exists() else timestamped, encoding="utf-8")

    log_message(f"Iniciando generación: {config.num_records} registros, salida {args.output}")

    for idx in tqdm(range(config.num_records), desc="Generando PRP1 dummy"):
        record_id = f"REQ{idx+1:04d}"
        metadata, profile = sampler.sample(config.batch)
        validator.validate(metadata, profile.identifier)

        context_json = json.dumps(metadata, ensure_ascii=False, indent=2)
        prompt = config.prompt_template.replace("{context}", context_json)
        if config.debug_prompts:
            LOGGER.debug("Prompt for %s:\n%s", record_id, prompt)

        text = ""
        model_used = config.ollama_model
        origin = "ollama"
        error_message = None
        try:
            text = client.generate(prompt)
        except OllamaError as exc:
            origin = "fallback"
            model_used = "template"
            error_message = str(exc)
            text = fallback_renderer.render(metadata)

        sections = build_sections(metadata)
        fixtures_map = fixtures.render(record_id, metadata, sections)
        record = CorpusRecord(
            record_id=record_id,
            text=text,
            metadata=metadata,
            sections=sections,
            fixtures=fixtures_map,
            prompt=prompt if config.debug_prompts else None,
            model=model_used,
        )
        records.append(record)
        audit.log(
            {
                "record_id": record_id,
                "profile": profile.identifier,
                "seed": config.seed,
                "origin": origin,
                "error": error_message,
                "fixtures": fixtures_map,
                "hash": record.hash,
            }
        )
        if (idx + 1) % 5 == 0 or idx + 1 == config.num_records:
            log_message(f"Generados {idx+1}/{config.num_records} registros; último ID {record_id} (origen {origin})")

    exporter.write(records)
    log_message(f"Finalizado: {len(records)} registros almacenados en {args.output}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate PRP1-like dummy requerimientos.")
    parser.add_argument("--num", type=int, default=5, help="Number of records to generate")
    parser.add_argument("--output", default="test_corpus.json", help="Output JSON path")
    parser.add_argument("--ollama-model", default="llama3.2:latest", help="Ollama model")
    parser.add_argument(
        "--ollama-url",
        default="http://localhost:11434",
        help="Ollama base URL (use docker mapped port)",
    )
    parser.add_argument("--seed", type=int, default=None, help="Random seed")
    parser.add_argument("--fixtures-output", help="Directory for PNG/PDF/XML fixtures")
    parser.add_argument(
        "--fixtures-format",
        default="png",
        choices=["png", "pdf", "both"],
        help="Fixture format to emit",
    )
    parser.add_argument("--audit-log", help="Optional JSONL audit log file")
    parser.add_argument("--batch", help="Requirement profile identifier to bias sampling")
    parser.add_argument("--prp1-summary", help="Path to parsed PRP1 summary JSON")
    parser.add_argument("--debug", action="store_true", help="Enable verbose logging")
    parser.add_argument(
        "--debug-prompts",
        action="store_true",
        help="Store prompts in audit log and output records",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    logging.basicConfig(level=logging.DEBUG if args.debug else logging.INFO)
    run_generation(args)


if __name__ == "__main__":
    main()
