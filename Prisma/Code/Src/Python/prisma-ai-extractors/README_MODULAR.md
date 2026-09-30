# Prisma AI Extractors - Modular Architecture

A **clean, modular** document extraction library following **Single Responsibility Principle (SRP)**. Each component has exactly one reason to change.

## 🏗️ Architecture Overview

### **Before: Monolithic Extractors**
```
❌ SmolVLMExtractor:
   - Model loading
   - Image processing  
   - Text extraction
   - JSON parsing
   - Validation
   - Error handling
   - Performance monitoring
```

### **After: Modular Components**
```
✅ Separated Responsibilities:
   📦 ModelLoader      → Model loading & caching
   🖼️  ImageProcessor   → Image loading & preprocessing  
   📝 TextExtractor    → Text generation from models
   🔧 JsonParser       → JSON parsing & cleaning
   ✅ DocumentValidator → Data validation
   📊 PerformanceMonitor → Performance tracking
   ⚙️  ConfigManager    → Configuration management
   🚨 ErrorHandler     → Error handling & recovery
```

## 🚀 Quick Start

### **Simple Usage**
```python
from prisma_extractors import quick_extract

# One-line extraction
result = quick_extract('document.jpg', 'smolvlm')
print(result['data'])
```

### **API Usage**
```python
from prisma_extractors import PrismaExtractorAPI

with PrismaExtractorAPI() as api:
    result = api.extract('document.jpg', 'smolvlm')
    performance = api.get_performance_report()
```

### **Factory Pattern**
```python
from prisma_extractors import ExtractorFactory

factory = ExtractorFactory()
extractor = factory.create_extractor('paddle')
result = extractor.extract('document.jpg')
```

## 📚 Available Modules

| Module | Responsibility | Usage |
|--------|----------------|-------|
| **ModelLoader** | Load & cache ML models | `loader.load_model(model_id, device)` |
| **ImageProcessor** | Image loading & preprocessing | `processor.load_image(path)` |
| **TextExtractor** | Text generation | `extractor.extract_with_vision_model(...)` |
| **JsonParser** | Parse JSON from text | `parser.parse(raw_text)` |
| **DocumentValidator** | Validate extracted data | `validator.validate(data)` |
| **PerformanceMonitor** | Track performance | `monitor.track_operation(name)` |
| **ConfigManager** | Manage configuration | `config.get('key', default)` |
| **ErrorHandler** | Handle errors | `handler.handle_error(exception)` |

## 🎯 Available Extractors

| Extractor | Type | Best For |
|-----------|------|----------|
| **smolvlm** | Vision-Language Model | Complex document understanding |
| **paddle** | OCR (PaddleOCR) | Fast text extraction |
| **doctr** | OCR (DocTR) | High-accuracy text recognition |

## 💡 Usage Examples

### **1. Compare All Extractors**
```python
from prisma_extractors import compare_all_extractors

results = compare_all_extractors('document.jpg')
for extractor, result in results.items():
    print(f\"{extractor}: {result['success']}\")
```

### **2. Batch Processing**
```python
from prisma_extractors import PrismaExtractorAPI

with PrismaExtractorAPI() as api:
    results = api.extract_batch([
        'doc1.jpg', 'doc2.jpg', 'doc3.jpg'
    ], extractor='smolvlm')
```

### **3. Custom Configuration**
```python
from prisma_extractors import ExtractorFactory

config = {
    'extractors': {
        'smolvlm': {
            'max_new_tokens': 256,
            'temperature': 0.0
        }
    }
}

factory = ExtractorFactory(config)
extractor = factory.create_extractor('smolvlm')
```

### **4. Using Individual Modules**
```python
from prisma_extractors import ImageProcessor, JsonParser

# Use modules independently
processor = ImageProcessor()
parser = JsonParser()

image = processor.load_image('document.jpg')
processed = processor.preprocess_for_ocr(image, enhance_contrast=True)

json_data = parser.parse('{\"fecha\": \"2024-01-15\"}')
```

### **5. Performance Monitoring**
```python
from prisma_extractors import PrismaExtractorAPI

with PrismaExtractorAPI() as api:
    api.extract('document.jpg', 'smolvlm')
    
    # Get detailed performance metrics
    performance = api.get_performance_report()
    print(f\"Average processing time: {performance['api_level']['summary']['avg_duration']:.2f}s\")
```

