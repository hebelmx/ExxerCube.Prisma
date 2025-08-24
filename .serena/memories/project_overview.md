# ExxerCube.Prisma Project Overview

## Project Purpose
ExxerCube.Prisma is a C# implementation of an OCR document processing pipeline using Hexagonal Architecture to integrate with existing Python modules. The solution provides a clean, maintainable interface for processing Spanish legal documents with high accuracy.

## Tech Stack
- **.NET 10** with latest features and performance improvements
- **C#** with nullable reference types and modern language features
- **Python 3.9+** with modular OCR pipeline (CSnakes integration)
- **Tesseract OCR** engine for text extraction
- **ASP.NET Core** with Blazor Server for web interface
- **MudBlazor** for modern UI components
- **SignalR** for real-time updates
- **Entity Framework Core** for data persistence
- **xUnit v3** for testing with Shouldly and NSubstitute
- **Meziantou.Extensions.Logging.Xunit.v3** for test logging

## Architecture
- **Hexagonal Architecture**: Clean separation between domain, application, and infrastructure layers
- **Railway Oriented Programming**: Uses `Result<T>` pattern for error handling without exceptions
- **Python-C# Integration**: Uses `csnakes` library for seamless Python module integration
- **Async Processing**: Supports concurrent document processing for high throughput
- **Comprehensive Testing**: Unit tests, integration tests, and end-to-end tests

## Code Style and Conventions
- **TreatWarningsAsErrors**: All code must compile with warnings treated as errors
- **Nullable Reference Types**: Enabled for better null safety
- **XML Documentation**: All public APIs must have XML documentation
- **Expression-bodied Members**: Preferred without curly braces for simple methods
- **Railway Oriented Programming**: Use `Result<T>` pattern for error handling
- **Fluent API**: Non-exception throwing, fluent language design
- **Comprehensive Logging**: Structured logging with correlation IDs

## Project Structure
```
CSharp/
├── docs/                           # Documentation
│   ├── architecture/               # Architecture documents
│   ├── api/                        # API documentation
│   └── deployment/                 # Deployment guides
├── Domain/                         # Domain layer (entities, interfaces)
├── Application/                    # Application layer (services, use cases)
├── Infrastructure/                 # Infrastructure layer (adapters, external services)
├── Tests/                          # Test projects
├── UI/                             # Web UI (Blazor Server)
└── Python/                         # Python modules (OCR pipeline)
```

## Key Features
- **Modular OCR Pipeline**: Python-based with individual modules for each processing step
- **Real-time Processing**: SignalR integration for live status updates
- **Comprehensive Metrics**: Performance monitoring and analytics
- **Health Monitoring**: System health checks and diagnostics
- **Web Interface**: Modern Blazor UI with MudBlazor components
- **API Endpoints**: RESTful API for document processing
- **Error Handling**: Robust error handling with circuit breaker pattern
- **Configuration Management**: Environment-specific configuration
- **Testing Strategy**: Unit, integration, and end-to-end tests

## Current State
- **Python Pipeline**: Complete and tested with unit tests
- **C# Integration**: Basic integration with TODO items for field extraction
- **Web Interface**: Functional with document upload and processing
- **Testing**: Unit tests implemented, some using mocks instead of real Python modules
- **TODO Items**: Several TODO comments in OcrProcessingAdapter for field extraction methods
- **Quality Assurance**: Comprehensive QA roadmap in quality-assurance-todo.md

## Issues Identified
1. **TODO Comments**: Multiple TODO items in OcrProcessingAdapter for field extraction
2. **Mock Testing**: Some tests use mocks instead of real Python implementation
3. **Missing Integration**: Field extraction methods not fully implemented
4. **Quality Gaps**: Need comprehensive test coverage, mutation testing, and E2E tests
5. **Production Readiness**: Missing monitoring, security, and performance optimizations