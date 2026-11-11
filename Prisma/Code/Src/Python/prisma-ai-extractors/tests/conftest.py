"""
Pytest configuration and shared fixtures.
"""

import os
import sys
import tempfile
import shutil
from pathlib import Path
from typing import Dict, Any, Generator
import pytest
from unittest.mock import Mock, MagicMock
from PIL import Image
import numpy as np
import json

# Add src to Python path
sys.path.insert(0, str(Path(__file__).parent.parent / 'src'))

from src.modules import (
    ModelLoader, ImageProcessor, TextExtractor, JsonParser,
    DocumentValidator, PerformanceMonitor, ConfigManager, ErrorHandler
)
from src.models import ExtractionResult, ExtractedDocument, Requerimiento


# Test configuration
TEST_DATA_DIR = Path(__file__).parent / "data"
TEST_IMAGES_DIR = TEST_DATA_DIR / "images"
TEST_CONFIGS_DIR = TEST_DATA_DIR / "configs"
TEST_OUTPUTS_DIR = TEST_DATA_DIR / "outputs"

# Ensure test directories exist
for directory in [TEST_DATA_DIR, TEST_IMAGES_DIR, TEST_CONFIGS_DIR, TEST_OUTPUTS_DIR]:
    directory.mkdir(exist_ok=True, parents=True)


@pytest.fixture(scope="session")
def test_config():
    """Test configuration dictionary."""
    return {
        'extractors': {
            'smolvlm': {
                'model_id': 'test-model',
                'max_new_tokens': 128,
                'device': 'cpu',
                'dtype': 'float32'
            },
            'paddle': {
                'use_angle_cls': False,
                'lang': 'es',
                'use_gpu': False
            },
            'doctr': {
                'det_arch': 'db_resnet50',
                'reco_arch': 'crnn_vgg16_bn',
                'pretrained': False
            }
        },
        'processing': {
            'batch_size': 1,
            'timeout': 30,
            'max_image_size': 1024
        },
        'validation': {
            'required_fields': ['fecha', 'autoridadEmisora'],
            'strict_mode': False
        },
        'testing': {
            'mock_models': True,
            'use_cpu_only': True,
            'disable_gpu': True
        }
    }


@pytest.fixture
def temp_dir():
    """Create a temporary directory for test files."""
    temp_path = tempfile.mkdtemp()
    yield Path(temp_path)
    shutil.rmtree(temp_path, ignore_errors=True)


@pytest.fixture
def sample_image():
    """Create a sample PIL image for testing."""
    # Create a simple test image with text-like patterns
    image = Image.new('RGB', (800, 600), color='white')
    return image


@pytest.fixture
def sample_document_image():
    """Create a sample document-like image."""
    # Create a more realistic document image
    image = Image.new('RGB', (1200, 900), color='white')
    
    # Add some text-like patterns (simple rectangles)
    pixels = np.array(image)
    
    # Add header area
    pixels[50:100, 100:1100] = [240, 240, 240]  # Light gray
    
    # Add text lines
    for i, y in enumerate(range(150, 700, 60)):
        width = 800 if i % 2 == 0 else 600
        pixels[y:y+20, 100:100+width] = [50, 50, 50]  # Dark text
    
    return Image.fromarray(pixels.astype('uint8'))


@pytest.fixture
def sample_legal_document():
    """Sample legal document data."""
    return {
        "fecha": "2024-01-15",
        "autoridadEmisora": "CONDUSEF",
        "expediente": "EXP-2024-001",
        "tipoRequerimiento": "EMBARGO",
        "subtipoRequerimiento": "CUENTA_BANCARIA",
        "fundamentoLegal": "Artículo 142 Ley de Instituciones de Crédito",
        "motivacion": "Incumplimiento de obligaciones crediticias",
        "partes": ["Juan Pérez García", "Banco Nacional S.A."],
        "detalle": {
            "descripcion": "Embargo total de cuenta corriente",
            "monto": 125000.00,
            "moneda": "MXN",
            "activoVirtual": "N/A"
        }
    }


