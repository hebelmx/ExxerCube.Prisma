# Comprehensive Testing Suite for Prisma AI Extractors

## 📋 Overview

This document describes the comprehensive testing framework for the modularized Prisma AI Extractors. The testing suite covers **unit tests**, **integration tests**, **end-to-end tests**, and **performance testing** with **80%+ code coverage** target.

## 🏗️ Test Architecture

### **Test Pyramid Structure**
```
           E2E Tests (Slow, High Value)
          /                           \
     Integration Tests (Medium Speed)
    /                                 \
Unit Tests (Fast, High Coverage)
```

### **Test Categories**

| Test Type | Purpose | Speed | Coverage |
|-----------|---------|-------|----------|
| **Unit Tests** | Individual module functionality | Fast (< 1s each) | 90%+ |
| **Integration Tests** | Module interactions | Medium (1-5s each) | 70%+ |
| **E2E Tests** | Complete workflow | Slow (5-30s each) | Full pipeline |
| **Performance Tests** | Speed & memory benchmarks | Variable | Critical paths |

## 🚀 Quick Start

### **Run All Tests**
```bash
make test
```

### **Quick Development Testing**
```bash
make test-quick
```

### **With Coverage Report**
```bash
make coverage
```

### **Generate Test Fixtures**
```bash
make fixtures
```

## 📁 Test Structure

```
tests/
├── conftest.py                 # Shared fixtures and configuration
├── pytest.ini                  # Pytest configuration
├── unit/                       # Unit tests (90%+ coverage target)
│   ├── test_model_loader.py
│   ├── test_image_processor.py
│   ├── test_json_parser.py
│   ├── test_document_validator.py
│   └── ...
├── integration/                # Integration tests
│   ├── test_modular_extractors.py
│   ├── test_api_integration.py
│   └── test_factory_integration.py
├── e2e/                       # End-to-end tests
│   ├── test_end_to_end.py
│   └── test_real_world_scenarios.py
├── performance/               # Performance and load tests
│   ├── test_performance.py
│   └── test_load_testing.py
└── data/                      # Test data and fixtures
    ├── configs/
    │   └── test_config.yaml
    └── fixtures/
        ├── generate_test_fixtures.py
        ├── images/            # Generated test images
        └── ground_truth/      # Expected results
```

## 🧪 Test Types Detail

### **1. Unit Tests** 
Test individual modules in isolation with mocked dependencies.

**Coverage Target: 90%+**

```bash
# Run unit tests only
make test-unit

# With coverage
pytest tests/unit/ --cov=src --cov-report=html
```

**Key Test Files:**
- `test_model_loader.py` - Model loading and caching
- `test_image_processor.py` - Image processing operations  
- `test_json_parser.py` - JSON parsing with multiple strategies
- `test_document_validator.py` - Data validation logic
- `test_performance_monitor.py` - Performance tracking
- `test_config_manager.py` - Configuration management
- `test_error_handler.py` - Error handling and recovery

### **2. Integration Tests**
Test interactions between modules with mocked external dependencies.

```bash
# Run integration tests
make test-integration

# Run with real models (slow)
make test-real
```

**Test Scenarios:**
- ✅ Extractor initialization and configuration
- ✅ Module interaction workflows
- ✅ Error propagation between components
- ✅ Performance monitoring integration
- ✅ Configuration cascade effects

### **3. End-to-End Tests**
Test complete extraction pipeline from image to structured data.

```bash
# Run E2E tests (mocked)
make test-e2e

# Run with realistic document images
pytest tests/e2e/ -m "not mock"
```

**Test Scenarios:**
- ✅ Complete extraction workflow
- ✅ API interface testing
- ✅ Batch processing
- ✅ Error recovery scenarios  
- ✅ Configuration variations
- ✅ Multi-extractor comparisons

### **4. Performance Tests**
Benchmark performance and identify bottlenecks.

```bash
# Run performance tests
make test-performance

# Generate benchmarks
make benchmark

# Monitor memory usage
make monitor-memory
```

**Performance Metrics:**
- ⏱️ Processing time per document
- 💾 Memory usage patterns
- 🔄 Concurrent processing capability
- 📊 Throughput measurements
- 🚀 Model loading times

