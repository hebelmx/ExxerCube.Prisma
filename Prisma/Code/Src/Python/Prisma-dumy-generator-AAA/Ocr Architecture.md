# OCR Pipeline Integration Architecture (C#-Centric Hexagonal Design)

## **1. Executive Summary**

This document outlines the **Product Requirements Planning (PRP)** for integrating a modular Python-based OCR preprocessing and extraction pipeline into a larger C# application using **Hexagonal Architecture**. The design ensures clear separation of concerns, language interoperability through port/adapters, and maintainability via strict adherence to SRP in modular boundaries.

### **Key Business Value**
- **Automated Document Processing**: Extract structured data from Spanish legal documents with 95%+ accuracy
- **Scalable Architecture**: Support 1000+ documents per hour with configurable processing pipelines
- **Maintainable Codebase**: Clear module boundaries enabling parallel development by agile teams
- **Language Interoperability**: Seamless Python-C# integration via `csnakes` library

---

## **2. Product Requirements Planning (PRP)**

### **2.1 Epic: OCR Document Processing System**
**Epic ID**: OCR-001  
**Priority**: High  
**Story Points**: 21  
**Sprint Target**: 3 sprints

#### **User Stories**

**US-001: Process Legal Documents**  
*As a legal assistant, I want to automatically extract case information from scanned documents so that I can reduce manual data entry by 80%*

**Acceptance Criteria:**
- [ ] System processes Spanish legal documents (PDF/Images)
- [ ] Extracts expediente, causa, accion_solicitada, dates, amounts
- [ ] Outputs structured JSON and plain text
- [ ] Handles documents with red watermarks
- [ ] Achieves 95%+ OCR accuracy

**US-002: Configure Processing Pipeline**  
*As a system administrator, I want to configure which processing steps to apply so that I can optimize for different document types*

**Acceptance Criteria:**
- [ ] Enable/disable watermark removal
- [ ] Enable/disable image deskewing
- [ ] Enable/disable binarization
- [ ] Configure OCR language settings
- [ ] Enable/disable section extraction

**US-003: Monitor Processing Quality**  
*As a quality analyst, I want to see processing confidence scores so that I can identify documents needing manual review*

**Acceptance Criteria:**
- [ ] Display OCR confidence per page
- [ ] Show average confidence across documents
- [ ] Flag low-confidence results (<70%)
- [ ] Provide processing summary statistics

### **2.2 Technical Requirements**

#### **Performance Requirements**
- **Throughput**: Process 1000+ documents/hour
- **Latency**: <30 seconds per document
- **Accuracy**: 95%+ OCR confidence for clean documents
- **Scalability**: Support concurrent processing of 10+ documents

#### **Quality Requirements**
- **Reliability**: 99.9% uptime for processing pipeline
- **Error Handling**: Graceful degradation with detailed error reporting
- **Testing**: 90%+ code coverage with unit and integration tests

---

## **3. Current Modular Architecture (Implemented)**

### **3.1 Python Module Decomposition**

The OCR pipeline has been successfully modularized into **14 focused modules** following SRP:

#### **📊 Data Models (`models.py`)**
```python
# Core data structures with Pydantic validation
- ImageData: Image with metadata
- OCRConfig: OCR engine configuration  
- OCRResult: OCR output with confidence metrics
- ExtractedFields: Structured extracted data
- ProcessingConfig: Pipeline configuration
- ProcessingResult: Final processing result
```

#### **📁 File Operations**
- **`file_loader.py`**: Load images/PDFs with metadata
- **`output_writer.py`**: Persist results to TXT/JSON files

#### **🖼️ Image Processing (Pure Functions)**
- **`watermark_remover.py`**: Remove red diagonal watermarks
- **`image_deskewer.py`**: Detect and correct document skew  
- **`image_binarizer.py`**: Apply adaptive thresholding

#### **🔤 OCR Execution**
- **`ocr_executor.py`**: Execute Tesseract with fallback languages

