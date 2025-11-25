# Siara Simulator - Deployment Package

## Overview
This is a self-contained deployment of the Siara Simulator, used for E2E testing of the main ExxerCube.Prisma project.

## Deployment Location
```
F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Deployments\Siara.Simulator
```

## Directory Structure
```
Siara.Simulator/
├── app/                              # Published application
│   ├── Siara.Simulator.exe          # Self-contained executable (101MB single file)
│   ├── appsettings.json             # Main configuration
│   ├── appsettings.Production.json  # Production configuration
│   ├── appsettings.Development.json # Development configuration
│   ├── wwwroot/                     # Static web assets
│   └── cases.json                   # Case persistence file
├── bulk_generated_documents_all_formats/ # Test documents for simulator
└── README.md                        # This file
```

## Build Configuration
This deployment was created with the following parameters:
- **Configuration**: Release
- **Runtime**: win-x64
- **Self-contained**: true
- **Single File**: true

**Build Command:**
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Running the Simulator

### Quick Start
```bash
cd app
.\Siara.Simulator.exe
```

The simulator will start and listen on:
- HTTP: `http://localhost:5001`
- HTTPS: `https://localhost:5002`

### Configuration
The simulator uses `appsettings.json` by default. Key settings:

**Development Configuration (appsettings.json):**
- **DocumentSourcePath**: `../bulk_generated_documents_all_formats` (relative path to document artifacts)
- **PersistenceFilePath**: `cases.json` (stored in app directory)
- **AverageArrivalsPerMinute**: 6.0
- **ResetCasesOnStartup**: false

**Production Configuration (appsettings.Production.json):**
- **DocumentSourcePath**: `C:\SiaraData\Documents`
- **PersistenceFilePath**: `C:\SiaraData\cases.json`
- **AverageArrivalsPerMinute**: 3.0
- **ResetCasesOnStartup**: false

### Setting Environment
To use production settings:
```bash
$env:ASPNETCORE_ENVIRONMENT="Production"
.\Siara.Simulator.exe
```

## Artifacts Included

### Document Source
The `bulk_generated_documents_all_formats/` folder contains test documents in various formats used by the simulator for generating test cases.

### Logs
Logs are written to:
- Console (all environments)
- File: `logs/siara-.log` (rolling daily, 30 day retention)
- Seq: `http://localhost:5341` (development only)

## E2E Testing Usage

This simulator is designed to be used in E2E tests for the main project. The isolated deployment ensures:
1. No interference with development builds
2. Consistent test environment
3. Easy access to required document artifacts
4. Self-contained runtime (no .NET SDK required)

## Updating the Deployment

To update this deployment:
```bash
cd F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma
dotnet publish Siara.Simulator/Siara.Simulator.sln -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o Deployments/Siara.Simulator/app
```

## Notes
- The executable is self-contained (101MB) and includes the .NET runtime
- No .NET installation required to run
- All dependencies are bundled into the single executable
- Configuration files are still external for easy modification
- Document artifacts are isolated in this deployment folder

## Support
For issues or questions about the simulator, refer to the main project documentation or contact the development team.

---
**Deployment Date:** 2025-11-25
**Build Configuration:** Release | win-x64 | Self-Contained | Single File
