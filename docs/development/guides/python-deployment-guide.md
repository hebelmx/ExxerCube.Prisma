# Deployment Guide

## Overview

This guide provides step-by-step instructions for deploying the ExxerCube OCR Pipeline C# application to various environments using .NET 10 and modern deployment practices.

## Prerequisites

### Production Environment Requirements

1. **Operating System**
   - Windows Server 2019/2022
   - Ubuntu 20.04 LTS or later
   - CentOS 8 or later

2. **Runtime Dependencies**
   - .NET 10.0 Runtime
   - Python 3.9+
   - Tesseract OCR 4.1+

3. **System Resources**
   - **CPU**: 4+ cores recommended
   - **RAM**: 8GB minimum, 16GB recommended
   - **Storage**: 50GB+ available space
   - **Network**: Stable internet connection for package downloads

## Deployment Options

### Option 1: Self-Hosted Application

#### Windows Deployment

1. **Install Dependencies**

```powershell
# Install .NET 10.0 Runtime
winget install Microsoft.DotNet.Runtime.10

# Install Python 3.9+
winget install Python.Python.3.9

# Install Tesseract OCR
# Download from: https://github.com/UB-Mannheim/tesseract/wiki
```

2. **Install Python Dependencies**

```bash
# Navigate to Python modules directory
cd Prisma/Code/Src/ocr_modules

# Install dependencies
pip install -r requirements.txt
pip install pythonnet numpy pillow
```

3. **Deploy Application**

```bash
# Publish application with .NET 10
dotnet publish -c Release -r win-x64 --self-contained -o ./publish

# Copy to deployment directory
xcopy ./publish C:\ExxerCube\OcrPipeline\ /E /I /Y
```

4. **Configure Environment**

```powershell
# Set environment variables
[Environment]::SetEnvironmentVariable("PYTHONPATH", "C:\ExxerCube\OcrPipeline\ocr_modules", "Machine")
[Environment]::SetEnvironmentVariable("TESSERACT_PATH", "C:\Program Files\Tesseract-OCR\tesseract.exe", "Machine")
[Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production", "Machine")
```

#### Linux Deployment

1. **Install Dependencies**

```bash
# Install .NET 10.0 Runtime
wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-runtime-10.0

# Install Python 3.9+
sudo apt-get install -y python3.9 python3.9-pip

# Install Tesseract OCR
sudo apt-get install -y tesseract-ocr tesseract-ocr-spa
```

2. **Install Python Dependencies**

```bash
# Navigate to Python modules directory
cd Prisma/Code/Src/ocr_modules

# Install dependencies
pip3 install -r requirements.txt
pip3 install pythonnet numpy pillow
```

3. **Deploy Application**

```bash
# Publish application with .NET 10
dotnet publish -c Release -r linux-x64 --self-contained -o ./publish

# Copy to deployment directory
sudo mkdir -p /opt/exxercube/ocr-pipeline
sudo cp -r ./publish/* /opt/exxercube/ocr-pipeline/
```

4. **Configure Environment**

```bash
# Set environment variables
echo 'export PYTHONPATH="/opt/exxercube/ocr-pipeline/ocr_modules"' | sudo tee -a /etc/environment
echo 'export TESSERACT_PATH="/usr/bin/tesseract"' | sudo tee -a /etc/environment
echo 'export ASPNETCORE_ENVIRONMENT=Production' | sudo tee -a /etc/environment
```

### Option 2: Docker Container

#### Dockerfile

```dockerfile
# Use .NET 10.0 runtime image
FROM mcr.microsoft.com/dotnet/runtime:10.0

# Install Python and dependencies
RUN apt-get update && apt-get install -y \
    python3.9 \
    python3.9-pip \
    tesseract-ocr \
    tesseract-ocr-spa \
    && rm -rf /var/lib/apt/lists/*

# Set working directory
WORKDIR /app

# Copy Python modules
COPY Prisma/Code/Src/ocr_modules ./ocr_modules

# Install Python dependencies
RUN pip3 install -r ocr_modules/requirements.txt
RUN pip3 install pythonnet numpy pillow

# Copy application
COPY publish .

# Set environment variables
ENV PYTHONPATH=/app/ocr_modules
ENV TESSERACT_PATH=/usr/bin/tesseract
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port (if needed)
EXPOSE 5000

# Run application
ENTRYPOINT ["dotnet", "ExxerCube.Ocr.Console.dll"]
```

#### Docker Compose

```yaml
version: '3.8'

services:
  ocr-pipeline:
    build: .
    ports:
      - "5000:5000"
    volumes:
      - ./input:/app/input
      - ./output:/app/output
    environment:
      - PYTHONPATH=/app/ocr_modules
      - TESSERACT_PATH=/usr/bin/tesseract
      - ASPNETCORE_ENVIRONMENT=Production
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "dotnet", "ExxerCube.Ocr.Console.dll", "--health-check"]
      interval: 30s
      timeout: 10s
      retries: 3
```

### Option 3: Azure App Service

#### Azure Deployment Script

```bash
# Install Azure CLI
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# Login to Azure
az login

# Create resource group
az group create --name ExxerCubeOcr --location EastUS

# Create App Service plan
az appservice plan create --name ExxerCubeOcrPlan --resource-group ExxerCubeOcr --sku B1 --is-linux

# Create web app
az webapp create --name exxercube-ocr --resource-group ExxerCubeOcr --plan ExxercubeOcrPlan --runtime "DOTNETCORE|10.0"

# Configure environment variables
az webapp config appsettings set --name exxercube-ocr --resource-group ExxerCubeOcr --settings \
  PYTHONPATH="/home/site/wwwroot/ocr_modules" \
  TESSERACT_PATH="/usr/bin/tesseract" \
  ASPNETCORE_ENVIRONMENT=Production

# Deploy application
az webapp deployment source config-zip --resource-group ExxerCubeOcr --name exxercube-ocr --src ./publish.zip
```

