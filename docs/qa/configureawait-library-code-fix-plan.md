# ConfigureAwait(false) Fix Plan - Library Code Only

**Date:** 2025-01-15  
**Scope:** Application + Infrastructure layers (library code)  
**Excluded:** UI code (Razor components, controllers that interact with UI)

---

## Summary

**Total async calls in library code:** ~102 calls  
**Currently fixed:** 8 (DecisionLogicService)  
**Remaining to fix:** ~94 calls

---

## Files to Fix

### Application/Services Layer (60 calls to fix)

1. **DocumentIngestionService.cs** - 14 async calls
2. **OcrProcessingService.cs** - 19 async calls  
3. **MetadataExtractionService.cs** - 9 async calls
4. **FieldMatchingService.cs** - 4 async calls
5. **HealthCheckService.cs** - 9 async calls
6. **ProcessingMetricsService.cs** - 5 async calls

### Infrastructure Layer (34 calls to fix)

1. **CircuitBreakerPythonInteropService.cs** - 11 async calls
2. **OcrProcessingAdapter.cs** - 11 async calls
3. **FileSystemOutputWriter.cs** - 6 async calls
4. **PrismaOcrService.cs** - 2 async calls
5. **PrismaOcrWrapperAdapter.cs** - 1 async call
6. **FileSystemLoader.cs** - 2 async calls
7. **Other Infrastructure services** - 1 async call

### Infrastructure.Classification Layer

- **PersonIdentityResolverService.cs**
- **LegalDirectiveClassifierService.cs**
- **MatchingPolicyService.cs**
- **FieldMatcherService.cs**
- **FileClassifierService.cs**

### Infrastructure.Extraction Layer

- **PdfOcrFieldExtractor.cs**
- **PdfMetadataExtractor.cs**
- **XmlMetadataExtractor.cs**
- **FileTypeIdentifierService.cs**

### Infrastructure.FileStorage Layer

- **FileMoverService.cs**
- **SafeFileNamerService.cs**
- **FileSystemDownloadStorageAdapter.cs**

### Infrastructure.Database Layer

- **FileMetadataLoggerService.cs**
- **DownloadTrackerService.cs**

### Infrastructure.BrowserAutomation Layer

- **PlaywrightBrowserAutomationAdapter.cs**

---

## Pattern to Apply

**Before:**
```csharp
var result = await _service.DoSomethingAsync(param, cancellationToken);
```

**After:**
```csharp
var result = await _service.DoSomethingAsync(param, cancellationToken).ConfigureAwait(false);
```

---

## Excluded (UI Code - DO NOT FIX)

- `UI/ExxerCube.Prisma.Web.UI/**/*.razor` - Razor components need UI context
- `UI/ExxerCube.Prisma.Web.UI/Controllers/*Controller.cs` - If they interact with UI
- Test files - Tests can keep default behavior

---

## Implementation Strategy

1. Fix Application/Services layer first (higher priority)
2. Fix Infrastructure layer second
3. Verify no UI code is modified
4. Run tests to ensure no regressions

---

**Status:** Ready to implement

