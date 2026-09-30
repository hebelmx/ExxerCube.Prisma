"""Docker orchestration for Ollama service - Enhanced Edition."""

from __future__ import annotations

import logging
import subprocess
import sys
import time
from typing import Optional

import requests

LOGGER = logging.getLogger(__name__)


class OrchestratorError(RuntimeError):
    """Raised when orchestration fails."""


def is_container_running(container_name: str) -> bool:
    """
    Checks if a Docker container with the given name is currently running.

    Args:
        container_name: Name of the Docker container

    Returns:
        True if container is running, False otherwise
    """
    LOGGER.debug("Checking if container '%s' is running...", container_name)
    command = ["docker", "ps", "-q", "-f", f"name={container_name}"]
    try:
        result = subprocess.run(command, capture_output=True, text=True, check=False, timeout=10)
        is_running = bool(result.stdout.strip())
        LOGGER.debug("Container '%s' running: %s", container_name, is_running)
        return is_running
    except subprocess.TimeoutExpired:
        LOGGER.warning("Timeout checking container status")
        return False
    except FileNotFoundError:
        raise OrchestratorError(
            "Docker command not found. Please ensure Docker is installed and in your PATH."
        )


def start_ollama_container(container_name: str, port: str, use_gpu: bool = True) -> None:
    """
    Starts the Ollama Docker container if it is not already running.

    Args:
        container_name: Name for the Docker container
        port: Port to expose the Ollama API
        use_gpu: Whether to use GPU (requires NVIDIA Docker runtime)

    Raises:
        OrchestratorError: If container fails to start
    """
    if is_container_running(container_name):
        LOGGER.info("Container '%s' is already running.", container_name)
        return

    LOGGER.info("Starting container '%s' on port %s...", container_name, port)

    command = [
        "docker", "run", "-d", "--rm",
        "-v", "ollama:/root/.ollama",
        "-p", f"{port}:{port}",
        "--name", container_name,
    ]

    if use_gpu:
        try:
            # Check if nvidia-docker is available
            subprocess.run(
                ["docker", "run", "--rm", "--gpus", "all", "hello-world"],
                capture_output=True,
                timeout=10,
                check=True,
            )
            command.insert(3, "--gpus")
            command.insert(4, "all")
            LOGGER.info("GPU support enabled")
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired, FileNotFoundError):
            LOGGER.warning("GPU support not available, running on CPU")

    command.append("ollama/ollama")

    try:
        result = subprocess.run(command, capture_output=True, text=True, check=True, timeout=30)
        LOGGER.info("Container started successfully. Container ID: %s", result.stdout.strip()[:12])
    except subprocess.CalledProcessError as e:
        raise OrchestratorError(
            f"Error starting Docker container: {e.stderr}\n"
            f"Please ensure Docker is running and the 'ollama/ollama' image is available."
        ) from e
    except subprocess.TimeoutExpired:
        raise OrchestratorError("Timeout while starting Docker container")


def wait_for_service_ready(api_url: str, retries: int = 30, delay: int = 10) -> None:
    """
    Polls the Ollama API until it's ready or times out.

    Args:
        api_url: Base URL of the Ollama API
        retries: Number of retry attempts
        delay: Delay between retries in seconds

    Raises:
        OrchestratorError: If service doesn't become ready in time
    """
    LOGGER.info("Waiting for Ollama service to become ready...")
    for i in range(retries):
        try:
            response = requests.get(api_url, timeout=5)
            if response.status_code == 200:
                LOGGER.info("Ollama service is ready!")
                return
        except requests.exceptions.ConnectionError:
            pass  # Service is not up yet, continue polling
        except requests.exceptions.RequestException as e:
            LOGGER.debug("Error polling service: %s", e)

        if i < retries - 1:
            LOGGER.debug(
                "Service not yet available. Retrying in %d seconds... (Attempt %d/%d)",
                delay, i + 1, retries
            )
            time.sleep(delay)

    raise OrchestratorError(
        f"Ollama service at {api_url} did not become ready after {retries * delay} seconds."
    )


