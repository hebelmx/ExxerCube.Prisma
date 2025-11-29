using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Tests.Domain.Domain.Events;

/// <summary>
/// Unit tests for processing domain events covering event initialization and property validation.
/// </summary>
public class ProcessingEventsTests
{
    /// <summary>
    /// Tests that <see cref="DocumentDownloadedEvent"/> initializes with correct EventType.
    /// </summary>
    [Fact]
    public void DocumentDownloadedEvent_Initialize_SetsEventTypeCorrectly()
    {
        // Arrange & Act
        var evt = new DocumentDownloadedEvent
        {
            FileId = Guid.NewGuid(),
            FileName = "test.pdf",
            Source = "SIARA",
            FileSizeBytes = 1024,
            Format = FileFormat.Pdf,
            DownloadUrl = "https://test.com/file.pdf"
        };

        // Assert
        evt.EventType.ShouldBe(nameof(DocumentDownloadedEvent));
        evt.EventId.ShouldNotBe(Guid.Empty);
        evt.Timestamp.ShouldBeInRange(DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow);
        evt.FileId.ShouldNotBe(Guid.Empty);
        evt.FileName.ShouldBe("test.pdf");
        evt.Source.ShouldBe("SIARA");
        evt.FileSizeBytes.ShouldBe(1024);
        evt.Format.ShouldBe(FileFormat.Pdf);
        evt.DownloadUrl.ShouldBe("https://test.com/file.pdf");
    }

    /// <summary>
    /// Tests that <see cref="DocumentDownloadedEvent"/> preserves CorrelationId when set.
    /// </summary>
    [Fact]
    public void DocumentDownloadedEvent_WithCorrelationId_PreservesCorrelationId()
    {
        // Arrange
        var correlationId = Guid.NewGuid();

        // Act
        var evt = new DocumentDownloadedEvent
        {
            FileId = Guid.NewGuid(),
            FileName = "test.pdf",
            Source = "SIARA",
            CorrelationId = correlationId
        };

        // Assert
        evt.CorrelationId.ShouldBe(correlationId);
    }

    /// <summary>
    /// Tests that <see cref="QualityAnalysisCompletedEvent"/> initializes with correct EventType.
    /// </summary>
    [Fact]
    public void QualityAnalysisCompletedEvent_Initialize_SetsEventTypeCorrectly()
    {
        // Arrange & Act
        var evt = new QualityAnalysisCompletedEvent
        {
            FileId = Guid.NewGuid(),
            QualityLevel = ImageQualityLevel.Pristine,
            BlurScore = 12.5m,
            NoiseScore = 8.3m,
            ContrastScore = 85.2m,
            SharpnessScore = 92.1m
        };

        // Assert
        evt.EventType.ShouldBe(nameof(QualityAnalysisCompletedEvent));
        evt.EventId.ShouldNotBe(Guid.Empty);
        evt.Timestamp.ShouldBeInRange(DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow);
        evt.FileId.ShouldNotBe(Guid.Empty);
        evt.QualityLevel.ShouldBe(ImageQualityLevel.Pristine);
        evt.BlurScore.ShouldBe(12.5m);
        evt.NoiseScore.ShouldBe(8.3m);
        evt.ContrastScore.ShouldBe(85.2m);
        evt.SharpnessScore.ShouldBe(92.1m);
    }

    /// <summary>
    /// Tests that <see cref="OcrCompletedEvent"/> initializes with correct EventType.
    /// </summary>
    [Fact]
    public void OcrCompletedEvent_Initialize_SetsEventTypeCorrectly()
    {
        // Arrange & Act
        var processingTime = TimeSpan.FromMilliseconds(1500);
        var evt = new OcrCompletedEvent
        {
            FileId = Guid.NewGuid(),
            OcrEngine = "Tesseract",
            Confidence = 95.5m,
            ExtractedTextLength = 2500,
            ProcessingTime = processingTime,
            FallbackTriggered = false
        };

        // Assert
        evt.EventType.ShouldBe(nameof(OcrCompletedEvent));
        evt.EventId.ShouldNotBe(Guid.Empty);
        evt.Timestamp.ShouldBeInRange(DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow);
        evt.FileId.ShouldNotBe(Guid.Empty);
        evt.OcrEngine.ShouldBe("Tesseract");
        evt.Confidence.ShouldBe(95.5m);
        evt.ExtractedTextLength.ShouldBe(2500);
        evt.ProcessingTime.ShouldBe(processingTime);
        evt.FallbackTriggered.ShouldBeFalse();
    }