@pytest.fixture
def sample_json_responses():
    """Sample JSON responses for different extraction scenarios."""
    return {
        'valid': '{"fecha": "2024-01-15", "autoridadEmisora": "CONDUSEF", "expediente": "EXP-001"}',
        'malformed': '{"fecha": "2024-01-15", "autoridadEmisora": "CONDUSEF", "expediente": "EXP-001"',  # Missing closing brace
        'with_markdown': '```json\n{"fecha": "2024-01-15", "autoridadEmisora": "CONDUSEF"}\n```',
        'with_extra_text': 'Based on the document, here is the extracted data: {"fecha": "2024-01-15", "autoridadEmisora": "CONDUSEF"} as requested.',
        'empty': '',
        'invalid': 'This is not JSON at all',
        'complex': '''
        {
            "fecha": "2024-01-15",
            "autoridadEmisora": "COMISIÓN NACIONAL BANCARIA Y DE VALORES",
            "expediente": "CNBV/EXP/2024/001",
            "tipoRequerimiento": "REQUERIMIENTO DE INFORMACIÓN",
            "partes": ["Banco XYZ S.A.", "Juan Pérez"],
            "detalle": {
                "descripcion": "Información sobre movimientos bancarios",
                "monto": null,
                "moneda": "MXN"
            }
        }'''
    }


# Module fixtures
@pytest.fixture
def mock_model_loader():
    """Mock ModelLoader for testing."""
    loader = Mock(spec=ModelLoader)
    loader.load_model.return_value = Mock()
    loader.load_processor.return_value = Mock()
    loader.clear_cache.return_value = None
    loader.get_cached_models.return_value = {'models': [], 'processors': [], 'cache_size': 0}
    return loader


@pytest.fixture
def image_processor():
    """Real ImageProcessor instance for testing."""
    return ImageProcessor()


@pytest.fixture
def mock_text_extractor():
    """Mock TextExtractor for testing."""
    extractor = Mock(spec=TextExtractor)
    extractor.extract_with_vision_model.return_value = "Sample extracted text"
    extractor.extract_with_ocr_model.return_value = "OCR extracted text"
    return extractor


@pytest.fixture
def json_parser():
    """Real JsonParser instance for testing."""
    return JsonParser()


@pytest.fixture
def document_validator():
    """Real DocumentValidator instance for testing."""
    return DocumentValidator()


@pytest.fixture
def performance_monitor():
    """Real PerformanceMonitor instance for testing."""
    return PerformanceMonitor()


@pytest.fixture
def config_manager(test_config):
    """ConfigManager with test configuration."""
    manager = ConfigManager()
    for key, value in test_config.items():
        if isinstance(value, dict):
            for subkey, subvalue in value.items():
                manager.set(f"{key}.{subkey}", subvalue)
        else:
            manager.set(key, value)
    return manager


@pytest.fixture
def error_handler():
    """Real ErrorHandler instance for testing."""
    return ErrorHandler()


# Mock extractor fixtures
@pytest.fixture
def mock_smolvlm_components():
    """Mock components for SmolVLM testing."""
    model = Mock()
    model.generate.return_value = Mock()
    
    processor = Mock()
    processor.apply_chat_template.return_value = {'input_ids': Mock(), 'attention_mask': Mock()}
    processor.batch_decode.return_value = ['{"fecha": "2024-01-15", "autoridadEmisora": "CONDUSEF"}']
    
    return {
        'model': model,
        'processor': processor
    }


@pytest.fixture
def mock_paddle_components():
    """Mock components for PaddleOCR testing."""
    ocr_model = Mock()
    ocr_model.ocr.return_value = [
        [
            [[10, 10], [100, 10], [100, 30], [10, 30]], 
            ['FECHA: 2024-01-15', 0.95]
        ],
        [
            [[10, 50], [200, 50], [200, 70], [10, 70]], 
            ['AUTORIDAD: CONDUSEF', 0.92]
        ]
    ]
    
    return {'ocr_model': ocr_model}


@pytest.fixture
def mock_doctr_components():
    """Mock components for DocTR testing."""
    ocr_model = Mock()
    ocr_result = Mock()
    ocr_result.render.return_value = "FECHA: 2024-01-15\nAUTORIDAD: CONDUSEF"
    ocr_model.predict.return_value = ocr_result
    
    return {'ocr_model': ocr_model}