def is_model_available(api_url: str, model: str) -> bool:
    """
    Checks via the Ollama API if a specific model is available.

    Args:
        api_url: Base URL of the Ollama API
        model: Name of the model to check

    Returns:
        True if model is available, False otherwise
    """
    LOGGER.debug("Checking for model '%s' at %s...", model, api_url)
    try:
        response = requests.get(f"{api_url}/api/tags", timeout=10)
        response.raise_for_status()
        data = response.json()
        available_models = [m["name"].split(':')[0] for m in data.get("models", [])]
        model_exists = model in available_models
        LOGGER.debug("Model '%s' available: %s", model, model_exists)
        return model_exists
    except requests.RequestException as e:
        LOGGER.warning("Could not connect to Ollama API to check for models: %s", e)
        return False


def pull_model(container_name: str, model: str) -> None:
    """
    Pulls the specified model using 'docker exec'.

    Args:
        container_name: Name of the Ollama container
        model: Name of the model to pull

    Raises:
        OrchestratorError: If model pull fails
    """
    LOGGER.info("Model '%s' not found. Pulling it now. This may take several minutes...", model)
    command = ["docker", "exec", container_name, "ollama", "pull", model]
    try:
        subprocess.run(command, check=True, timeout=1800)  # 30 minute timeout
        LOGGER.info("Successfully pulled model '%s'.", model)
    except subprocess.CalledProcessError as e:
        raise OrchestratorError(f"Error pulling model '{model}': {e}") from e
    except subprocess.TimeoutExpired:
        raise OrchestratorError(f"Timeout while pulling model '{model}'")


def prewarm_model(api_url: str, model: str) -> None:
    """
    Sends a trivial request to the model to force it to load into memory.

    Args:
        api_url: Base URL of the Ollama API
        model: Name of the model to prewarm

    Raises:
        OrchestratorError: If prewarming fails critically
    """
    LOGGER.info("Pre-warming model '%s'. This may take a few minutes on first run...", model)
    try:
        payload = {
            "model": model,
            "prompt": "Hi",
            "stream": False
        }
        response = requests.post(
            f"{api_url}/api/generate",
            json=payload,
            timeout=600  # 10 minute timeout for first load
        )
        response.raise_for_status()
        LOGGER.info("Model '%s' is warmed up and ready.", model)
    except requests.exceptions.RequestException as e:
        LOGGER.warning(
            "Warning: Could not pre-warm model. The first generation may be slow. Error: %s", e
        )


def ensure_ollama_ready(
    model: str,
    container_name: str = "ollama",
    port: str = "11434",
    use_gpu: bool = True,
    skip_prewarm: bool = False,
) -> str:
    """
    The main orchestration function. Ensures Ollama is running and the model is available.

    Args:
        model: Name of the model to use
        container_name: Name for the Docker container
        port: Port to expose the Ollama API
        use_gpu: Whether to use GPU acceleration
        skip_prewarm: Skip model prewarming (useful for batch operations)

    Returns:
        The API URL for the ready Ollama service

    Raises:
        OrchestratorError: If orchestration fails
    """
    LOGGER.info("=== Ensuring Ollama Service is Ready ===")
    api_url = f"http://localhost:{port}"

    # 1. Start container if not running
    start_ollama_container(container_name, port, use_gpu)

    # 2. Wait for the service to be responsive
    wait_for_service_ready(api_url)

    # 3. Check for and pull model if necessary
    if not is_model_available(api_url, model):
        pull_model(container_name, model)
        # Wait again after pulling, as the service might be busy
        wait_for_service_ready(api_url, retries=10, delay=5)
    else:
        LOGGER.info("Model '%s' is already available.", model)

    # 4. Pre-warm the model to load it into memory
    if not skip_prewarm:
        prewarm_model(api_url, model)

    LOGGER.info("=== Ollama Service is Ready ===")
    return api_url


if __name__ == '__main__':
    logging.basicConfig(level=logging.INFO)
    try:
        url = ensure_ollama_ready(model="llama3")
        print(f"\nSuccess! Ollama is ready at: {url}")
    except OrchestratorError as e:
        print(f"\nError: {e}", file=sys.stderr)
        sys.exit(1)