## Configuration Management

### Application Settings

#### appsettings.Production.json

```json
{
  "OcrProcessing": {
    "DefaultLanguage": "spa",
    "FallbackLanguage": "eng",
    "MaxConcurrency": 10,
    "TimeoutSeconds": 600,
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
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ExxerCubeOcr;Trusted_Connection=true;"
  }
}
```

#### Environment Variables

```bash
# Production environment variables
export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS=http://localhost:5000
export PYTHONPATH=/app/ocr_modules
export TESSERACT_PATH=/usr/bin/tesseract
export LOG_LEVEL=Information
export TELEMETRY_ENABLED=true
```

## Monitoring and Logging

### Application Insights Integration

```csharp
// Program.cs
builder.Services.AddApplicationInsightsTelemetry();

// Configure logging
builder.Logging.AddApplicationInsights();
```

### Health Checks

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddCheck("ocr-service", () => HealthCheckResult.Healthy())
    .AddCheck("python-modules", () => CheckPythonModules())
    .AddCheck("tesseract", () => CheckTesseract());
```

### Logging Configuration

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    },
    "ApplicationInsights": {
      "LogLevel": {
        "Default": "Information"
      }
    }
  }
}
```

## Performance Optimization

### Production Optimizations

1. **Memory Management**
   - Configure garbage collection settings
   - Monitor memory usage
   - Implement proper disposal patterns

2. **Concurrency Settings**
   - Adjust MaxConcurrency based on system resources
   - Monitor thread pool usage
   - Implement connection pooling

3. **Caching Strategy**
   - Implement result caching
   - Use distributed caching for multi-instance deployments
   - Configure cache expiration policies

### Performance Monitoring

```csharp
// Performance counters
public class PerformanceMetrics
{
    public static readonly Counter ProcessedDocuments = Metrics.CreateCounter("processed_documents_total", "Total documents processed");
    public static readonly Histogram ProcessingTime = Metrics.CreateHistogram("processing_time_seconds", "Document processing time");
    public static readonly Gauge ActiveProcessing = Metrics.CreateGauge("active_processing", "Currently processing documents");
}
```

## Security Considerations

### Network Security

1. **Firewall Configuration**
   - Restrict access to necessary ports only
   - Implement network segmentation
   - Use VPN for remote access

2. **SSL/TLS Configuration**
   - Enable HTTPS for all communications
   - Use strong cipher suites
   - Implement certificate management

### Application Security

1. **Input Validation**
   - Validate all file inputs
   - Implement file type restrictions
   - Sanitize file paths

2. **Access Control**
   - Implement authentication and authorization
   - Use role-based access control
   - Audit all operations

## Backup and Recovery

### Data Backup Strategy

1. **Configuration Backup**
   - Backup application settings
   - Version control configuration files
   - Document configuration changes

2. **Processing Results Backup**
   - Implement automated backup of results
   - Use cloud storage for redundancy
   - Test backup restoration procedures

### Disaster Recovery

1. **Recovery Procedures**
   - Document recovery steps
   - Test recovery procedures regularly
   - Maintain recovery runbooks

2. **High Availability**
   - Implement load balancing
   - Use multiple instances
   - Configure auto-scaling

## Troubleshooting

### Common Issues

1. **Python Module Import Errors**
   - Verify PYTHONPATH environment variable
   - Check Python module installation
   - Validate file permissions

2. **Tesseract OCR Issues**
   - Verify Tesseract installation
   - Check language pack installation
   - Validate TESSERACT_PATH environment variable

3. **Performance Issues**
   - Monitor system resources
   - Check concurrency settings
   - Review logging for bottlenecks

4. **.NET 10 Issues**
   - Verify .NET 10 runtime installation
   - Check for compatibility issues
   - Update to latest patches

### Diagnostic Tools

```bash
# Check system resources
htop
df -h
free -h

# Check application logs
journalctl -u exxercube-ocr -f

# Test Python integration
python3 -c "import ocr_modules; print('Python modules loaded successfully')"

# Test Tesseract
tesseract --version
tesseract --list-langs

# Check .NET runtime
dotnet --version
dotnet --info
```

## Maintenance

### Regular Maintenance Tasks

1. **System Updates**
   - Update .NET 10 runtime
   - Update Python packages
   - Update Tesseract OCR

2. **Log Management**
   - Rotate log files
   - Archive old logs
   - Monitor log sizes

3. **Performance Monitoring**
   - Review performance metrics
   - Optimize configuration
   - Update monitoring alerts

### Update Procedures

1. **Application Updates**
   - Test updates in staging environment
   - Plan maintenance windows
   - Implement rollback procedures

2. **Dependency Updates**
   - Monitor security advisories
   - Test compatibility
   - Update gradually

## Support and Documentation

### Support Contacts

- **Development Team**: dev-team@exxercube.com
- **Operations Team**: ops-team@exxercube.com
- **Emergency Contact**: emergency@exxercube.com

### Documentation Resources

- **Architecture Documentation**: `/docs/architecture/`
- **API Reference**: `/docs/api/`
- **Troubleshooting Guide**: `/docs/troubleshooting/`
- **Performance Tuning**: `/docs/performance/`
