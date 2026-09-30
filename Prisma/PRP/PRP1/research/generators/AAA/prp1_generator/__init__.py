"""PRP1-style requerimiento generator package - Enhanced Edition."""

from .config import GeneratorConfig, OutputOptions, load_config_from_args
from .context import ContextSampler, RequirementProfile
from .ollama_client import OllamaClient, OllamaError
from .ollama_orchestrator import ensure_ollama_ready
from .fallback import FallbackTemplateRenderer
from .exporters import CorpusExporter, CorpusRecord, AuditLogger
from .fixtures import FixtureRenderer
from .validators import MetadataValidator, ValidationError

# CNBV Visual Fidelity modules
from .cnbv_schema import CNBVExpediente, parse_cnbv_xml, create_cnbv_xml
from .cnbv_pdf_generator import CNBVPDFGenerator, xml_to_pdf
from .visual_similarity import VisualSimilarityMeasurer, measure_similarity, SimilarityScore
from .chaos_simulator import ChaosSimulator, ChaosProfile, simulate_real_world_batch

__all__ = [
    # Original exports
    "GeneratorConfig",
    "OutputOptions",
    "ContextSampler",
    "RequirementProfile",
    "OllamaClient",
    "OllamaError",
    "ensure_ollama_ready",
    "FallbackTemplateRenderer",
    "CorpusExporter",
    "CorpusRecord",
    "FixtureRenderer",
    "MetadataValidator",
    "ValidationError",
    "AuditLogger",
    "load_config_from_args",
    # CNBV Visual Fidelity exports
    "CNBVExpediente",
    "parse_cnbv_xml",
    "create_cnbv_xml",
    "CNBVPDFGenerator",
    "xml_to_pdf",
    "VisualSimilarityMeasurer",
    "measure_similarity",
    "SimilarityScore",
    "ChaosSimulator",
    "ChaosProfile",
    "simulate_real_world_batch",
]

__version__ = "2.1.0"
