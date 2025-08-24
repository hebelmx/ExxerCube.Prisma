#!/usr/bin/env python3
"""
Generate a corpus of fictitious legal requirements using Ollama.
This script creates diverse, realistic Spanish legal documents for testing OCR systems.
"""

import json
import random
import hashlib
import requests
from datetime import datetime, timedelta
from pathlib import Path
from typing import Dict, List, Any
import argparse
import time
from tqdm import tqdm

class RequerimientoGenerator:
    def __init__(self, entities_file: str = "entities.json", 
                 prompt_template_file: str = "prompt_template.txt",
                 ollama_model: str = "llama3.2:latest",
                 ollama_url: str = "http://localhost:11434"):
        """
        Initialize the generator with entities and prompt template.
        
        Args:
            entities_file: Path to JSON file with legal entities
            prompt_template_file: Path to prompt template file
            ollama_model: Ollama model to use
            ollama_url: Ollama API endpoint
        """
        self.ollama_model = ollama_model
        self.ollama_url = ollama_url
        
        # Load entities
        with open(entities_file, 'r', encoding='utf-8') as f:
            self.entities = json.load(f)
        
        # Load prompt template
        with open(prompt_template_file, 'r', encoding='utf-8') as f:
            self.prompt_template = f.read()
    
    def generate_random_date(self, start_year: int = 2023, end_year: int = 2024) -> str:
        """Generate a random date in YYYY-MM-DD format."""
        start_date = datetime(start_year, 1, 1)
        end_date = datetime(end_year, 12, 31)
        random_days = random.randint(0, (end_date - start_date).days)
        return (start_date + timedelta(days=random_days)).strftime("%Y-%m-%d")
    
    def generate_context(self) -> Dict[str, Any]:
        """Generate random context for a requirement."""
        context = {
            "fecha": self.generate_random_date(),
            "autoridad": random.choice(self.entities["autoridades"]),
            "expediente": random.choice(self.entities["numeros_expediente"]),
            "tipo_requerimiento": random.choice(self.entities["tipos_requerimiento"]),
            "subtipo": random.choice(self.entities["subtipos_requerimiento"]),
            "fundamento": random.choice(self.entities["fundamentos_legales"]),
            "motivacion": random.choice(self.entities["motivaciones"]),
            "entidad_financiera": random.choice(self.entities["entidades_financieras"]),
            "moneda": random.choice(self.entities["monedas"]),
            "frases": random.sample(self.entities["frases_tipicas"], k=3)
        }
        
        # Add parties (personas or empresas)
        num_parties = random.randint(1, 3)
        if random.random() > 0.5:
            context["partes"] = random.sample(self.entities["nombres_personas"], k=min(num_parties, len(self.entities["nombres_personas"])))
        else:
            context["partes"] = random.sample(self.entities["empresas"], k=min(num_parties, len(self.entities["empresas"])))
        
        # Add amount if applicable
        if any(word in context["tipo_requerimiento"].lower() for word in ["embargo", "aseguramiento", "retención"]):
            context["monto"] = random.choice(self.entities["montos_comunes"])
            context["monto_texto"] = f"${context['monto']:,.2f} {context['moneda']}"
        
        # Add crypto if relevant
        if random.random() > 0.85:  # 15% chance of crypto involvement
            context["activo_virtual"] = random.choice(self.entities["activos_virtuales"])
        
        return context
    
    def call_ollama(self, prompt: str) -> str:
        """
        Call Ollama API to generate text.
        
        Args:
            prompt: The prompt to send to Ollama
            
        Returns:
            Generated text from Ollama
        """
        try:
            response = requests.post(
                f"{self.ollama_url}/api/generate",
                json={
                    "model": self.ollama_model,
                    "prompt": prompt,
                    "stream": False,
                    "options": {
                        "temperature": 0.8,
                        "top_p": 0.9,
                        "num_predict": 800,
                        "stop": ["---", "###", "```"]
                    }
                },
                timeout=30
            )
            response.raise_for_status()
            result = response.json()
            return result.get("response", "").strip()
        except requests.exceptions.RequestException as e:
            print(f"Error calling Ollama: {e}")
            return None
    
    def generate_requerimiento(self) -> Dict[str, str]:
        """
        Generate a single requirement document.
        
        Returns:
            Dictionary with 'text' and 'hash' keys
        """
        context = self.generate_context()
        
        # Format context for prompt
        context_str = json.dumps(context, ensure_ascii=False, indent=2)
        prompt = self.prompt_template.replace("{context}", context_str)
        
        # Generate text using Ollama
        text = self.call_ollama(prompt)
        
        if not text:
            # Fallback to template-based generation
            text = self.generate_fallback_text(context)
        
        # Calculate SHA256 hash
        hash_value = hashlib.sha256(text.encode('utf-8')).hexdigest()
        
        return {
            "text": text,
            "hash": hash_value,
            "metadata": context
        }
    
    def generate_fallback_text(self, context: Dict[str, Any]) -> str:
        """Generate fallback text if Ollama fails."""
        text = f"""
PODER JUDICIAL DE LA FEDERACIÓN
{context['autoridad']}

EXPEDIENTE: {context['expediente']}
OFICIO: {random.randint(1000, 9999)}/{datetime.now().year}

{context['entidad_financiera']}
PRESENTE

En los autos del expediente número {context['expediente']}, relativo al {context['motivacion']}, 
promovido por {context['partes'][0] if context['partes'] else 'LA PARTE ACTORA'}, 
{random.choice(['en contra de', 'respecto de'])} {context['partes'][1] if len(context['partes']) > 1 else 'LA PARTE DEMANDADA'}, 
se dictó un acuerdo que a la letra dice:

{context['frases'][0]}, {context['fundamento']}, y en atención a lo solicitado por la parte actora, 
SE ORDENA girar atento oficio a {context['entidad_financiera']}, a efecto de que proceda a realizar 
{context['tipo_requerimiento'].upper()} sobre {context['subtipo']} que el demandado tenga o llegare a tener 
en esa institución.

{context.get('monto_texto', '')}

Lo anterior deberá cumplimentarse en un término no mayor a TRES DÍAS HÁBILES contados a partir de la 
recepción del presente, {context['frases'][1]}.

BAJO APERCIBIMIENTO que de no dar cumplimiento a lo ordenado, se le impondrá una multa equivalente a 
CIEN UNIDADES DE MEDIDA Y ACTUALIZACIÓN, sin perjuicio de las demás sanciones que resulten aplicables.

{context['frases'][2]}.

{context['autoridad']}
{self.generate_random_date()}

LIC. {random.choice(self.entities['nombres_personas']).split()[0:2]}
JUEZ

LIC. {random.choice(self.entities['nombres_personas']).split()[0:2]}
SECRETARIO DE ACUERDOS
"""
        return text.strip()
    
    def generate_corpus(self, num_documents: int = 100, output_file: str = "corpus_requerimientos.json") -> None:
        """
        Generate a corpus of requirement documents.
        
        Args:
            num_documents: Number of documents to generate
            output_file: Output JSON file path
        """
        corpus = []
        
        print(f"Generating {num_documents} documents using {self.ollama_model}...")
        
        for i in tqdm(range(num_documents), desc="Generating documents"):
            try:
                doc = self.generate_requerimiento()
                doc["id"] = f"REQ{i+1:04d}"
                corpus.append(doc)
                
                # Small delay to avoid overwhelming Ollama
                time.sleep(0.5)
                
            except Exception as e:
                print(f"Error generating document {i+1}: {e}")
                continue
        
        # Save corpus to JSON
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(corpus, f, ensure_ascii=False, indent=2)
        
        print(f"Corpus saved to {output_file}")
        
        # Also save in markdown format for readability
        markdown_file = output_file.replace('.json', '.md')
        with open(markdown_file, 'w', encoding='utf-8') as f:
            f.write("# Corpus de Requerimientos Legales\n\n")
            for doc in corpus:
                f.write(f"<--Start Requirement {doc['id']}-->\n")
                f.write("**Requerimiento**\n")
                f.write(doc['text'])
                f.write("\n\n**Hash**\n")
                f.write(doc['hash'])
                f.write("\n<--End Requirement-->\n\n")
        
        print(f"Markdown version saved to {markdown_file}")

