# UI Enhancements Sharding - Handoff Instructions

**Status:** In Progress  
**Created:** 2025-01-15  
**Purpose:** Complete the sharding of UI enhancement documents into individual story files

---

## Current Status

### ✅ Completed Files

1. **`1.1-browser-automation-ui.md`** - ✅ Complete
   - Document Ingestion Dashboard component
   - File Metadata Viewer component
   - Code aligned to project standards
   - SignalR integration via `Dashboard<FileMetadata>`

2. **`SHARDING-APPROACH.md`** - ✅ Complete
   - Overview of sharding approach
   - Code standards alignment guide
   - Testing considerations

3. **`index.md`** - ✅ Complete
   - Navigation index for all enhancement documents

### ⏳ Remaining Files to Create

**Stories 1.2-1.3 (from `docs/qa/ui-enhancements-stories-1.1-1.3-bmad.md`):**
- `1.2-metadata-extraction-ui.md` - Classification Results Display
- `1.3-field-matching-ui.md` - Field Matching Visualization

**Stories 1.4-1.6 (from `docs/qa/ui-enhancements-stories-1.4-1.6-bmad.md`):**
- `1.4-identity-resolution-ui.md` - Identity Resolution Display
- `1.5-sla-tracking-ui.md` - SLA Dashboard (AC5 - Critical)
- `1.6-manual-review-ui.md` - Manual Review Interface Enhancements

**Stories 1.7-1.9 (from `docs/qa/ui-enhancements-stories-1.7-1.9-bmad.md`):**
- `1.7-siro-export-ui.md` - Export Management Dashboard
- `1.8-pdf-signing-ui.md` - PDF Signing Status Display
- `1.9-audit-trail-ui.md` - Audit Trail Viewer (AC3 & AC6)

---

## File Structure Template

Each story file should follow this structure:

```markdown
# UI/UX Enhancements: Story X.X - [Story Name]

**Story:** X.X - [Story Name]  
**Status:** UX Enhancement Recommendations  
**Created:** 2025-01-15  
**Last Updated:** 2025-01-15

---

## Current State Analysis

**Backend Status:** ✅ Complete (QA Approved)  
**UI Status:** ⚠️ **[Status]** - [Description]

**Gaps Identified:**
- [List of gaps]

---

## Required UI Components

### 1. [Component Name]

**Location:** `Components/[Path]/[ComponentName].razor`  
**Purpose:** [Purpose description]

**Features:**

**A. [Feature Section]**
- [Feature details]

**B. [Feature Section]**
- [Feature details]

---

## Implementation Code

### [Component Name] Component

```razor
@page "/[route]"
@using ExxerCube.Prisma.Domain.Entities
@using ExxerCube.Prisma.Application.Services
@using ExxerCube.Prisma.SignalR.Abstractions
@inject [IService] Service
@inject IDashboard<T> DashboardService
@inject ILogger<Component> Logger
@implements IAsyncDisposable

[Component markup]

@code {
    // Code aligned to standards (see below)
}
```

---

## Code Standards Compliance

### ✅ Result<T> Pattern
- All service calls return `Result<T>`
- Error handling uses Railway-Oriented Programming
- No exceptions for control flow

### ✅ CancellationToken Support
- All async methods accept `CancellationToken`
- Proper cancellation handling throughout
- `CancellationTokenSource` properly disposed

### ✅ Structured Logging
- Uses `ILogger<T>` with structured logging
- Logs key operations, errors, and non-normal flows
- Uses appropriate log levels

### ✅ Expression-Bodied Members
- Simple methods use expression-bodied syntax
- Switch expressions for format/status mapping

### ✅ SignalR Integration
- Uses `Dashboard<T>` abstraction from Story 1.10
- Proper connection state management
- Automatic reconnection handling

### ✅ Immutability
- Uses `ReadOnlyCollections` where appropriate
- Prefers `init` properties

---

## Testing Considerations

### Unit Tests
- [Test scenarios]

### Integration Tests
- [Test scenarios]

---

## Related Documentation

- [Story X.X: Story Name](../../stories/X.X.story-name.md)
- [ADR-001: SignalR Unified Hub Abstraction](../../adr/ADR-001-SignalR-Unified-Hub-Abstraction.md)
- [Story 1.10: SignalR Infrastructure](../../stories/1.10.signalr-unified-hub-abstraction.md)
- [Sharding Approach](./SHARDING-APPROACH.md)

---

**Document Status:** ✅ **Complete**  
**Ready for Implementation** 🚀
```

---

## Code Standards Alignment Checklist

When creating each file, ensure:

### 1. Result<T> Pattern
```csharp
// ✅ CORRECT
var result = await Service.GetDataAsync(cancellationToken);
if (result.IsFailure)
{
    Logger.LogWarning("Failed to load data: {Error}", result.Error);
    errorMessage = result.Error;
    return;
}
data = result.Value;

// ❌ WRONG - Don't throw exceptions
try
{
    data = await Service.GetDataAsync(cancellationToken);
}
catch (Exception ex)
{
    // ...
}
```

### 2. CancellationToken Support
```csharp
// ✅ CORRECT
private CancellationTokenSource? cancellationTokenSource;

protected override async Task OnInitializedAsync()
{
    cancellationTokenSource = new CancellationTokenSource();
    await LoadDataAsync(cancellationTokenSource.Token);
}

public async ValueTask DisposeAsync()
{
    if (cancellationTokenSource != null)
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }
}

// ❌ WRONG - Missing cancellation token
protected override async Task OnInitializedAsync()
{
    await LoadDataAsync(); // No cancellation token
}
```

### 3. Structured Logging
```csharp
// ✅ CORRECT
Logger.LogInformation("Loading data for file {FileId}", fileId);
Logger.LogWarning("Operation failed: {Error}", errorMessage);
Logger.LogError(ex, "Unexpected error loading data");

// ❌ WRONG - String interpolation or no structured logging
Logger.LogInformation($"Loading data for file {fileId}");
Logger.LogWarning("Operation failed: " + errorMessage);
```

### 4. Expression-Bodied Members
```csharp
// ✅ CORRECT
private string FormatFileSize(long bytes) => bytes switch
{
    >= 1073741824 => $"{bytes / 1073741824.0:F2} GB",
    >= 1048576 => $"{bytes / 1048576.0:F2} MB",
    >= 1024 => $"{bytes / 1024.0:F2} KB",
    _ => $"{bytes} B"
};

private Color GetStatusColor(string status) => status switch
{
    "Success" => Color.Success,
    "Failed" => Color.Error,
    _ => Color.Default
};

// ❌ WRONG - Verbose switch statements
private string FormatFileSize(long bytes)
{
    if (bytes >= 1073741824)
        return $"{bytes / 1073741824.0:F2} GB";
    // ... more if statements
}
```

### 5. SignalR Integration
```csharp
// ✅ CORRECT
@inject IDashboard<FileMetadata> DashboardService
private ConnectionState connectionState = ConnectionState.Disconnected;

protected override async Task OnInitializedAsync()
{
    await InitializeSignalRConnectionAsync(cancellationTokenSource.Token);
}

private async Task InitializeSignalRConnectionAsync(CancellationToken cancellationToken)
{
    try
    {
        Logger.LogInformation("Initializing SignalR connection");
        connectionState = ConnectionState.Connecting;
        await InvokeAsync(StateHasChanged);

        var subscribeResult = await DashboardService.SubscribeAsync("channel", cancellationToken);
        if (subscribeResult.IsFailure)
        {
            Logger.LogWarning("Failed to subscribe: {Error}", subscribeResult.Error);
            connectionState = ConnectionState.Failed;
            return;
        }

        connectionState = ConnectionState.Connected;
        Logger.LogInformation("Successfully connected to hub");
        await InvokeAsync(StateHasChanged);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Error initializing SignalR connection");
        connectionState = ConnectionState.Failed;
        await InvokeAsync(StateHasChanged);
    }
}

public async ValueTask DisposeAsync()
{
    if (connectionState == ConnectionState.Connected)
    {
        try
        {
            await DashboardService.UnsubscribeAsync("channel", CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error unsubscribing from updates");
        }
    }
}

// ❌ WRONG - Direct SignalR hub usage
@inject HubConnection HubConnection
// Missing abstraction layer
```

### 6. Immutability
```csharp
// ✅ CORRECT
private readonly List<Item> items = new();
private IReadOnlyList<Item> FilteredItems => items.Where(Filter).ToList();

// ✅ CORRECT - Using init properties in DTOs
public record FileMetadataDto(
    string FileName,
    string? Url,
    DateTime DownloadTimestamp,
    long FileSize);

// ❌ WRONG - Mutable collections exposed
private List<Item> items = new();
public List<Item> Items => items; // Exposes mutable collection
```

---

## Source File Mapping

### Story 1.2: Metadata Extraction
**Source:** `docs/qa/ui-enhancements-stories-1.1-1.3-bmad.md`
- **Lines:** 516-857
- **Key Components:**
  - Classification Results Display Component
  - Processing Stage Pipeline
  - Metadata Extraction Details Tabs
- **SignalR:** Use `Dashboard<ClassificationResult>` for real-time classification updates

