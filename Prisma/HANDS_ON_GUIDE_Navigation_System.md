# Hands-On Guide: Navigation System & Web UI Setup

**Last Updated**: 2025-01-24
**Branch**: `kat`
**Commit**: `2c7e5d9`
**Prerequisites**: .NET 10.0, SQL Server or LocalDB, Node.js (for Playwright)

---

## Table of Contents

1. [Quick Start](#1-quick-start)
2. [Navigation System Setup](#2-navigation-system-setup)
3. [SIARA Simulator Setup](#3-siara-simulator-setup)
4. [Web UI Configuration](#4-web-ui-configuration)
5. [Database Migration](#5-database-migration)
6. [Running the Application](#6-running-the-application)
7. [Testing Navigation](#7-testing-navigation)
8. [Troubleshooting](#8-troubleshooting)

---

## 1. Quick Start

### For the Impatient Developer

```bash
# Clone and navigate to project
cd F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma

# Checkout the branch
git checkout kat

# Build everything
cd Prisma/Code/Src/CSharp
dotnet restore
dotnet build

# Run SIARA Simulator (Terminal 1)
cd ../../../../Siara.Simulator
dotnet run

# Run Web UI (Terminal 2) - AFTER fixing SQL Server trigger issue
cd ../Prisma/Code/Src/CSharp/UI/ExxerCube.Prisma.Web.UI
dotnet ef database update --context ApplicationDbContext
dotnet ef database update --context PrismaDbContext
dotnet run

# Open browser
# SIARA: https://localhost:5002
# Web UI: https://localhost:5001
```

---

## 2. Navigation System Setup

### 2.1 Understanding the Architecture

The navigation system follows **Hexagonal Architecture**:

```
Domain (Interfaces)
  └─ INavigationTarget.cs          [Contract definition]
       ↑
Infrastructure.BrowserAutomation (Implementations)
  ├─ SiaraNavigationTarget.cs      [CNBV simulator]
  ├─ InternetArchiveNavigationTarget.cs
  └─ GutenbergNavigationTarget.cs
```

### 2.2 Adding a New Navigation Target

**Step 1**: Create interface implementation in `Infrastructure.BrowserAutomation/NavigationTargets/`:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Interfaces.Navigation;
using IndQuestResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.NavigationTargets;

public class MyCustomNavigationTarget : INavigationTarget
{
    private readonly ILogger<MyCustomNavigationTarget> _logger;
    private readonly NavigationTargetOptions _options;

    public MyCustomNavigationTarget(
        ILogger<MyCustomNavigationTarget> logger,
        IOptions<NavigationTargetOptions> options)
    {
        _logger = logger;
        _options = options.Value;  // Important: use .Value!
    }

    public string Id => "my-custom";
    public string DisplayName => "My Custom Source";
    public string Description => "Custom document source description";
    public string BaseUrl => _options.MyCustomUrl ?? "https://example.com";

    public async Task<Result> NavigateAsync(IBrowserAutomationAgent agent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Navigating to {DisplayName} at {Url}", DisplayName, BaseUrl);

        var launchResult = await agent.LaunchBrowserAsync(cancellationToken);
        if (!launchResult.IsSuccess) return launchResult;

        var navigateResult = await agent.NavigateToAsync(BaseUrl, cancellationToken);
        if (!navigateResult.IsSuccess)
        {
            await agent.CloseBrowserAsync(cancellationToken);
            return navigateResult;
        }

        return Result.Success();
    }

    public async Task<Result<List<DownloadableFile>>> RetrieveDocumentsAsync(
        IBrowserAutomationAgent agent,
        CancellationToken cancellationToken = default)
    {
        var filePatterns = new[] { "*.pdf", "*.xml", "*.docx" };
        return await agent.IdentifyDownloadableFilesAsync(filePatterns, cancellationToken);
    }
}
```

**Step 2**: Add property to `NavigationTargetOptions.cs`:

```csharp
public class NavigationTargetOptions
{
    public string? SiaraUrl { get; set; }
    public string? ArchiveUrl { get; set; }
    public string? GutenbergUrl { get; set; }
    public string? MyCustomUrl { get; set; }  // Add this
}
```

**Step 3**: Register in `ServiceCollectionExtensions.cs`:

```csharp
services.AddKeyedScoped<INavigationTarget, MyCustomNavigationTarget>("my-custom");
```

**Step 4**: Add configuration to `appsettings.json`:

```json
"NavigationTargets": {
  "SiaraUrl": "https://localhost:5002",
  "ArchiveUrl": "https://archive.org",
  "GutenbergUrl": "https://www.gutenberg.org",
  "MyCustomUrl": "https://example.com"
}
```

**Step 5**: Add button to `Home.razor`:

```razor
<MudItem xs="12" md="4">
    <MudCard Elevation="3" Class="h-100">
        <MudCardHeader>
            <MudText Typo="Typo.h6">
                <MudIcon Icon="@Icons.Material.Filled.Source" Class="mr-2" />
                My Custom Source
            </MudText>
        </MudCardHeader>
        <MudCardContent>
            <MudText Typo="Typo.body2" Class="mb-2">
                Custom document source description
            </MudText>
            <MudText Typo="Typo.caption" Color="Color.Secondary">
                Additional context or features
            </MudText>
        </MudCardContent>
        <MudCardActions>
            <MudButton Variant="Variant.Outlined" Color="Color.Primary"
                       OnClick="@(() => NavigateToSource("my-custom"))"
                       StartIcon="@Icons.Material.Filled.Launch">
                Open Source
            </MudButton>
        </MudCardActions>
    </MudCard>
</MudItem>
```

Update the `NavigateToSource` method:

```csharp
private void NavigateToSource(string sourceId)
{
    var options = NavigationTargetOptions.Value;
    var url = sourceId.ToLower() switch
    {
        "siara" => options.SiaraUrl ?? "https://localhost:5002",
        "archive" => options.ArchiveUrl ?? "https://archive.org",
        "gutenberg" => options.GutenbergUrl ?? "https://www.gutenberg.org",
        "my-custom" => options.MyCustomUrl ?? "https://example.com",
        _ => null
    };

    if (!string.IsNullOrEmpty(url))
    {
        Snackbar.Add($"Opening {sourceId.ToUpper()} in new window...", Severity.Info);
        NavigationManager.NavigateTo(url, forceLoad: true);
    }
}
```

---

## 3. SIARA Simulator Setup

### 3.1 First Time Setup

```bash
cd F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Siara.Simulator

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run
dotnet run
```

**Expected Output**:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5002
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

### 3.2 Using the Simulator

1. **Open browser**: Navigate to `https://localhost:5002`

2. **Dashboard Overview**:
   - **Tasa de Llegada** slider: Adjust arrival rate (0.1 - 60 cases/minute)
   - **Active Cases**: Real-time list of pending cases
   - **Statistics**: Total arrived, active, processed

3. **Adjusting Arrival Rate**:
   ```
   Slower (0.1 cases/min) ←──[======o========]──→ Faster (60 cases/min)
                                     ↑
                              Current: 6.0
   ```

4. **Case Lifecycle**:
   - Case arrives (Poisson distribution)
   - Displays in "Casos Activos" table
   - Shows Folio, Entidad, Tipo Requerimiento, Fecha Límite
   - Download links for PDF, DOCX, XML, HTML

### 3.3 Configuration

**Location**: `Siara.Simulator/Services/CaseService.cs`

```csharp
// Adjust default arrival rate
public double AverageArrivalsPerMinute
{
    get => _averageArrivalsPerMinute;
    set
    {
        // Valid range: 0.1 - 60 cases/minute
        _averageArrivalsPerMinute = Math.Clamp(value, 0.1, 60);
        OnSettingsChanged?.Invoke();
    }
}
```

**Document Source**: `Siara.Simulator/bulk_generated_documents_all_formats/`
- 500 case directories (CASE_001 - CASE_500)
- Each contains: PDF, DOCX, XML, HTML

---

## 4. Web UI Configuration

### 4.1 appsettings.json Setup

**Location**: `UI/ExxerCube.Prisma.Web.UI/appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ExxerCube.Prisma;Trusted_Connection=true;MultipleActiveResultSets=true",
    "PrismaDbContext": "Server=(localdb)\\mssqllocaldb;Database=ExxerCube.Prisma;Trusted_Connection=true;MultipleActiveResultSets=true"
  },
  "BrowserAutomation": {
    "Headless": true,
    "BrowserLaunchTimeoutMs": 30000,
    "PageTimeoutMs": 30000,
    "DefaultWebsiteUrl": "https://www.gob.mx/uif",
    "FilePatterns": [ "*.pdf", "*.xml", "*.docx" ]
  },
  "NavigationTargets": {
    "SiaraUrl": "https://localhost:5002",
    "ArchiveUrl": "https://archive.org",
    "GutenbergUrl": "https://www.gutenberg.org"
  },
  "PythonConfiguration": {
    "MaxConcurrency": 5,
    "OperationTimeoutSeconds": 30,
    "EnableDebugging": true
  }
}
```

### 4.2 Development vs Production Configuration

**appsettings.Development.json**:
```json
{
  "NavigationTargets": {
    "SiaraUrl": "https://localhost:5002"
  },
  "PythonConfiguration": {
    "EnableDebugging": true
  }
}
```

**appsettings.Production.json**:
```json
{
  "NavigationTargets": {
    "SiaraUrl": "https://siara.internal.company.com"
  },
  "PythonConfiguration": {
    "EnableDebugging": false
  }
}
```

---

## 5. Database Migration

### 5.1 SQL Server Logon Trigger Issue

**Problem**: If you encounter this error:
```
Error Number:17892 - Logon failed for login due to trigger execution.
```

**Solution Options**:

#### Option 1: Disable Trigger (Temporary)
```sql
-- Connect to SQL Server via SSMS
USE master;
GO

-- List all server-level triggers
SELECT * FROM sys.server_triggers;

-- Disable the blocking trigger
DISABLE TRIGGER [YourTriggerName] ON ALL SERVER;
```

#### Option 2: Use LocalDB (Recommended for Development)
```json
// Update appsettings.json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ExxerCube.Prisma;Trusted_Connection=true",
  "PrismaDbContext": "Server=(localdb)\\mssqllocaldb;Database=ExxerCube.Prisma;Trusted_Connection=true"
}
```

#### Option 3: SQL Authentication
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=ExxerCube.Prisma;User Id=sa;Password=YourPassword;TrustServerCertificate=true",
  "PrismaDbContext": "Server=localhost;Database=ExxerCube.Prisma;User Id=sa;Password=YourPassword;TrustServerCertificate=true"
}
```

### 5.2 Applying Migrations

**Step 1**: Navigate to Web UI project
```bash
cd F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp\UI\ExxerCube.Prisma.Web.UI
```

**Step 2**: Apply ApplicationDbContext migrations (Identity)
```bash
dotnet ef database update --context ApplicationDbContext
```

**Expected Output**:
```
Build started...
Build succeeded.
Applying migration '20241120_CreateIdentitySchema'.
Done.
```

**Step 3**: Apply PrismaDbContext migrations (Domain)
```bash
dotnet ef database update --context PrismaDbContext
```

**Expected Output**:
```
Build started...
Build succeeded.
Applying migration '20241115_InitialCreate'.
Applying migration '20241118_AddFileMetadata'.
Done.
```

### 5.3 Verifying Migrations

**SQL Server Management Studio**:
```sql
USE ExxerCube.Prisma;
GO

-- Check Identity tables
SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME LIKE 'AspNet%';

-- Check Domain tables
SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo';
```

**Expected Tables**:
- `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles` (Identity)
- `FileMetadata`, `Documents`, `ReviewCases`, etc. (Domain)

---

## 6. Running the Application

### 6.1 Multi-Terminal Setup

**Terminal 1 - SIARA Simulator**:
```bash
cd F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Siara.Simulator
dotnet run
```
Wait for: `Now listening on: https://localhost:5002`

**Terminal 2 - Web UI**:
```bash
cd F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp\UI\ExxerCube.Prisma.Web.UI
dotnet run
```
Wait for: `Now listening on: https://localhost:5001`

### 6.2 Opening in Browser

1. **Web UI Home**: https://localhost:5001
2. **SIARA Dashboard**: https://localhost:5002
3. **Swagger API** (if enabled): https://localhost:5001/swagger

### 6.3 Expected Startup Logs

**Web UI**:
```
[21:00:00 INF] Starting ExxerCube.Prisma.Web.UI application
[21:00:01 INF] Registered navigation target: siara
[21:00:01 INF] Registered navigation target: archive
[21:00:01 INF] Registered navigation target: gutenberg
[21:00:02 INF] Python environment initialized
[21:00:02 INF] Application started. Press Ctrl+C to shut down.
```

**SIARA Simulator**:
```
[21:00:05 INF] Starting SIARA Simulator
[21:00:05 INF] Loaded 500 case templates
[21:00:05 INF] Arrival rate: 6.0 cases/minute
[21:00:06 INF] Application started. Press Ctrl+C to shut down.
```

---

## 7. Testing Navigation

### 7.1 Testing SIARA Navigation

1. Open Web UI: https://localhost:5001
2. Scroll to "Document Sources" section
3. Click "Open SIARA" button on the first card
4. **Expected**: New browser tab opens to https://localhost:5002
5. **Verify**: SIARA dashboard displays with active cases

### 7.2 Testing Arrival Rate Adjustment

1. On SIARA dashboard, locate "Tasa de Llegada" slider
2. Drag slider to different values (e.g., 12 cases/minute)
3. **Expected**: Console log shows: `Arrival rate changed to 12.0 cases/minute`
4. **Verify**: Cases arrive faster/slower based on setting

### 7.3 Testing Internet Archive Navigation

1. On Web UI home, click "Open Archive" button
2. **Expected**: New tab opens to https://archive.org
3. **Verify**: Internet Archive homepage loads

### 7.4 Testing Project Gutenberg Navigation

1. On Web UI home, click "Open Gutenberg" button
2. **Expected**: New tab opens to https://www.gutenberg.org
3. **Verify**: Project Gutenberg homepage loads

### 7.5 Automated Testing

**Run E2E Tests**:
```bash
cd F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp\Tests.UI
dotnet test --filter "FullyQualifiedName~NavigationSmokeTests"
```

**Expected Output**:
```
Passed! - Failed:     0, Passed:     3, Skipped:     0, Total:     3
```

---

## 8. Troubleshooting

### 8.1 Build Errors

#### Error: "Cannot find type INavigationTarget"

**Cause**: Missing using directive

**Fix**:
```csharp
using ExxerCube.Prisma.Domain.Interfaces.Navigation;
```

#### Error: "'IServiceCollection' does not contain a definition for 'AddPrismaPythonEnvironment'"

**Cause**: Missing namespace import

**Fix** (in `Program.cs`):
```csharp
using ExxerCube.Prisma.Infrastructure.Python;
using ExxerCube.Prisma.Infrastructure.Metrics;
```

### 8.2 Runtime Errors

#### Error: "Unable to resolve service for type 'INavigationTarget'"

**Cause**: Navigation targets not registered in DI

**Fix**: Check `ServiceCollectionExtensions.cs`:
```csharp
services.AddKeyedScoped<INavigationTarget, SiaraNavigationTarget>("siara");
services.AddKeyedScoped<INavigationTarget, InternetArchiveNavigationTarget>("archive");
services.AddKeyedScoped<INavigationTarget, GutenbergNavigationTarget>("gutenberg");
```

#### Error: "Configuration section 'NavigationTargets' not found"

**Cause**: Missing configuration in `appsettings.json`

**Fix**: Add to `appsettings.json`:
```json
"NavigationTargets": {
  "SiaraUrl": "https://localhost:5002",
  "ArchiveUrl": "https://archive.org",
  "GutenbergUrl": "https://www.gutenberg.org"
}
```

### 8.3 SIARA Simulator Issues

#### Error: "Port 5002 is already in use"

**Cause**: Another process using the port

**Fix**:
```bash
# Find process using port 5002
netstat -ano | findstr :5002

# Kill process (replace PID)
taskkill /PID <PID> /F

# Or change port in launchSettings.json
```

#### Issue: No cases appearing

**Cause**: Arrival rate too low or case data missing

**Fix**:
1. Increase arrival rate slider to 12-60 cases/minute
2. Verify `bulk_generated_documents_all_formats/` directory exists
3. Check console for errors

### 8.4 Database Issues

#### Error: "Database does not exist"

**Cause**: Migrations not applied

**Fix**:
```bash
dotnet ef database update --context ApplicationDbContext
dotnet ef database update --context PrismaDbContext
```

#### Error: "Logon failed for login due to trigger execution"

**Cause**: SQL Server logon trigger

**Fix**: See Section 5.1 - SQL Server Logon Trigger Issue

### 8.5 Python Environment Issues

#### Error: "Unable to resolve service for type 'IPythonEnvironment'"

**Cause**: Missing `AddPrismaPythonEnvironment()` registration

**Fix** (in `Program.cs`):
```csharp
services.AddPrismaPythonEnvironment();
```

#### Error: "Python executable not found"

**Cause**: Python not installed or not in PATH

**Fix**:
1. Install Python 3.10+
2. Add Python to PATH
3. Update `appsettings.json`:
   ```json
   "PythonConfiguration": {
     "PythonExecutablePath": "C:\\Python310\\python.exe"
   }
   ```

---

## 9. Quick Reference

### Port Assignments
- **Web UI**: https://localhost:5001
- **SIARA Simulator**: https://localhost:5002
- **SQL Server**: localhost:1433
- **LocalDB**: (localdb)\mssqllocaldb

### Key Configuration Files
- `UI/Web.UI/appsettings.json` - Web UI configuration
- `Siara.Simulator/appsettings.json` - SIARA configuration
- `UI/Web.UI/launchSettings.json` - Development ports

### Important Commands

```bash
# Build entire solution
dotnet build

# Run Web UI
cd UI/ExxerCube.Prisma.Web.UI && dotnet run

# Run SIARA
cd Siara.Simulator && dotnet run

# Apply migrations
dotnet ef database update --context ApplicationDbContext
dotnet ef database update --context PrismaDbContext

# Run tests
dotnet test

# Clean build
dotnet clean && dotnet build
```

### Log Locations
- **Web UI**: Console output + `logs/` directory (if configured)
- **SIARA**: Console output
- **SQL Server**: SQL Server Management Studio → Management → SQL Server Logs

---

## 10. Next Steps

After completing this guide, you should be able to:
- ✅ Run SIARA Simulator with configurable arrival rates
- ✅ Navigate to document sources from Web UI
- ✅ Understand navigation system architecture
- ✅ Add new navigation targets
- ✅ Troubleshoot common issues

**Recommended Next Steps**:
1. Review `LESSONS_LEARNED_2025-01-24_Navigation_And_DI.md` for architectural insights
2. Explore `ClosingInitiativeMvp.md` for MVP objectives
3. Review GOT-OCR2 integration documentation
4. Set up CI/CD pipeline for automated testing

---

**End of Hands-On Guide**

*Last Updated: 2025-01-24*
*For questions or issues, refer to Troubleshooting section or check commit history*