# Test data generators
@pytest.fixture
def create_test_images():
    """Factory for creating test images with different characteristics."""
    
    def _create_image(
        width: int = 800,
        height: int = 600,
        color: str = 'white',
        add_noise: bool = False,
        add_text_patterns: bool = False
    ) -> Image.Image:
        image = Image.new('RGB', (width, height), color=color)
        
        if add_text_patterns:
            pixels = np.array(image)
            # Add horizontal lines to simulate text
            for y in range(50, height-50, 30):
                pixels[y:y+15, 50:width-50] = [30, 30, 30]
        
        if add_noise:
            pixels = np.array(image)
            noise = np.random.randint(0, 50, pixels.shape)
            pixels = np.clip(pixels.astype(int) + noise - 25, 0, 255)
            image = Image.fromarray(pixels.astype('uint8'))
        
        return image
    
    return _create_image


@pytest.fixture
def create_test_documents():
    """Factory for creating test document data."""
    
    def _create_document(**kwargs) -> Dict[str, Any]:
        base_doc = {
            "fecha": "2024-01-15",
            "autoridadEmisora": "CONDUSEF",
            "expediente": "EXP-2024-001",
            "tipoRequerimiento": "EMBARGO",
            "partes": ["Juan Pérez"],
            "detalle": {
                "descripcion": "Test requirement",
                "monto": 10000.0,
                "moneda": "MXN"
            }
        }
        base_doc.update(kwargs)
        return base_doc
    
    return _create_document


# Test file management
@pytest.fixture
def save_test_image():
    """Helper to save images for testing."""
    saved_files = []
    
    def _save_image(image: Image.Image, filename: str = None) -> Path:
        if filename is None:
            filename = f"test_image_{len(saved_files)}.png"
        
        filepath = TEST_IMAGES_DIR / filename
        image.save(filepath)
        saved_files.append(filepath)
        return filepath
    
    yield _save_image
    
    # Cleanup
    for filepath in saved_files:
        try:
            filepath.unlink()
        except FileNotFoundError:
            pass


@pytest.fixture(autouse=True)
def cleanup_test_outputs():
    """Automatically cleanup test outputs after each test."""
    yield
    
    # Clean up any files created during testing
    for file_pattern in ["test_*.json", "test_*.png", "test_*.txt"]:
        for file_path in TEST_OUTPUTS_DIR.glob(file_pattern):
            try:
                file_path.unlink()
            except FileNotFoundError:
                pass


# Performance testing fixtures
@pytest.fixture
def benchmark_config():
    """Configuration for performance benchmarks."""
    return {
        'min_rounds': 3,
        'max_time': 10.0,  # seconds
        'warmup_rounds': 1,
        'measure_memory': True,
        'measure_gpu_memory': False  # Disabled for CPU testing
    }


# Error simulation fixtures
@pytest.fixture
def error_scenarios():
    """Common error scenarios for testing."""
    return {
        'file_not_found': FileNotFoundError("Test file not found"),
        'permission_error': PermissionError("Permission denied"),
        'value_error': ValueError("Invalid value provided"),
        'json_error': json.JSONDecodeError("Invalid JSON", "", 0),
        'runtime_error': RuntimeError("Runtime error occurred"),
        'memory_error': MemoryError("Out of memory"),
        'keyboard_interrupt': KeyboardInterrupt("User interrupted"),
    }


# Skip conditions
def pytest_configure(config):
    """Configure pytest with custom markers and skip conditions."""
    config.addinivalue_line(
        "markers", "gpu_required: mark test as requiring GPU"
    )
    config.addinivalue_line(
        "markers", "slow: mark test as slow running"
    )
    config.addinivalue_line(
        "markers", "integration: mark test as integration test"
    )


def pytest_runtest_setup(item):
    """Setup function that runs before each test."""
    # Skip GPU tests if no GPU available
    if "gpu_required" in [mark.name for mark in item.iter_markers()]:
        try:
            import torch
            if not torch.cuda.is_available():
                pytest.skip("GPU not available")
        except ImportError:
            pytest.skip("PyTorch not available")


# Parameterized fixtures for comprehensive testing
@pytest.fixture(params=['cpu', 'cuda'])
def device_config(request):
    """Test different device configurations."""
    if request.param == 'cuda':
        pytest.importorskip("torch")
        try:
            import torch
            if not torch.cuda.is_available():
                pytest.skip("CUDA not available")
        except ImportError:
            pytest.skip("PyTorch not available")
    
    return {
        'device': request.param,
        'dtype': 'float32' if request.param == 'cpu' else 'float16'
    }


@pytest.fixture(params=[
    {'width': 800, 'height': 600},
    {'width': 1200, 'height': 900}, 
    {'width': 2000, 'height': 1500}
])
def image_dimensions(request):
    """Test different image sizes."""
    return request.param