    /// <summary>
    /// Tests that <see cref="OcrCompletedEvent"/> tracks fallback engine triggering.
    /// </summary>
    [Fact]
    public void OcrCompletedEvent_FallbackTriggered_TracksEngineSwitch()
    {
        // Arrange & Act
        var evt = new OcrCompletedEvent
        {
            FileId = Guid.NewGuid(),
            OcrEngine = "GOT-OCR2",
            Confidence = 78.3m,
            FallbackTriggered = true
        };

        // Assert
        evt.FallbackTriggered.ShouldBeTrue();
        evt.OcrEngine.ShouldBe("GOT-OCR2");
    }

    /// <summary>
    /// Tests that <see cref="ClassificationCompletedEvent"/> initializes with correct EventType.
    /// </summary>
    [Fact]
    public void ClassificationCompletedEvent_Initialize_SetsEventTypeCorrectly()
    {
        // Arrange & Act
        var warnings = new List<string> { "Low confidence on field X", "Missing signature" };
        var evt = new ClassificationCompletedEvent
        {
            FileId = Guid.NewGuid(),
            RequirementTypeId = 5,
            RequirementTypeName = "Aseguramiento",
            Confidence = 92,
            Warnings = warnings,
            RequiresManualReview = false,
            RelationType = "NewRequirement"
        };

        // Assert
        evt.EventType.ShouldBe(nameof(ClassificationCompletedEvent));
        evt.EventId.ShouldNotBe(Guid.Empty);
        evt.Timestamp.ShouldBeInRange(DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow);
        evt.FileId.ShouldNotBe(Guid.Empty);
        evt.RequirementTypeId.ShouldBe(5);
        evt.RequirementTypeName.ShouldBe("Aseguramiento");
        evt.Confidence.ShouldBe(92);
        evt.Warnings.ShouldNotBeNull();
        evt.Warnings.Count.ShouldBe(2);
        evt.RequiresManualReview.ShouldBeFalse();
        evt.RelationType.ShouldBe("NewRequirement");
    }

    /// <summary>
    /// Tests that <see cref="ClassificationCompletedEvent"/> flags manual review when needed.
    /// </summary>
    [Fact]
    public void ClassificationCompletedEvent_LowConfidence_FlagsManualReview()
    {
        // Arrange & Act
        var evt = new ClassificationCompletedEvent
        {
            FileId = Guid.NewGuid(),
            RequirementTypeId = 3,
            RequirementTypeName = "Desbloqueo",
            Confidence = 65,
            RequiresManualReview = true
        };

        // Assert
        evt.RequiresManualReview.ShouldBeTrue();
        evt.Confidence.ShouldBe(65);
    }

    /// <summary>
    /// Tests that <see cref="ConflictDetectedEvent"/> initializes with correct EventType.
    /// </summary>
    [Fact]
    public void ConflictDetectedEvent_Initialize_SetsEventTypeCorrectly()
    {
        // Arrange & Act
        var evt = new ConflictDetectedEvent
        {
            FileId = Guid.NewGuid(),
            FieldName = "InvoiceNumber",
            XmlValue = "INV-12345",
            OcrValue = "INV-12346",
            SimilarityScore = 0.92m,
            ConflictSeverity = "Medium"
        };

        // Assert
        evt.EventType.ShouldBe(nameof(ConflictDetectedEvent));
        evt.EventId.ShouldNotBe(Guid.Empty);
        evt.Timestamp.ShouldBeInRange(DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow);
        evt.FileId.ShouldNotBe(Guid.Empty);
        evt.FieldName.ShouldBe("InvoiceNumber");
        evt.XmlValue.ShouldBe("INV-12345");
        evt.OcrValue.ShouldBe("INV-12346");
        evt.SimilarityScore.ShouldBe(0.92m);
        evt.ConflictSeverity.ShouldBe("Medium");
    }

    /// <summary>
    /// Tests that <see cref="ConflictDetectedEvent"/> tracks high severity conflicts.
    /// </summary>
    [Fact]
    public void ConflictDetectedEvent_HighSeverity_TracksLowSimilarity()
    {
        // Arrange & Act
        var evt = new ConflictDetectedEvent
        {
            FileId = Guid.NewGuid(),
            FieldName = "TotalAmount",
            XmlValue = "1500.00",
            OcrValue = "15000.00",
            SimilarityScore = 0.45m,
            ConflictSeverity = "High"
        };

        // Assert
        evt.ConflictSeverity.ShouldBe("High");
        evt.SimilarityScore.ShouldBe(0.45m);
    }

