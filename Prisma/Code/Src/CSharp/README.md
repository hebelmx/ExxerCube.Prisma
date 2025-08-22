# ExxerCube OCR Pipeline - C# Implementation

## Project Overview

This C# solution implements the OCR document processing pipeline using Hexagonal Architecture to integrate with the existing Python modules. The solution provides a clean, maintainable interface for processing Spanish legal documents with high accuracy.

## Architecture

- **Hexagonal Architecture**: Clean separation between domain, application, and infrastructure layers
- **Railway Oriented Programming**: Uses `Result<T>` pattern for error handling without exceptions
- **Python-C# Integration**: Uses `csnakes` library for seamless Python module integration
- **Async Processing**: Supports concurrent document processing for high throughput
- **Comprehensive Testing**: Unit tests, integration tests, and end-to-end tests
- **Modern .NET**: Built on .NET 10 with latest features and performance improvements

## Quick Start

### Prerequisites

1. **.NET 10 SDK**
2. **Python 3.9+** with required packages
3. **Tesseract OCR** engine
4. **Visual Studio 2022** or **VS Code**

### Setup

```bash
# Clone and navigate to project
cd Prisma/Code/Src/CSharp

# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run tests
dotnet test

# Run the application
dotnet run --project src/ExxerCube.Ocr.Console
```

## Project Structure

```
CSharp/
├── docs/                           # Documentation
│   ├── architecture/               # Architecture documents
│   ├── api/                        # API documentation
│   └── deployment/                 # Deployment guides
├── src/                            # Source code
│   ├── ExxerCube.Ocr.Domain/       # Domain layer
│   ├── ExxerCube.Ocr.Application/  # Application layer
│   ├── ExxerCube.Ocr.Infrastructure/ # Infrastructure layer
│   └── ExxerCube.Ocr.Console/      # Console application
├── tests/                          # Test projects
│   ├── ExxerCube.Ocr.Domain.Tests/
│   ├── ExxerCube.Ocr.Application.Tests/
│   └── ExxerCube.Ocr.Integration.Tests/
├── scripts/                        # Build and deployment scripts
└── samples/                        # Sample documents and configurations
```

## Development Workflow

1. **Feature Development**: Create feature branch from `main`
2. **Implementation**: Follow TDD approach with unit tests
3. **Documentation**: Add XML documentation for all public APIs
4. **Code Review**: Submit pull request for review
5. **Integration**: Merge to `main` after approval

## Testing Strategy

- **Unit Tests**: Test individual components in isolation using xUnit v3
- **Integration Tests**: Test Python-C# integration
- **End-to-End Tests**: Test complete document processing pipeline
- **Performance Tests**: Validate throughput and latency requirements

## Quality Standards

- **Warnings as Errors**: All code must compile with warnings treated as errors
- **Railway Oriented Programming**: Use `Result<T>` pattern for error handling
- **Fluent API**: Non-exception throwing, fluent language design
- **Comprehensive Logging**: Structured logging with correlation IDs
- **Telemetry & Metrics**: Application Insights and custom metrics
- **Code Coverage**: 80%+ unit test coverage requirement

## Configuration

See `docs/configuration.md` for detailed configuration options.

## Contributing

See `docs/contributing.md` for development guidelines and standards.
