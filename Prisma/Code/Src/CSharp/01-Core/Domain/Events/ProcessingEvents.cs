// <copyright file="ProcessingEvents.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Document downloaded from SIARA, email, or manual upload.
/// </summary>
public record DocumentDownloadedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the downloaded file.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the name of the downloaded file.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the source of the download (SIARA, Email, Manual).
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Gets the detected file format.
    /// </summary>
    public FileFormat Format { get; init; } = FileFormat.Unknown;

    /// <summary>
    /// Gets the URL from which the document was downloaded.
    /// </summary>
    public string DownloadUrl { get; init; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentDownloadedEvent"/> class.
    /// </summary>
    public DocumentDownloadedEvent()
    {
        EventType = nameof(DocumentDownloadedEvent);
    }
}

/// <summary>
/// Image quality analysis completed (EmguCV).
/// </summary>
public record QualityAnalysisCompletedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the analyzed file.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the overall quality level determined by analysis.
    /// </summary>
    public ImageQualityLevel QualityLevel { get; init; } = ImageQualityLevel.Unknown;

    /// <summary>
    /// Gets the blur score (higher values indicate more blur).
    /// </summary>
    public decimal BlurScore { get; init; }

    /// <summary>
    /// Gets the noise score (higher values indicate more noise).
    /// </summary>
    public decimal NoiseScore { get; init; }

    /// <summary>
    /// Gets the contrast score (higher values indicate better contrast).
    /// </summary>
    public decimal ContrastScore { get; init; }

    /// <summary>
    /// Gets the sharpness score (higher values indicate sharper image).
    /// </summary>
    public decimal SharpnessScore { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="QualityAnalysisCompletedEvent"/> class.
    /// </summary>
    public QualityAnalysisCompletedEvent()
    {
        EventType = nameof(QualityAnalysisCompletedEvent);
    }
}

/// <summary>
/// OCR processing completed (Tesseract or GOT-OCR2).
/// </summary>
public record OcrCompletedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the processed file.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the OCR engine used (Tesseract, GOT-OCR2).
    /// </summary>
    public string OcrEngine { get; init; } = string.Empty;

    /// <summary>
    /// Gets the confidence score of OCR results (0-100).
    /// </summary>
    public decimal Confidence { get; init; }

    /// <summary>
    /// Gets the length of extracted text in characters.
    /// </summary>
    public int ExtractedTextLength { get; init; }

    /// <summary>
    /// Gets the total processing time for OCR operation.
    /// </summary>
    public TimeSpan ProcessingTime { get; init; }

    /// <summary>
    /// Gets a value indicating whether fallback OCR engine was triggered.
    /// </summary>
    public bool FallbackTriggered { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OcrCompletedEvent"/> class.
    /// </summary>
    public OcrCompletedEvent()
    {
        EventType = nameof(OcrCompletedEvent);
    }
}

/// <summary>
/// Classification completed with confidence and warnings.
/// </summary>
public record ClassificationCompletedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the classified file.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the requirement type ID from classification.
    /// </summary>
    public int RequirementTypeId { get; init; }

    /// <summary>
    /// Gets the requirement type name (e.g., Aseguramiento, Desbloqueo).
    /// </summary>
    public string RequirementTypeName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the classification confidence score (0-100).
    /// </summary>
    public int Confidence { get; init; }

    /// <summary>
    /// Gets the list of warnings generated during classification.
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// Gets a value indicating whether manual review is required.
    /// </summary>
    public bool RequiresManualReview { get; init; }

    /// <summary>
    /// Gets the relation type (NewRequirement, Recordatorio, Alcance, Precision).
    /// </summary>
    public string RelationType { get; init; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassificationCompletedEvent"/> class.
    /// </summary>
    public ClassificationCompletedEvent()
    {
        EventType = nameof(ClassificationCompletedEvent);
    }
}

