# SIARA Simulator Configuration Guide

## Overview

The SIARA Simulator uses **IOptions pattern** for configuration management. All settings can be configured via `appsettings.json` without code changes.

## Configuration Settings

### SimulatorSettings Section

Located in `appsettings.json`:

```json
"SimulatorSettings": {
  "DocumentSourcePath": "../bulk_generated_documents_all_formats",
  "PersistenceFilePath": "cases.json",
  "AverageArrivalsPerMinute": 6.0,
  "ResetCasesOnStartup": false
}
```

### Configuration Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DocumentSourcePath` | string | `"../bulk_generated_documents_all_formats"` | Path to directory containing case documents. Supports both absolute and relative paths. |
| `PersistenceFilePath` | string | `"cases.json"` | Path to JSON file tracking served case IDs. Supports both absolute and relative paths. |
| `AverageArrivalsPerMinute` | double | `6.0` | Average case arrival rate (0.1 - 60 cases/minute). Uses Poisson distribution. |
| `ResetCasesOnStartup` | bool | `false` | If `true`, clears served cases history on startup, making all cases available again. |

## Case Tracking Mechanism

### How It Works

1. **Persistence File** (`cases.json`):
   - Stores a JSON array of served case IDs
   - Format: `["FGR-2023-779693_20251123_184840", "FGR-2023-779694_20251123_184841", ...]`
   - Updated each time a new case arrives

2. **Case Resolution**:
   - Cases are randomly selected from available (not yet served) cases
   - Once served, case ID is added to persistence file
   - On app restart, previously served cases are loaded and excluded

3. **Reset Behavior**:
   - Set `ResetCasesOnStartup: true` to start fresh
   - Deletes persistence file and makes all cases available again
   - Useful for testing and demos

## Publishing as Executable

### Option 1: Self-Contained Executable (Recommended)

Publish as a single, self-contained EXE that includes .NET runtime:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

**Output**: `bin\Release\net10.0\win-x64\publish\Siara.Simulator.exe`

**Advantages**:
- No .NET runtime installation required on target machine
- Single EXE file (easier distribution)
- Includes all dependencies

### Option 2: Framework-Dependent Executable

Requires .NET 10 runtime on target machine:

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

**Advantages**:
- Smaller file size
- Faster startup time

### Option 3: Cross-Platform Publishing

**Windows**:
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

**Linux**:
```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

**macOS**:
```bash
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true
```

## Deployment Configuration

### Production Setup

1. **Copy Files to Target Location**:
   ```
   C:\SiaraSimulator\
   ├── Siara.Simulator.exe
   ├── appsettings.json              (copied automatically)
   ├── appsettings.Production.json   (environment-specific)
   └── wwwroot\                      (static files)
   ```

2. **Create Data Directory**:
   ```
   C:\SiaraData\
   ├── Documents\                    (case documents)
   ├── logs\                         (application logs)
   └── cases.json                    (persistence file)
   ```

3. **Update appsettings.Production.json**:
   ```json
   {
     "SimulatorSettings": {
       "DocumentSourcePath": "C:\\SiaraData\\Documents",
       "PersistenceFilePath": "C:\\SiaraData\\cases.json",
       "AverageArrivalsPerMinute": 3.0,
       "ResetCasesOnStartup": false
     }
   }
   ```

4. **Run with Production Configuration**:
   ```bash
   set ASPNETCORE_ENVIRONMENT=Production
   Siara.Simulator.exe
   ```

### Environment-Specific Configuration

The app supports multiple environment configurations:

- `appsettings.json` - Base configuration (all environments)
- `appsettings.Development.json` - Development overrides
- `appsettings.Production.json` - Production overrides

Set environment variable to switch:
```bash
# Windows
set ASPNETCORE_ENVIRONMENT=Production

# Linux/Mac
export ASPNETCORE_ENVIRONMENT=Production
```

## Testing Scenarios

### Scenario 1: Demo with Reset Every Run

**Configuration**:
```json
{
  "SimulatorSettings": {
    "ResetCasesOnStartup": true,
    "AverageArrivalsPerMinute": 10.0
  }
}
```

**Use Case**: Demos, presentations, trade shows

### Scenario 2: Persistent Testing

**Configuration**:
```json
{
  "SimulatorSettings": {
    "ResetCasesOnStartup": false,
    "AverageArrivalsPerMinute": 6.0
  }
}
```

**Use Case**: Long-running tests, QA validation

### Scenario 3: Stress Testing

**Configuration**:
```json
{
  "SimulatorSettings": {
    "ResetCasesOnStartup": true,
    "AverageArrivalsPerMinute": 60.0
  }
}
```

**Use Case**: Performance testing, load testing

## Monitoring

### Log Files

Logs are written to:
- **Console**: Real-time output
- **File**: `logs/siara-{Date}.log` (configurable in appsettings.json)
- **Seq** (Optional): Structured logging server at `http://localhost:5341`

### Key Log Messages

```
[INF] CaseService created with DocumentSourcePath: {...}, PersistenceFile: {...}
[INF] Initial arrival rate: 6.0 cases/minute
[INF] Loaded 125 served case IDs from persistence
[INF] Publishing new case arrival to CaseArrived observable - Case ID: FGR-2023-779693
[INF] Dashboard received new case notification - Case ID: FGR-2023-779693
```

## Troubleshooting

### Issue: "No more available cases to serve"

**Solution**: All cases have been served. Either:
1. Set `ResetCasesOnStartup: true` and restart
2. Delete `cases.json` manually
3. Add more case documents to `DocumentSourcePath`

### Issue: "Document source directory not found"

**Solution**: Verify `DocumentSourcePath` in appsettings.json:
- Check path is correct
- Use absolute path if relative path doesn't resolve
- Ensure documents exist in specified directory

### Issue: Cases not appearing in UI

**Solution**:
1. Check logs for "Publishing new case arrival" messages
2. Verify `@rendermode InteractiveServer` is set on Dashboard
3. Ensure Observable subscriptions are active

## Advanced Configuration

### Custom Arrival Rate Formula

The simulator uses **Poisson distribution** for realistic case arrivals:

```csharp
// DistributionService.cs
var lambda = averageArrivalsPerMinute / 60.0;  // Convert to per-second rate
var delay = -Math.Log(1.0 - random) / lambda;  // Exponential distribution
```

To customize, modify `AverageArrivalsPerMinute` in appsettings.json.

### Multiple Test Profiles

Create separate appsettings files:

```bash
appsettings.Demo.json      # High arrival rate, reset on startup
appsettings.Testing.json   # Moderate rate, persistent
appsettings.Stress.json    # Maximum rate, reset on startup
```

Run with specific profile:
```bash
set ASPNETCORE_ENVIRONMENT=Demo
Siara.Simulator.exe
```

## Summary

✅ **Configuration via appsettings.json** - No code changes needed
✅ **IOptions pattern** - Type-safe, validated configuration
✅ **Flexible paths** - Absolute or relative path support
✅ **Case tracking** - Persistent served cases history
✅ **Environment-specific** - Development, Production, Custom profiles
✅ **Single EXE publishing** - Easy deployment

For questions or issues, check logs at `logs/siara-{Date}.log`.