def check_ollama_availability(url: str = "http://localhost:11434") -> bool:
    """Check if Ollama is running and available."""
    try:
        response = requests.get(f"{url}/api/tags", timeout=5)
        return response.status_code == 200
    except:
        return False

def main():
    parser = argparse.ArgumentParser(description="Generate corpus of legal requirements using Ollama")
    parser.add_argument("--num", type=int, default=100, help="Number of documents to generate")
    parser.add_argument("--output", default="corpus_requerimientos.json", help="Output file name")
    parser.add_argument("--model", default="llama3.2:latest", help="Ollama model to use")
    parser.add_argument("--ollama-url", default="http://localhost:11434", help="Ollama API URL")
    parser.add_argument("--entities", default="entities.json", help="Entities JSON file")
    parser.add_argument("--template", default="prompt_template.txt", help="Prompt template file")
    
    args = parser.parse_args()
    
    # Check if Ollama is available
    if not check_ollama_availability(args.ollama_url):
        print(f"Warning: Ollama not available at {args.ollama_url}")
        print("Please ensure Ollama is running with: ollama serve")
        print("And that you have pulled a model: ollama pull llama3.2:latest")
        response = input("Continue with fallback generation? (y/n): ")
        if response.lower() != 'y':
            return
    
    # Check if required files exist
    for file in [args.entities, args.template]:
        if not Path(file).exists():
            print(f"Error: Required file {file} not found")
            return
    
    # Initialize generator
    generator = RequerimientoGenerator(
        entities_file=args.entities,
        prompt_template_file=args.template,
        ollama_model=args.model,
        ollama_url=args.ollama_url
    )
    
    # Generate corpus
    generator.generate_corpus(
        num_documents=args.num,
        output_file=args.output
    )

if __name__ == "__main__":
    main()