#### **📝 Text Processing (Pure Functions)**
- **`text_normalizer.py`**: Clean and normalize OCR output
- **`section_extractor.py`**: Extract document sections by headers
- **`expediente_extractor.py`**: Extract case file identifiers
- **`date_extractor.py`**: Extract and normalize Spanish dates
- **`amount_extractor.py`**: Extract monetary amounts

#### **🔧 Pipeline Orchestration**
- **`pipeline.py`**: Compose all modules into complete workflow
- **`__init__.py`**: Package interface and exports

### **3.2 CLI Interface (`modular_ocr_cli.py`)**

```bash
# Basic usage
python modular_ocr_cli.py --input /path/to/docs --outdir /output

# Advanced configuration
python modular_ocr_cli.py \
  --input /path/to/docs \
  --outdir /output \
  --language spa \
  --fallback-language eng \
  --no-watermark-removal \
  --verbose
```

---

## **4. Hexagonal Architecture Design**

### **4.1 Domain Layer (C#)**

#### **Entities & Value Objects**
```csharp
public class ImageData
{
    public byte[] Data { get; set; }
    public string SourcePath { get; set; }
    public int PageNumber { get; set; }
    public int TotalPages { get; set; }
}

public class OCRResult
{
    public string Text { get; set; }
    public float ConfidenceAvg { get; set; }
    public float ConfidenceMedian { get; set; }
    public List<float> Confidences { get; set; }
    public string LanguageUsed { get; set; }
}

public class ExtractedFields
{
    public string? Expediente { get; set; }
    public string? Causa { get; set; }
    public string? AccionSolicitada { get; set; }
    public List<string> Fechas { get; set; } = new();
    public List<AmountData> Montos { get; set; } = new();
}
```

#### **Domain Interfaces (Ports)**
```csharp
public interface IOcrProcessingService
{
    Task<OCRResult> ExecuteOcrAsync(ImageData image, ProcessingConfig config);
}

public interface IImagePreprocessor
{
    Task<ImageData> PreprocessImageAsync(ImageData image, ProcessingConfig config);
}

public interface ITextFieldExtractor
{
    Task<ExtractedFields> ExtractFieldsAsync(string text, float confidence);
}

public interface IFileLoader
{
    Task<List<ImageData>> LoadImagesAsync(string path);
}

public interface IOutputWriter
{
    Task WriteOutputAsync(ProcessingResult result, string outputPath);
}
```

### **4.2 Application Layer (C#)**

#### **Use Cases**
```csharp
public class ProcessDocumentCommand
{
    public string InputPath { get; set; }
    public string OutputPath { get; set; }
    public ProcessingConfig Config { get; set; }
}

public class ProcessDocumentHandler : IRequestHandler<ProcessDocumentCommand, List<ProcessingResult>>
{
    private readonly IOcrProcessingService _ocrService;
    private readonly IImagePreprocessor _preprocessor;
    private readonly ITextFieldExtractor _extractor;
    private readonly IFileLoader _fileLoader;
    private readonly IOutputWriter _outputWriter;

    public async Task<List<ProcessingResult>> Handle(ProcessDocumentCommand request)
    {
        // 1. Load images
        var images = await _fileLoader.LoadImagesAsync(request.InputPath);
        
        // 2. Process each image
        var results = new List<ProcessingResult>();
        foreach (var image in images)
        {
            // 3. Preprocess
            var preprocessed = await _preprocessor.PreprocessImageAsync(image, request.Config);
            
            // 4. OCR
            var ocrResult = await _ocrService.ExecuteOcrAsync(preprocessed, request.Config);
            
            // 5. Extract fields
            var fields = await _extractor.ExtractFieldsAsync(ocrResult.Text, ocrResult.ConfidenceAvg);
            
            // 6. Write output
            var result = new ProcessingResult { /* ... */ };
            await _outputWriter.WriteOutputAsync(result, request.OutputPath);
            
            results.Add(result);
        }
        
        return results;
    }
}
```