### Story 1.3: Field Matching
**Source:** `docs/qa/ui-enhancements-stories-1.1-1.3-bmad.md`
- **Lines:** 859-1328
- **Key Components:**
  - Field Matching Visualization Component
  - Source Comparison Table
  - Unified Metadata Record Display
- **SignalR:** Use `Dashboard<MatchedFields>` for real-time field matching updates

### Story 1.4: Identity Resolution
**Source:** `docs/qa/ui-enhancements-stories-1.4-1.6-bmad.md`
- **Lines:** 22-383
- **Key Components:**
  - Identity Resolution Display Component
  - Legal Directive Classification Display Component
- **SignalR:** Use `Dashboard<ResolvedPerson>` for real-time identity resolution updates

### Story 1.5: SLA Tracking (CRITICAL - AC5)
**Source:** `docs/qa/ui-enhancements-stories-1.4-1.6-bmad.md`
- **Lines:** 387-862
- **Key Components:**
  - SLA Dashboard Component
  - SLA Timeline Visualization Component
- **SignalR:** Use `Dashboard<SlaStatus>` for real-time countdown timer updates
- **Note:** This is AC5 and MUST be implemented for story completion

### Story 1.6: Manual Review
**Source:** `docs/qa/ui-enhancements-stories-1.4-1.6-bmad.md`
- **Lines:** 866-1215
- **Key Components:**
  - Manual Review Dashboard Enhancements
  - Review Case Detail Enhancements
- **SignalR:** Use `Dashboard<ReviewCase>` for real-time review status updates

### Story 1.7: SIRO Export
**Source:** `docs/qa/ui-enhancements-stories-1.7-1.9-bmad.md`
- **Lines:** 22-686
- **Key Components:**
  - Export Management Dashboard Component
  - Export Queue Table
  - Export Details Dialog
- **SignalR:** Use `Dashboard<ExportStatus>` for real-time export progress updates

### Story 1.8: PDF Signing
**Source:** `docs/qa/ui-enhancements-stories-1.7-1.9-bmad.md`
- **Lines:** 690-1064
- **Key Components:**
  - PDF Export Extension to Export Management
  - Certificate Management Panel
  - PDF Export Status Display
- **SignalR:** Use `Dashboard<PdfSigningStatus>` for real-time signing progress
- **Note:** References ADR-002 for custom cryptographic watermarking approach

### Story 1.9: Audit Trail (CRITICAL - AC3 & AC6)
**Source:** `docs/qa/ui-enhancements-stories-1.7-1.9-bmad.md`
- **Lines:** 1068-1467+
- **Key Components:**
  - Audit Trail Viewer Component
  - Correlation ID Tracking
  - Export Functionality
  - Statistics Dashboard
- **SignalR:** Use `Dashboard<AuditRecord>` for real-time audit log updates
- **Note:** AC3 (Audit Trail Viewer) and AC6 (Export functionality) MUST be implemented

---

## Step-by-Step Instructions

### For Each Remaining Story File:

1. **Read the source section** from the original document (use line numbers above)

2. **Extract the component specifications**:
   - Features
   - UI requirements
   - Code examples (if any)

3. **Create the file** at `docs/qa/ui-enhancements/[story-number]-[story-name]-ui.md`

4. **Follow the template structure** above

5. **Align all code examples** to match:
   - Result<T> pattern
   - CancellationToken support
   - Structured logging
   - Expression-bodied members
   - SignalR via Dashboard<T>
   - Immutability patterns

6. **Reference Story 1.1** (`1.1-browser-automation-ui.md`) as the gold standard example

7. **Add SignalR integration** using the appropriate `Dashboard<T>` type:
   - Story 1.2: `Dashboard<ClassificationResult>`
   - Story 1.3: `Dashboard<MatchedFields>`
   - Story 1.4: `Dashboard<ResolvedPerson>`
   - Story 1.5: `Dashboard<SlaStatus>`
   - Story 1.6: `Dashboard<ReviewCase>`
   - Story 1.7: `Dashboard<ExportStatus>`
   - Story 1.8: `Dashboard<PdfSigningStatus>`
   - Story 1.9: `Dashboard<AuditRecord>`

8. **Add proper error handling**:
   - Loading states
   - Error messages
   - Empty states

9. **Include testing considerations**:
   - Unit test scenarios
   - Integration test scenarios

10. **Add related documentation links**:
    - Story document
    - ADR-001 (SignalR)
    - Story 1.10 (SignalR Infrastructure)
    - Sharding Approach document

---

## Key Patterns to Follow

