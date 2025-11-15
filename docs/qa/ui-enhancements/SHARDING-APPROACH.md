# UI Enhancements Sharding Approach

**Created:** 2025-01-15  
**Status:** Active

---

## Overview

The original UI enhancement documents (`ui-enhancements-stories-1.1-1.3-bmad.md`, `ui-enhancements-stories-1.4-1.6-bmad.md`, `ui-enhancements-stories-1.7-1.9-bmad.md`) have been sharded into individual story files for better maintainability and alignment with code standards.

## Sharding Structure

Each story now has its own enhancement document:

### Stage 1: Ingestion
- `1.1-browser-automation-ui.md` - Document Ingestion Dashboard

### Stage 2: Extraction  
- `1.2-metadata-extraction-ui.md` - Classification Results Display
- `1.3-field-matching-ui.md` - Field Matching Visualization

### Stage 3: Decision Logic
- `1.4-identity-resolution-ui.md` - Identity Resolution Display
- `1.5-sla-tracking-ui.md` - SLA Dashboard
- `1.6-manual-review-ui.md` - Manual Review Interface

### Stage 4: Final Compliance Response
- `1.7-siro-export-ui.md` - Export Management Dashboard
- `1.8-pdf-signing-ui.md` - PDF Signing Status Display
- `1.9-audit-trail-ui.md` - Audit Trail Viewer

## Code Standards Alignment

All code examples in the sharded files have been aligned to match project coding standards:

### **1. Result<T> Pattern**
- All service calls return `Result<T>` instead of throwing exceptions
- Error handling uses Railway-Oriented Programming patterns
- Example:
```csharp
var result = await DocumentIngestionService.GetDownloadHistoryAsync(cancellationToken);
if (result.IsFailure)
{
    Logger.LogWarning("Failed to load download history: {Error}", result.Error);
    return;
}
downloads = result.Value;
```

### **2. CancellationToken Support**
- All async methods accept `CancellationToken`
- Proper cancellation handling throughout
- Example:
```csharp
protected override async Task OnInitializedAsync()
{
    using var cts = new CancellationTokenSource();
    await LoadDownloadHistoryAsync(cts.Token);
}
```

### **3. Structured Logging**
- Use `ILogger<T>` with structured logging
- Log key operations, errors, and non-normal flows
- Example:
```csharp
Logger.LogInformation("Loading download history for file {FileId}", fileId);
Logger.LogWarning("Download failed: {Error}", errorMessage);
```

### **4. Expression-Bodied Members**
- Use expression-bodied members for simple methods
- Example:
```csharp
private string FormatFileSize(long bytes) => 
    bytes switch
    {
        >= 1073741824 => $"{bytes / 1073741824.0:F2} GB",
        >= 1048576 => $"{bytes / 1048576.0:F2} MB",
        >= 1024 => $"{bytes / 1024.0:F2} KB",
        _ => $"{bytes} B"
    };
```

### **5. Immutability**
- Prefer `init` properties, records, and `ReadOnlyCollections<T>`
- Example:
```csharp
public record FileMetadataDto(
    string FileName,
    string? Url,
    DateTime DownloadTimestamp,
    long FileSize,
    FileFormat Format,
    string Status);
```

### **6. Async/Await Patterns**
- Proper async/await usage
- Use `ConfigureAwait(false)` for background operations
- Example:
```csharp
private async Task LoadDownloadHistoryAsync(CancellationToken cancellationToken)
{
    var result = await DocumentIngestionService
        .GetDownloadHistoryAsync(cancellationToken)
        .ConfigureAwait(false);
    
    if (result.IsSuccess)
    {
        downloads = result.Value.ToList();
        await InvokeAsync(StateHasChanged);
    }
}
```

### **7. SignalR Integration**
- Use `Dashboard<T>` abstraction from Story 1.10
- Proper connection state management
- Example:
```csharp
@inherits Dashboard<FileMetadata>
@inject IDashboard<FileMetadata> DashboardService

protected override async Task OnInitializedAsync()
{
    await base.OnInitializedAsync();
    await DashboardService.SubscribeAsync("downloads", cancellationToken);
}
```

## Real-time Updates

All components requiring real-time updates use the `Dashboard<T>` abstraction from Story 1.10:

- **Story 1.1**: `Dashboard<FileMetadata>` for download feed
- **Story 1.2**: `Dashboard<ClassificationResult>` for classification updates
- **Story 1.3**: `Dashboard<MatchedFields>` for field matching progress
- **Story 1.4**: `Dashboard<ResolvedPerson>` for identity resolution updates
- **Story 1.5**: `Dashboard<SlaStatus>` for SLA tracking
- **Story 1.6**: `Dashboard<ReviewCase>` for review status updates
- **Story 1.7**: `Dashboard<ExportStatus>` for export progress
- **Story 1.8**: `Dashboard<PdfSigningStatus>` for signing progress
- **Story 1.9**: `Dashboard<AuditRecord>` for audit log updates

## Testing Considerations

All UI components should follow testing standards:

- **xUnit v3** for test framework
- **NSubstitute** for mocking
- **Shouldly** for assertions
- Unit tests for component logic
- Integration tests for SignalR communication

## Related Documentation

- [ADR-001: SignalR Unified Hub Abstraction](../../adr/ADR-001-SignalR-Unified-Hub-Abstraction.md)
- [Story 1.10: SignalR Infrastructure](../../stories/1.10.signalr-unified-hub-abstraction.md)
- [Architectural and Code Pattern Rules](../../.cursor/rules/1016_ArchitecturalAndCodePatternRules.mdc)

---

**Last Updated:** 2025-01-15

