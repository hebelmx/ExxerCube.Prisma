import xml.etree.ElementTree as ET
import markdown
import os
from datetime import datetime
import base64
import hashlib

try:
    from weasyprint import HTML
    WEASYPRINT_AVAILABLE = True
except:
    WEASYPRINT_AVAILABLE = False

try:
    from xhtml2pdf import pisa
    XHTML2PDF_AVAILABLE = True
except:
    XHTML2PDF_AVAILABLE = False

try:
    import pdfkit
    PDFKIT_AVAILABLE = True
except:
    PDFKIT_AVAILABLE = False

# ---------------------------------------------------------
# 1. Load XML and generate timestamped output filenames
# ---------------------------------------------------------
XML_FILE = "222AAA-44444444442025.xml"

# Extract base name from XML file (without extension)
xml_basename = os.path.splitext(XML_FILE)[0]

# Generate timestamp
timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")

# Create output folder with timestamp
output_folder = f"output_{xml_basename}_{timestamp}"
os.makedirs(output_folder, exist_ok=True)

# Create output filenames with XML basename and timestamp
OUTPUT_MD = os.path.join(output_folder, f"{xml_basename}.md")
OUTPUT_PDF = os.path.join(output_folder, f"{xml_basename}.pdf")
OUTPUT_HTML = os.path.join(output_folder, f"{xml_basename}.html")

tree = ET.parse(XML_FILE)
root = tree.getroot()

# Define namespace
ns = {'cnbv': 'http://www.cnbv.gob.mx'}

# ---------------------------------------------------------
# 2. Helper: Safe XML text extraction
# ---------------------------------------------------------
def get(tag):
    """Return the text of an XML tag or empty string."""
    el = root.find(tag, ns)
    return el.text.strip() if el is not None and el.text else ""


# ---------------------------------------------------------
# 3. Extract XML fields
# ---------------------------------------------------------