/// <summary>
/// Conflict detected between XML and OCR data during reconciliation.
/// </summary>
public record ConflictDetectedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the file with conflict.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the name of the field where conflict was detected.
    /// </summary>
    public string FieldName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the value from XML metadata.
    /// </summary>
    public string XmlValue { get; init; } = string.Empty;

    /// <summary>
    /// Gets the value from OCR extraction.
    /// </summary>
    public string OcrValue { get; init; } = string.Empty;

    /// <summary>
    /// Gets the similarity score between XML and OCR values (0-1).
    /// </summary>
    public decimal SimilarityScore { get; init; }

    /// <summary>
    /// Gets the conflict severity level (Low, Medium, High).
    /// </summary>
    public string ConflictSeverity { get; init; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConflictDetectedEvent"/> class.
    /// </summary>
    public ConflictDetectedEvent()
    {
        EventType = nameof(ConflictDetectedEvent);
    }
}

/// <summary>
/// Document flagged for manual review (defensive intelligence - not rejection).
/// </summary>
public record DocumentFlaggedForReviewEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the flagged file.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the list of reasons why the document was flagged.
    /// </summary>
    public List<string> Reasons { get; init; } = new();

    /// <summary>
    /// Gets the review priority level (Low, Normal, High, Urgent).
    /// </summary>
    public string Priority { get; init; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentFlaggedForReviewEvent"/> class.
    /// </summary>
    public DocumentFlaggedForReviewEvent()
    {
        EventType = nameof(DocumentFlaggedForReviewEvent);
    }
}

/// <summary>
/// Document processing completed successfully (80%+ auto-processed goal).
/// </summary>
public record DocumentProcessingCompletedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the completed file.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the total processing time from download to completion.
    /// </summary>
    public TimeSpan TotalProcessingTime { get; init; }

    /// <summary>
    /// Gets a value indicating whether the document was auto-processed (true) or flagged for review (false).
    /// </summary>
    public bool AutoProcessed { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentProcessingCompletedEvent"/> class.
    /// </summary>
    public DocumentProcessingCompletedEvent()
    {
        EventType = nameof(DocumentProcessingCompletedEvent);
    }
}

/// <summary>
/// Processing error occurred (logged but system continues - defensive intelligence).
/// </summary>
public record ProcessingErrorEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the file where error occurred (null if system-level error).
    /// </summary>
    public Guid? FileId { get; init; }

    /// <summary>
    /// Gets the error message describing what went wrong.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets the stack trace for debugging purposes.
    /// </summary>
    public string StackTrace { get; init; } = string.Empty;

    /// <summary>
    /// Gets the component where error occurred (OCR, Classification, Storage, etc.).
    /// </summary>
    public string Component { get; init; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingErrorEvent"/> class.
    /// </summary>
    public ProcessingErrorEvent()
    {
        EventType = nameof(ProcessingErrorEvent);
    }
}

/// <summary>
/// Document rejected due to unacceptable quality (defensive flagging - not crash).
/// </summary>
public record QualityRejectedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the rejected file.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the quality score that caused rejection.
    /// </summary>
    public decimal Score { get; init; }

    /// <summary>
    /// Gets the reason for quality rejection.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="QualityRejectedEvent"/> class.
    /// </summary>
    public QualityRejectedEvent()
    {
        EventType = nameof(QualityRejectedEvent);
    }
}

/// <summary>
/// Fusion/reconciliation completed successfully (XML + PDF + DOCX merged).
/// </summary>
public record FusionCompletedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the source file.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the unique identifier for the resulting Expediente.
    /// </summary>
    public Guid ExpedienteId { get; init; }

    /// <summary>
    /// Gets the number of fields successfully fused.
    /// </summary>
    public int FieldsFused { get; init; }

    /// <summary>
    /// Gets the number of conflicts detected during fusion.
    /// </summary>
    public int ConflictsDetected { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FusionCompletedEvent"/> class.
    /// </summary>
    public FusionCompletedEvent()
    {
        EventType = nameof(FusionCompletedEvent);
    }
}

/// <summary>
/// Adaptive export completed successfully (Expediente → target format).
/// </summary>
public record ExportCompletedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the source file.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the destination path where file was exported.
    /// </summary>
    public string Destination { get; init; } = string.Empty;

    /// <summary>
    /// Gets the export format used (PDF, Excel, Word, etc.).
    /// </summary>
    public string Format { get; init; } = string.Empty;

    /// <summary>
    /// Gets the size of the exported file in bytes.
    /// </summary>
    public long ExportedSizeBytes { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportCompletedEvent"/> class.
    /// </summary>
    public ExportCompletedEvent()
    {
        EventType = nameof(ExportCompletedEvent);
    }
}