### Component Initialization Pattern
```csharp
protected override async Task OnInitializedAsync()
{
    cancellationTokenSource = new CancellationTokenSource();
    await LoadDataAsync(cancellationTokenSource.Token);
    await InitializeSignalRConnectionAsync(cancellationTokenSource.Token);
}
```

### Data Loading Pattern
```csharp
private async Task LoadDataAsync(CancellationToken cancellationToken)
{
    isLoading = true;
    errorMessage = null;
    StateHasChanged();

    try
    {
        Logger.LogInformation("Loading data");
        var result = await Service.GetDataAsync(cancellationToken);
        
        if (result.IsFailure)
        {
            Logger.LogWarning("Failed to load data: {Error}", result.Error);
            errorMessage = $"Failed to load data: {result.Error}";
            return;
        }

        data.Clear();
        data.AddRange(result.Value);
        ApplyFilters();
        
        Logger.LogInformation("Loaded {Count} records", data.Count);
    }
    catch (OperationCanceledException)
    {
        Logger.LogInformation("Load cancelled");
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Error loading data");
        errorMessage = $"Error loading data: {ex.Message}";
    }
    finally
    {
        isLoading = false;
        await InvokeAsync(StateHasChanged);
    }
}
```

### Filtering Pattern
```csharp
private void ApplyFilters()
{
    filteredData.Clear();
    filteredData.AddRange(data.Where(FilterItem));
}

private bool FilterItem(Item item)
{
    if (!string.IsNullOrEmpty(searchText) && 
        !item.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }
    
    if (selectedStatus.HasValue && item.Status != selectedStatus.Value)
        return false;
        
    return true;
}
```

### SignalR Connection Pattern
```csharp
private async Task InitializeSignalRConnectionAsync(CancellationToken cancellationToken)
{
    try
    {
        Logger.LogInformation("Initializing SignalR connection for {Channel}", "channel-name");
        connectionState = ConnectionState.Connecting;
        await InvokeAsync(StateHasChanged);

        var subscribeResult = await DashboardService.SubscribeAsync("channel-name", cancellationToken);
        if (subscribeResult.IsFailure)
        {
            Logger.LogWarning("Failed to subscribe to updates: {Error}", subscribeResult.Error);
            connectionState = ConnectionState.Failed;
            return;
        }

        connectionState = ConnectionState.Connected;
        Logger.LogInformation("Successfully connected to updates hub");
        await InvokeAsync(StateHasChanged);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Error initializing SignalR connection");
        connectionState = ConnectionState.Failed;
        await InvokeAsync(StateHasChanged);
    }
}
```

---

## Validation Checklist

Before marking a file as complete, verify:

- [ ] File follows the template structure
- [ ] All code examples use Result<T> pattern
- [ ] All async methods have CancellationToken support
- [ ] Structured logging is used throughout
- [ ] Expression-bodied members for simple methods
- [ ] SignalR integration via Dashboard<T> abstraction
- [ ] Proper error handling (loading states, error messages)
- [ ] Immutability patterns applied
- [ ] Testing considerations included
- [ ] Related documentation links added
- [ ] Code matches the style of Story 1.1 example

---

## Priority Order

1. **Story 1.5 (SLA Dashboard)** - CRITICAL - AC5 requirement
2. **Story 1.9 (Audit Trail)** - CRITICAL - AC3 & AC6 requirements
3. **Story 1.2 (Metadata Extraction)** - High priority
4. **Story 1.3 (Field Matching)** - High priority
5. **Story 1.4 (Identity Resolution)** - Medium priority
6. **Story 1.6 (Manual Review)** - Medium priority
7. **Story 1.7 (SIRO Export)** - Medium priority
8. **Story 1.8 (PDF Signing)** - Medium priority (references ADR-002)

---

## Reference Files

- **Gold Standard Example:** `docs/qa/ui-enhancements/1.1-browser-automation-ui.md`
- **Sharding Approach:** `docs/qa/ui-enhancements/SHARDING-APPROACH.md`
- **Index:** `docs/qa/ui-enhancements/index.md`
- **ADR-001:** `docs/adr/ADR-001-SignalR-Unified-Hub-Abstraction.md`
- **ADR-002:** `docs/adr/ADR-002-Custom-PDF-Signing-Cryptographic-Watermarking.md`
- **Story 1.10:** `docs/stories/1.10.signalr-unified-hub-abstraction.md`

---

## Notes

- All files should be created in `docs/qa/ui-enhancements/` directory
- Use kebab-case for filenames: `[story-number]-[story-name]-ui.md`
- Update `index.md` after creating each file
- Ensure consistency with Story 1.1 example
- Pay special attention to Stories 1.5 and 1.9 as they have critical AC requirements

---

**Last Updated:** 2025-01-15  
**Status:** Ready for continuation in next session