## 🔧 Configuration

### **File-based Configuration**
```yaml
# config.yaml
extractors:
  smolvlm:
    model_id: \"HuggingFaceTB/SmolVLM2-2.2B-Instruct\"
    max_new_tokens: 512
    device: \"auto\"
  
processing:
  batch_size: 1
  timeout: 300

validation:
  required_fields: [\"fecha\", \"autoridadEmisora\"]
  strict_mode: false
```

```python
from prisma_extractors import PrismaExtractorAPI

api = PrismaExtractorAPI('config.yaml')
```

### **Programmatic Configuration**
```python
from prisma_extractors import ConfigManager

config = ConfigManager()
config.set('extractors.smolvlm.max_new_tokens', 256)
config.set('processing.timeout', 60)

# Use with API
api = PrismaExtractorAPI(config.to_dict())
```

## 🧪 Advanced Usage

### **Custom Error Handling**
```python
from prisma_extractors.modules import ErrorHandler, ErrorSeverity

error_handler = ErrorHandler()

@error_handler.with_error_handling(
    severity=ErrorSeverity.HIGH,
    recovery_strategy='retry_with_fallback'
)
def extract_with_retry(image_path):
    # Your extraction logic
    pass
```

### **Custom Extractor**
```python
from prisma_extractors import ModularExtractor

class MyCustomExtractor(ModularExtractor):
    def _initialize_extractor(self):
        # Initialize your custom model
        pass
    
    def _extract_text(self, image):
        # Your custom extraction logic
        return \"extracted text\"

# Register with factory
factory = ExtractorFactory()
factory.register_extractor('custom', MyCustomExtractor)
```

## 📊 Benefits of Modular Architecture

### **✅ Single Responsibility Principle**
- Each module has **one reason to change**
- **Easy to test** individual components
- **Clear separation of concerns**

### **✅ Improved Testability**
- **Mock individual modules** easily
- **Unit test each responsibility**
- **Integration tests are cleaner**

### **✅ Better Maintainability**
- **Add new extractors** without changing existing code
- **Swap implementations** (e.g., different JSON parsers)
- **Bug fixes are isolated**

### **✅ Enhanced Reusability**
- **Use modules independently** in other projects
- **Compose custom workflows**
- **Mix and match components**

### **✅ Performance Benefits**
- **Model caching** across extractors
- **Centralized performance monitoring**
- **Efficient resource management**

## 🔍 Module Details

### **ModelLoader**
- **Caches models** to avoid reloading
- **Supports multiple model types** (vision, OCR, etc.)
- **Device-aware loading**

### **ImageProcessor** 
- **Validates image files**
- **Preprocessing for OCR**
- **Batch loading support**

### **TextExtractor**
- **Unified interface** for vision and OCR models
- **Configurable generation parameters**
- **Device management**

### **JsonParser**
- **Multiple parsing strategies**
- **Handles malformed JSON**
- **Flexible cleaning rules**

### **DocumentValidator**
- **Schema-based validation**
- **Custom validation rules**
- **Batch validation support**

### **PerformanceMonitor**
- **Operation timing**
- **Memory usage tracking**
- **Comprehensive reporting**

### **ConfigManager**
- **Multiple config sources** (file, dict, env)
- **Hierarchical configuration**
- **Validation support**

### **ErrorHandler**
- **Categorized error handling**
- **Recovery strategies**
- **Statistics tracking**

## 🚦 Migration from Original Code

### **Old Way**
```python
from smolvlm_extractor import SmolVLMExtractor

extractor = SmolVLMExtractor()
result = extractor.extract('document.jpg')
```

### **New Way**
```python
from prisma_extractors import quick_extract

result = quick_extract('document.jpg', 'smolvlm')
```

The new modular architecture is **100% compatible** but provides much more **flexibility and maintainability**.

## 📈 Next Steps

1. **Add more extractors** using the modular framework
2. **Implement caching strategies** at the module level
3. **Add monitoring dashboards** using PerformanceMonitor
4. **Create custom validators** for specific document types
5. **Build test suites** for each module independently

---

**The modular architecture makes the codebase more maintainable, testable, and reusable while following SOLID principles.**