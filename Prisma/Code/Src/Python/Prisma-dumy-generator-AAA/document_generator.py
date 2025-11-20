# document_generator.py

import xml.etree.ElementTree as ET
from faker import Faker
import random
import datetime
import os
import argparse
from tqdm import tqdm

# Import the client and the new orchestrator
from ollama_client import get_llm_narrative, construct_junior_lawyer_prompt
from ollama_orchestrator import ensure_ollama_ready


# Initialize Faker for Mexican Spanish
fake = Faker('es_MX')

def create_xml_expediente(ollama_api_url: str, llm_model: str):
    """
    Generates a single, realistic XML expediente document based on the CNBV schema.
    
    Args:
        ollama_api_url: The base URL for the Ollama API service.
        llm_model: The name of the LLM model to use.
    """
    # --- Root Element ---
    cnbv_namespace = "http://www.cnbv.gob.mx"
    
    def nstag(tag):
        return f"{{{cnbv_namespace}}}{tag}"

    ET.register_namespace('', cnbv_namespace)
    
    root = ET.Element(nstag("Expediente"), {
        "xmlns:xsi": "http://www.w3.org/2001/XMLSchema-instance",
        "xmlns:xsd": "http://www.w3.org/2001/XMLSchema"
    })

    # --- Generate Fake Data ---
    company_name = fake.company()
    
    # --- CNBV Header Fields ---
    oficio_parts = [
        "222",
        fake.random_element(elements=('AAA', 'BBB', 'CCC')),
        str(fake.random_number(digits=10, fix_len=True)),
        str(datetime.date.today().year)
    ]
    ET.SubElement(root, nstag("Cnbv_NumeroOficio")).text = "/".join(oficio_parts)
    
    ET.SubElement(root, nstag("Cnbv_NumeroExpediente")).text = f"A/AS1-{fake.random_number(digits=4, fix_len=True)}-{fake.random_number(digits=6, fix_len=True)}-{oficio_parts[1]}"
    ET.SubElement(root, nstag("Cnbv_SolicitudSiara")).text = f"AGAFADAFSON2/{datetime.date.today().year}/{fake.random_number(digits=6, fix_len=True)}"
    ET.SubElement(root, nstag("Cnbv_Folio")).text = str(fake.random_number(digits=4))
    ET.SubElement(root, nstag("Cnbv_OficioYear")).text = str(datetime.date.today().year)
    
    area_clave = random.choice([3, 4, 5])
    area_desc = "ASEGURAMIENTO" if area_clave == 3 else "INFORMACION"
    ET.SubElement(root, nstag("Cnbv_AreaClave")).text = str(area_clave)
    ET.SubElement(root, nstag("Cnbv_AreaDescripcion")).text = area_desc
    
    ET.SubElement(root, nstag("Cnbv_FechaPublicacion")).text = datetime.date.today().isoformat()
    ET.SubElement(root, nstag("Cnbv_DiasPlazo")).text = str(random.choice([3, 5, 7, 10]))
    
    ET.SubElement(root, nstag("AutoridadNombre")).text = fake.company() + " " + fake.company_suffix()
    
    nombre_solicitante = ET.SubElement(root, nstag("NombreSolicitante"))
    nombre_solicitante.set("xsi:nil", "true")
    
    ET.SubElement(root, nstag("Referencia")).text = " " * 25
    ET.SubElement(root, nstag("Referencia1")).text = " " * 25
    ET.SubElement(root, nstag("Referencia2")).text = f"IMSSCOB/{fake.random_number(digits=2)}/{fake.random_number(digits=2)}/{fake.random_number(digits=6)}/{datetime.date.today().year}"
    
    ET.SubElement(root, nstag("TieneAseguramiento")).text = "true" if area_desc == "ASEGURAMIENTO" else "false"

    # --- SolicitudPartes ---
    solicitud_partes = ET.SubElement(root, nstag("SolicitudPartes"))
    ET.SubElement(solicitud_partes, nstag("ParteId")).text = "1"
    ET.SubElement(solicitud_partes, nstag("Caracter")).text = "Patrón Determinado"
    ET.SubElement(solicitud_partes, nstag("Persona")).text = "Moral"
    ET.SubElement(solicitud_partes, nstag("Paterno"))
    ET.SubElement(solicitud_partes, nstag("Materno"))
    ET.SubElement(solicitud_partes, nstag("Nombre")).text = company_name
    ET.SubElement(solicitud_partes, nstag("Rfc")).text = " " * 13

    # --- SolicitudEspecifica ---
    solicitud_especifica = ET.SubElement(root, nstag("SolicitudEspecifica"))
    ET.SubElement(solicitud_especifica, nstag("SolicitudEspecificaId")).text = "1"
    
    case_details = {"company_name": company_name}
    prompt = construct_junior_lawyer_prompt(case_details)
    instrucciones_text = get_llm_narrative(ollama_api_url, prompt, model=llm_model)
    
    ET.SubElement(solicitud_especifica, nstag("InstruccionesCuentasPorConocer")).text = instrucciones_text

    personas_solicitud = ET.SubElement(solicitud_especifica, nstag("PersonasSolicitud"))
    ET.SubElement(personas_solicitud, nstag("PersonaId")).text = "1"
    ET.SubElement(personas_solicitud, nstag("Caracter")).text = "Patrón Determinado"
    ET.SubElement(personas_solicitud, nstag("Persona")).text = "Moral"
    ET.SubElement(personas_solicitud, nstag("Paterno"))
    ET.SubElement(personas_solicitud, nstag("Materno"))
    ET.SubElement(personas_solicitud, nstag("Nombre")).text = f"{company_name}, S.A. DE C.V."
    ET.SubElement(personas_solicitud, nstag("Rfc")).text = fake.rfc(natural=False)
    ET.SubElement(personas_solicitud, nstag("Relacion"))
    
    colonia = fake.random_element(elements=('Centro', 'Roma Norte', 'Condesa', 'Polanco', 'Juárez'))
    ET.SubElement(personas_solicitud, nstag("Domicilio")).text = f"{fake.street_address()} CP {fake.postcode()} Col. {colonia}, {fake.city()}"
    ET.SubElement(personas_solicitud, nstag("Complementarios")).text = f"Y{fake.random_number(digits=2)} W {fake.random_number(digits=5)} {fake.random_number(digits=2)}"

    # --- Return the XML tree ---
    return ET.ElementTree(root)