data = {
    # Identification fields
    "Cnbv_SolicitudSiara": get(".//cnbv:Cnbv_SolicitudSiara"),
    "Cnbv_NumeroOficio": get(".//cnbv:Cnbv_NumeroOficio"),
    "Cnbv_OficioYear": get(".//cnbv:Cnbv_OficioYear"),
    "Cnbv_AreaDescripcion": get(".//cnbv:Cnbv_AreaDescripcion"),
    "Cnbv_Folio": get(".//cnbv:Cnbv_Folio"),

    # Additional CNBV fields
    "Cnbv_NumeroExpediente": get(".//cnbv:Cnbv_NumeroExpediente"),
    "Cnbv_FechaPublicacion": get(".//cnbv:Cnbv_FechaPublicacion"),
    "Cnbv_DiasPlazo": get(".//cnbv:Cnbv_DiasPlazo"),

    # Authority
    "AutoridadNombre": get(".//cnbv:AutoridadNombre"),
    "NombreSolicitante": get(".//cnbv:NombreSolicitante"),

    # References
    "Referencia": get(".//cnbv:Referencia"),
    "Referencia1": get(".//cnbv:Referencia1"),
    "Referencia2": get(".//cnbv:Referencia2"),

    # Destinatario - Example data (replace with actual XML extraction if available)
    "Destinatario_Nombre": "Juan Juan Melón Sandía",
    "Destinatario_Cargo": "Vicepresidente de Supervisión de Procesos Preventivos",
    "Destinatario_Institucion": "Comisión Nacional Bancaria y de valores",
    "Destinatario_Direccion": "Insurgentes Sur 1971, Conjunto Plaza Inn, col. Guadalupe Inn,\nDel Alvaro obregón, C.P. 01020, Ciudad de México",

    # Solicitante details
    "UnidadSolicitante": "ADMINSTRAIÓN DESCONCENTRADA DE AUTORÍA FISCAL DE SONORA \"2\"",
    "DomicilioSolicitante": "AVE. RODOLFO ELEIAS CALLES PTE, No 1111, Col centro,\nMunicipio Centro CP 00001",
    "ServidorPublico_Nombre": "MTRO. GUADALUPE PEPITA PEPITA",
    "ServidorPublico_Cargo": "ADMINISTRADOR DESCONCENTRADA DE AUTORÍA FISCAL DE SONORA \"2\"",
    "ServidorPublico_Telefono": "(55) 1111-2222",
    "ServidorPublico_Correo": "lupita.pepita.pepita@sat.gob.mx",

    # Legal texts - Sample (replace with actual text from requirements)
    "FacultadesTexto": """esta oficina para cobros 8 san Ángel órgano integrante de la subdelegación 8 san Ángel del instituto mexicano de seguridad social órgano operativo de la delegación sur del distrito federal organismo fiscal autónomo con fundamento en los artículos 251 fracciónVII XXV y XXXXVII 251 a de la ley del seguro social en vigor uno y 2 fracción V incisos VYC 142 fracción III 149, 150, 154 fracciones - i, ii iii, y 155 primer párrafo secciónXXXVI párrafos primero y segundo inciso c párrafos primero y segundo 159 segundo párrafo del reglamento interior de institución mexicana del seguro social en vigor""",

    "FundamentoTexto": """artículo 16 de la constitución política de los Estados Unidos Mexicanos 3 fracción uno y 45 de la ley orgánica de la administración pública federal 5, 9, 250 fracciones VII , XX y XXVI 251-a 270, 287 y 291 de la ley del seguro social en vigor 145, 151, 152, 153, 154, fracción y 160 del código fiscal de la federación vigente 142 de las instituciones de crédito vigente y 192 de la ley del mercado de valores.""",

    "MotivacionTexto": """Mediante diligencia de fecha 21/03/2025 se practicó dentro del procedimiento administrativo de ejecución embargo sobre los depósitos bancarios en cuentas a nombre del patrón referido que se localizaron en algunas de las instituciones de crédito y entidades financieras e integrantes del sistema financiero mexicano para la recuperación de los créditos fiscales a favor del instituto mexicano del seguro social.""",

    "MontoTexto": """El embargo practicado sobre los depósitos bancarios es hasta por la cantidad de 1,549,481.25 8UN MILLÓN QUINIENTOS CUARENTA Y NUEVE MIL CUATROCIENTOS OCHENTA Y UN PESOS 25/100 M.N.) (UN MILLÓN ) más los accesorios legales que se sigan generando hasta la fecha de pago del crédito fiscal adeudados por el patrón de referencia.""",

    # Motivación details
    "FechaDiligencia": "21/03/2025",
    "MontoEmbargado": "$1,549,481.25",
    "MontoEnLetra": "UN MILLÓN QUINIENTOS CUARENTA Y NUEVE MIL CUATROCIENTOS OCHENTA Y UN PESOS 25/100 M.N.",

    # Origen del requerimiento
    "TieneAseguramiento": "Sí" if get(".//cnbv:TieneAseguramiento") == "true" else "No",
    "NoOficioRevision": "N/A" if not get(".//cnbv:Referencia2") else get(".//cnbv:Referencia2"),
    "MontoCredito": "$1,549,481.25",
    "CreditosFiscales": "12345678 910111213 14151617 181920212 223242526",
    "Periodos": "05/2023 06/2023 10/2023 10/2023 11/2023 11/2023",

    # Partes (first item)
    "SolicitudPartes_Nombre": get(".//cnbv:SolicitudPartes/cnbv:Nombre"),
    "SolicitudPartes_Caracter": get(".//cnbv:SolicitudPartes/cnbv:Caracter"),

    # Solicitud Especifica
    "InstruccionesCuentasPorConocer": get(".//cnbv:InstruccionesCuentasPorConocer"),

    # Sectores Bancarios
    "SectoresBancarios": """Sector Casas de Bolsa
Sector Instituciones de Banca de Desarrollo
Sector Instituciones de Banca Múltiple.""",

    # Persona inside SolicitudEspecifica
    "Persona_Nombre": get(".//cnbv:PersonasSolicitud/cnbv:Nombre"),
    "Persona_Rfc": get(".//cnbv:PersonasSolicitud/cnbv:Rfc"),
    "Persona_Caracter": get(".//cnbv:PersonasSolicitud/cnbv:Caracter"),
    "Persona_Domicilio": get(".//cnbv:PersonasSolicitud/cnbv:Domicilio"),
    "Persona_Complementarios": get(".//cnbv:PersonasSolicitud/cnbv:Complementarios"),
}

