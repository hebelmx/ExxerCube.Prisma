"""Tests for Ollama client functionality."""

import pytest
from unittest.mock import Mock, patch, MagicMock
import json

import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent.parent))

from prp1_generator import OllamaClient, OllamaError


class TestOllamaClient:
    """Test suite for OllamaClient."""

    def test_client_initialization(self):
        """Test client initializes with correct parameters."""
        client = OllamaClient("http://localhost:11434", "llama3", timeout=600)
        assert client.base_url == "http://localhost:11434"
        assert client.model == "llama3"
        assert client.timeout == 600

    def test_base_url_trailing_slash_removed(self):
        """Test that trailing slashes are removed from base URL."""
        client = OllamaClient("http://localhost:11434/", "llama3")
        assert client.base_url == "http://localhost:11434"

    @patch('prp1_generator.ollama_client.requests.post')
    def test_generate_streaming_success(self, mock_post):
        """Test successful streaming generation."""
        # Mock streaming response
        mock_response = Mock()
        mock_response.status_code = 200
        mock_response.iter_lines.return_value = [
            json.dumps({"response": "Hello ", "done": False}),
            json.dumps({"response": "world!", "done": True}),
        ]
        mock_post.return_value = mock_response

        client = OllamaClient("http://localhost:11434", "llama3")
        result = client.generate("Test prompt")

        assert result == "Hello world!"
        mock_post.assert_called_once()

    @patch('prp1_generator.ollama_client.requests.post')
    def test_generate_non_streaming_success(self, mock_post):
        """Test successful non-streaming generation."""
        # Mock non-streaming response
        mock_response = Mock()
        mock_response.status_code = 200
        mock_response.json.return_value = {"response": "Generated text"}
        mock_post.return_value = mock_response

        client = OllamaClient("http://localhost:11434", "llama3")
        result = client.generate("Test prompt", stream=False)

        assert result == "Generated text"

    @patch('prp1_generator.ollama_client.requests.post')
    def test_generate_handles_error_response(self, mock_post):
        """Test that error responses raise OllamaError."""
        mock_response = Mock()
        mock_response.status_code = 500
        mock_response.text = "Internal server error"
        mock_post.return_value = mock_response

        client = OllamaClient("http://localhost:11434", "llama3")
        with pytest.raises(OllamaError, match="HTTP 500"):
            client.generate("Test prompt")

    @patch('prp1_generator.ollama_client.requests.post')
    def test_generate_handles_connection_error(self, mock_post):
        """Test that connection errors are wrapped in OllamaError."""
        mock_post.side_effect = Exception("Connection refused")

        client = OllamaClient("http://localhost:11434", "llama3")
        with pytest.raises(OllamaError, match="request failed"):
            client.generate("Test prompt")

    @patch('prp1_generator.ollama_client.requests.post')
    def test_generate_handles_empty_response(self, mock_post):
        """Test that empty responses raise OllamaError."""
        mock_response = Mock()
        mock_response.status_code = 200
        mock_response.iter_lines.return_value = [
            json.dumps({"response": "", "done": True}),
        ]
        mock_post.return_value = mock_response

        client = OllamaClient("http://localhost:11434", "llama3")
        with pytest.raises(OllamaError, match="missing content"):
            client.generate("Test prompt")

    @patch('prp1_generator.ollama_client.requests.post')
    def test_generate_with_persona(self, mock_post):
        """Test persona-based generation."""
        mock_response = Mock()
        mock_response.status_code = 200
        mock_response.json.return_value = {"response": "Legal text"}
        mock_post.return_value = mock_response

        client = OllamaClient("http://localhost:11434", "llama3")
        result = client.generate_with_persona(
            {"company_name": "Test Corp"},
            persona="junior_lawyer"
        )

        assert result == "Legal text"
        # Verify the prompt was constructed
        call_args = mock_post.call_args
        payload = call_args[1]['json']
        assert "junior lawyer" in payload['prompt']
        assert "Test Corp" in payload['prompt']

    def test_generate_with_invalid_persona(self):
        """Test that invalid persona raises ValueError."""
        client = OllamaClient("http://localhost:11434", "llama3")
        with pytest.raises(ValueError, match="Unknown persona"):
            client.generate_with_persona({}, persona="invalid_persona")
