# ollama_orchestrator.py

import subprocess
import time
import requests
import sys

def is_container_running(container_name: str) -> bool:
    """Checks if a Docker container with the given name is currently running."""
    print(f"Checking if container '{container_name}' is running...")
    command = ["docker", "ps", "-q", "-f", f"name={container_name}"]
    result = subprocess.run(command, capture_output=True, text=True, check=False)
    return bool(result.stdout.strip())

def start_ollama_container(container_name: str, port: str):
    """Starts the Ollama Docker container if it is not already running."""
    if is_container_running(container_name):
        print(f"Container '{container_name}' is already running.")
        return

    print(f"Starting container '{container_name}' on port {port}...")
    command = [
        "docker", "run", "-d", "--rm", "--gpus", "all",
        "-v", "ollama:/root/.ollama",
        "-p", f"{port}:{port}",
        "--name", container_name,
        "ollama/ollama"
    ]
    try:
        subprocess.run(command, capture_output=True, text=True, check=True)
        print("Container started. Waiting for service to become available...")
        # We no longer sleep here; the wait_for_service_ready function will handle it.
    except subprocess.CalledProcessError as e:
        print(f"Error starting Docker container: {e.stderr}", file=sys.stderr)
        print("Please ensure Docker is running and the 'ollama/ollama' image is available.", file=sys.stderr)
        raise

def wait_for_service_ready(api_url: str, retries: int = 30, delay: int = 10):
    """Polls the Ollama API until it's ready or times out."""
    print("Connecting to Ollama service...")
    for i in range(retries):
        try:
            response = requests.get(api_url, timeout=5)
            if response.status_code == 200:
                print("Ollama service is ready.")
                return
        except requests.exceptions.ConnectionError:
            pass # Service is not up yet, continue polling
        except requests.exceptions.RequestException as e:
            print(f"An unexpected error occurred while polling: {e}", file=sys.stderr)

        print(f"Service not yet available. Retrying in {delay} seconds... (Attempt {i+1}/{retries})")
        time.sleep(delay)
    
    raise RuntimeError(f"Ollama service at {api_url} did not become ready after {retries * delay} seconds.")

def is_model_available(api_url: str, model: str) -> bool:
    """Checks via the Ollama API if a specific model is available."""
    print(f"Checking for model '{model}' at {api_url}...")
    try:
        response = requests.get(f"{api_url}/api/tags", timeout=10)
        response.raise_for_status()
        data = response.json()
        available_models = [m["name"].split(':')[0] for m in data.get("models", [])]
        return model in available_models
    except requests.RequestException as e:
        print(f"Could not connect to Ollama API to check for models: {e}", file=sys.stderr)
        return False

def pull_model(container_name: str, model: str):
    """Pulls the specified model using 'docker exec'."""
    print(f"Model '{model}' not found. Pulling it now. This may take a while...")
    command = ["docker", "exec", container_name, "ollama", "pull", model]
    try:
        subprocess.run(command, check=True)
        print(f"Successfully pulled model '{model}'.")
    except subprocess.CalledProcessError as e:
        print(f"Error pulling model '{model}': {e}", file=sys.stderr)
        raise

def prewarm_model(api_url: str, model: str):
    """Sends a trivial request to the model to force it to load into memory."""
    print(f"Pre-warming model '{model}'. This may take a few minutes on first run...")
    try:
        payload = {
            "model": model,
            "prompt": "Hi",
            "stream": False
        }
        # Use a very long timeout specifically for this pre-warming step
        requests.post(f"{api_url}/api/generate", json=payload, timeout=600)
        print(f"Model '{model}' is warmed up and ready.")
    except requests.exceptions.RequestException as e:
        print(f"Warning: Could not pre-warm model. The first generation may be slow or time out. Error: {e}", file=sys.stderr)


def ensure_ollama_ready(model: str, container_name: str = "ollama", port: str = "11434") -> str:
    """
    The main orchestration function. Ensures Ollama is running and the model is available.
    
    Returns:
        The API URL for the ready Ollama service.
    """
    print("--- Ensuring Ollama Service is Ready ---")
    api_url = f"http://localhost:{port}"
    
    # 1. Start container if not running
    start_ollama_container(container_name, port)
    
    # 2. Wait for the service to be responsive
    wait_for_service_ready(api_url)
    
    # 3. Check for and pull model if necessary
    if not is_model_available(api_url, model):
        pull_model(container_name, model)
        # It's good practice to wait again after pulling, as the service might be busy
        wait_for_service_ready(api_url, retries=10) 
    else:
        print(f"Model '{model}' is already available.")
        
    # 4. Pre-warm the model to load it into memory
    prewarm_model(api_url, model)
        
    print("--- Ollama Service is Ready ---")
    return api_url

if __name__ == '__main__':
    ensure_ollama_ready(model="llama3")

