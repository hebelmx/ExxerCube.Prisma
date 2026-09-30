#!/usr/bin/env python3
"""
PRP1 Document Generator - Enhanced Edition
Unified entry point combining the best features from both implementations.
"""

from __future__ import annotations

import argparse
import json
import logging
import sys
from datetime import datetime
from pathlib import Path
from typing import Dict

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
    ensure_ollama_ready,
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
    """
    Main generation pipeline.

    Args:
        args: Parsed command-line arguments
    """
    # Initialize configuration
    config = load_config_from_args(args)

    # Setup Ollama service if not disabled
    ollama_url = config.ollama_url
    if not args.skip_orchestration:
        try:
            LOGGER.info("Setting up Ollama service...")
            ollama_url = ensure_ollama_ready(
                model=config.ollama_model,
                container_name=args.container_name,
                port=args.ollama_port,
                use_gpu=args.use_gpu,
                skip_prewarm=args.skip_prewarm,
            )
            config.ollama_url = ollama_url
        except Exception as e:
            LOGGER.error("Failed to setup Ollama service: %s", e)
            if not args.allow_fallback:
                raise
            LOGGER.warning("Continuing with fallback templates only")

    # Initialize components
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

    # Setup progress logging
    progress_log = Path(args.fixtures_output or "Prp1Dummy") / "job_progress.log"
    if progress_log.parent:
        progress_log.parent.mkdir(parents=True, exist_ok=True)

    def log_message(msg: str) -> None:
        LOGGER.info(msg)
        timestamped = f"[{datetime.now().isoformat()}] {msg}\n"
        if progress_log.exists():
            progress_log.write_text(
                progress_log.read_text(encoding="utf-8") + timestamped,
                encoding="utf-8"
            )
        else:
            progress_log.write_text(timestamped, encoding="utf-8")

    log_message(f"Starting generation: {config.num_records} records, output {args.output}")
    log_message(f"Using model: {config.ollama_model} at {config.ollama_url}")

    # Main generation loop
    for idx in tqdm(range(config.num_records), desc="Generating PRP1 documents"):
        record_id = f"REQ{idx+1:04d}"

        try:
            # Sample metadata
            metadata, profile = sampler.sample(config.batch)
            validator.validate(metadata, profile.identifier)

            # Generate text using LLM or fallback
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
                LOGGER.warning("Fallback used for %s: %s", record_id, error_message)

            # Build sections and render fixtures
            sections = build_sections(metadata)
            fixtures_map = fixtures.render(record_id, metadata, sections)

            # Create record
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

            # Audit logging
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

            # Progress logging
            if (idx + 1) % 5 == 0 or idx + 1 == config.num_records:
                log_message(
                    f"Generated {idx+1}/{config.num_records} records; "
                    f"last ID {record_id} (origin {origin})"
                )

        except Exception as e:
            LOGGER.error("Error generating record %s: %s", record_id, e)
            if not args.continue_on_error:
                raise
            log_message(f"ERROR on record {record_id}: {e}")
            continue

    # Export results
    exporter.write(records)
    log_message(f"Completed: {len(records)} records stored in {args.output}")
    LOGGER.info("\n" + "="*60)
    LOGGER.info("Generation Complete!")
    LOGGER.info(f"Total records: {len(records)}")
    LOGGER.info(f"Output corpus: {args.output}")
    if config.output.fixtures_output:
        LOGGER.info(f"Fixtures directory: {config.output.fixtures_output}")
    LOGGER.info("="*60)


def parse_args() -> argparse.Namespace:
    """Parse command-line arguments."""
    parser = argparse.ArgumentParser(
        description="Generate PRP1-like dummy requerimientos with automated Ollama orchestration.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Generate 10 documents with default settings
  python generate_documents.py --num 10

  # Generate with specific model and output directory
  python generate_documents.py --num 5 --model llama3.2 --fixtures-output ./output

  # Generate without Docker orchestration (manual Ollama setup)
  python generate_documents.py --num 3 --skip-orchestration

  # Generate with GPU disabled
  python generate_documents.py --num 10 --no-gpu
        """
    )

    # Generation parameters
    parser.add_argument("--num", type=int, default=5, help="Number of records to generate")
    parser.add_argument("--output", default="test_corpus.json", help="Output JSON corpus path")
    parser.add_argument("--seed", type=int, default=None, help="Random seed for reproducibility")

    # Ollama configuration
    parser.add_argument("--ollama-model", "--model", dest="ollama_model", default="llama3.2:latest",
                        help="Ollama model to use")
    parser.add_argument("--ollama-url", default="http://localhost:11434",
                        help="Ollama base URL")
    parser.add_argument("--ollama-port", default="11434", help="Ollama API port")

    # Docker orchestration
    parser.add_argument("--skip-orchestration", action="store_true",
                        help="Skip Docker orchestration (use existing Ollama instance)")
    parser.add_argument("--container-name", default="ollama",
                        help="Docker container name for Ollama")
    parser.add_argument("--no-gpu", dest="use_gpu", action="store_false",
                        help="Disable GPU acceleration")
    parser.add_argument("--skip-prewarm", action="store_true",
                        help="Skip model prewarming (faster startup, slower first generation)")

    # Output options
    parser.add_argument("--fixtures-output", help="Directory for PNG/PDF/XML/DOCX fixtures")
    parser.add_argument(
        "--fixtures-format",
        default="png",
        choices=["png", "pdf", "both"],
        help="Fixture format to emit",
    )
    parser.add_argument("--audit-log", help="Optional JSONL audit log file")

    # Advanced options
    parser.add_argument("--batch", help="Requirement profile identifier to bias sampling")
    parser.add_argument("--prp1-summary", help="Path to parsed PRP1 summary JSON")
    parser.add_argument("--allow-fallback", action="store_true",
                        help="Continue with fallback if Ollama setup fails")
    parser.add_argument("--continue-on-error", action="store_true",
                        help="Continue generation even if individual records fail")

    # Debugging
    parser.add_argument("--debug", action="store_true", help="Enable verbose logging")
    parser.add_argument(
        "--debug-prompts",
        action="store_true",
        help="Store prompts in audit log and output records",
    )

    return parser.parse_args()


def main() -> None:
    """Main entry point."""
    args = parse_args()

    # Configure logging
    log_level = logging.DEBUG if args.debug else logging.INFO
    logging.basicConfig(
        level=log_level,
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
        datefmt='%Y-%m-%d %H:%M:%S'
    )

    try:
        run_generation(args)
        sys.exit(0)
    except KeyboardInterrupt:
        LOGGER.warning("\nGeneration interrupted by user")
        sys.exit(130)
    except Exception as e:
        LOGGER.error("Fatal error: %s", e, exc_info=args.debug)
        sys.exit(1)


if __name__ == "__main__":
    main()
