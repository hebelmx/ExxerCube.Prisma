# ollama_client.py

import requests
import json

# The base URL is no longer a constant here; it will be passed in.

def get_llm_narrative(api_url: str, persona_prompt: str, model: str = "llama3") -> str:
    """
    Generates narrative text by querying the Ollama LLM service.

    Args:
        api_url: The full base URL of the Ollama API.
        persona_prompt: The detailed prompt defining the persona and task.
        model: The name of the Ollama model to use.

    Returns:
        The generated text from the LLM.
    """
    generate_url = f"{api_url}/api/generate"
    try:
        payload = {
            "model": model,
            "prompt": persona_prompt,
            "stream": False
        }
        
        response = requests.post(generate_url, json=payload, timeout=180) # Increased timeout to 3 minutes
        response.raise_for_status()

        response_data = response.json()
        
        if "response" in response_data:
            return response_data["response"].strip()
        else:
            print(f"Warning: Ollama response did not contain 'response' key. Full response: {response_data}")
            return "Error: Unexpected response format from Ollama."

    except requests.exceptions.RequestException as e:
        print(f"Error connecting to Ollama service at {generate_url}: {e}")
        return "Error: Could not connect to LLM service."
    except json.JSONDecodeError:
        print(f"Error decoding JSON response from Ollama. Response text: {response.text}")
        return "Error: Invalid JSON response from LLM service."

def construct_junior_lawyer_prompt(case_details: dict) -> str:
    """
    Constructs a specific prompt for the "junior lawyer" persona.
    """
    # This function can be expanded to include more details from the case
    # For now, it's a static prompt that defines the persona.
    
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

if __name__ == '__main__':
    # Example of how to use the client directly
    print("Testing Ollama client...")
    
    # This is a placeholder for real case details
    details = {"company_name": "AEROLINEAS PAYASO ORGULLO NACIONAL"}
    
    prompt = construct_junior_lawyer_prompt(details)
    
    print("\n--- PROMPT ---")
    print(prompt)
    
    print("\n--- OLLAMA RESPONSE ---")
    # Make sure you have Ollama running with a model like 'llama3'
    # docker run -d -v ollama:/root/.ollama -p 11434:11434 --name ollama ollama/ollama
    # docker exec ollama ollama pull llama3
    narrative = get_llm_narrative(prompt)
    print(narrative)

