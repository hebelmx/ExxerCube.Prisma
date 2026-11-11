"""HTTP client for interacting with Ollama (Docker-hosted)."""

from __future__ import annotations

import json
import logging
from typing import Any, Dict, Optional

import requests

LOGGER = logging.getLogger(__name__)


class OllamaError(RuntimeError):
    """Raised when Ollama fails or returns an invalid payload."""


class OllamaClient:
    """Tiny wrapper around the Ollama HTTP API."""

    def __init__(self, base_url: str, model: str, timeout: int = 900) -> None:
        self.base_url = base_url.rstrip("/")
        self.model = model
        self.timeout = timeout

    def generate(self, prompt: str, options: Optional[Dict[str, Any]] = None) -> str:
        payload = {
            "model": self.model,
            "prompt": prompt,
            "stream": True,
            "options": options
            or {
                "temperature": 0.65,
                "top_p": 0.9,
                "num_predict": 400,
                "stop": ["```", "###"],
            },
        }
        LOGGER.debug("Calling Ollama %s with model %s", self.base_url, self.model)
        try:
            response = requests.post(
                f"{self.base_url}/api/generate",
                json=payload,
                timeout=self.timeout,
                stream=True,
            )
        except requests.RequestException as exc:
            raise OllamaError(f"Ollama request failed: {exc}") from exc

        if response.status_code != 200:
            raise OllamaError(f"Ollama returned HTTP {response.status_code}: {response.text[:200]}")

        text_parts = []
        for line in response.iter_lines(decode_unicode=True):
            if not line:
                continue
            try:
                chunk = json.loads(line)
            except json.JSONDecodeError as exc:
                LOGGER.debug("Skipping malformed chunk from Ollama: %s", line)
                continue
            if "error" in chunk:
                raise OllamaError(chunk["error"])
            if chunk.get("response"):
                text_parts.append(chunk["response"])
            if chunk.get("done"):
                break

        text = "".join(text_parts).strip()
        if not text:
            raise OllamaError("Ollama response missing content")
        return text
