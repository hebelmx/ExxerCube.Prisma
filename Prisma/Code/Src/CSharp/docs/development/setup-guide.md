# Development Environment Setup Guide

## Prerequisites

### Required Software

1. **.NET 10 SDK**
   - Download from: https://dotnet.microsoft.com/download/dotnet/10.0
   - Verify installation: `dotnet --version`

2. **Python 3.9+**
   - Download from: https://www.python.org/downloads/
   - Verify installation: `python --version`

3. **Tesseract OCR**
   - **Windows**: Download from https://github.com/UB-Mannheim/tesseract/wiki
   - **macOS**: `brew install tesseract`
   - **Linux**: `sudo apt-get install tesseract-ocr`
   - Verify installation: `tesseract --version`

4. **Visual Studio 2022** or **VS Code**
   - Visual Studio 2022 Community Edition (free)
   - VS Code with C# extension

### Python Dependencies

Install required Python packages:

```bash
# Navigate to Python modules directory
cd Prisma/Code/Src/ocr_modules

# Install dependencies
pip install -r requirements.txt

# Additional packages for C# integration
pip install pythonnet
pip install numpy
pip install pillow
```

## Project Setup

### 1. Clone and Navigate

```bash
# Navigate to C# project directory
cd Prisma/Code/Src/CSharp
```

### 2. Restore Dependencies

```bash
# Restore .NET packages
dotnet restore
```

### 3. Build Solution

```bash
# Build all projects
dotnet build
```

### 4. Run Tests

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test tests/ExxerCube.Ocr.Domain.Tests/
```

## Environment Configuration

### 1. Environment Variables

Set the following environment variables:

**Windows (PowerShell)**:
```powershell
$env:PYTHONPATH = "F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\ocr_modules"
$env:TESSERACT_PATH = "C:\Program Files\Tesseract-OCR\tesseract.exe"
$env:ASPNETCORE_ENVIRONMENT = "Development"
```

**Windows (Command Prompt)**:
```cmd
set PYTHONPATH=F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\ocr_modules
set TESSERACT_PATH=C:\Program Files\Tesseract-OCR\tesseract.exe
set ASPNETCORE_ENVIRONMENT=Development
```

**macOS/Linux**:
```bash
export PYTHONPATH="/path/to/ocr_modules"
export TESSERACT_PATH="/usr/bin/tesseract"
export ASPNETCORE_ENVIRONMENT=Development
```

### 2. Configuration Files

Create `appsettings.Development.json` in the console project:

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
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "Telemetry": {
    "ApplicationInsights": {
      "ConnectionString": "YOUR_CONNECTION_STRING"
    },
    "OpenTelemetry": {
      "Enabled": true,
      "Metrics": {
        "Enabled": true
      },
      "Tracing": {
        "Enabled": true
      }
    }
  }
}
```

## Development Workflow

### 1. Feature Development

```bash
# Create feature branch
git checkout -b feature/US-001-project-structure

# Make changes and commit
git add .
git commit -m "Implement project structure with Hexagonal Architecture"

# Push to remote
git push origin feature/US-001-project-structure
```

### 2. Running the Application

```bash
# Run console application
dotnet run --project src/ExxerCube.Ocr.Console

# Run with specific configuration
dotnet run --project src/ExxerCube.Ocr.Console -- --input samples/documents --output results
```

### 3. Testing

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test
dotnet test --filter "FullyQualifiedName~TestClassName"

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"
```

### 4. Code Quality Checks

```bash
# Run code analysis
dotnet build --verbosity normal

# Check for warnings (should be treated as errors)
dotnet build --verbosity normal

# Run style analysis
dotnet format --verify-no-changes

# Run security analysis
dotnet list package --vulnerable
```

## IDE Configuration

### Visual Studio 2022

1. **Extensions**:
   - Install "Python" extension
   - Install "Test Explorer" extension
   - Install "SonarLint" extension
   - Install "CodeMaid" extension

2. **Settings**:
   - Set Python interpreter path
   - Configure test discovery
   - Enable "Treat Warnings as Errors"
   - Configure code analysis rules

3. **Code Style**:
   - Enable EditorConfig support
   - Configure formatting rules
   - Set up code snippets

### VS Code

1. **Extensions**:
   - C# Dev Kit
   - Python
   - Test Explorer UI
   - SonarLint
   - EditorConfig for VS Code

2. **Settings**:
   - Set Python interpreter
   - Configure C# language server
   - Enable "Treat Warnings as Errors"
   - Configure formatting rules

3. **Workspace Settings**:
```json
{
  "dotnet.defaultSolution": "ExxerCube.Ocr.sln",
  "dotnet.testExplorer.enabled": true,
  "dotnet.testExplorer.testProjectPath": "**/*Tests.csproj",
  "omnisharp.enableEditorConfigSupport": true,
  "omnisharp.enableRoslynAnalyzers": true,
  "csharp.format.enable": true,
  "editor.formatOnSave": true,
  "editor.codeActionsOnSave": {
    "source.fixAll": true,
    "source.organizeImports": true
  }
}
```

## Quality Assurance

### 1. Pre-commit Checks

```bash
# Run all quality checks before committing
dotnet build --verbosity normal
dotnet test --collect:"XPlat Code Coverage"
dotnet format --verify-no-changes
dotnet list package --vulnerable
```

### 2. Code Coverage Requirements

```bash
# Generate coverage report
dotnet test --collect:"XPlat Code Coverage" --results-directory coverage