    /// <summary>
    /// Tests that <see cref="DocumentFlaggedForReviewEvent"/> initializes with correct EventType.
    /// </summary>
    [Fact]
    public void DocumentFlaggedForReviewEvent_Initialize_SetsEventTypeCorrectly()
    {
        // Arrange & Act
        var reasons = new List<string> { "Low OCR confidence", "Missing required fields" };
        var evt = new DocumentFlaggedForReviewEvent
        {
            FileId = Guid.NewGuid(),
            Reasons = reasons,
            Priority = "High"
        };

        // Assert
        evt.EventType.ShouldBe(nameof(DocumentFlaggedForReviewEvent));
        evt.EventId.ShouldNotBe(Guid.Empty);
        evt.Timestamp.ShouldBeInRange(DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow);
        evt.FileId.ShouldNotBe(Guid.Empty);
        evt.Reasons.ShouldNotBeNull();
        evt.Reasons.Count.ShouldBe(2);
        evt.Priority.ShouldBe("High");
    }

    /// <summary>
    /// Tests that <see cref="DocumentFlaggedForReviewEvent"/> supports defensive intelligence philosophy.
    /// Defensive Intelligence: Flag for review instead of rejecting.
    /// </summary>
    [Fact]
    public void DocumentFlaggedForReviewEvent_DefensiveIntelligence_FlagsInsteadOfRejects()
    {
        // Arrange & Act
        var evt = new DocumentFlaggedForReviewEvent
        {
            FileId = Guid.NewGuid(),
            Reasons = new List<string> { "Uncertain classification" },
            Priority = "Normal"
        };

        // Assert - Event exists (not rejected), just flagged for review
        evt.EventType.ShouldBe(nameof(DocumentFlaggedForReviewEvent));
        evt.Priority.ShouldBe("Normal");
    }

