# Process Tracking Techniques for Document Opening

## The Problem

When opening documents in tests using `Process.Start()` with `UseShellExecute = true`:

```csharp
var process = Process.Start(new ProcessStartInfo
{
    FileName = "document.pdf",
    UseShellExecute = true
});
```

**Issues:**
1. The returned `Process` is often just a **shell process**, not the actual viewer
2. The shell spawns the real viewer (Adobe Reader, Word, etc.) as a **child process** and exits
3. Simple PID tracking only captures the shell process, missing child processes
4. **Result:** Resource leaks - PDF readers and Word instances remain open after tests complete

## Solutions (Ranked by Effectiveness)

### 1. ⭐ **Windows Job Objects** (Best for Windows)

**Automatic child process tracking** - The OS tracks all descendants for you.

```csharp
using var documentOpener = new DocumentOpener(logger);

// Open documents - all child processes are automatically tracked
documentOpener.OpenDocument("file1.pdf");
documentOpener.OpenDocument("file2.docx");

// Cleanup is automatic on disposal - kills ALL processes including children
```

**Advantages:**
- ✅ Automatically tracks ALL child processes (even grandchildren)
- ✅ Guaranteed cleanup via OS mechanism
- ✅ No manual process enumeration needed
- ✅ Works even if parent process exits immediately
- ✅ Kills entire process tree atomically

**Disadvantages:**
- ❌ Windows-only (uses Win32 API)
- ❌ Requires P/Invoke code
- ❌ Slightly more complex setup

**Implementation:** See `ProcessJobObject.cs` and `DocumentOpener.cs`

---

### 2. **Process.Kill(entireProcessTree: true)** (Current Approach)

**.NET 5+ has built-in process tree killing.**

```csharp
private readonly List<Process> _openedProcesses = new();

void OpenDocument(string path)
{
    var process = Process.Start(new ProcessStartInfo
    {
        FileName = path,
        UseShellExecute = true
    });

    if (process != null)
    {
        _openedProcesses.Add(process);
    }
}

void Cleanup()
{
    foreach (var process in _openedProcesses)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true); // Kills children too!
        }
        process.Dispose();
    }
}
```

**Advantages:**
- ✅ Simple and cross-platform
- ✅ No P/Invoke required
- ✅ Built into .NET 5+

**Disadvantages:**
- ❌ Only works if parent process is still running
- ❌ If parent exits immediately, you lose the handle to children
- ❌ May fail if process already exited
- ❌ Timing issues - children may spawn after you enumerate

---

### 3. **Track by File Handles** (Alternative Approach)

**Find processes that have specific files open.**

```csharp
// Step 1: Track which files you opened
private readonly HashSet<string> _openedFiles = new();

void OpenDocument(string path)
{
    _openedFiles.Add(Path.GetFullPath(path));
    Process.Start(new ProcessStartInfo
    {
        FileName = path,
        UseShellExecute = true
    });
}

// Step 2: Find all processes with those files open
void Cleanup()
{
    foreach (var filePath in _openedFiles)
    {
        var processes = FindProcessesUsingFile(filePath);
        foreach (var process in processes)
        {
            process.Kill();
            process.Dispose();
        }
    }
}

// Using Restart Manager API or handle.exe
IEnumerable<Process> FindProcessesUsingFile(string filePath)
{
    // Implementation using Windows Restart Manager API
    // See: https://docs.microsoft.com/en-us/windows/win32/rstmgr/restart-manager-portal
    // Or use handle.exe from Sysinternals
}
```

**Advantages:**
- ✅ Doesn't depend on parent process handle
- ✅ Finds all processes using the file

**Disadvantages:**
- ❌ Complex - requires Restart Manager API or external tools
- ❌ May not work for all file types
- ❌ Can kill unrelated processes if file is shared

---

### 4. **Don't Open Documents** (Pragmatic for CI/CD)

**For automated testing, just verify download success without opening.**

```csharp
// Instead of this:
OpenDocument(downloadedFile);
await Task.Delay(TimeSpan.FromMinutes(2)); // Show to user

// Do this:
Assert.That(File.Exists(downloadedFile));
Assert.That(new FileInfo(downloadedFile).Length, Is.GreaterThan(0));
_logger.LogInformation("Downloaded successfully: {File}", downloadedFile);
```

