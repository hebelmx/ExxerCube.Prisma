

import os
import json
import argparse
from typing import List, Dict, Any
import PyPDF2
import requests

# --- Configuration ---
# Directory where your PDF files are located.
PDF_DIRECTORY = r"F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Entidades Legales"

# Output file where the final structured JSON data will be saved.
OUTPUT_FILE = "extracted_authorities.json"

# Ollama API endpoint.
OLLAMA_ENDPOINT = "http://localhost:11434/api/generate"

# --- Core Functions ---

def extract_text_from_pdf(pdf_path: str) -> str:
    """
    Extracts all text from a given PDF file.
    """
    text = ""
    try:
        with open(pdf_path, "rb") as file:
            pdf_reader = PyPDF2.PdfReader(file)
            for page in pdf_reader.pages:
                text += page.extract_text() or ""
        print(f"Successfully extracted text from: {os.path.basename(pdf_path)}")
    except FileNotFoundError:
        print(f"Error: File not found at {pdf_path}")
    except Exception as e:
        print(f"An error occurred while reading {pdf_path}: {e}")
    return text

def extract_structured_data_with_ollama(text_content: str, model: str) -> Dict[str, Any]:
    """
    Calls a local Ollama container to extract structured data from text.

    Args:
        text_content: The raw text extracted from a PDF.
        model: The name of the Ollama model to use.

    Returns:
        A dictionary with the structured data, or an error dictionary.
    """
    prompt = f"""
    From the following text from a legal document about Mexican government bodies, identify the primary legal authority ("Autoridad"), its specific powers ("Facultades"), and any sub-authorities ("componentes").
    Structure the output as a single, clean JSON object with the following keys: "autoridad_principal", "descripcion", "facultades", and "componentes".
    - "autoridad_principal": A string with the name of the main entity.
    - "descripcion": A brief string describing the entity.
    - "facultades": A list of strings, where each string is a power or function.
    - "componentes": A list of objects, where each object represents a sub-authority and has "nombre", "descripcion", and "facultades" keys. If no sub-authorities are found, this should be an empty list.

    IMPORTANT: Respond ONLY with the valid JSON object and nothing else. Do not include any introductory text, markdown code blocks, or explanations.

    Text to analyze:
    ---
    {text_content[:8000]}
    ---
    """
    print(f"Sending request to Ollama with model '{model}'...")
    try:
        response = requests.post(
            OLLAMA_ENDPOINT,
            json={
                "model": model,
                "prompt": prompt,
                "format": "json",
                "stream": False
            },
            timeout=120  # Increased timeout for potentially long generation
        )
        response.raise_for_status()

        response_json_str = response.json().get("response", "{}")
        structured_data = json.loads(response_json_str)

        print("Successfully extracted structured data using Ollama.")
        return structured_data

    except requests.exceptions.ConnectionError:
        print(f"\n--- Ollama Connection Error ---")
        print(f"Could not connect to the Ollama endpoint at '{OLLAMA_ENDPOINT}'.")
        print("Please ensure your Ollama container is running and accessible.")
        return {"error": "Ollama connection failed."}
    except requests.exceptions.RequestException as e:
        print(f"\n--- Ollama Request Error ---")
        print(f"An error occurred during the request to Ollama: {e}")
        return {"error": f"Ollama request failed: {e}"}
    except json.JSONDecodeError:
        print(f"\n--- JSON Decode Error ---")
        print("Failed to decode the JSON response from Ollama.")
        print(f"Raw response received: {response_json_str}")
        return {"error": "Failed to decode JSON from Ollama."}
    except Exception as e:
        print(f"\nAn unexpected error occurred: {e}")
        return {"error": str(e)}

def save_to_json(data: List[Dict[str, Any]], output_path: str):
    """
    Saves the provided data to a JSON file.
    """
    try:
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=4, ensure_ascii=False)
        print(f"\nSuccessfully saved data for {len(data)} files to {output_path}")
    except Exception as e:
        print(f"An error occurred while saving the JSON file: {e}")

# --- Main Execution ---

def main():
    """
    Main function to run the script.
    """
    parser = argparse.ArgumentParser(
        description="Extract legal authority information from PDF documents using Ollama."
    )
    parser.add_argument(
        "--model",
        type=str,
        default="llama3",
        help="The name of the Ollama model to use (e.g., 'llama3', 'mistral'). Default: 'llama3'."
    )
    parser.add_argument(
        "--skip-llm",
        action="store_true",
        help="If set, skips the Ollama processing and only extracts raw text."
    )
    args = parser.parse_args()

    # --- Step 1: Process all PDFs to get raw text ---
    if not os.path.isdir(PDF_DIRECTORY):
        print(f"Error: Source directory not found at '{PDF_DIRECTORY}'")
        return

    all_results = []
    pdf_files = [f for f in os.listdir(PDF_DIRECTORY) if f.lower().endswith(".pdf")]

    if not pdf_files:
        print(f"No PDF files found in '{PDF_DIRECTORY}'. Exiting.")
        return

    for filename in pdf_files:
        pdf_path = os.path.join(PDF_DIRECTORY, filename)
        text_content = extract_text_from_pdf(pdf_path)

        if not text_content:
            print(f"Skipping LLM processing for {filename} due to empty text content.")
            continue

        if args.skip_llm:
            # If skipping LLM, just collect the raw text
            all_results.append({
                "source_file": filename,
                "text_content": text_content
            })
        else:
            # --- Step 2: Process text with Ollama ---
            structured_info = extract_structured_data_with_ollama(text_content, args.model)
            structured_info['source_file'] = filename
            all_results.append(structured_info)

    # --- Step 3: Save the results ---
    if args.skip_llm:
        save_to_json(all_results, "raw_text_extraction.json")
        print("\nLLM processing was skipped.")
        print("Raw text has been extracted to 'raw_text_extraction.json'.")
    else:
        save_to_json(all_results, OUTPUT_FILE)

if __name__ == "__main__":
    main()

