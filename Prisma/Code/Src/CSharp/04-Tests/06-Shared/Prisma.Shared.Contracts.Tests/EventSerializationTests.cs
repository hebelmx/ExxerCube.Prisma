using Prisma.Shared.Contracts;

namespace Prisma.Shared.Contracts.Tests;

/// <summary>
/// ITDD Stage 1: Contract tests for event serialization round-trip (JSON).
/// Proves Liskov: Any JSON serializer should preserve Pascal case and correlation IDs.
/// </summary>
public sealed class EventSerializationTests
{
    [Fact]
    public void DocumentDownloadedEvent_SerializesAndDeserializes_PreservesPascalCase()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        var originalEvent = new DocumentDownloadedEvent(
            FileId: fileId,
            FileName: "test-document.pdf",
            Source: "SIARA",
            FileSizeBytes: 1024 * 500, // 500 KB
            Path: "2024/12/02/test-document.pdf",
            JournalPath: "/journals/2024-12-02.json",
            CorrelationId: correlationId,
            Timestamp: timestamp
        );

        // Act - Serialize to JSON (System.Text.Json defaults to camelCase, but records preserve property names)
        var json = JsonSerializer.Serialize(originalEvent);

        // Assert - Verify Pascal case preserved in JSON (record types preserve property casing by default)
        json.ShouldContain("\"FileId\":");
        json.ShouldContain("\"FileName\":");
        json.ShouldContain("\"CorrelationId\":");

        // Act - Deserialize back
        var deserializedEvent = JsonSerializer.Deserialize<DocumentDownloadedEvent>(json);