**Advantages:**
- ✅ No resource leaks
- ✅ Faster test execution
- ✅ Works in headless CI/CD environments

**Disadvantages:**
- ❌ Can't visually verify document content
- ❌ Not suitable for stakeholder demos

---

### 5. **Process Enumeration by Name** (Fragile)

**Track process counts before/after and kill new instances.**

```csharp
Dictionary<string, int> GetProcessCounts()
{
    return Process.GetProcesses()
        .GroupBy(p => p.ProcessName)
        .ToDictionary(g => g.Key, g => g.Count());
}

void Setup()
{
    _processCountsBefore = GetProcessCounts();
}

void OpenDocuments()
{
    // Open PDFs, DOCX, etc.
}

void Cleanup()
{
    var countsAfter = GetProcessCounts();

    // Kill new Adobe Reader processes
    var newAcrobat = countsAfter.GetValueOrDefault("AcroRd32", 0) -
                     _processCountsBefore.GetValueOrDefault("AcroRd32", 0);

    if (newAcrobat > 0)
    {
        var processes = Process.GetProcessesByName("AcroRd32")
            .OrderByDescending(p => p.StartTime)
            .Take(newAcrobat);

        foreach (var p in processes)
        {
            p.Kill();
        }
    }
}
```

**Advantages:**
- ✅ Can work even without process handles

**Disadvantages:**
- ❌ Very fragile - may kill wrong processes
- ❌ Doesn't work if same app was already open
- ❌ Hard-coded process names (AcroRd32, WINWORD, etc.)
- ❌ Race conditions with other tests

---

## Recommendations

### For Windows-only projects:
**Use Job Objects** (`DocumentOpener` class) - Most reliable and robust.

### For cross-platform projects:
**Use `Process.Kill(entireProcessTree: true)`** - Good enough for most cases, but be aware of limitations with short-lived parent processes.

### For CI/CD environments:
**Don't open documents** - Just verify downloads succeeded. Opening documents is for stakeholder demos only.

### Current implementation in SiaraSimulatorTests.cs:
Currently using approach #2 (`entireProcessTree: true`). Consider upgrading to Job Objects (#1) for guaranteed cleanup.

---

## Migration Example

**Before (Current):**
```csharp
private readonly List<Process> _openedDocumentProcesses = new();

private void OpenDocument(string filePath)
{
    var process = Process.Start(new ProcessStartInfo
    {
        FileName = filePath,
        UseShellExecute = true
    });

    if (process != null)
    {
        _openedDocumentProcesses.Add(process);
    }
}

public async ValueTask DisposeAsync()
{
    foreach (var process in _openedDocumentProcesses)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(2000);
        }
        process.Dispose();
    }
}
```

**After (With Job Objects):**
```csharp
private readonly DocumentOpener _documentOpener = new(logger);

private void OpenDocument(string filePath)
{
    _documentOpener.OpenDocument(filePath);
}

public async ValueTask DisposeAsync()
{
    // Automatically kills all processes and children
    _documentOpener.Dispose();
}
```

**Result:**
- Simpler code
- Guaranteed cleanup of ALL child processes
- No leaked resources

---

## Testing the Solution

```csharp
[Fact]
public async Task DocumentOpener_ShouldCleanupAllProcesses()
{
    // Get initial process count
    var initialProcessCount = Process.GetProcesses().Length;

    using (var opener = new DocumentOpener())
    {
        // Open several documents
        opener.OpenDocument("test1.pdf");
        opener.OpenDocument("test2.docx");
        opener.OpenDocument("test3.pdf");

        // Let viewers fully start
        await Task.Delay(2000);

        // Verify processes increased
        var midProcessCount = Process.GetProcesses().Length;
        Assert.That(midProcessCount, Is.GreaterThan(initialProcessCount));
    }

    // Wait for cleanup
    await Task.Delay(1000);

    // Verify all processes cleaned up
    var finalProcessCount = Process.GetProcesses().Length;
    Assert.That(finalProcessCount, Is.LessThanOrEqualTo(initialProcessCount + 2)); // Allow small variance
}
```

---

## References

- [Windows Job Objects](https://docs.microsoft.com/en-us/windows/win32/procthread/job-objects)
- [Process.Kill with entireProcessTree](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill)
- [Windows Restart Manager API](https://docs.microsoft.com/en-us/windows/win32/rstmgr/restart-manager-portal)