### **4.3 Infrastructure Layer (Adapters)**

#### **Python Adapter Layer (via `csnakes`)**
```csharp
public class PythonOcrProcessingAdapter : IOcrProcessingService
{
    public async Task<OCRResult> ExecuteOcrAsync(ImageData image, ProcessingConfig config)
    {
        using (Py.GIL())
        {
            dynamic ocrModule = Py.Import("ocr_modules.ocr_executor");
            dynamic result = ocrModule.execute_ocr(image.ToNumpyArray(), config.ToDict());

            return new OCRResult
            {
                Text = result.text,
                ConfidenceAvg = (float)result.confidence_avg,
                ConfidenceMedian = (float)result.confidence_median,
                Confidences = ((PyList)result.confidences).ToList<float>(),
                LanguageUsed = result.language_used
            };
        }
    }
}

public class PythonImagePreprocessor : IImagePreprocessor
{
    public async Task<ImageData> PreprocessImageAsync(ImageData image, ProcessingConfig config)
    {
        using (Py.GIL())
        {
            dynamic pipelineModule = Py.Import("ocr_modules.pipeline");
            dynamic result = pipelineModule.preprocess_image(image.ToDict(), config.ToDict());
            
            return ImageData.FromDict(result);
        }
    }
}
```

#### **I/O Adapter Layer**
```csharp
public class FileSystemLoader : IFileLoader
{
    public async Task<List<ImageData>> LoadImagesAsync(string path)
    {
        using (Py.GIL())
        {
            dynamic fileLoader = Py.Import("ocr_modules.file_loader");
            dynamic images = fileLoader.load_images_from_path(path);
            
            return ((PyList)images).ToList<ImageData>();
        }
    }
}

public class FileSystemOutputWriter : IOutputWriter
{
    public async Task WriteOutputAsync(ProcessingResult result, string outputPath)
    {
        using (Py.GIL())
        {
            dynamic outputWriter = Py.Import("ocr_modules.output_writer");
            outputWriter.write_processing_result(result.ToDict(), outputPath);
        }
    }
}
```

---

## **5. Sprint Planning & Development Roadmap**

### **Sprint 1: Foundation (Story Points: 8)**
**Goal**: Establish core infrastructure and basic OCR integration

**Tasks:**
- [ ] Set up C# project structure with Hexagonal Architecture
- [ ] Implement domain models and interfaces
- [ ] Create Python adapter layer with `csnakes`
- [ ] Implement basic file I/O adapters
- [ ] Create unit tests for domain layer

**Definition of Done:**
- [ ] All domain interfaces defined and documented
- [ ] Python-C# integration working for basic OCR
- [ ] 80%+ test coverage for domain layer
- [ ] CI/CD pipeline configured

### **Sprint 2: Core Processing (Story Points: 8)**
**Goal**: Implement complete document processing pipeline

**Tasks:**
- [ ] Implement image preprocessing adapters
- [ ] Implement text field extraction adapters
- [ ] Create processing orchestration service
- [ ] Add error handling and logging
- [ ] Implement configuration management

**Definition of Done:**
- [ ] End-to-end document processing working
- [ ] All Python modules integrated via adapters
- [ ] Comprehensive error handling implemented
- [ ] Performance benchmarks established

### **Sprint 3: Quality & Optimization (Story Points: 5)**
**Goal**: Optimize performance and ensure production readiness

**Tasks:**
- [ ] Implement async processing for scalability
- [ ] Add monitoring and metrics collection
- [ ] Optimize memory usage and performance
- [ ] Create comprehensive integration tests
- [ ] Document deployment procedures

**Definition of Done:**
- [ ] System processes 1000+ documents/hour
- [ ] 95%+ OCR accuracy achieved
- [ ] Full integration test suite passing
- [ ] Production deployment guide completed

---

## **6. Technical Implementation Details**

### **6.1 Interoperability Contract**

