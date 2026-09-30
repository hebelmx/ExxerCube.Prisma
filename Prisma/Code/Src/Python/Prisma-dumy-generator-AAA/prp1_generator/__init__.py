"""PRP1-style requerimiento generator package."""

from .config import GeneratorConfig, OutputOptions, load_config_from_args
from .context import ContextSampler, RequirementProfile
from .ollama_client import OllamaClient, OllamaError
from .fallback import FallbackTemplateRenderer
from .exporters import CorpusExporter, CorpusRecord, AuditLogger
from .fixtures import FixtureRenderer
from .validators import MetadataValidator, ValidationError

__all__ = [
    "GeneratorConfig",
    "OutputOptions",
    "ContextSampler",
    "RequirementProfile",
    "OllamaClient",
    "OllamaError",
    "FallbackTemplateRenderer",
    "CorpusExporter",
    "CorpusRecord",
    "FixtureRenderer",
    "MetadataValidator",
    "ValidationError",
    "AuditLogger",
    "load_config_from_args",
]
