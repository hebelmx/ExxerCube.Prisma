using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Testing.Infrastructure;

/// <summary>
/// Opens documents using system default applications while tracking all spawned processes
/// to prevent resource leaks during testing.
/// </summary>
/// <remarks>
/// PROBLEM: When opening documents with Process.Start(UseShellExecute=true), you often get
/// a shell process that immediately spawns the actual viewer (Adobe Reader, Word) as a child
/// and then exits. Simple PID tracking misses these child processes, causing resource leaks.
///
/// SOLUTION: This class uses Windows Job Objects to automatically track ALL descendant processes,
/// ensuring complete cleanup when disposed.
/// </remarks>
public sealed class DocumentOpener : IDisposable
{
    private readonly ILogger<DocumentOpener>? _logger;
    private readonly ProcessJobObject _jobObject;
    private readonly List<string> _openedFiles = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentOpener"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for tracking document operations.</param>
    public DocumentOpener(ILogger<DocumentOpener>? logger = null)
    {
        _logger = logger;
        _jobObject = new ProcessJobObject();
    }

    /// <summary>
    /// Gets the count of documents that were successfully opened.
    /// </summary>
    public int OpenedDocumentCount => _openedFiles.Count;

    /// <summary>
    /// Gets the list of file paths that were opened.
    /// </summary>
    public IReadOnlyList<string> OpenedFiles => _openedFiles.AsReadOnly();

    /// <summary>
    /// Opens a document using the system's default application.
    /// The opened process and all its children are tracked and will be terminated on disposal.
    /// </summary>
    /// <param name="filePath">Path to the document to open.</param>
    /// <returns>True if the document was opened successfully, false otherwise.</returns>
    public bool OpenDocument(string filePath)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DocumentOpener));
        }

        if (!File.Exists(filePath))
        {
            _logger?.LogWarning("Cannot open document - file not found: {FilePath}", filePath);
            return false;
        }

        try
        {
            _logger?.LogInformation("Opening document: {FileName}", Path.GetFileName(filePath));

            var startInfo = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };

            var process = Process.Start(startInfo);
            if (process != null)
            {
                // Assign process to job object - all child processes will be automatically tracked
                _jobObject.AssignProcess(process);
                _openedFiles.Add(filePath);

                _logger?.LogInformation("Document opened successfully (PID: {ProcessId}): {FileName}",
                    process.Id, Path.GetFileName(filePath));

                return true;
            }
            else
            {
                _logger?.LogWarning("Document opened but process handle not available: {FilePath}", filePath);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open document: {FilePath}", filePath);
            return false;
        }
    }

    /// <summary>
    /// Opens multiple documents concurrently.
    /// </summary>
    /// <param name="filePaths">Paths to the documents to open.</param>
    /// <param name="delayBetweenMs">Delay in milliseconds between opening each document (default: 500ms).</param>
    /// <returns>Number of documents successfully opened.</returns>
    public async Task<int> OpenDocumentsAsync(IEnumerable<string> filePaths, int delayBetweenMs = 500)
    {
        var count = 0;
        foreach (var filePath in filePaths)
        {
            if (OpenDocument(filePath))
            {
                count++;
            }

            if (delayBetweenMs > 0)
            {
                await Task.Delay(delayBetweenMs);
            }
        }

        return count;
    }

    /// <summary>
    /// Closes all opened documents by terminating all tracked processes and their children.
    /// </summary>
    public void CloseAllDocuments()
    {
        if (_disposed)
        {
            return;
        }

        _logger?.LogInformation("Closing all opened documents ({Count} files)...", _openedFiles.Count);

        // Job object disposal automatically terminates all processes and their children
        _jobObject.Dispose();

        _logger?.LogInformation("All documents closed successfully");
    }

    /// <summary>
    /// Disposes the document opener, closing all opened documents and their child processes.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CloseAllDocuments();
        _disposed = true;
    }
}
