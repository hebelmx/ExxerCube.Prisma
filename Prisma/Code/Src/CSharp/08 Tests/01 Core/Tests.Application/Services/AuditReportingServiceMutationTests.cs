namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Mutation-killing tests for <see cref="AuditReportingService"/>.
/// Pins the exact CSV/JSON output (headers, row format, ordering, CSV escaping, camelCase + indentation)
/// and every guard/failure/null branch of the four report methods. The service is deterministic given
/// a fixed set of <see cref="AuditRecord"/>s from a mocked <see cref="IAuditLogger"/>.
/// </summary>
public class AuditReportingServiceMutationTests
{
    private readonly IAuditLogger _auditLogger;
    private readonly AuditReportingService _service;
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);

    public AuditReportingServiceMutationTests(ITestOutputHelper output)
    {
        _auditLogger = Substitute.For<IAuditLogger>();
        var logger = XUnitLogger.CreateLogger<AuditReportingService>(output);
        _service = new AuditReportingService(_auditLogger, logger);
    }

    private void ReturnRecords(params AuditRecord[] records) =>
        _auditLogger.GetAuditRecordsAsync(
                Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<AuditRecord>>.Success(records.ToList()));

    private static AuditRecord Record(
        string auditId = "AID1",
        string correlationId = "C1",
        string? fileId = "F1",
        AuditActionType? actionType = null,
        ProcessingStage? stage = null,
        string? userId = "U1",
        DateTime? timestamp = null,
        bool success = true,
        string? actionDetails = "D1",
        string? errorMessage = "E1") => new()
        {
            AuditId = auditId,
            CorrelationId = correlationId,
            FileId = fileId,
            ActionType = actionType ?? AuditActionType.Classification,
            Stage = stage ?? ProcessingStage.Extraction,
            UserId = userId,
            Timestamp = timestamp ?? new DateTime(2026, 1, 2, 13, 4, 5, DateTimeKind.Utc),
            Success = success,
            ActionDetails = actionDetails,
            ErrorMessage = errorMessage,
        };

    private static bool ContainsOrdinal(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.Ordinal);

    private static string[] Lines(string csv) => csv.Replace("\r\n", "\n").Split('\n');

    // ============ Constructor guards ============

    [Fact]
    public void Constructor_NullAuditLogger_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new AuditReportingService(null!, Substitute.For<ILogger<AuditReportingService>>()))
            .ParamName.ShouldBe("auditLogger");

    [Fact]
    public void Constructor_NullLogger_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new AuditReportingService(Substitute.For<IAuditLogger>(), null!))
            .ParamName.ShouldBe("logger");

    // ============ Date-range guard (the `endDate < startDate` boundary), all 4 methods ============

    [Fact]
    public async Task ClassificationCsv_EndBeforeStart_ReturnsExactFailure()
    {
        var result = await _service.GenerateClassificationReportCsvAsync(End, Start, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("EndDate must be greater than or equal to StartDate");
    }

    [Fact]
    public async Task ClassificationJson_EndBeforeStart_ReturnsExactFailure()
    {
        var result = await _service.GenerateClassificationReportJsonAsync(End, Start, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("EndDate must be greater than or equal to StartDate");
    }

    [Fact]
    public async Task ExportCsv_EndBeforeStart_ReturnsExactFailure()
    {
        var result = await _service.ExportAuditLogCsvAsync(End, Start, null, null, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("EndDate must be greater than or equal to StartDate");
    }

    [Fact]
    public async Task ExportJson_EndBeforeStart_ReturnsExactFailure()
    {
        var result = await _service.ExportAuditLogJsonAsync(End, Start, null, null, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("EndDate must be greater than or equal to StartDate");
    }

    [Fact]
    public async Task ClassificationCsv_EqualDates_SucceedsBoundary()
    {
        // endDate == startDate must NOT be rejected: kills `<` -> `<=` and the boundary mutant.
        ReturnRecords(Record());
        var result = await _service.GenerateClassificationReportCsvAsync(Start, Start, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeFalse();
        result.Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task ClassificationJson_EqualDates_SucceedsBoundary()
    {
        ReturnRecords(Record());
        var result = await _service.GenerateClassificationReportJsonAsync(Start, Start, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeFalse();
        result.Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExportCsv_EqualDates_SucceedsBoundary()
    {
        ReturnRecords(Record());
        var result = await _service.ExportAuditLogCsvAsync(Start, Start, null, null, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeFalse();
        result.Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExportJson_EqualDates_SucceedsBoundary()
    {
        ReturnRecords(Record());
        var result = await _service.ExportAuditLogJsonAsync(Start, Start, null, null, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeFalse();
        result.Value.ShouldNotBeNull();
    }

    // ============ Retrieval failure / cancellation / null, all 4 methods ============

    [Fact]
    public async Task ClassificationCsv_RetrievalFailure_WrapsErrorExactly()
    {
        _auditLogger.GetAuditRecordsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<AuditRecord>>.WithFailure("db down"));

        var result = await _service.GenerateClassificationReportCsvAsync(Start, End, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Failed to retrieve audit records: db down");
    }

    [Fact]
    public async Task ClassificationCsv_RetrievalCancelled_ReturnsCancelled()
    {
        _auditLogger.GetAuditRecordsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<List<AuditRecord>>());

        var result = await _service.GenerateClassificationReportCsvAsync(Start, End, TestContext.Current.CancellationToken);
        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task ClassificationCsv_NullRecords_ReturnsExactFailure()
    {
        _auditLogger.GetAuditRecordsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<AuditRecord>>.Success(null!));

        var result = await _service.GenerateClassificationReportCsvAsync(Start, End, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("No audit records returned");
    }

    [Fact]
    public async Task ClassificationJson_RetrievalFailure_WrapsErrorExactly()
    {
        _auditLogger.GetAuditRecordsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<AuditRecord>>.WithFailure("db down"));

        var result = await _service.GenerateClassificationReportJsonAsync(Start, End, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Failed to retrieve audit records: db down");
    }

    [Fact]
    public async Task ClassificationJson_NullRecords_ReturnsExactFailure()
    {
        _auditLogger.GetAuditRecordsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<AuditRecord>>.Success(null!));

        var result = await _service.GenerateClassificationReportJsonAsync(Start, End, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("No audit records returned");
    }

    [Fact]
    public async Task ClassificationJson_RetrievalCancelled_ReturnsCancelled()
    {
        _auditLogger.GetAuditRecordsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<List<AuditRecord>>());

        var result = await _service.GenerateClassificationReportJsonAsync(Start, End, TestContext.Current.CancellationToken);
        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task ExportCsv_RetrievalFailure_WrapsErrorExactly()
    {
        _auditLogger.GetAuditRecordsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<AuditRecord>>.WithFailure("db down"));

        var result = await _service.ExportAuditLogCsvAsync(Start, End, null, null, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Failed to retrieve audit records: db down");
    }

    [Fact]
    public async Task ExportCsv_NullRecords_ReturnsExactFailure()
    {
        _auditLogger.GetAuditRecordsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<AuditRecord>>.Success(null!));

        var result = await _service.ExportAuditLogCsvAsync(Start, End, null, null, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("No audit records returned");
    }

    [Fact]
    public async Task ExportCsv_RetrievalCancelled_ReturnsCancelled()
    {
        _auditLogger.GetAuditRecordsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<List<AuditRecord>>());

        var result = await _service.ExportAuditLogCsvAsync(Start, End, null, null, TestContext.Current.CancellationToken);
        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task ExportJson_RetrievalFailure_WrapsErrorExactly()
    {
        _auditLogger.GetAuditRecordsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<AuditRecord>>.WithFailure("db down"));

        var result = await _service.ExportAuditLogJsonAsync(Start, End, null, null, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Failed to retrieve audit records: db down");
    }

    [Fact]
    public async Task ExportJson_NullRecords_ReturnsExactFailure()
    {
        _auditLogger.GetAuditRecordsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<AuditRecord>>.Success(null!));

        var result = await _service.ExportAuditLogJsonAsync(Start, End, null, null, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("No audit records returned");
    }

    [Fact]
    public async Task ExportJson_RetrievalCancelled_ReturnsCancelled()
    {
        _auditLogger.GetAuditRecordsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<List<AuditRecord>>());

        var result = await _service.ExportAuditLogJsonAsync(Start, End, null, null, TestContext.Current.CancellationToken);
        result.IsCancelled().ShouldBeTrue();
    }

    // ============ Cancellation before start, all 4 methods ============

    [Fact]
    public async Task ClassificationCsv_CancelledBeforeStart_DoesNotCallLogger()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await _service.GenerateClassificationReportCsvAsync(Start, End, cts.Token);
        result.IsCancelled().ShouldBeTrue();
        await _auditLogger.DidNotReceive().GetAuditRecordsAsync(
            Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClassificationJson_CancelledBeforeStart_DoesNotCallLogger()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await _service.GenerateClassificationReportJsonAsync(Start, End, cts.Token);
        result.IsCancelled().ShouldBeTrue();
        await _auditLogger.DidNotReceive().GetAuditRecordsAsync(
            Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportCsv_CancelledBeforeStart_DoesNotCallLogger()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await _service.ExportAuditLogCsvAsync(Start, End, null, null, cts.Token);
        result.IsCancelled().ShouldBeTrue();
        await _auditLogger.DidNotReceive().GetAuditRecordsAsync(
            Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportJson_CancelledBeforeStart_DoesNotCallLogger()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await _service.ExportAuditLogJsonAsync(Start, End, null, null, cts.Token);
        result.IsCancelled().ShouldBeTrue();
        await _auditLogger.DidNotReceive().GetAuditRecordsAsync(
            Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ============ Exception path (throwing logger) ============

    [Fact]
    public async Task ClassificationCsv_LoggerThrows_ReturnsWrappedError()
    {
        _auditLogger.GetAuditRecordsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<List<AuditRecord>>>>(_ => throw new InvalidOperationException("boom"));

        var result = await _service.GenerateClassificationReportCsvAsync(Start, End, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Error generating classification report: boom");
    }

    [Fact]
    public async Task ClassificationJson_LoggerThrows_ReturnsWrappedError()
    {
        _auditLogger.GetAuditRecordsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<List<AuditRecord>>>>(_ => throw new InvalidOperationException("boom"));

        var result = await _service.GenerateClassificationReportJsonAsync(Start, End, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Error generating classification report: boom");
    }

    [Fact]
    public async Task ExportCsv_LoggerThrows_ReturnsWrappedError()
    {
        _auditLogger.GetAuditRecordsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<List<AuditRecord>>>>(_ => throw new InvalidOperationException("boom"));

        var result = await _service.ExportAuditLogCsvAsync(Start, End, null, null, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Error exporting audit log: boom");
    }

    [Fact]
    public async Task ExportJson_LoggerThrows_ReturnsWrappedError()
    {
        _auditLogger.GetAuditRecordsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<AuditActionType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<List<AuditRecord>>>>(_ => throw new InvalidOperationException("boom"));

        var result = await _service.ExportAuditLogJsonAsync(Start, End, null, null, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Error exporting audit log: boom");
    }

    // ============ Classification CSV — exact header + row ============

    [Fact]
    public async Task ClassificationCsv_Header_IsExact()
    {
        ReturnRecords(Record());
        var result = await _service.GenerateClassificationReportCsvAsync(Start, End, TestContext.Current.CancellationToken);
        result.Value.ShouldNotBeNull();
        ContainsOrdinal(result.Value!, "Timestamp,FileId,CorrelationId,Stage,Success,ActionDetails,ErrorMessage").ShouldBeTrue();
    }

    [Fact]
    public async Task ClassificationCsv_Row_IsExactlyFormatted()
    {
        ReturnRecords(Record(
            fileId: "F1", correlationId: "C1", stage: ProcessingStage.Extraction,
            success: true, actionDetails: "D1", errorMessage: "E1",
            timestamp: new DateTime(2026, 1, 2, 13, 4, 5, DateTimeKind.Utc)));

        var result = await _service.GenerateClassificationReportCsvAsync(Start, End, TestContext.Current.CancellationToken);
        result.Value.ShouldNotBeNull();
        // Order, timestamp format, Stage name, bool, and the two trailing detail/error fields.
        ContainsOrdinal(result.Value!, "2026-01-02 13:04:05,F1,C1,Extraction,True,D1,E1").ShouldBeTrue();
    }

    [Fact]
    public async Task ClassificationCsv_NullFileAndDetailAndError_RenderEmptyFields()
    {
        // `?? string.Empty` fallbacks on FileId/ActionDetails/ErrorMessage.
        ReturnRecords(Record(
            fileId: null, correlationId: "C1", stage: ProcessingStage.Extraction,
            success: false, actionDetails: null, errorMessage: null,
            timestamp: new DateTime(2026, 1, 2, 13, 4, 5, DateTimeKind.Utc)));

        var result = await _service.GenerateClassificationReportCsvAsync(Start, End, TestContext.Current.CancellationToken);
        result.Value.ShouldNotBeNull();
        // EXACT line (not substring): pins the trailing ActionDetails + ErrorMessage `?? string.Empty` fallbacks
        // (a substring check would let a mutated trailing field slip through).
        Lines(result.Value!).ShouldContain("2026-01-02 13:04:05,,C1,Extraction,False,,");
    }

    [Fact]
    public async Task ClassificationCsv_OrdersByTimestampAscending()
    {
        var later = Record(fileId: "LATER", timestamp: new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc));
        var earlier = Record(fileId: "EARLIER", timestamp: new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));
        ReturnRecords(later, earlier); // intentionally out of order

        var result = await _service.GenerateClassificationReportCsvAsync(Start, End, TestContext.Current.CancellationToken);
        result.Value.ShouldNotBeNull();
        var idxEarlier = result.Value!.IndexOf("EARLIER", StringComparison.Ordinal);
        var idxLater = result.Value!.IndexOf("LATER", StringComparison.Ordinal);
        idxEarlier.ShouldBeGreaterThan(0);
        idxEarlier.ShouldBeLessThan(idxLater); // ascending order: earlier row first
    }

    [Fact]
    public async Task ClassificationCsv_NullCorrelationId_DoesNotThrow_EmptyField()
    {
        // CorrelationId is passed to EscapeCsvField WITHOUT `?? string.Empty`, so the IsNullOrEmpty guard
        // is what prevents an NRE. A null CorrelationId proves that guard is reachable/needed.
        ReturnRecords(Record(correlationId: null!, timestamp: new DateTime(2026, 1, 2, 13, 4, 5, DateTimeKind.Utc)));
        var result = await _service.GenerateClassificationReportCsvAsync(Start, End, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeFalse();
        result.Value.ShouldNotBeNull();
        ContainsOrdinal(result.Value!, "2026-01-02 13:04:05,F1,,Extraction,").ShouldBeTrue();
    }

    // ============ CSV escaping (EscapeCsvField branches) ============

    [Fact]
    public async Task Csv_CommaOnly_IsQuoted()
    {
        ReturnRecords(Record(actionDetails: "a,b"));
        var result = await _service.GenerateClassificationReportCsvAsync(Start, End, TestContext.Current.CancellationToken);
        ContainsOrdinal(result.Value!, "\"a,b\"").ShouldBeTrue();
    }

    [Fact]
    public async Task Csv_QuoteOnly_IsDoubledAndQuoted()
    {
        ReturnRecords(Record(actionDetails: "a\"b"));
        var result = await _service.GenerateClassificationReportCsvAsync(Start, End, TestContext.Current.CancellationToken);
        ContainsOrdinal(result.Value!, "\"a\"\"b\"").ShouldBeTrue();
    }

    [Fact]
    public async Task Csv_NewlineOnly_IsQuoted()
    {
        ReturnRecords(Record(actionDetails: "a\nb"));
        var result = await _service.GenerateClassificationReportCsvAsync(Start, End, TestContext.Current.CancellationToken);
        ContainsOrdinal(result.Value!, "\"a\nb\"").ShouldBeTrue();
    }

    [Fact]
    public async Task Csv_NoSpecialChars_IsNotQuoted()
    {
        ReturnRecords(Record(actionDetails: "plain"));
        var result = await _service.GenerateClassificationReportCsvAsync(Start, End, TestContext.Current.CancellationToken);
        ContainsOrdinal(result.Value!, ",plain,").ShouldBeTrue();
        ContainsOrdinal(result.Value!, "\"plain\"").ShouldBeFalse(); // not wrapped
    }

    // ============ Export CSV — exact header + row + filters ============

    [Fact]
    public async Task ExportCsv_Header_IsExact()
    {
        ReturnRecords(Record());
        var result = await _service.ExportAuditLogCsvAsync(Start, End, null, null, TestContext.Current.CancellationToken);
        result.Value.ShouldNotBeNull();
        ContainsOrdinal(result.Value!, "AuditId,Timestamp,CorrelationId,FileId,ActionType,Stage,UserId,Success,ActionDetails,ErrorMessage").ShouldBeTrue();
    }

    [Fact]
    public async Task ExportCsv_Row_IsExactlyFormatted()
    {
        ReturnRecords(Record(
            auditId: "AID1", correlationId: "C1", fileId: "F1",
            actionType: AuditActionType.Review, stage: ProcessingStage.DecisionLogic,
            userId: "U1", success: true, actionDetails: "D1", errorMessage: "E1",
            timestamp: new DateTime(2026, 1, 2, 13, 4, 5, DateTimeKind.Utc)));

        var result = await _service.ExportAuditLogCsvAsync(Start, End, null, null, TestContext.Current.CancellationToken);
        result.Value.ShouldNotBeNull();
        ContainsOrdinal(result.Value!, "AID1,2026-01-02 13:04:05,C1,F1,Review,Decision Logic,U1,True,D1,E1").ShouldBeTrue();
    }

    [Fact]
    public async Task ExportCsv_NullUserAndFile_RenderEmptyFields()
    {
        ReturnRecords(Record(
            auditId: "AID1", correlationId: "C1", fileId: null,
            actionType: AuditActionType.Review, stage: ProcessingStage.DecisionLogic,
            userId: null, success: true, actionDetails: "D1", errorMessage: "E1",
            timestamp: new DateTime(2026, 1, 2, 13, 4, 5, DateTimeKind.Utc)));

        var result = await _service.ExportAuditLogCsvAsync(Start, End, null, null, TestContext.Current.CancellationToken);
        result.Value.ShouldNotBeNull();
        ContainsOrdinal(result.Value!, "AID1,2026-01-02 13:04:05,C1,,Review,Decision Logic,,True,D1,E1").ShouldBeTrue();
    }

    [Fact]
    public async Task ExportCsv_NullTrailingDetailAndError_ExactLine()
    {
        // Pins the trailing ActionDetails + ErrorMessage `?? string.Empty` fallbacks for the export row.
        ReturnRecords(Record(
            auditId: "AID1", correlationId: "C1", fileId: "F1",
            actionType: AuditActionType.Review, stage: ProcessingStage.DecisionLogic,
            userId: "U1", success: true, actionDetails: null, errorMessage: null,
            timestamp: new DateTime(2026, 1, 2, 13, 4, 5, DateTimeKind.Utc)));

        var result = await _service.ExportAuditLogCsvAsync(Start, End, null, null, TestContext.Current.CancellationToken);
        result.Value.ShouldNotBeNull();
        Lines(result.Value!).ShouldContain("AID1,2026-01-02 13:04:05,C1,F1,Review,Decision Logic,U1,True,,");
    }

    [Fact]
    public async Task ExportCsv_OrdersByTimestampAscending()
    {
        var later = Record(auditId: "LATER", timestamp: new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc));
        var earlier = Record(auditId: "EARLIER", timestamp: new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));
        ReturnRecords(later, earlier);

        var result = await _service.ExportAuditLogCsvAsync(Start, End, null, null, TestContext.Current.CancellationToken);
        result.Value.ShouldNotBeNull();
        result.Value!.IndexOf("EARLIER", StringComparison.Ordinal)
            .ShouldBeLessThan(result.Value!.IndexOf("LATER", StringComparison.Ordinal));
    }

    // ============ Classification JSON — camelCase, indentation, values ============

    [Fact]
    public async Task ClassificationJson_UsesCamelCaseNotPascalCase()
    {
        ReturnRecords(Record());
        var result = await _service.GenerateClassificationReportJsonAsync(Start, End, TestContext.Current.CancellationToken);
        result.Value.ShouldNotBeNull();
        ContainsOrdinal(result.Value!, "\"startDate\"").ShouldBeTrue();
        ContainsOrdinal(result.Value!, "\"recordCount\"").ShouldBeTrue();
        ContainsOrdinal(result.Value!, "\"StartDate\"").ShouldBeFalse(); // kills CamelCase-policy removal
        ContainsOrdinal(result.Value!, "\"RecordCount\"").ShouldBeFalse();
    }

    [Fact]
    public async Task ClassificationJson_IsIndented()
    {
        ReturnRecords(Record());
        var result = await _service.GenerateClassificationReportJsonAsync(Start, End, TestContext.Current.CancellationToken);
        result.Value.ShouldNotBeNull();
        // WriteIndented=true => newlines + leading spaces. Kills the `true`->`false` mutant.
        ContainsOrdinal(result.Value!, "\n").ShouldBeTrue();
        ContainsOrdinal(result.Value!, "  \"").ShouldBeTrue();
    }

    [Fact]
    public async Task ClassificationJson_RecordCountAndFields()
    {
        ReturnRecords(
            Record(auditId: "A1", fileId: "F1", correlationId: "C1"),
            Record(auditId: "A2", fileId: "F2", correlationId: "C2"));

        var result = await _service.GenerateClassificationReportJsonAsync(Start, End, TestContext.Current.CancellationToken);
        result.Value.ShouldNotBeNull();
        ContainsOrdinal(result.Value!, "\"recordCount\": 2").ShouldBeTrue();
        ContainsOrdinal(result.Value!, "\"fileId\": \"F1\"").ShouldBeTrue();
        ContainsOrdinal(result.Value!, "\"correlationId\": \"C2\"").ShouldBeTrue();
    }

    [Fact]
    public async Task ClassificationJson_OrdersByTimestampAscending()
    {
        var later = Record(auditId: "LATER", timestamp: new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc));
        var earlier = Record(auditId: "EARLIER", timestamp: new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));
        ReturnRecords(later, earlier);

        var result = await _service.GenerateClassificationReportJsonAsync(Start, End, TestContext.Current.CancellationToken);
        result.Value.ShouldNotBeNull();
        result.Value!.IndexOf("EARLIER", StringComparison.Ordinal)
            .ShouldBeLessThan(result.Value!.IndexOf("LATER", StringComparison.Ordinal));
    }

    // ============ Export JSON — top-level actionType/userId, camelCase, values ============

    [Fact]
    public async Task ExportJson_TopLevelActionTypeAndUserId_Present()
    {
        ReturnRecords(Record(actionType: AuditActionType.Export, userId: "U1"));
        var result = await _service.ExportAuditLogJsonAsync(Start, End, AuditActionType.Export, "the-user", TestContext.Current.CancellationToken);
        result.Value.ShouldNotBeNull();
        ContainsOrdinal(result.Value!, "\"actionType\": \"Export\"").ShouldBeTrue();
        ContainsOrdinal(result.Value!, "\"userId\": \"the-user\"").ShouldBeTrue();
    }

    [Fact]
    public async Task ExportJson_NullActionTypeAndUserId_RenderNull()
    {
        // top-level `ActionType = actionType?.ToString()` and `UserId = userId` => null when args are null.
        ReturnRecords(Record());
        var result = await _service.ExportAuditLogJsonAsync(Start, End, null, null, TestContext.Current.CancellationToken);
        result.Value.ShouldNotBeNull();
        ContainsOrdinal(result.Value!, "\"actionType\": null").ShouldBeTrue();
        ContainsOrdinal(result.Value!, "\"userId\": null").ShouldBeTrue();
    }

    [Fact]
    public async Task ExportJson_PerRecordActionTypeAndStageAreStrings()
    {
        ReturnRecords(Record(actionType: AuditActionType.Review, stage: ProcessingStage.DecisionLogic));
        var result = await _service.ExportAuditLogJsonAsync(Start, End, null, null, TestContext.Current.CancellationToken);
        result.Value.ShouldNotBeNull();
        // per-record ActionType/Stage use .ToString() -> string values (Stage display name has a space)
        ContainsOrdinal(result.Value!, "\"Review\"").ShouldBeTrue();
        ContainsOrdinal(result.Value!, "\"Decision Logic\"").ShouldBeTrue();
    }

    [Fact]
    public async Task ExportJson_UsesCamelCaseNotPascalCase()
    {
        ReturnRecords(Record());
        var result = await _service.ExportAuditLogJsonAsync(Start, End, null, null, TestContext.Current.CancellationToken);
        result.Value.ShouldNotBeNull();
        ContainsOrdinal(result.Value!, "\"recordCount\"").ShouldBeTrue();
        ContainsOrdinal(result.Value!, "\"RecordCount\"").ShouldBeFalse();
    }
}
