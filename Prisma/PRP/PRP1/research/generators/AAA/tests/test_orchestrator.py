"""Tests for Ollama orchestrator functionality."""

import pytest
from unittest.mock import Mock, patch, MagicMock
import subprocess

import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent.parent))

from prp1_generator import OrchestratorError
from prp1_generator.ollama_orchestrator import (
    is_container_running,
    start_ollama_container,
    wait_for_service_ready,
    is_model_available,
    pull_model,
    prewarm_model,
    ensure_ollama_ready,
)


class TestContainerManagement:
    """Test container management functions."""

    @patch('prp1_generator.ollama_orchestrator.subprocess.run')
    def test_is_container_running_true(self, mock_run):
        """Test detecting a running container."""
        mock_run.return_value = Mock(stdout="abc123\n")
        result = is_container_running("ollama")
        assert result is True

    @patch('prp1_generator.ollama_orchestrator.subprocess.run')
    def test_is_container_running_false(self, mock_run):
        """Test detecting a stopped container."""
        mock_run.return_value = Mock(stdout="")
        result = is_container_running("ollama")
        assert result is False

    @patch('prp1_generator.ollama_orchestrator.subprocess.run')
    def test_is_container_running_docker_not_found(self, mock_run):
        """Test handling when Docker is not installed."""
        mock_run.side_effect = FileNotFoundError()
        with pytest.raises(OrchestratorError, match="Docker command not found"):
            is_container_running("ollama")

    @patch('prp1_generator.ollama_orchestrator.is_container_running')
    @patch('prp1_generator.ollama_orchestrator.subprocess.run')
    def test_start_container_already_running(self, mock_run, mock_is_running):
        """Test that starting an already running container is a no-op."""
        mock_is_running.return_value = True
        start_ollama_container("ollama", "11434")
        mock_run.assert_not_called()

    @patch('prp1_generator.ollama_orchestrator.is_container_running')
    @patch('prp1_generator.ollama_orchestrator.subprocess.run')
    def test_start_container_success(self, mock_run, mock_is_running):
        """Test successful container start."""
        mock_is_running.return_value = False
        # First call for GPU check (fails), second for container start
        mock_run.side_effect = [
            subprocess.CalledProcessError(1, "cmd"),  # GPU check fails
            Mock(stdout="container_id_123\n")  # Container starts
        ]

        start_ollama_container("ollama", "11434", use_gpu=True)
        assert mock_run.call_count == 2

    @patch('prp1_generator.ollama_orchestrator.is_container_running')
    @patch('prp1_generator.ollama_orchestrator.subprocess.run')
    def test_start_container_with_gpu(self, mock_run, mock_is_running):
        """Test starting container with GPU support."""
        mock_is_running.return_value = False
        # First call for GPU check (succeeds), second for container start
        mock_run.side_effect = [
            Mock(stdout=""),  # GPU check succeeds
            Mock(stdout="container_id_123\n")  # Container starts
        ]

        start_ollama_container("ollama", "11434", use_gpu=True)
        # Check that --gpus flag was used
        call_args = mock_run.call_args_list[1][0][0]
        assert "--gpus" in call_args
        assert "all" in call_args


class TestServiceReadiness:
    """Test service readiness checks."""

    @patch('prp1_generator.ollama_orchestrator.requests.get')
    def test_wait_for_service_ready_immediate(self, mock_get):
        """Test service ready immediately."""
        mock_get.return_value = Mock(status_code=200)
        wait_for_service_ready("http://localhost:11434", retries=5, delay=1)
        mock_get.assert_called_once()

    @patch('prp1_generator.ollama_orchestrator.requests.get')
    @patch('prp1_generator.ollama_orchestrator.time.sleep')
    def test_wait_for_service_ready_retry(self, mock_sleep, mock_get):
        """Test service becomes ready after retries."""
        # Fail twice, then succeed
        mock_get.side_effect = [
            Exception("Connection refused"),
            Exception("Connection refused"),
            Mock(status_code=200)
        ]
        wait_for_service_ready("http://localhost:11434", retries=5, delay=1)
        assert mock_get.call_count == 3
        assert mock_sleep.call_count == 2

    @patch('prp1_generator.ollama_orchestrator.requests.get')
    @patch('prp1_generator.ollama_orchestrator.time.sleep')
    def test_wait_for_service_timeout(self, mock_sleep, mock_get):
        """Test timeout when service never becomes ready."""
        mock_get.side_effect = Exception("Connection refused")
        with pytest.raises(OrchestratorError, match="did not become ready"):
            wait_for_service_ready("http://localhost:11434", retries=3, delay=1)