# ---------------------------------------------------------
# 4. Generate watermark text from ID and hash
# ---------------------------------------------------------
def generate_watermark(folio):
    """Generate a scrambled watermark from the folio number."""
    # Create a hash from the folio
    hash_obj = hashlib.sha256(folio.encode())
    hash_hex = hash_obj.hexdigest()[:16].upper()

    # Mix the original folio with parts of the hash
    parts = folio.split('/')
    if len(parts) >= 3:
        watermark = f"{parts[0][:4]}-{hash_hex[:8]}-{parts[2]}"
    else:
        watermark = f"{folio}-{hash_hex[:8]}"

    return watermark

watermark_text = generate_watermark(data.get("Cnbv_SolicitudSiara", "DOCUMENTO"))

# ---------------------------------------------------------
# 5. Encode logo image as base64 for embedding in HTML
# ---------------------------------------------------------
logo_path = "LogoMexico.jpg"
logo_base64 = ""
if os.path.exists(logo_path):
    with open(logo_path, "rb") as img_file:
        logo_base64 = base64.b64encode(img_file.read()).decode('utf-8')

# ---------------------------------------------------------
# 6. Load Markdown template
# ---------------------------------------------------------

with open("Template.md", "r", encoding="utf-8") as f:
    template = f.read()


# ---------------------------------------------------------
# 7. Replace placeholders {{FIELD}} with XML data
# ---------------------------------------------------------

markdown_output = template
for key, value in data.items():
    markdown_output = markdown_output.replace("{{" + key + "}}", value)


# ---------------------------------------------------------
# 8. Save the generated Markdown file
# ---------------------------------------------------------

with open(OUTPUT_MD, "w", encoding="utf-8") as f:
    f.write(markdown_output)

print(f"✓ Markdown generado: {OUTPUT_MD}")


# ---------------------------------------------------------
# 9. Convert Markdown to PDF with enhanced styling
# ---------------------------------------------------------

# Convert markdown to HTML
html_content = markdown.markdown(markdown_output, extensions=['tables', 'fenced_code', 'nl2br'])

# Add header with logo repeated 5 times (as in reference)
header_logo_html = ""
if logo_base64:
    for i in range(5):
        header_logo_html += f'<img src="data:image/jpeg;base64,{logo_base64}" class="header-logo"/>'