## 🎯 Test Fixtures & Data

### **Automated Test Data Generation**

The test suite includes a comprehensive fixture generator that creates realistic legal documents:

```bash
# Generate test fixtures
cd tests/data/fixtures
python generate_test_fixtures.py
```

**Generated Content:**
- **100+ realistic legal documents** with variations
- **Multiple image qualities** (high, medium, low)
- **Realistic Spanish legal terminology**
- **Watermarks and noise simulation**
- **Edge cases and malformed data**

**Document Variants:**
- `standard` - Complete documents with all fields
- `minimal` - Documents with only required fields  
- `complex` - Documents with nested data and attachments
- `malformed` - Documents with common data issues

### **Test Data Categories**

| Category | Count | Purpose |
|----------|--------|---------|
| **Standard Documents** | 40 | Normal processing scenarios |
| **Edge Cases** | 20 | Boundary condition testing |
| **Performance Data** | 25 | Load and stress testing |
| **Error Scenarios** | 15 | Error handling validation |

## 🔧 Configuration

### **Test Configuration (`tests/data/configs/test_config.yaml`)**

```yaml
extractors:
  smolvlm:
    model_id: "test-smolvlm-model"
    max_new_tokens: 128
    device: "cpu"
  paddle:
    use_gpu: false
    lang: "es"

testing:
  mock_models: true
  use_cpu_only: true
  fast_mode: true
  test_data_path: "tests/data"
```

### **Pytest Configuration (`pytest.ini`)**

```ini
[tool:pytest]
testpaths = tests
addopts = --strict-markers --cov=src --cov-report=term-missing
markers =
    unit: Unit tests
    integration: Integration tests  
    e2e: End-to-end tests
    performance: Performance tests
    slow: Slow running tests
    mock: Tests using mocks
```

## 📊 Coverage Requirements

### **Coverage Targets**
- **Unit Tests**: 90%+ line coverage
- **Integration Tests**: 70%+ additional coverage
- **Overall Project**: 80%+ combined coverage

### **Coverage Commands**
```bash
# Generate HTML coverage report
make coverage

# Check coverage thresholds
pytest --cov=src --cov-fail-under=80

# View coverage report
open htmlcov/index.html
```

### **Coverage Exclusions**
```python
# Lines excluded from coverage:
- Abstract methods
- Debug-only code  
- Exception handling for impossible cases
- Third-party integration stubs
```

## 🏃‍♂️ Running Tests

### **Development Workflow**

```bash
# 1. Quick unit tests during development
make test-quick

# 2. Full test suite before commits
make test

# 3. Generate fixtures when needed
make fixtures

# 4. Check coverage
make coverage

# 5. Format code
make format

# 6. Full validation
make validate
```

### **CI/CD Pipeline**

```bash
# Install dependencies  
make ci-setup

# Run CI test suite
make ci-test

# Generate reports
make report
```

### **Different Test Modes**

```bash
# Fast tests only (< 30 seconds)
make test-quick

# All tests including slow ones
make test-all  

# Tests with real models (requires GPU)
make test-real

# Performance benchmarks
make benchmark

# Memory profiling
make profile
```

## ⚡ Performance Benchmarks

### **Current Performance Targets**

| Metric | Target | Measurement |
|--------|---------|-------------|
| **Unit Test Runtime** | < 30 seconds | All unit tests combined |
| **Image Loading** | < 1 second | Per 1920x1080 image |
| **JSON Parsing** | < 0.1 seconds | Per complex document |
| **Document Validation** | < 0.01 seconds | Per document |
| **Memory Usage** | < 500MB growth | Per 50 document batch |
| **Model Caching** | < 10% overhead | Vs direct loading |

### **Benchmark Reports**

```bash
# Run performance benchmarks
pytest tests/performance/ --benchmark-json=benchmark.json

# View benchmark history
python -m pytest_benchmark compare
```

## 🐛 Debugging Tests

