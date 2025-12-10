namespace ExxerCube.Prisma.Web.UI.Models;

using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.SignalR.Client;

/// <summary>
/// View model for the Document Processing page, providing isolated state containers
/// for different processing pipelines to prevent cross-contamination bugs.
/// </summary>
public sealed class DocumentProcessingViewModel
{
    /// <summary>
    /// State container for XML processing pipeline.
    /// </summary>
    public XmlProcessingState XmlState { get; init; } = new();

    /// <summary>
    /// State container for PDF/OCR processing pipeline.
    /// </summary>
    public PdfProcessingState PdfState { get; init; } = new();

    /// <summary>
    /// State container for comparison and fusion operations.
    /// </summary>
    public ComparisonState ComparisonState { get; init; } = new();

    /// <summary>
    /// State container for bulk processing operations.
    /// </summary>
    public BulkProcessingState BulkState { get; init; } = new();

    /// <summary>
    /// Shared UI state across all processing pipelines.
    /// </summary>
    public SharedUiState SharedState { get; init; } = new();
}

/// <summary>
/// Isolated state for XML fixture loading and processing.
/// </summary>
public sealed class XmlProcessingState
{
    /// <summary>
    /// Parsed Expediente entity from XML.
    /// </summary>
    public Expediente? Expediente { get; set; }

    /// <summary>
    /// Name of the currently loaded XML fixture file.
    /// </summary>
    public string? FixtureName { get; set; }

    /// <summary>
    /// Raw XML content for display/debugging.
    /// </summary>
    public string? SourceXml { get; set; }

    /// <summary>
    /// Count of successfully extracted fields.
    /// </summary>
    public int FieldCount { get; set; }

    /// <summary>
    /// Extraction metadata for fusion service.
    /// </summary>
    public ExtractionMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Serialized JSON representation of the Expediente.
    /// </summary>
    public string? JsonResult { get; set; }

    /// <summary>
    /// Flag to show/hide raw XML source in UI.
    /// </summary>
    public bool ShowSource { get; set; }
}

/// <summary>
/// Isolated state for PDF/OCR fixture loading and processing.
/// </summary>
public sealed class PdfProcessingState
{
    /// <summary>
    /// OCR processing result containing extracted text and confidence scores.
    /// </summary>
    public ProcessingResult? OcrResult { get; set; }

    /// <summary>
    /// Expediente entity extracted from PDF via OCR field extraction.
    /// </summary>
    public Expediente? Expediente { get; set; }

    /// <summary>
    /// Name of the currently loaded PDF fixture file.
    /// </summary>
    public string? FixtureName { get; set; }

    /// <summary>
    /// PDF file bytes for viewer rendering.
    /// </summary>
    public byte[]? PdfBytes { get; set; }

    /// <summary>
    /// Extraction metadata for fusion service.
    /// </summary>
    public ExtractionMetadata Metadata { get; set; } = new();
}

/// <summary>
/// State for comparison and intelligent fusion operations.
/// </summary>
public sealed class ComparisonState
{
    /// <summary>
    /// Result of comparing XML and PDF Expediente entities.
    /// </summary>
    public ComparisonResult? ComparisonResult { get; set; }

    /// <summary>
    /// Result of 3-way intelligent fusion (XML + PDF + DOCX).
    /// </summary>
    public FusionResult? FusionResult { get; set; }

    /// <summary>
    /// Indicates whether comparison can be performed (both XML and PDF loaded).
    /// </summary>
    public bool CanCompare(DocumentProcessingViewModel viewModel)
    {
        return viewModel.XmlState.Expediente != null &&
               viewModel.PdfState.OcrResult != null;
    }
}

/// <summary>
/// State for bulk processing operations.
/// </summary>
public sealed class BulkProcessingState
{
    /// <summary>
    /// List of documents queued for batch processing.
    /// </summary>
    public List<BulkDocument> Documents { get; set; } = new();

    /// <summary>
    /// Indicates whether a batch operation is currently running.
    /// </summary>
    public bool IsProcessing { get; set; }

    /// <summary>
    /// Summary statistics for completed batch operations.
    /// </summary>
    public BatchSummary? Summary { get; set; }
}

/// <summary>
/// Shared UI state across all processing pipelines.
/// </summary>
public sealed class SharedUiState
{
    /// <summary>
    /// User-selected file for upload processing.
    /// </summary>
    public IBrowserFile? SelectedFile { get; set; }

    /// <summary>
    /// General processing flag (used across all pipelines).
    /// </summary>
    public bool IsProcessing { get; set; }

    /// <summary>
    /// Current job ID for SignalR tracking.
    /// </summary>
    public string? CurrentJobId { get; set; }

    /// <summary>
    /// Processing status message (e.g., "Loading XML fixture...").
    /// </summary>
    public string ProcessingStatus { get; set; } = "";

    /// <summary>
    /// Detailed processing message for user feedback.
    /// </summary>
    public string ProcessingMessage { get; set; } = "";

    /// <summary>
    /// Processing progress percentage (0-100).
    /// </summary>
    public int ProcessingProgress { get; set; }

    /// <summary>
    /// Error message to display to user.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// SignalR hub connection for real-time updates.
    /// </summary>
    public HubConnection? HubConnection { get; set; }
}
