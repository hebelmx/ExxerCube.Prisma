namespace ExxerCube.Prisma.Web.UI.Services;

using ExxerCube.Prisma.Web.UI.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Centralized state management service for the Document Processing page.
/// Provides isolated state containers with change notifications to prevent
/// fixture mismatch bugs and enable predictable state updates.
/// </summary>
public sealed class DocumentProcessingStateService
{
    private readonly ILogger<DocumentProcessingStateService> _logger;
    private readonly DocumentProcessingViewModel _state;

    /// <summary>
    /// Event fired when state changes, triggering UI re-render.
    /// </summary>
    public event Action? OnStateChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentProcessingStateService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for state management operations</param>
    public DocumentProcessingStateService(ILogger<DocumentProcessingStateService> logger)
    {
        _logger = logger;
        _state = new DocumentProcessingViewModel();
    }

    /// <summary>
    /// Gets the current view model state (read-only access).
    /// </summary>
    public DocumentProcessingViewModel State => _state;

      // XML State Management

    /// <summary>
    /// Updates the XML processing state using a callback.
    /// </summary>
    /// <param name="update">Action to apply to XmlProcessingState</param>
    public void UpdateXmlState(Action<XmlProcessingState> update)
    {
        update(_state.XmlState);
        _logger.LogDebug("XML state updated: FixtureName={FixtureName}, FieldCount={FieldCount}",
            _state.XmlState.FixtureName, _state.XmlState.FieldCount);
        NotifyStateChanged();
    }

    /// <summary>
    /// Clears all XML processing state.
    /// </summary>
    public void ClearXmlState()
    {
        _state.XmlState.Expediente = null;
        _state.XmlState.FixtureName = null;
        _state.XmlState.SourceXml = null;
        _state.XmlState.FieldCount = 0;
        _state.XmlState.Metadata = new();
        _state.XmlState.JsonResult = null;
        _state.XmlState.ShowSource = false;

        _logger.LogInformation("XML state cleared");
        NotifyStateChanged();
    }

      // 

      // PDF State Management

    /// <summary>
    /// Updates the PDF processing state using a callback.
    /// </summary>
    /// <param name="update">Action to apply to PdfProcessingState</param>
    public void UpdatePdfState(Action<PdfProcessingState> update)
    {
        update(_state.PdfState);
        _logger.LogDebug("PDF state updated: FixtureName={FixtureName}, OcrConfidence={OcrConfidence}",
            _state.PdfState.FixtureName, _state.PdfState.OcrResult?.OCRResult.Confidence.Value * 100);
        NotifyStateChanged();
    }

    /// <summary>
    /// Clears all PDF processing state.
    /// </summary>
    public void ClearPdfState()
    {
        _state.PdfState.OcrResult = null;
        _state.PdfState.Expediente = null;
        _state.PdfState.FixtureName = null;
        _state.PdfState.PdfBytes = null;
        _state.PdfState.Metadata = new();

        _logger.LogInformation("PDF state cleared");
        NotifyStateChanged();
    }

      // 

      // Comparison State Management

    /// <summary>
    /// Updates the comparison state using a callback.
    /// </summary>
    /// <param name="update">Action to apply to ComparisonState</param>
    public void UpdateComparisonState(Action<ComparisonState> update)
    {
        update(_state.ComparisonState);
        _logger.LogDebug("Comparison state updated: HasComparison={HasComparison}, HasFusion={HasFusion}",
            _state.ComparisonState.ComparisonResult != null, _state.ComparisonState.FusionResult != null);
        NotifyStateChanged();
    }

    /// <summary>
    /// Clears all comparison and fusion state.
    /// </summary>
    public void ClearComparisonState()
    {
        _state.ComparisonState.ComparisonResult = null;
        _state.ComparisonState.FusionResult = null;

        _logger.LogInformation("Comparison state cleared");
        NotifyStateChanged();
    }

      // 

      // Bulk Processing State Management

    /// <summary>
    /// Updates the bulk processing state using a callback.
    /// </summary>
    /// <param name="update">Action to apply to BulkProcessingState</param>
    public void UpdateBulkState(Action<BulkProcessingState> update)
    {
        update(_state.BulkState);
        _logger.LogDebug("Bulk state updated: DocumentCount={DocumentCount}, IsProcessing={IsProcessing}",
            _state.BulkState.Documents.Count, _state.BulkState.IsProcessing);
        NotifyStateChanged();
    }

    /// <summary>
    /// Clears all bulk processing state.
    /// </summary>
    public void ClearBulkState()
    {
        _state.BulkState.Documents.Clear();
        _state.BulkState.IsProcessing = false;
        _state.BulkState.Summary = null;

        _logger.LogInformation("Bulk processing state cleared");
        NotifyStateChanged();
    }

      // 

      // Shared UI State Management

    /// <summary>
    /// Updates the shared UI state using a callback.
    /// </summary>
    /// <param name="update">Action to apply to SharedUiState</param>
    public void UpdateSharedState(Action<SharedUiState> update)
    {
        update(_state.SharedState);
        _logger.LogDebug("Shared UI state updated: IsProcessing={IsProcessing}, Progress={Progress}%",
            _state.SharedState.IsProcessing, _state.SharedState.ProcessingProgress);
        NotifyStateChanged();
    }

    /// <summary>
    /// Clears all shared UI state.
    /// </summary>
    public void ClearSharedState()
    {
        _state.SharedState.SelectedFile = null;
        _state.SharedState.IsProcessing = false;
        _state.SharedState.CurrentJobId = null;
        _state.SharedState.ProcessingStatus = "";
        _state.SharedState.ProcessingMessage = "";
        _state.SharedState.ProcessingProgress = 0;
        _state.SharedState.ErrorMessage = null;

        _logger.LogInformation("Shared UI state cleared");
        NotifyStateChanged();
    }

      // 

      // Global Operations

    /// <summary>
    /// Clears all state across all processing pipelines.
    /// </summary>
    public void ClearAll()
    {
        _logger.LogInformation("Clearing all state");

        ClearXmlState();
        ClearPdfState();
        ClearComparisonState();
        ClearBulkState();
        ClearSharedState();

        // NotifyStateChanged already called by individual clear methods
    }

    /// <summary>
    /// Clears comparison state when either source changes (XML or PDF).
    /// This prevents stale comparison results from being displayed.
    /// </summary>
    public void InvalidateComparison()
    {
        if (_state.ComparisonState.ComparisonResult != null ||
            _state.ComparisonState.FusionResult != null)
        {
            _logger.LogInformation("Invalidating comparison state due to source data change");
            ClearComparisonState();
        }
    }

      // 

    /// <summary>
    /// Notifies subscribers that state has changed, triggering UI re-render.
    /// </summary>
    private void NotifyStateChanged()
    {
        OnStateChanged?.Invoke();
    }
}