### **Verbose Test Output**
```bash
# Detailed test output
pytest tests/ -v -s

# Show test durations
pytest tests/ --durations=10

# Stop on first failure
pytest tests/ -x

# Run specific test
pytest tests/unit/test_json_parser.py::TestJsonParser::test_parse_valid_json -v
```

### **Debug Configuration**
```bash
# Enable debug logging
export PYTEST_CURRENT_TEST=$(pytest --collect-only -q tests/unit/test_json_parser.py)

# Run with debugger
pytest tests/ --pdb

# Capture print statements
pytest tests/ -s
```

## 🔄 Continuous Integration

### **GitHub Actions Workflow**

The project includes a comprehensive CI/CD pipeline (`.github/workflows/test.yml`):

**Jobs:**
1. **Code Quality** - Linting, formatting, security
2. **Unit Tests** - Python 3.9, 3.10, 3.11 matrix
3. **Integration Tests** - Mocked dependencies  
4. **E2E Tests** - Full pipeline testing
5. **Performance Tests** - Nightly benchmarks
6. **Security Scanning** - Vulnerability detection
7. **Test Reporting** - Coverage and results

### **Local CI Simulation**
```bash
# Run full CI pipeline locally
tox

# Test specific environments
tox -e py311-unit
tox -e py311-integration  
tox -e coverage
```

## 🎨 Test Markers

Tests are organized using pytest markers for selective execution:

```bash
# Run only unit tests
pytest -m unit

# Run only mocked tests (CI-friendly)
pytest -m mock

# Run integration tests
pytest -m integration

# Run performance tests
pytest -m performance

# Skip slow tests
pytest -m "not slow"

# Run specific combinations
pytest -m "unit and not slow"
pytest -m "integration and mock"
```

## 📈 Metrics and Reporting

### **Test Metrics Tracked**
- ✅ **Test Count**: 200+ total tests
- ✅ **Coverage**: 80%+ line coverage
- ✅ **Performance**: Sub-second processing
- ✅ **Success Rate**: 99%+ test pass rate
- ✅ **Reliability**: No flaky tests

### **Generated Reports**
```bash
# HTML test report with details
make report

# Coverage report
make coverage

# Performance benchmarks  
make benchmark

# Security scan
make security
```

## 🚨 Troubleshooting

### **Common Issues**

**Test Failures:**
```bash
# Clean and retry
make clean
make fixtures
make test

# Check dependencies
pip install -r requirements-test.txt

# Verify fixtures
ls tests/data/fixtures/images/
```

**Performance Issues:**
```bash
# Run only fast tests
make test-quick

# Skip slow tests
pytest -m "not slow"

# Use CPU-only mode
export CUDA_VISIBLE_DEVICES=""
```

**Coverage Issues:**
```bash
# Check missing coverage
pytest --cov=src --cov-report=term-missing

# Exclude test files
pytest --cov=src --cov-report=html
```

### **Test Environment**
```bash
# Verify test environment
python -c "
import sys
print(f'Python: {sys.version}')
import torch
print(f'PyTorch: {torch.__version__}')
import pytest
print(f'Pytest: {pytest.__version__}')
"
```

## 🎯 Best Practices

### **Writing Tests**
1. **Use descriptive test names** that explain the scenario
2. **Follow AAA pattern** (Arrange, Act, Assert)
3. **Mock external dependencies** for unit tests
4. **Use fixtures** for common test data
5. **Test both success and failure paths**
6. **Keep tests fast and independent**

### **Test Organization**
1. **Group related tests** in classes
2. **Use appropriate markers** for categorization
3. **Maintain test data** separate from test logic
4. **Document complex test scenarios**
5. **Keep mocks simple** and realistic

### **Performance Testing**
1. **Set realistic benchmarks** based on hardware
2. **Test with different data sizes**
3. **Monitor memory usage**
4. **Use profiling** to identify bottlenecks
5. **Test concurrent scenarios**

---

## 📚 Additional Resources

- **Pytest Documentation**: https://docs.pytest.org/
- **Coverage.py Guide**: https://coverage.readthedocs.io/
- **Testing Best Practices**: Internal wiki link
- **Performance Profiling**: Internal performance guide

---

**The testing suite ensures high code quality, comprehensive coverage, and reliable performance for production deployment.**