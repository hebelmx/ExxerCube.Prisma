"""HTTP client for interacting with Ollama (Docker-hosted) - Enhanced Edition."""

from __future__ import annotations

import json
import logging
from typing import Any, Dict, Optional

import requests

LOGGER = logging.getLogger(__name__)


class OllamaError(RuntimeError):
    """Raised when Ollama fails or returns an invalid payload."""


class OllamaClient:
    """Enhanced wrapper around the Ollama HTTP API with streaming support."""

    def __init__(self, base_url: str, model: str, timeout: int = 900) -> None:
        self.base_url = base_url.rstrip("/")
        self.model = model
        self.timeout = timeout

    def generate(self, prompt: str, options: Optional[Dict[str, Any]] = None, stream: bool = True) -> str:
        """
        Generate text using the Ollama model.

        Args:
            prompt: The prompt to send to the model
            options: Optional generation parameters
            stream: Whether to use streaming (default: True for better performance)

        Returns:
            Generated text

        Raises:
            OllamaError: If generation fails
        """
        payload = {
            "model": self.model,
            "prompt": prompt,
            "stream": stream,
            "options": options
            or {
                "temperature": 0.65,
                "top_p": 0.9,
                "num_predict": 400,
                "stop": ["```", "###"],
            },
        }
        LOGGER.debug("Calling Ollama %s with model %s (stream=%s)", self.base_url, self.model, stream)

        try:
            response = requests.post(
                f"{self.base_url}/api/generate",
                json=payload,
                timeout=self.timeout,
                stream=stream,
            )
        except requests.RequestException as exc:
            raise OllamaError(f"Ollama request failed: {exc}") from exc

        if response.status_code != 200:
            raise OllamaError(f"Ollama returned HTTP {response.status_code}: {response.text[:200]}")

        if stream:
            return self._handle_streaming_response(response)
        else:
            return self._handle_non_streaming_response(response)

    def _handle_streaming_response(self, response: requests.Response) -> str:
        """Handle streaming response from Ollama."""
        text_parts = []
        for line in response.iter_lines(decode_unicode=True):
            if not line:
                continue
            try:
                chunk = json.loads(line)
            except json.JSONDecodeError:
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

    def _handle_non_streaming_response(self, response: requests.Response) -> str:
        """Handle non-streaming response from Ollama."""
        try:
            response_data = response.json()
        except json.JSONDecodeError:
            raise OllamaError(f"Invalid JSON response: {response.text[:200]}")

        if "error" in response_data:
            raise OllamaError(response_data["error"])

        if "response" in response_data:
            text = response_data["response"].strip()
            if not text:
                raise OllamaError("Ollama response missing content")
            return text
        else:
            raise OllamaError(f"Unexpected response format: {response_data}")

    def generate_with_persona(self, case_details: dict, persona: str = "junior_lawyer") -> str:
        """
        Generate text using a predefined persona.

        Args:
            case_details: Dictionary with case information
            persona: The persona to use (default: "junior_lawyer")

        Returns:
            Generated text
        """
        if persona == "junior_lawyer":
            prompt = self._construct_junior_lawyer_prompt(case_details)
        else:
            raise ValueError(f"Unknown persona: {persona}")

        return self.generate(prompt, stream=False)

    def _construct_junior_lawyer_prompt(self, case_details: dict) -> str:
        """Constructs a specific prompt for the junior lawyer persona."""
        prompt = f"""
You are a junior lawyer working for a Mexican government authority. You need to write the instructional text for a legal requirement document ('requerimiento').
Your writing style must be formal but also slightly rushed and imperfect. It should be legally sound but not perfectly polished.
- Use long, run-on sentences occasionally.
- Include minor grammatical mistakes or slightly awkward phrasing that a native speaker might make when writing quickly.
- Do NOT write perfect, academic, or overly complex legal prose.
- The document is an 'ASEGURAMIENTO' (asset freeze) for the company '{case_details.get("company_name", "the company in question")}'.
- The legal basis is Article 160 of the fiscal code and Article 142 of the credit institutions law.
- The goal is to instruct the bank to freeze funds up to a certain amount and report back the details.

Generate only the instructional text based on these requirements.
"""
        return prompt.strip()
