# tests/test_ollama_orchestrator.py

import pytest
from unittest.mock import patch, MagicMock, call
import subprocess
import requests

# Add project root to the Python path
import os
import sys
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

# Module to be tested
import ollama_orchestrator

@patch('subprocess.run')
def test_is_container_running_when_it_is(mock_subprocess_run):
    mock_result = MagicMock()
    mock_result.stdout = "ollama_container_id\n"
    mock_subprocess_run.return_value = mock_result
    assert ollama_orchestrator.is_container_running('ollama') is True

@patch('subprocess.run')
def test_is_container_running_when_it_is_not(mock_subprocess_run):
    mock_result = MagicMock()
    mock_result.stdout = ""
    mock_subprocess_run.return_value = mock_result
    assert ollama_orchestrator.is_container_running('ollama') is False

@patch('ollama_orchestrator.is_container_running', return_value=False)
@patch('subprocess.run')
def test_start_container_when_not_running(mock_subprocess_run, mock_is_running):
    ollama_orchestrator.start_ollama_container('ollama', '11434')
    expected_command = [
        "docker", "run", "-d", "--rm", "--gpus", "all",
        "-v", "ollama:/root/.ollama",
        "-p", "11434:11434",
        "--name", "ollama",
        "ollama/ollama"
    ]
    mock_subprocess_run.assert_called_once_with(expected_command, capture_output=True, text=True, check=True)

@patch('ollama_orchestrator.is_container_running', return_value=True)
@patch('subprocess.run')
def test_start_container_when_already_running(mock_subprocess_run, mock_is_running):
    ollama_orchestrator.start_ollama_container('ollama', '11434')
    mock_subprocess_run.assert_not_called()

@patch('requests.get')
def test_is_model_available_when_it_is(mock_requests_get):
    mock_response = MagicMock()
    mock_response.json.return_value = {"models": [{"name": "llama3:latest"}]}
    mock_requests_get.return_value = mock_response
    assert ollama_orchestrator.is_model_available("http://localhost:11434", "llama3") is True

@patch('requests.get')
def test_is_model_available_when_it_is_not(mock_requests_get):
    mock_response = MagicMock()
    mock_response.json.return_value = {"models": [{"name": "llama2:latest"}]}
    mock_requests_get.return_value = mock_response
    assert ollama_orchestrator.is_model_available("http://localhost:11434", "llama3") is False

@patch('subprocess.run')
def test_pull_model(mock_subprocess_run):
    ollama_orchestrator.pull_model('ollama', 'llama3')
    expected_command = ["docker", "exec", "ollama", "ollama", "pull", "llama3"]
    mock_subprocess_run.assert_called_once_with(expected_command, check=True)

@patch('time.sleep', return_value=None)
@patch('requests.get')
def test_wait_for_service_ready(mock_requests_get, mock_sleep):
    mock_requests_get.side_effect = [
        requests.exceptions.ConnectionError,
        MagicMock(status_code=200)
    ]
    ollama_orchestrator.wait_for_service_ready("http://localhost:11434", retries=5, delay=1)
    assert mock_requests_get.call_count == 2
    assert mock_sleep.call_count == 1

@patch('time.sleep', return_value=None)
@patch('requests.get')
def test_wait_for_service_fails_after_retries(mock_requests_get, mock_sleep):
    mock_requests_get.side_effect = requests.exceptions.ConnectionError
    with pytest.raises(RuntimeError):
        ollama_orchestrator.wait_for_service_ready("http://localhost:11434", retries=3, delay=1)
    assert mock_requests_get.call_count == 3

@patch('requests.post')
def test_prewarm_model(mock_requests_post):
    """
    Tests that the pre-warming function sends a request with the correct payload.
    """
    api_url = "http://localhost:11434"
    model = "llama3"
    ollama_orchestrator.prewarm_model(api_url, model)

    expected_payload = {
        "model": model,
        "prompt": "Hi",
        "stream": False
    }
    mock_requests_post.assert_called_once_with(
        f"{api_url}/api/generate",
        json=expected_payload,
        timeout=600  # Check for a long timeout
    )

@patch('ollama_orchestrator.is_container_running', return_value=True)
@patch('ollama_orchestrator.start_ollama_container')
@patch('ollama_orchestrator.wait_for_service_ready', return_value=None)
@patch('ollama_orchestrator.is_model_available', return_value=True)
@patch('ollama_orchestrator.pull_model')
@patch('ollama_orchestrator.prewarm_model')
def test_ensure_ollama_ready_prewarms_model(mock_prewarm, mock_pull, mock_is_model, mock_wait, mock_start, mock_is_running):
    """
    Tests that the main orchestrator calls the pre-warm function.
    """
    api_url = "http://localhost:11434"
    model = "llama3"
    
    ollama_orchestrator.ensure_ollama_ready(model=model)

    mock_wait.assert_called_once_with(api_url)
    mock_is_model.assert_called_once_with(api_url, model)
    mock_pull.assert_not_called() # Model is available, so pull should not be called
    mock_prewarm.assert_called_once_with(api_url, model)
