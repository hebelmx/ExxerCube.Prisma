# tests/test_document_generator.py

import pytest
import os
import sys
import shutil
from xml.etree import ElementTree as ET
from unittest.mock import patch

# Add project root to the Python path
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from document_generator import generate_document_data, create_xml_expediente, create_docx_document, create_pdf_document, generate_documents

# Define the output directory for test artifacts
TEST_OUTPUT_DIR = "test_output/batch"

class TestDocumentGenerator:
    """
    Test suite for the document_generator module.
    """
    def setup_method(self):
        """Create a clean directory for each test."""
        if os.path.exists(TEST_OUTPUT_DIR):
            shutil.rmtree(TEST_OUTPUT_DIR)
        os.makedirs(TEST_OUTPUT_DIR, exist_ok=True)

    def teardown_method(self):
        """Clean up the test directory after each test."""
        if os.path.exists(TEST_OUTPUT_DIR):
            shutil.rmtree(TEST_OUTPUT_DIR)

    @patch('document_generator.get_llm_narrative', return_value="Mocked narrative")
    def test_data_generation_and_xml_creation(self, mock_llm):
        """
        Tests that data is generated and a single XML file is created from it.
        """
        # 1. Generate data
        data = generate_document_data("http://mock.url", "mock_model")
        assert isinstance(data, dict)
        assert data["InstruccionesCuentasPorConocer"] == "Mocked narrative"

        # 2. Create XML from data
        xml_tree = create_xml_expediente(data)
        assert xml_tree is not None
        
        output_filename = os.path.join(TEST_OUTPUT_DIR, "single_expediente.xml")
        xml_tree.write(output_filename, encoding='utf-8', xml_declaration=True)
        
        assert os.path.exists(output_filename)

        # 3. Validate XML
        try:
            parser = ET.XMLParser(encoding="utf-8")
            parsed_tree = ET.parse(output_filename, parser=parser)
            root = parsed_tree.getroot()
        except ET.ParseError as e:
            pytest.fail(f"Generated XML is not well-formed: {e}")

        assert root.tag == "{http://www.cnbv.gob.mx}Expediente"
    
    @patch('document_generator.generate_document_data')
    def test_batch_generation_creates_full_package(self, mock_generate_data):
        """
        Tests that batch generation creates a complete package (.xml, .docx, .pdf) for each case.
        """
        # 1. Configure mock to return some dummy data
        mock_generate_data.return_value = {
            "Cnbv_NumeroOficio": "Test-Oficio-001",
            "Cnbv_FechaPublicacion": "2025-01-01",
            "AutoridadNombre": "Test Authority",
            "Nombre": "Test Name",
            "NombreCompleto": "Test Name, S.A. DE C.V.",
            "Rfc": "XXX010101XXX",
            "Domicilio": "Test Address 123",
            "InstruccionesCuentasPorConocer": "Test instructions.",
            # Add other keys required by the functions to avoid KeyErrors
            "Cnbv_NumeroExpediente": "1", "Cnbv_SolicitudSiara": "1", "Cnbv_Folio": "1",
            "Cnbv_OficioYear": "2025", "Cnbv_AreaClave": "1", "Cnbv_AreaDescripcion": "1",
            "Cnbv_DiasPlazo": "1", "Referencia2": "1", "TieneAseguramiento": "false",
            "Complementarios": "1"
        }
        
        # 2. Define batch size and run the generator
        num_files = 3
        generate_documents(
            count=num_files, 
            output_dir=TEST_OUTPUT_DIR,
            ollama_api_url="http://mock.url",
            llm_model="mock_model"
        )

        # 3. Assert that the correct number of files of each type were created
        generated_files = os.listdir(TEST_OUTPUT_DIR)
        assert len(generated_files) == num_files * 3

        # 4. Assert that all files for each case exist
        for i in range(num_files):
            base_filename = f"expediente_{i+1:03d}"
            assert f"{base_filename}.xml" in generated_files
            assert f"{base_filename}.docx" in generated_files
            assert f"{base_filename}.pdf" in generated_files
        
        print(f"\nTest passed: Batch generation created {num_files} complete document packages.")
