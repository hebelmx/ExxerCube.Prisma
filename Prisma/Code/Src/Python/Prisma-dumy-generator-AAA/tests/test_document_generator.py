# tests/test_document_generator.py

import pytest
import os
import sys
import shutil
from xml.etree import ElementTree as ET
from unittest.mock import patch

# Add project root to the Python path
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from document_generator import create_xml_expediente, generate_documents

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
    def test_single_xml_file_is_created_and_valid(self, mock_llm):
        """
        Tests that a single XML file is created, is well-formed, and has the correct root element.
        """
        xml_tree = create_xml_expediente("http://mock.url", "mock_model")
        assert xml_tree is not None
        
        output_filename = os.path.join(TEST_OUTPUT_DIR, "single_expediente.xml")
        xml_tree.write(output_filename, encoding='utf-8', xml_declaration=True)
        
        assert os.path.exists(output_filename)

        try:
            parser = ET.XMLParser(encoding="utf-8")
            parsed_tree = ET.parse(output_filename, parser=parser)
            root = parsed_tree.getroot()
        except ET.ParseError as e:
            pytest.fail(f"Generated XML is not well-formed: {e}")

        assert root.tag == "{http://www.cnbv.gob.mx}Expediente"
    
    @patch('document_generator.create_xml_expediente')
    def test_batch_generation_creates_multiple_files(self, mock_create_xml):
        """
        Tests that the batch generation function creates the specified number of files.
        """
        # 1. Configure the mock to return a valid (but simple) XML tree
        mock_tree = ET.ElementTree(ET.Element("Expediente"))
        mock_create_xml.return_value = mock_tree
        
        # 2. Define batch size and run the generator
        num_files = 5
        generate_documents(
            count=num_files, 
            output_dir=TEST_OUTPUT_DIR,
            ollama_api_url="http://mock.url",
            llm_model="mock_model"
        )

        # 3. Assert that the correct number of files were created
        generated_files = os.listdir(TEST_OUTPUT_DIR)
        assert len(generated_files) == num_files

        # 4. Assert that the files have unique names and are XML
        for i in range(num_files):
            expected_filename = f"expediente_{i+1:03d}.xml"
            assert expected_filename in generated_files
        
        print(f"\nTest passed: Batch generation created {num_files} unique XML files.")
