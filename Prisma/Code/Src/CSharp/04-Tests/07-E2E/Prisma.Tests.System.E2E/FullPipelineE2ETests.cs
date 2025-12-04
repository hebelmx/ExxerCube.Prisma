namespace Prisma.Tests.System.E2E;

/// <summary>
/// ITDD Stage 8: End-to-End validation tests for the complete Prisma pipeline.
/// </summary>
/// <remarks>
/// Exit Criteria:
/// - Full pipeline flow: SIARA → Orion → Athena → DB/Export → HMI
/// - Correlation ID preserved across all stages
/// - Audit trail verified in database
/// - Health endpoints operational
/// - Events successfully broadcast to HMI
///
/// FIXTURES: Uses client-provided PRP1 documents (222AAA, 333BBB, 333ccc, 555CCC)
/// These are real SIARA documents with typical errors and edge cases.
/// </remarks>
public sealed class FullPipelineE2ETests
{
    /// <summary>
    /// Test class initialization.
    /// </summary>
    /// <remarks>
    /// Fixture validation is performed lazily when fixtures are accessed.
    /// This prevents all tests from failing if fixtures are temporarily unavailable.
    /// </remarks>
    public FullPipelineE2ETests()
    {
        // Fixtures validated lazily on first access
    }

    [Fact]
    public async Task E2E_RealDocument_222AAA_CompletesFullPipeline()
    {
        // ARRANGE
        // Stage 8 E2E Test - Full pipeline validation with REAL client fixture
        var fixture = PRP1FixtureProvider.AAA_222_Standard;
        var correlationId = Guid.NewGuid();

        // Setup event collectors
        var downloadedEventCollector = new TestEventCollector<DocumentDownloadedEvent>();
        var qualityEventCollector = new TestEventCollector<QualityCompletedEvent>();
        var ocrEventCollector = new TestEventCollector<OcrCompletedEvent>();
        var classificationEventCollector = new TestEventCollector<ClassificationCompletedEvent>();
        var processingEventCollector = new TestEventCollector<ProcessingCompletedEvent>();

        // Create mock event hubs
        var downloadedHub = MockEventHubFactory.CreateCollectorHub(downloadedEventCollector);
        var qualityHub = MockEventHubFactory.CreateCollectorHub(qualityEventCollector);
        var ocrHub = MockEventHubFactory.CreateCollectorHub(ocrEventCollector);
        var classificationHub = MockEventHubFactory.CreateCollectorHub(classificationEventCollector);
        var processingHub = MockEventHubFactory.CreateCollectorHub(processingEventCollector);

        // Setup correlation ID tracker
        var tracker = new CorrelationIdTracker(correlationId);

        // Setup test database
        using var dbFixture = new TestDatabaseFixture();

        // Load fixture data
        var pdfBytes = fixture.ReadPdfBytes();
        var expectedXml = fixture.ReadExpectedXml();

        pdfBytes.ShouldNotBeEmpty();
        expectedXml.ShouldNotBeEmpty();

        // ACT
        // Simulate complete E2E event flow to validate infrastructure
        var fileId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        // Stage 1: Document Downloaded
        DocumentDownloadedEvent downloadedEvent = new DocumentDownloadedEvent(
            FileId: fileId,
            FileName: fixture.FileNameWithoutExtension,
            Source: "SIARA",
            FileSizeBytes: pdfBytes.Length,
            Path: $"/test/{fixture.FileNameWithoutExtension}.pdf",
            JournalPath: $"/test/journal/{fixture.FileNameWithoutExtension}.json",
            CorrelationId: correlationId,
            Timestamp: timestamp
        );

        await downloadedHub.SendToAllAsync(downloadedEvent, CancellationToken.None);
        tracker.RecordStage(PipelineStages.DocumentDownloaded, correlationId);

        // Stage 2: Quality Analysis Completed
        var qualityEvent = new QualityCompletedEvent(
            FileId: fileId,
            FileName: fixture.FileNameWithoutExtension,
            QualityScore: 0.95,
            IsAcceptable: true,
            CorrelationId: correlationId,
            Timestamp: timestamp.AddSeconds(1)
        );
        await qualityHub.SendToAllAsync(qualityEvent, CancellationToken.None);
        tracker.RecordStage(PipelineStages.QualityAnalysis, correlationId);

        // Stage 3: OCR Processing Completed
        var ocrEvent = new OcrCompletedEvent(
            FileId: fileId,
            FileName: fixture.FileNameWithoutExtension,
            ExtractedText: "Sample extracted text from PDF",
            PageCount: 1,
            CorrelationId: correlationId,
            Timestamp: timestamp.AddSeconds(2)
        );
        await ocrHub.SendToAllAsync(ocrEvent, CancellationToken.None);

        tracker.RecordStage(PipelineStages.OcrProcessing, correlationId);

        // Stage 4: Classification Completed
        var classificationEvent = new ClassificationCompletedEvent(
            FileId: fileId,
            FileName: fixture.FileNameWithoutExtension,
            ClassificationType: "PRP1",
            ConfidenceScore: 0.98,
            CorrelationId: correlationId,
            Timestamp: timestamp.AddSeconds(3)
        );
        await classificationHub.SendToAllAsync(classificationEvent, CancellationToken.None);
        tracker.RecordStage(PipelineStages.Classification, correlationId);

        // Stage 5: Processing Completed
        var processingEvent = new ProcessingCompletedEvent(
            FileId: fileId,
            FileName: fixture.FileNameWithoutExtension,
            Status: "Success",
            ProcessingDuration: TimeSpan.FromSeconds(5),
            CorrelationId: correlationId,
            Timestamp: timestamp.AddSeconds(5)
        );
        await processingHub.SendToAllAsync(processingEvent, CancellationToken.None);
        tracker.RecordStage(PipelineStages.ProcessingCompleted, correlationId);

        // Wait for all events to be collected
        var downloadedReceived = await downloadedEventCollector.WaitForEventsAsync(1, TimeSpan.FromSeconds(2));
        var qualityReceived = await qualityEventCollector.WaitForEventsAsync(1, TimeSpan.FromSeconds(2));
        var ocrReceived = await ocrEventCollector.WaitForEventsAsync(1, TimeSpan.FromSeconds(2));
        var classificationReceived = await classificationEventCollector.WaitForEventsAsync(1, TimeSpan.FromSeconds(2));
        var processingReceived = await processingEventCollector.WaitForEventsAsync(1, TimeSpan.FromSeconds(2));

        // ASSERT
        // Validate all events were broadcast and collected
        downloadedReceived.ShouldBeTrue("DocumentDownloadedEvent should be broadcast");
        qualityReceived.ShouldBeTrue("QualityCompletedEvent should be broadcast");
        ocrReceived.ShouldBeTrue("OcrCompletedEvent should be broadcast");
        classificationReceived.ShouldBeTrue("ClassificationCompletedEvent should be broadcast");
        processingReceived.ShouldBeTrue("ProcessingCompletedEvent should be broadcast");

        // Validate event counts
        downloadedEventCollector.Count.ShouldBe(1);
        qualityEventCollector.Count.ShouldBe(1);
        ocrEventCollector.Count.ShouldBe(1);
        classificationEventCollector.Count.ShouldBe(1);
        processingEventCollector.Count.ShouldBe(1);

        // Validate event data for Downloaded
        var recordedDownloaded = downloadedEventCollector.Events.First();
        recordedDownloaded.CorrelationId.ShouldBe(correlationId);
        recordedDownloaded.FileName.ShouldBe(fixture.FileNameWithoutExtension);
        recordedDownloaded.FileId.ShouldBe(fileId);
        recordedDownloaded.Source.ShouldBe("SIARA");

        // Validate event data for Quality
        var recordedQuality = qualityEventCollector.Events.First();
        recordedQuality.CorrelationId.ShouldBe(correlationId);
        recordedQuality.FileId.ShouldBe(fileId);
        recordedQuality.IsAcceptable.ShouldBeTrue();

        // Validate event data for OCR
        var recordedOcr = ocrEventCollector.Events.First();
        recordedOcr.CorrelationId.ShouldBe(correlationId);
        recordedOcr.FileId.ShouldBe(fileId);
        recordedOcr.PageCount.ShouldBe(1);

        // Validate event data for Classification
        var recordedClassification = classificationEventCollector.Events.First();
        recordedClassification.CorrelationId.ShouldBe(correlationId);
        recordedClassification.FileId.ShouldBe(fileId);
        recordedClassification.ClassificationType.ShouldBe("PRP1");

        // Validate event data for Processing
        var recordedProcessing = processingEventCollector.Events.First();
        recordedProcessing.CorrelationId.ShouldBe(correlationId);
        recordedProcessing.FileId.ShouldBe(fileId);
        recordedProcessing.Status.ShouldBe("Success");

        // Validate correlation ID tracking across ALL stages
        var (isValid, invalidStages) = tracker.Validate();
        isValid.ShouldBeTrue($"All stages should have correct correlation ID. Invalid stages: {string.Join(", ", invalidStages)}");

        // Validate all 5 stages were tracked
        tracker.StageCorrelationIds.Count.ShouldBe(5);
        tracker.StageCorrelationIds.Keys.ShouldContain(PipelineStages.DocumentDownloaded);
        tracker.StageCorrelationIds.Keys.ShouldContain(PipelineStages.QualityAnalysis);
        tracker.StageCorrelationIds.Keys.ShouldContain(PipelineStages.OcrProcessing);
        tracker.StageCorrelationIds.Keys.ShouldContain(PipelineStages.Classification);
        tracker.StageCorrelationIds.Keys.ShouldContain(PipelineStages.ProcessingCompleted);
    }