# View coverage report (requires ReportGenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:coverage/**/coverage.cobertura.xml -targetdir:coverage-report -reporttypes:Html
```

### 3. Performance Testing

```bash
# Run performance tests
dotnet test --filter "Category=Performance"

# Run benchmarks
dotnet run --project benchmarks/ExxerCube.Ocr.Benchmarks
```

## Troubleshooting

### Common Issues

1. **Python Import Errors**
   - Verify PYTHONPATH is set correctly
   - Ensure Python modules are in the correct location
   - Check Python version compatibility
   - Verify pip packages are installed

2. **Tesseract Not Found**
   - Verify TESSERACT_PATH environment variable
   - Ensure Tesseract is installed and accessible
   - Check PATH environment variable
   - Verify language packs are installed

3. **Build Errors**
   - Clean solution: `dotnet clean`
   - Restore packages: `dotnet restore`
   - Rebuild: `dotnet build`
   - Check for warnings treated as errors

4. **Test Failures**
   - Check environment variables
   - Verify Python dependencies are installed
   - Ensure test data files are present
   - Check test configuration

5. **.NET 10 Issues**
   - Verify .NET 10 SDK is installed
   - Check global.json for version constraints
   - Update Visual Studio to latest version
   - Clear NuGet cache: `dotnet nuget locals all --clear`

### Getting Help

1. Check the troubleshooting section above
2. Review the architecture documentation
3. Check existing issues in the project repository
4. Contact the development team

## Performance Optimization

### Development Tips

1. **Use Release Builds** for performance testing
2. **Monitor Memory Usage** during Python interop
3. **Profile Application** to identify bottlenecks
4. **Use Async/Await** for I/O operations
5. **Enable Hot Reload** for faster development

### Debugging

1. **Enable Debug Logging** in development
2. **Use Visual Studio Debugger** for C# code
3. **Use Python Debugger** for Python modules
4. **Monitor Performance Counters**
5. **Use Application Insights** for telemetry

### Memory Management

```bash
# Monitor memory usage
dotnet-counters monitor --process-id <PID>

# Profile memory allocation
dotnet-trace collect --process-id <PID>

# Analyze memory dumps
dotnet-dump collect --process-id <PID>
```

## Security Considerations

### Development Security

1. **Secrets Management**
   - Use User Secrets for development
   - Never commit sensitive data
   - Use Azure Key Vault for production

2. **Package Security**
   - Regularly update packages
   - Check for vulnerabilities
   - Use trusted package sources

3. **Code Security**
   - Enable security analyzers
   - Follow secure coding practices
   - Regular security reviews

### Security Tools

```bash
# Check for package vulnerabilities
dotnet list package --vulnerable

# Run security analysis
dotnet list package --outdated

# Audit dependencies
dotnet audit
```

## Continuous Integration

### GitHub Actions Example

```yaml
name: Build and Test

on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET 10
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'
    
    - name: Setup Python
      uses: actions/setup-python@v4
      with:
        python-version: '3.9'
    
    - name: Install dependencies
      run: |
        pip install -r Prisma/Code/Src/ocr_modules/requirements.txt
        pip install pythonnet numpy pillow
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore --verbosity normal
    
    - name: Test
      run: dotnet test --no-build --verbosity normal --collect:"XPlat Code Coverage"
    
    - name: Upload coverage
      uses: codecov/codecov-action@v3
      with:
        file: ./coverage/**/coverage.cobertura.xml
```

## Best Practices

### Development Workflow

1. **Feature Branches**: Always work on feature branches
2. **Small Commits**: Make small, focused commits
3. **Pull Requests**: Use pull requests for code review
4. **Continuous Integration**: Ensure CI passes before merging
5. **Documentation**: Update documentation with code changes

### Code Quality

1. **Warnings as Errors**: Treat all warnings as errors
2. **Code Coverage**: Maintain 80%+ test coverage
3. **Code Review**: All code must be reviewed
4. **Static Analysis**: Use built-in analyzers
5. **Performance**: Monitor and optimize performance

### Testing

1. **Unit Tests**: Test individual components
2. **Integration Tests**: Test component interactions
3. **End-to-End Tests**: Test complete workflows
4. **Performance Tests**: Test performance requirements
5. **Security Tests**: Test security requirements
