#!/usr/bin/env python3
"""
Quick test corpus generator using template-based generation.
Creates 10 test documents similar to DumyPrisma1.png format.
"""

import json
import random
import hashlib
from datetime import datetime, timedelta

def load_entities():
    with open('entities.json', 'r', encoding='utf-8') as f:
        return json.load(f)

def generate_date():
    days_ago = random.randint(1, 365)
    date = datetime.now() - timedelta(days=days_ago)
    return date.strftime("%Y-%m-%d")

def generate_test_document(entities, index):
    """Generate a document similar to DumyPrisma1.png format."""
    
    # Select random entities
    autoridad = random.choice(entities["autoridades"])
    expediente = random.choice(entities["numeros_expediente"])
    tipo_req = random.choice(entities["tipos_requerimiento"])
    fundamento = random.choice(entities["fundamentos_legales"])
    motivacion = random.choice(entities["motivaciones"])
    banco = random.choice(entities["entidades_financieras"])
    
    # Select parties
    personas = random.sample(entities["nombres_personas"], k=2)
    actor = personas[0]
    demandado = personas[1]
    
    # Generate amount if applicable
    monto = ""
    if "embargo" in tipo_req.lower() or "aseguramiento" in tipo_req.lower():
        cantidad = random.choice(entities["montos_comunes"])
        moneda = random.choice(entities["monedas"])
        monto = f"HASTA POR LA CANTIDAD DE ${cantidad:,.2f} {moneda}"
    
    # Build document text
    text = f"""PODER JUDICIAL DE LA FEDERACIÓN
{autoridad.upper()}

EXPEDIENTE: {expediente}
OFICIO: {random.randint(1000,9999)}/{datetime.now().year}
FECHA: {generate_date()}

{banco}
PRESENTE

CAUSA QUE MOTIVA EL REQUERIMIENTO

Con fundamento en lo dispuesto por el {fundamento}, dentro del expediente 
número {expediente}, relativo al {motivacion}, promovido por {actor} 
en contra de {demandado}, y en cumplimiento al acuerdo dictado con fecha 
{generate_date()}, SE ORDENA:

ACCIÓN SOLICITADA

Se requiere a la institución bancaria {banco} para que dentro del término 
de TRES DÍAS HÁBILES contados a partir de la recepción del presente, 
proceda a realizar {tipo_req.upper()} sobre las cuentas que el demandado 
{demandado} tenga o llegare a tener en esa institución, ya sean cuentas 
de ahorro, cheques, inversión o cualquier otro instrumento financiero.

{monto}

La institución deberá informar por escrito a este juzgado sobre el 
cumplimiento de lo ordenado, detallando los números de cuenta afectados, 
saldos existentes y las acciones realizadas.

APERCIBIMIENTO

Se apercibe a la institución bancaria que en caso de no dar cumplimiento 
a lo ordenado en el presente oficio, se le impondrá una multa equivalente 
a CIEN UNIDADES DE MEDIDA Y ACTUALIZACIÓN, sin perjuicio de las demás 
sanciones que resulten aplicables conforme a la ley.

Así lo acordó y firma el C. Juez {random.choice(['Primero', 'Segundo', 'Tercero'])} 
de lo {random.choice(['Civil', 'Mercantil'])} del {random.choice(['Primer', 'Segundo'])} 
Distrito Judicial en el Estado, quien actúa con Secretario de Acuerdos que 
autoriza y da fe.

ATENTAMENTE
{autoridad}

LIC. {random.choice(entities['nombres_personas']).upper()}
JUEZ

LIC. {random.choice(entities['nombres_personas']).upper()}
SECRETARIO DE ACUERDOS"""
    
    # Calculate hash
    hash_value = hashlib.sha256(text.encode('utf-8')).hexdigest()
    
    return {
        "id": f"TEST{index+1:03d}",
        "text": text,
        "hash": hash_value,
        "metadata": {
            "fecha": generate_date(),
            "autoridad": autoridad,
            "expediente": expediente,
            "tipo_requerimiento": tipo_req,
            "partes": [actor, demandado]
        }
    }

def main():
    print("Generating 999 test documents...")
    
    entities = load_entities()
    corpus = []
    
    num_docs = 999
    for i in range(num_docs):
        doc = generate_test_document(entities, i)
        corpus.append(doc)
        if (i + 1) % 100 == 0:  # Progress every 100 docs
            print(f"Generated document {i+1}/{num_docs}")
    
    # Save JSON
    with open('test_corpus.json', 'w', encoding='utf-8') as f:
        json.dump(corpus, f, ensure_ascii=False, indent=2)
    
    # Save Markdown
    with open('test_corpus.md', 'w', encoding='utf-8') as f:
        f.write("# Test Corpus - 10 Documents\n\n")
        for doc in corpus:
            f.write(f"<--Start Requirement {doc['id']}-->\n")
            f.write("**Requerimiento**\n")
            f.write(doc['text'])
            f.write("\n\n**Hash**\n")
            f.write(doc['hash'])
            f.write("\n<--End Requirement-->\n\n")
    
    print(f"Saved {num_docs} documents to test_corpus.json and test_corpus.md")

if __name__ == "__main__":
    main()