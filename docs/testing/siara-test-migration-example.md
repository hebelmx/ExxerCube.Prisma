# Migrating SiaraSimulatorTests to Use DocumentOpener

## Current Implementation Issues

The current `SiaraSimulatorTests.cs` tracks opened documents manually:

```csharp
private readonly List<Process> _openedDocumentProcesses = new();

private void OpenDocument(string filePath)
{
    var process = Process.Start(new ProcessStartInfo
    {
        FileName = filePath,
        UseShellExecute = true,
        WindowStyle = ProcessWindowStyle.Normal
    });

    if (process != null)
    {
        _openedDocumentProcesses.Add(process);
        _logger.LogInformation("Document opened (PID: {ProcessId})", process.Id);
    }
}
```

**Problem:** When opening a PDF, the Process object points to the shell process, but Adobe Reader spawns as a child and may not be properly tracked.

---

## Improved Implementation with DocumentOpener

### Step 1: Update Class Fields

**Before:**
```csharp
private readonly List<Process> _openedDocumentProcesses = new();
```

**After:**
```csharp
private DocumentOpener? _documentOpener;
```

### Step 2: Initialize in Setup

**Add to `InitializeAsync()`:**
```csharp
public async ValueTask InitializeAsync()
{
    // ... existing code ...

    // Initialize document opener with job object tracking
    _documentOpener = new DocumentOpener(
        XUnitLogger.CreateLogger<DocumentOpener>(_output));

    _logger.LogInformation("Document opener initialized with Job Object tracking");
}
```

### Step 3: Simplify OpenDocument Method

**Before:**
```csharp
private void OpenDocument(string filePath)
{
    try
    {
        _logger.LogInformation("Opening document: {FileName}", Path.GetFileName(filePath));

        var startInfo = new ProcessStartInfo
        {
            FileName = filePath,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal
        };

        var process = Process.Start(startInfo);
        if (process != null)
        {
            _openedDocumentProcesses.Add(process);
            _logger.LogInformation("Document opened (PID: {ProcessId})", process.Id);
        }
        else
        {
            _logger.LogWarning("Document opened but process handle not available");
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to open document: {FilePath}", filePath);
    }
}
```

**After:**
```csharp
private void OpenDocument(string filePath)
{
    _documentOpener?.OpenDocument(filePath);
}
```

**Result:** 15 lines → 1 line! 🎉

### Step 4: Simplify Cleanup

**Before:**
```csharp
public async ValueTask DisposeAsync()
{
    // ... browser cleanup ...

    // Cleanup opened document processes to prevent resource leaks
    if (_openedDocumentProcesses.Any())
    {
        _logger.LogInformation("Cleaning up {Count} opened document processes...",
            _openedDocumentProcesses.Count);

        foreach (var process in _openedDocumentProcesses)
        {
            try
            {
                if (!process.HasExited)
                {
                    _logger.LogInformation("Closing document process (PID: {ProcessId})", process.Id);
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(2000);
                }
                process.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to close document process");
            }
        }

        _openedDocumentProcesses.Clear();
        _logger.LogInformation("All document processes cleaned up");
    }

    // ... rest of cleanup ...
}
```

**After:**
```csharp
public async ValueTask DisposeAsync()
{
    // ... browser cleanup ...

    // Cleanup document opener - automatically kills all processes and children
    _documentOpener?.Dispose();

    // ... rest of cleanup ...
}
```

**Result:** 25 lines → 1 line! 🎉

---

## Complete Diff

```diff
public class SiaraSimulatorTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<SiaraSimulatorTests> _logger;
    private IBrowserAutomationAgent? _automationAgent;
-   private readonly List<Process> _openedDocumentProcesses = new();
+   private DocumentOpener? _documentOpener;
    private Process? _simulatorProcess;

    public async ValueTask InitializeAsync()
    {
        // ... existing browser setup ...

+       // Initialize document opener with Job Object tracking
+       _documentOpener = new DocumentOpener(
+           XUnitLogger.CreateLogger<DocumentOpener>(_output));
+       _logger.LogInformation("Document opener initialized with Job Object tracking");
    }

-   private void OpenDocument(string filePath)
-   {
-       try
-       {
-           _logger.LogInformation("Opening document: {FileName}", Path.GetFileName(filePath));
-
-           var startInfo = new ProcessStartInfo
-           {
-               FileName = filePath,
-               UseShellExecute = true,
-               WindowStyle = ProcessWindowStyle.Normal
-           };
-
-           var process = Process.Start(startInfo);
-           if (process != null)
-           {
-               _openedDocumentProcesses.Add(process);
-               _logger.LogInformation("Document opened (PID: {ProcessId})", process.Id);
-           }
-           else
-           {
-               _logger.LogWarning("Document opened but process handle not available");
-           }
-       }
-       catch (Exception ex)
-       {
-           _logger.LogWarning(ex, "Failed to open document: {FilePath}", filePath);
-       }
-   }
+   private void OpenDocument(string filePath)
+   {
+       _documentOpener?.OpenDocument(filePath);
+   }

    public async ValueTask DisposeAsync()
    {
        // ... browser cleanup ...

-       // Cleanup opened document processes
-       if (_openedDocumentProcesses.Any())
-       {
-           _logger.LogInformation("Cleaning up {Count} opened document processes...",
-               _openedDocumentProcesses.Count);
-
-           foreach (var process in _openedDocumentProcesses)
-           {
-               try
-               {
-                   if (!process.HasExited)
-                   {
-                       _logger.LogInformation("Closing document process (PID: {ProcessId})", process.Id);
-                       process.Kill(entireProcessTree: true);
-                       process.WaitForExit(2000);
-                   }
-                   process.Dispose();
-               }
-               catch (Exception ex)
-               {
-                   _logger.LogWarning(ex, "Failed to close document process");
-               }
-           }
-
-           _openedDocumentProcesses.Clear();
-           _logger.LogInformation("All document processes cleaned up");
-       }
+       // Cleanup document opener - automatically kills all processes and children
+       _documentOpener?.Dispose();

        // ... rest of cleanup ...
    }
}
```

---

## Benefits

### Before Migration:
- ❌ **40+ lines** of manual process tracking code
- ❌ May miss child processes if parent exits quickly
- ❌ Complex error handling in cleanup loop
- ❌ ReSharper warnings about resource leaks

### After Migration:
- ✅ **2 lines** total (init + dispose)
- ✅ **Guaranteed** child process cleanup via Job Objects
- ✅ No manual error handling needed
- ✅ No ReSharper warnings

---

## Verification

After migration, verify cleanup is working:

1. **Run test with Process Explorer open**
2. **Watch for Adobe Reader / Word processes during test**
3. **Verify all viewers close when test completes**

Expected behavior:
- ✅ Documents open during test
- ✅ **All** viewer processes close on test completion
- ✅ No lingering AcroRd32.exe or WINWORD.EXE processes

---

## Platform Considerations

**Windows:** Job Objects work perfectly ✅

**Linux/macOS:** Job Objects are Windows-only. For cross-platform:
- Use `Process.Kill(entireProcessTree: true)` (already in code)
- Or don't open documents in CI/CD (just verify download)

**Recommendation:**
```csharp
// Conditional compilation for platform-specific code
#if WINDOWS
    _documentOpener = new DocumentOpener(logger);
#endif

private void OpenDocument(string filePath)
{
#if WINDOWS
    _documentOpener?.OpenDocument(filePath);
#else
    // On non-Windows, just log instead of opening
    _logger.LogInformation("Would open document: {File}", filePath);
#endif
}
```