# Add CSS styling to match reference document
styled_html = f"""
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <style>
        @page {{
            margin: 40px;
            @top-center {{
                content: element(header);
            }}
        }}

        body {{
            font-family: Arial, Helvetica, sans-serif;
            font-size: 11pt;
            line-height: 1.4;
            color: #000;
            margin: 0;
            padding: 20px;
        }}

        .header {{
            position: running(header);
            text-align: center;
            margin-bottom: 10px;
        }}

        .header-logo {{
            width: 60px;
            height: auto;
            margin: 0 5px;
            display: inline-block;
        }}

        h1 {{
            font-size: 11pt;
            font-weight: normal;
            margin: 5px 0;
            text-align: left;
        }}

        .id-box {{
            border: 2px solid #000;
            padding: 8px 15px;
            float: right;
            margin: 10px 0 20px 20px;
            font-size: 10pt;
            text-align: center;
        }}

        .section-header {{
            border: 1px solid #000;
            padding: 5px 10px;
            background-color: #f5f5f5;
            font-weight: bold;
            margin: 15px 0 10px 0;
            text-align: center;
        }}

        .subsection-header {{
            border: 1px solid #000;
            padding: 5px 10px;
            background-color: #f9f9f9;
            margin: 10px 0;
            text-align: center;
        }}

        .two-column-table {{
            width: 100%;
            border: 1px solid #000;
            border-collapse: collapse;
            margin: 15px 0;
        }}

        .two-column-table td {{
            border: 1px solid #000;
            padding: 15px;
            vertical-align: top;
            width: 50%;
        }}

        table {{
            width: 100%;
            border-collapse: collapse;
            margin: 15px 0;
            font-size: 10pt;
        }}

        table, th, td {{
            border: 1px solid #000;
        }}

        th, td {{
            padding: 8px;
            text-align: left;
        }}

        th {{
            background-color: #f2f2f2;
            font-weight: bold;
        }}

        hr {{
            border: none;
            border-top: 1px solid #ccc;
            margin: 20px 0;
        }}

        p {{
            margin: 10px 0;
            text-align: justify;
        }}

        strong {{
            font-weight: bold;
        }}

        .signature {{
            text-align: center;
            margin-top: 80px;
            page-break-inside: avoid;
        }}

        /* Watermark - Diagonal text across page in red */
        .watermark {{
            position: fixed;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%) rotate(-45deg);
            font-size: 80pt;
            font-weight: bold;
            color: rgba(255, 0, 0, 0.12);
            z-index: 9999;
            white-space: nowrap;
            pointer-events: none;
            font-family: 'Courier New', monospace;
            letter-spacing: 8px;
            text-shadow: 0 0 2px rgba(255, 0, 0, 0.1);
        }}

        /* Additional watermark on each page */
        @media print {{
            .watermark {{
                position: fixed;
                color: rgba(255, 0, 0, 0.12);
            }}
        }}
    </style>
</head>
<body>
    <div class="watermark">{watermark_text}</div>
    <div class="header">
        {header_logo_html}
    </div>
    {html_content}
</body>
</html>
"""

# Save HTML version
with open(OUTPUT_HTML, "w", encoding="utf-8") as f:
    f.write(styled_html)
print(f"✓ HTML generado: {OUTPUT_HTML}")

# ---------------------------------------------------------
# 10. Convert HTML to PDF using Chrome/Chromium headless
# ---------------------------------------------------------

print("\n🔄 Generando PDF desde HTML...")
print(f"   HTML: {OUTPUT_HTML}")
print(f"   PDF:  {OUTPUT_PDF}")

# Use Chrome/Edge in headless mode for PDF generation (best rendering)
import subprocess
import shutil

# Try to find Chrome or Edge executable
chrome_paths = [
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
]

chrome_exe = None
for path in chrome_paths:
    if os.path.exists(path):
        chrome_exe = path
        break

if chrome_exe:
    try:
        # Get absolute paths
        html_path = os.path.abspath(OUTPUT_HTML)
        pdf_path = os.path.abspath(OUTPUT_PDF)

        # Chrome/Edge headless command for PDF generation
        cmd = [
            chrome_exe,
            "--headless",
            "--disable-gpu",
            "--no-pdf-header-footer",  # Remove browser headers/footers
            "--print-to-pdf=" + pdf_path,
            html_path
        ]

        result = subprocess.run(cmd, capture_output=True, text=True, timeout=30)

        if os.path.exists(pdf_path) and os.path.getsize(pdf_path) > 0:
            print(f"✓ PDF generado exitosamente: {OUTPUT_PDF}")
            print(f"  Tamaño: {os.path.getsize(pdf_path):,} bytes")
        else:
            print("⚠ El PDF no se generó correctamente")
            if result.stderr:
                print(f"  Error: {result.stderr}")
    except subprocess.TimeoutExpired:
        print("⚠ Timeout al generar PDF (>30 segundos)")
    except Exception as e:
        print(f"⚠ Error al generar PDF: {e}")
else:
    print("⚠ No se encontró Chrome o Edge instalado")
    print("\n💡 Opciones para generar el PDF:")
    print(f"  1. Instalar Google Chrome o Microsoft Edge")
    print(f"  2. Abrir manualmente: {OUTPUT_HTML}")
    print(f"     Presionar Ctrl+P -> 'Más configuración' -> Desmarcar 'Encabezados y pies de página'")
    print(f"     -> Guardar como PDF")