#### **Data Serialization**
```csharp
// C# to Python conversion
public static class DataConverter
{
    public static dynamic ToDict(this ImageData image)
    {
        return new PyDict
        {
            ["data"] = image.Data.ToNumpyArray(),
            ["source_path"] = image.SourcePath,
            ["page_number"] = image.PageNumber,
            ["total_pages"] = image.TotalPages
        };
    }
    
    public static ImageData FromDict(dynamic dict)
    {
        return new ImageData
        {
            Data = ((PyArray)dict["data"]).GetData<byte>(),
            SourcePath = dict["source_path"],
            PageNumber = dict["page_number"],
            TotalPages = dict["total_pages"]
        };
    }
}
```

#### **Configuration Mapping**
```csharp
public static class ConfigMapper
{
    public static dynamic ToDict(this ProcessingConfig config)
    {
        return new PyDict
        {
            ["remove_watermark"] = config.RemoveWatermark,
            ["deskew"] = config.Deskew,
            ["binarize"] = config.Binarize,
            ["ocr_config"] = config.OcrConfig.ToDict(),
            ["extract_sections"] = config.ExtractSections,
            ["normalize_text"] = config.NormalizeText
        };
    }
}
```

### **6.2 Error Handling Strategy**

```csharp
public class ProcessingException : Exception
{
    public string Module { get; }
    public string Operation { get; }
    public ProcessingResult PartialResult { get; }

    public ProcessingException(string module, string operation, string message, ProcessingResult partialResult = null)
        : base(message)
    {
        Module = module;
        Operation = operation;
        PartialResult = partialResult;
    }
}

public class ResilientProcessingService
{
    public async Task<ProcessingResult> ProcessWithFallback(ImageData image, ProcessingConfig config)
    {
        try
        {
            return await ProcessNormally(image, config);
        }
        catch (ProcessingException ex) when (ex.Module == "OCR")
        {
            // Fallback to different OCR settings
            return await ProcessWithFallbackConfig(image, config);
        }
        catch (ProcessingException ex) when (ex.Module == "Preprocessing")
        {
            // Skip preprocessing, try OCR on original image
            return await ProcessWithoutPreprocessing(image, config);
        }
    }
}
```

### **6.3 Performance Optimization**

#### **Async Processing Pipeline**
```csharp
public class AsyncProcessingPipeline
{
    private readonly SemaphoreSlim _semaphore;
    
    public AsyncProcessingPipeline(int maxConcurrency = 5)
    {
        _semaphore = new SemaphoreSlim(maxConcurrency);
    }
    
    public async Task<List<ProcessingResult>> ProcessBatchAsync(
        List<ImageData> images, 
        ProcessingConfig config)
    {
        var tasks = images.Select(image => ProcessSingleAsync(image, config));
        return await Task.WhenAll(tasks);
    }
    
    private async Task<ProcessingResult> ProcessSingleAsync(ImageData image, ProcessingConfig config)
    {
        await _semaphore.WaitAsync();
        try
        {
            using (Py.GIL())
            {
                // Process image with Python modules
                return await ProcessImage(image, config);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

---

## **7. Testing Strategy**

### **7.1 Unit Testing**
```csharp
[TestFixture]
public class OcrProcessingServiceTests
{
    private Mock<IImagePreprocessor> _preprocessorMock;
    private Mock<ITextFieldExtractor> _extractorMock;
    private IOcrProcessingService _service;

    [SetUp]
    public void Setup()
    {
        _preprocessorMock = new Mock<IImagePreprocessor>();
        _extractorMock = new Mock<ITextFieldExtractor>();
        _service = new PythonOcrProcessingAdapter();
    }