def generate_documents(count: int, output_dir: str, ollama_api_url: str, llm_model: str):
    """
    Generates a batch of XML documents.
    """
    os.makedirs(output_dir, exist_ok=True)
    print(f"Generating {count} document(s) in '{output_dir}'...")

    for i in tqdm(range(count)):
        try:
            xml_tree = create_xml_expediente(ollama_api_url, llm_model)
            output_filename = os.path.join(output_dir, f"expediente_{i+1:03d}.xml")
            xml_tree.write(output_filename, encoding='utf-8', xml_declaration=True)
        except Exception as e:
            print(f"\nError generating document {i+1}: {e}")
            continue

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Autonomous Dummy Document Generator")
    parser.add_argument("--count", type=int, default=1, help="Number of documents to generate.")
    parser.add_argument("--output", type=str, default="generated_documents", help="Directory to save the generated documents.")
    parser.add_argument("--model", type=str, default="llama3", help="Name of the Ollama model to use.")
    args = parser.parse_args()

    print("--- Starting Autonomous Dummy Document Generation ---")
    
    try:
        # 1. Ensure Ollama is running and the model is ready
        api_url = ensure_ollama_ready(model=args.model)
        
        # 2. Generate the documents
        generate_documents(
            count=args.count,
            output_dir=args.output,
            ollama_api_url=api_url,
            llm_model=args.model
        )
        
        print(f"\nSUCCESS: Batch generation complete. Files are in '{args.output}'.")

    except Exception as e:
        print(f"\nFATAL ERROR: The generation pipeline failed. Reason: {e}", file=sys.stderr)

    print("\n--- Pipeline Finished ---")