        // Assert - Liskov: Round-trip preserves all data
        deserializedEvent.ShouldNotBeNull();
        deserializedEvent.FileId.ShouldBe(fileId);
        deserializedEvent.FileName.ShouldBe("test-document.pdf");
        deserializedEvent.Source.ShouldBe("SIARA");
        deserializedEvent.FileSizeBytes.ShouldBe(512000);
        deserializedEvent.Path.ShouldBe("2024/12/02/test-document.pdf");
        deserializedEvent.JournalPath.ShouldBe("/journals/2024-12-02.json");
        deserializedEvent.CorrelationId.ShouldBe(correlationId);
        deserializedEvent.Timestamp.ShouldBe(timestamp);
    }

    [Fact]
    public void DocumentDownloadedEvent_CorrelationId_SurvivesRoundTrip()
    {
        // Arrange - Correlation ID is critical for end-to-end tracing
        var correlationId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        var originalEvent = new DocumentDownloadedEvent(
            FileId: Guid.NewGuid(),
            FileName: "test.pdf",
            Source: "SIARA",
            FileSizeBytes: 1000,
            Path: "2024/12/02/test.pdf",
            JournalPath: "/journals/2024-12-02.json",
            CorrelationId: correlationId,
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act
        var json = JsonSerializer.Serialize(originalEvent);
        var deserializedEvent = JsonSerializer.Deserialize<DocumentDownloadedEvent>(json);

        // Assert - Liskov: Correlation ID MUST be preserved exactly
        deserializedEvent.ShouldNotBeNull();
        deserializedEvent.CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public void WorkerHeartbeat_SerializesAndDeserializes_PreservesPascalCase()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var lastEventTime = DateTime.UtcNow.AddMinutes(-5);
        var originalHeartbeat = new WorkerHeartbeat(
            WorkerId: "orion-worker-001",
            WorkerName: "Orion.Worker.Instance1",
            Timestamp: timestamp,
            Status: WorkerStatus.Processing,
            DocumentsProcessed: 5,
            LastEventTime: lastEventTime,
            HealthEndpoint: "http://localhost:5000/health"
        );

        // Act - Serialize to JSON
        var json = JsonSerializer.Serialize(originalHeartbeat);

        // Assert - Verify Pascal case preserved (record types preserve property casing)
        json.ShouldContain("\"WorkerId\":");
        json.ShouldContain("\"WorkerName\":");
        json.ShouldContain("\"Timestamp\":");
        json.ShouldContain("\"Status\":");
        json.ShouldContain("\"DocumentsProcessed\":");

        // Act - Deserialize back
        var deserializedHeartbeat = JsonSerializer.Deserialize<WorkerHeartbeat>(json);

        // Assert - Liskov: Round-trip preserves all data
        deserializedHeartbeat.ShouldNotBeNull();
        deserializedHeartbeat.WorkerId.ShouldBe("orion-worker-001");
        deserializedHeartbeat.WorkerName.ShouldBe("Orion.Worker.Instance1");
        deserializedHeartbeat.Timestamp.ShouldBe(timestamp);
        deserializedHeartbeat.Status.ShouldBe(WorkerStatus.Processing);
        deserializedHeartbeat.DocumentsProcessed.ShouldBe(5);
        deserializedHeartbeat.LastEventTime.ShouldBe(lastEventTime);
        deserializedHeartbeat.HealthEndpoint.ShouldBe("http://localhost:5000/health");
    }

    [Fact]
    public void WorkerHeartbeat_WithNullLastEventTime_SerializesCorrectly()
    {
        // Arrange - LastEventTime and HealthEndpoint are nullable
        var timestamp = DateTime.UtcNow;
        var originalHeartbeat = new WorkerHeartbeat(
            WorkerId: "athena-worker-002",
            WorkerName: "Athena.Worker.Instance2",
            Timestamp: timestamp,
            Status: WorkerStatus.Idle,
            DocumentsProcessed: 0,
            LastEventTime: null,
            HealthEndpoint: null
        );

        // Act
        var json = JsonSerializer.Serialize(originalHeartbeat);
        var deserializedHeartbeat = JsonSerializer.Deserialize<WorkerHeartbeat>(json);

        // Assert - Liskov: null values must round-trip correctly
        deserializedHeartbeat.ShouldNotBeNull();
        deserializedHeartbeat.WorkerId.ShouldBe("athena-worker-002");
        deserializedHeartbeat.WorkerName.ShouldBe("Athena.Worker.Instance2");
        deserializedHeartbeat.Status.ShouldBe(WorkerStatus.Idle);
        deserializedHeartbeat.DocumentsProcessed.ShouldBe(0);
        deserializedHeartbeat.LastEventTime.ShouldBeNull();
        deserializedHeartbeat.HealthEndpoint.ShouldBeNull();
    }

    [Fact]
    public void DocumentDownloadedEvent_WithSpecialCharacters_SerializesCorrectly()
    {
        // Arrange - Real-world filenames may have special characters
        var originalEvent = new DocumentDownloadedEvent(
            FileId: Guid.NewGuid(),
            FileName: "Oficio-123_A/2024 (Copia).pdf",
            Source: "SIARA Portal",
            FileSizeBytes: 2048,
            Path: "2024/12/02/Oficio-123_A-2024-(Copia).pdf",
            JournalPath: "/journals/2024-12-02.json",
            CorrelationId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act
        var json = JsonSerializer.Serialize(originalEvent);
        var deserializedEvent = JsonSerializer.Deserialize<DocumentDownloadedEvent>(json);

        // Assert - Liskov: Special characters must survive round-trip
        deserializedEvent.ShouldNotBeNull();
        deserializedEvent.FileName.ShouldBe("Oficio-123_A/2024 (Copia).pdf");
        deserializedEvent.Source.ShouldBe("SIARA Portal");
    }

    [Fact]
    public void DateTimeOffset_PreservesTimezone_ThroughSerialization()
    {
        // Arrange - DateTimeOffset must preserve timezone info
        var mexicoCityOffset = new DateTimeOffset(2024, 12, 2, 14, 30, 0, TimeSpan.FromHours(-6));
        var originalEvent = new DocumentDownloadedEvent(
            FileId: Guid.NewGuid(),
            FileName: "test.pdf",
            Source: "SIARA",
            FileSizeBytes: 1000,
            Path: "2024/12/02/test.pdf",
            JournalPath: "/journals/2024-12-02.json",
            CorrelationId: Guid.NewGuid(),
            Timestamp: mexicoCityOffset
        );

        // Act
        var json = JsonSerializer.Serialize(originalEvent);
        var deserializedEvent = JsonSerializer.Deserialize<DocumentDownloadedEvent>(json);

        // Assert - Liskov: Timezone info must be preserved exactly
        deserializedEvent.ShouldNotBeNull();
        deserializedEvent.Timestamp.ShouldBe(mexicoCityOffset);
        deserializedEvent.Timestamp.Offset.ShouldBe(TimeSpan.FromHours(-6));
    }

    [Fact]
    public void LargeFileSizeBytes_SerializesCorrectly()
    {
        // Arrange - Test with large file sizes (>2GB)
        const long largeFileSize = 3L * 1024 * 1024 * 1024; // 3 GB
        var originalEvent = new DocumentDownloadedEvent(
            FileId: Guid.NewGuid(),
            FileName: "large-archive.pdf",
            Source: "SIARA",
            FileSizeBytes: largeFileSize,
            Path: "2024/12/02/large-archive.pdf",
            JournalPath: "/journals/2024-12-02.json",
            CorrelationId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act
        var json = JsonSerializer.Serialize(originalEvent);
        var deserializedEvent = JsonSerializer.Deserialize<DocumentDownloadedEvent>(json);

        // Assert - Liskov: Long values must serialize without overflow
        deserializedEvent.ShouldNotBeNull();
        deserializedEvent.FileSizeBytes.ShouldBe(largeFileSize);
    }
}