    [Test]
    public async Task ExecuteOcr_ValidImage_ReturnsExpectedResult()
    {
        // Arrange
        var image = CreateTestImage();
        var config = CreateTestConfig();

        // Act
        var result = await _service.ExecuteOcrAsync(image, config);

        // Assert
        Assert.That(result.ConfidenceAvg, Is.GreaterThan(80.0f));
        Assert.That(result.Text, Is.Not.Empty);
    }
}
```

### **7.2 Integration Testing**
```csharp
[TestFixture]
public class EndToEndProcessingTests
{
    [Test]
    public async Task ProcessLegalDocument_CompletePipeline_ExtractsAllFields()
    {
        // Arrange
        var handler = CreateProcessingHandler();
        var command = new ProcessDocumentCommand
        {
            InputPath = "test_documents/legal_doc.pdf",
            OutputPath = "test_output",
            Config = CreateDefaultConfig()
        };

        // Act
        var results = await handler.Handle(command);

        // Assert
        Assert.That(results, Has.Count.EqualTo(1));
        var result = results[0];
        Assert.That(result.ExtractedFields.Expediente, Is.Not.Null);
        Assert.That(result.ExtractedFields.Causa, Is.Not.Null);
        Assert.That(result.OCRResult.ConfidenceAvg, Is.GreaterThan(90.0f));
    }
}
```

---

## **8. Deployment & Operations**

### **8.1 Environment Requirements**
- **Python 3.9+** with required packages (`pytesseract`, `opencv-python`, `pydantic`)
- **Tesseract OCR** engine installed and configured
- **.NET 6+** runtime environment
- **csnakes** library for Python-C# interop

### **8.2 Configuration Management**
```json
{
  "OcrProcessing": {
    "DefaultLanguage": "spa",
    "FallbackLanguage": "eng",
    "MaxConcurrency": 5,
    "TimeoutSeconds": 300,
    "EnableWatermarkRemoval": true,
    "EnableDeskewing": true,
    "EnableBinarization": true
  },
  "Logging": {
    "LogLevel": "Information",
    "EnableMetrics": true
  }
}
```

### **8.3 Monitoring & Metrics**
```csharp
public class ProcessingMetrics
{
    public Counter ProcessedDocuments { get; set; }
    public Histogram ProcessingTime { get; set; }
    public Gauge AverageConfidence { get; set; }
    public Counter ProcessingErrors { get; set; }
}
```

---

## **9. Risk Assessment & Mitigation**

### **9.1 Technical Risks**

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Python-C# interop performance | Medium | High | Implement connection pooling, async processing |
| Memory leaks in Python modules | Low | Medium | Implement proper disposal patterns, memory monitoring |
| OCR accuracy degradation | Medium | High | Implement confidence thresholds, fallback strategies |
| Scalability bottlenecks | High | Medium | Design for horizontal scaling, implement caching |

### **9.2 Business Risks**

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Document format changes | Medium | High | Implement flexible parsing, version compatibility |
| Regulatory compliance | Low | High | Implement audit trails, data retention policies |
| Performance requirements not met | Medium | Medium | Early performance testing, optimization sprints |

---

## **10. Success Criteria & KPIs**

### **10.1 Technical KPIs**
- **OCR Accuracy**: ≥95% for clean documents
- **Processing Speed**: ≥1000 documents/hour
- **System Uptime**: ≥99.9%
- **Error Rate**: ≤1% of processed documents

### **10.2 Business KPIs**
- **Manual Data Entry Reduction**: ≥80%
- **Processing Cost Reduction**: ≥60%
- **User Satisfaction**: ≥4.5/5.0
- **Time to Market**: ≤3 sprints

---

## **11. Conclusion**

This PRP provides a comprehensive roadmap for implementing a modular OCR pipeline integration using Hexagonal Architecture. The design leverages the existing Python modularization while providing a clean C# interface for enterprise integration.

**Key Success Factors:**
1. **Modular Design**: Leverages existing Python modules with clear SRP
2. **Hexagonal Architecture**: Ensures clean separation and testability
3. **Agile Delivery**: 3-sprint roadmap with clear milestones
4. **Quality Focus**: Comprehensive testing and monitoring strategy

**Next Steps:**
1. Review and approve this PRP with stakeholders
2. Begin Sprint 1 implementation
3. Set up development environment and CI/CD pipeline
4. Conduct regular sprint reviews and retrospectives

