"""
Tests for file operations modules.
"""
import pytest
import tempfile
import os
from pathlib import Path
from unittest.mock import patch, MagicMock
import numpy as np

from ..file_loader import (
    is_supported_file, list_supported_files, load_image_from_file, load_images_from_path
)
from ..output_writer import (
    prepare_json_data, format_text_output, generate_output_paths,
    write_text_file, write_json_file, ensure_directory_exists
)
from ..models import ExtractedFields, AmountData, OutputData


class TestFileLoader:
    def test_is_supported_file(self):
        """Test checking if file is supported."""
        assert is_supported_file("image.jpg") is True
        assert is_supported_file("document.pdf") is True
        assert is_supported_file("photo.PNG") is True  # Case insensitive
        assert is_supported_file("document.txt") is False
        assert is_supported_file("video.mp4") is False

    def test_list_supported_files_single_file(self):
        """Test listing supported files for single file."""
        with tempfile.NamedTemporaryFile(suffix=".jpg", delete=False) as tmp:
            tmp_path = tmp.name
        
        try:
            files = list_supported_files(tmp_path)
            assert len(files) == 1
            assert tmp_path in files[0]
        finally:
            os.unlink(tmp_path)

    def test_list_supported_files_directory(self):
        """Test listing supported files in directory."""
        with tempfile.TemporaryDirectory() as tmp_dir:
            # Create test files
            jpg_file = Path(tmp_dir) / "test.jpg"
            png_file = Path(tmp_dir) / "test.png"
            txt_file = Path(tmp_dir) / "test.txt"  # Should be ignored
            
            jpg_file.touch()
            png_file.touch()
            txt_file.touch()
            
            files = list_supported_files(tmp_dir)
            
            assert len(files) == 2  # Only jpg and png
            file_names = [Path(f).name for f in files]
            assert "test.jpg" in file_names
            assert "test.png" in file_names
            assert "test.txt" not in file_names

    def test_list_supported_files_nonexistent(self):
        """Test listing files for nonexistent path."""
        files = list_supported_files("/nonexistent/path")
        assert files == []

    @patch('cv2.imread')
    def test_load_image_from_file_success(self, mock_imread):
        """Test successful image loading."""
        # Mock successful image loading
        mock_img = np.zeros((100, 100, 3), dtype=np.uint8)
        mock_imread.return_value = mock_img
        
        result = load_image_from_file("test.jpg")
        
        assert np.array_equal(result, mock_img)
        mock_imread.assert_called_once_with("test.jpg", 1)  # IMREAD_COLOR = 1

    @patch('cv2.imread')
    def test_load_image_from_file_failure(self, mock_imread):
        """Test failed image loading."""
        mock_imread.return_value = None
        
        with pytest.raises(ValueError, match="Could not read image"):
            load_image_from_file("invalid.jpg")

    @patch('ocr_modules.file_loader.load_image_from_file')
    def test_load_images_from_path_image(self, mock_load_image):
        """Test loading images from single image file."""
        mock_img = np.zeros((100, 100, 3), dtype=np.uint8)
        mock_load_image.return_value = mock_img
        
        with tempfile.NamedTemporaryFile(suffix=".jpg", delete=False) as tmp:
            tmp_path = tmp.name
        
        try:
            images = load_images_from_path(tmp_path)
            
            assert len(images) == 1
            assert images[0].source_path == tmp_path
            assert images[0].page_number == 1
            assert images[0].total_pages == 1
            assert np.array_equal(images[0].data, mock_img)
        finally:
            os.unlink(tmp_path)

    def test_load_images_from_path_nonexistent(self):
        """Test loading from nonexistent path."""
        with pytest.raises(ValueError, match="File does not exist"):
            load_images_from_path("/nonexistent/file.jpg")

    def test_load_images_from_path_unsupported(self):
        """Test loading unsupported file type."""
        with tempfile.NamedTemporaryFile(suffix=".txt", delete=False) as tmp:
            tmp_path = tmp.name
        
        try:
            with pytest.raises(ValueError, match="Unsupported file format"):
                load_images_from_path(tmp_path)
        finally:
            os.unlink(tmp_path)


class TestOutputWriter:
    def test_prepare_json_data(self):
        """Test preparing data for JSON serialization."""
        amount = AmountData(value=100.0, currency="USD", original_text="$100")
        fields = ExtractedFields(
            expediente="TEST-123",
            montos=[amount],
            fechas=["2023-10-15"]
        )
        metadata = {"source": "test.jpg"}
        
        json_data = prepare_json_data(fields, metadata)
        
        assert json_data["expediente"] == "TEST-123"
        assert len(json_data["montos"]) == 1
        assert json_data["montos"][0]["value"] == 100.0
        assert json_data["metadata"]["source"] == "test.jpg"

    def test_format_text_output(self):
        """Test formatting text output."""
        amount = AmountData(value=100.0, currency="USD", original_text="$100")
        fields = ExtractedFields(
            expediente="TEST-123",
            montos=[amount],
            fechas=["2023-10-15"]
        )
        text = "Sample OCR text"
        
        formatted = format_text_output(text, fields)
        
        assert "OCR TEXT OUTPUT" in formatted
        assert "EXTRACTED FIELDS SUMMARY" in formatted
        assert "Expediente: TEST-123" in formatted
        assert "Sample OCR text" in formatted

    def test_generate_output_paths_single_page(self):
        """Test generating output paths for single page."""
        paths = generate_output_paths(
            "/output", "/source/document.pdf", 1, 1
        )
        
        assert paths["text"] == "/output/document.txt"
        assert paths["json"] == "/output/document.json"

    def test_generate_output_paths_multiple_pages(self):
        """Test generating output paths for multiple pages."""
        paths = generate_output_paths(
            "/output", "/source/document.pdf", 2, 5
        )
        
        assert paths["text"] == "/output/document_p2.txt"
        assert paths["json"] == "/output/document_p2.json"

    def test_ensure_directory_exists(self):
        """Test ensuring directory exists."""
        with tempfile.TemporaryDirectory() as tmp_dir:
            file_path = Path(tmp_dir) / "subdir" / "file.txt"
            
            # Directory doesn't exist yet
            assert not file_path.parent.exists()
            
            ensure_directory_exists(str(file_path))
            
            # Directory should now exist
            assert file_path.parent.exists()

    def test_write_text_file(self):
        """Test writing text file."""
        with tempfile.TemporaryDirectory() as tmp_dir:
            file_path = Path(tmp_dir) / "test.txt"
            content = "Test content with unicode: áéíóú"
            
            write_text_file(str(file_path), content)
            
            assert file_path.exists()
            with open(file_path, "r", encoding="utf-8") as f:
                read_content = f.read()
            assert read_content == content

    def test_write_json_file(self):
        """Test writing JSON file."""
        with tempfile.TemporaryDirectory() as tmp_dir:
            file_path = Path(tmp_dir) / "test.json"
            data = {
                "expediente": "TEST-123",
                "unicode_text": "áéíóú",
                "number": 42
            }
            
            write_json_file(str(file_path), data)
            
            assert file_path.exists()
            # Read back and verify
            import json
            with open(file_path, "r", encoding="utf-8") as f:
                read_data = json.load(f)
            assert read_data == data