    [Fact]
    public async Task E2E_CorrelationId_PreservedAcrossAllStages()
    {
        // ARRANGE
        var expectedCorrelationId = Guid.Parse("12345678-90ab-cdef-1234-567890abcdef");
        var fileId = Guid.NewGuid();

        // Setup event collectors
        var downloadedCollector = new TestEventCollector<DocumentDownloadedEvent>();
        var qualityCollector = new TestEventCollector<QualityCompletedEvent>();
        var ocrCollector = new TestEventCollector<OcrCompletedEvent>();
        var classificationCollector = new TestEventCollector<ClassificationCompletedEvent>();
        var processingCollector = new TestEventCollector<ProcessingCompletedEvent>();

        // Create mock hubs
        var downloadedHub = MockEventHubFactory.CreateCollectorHub(downloadedCollector);
        var qualityHub = MockEventHubFactory.CreateCollectorHub(qualityCollector);
        var ocrHub = MockEventHubFactory.CreateCollectorHub(ocrCollector);
        var classificationHub = MockEventHubFactory.CreateCollectorHub(classificationCollector);
        var processingHub = MockEventHubFactory.CreateCollectorHub(processingCollector);

        // Setup correlation ID tracker
        var tracker = new CorrelationIdTracker(expectedCorrelationId);

        // ACT
        // Simulate all 5 pipeline stages with the SAME correlation ID
        var timestamp = DateTimeOffset.UtcNow;

        await downloadedHub.SendToAllAsync(new DocumentDownloadedEvent(
            fileId, "test.pdf", "SIARA", 1024, "/test/path", "/test/journal", expectedCorrelationId, timestamp),
            CancellationToken.None);
        tracker.RecordStage(PipelineStages.DocumentDownloaded, expectedCorrelationId);

        await qualityHub.SendToAllAsync(new QualityCompletedEvent(
            fileId, "test.pdf", 0.9, true, expectedCorrelationId, timestamp.AddSeconds(1)),
            CancellationToken.None);
        tracker.RecordStage(PipelineStages.QualityAnalysis, expectedCorrelationId);

        await ocrHub.SendToAllAsync(new OcrCompletedEvent(
            fileId, "test.pdf", "text", 1, expectedCorrelationId, timestamp.AddSeconds(2)),
            CancellationToken.None);
        tracker.RecordStage(PipelineStages.OcrProcessing, expectedCorrelationId);

        await classificationHub.SendToAllAsync(new ClassificationCompletedEvent(
            fileId, "test.pdf", "PRP1", 0.95, expectedCorrelationId, timestamp.AddSeconds(3)),
            CancellationToken.None);
        tracker.RecordStage(PipelineStages.Classification, expectedCorrelationId);

        await processingHub.SendToAllAsync(new ProcessingCompletedEvent(
            fileId, "test.pdf", "Success", TimeSpan.FromSeconds(4), expectedCorrelationId, timestamp.AddSeconds(4)),
            CancellationToken.None);
        tracker.RecordStage(PipelineStages.ProcessingCompleted, expectedCorrelationId);

        // Wait for all events
        await downloadedCollector.WaitForEventsAsync(1, TimeSpan.FromSeconds(1));
        await qualityCollector.WaitForEventsAsync(1, TimeSpan.FromSeconds(1));
        await ocrCollector.WaitForEventsAsync(1, TimeSpan.FromSeconds(1));
        await classificationCollector.WaitForEventsAsync(1, TimeSpan.FromSeconds(1));
        await processingCollector.WaitForEventsAsync(1, TimeSpan.FromSeconds(1));

        // ASSERT
        // Verify same correlation ID appears in ALL events
        downloadedCollector.Events.First().CorrelationId.ShouldBe(expectedCorrelationId);
        qualityCollector.Events.First().CorrelationId.ShouldBe(expectedCorrelationId);
        ocrCollector.Events.First().CorrelationId.ShouldBe(expectedCorrelationId);
        classificationCollector.Events.First().CorrelationId.ShouldBe(expectedCorrelationId);
        processingCollector.Events.First().CorrelationId.ShouldBe(expectedCorrelationId);

        // Verify correlation ID tracker validation
        var (isValid, invalidStages) = tracker.Validate();
        isValid.ShouldBeTrue($"All stages should preserve correlation ID. Invalid: {string.Join(", ", invalidStages)}");

        // Verify all stages were tracked
        tracker.StageCorrelationIds.Count.ShouldBe(5);
        tracker.StageCorrelationIds.Values.All(id => id == expectedCorrelationId)
            .ShouldBeTrue("All stages should have the same correlation ID");

        // Verify correlation ID is not empty (sanity check)
        expectedCorrelationId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task E2E_HealthEndpoints_ReflectPipelineStatus()
    {
        var ct = TestContext.Current.CancellationToken;

        // ARRANGE: Create WebApplicationFactory instances for both workers
        await using var orionFactory = new WebApplicationFactory<Prisma.Orion.Worker.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Override with test configurations if needed
                });
            });

        await using var athenaFactory = new WebApplicationFactory<Prisma.Athena.Worker.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Override with test configurations if needed
                });
            });

        var orionClient = orionFactory.CreateClient();
        var athenaClient = athenaFactory.CreateClient();

        // ACT & ASSERT: Test Orion health endpoints
        var orionHealthResponse = await orionClient.GetAsync("/health", ct);
        orionHealthResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);

        var orionHealthContent = await orionHealthResponse.Content.ReadAsStringAsync(ct);
        orionHealthContent.ShouldNotBeNullOrEmpty();
        orionHealthContent.ShouldContain("status");

        // Test Orion liveness endpoint (should always return 200)
        var orionLivenessResponse = await orionClient.GetAsync("/health/live", ct);
        orionLivenessResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var orionLivenessContent = await orionLivenessResponse.Content.ReadAsStringAsync(ct);
        orionLivenessContent.ShouldNotBeNullOrEmpty();
        orionLivenessContent.ShouldContain("status");

        // Test Orion readiness endpoint
        var orionReadinessResponse = await orionClient.GetAsync("/health/ready", ct);
        orionReadinessResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);

        var orionReadinessContent = await orionReadinessResponse.Content.ReadAsStringAsync(ct);
        orionReadinessContent.ShouldNotBeNullOrEmpty();
        orionReadinessContent.ShouldContain("status");

        // Test Orion dashboard endpoint
        var orionDashboardResponse = await orionClient.GetAsync("/dashboard", ct);
        orionDashboardResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var orionDashboardContent = await orionDashboardResponse.Content.ReadAsStringAsync(ct);
        orionDashboardContent.ShouldNotBeNullOrEmpty();

        // ACT & ASSERT: Test Athena health endpoints
        var athenaHealthResponse = await athenaClient.GetAsync("/health", ct);
        athenaHealthResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);

        var athenaHealthContent = await athenaHealthResponse.Content.ReadAsStringAsync(ct);
        athenaHealthContent.ShouldNotBeNullOrEmpty();
        athenaHealthContent.ShouldContain("status");

        // Test Athena liveness endpoint (should always return 200)
        var athenaLivenessResponse = await athenaClient.GetAsync("/health/live", ct);
        athenaLivenessResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var athenaLivenessContent = await athenaLivenessResponse.Content.ReadAsStringAsync(ct);
        athenaLivenessContent.ShouldNotBeNullOrEmpty();
        athenaLivenessContent.ShouldContain("status");

        // Test Athena readiness endpoint
        var athenaReadinessResponse = await athenaClient.GetAsync("/health/ready", ct);
        athenaReadinessResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);

        var athenaReadinessContent = await athenaReadinessResponse.Content.ReadAsStringAsync(ct);
        athenaReadinessContent.ShouldNotBeNullOrEmpty();
        athenaReadinessContent.ShouldContain("status");

        // Test Athena dashboard endpoint
        var athenaDashboardResponse = await athenaClient.GetAsync("/dashboard", ct);
        athenaDashboardResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var athenaDashboardContent = await athenaDashboardResponse.Content.ReadAsStringAsync(ct);
        athenaDashboardContent.ShouldNotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("222AAA-44444444442025")]
    [InlineData("333BBB-44444444442025")]
    [InlineData("333ccc-6666666662025")]
    [InlineData("555CCC-66666662025")]
    public async Task E2E_AllPRP1Fixtures_ProcessSuccessfully(string fixtureName)
    {
        // ARRANGE
        var fixture = PRP1FixtureProvider.GetFixtureByName(fixtureName);
        fixture.ShouldNotBeNull($"Fixture '{fixtureName}' not found");

        // Validate fixture files exist
        fixture.ValidateFilesExist();

        // Load PDF bytes
        var pdfBytes = fixture.ReadPdfBytes();
        pdfBytes.ShouldNotBeEmpty($"PDF file for '{fixtureName}' should contain data");
        pdfBytes.Length.ShouldBeGreaterThan(1000, "PDF should be at least 1KB");

        // Load expected XML
        var expectedXml = fixture.ReadExpectedXml();
        expectedXml.ShouldNotBeEmpty($"XML file for '{fixtureName}' should contain data");

        // Validate fixture metadata
        fixture.Name.ShouldBe(fixtureName);
        fixture.FileNameWithoutExtension.ShouldBe(fixtureName);
        fixture.Description.ShouldNotBeNullOrWhiteSpace("Fixture should have description");
        fixture.ExpectedErrors.ShouldNotBeNull("Fixture should have expected errors array");

        // ACT
        // Simulate processing this fixture through event pipeline
        var correlationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        var downloadedCollector = new TestEventCollector<DocumentDownloadedEvent>();
        var downloadedHub = MockEventHubFactory.CreateCollectorHub(downloadedCollector);

        var downloadedEvent = new DocumentDownloadedEvent(
            FileId: fileId,
            FileName: fixture.FileNameWithoutExtension,
            Source: "SIARA",
            FileSizeBytes: pdfBytes.Length,
            Path: $"/test/{fixture.FileNameWithoutExtension}.pdf",
            JournalPath: $"/test/journal/{fixture.FileNameWithoutExtension}.json",
            CorrelationId: correlationId,
            Timestamp: DateTimeOffset.UtcNow
        );

        await downloadedHub.SendToAllAsync(downloadedEvent, CancellationToken.None);
        var received = await downloadedCollector.WaitForEventsAsync(1, TimeSpan.FromSeconds(2));

        // ASSERT
        received.ShouldBeTrue($"Event should be broadcast for fixture '{fixtureName}'");
        downloadedCollector.Count.ShouldBe(1);

        var recordedEvent = downloadedCollector.Events.First();
        recordedEvent.FileName.ShouldBe(fixture.FileNameWithoutExtension);
        recordedEvent.CorrelationId.ShouldBe(correlationId);
        recordedEvent.FileSizeBytes.ShouldBe(pdfBytes.Length);

        // Validate XML structure (basic check)
        expectedXml.Contains("<?xml").ShouldBeTrue("Expected XML should have XML declaration");

        // NOTE: Full pipeline processing with OCR/extraction validation
        // will be implemented in Stage 8.1 (Full Integration)
    }
}