class TestModelManagement:
    """Test model management functions."""

    @patch('prp1_generator.ollama_orchestrator.requests.get')
    def test_is_model_available_true(self, mock_get):
        """Test detecting available model."""
        mock_response = Mock()
        mock_response.json.return_value = {
            "models": [
                {"name": "llama3:latest"},
                {"name": "mistral:latest"}
            ]
        }
        mock_get.return_value = mock_response

        result = is_model_available("http://localhost:11434", "llama3")
        assert result is True

    @patch('prp1_generator.ollama_orchestrator.requests.get')
    def test_is_model_available_false(self, mock_get):
        """Test detecting unavailable model."""
        mock_response = Mock()
        mock_response.json.return_value = {
            "models": [
                {"name": "mistral:latest"}
            ]
        }
        mock_get.return_value = mock_response

        result = is_model_available("http://localhost:11434", "llama3")
        assert result is False

    @patch('prp1_generator.ollama_orchestrator.subprocess.run')
    def test_pull_model_success(self, mock_run):
        """Test successful model pull."""
        mock_run.return_value = Mock()
        pull_model("ollama", "llama3")
        mock_run.assert_called_once()
        call_args = mock_run.call_args[0][0]
        assert "docker" in call_args
        assert "exec" in call_args
        assert "llama3" in call_args

    @patch('prp1_generator.ollama_orchestrator.subprocess.run')
    def test_pull_model_failure(self, mock_run):
        """Test failed model pull."""
        mock_run.side_effect = subprocess.CalledProcessError(1, "cmd")
        with pytest.raises(OrchestratorError, match="Error pulling model"):
            pull_model("ollama", "llama3")

    @patch('prp1_generator.ollama_orchestrator.requests.post')
    def test_prewarm_model_success(self, mock_post):
        """Test successful model prewarming."""
        mock_post.return_value = Mock(status_code=200)
        prewarm_model("http://localhost:11434", "llama3")
        mock_post.assert_called_once()

    @patch('prp1_generator.ollama_orchestrator.requests.post')
    def test_prewarm_model_failure_warning(self, mock_post):
        """Test that prewarm failures only warn, don't fail."""
        mock_post.side_effect = Exception("Timeout")
        # Should not raise, only warn
        prewarm_model("http://localhost:11434", "llama3")


class TestOrchestratorIntegration:
    """Test full orchestration workflow."""

    @patch('prp1_generator.ollama_orchestrator.prewarm_model')
    @patch('prp1_generator.ollama_orchestrator.is_model_available')
    @patch('prp1_generator.ollama_orchestrator.wait_for_service_ready')
    @patch('prp1_generator.ollama_orchestrator.start_ollama_container')
    def test_ensure_ollama_ready_model_exists(
        self, mock_start, mock_wait, mock_is_available, mock_prewarm
    ):
        """Test orchestration when model already exists."""
        mock_is_available.return_value = True

        result = ensure_ollama_ready("llama3")

        assert result == "http://localhost:11434"
        mock_start.assert_called_once()
        mock_wait.assert_called_once()
        mock_prewarm.assert_called_once()

    @patch('prp1_generator.ollama_orchestrator.prewarm_model')
    @patch('prp1_generator.ollama_orchestrator.pull_model')
    @patch('prp1_generator.ollama_orchestrator.is_model_available')
    @patch('prp1_generator.ollama_orchestrator.wait_for_service_ready')
    @patch('prp1_generator.ollama_orchestrator.start_ollama_container')
    def test_ensure_ollama_ready_pulls_model(
        self, mock_start, mock_wait, mock_is_available, mock_pull, mock_prewarm
    ):
        """Test orchestration when model needs to be pulled."""
        mock_is_available.return_value = False

        result = ensure_ollama_ready("llama3")

        assert result == "http://localhost:11434"
        mock_pull.assert_called_once_with("ollama", "llama3")
        assert mock_wait.call_count == 2  # Once after start, once after pull

    @patch('prp1_generator.ollama_orchestrator.is_model_available')
    @patch('prp1_generator.ollama_orchestrator.wait_for_service_ready')
    @patch('prp1_generator.ollama_orchestrator.start_ollama_container')
    def test_ensure_ollama_ready_skip_prewarm(
        self, mock_start, mock_wait, mock_is_available
    ):
        """Test orchestration with prewarm skipped."""
        mock_is_available.return_value = True

        result = ensure_ollama_ready("llama3", skip_prewarm=True)

        assert result == "http://localhost:11434"
        # prewarm_model should not be called (we didn't patch it)