    /// <summary>
    /// Tests that <see cref="DocumentProcessingCompletedEvent"/> initializes with correct EventType.
    /// </summary>
    [Fact]
    public void DocumentProcessingCompletedEvent_Initialize_SetsEventTypeCorrectly()
    {
        // Arrange & Act
        var totalTime = TimeSpan.FromSeconds(45);
        var evt = new DocumentProcessingCompletedEvent
        {
            FileId = Guid.NewGuid(),
            TotalProcessingTime = totalTime,
            AutoProcessed = true
        };

        // Assert
        evt.EventType.ShouldBe(nameof(DocumentProcessingCompletedEvent));
        evt.EventId.ShouldNotBe(Guid.Empty);
        evt.Timestamp.ShouldBeInRange(DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow);
        evt.FileId.ShouldNotBe(Guid.Empty);
        evt.TotalProcessingTime.ShouldBe(totalTime);
        evt.AutoProcessed.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that <see cref="DocumentProcessingCompletedEvent"/> tracks auto-processing goal (80%+).
    /// </summary>
    [Fact]
    public void DocumentProcessingCompletedEvent_AutoProcessed_TracksSuccessGoal()
    {
        // Arrange & Act
        var autoProcessedEvent = new DocumentProcessingCompletedEvent
        {
            FileId = Guid.NewGuid(),
            TotalProcessingTime = TimeSpan.FromSeconds(30),
            AutoProcessed = true
        };

        var manualReviewEvent = new DocumentProcessingCompletedEvent
        {
            FileId = Guid.NewGuid(),
            TotalProcessingTime = TimeSpan.FromSeconds(120),
            AutoProcessed = false
        };

        // Assert
        autoProcessedEvent.AutoProcessed.ShouldBeTrue();
        manualReviewEvent.AutoProcessed.ShouldBeFalse();
    }

    /// <summary>
    /// Tests that <see cref="ProcessingErrorEvent"/> initializes with correct EventType.
    /// </summary>
    [Fact]
    public void ProcessingErrorEvent_Initialize_SetsEventTypeCorrectly()
    {
        // Arrange & Act
        var evt = new ProcessingErrorEvent
        {
            FileId = Guid.NewGuid(),
            ErrorMessage = "Failed to process PDF",
            StackTrace = "at System.IO.File.ReadAllBytes(String path)",
            Component = "OCR"
        };

        // Assert
        evt.EventType.ShouldBe(nameof(ProcessingErrorEvent));
        evt.EventId.ShouldNotBe(Guid.Empty);
        evt.Timestamp.ShouldBeInRange(DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow);
        evt.FileId.ShouldNotBe(Guid.Empty);
        evt.ErrorMessage.ShouldBe("Failed to process PDF");
        evt.StackTrace.ShouldBe("at System.IO.File.ReadAllBytes(String path)");
        evt.Component.ShouldBe("OCR");
    }

    /// <summary>
    /// Tests that <see cref="ProcessingErrorEvent"/> supports system-level errors with null FileId.
    /// Defensive Intelligence: System continues even when errors occur.
    /// </summary>
    [Fact]
    public void ProcessingErrorEvent_SystemLevelError_AllowsNullFileId()
    {
        // Arrange & Act
        var evt = new ProcessingErrorEvent
        {
            FileId = null,
            ErrorMessage = "Database connection timeout",
            Component = "Storage"
        };

        // Assert - System-level error (no specific file)
        evt.FileId.ShouldBeNull();
        evt.ErrorMessage.ShouldBe("Database connection timeout");
    }

    /// <summary>
    /// Tests that all event types inherit from <see cref="DomainEvent"/> and have unique EventIds.
    /// </summary>
    [Fact]
    public void AllEvents_InheritFromDomainEvent_AndHaveUniqueEventIds()
    {
        // Arrange & Act
        var events = new DomainEvent[]
        {
            new DocumentDownloadedEvent { FileId = Guid.NewGuid() },
            new QualityAnalysisCompletedEvent { FileId = Guid.NewGuid() },
            new OcrCompletedEvent { FileId = Guid.NewGuid() },
            new ClassificationCompletedEvent { FileId = Guid.NewGuid() },
            new ConflictDetectedEvent { FileId = Guid.NewGuid() },
            new DocumentFlaggedForReviewEvent { FileId = Guid.NewGuid() },
            new DocumentProcessingCompletedEvent { FileId = Guid.NewGuid() },
            new ProcessingErrorEvent { FileId = Guid.NewGuid() }
        };

        // Assert - All events should be DomainEvent instances
        events.ShouldAllBe(e => e is DomainEvent);

        // Assert - All EventIds should be unique
        var eventIds = events.Select(e => e.EventId).ToList();
        eventIds.Distinct().Count().ShouldBe(eventIds.Count);

        // Assert - All EventIds should not be empty
        eventIds.ShouldAllBe(id => id != Guid.Empty);

        // Assert - All Timestamps should be recent
        events.ShouldAllBe(e => e.Timestamp >= DateTime.UtcNow.AddSeconds(-5) && e.Timestamp <= DateTime.UtcNow);
    }

    /// <summary>
    /// Tests that record equality works correctly for events with same data.
    /// </summary>
    [Fact]
    public void Events_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var fileId = Guid.NewGuid();

        var evt1 = new DocumentDownloadedEvent
        {
            FileId = fileId,
            FileName = "test.pdf",
            Source = "SIARA"
        };

        var evt2 = new DocumentDownloadedEvent
        {
            FileId = fileId,
            FileName = "test.pdf",
            Source = "SIARA"
        };

        // Act & Assert - Events with different EventIds should not be equal
        (evt1 == evt2).ShouldBeFalse();
        evt1.EventId.ShouldNotBe(evt2.EventId);

        // Events with same FileId but different EventIds are different events
        evt1.FileId.ShouldBe(evt2.FileId);
    }

    /// <summary>
    /// Tests that events support with-expression for creating modified copies.
    /// </summary>
    [Fact]
    public void Events_WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var original = new DocumentDownloadedEvent
        {
            FileId = Guid.NewGuid(),
            FileName = "original.pdf",
            Source = "SIARA",
            FileSizeBytes = 1024
        };

        // Act - Create modified copy with different file size
        var modified = original with { FileSizeBytes = 2048 };

        // Assert - Original unchanged
        original.FileSizeBytes.ShouldBe(1024);

        // Assert - Modified has new value
        modified.FileSizeBytes.ShouldBe(2048);

        // Assert - Other properties copied
        modified.FileId.ShouldBe(original.FileId);
        modified.FileName.ShouldBe(original.FileName);
        modified.Source.ShouldBe(original.Source);

        // Assert - EventId and Timestamp are also copied (not regenerated)
        modified.EventId.ShouldBe(original.EventId);
        modified.Timestamp.ShouldBe(original.Timestamp);
    }
}
