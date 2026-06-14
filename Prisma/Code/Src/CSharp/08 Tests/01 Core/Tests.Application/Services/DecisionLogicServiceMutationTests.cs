namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Mutation-killing tests for <see cref="DecisionLogicService"/>: pins the exact failure/validation message
/// strings of every public method, the resolver-failure "continue" and null-value-skip branches, and the
/// partial-result confidence / missing-data-ratio arithmetic + warning composition. All collaborators are
/// mocked (Guid correlation IDs only flow into the mocked audit logger, so the Result outputs are deterministic).
/// </summary>
public class DecisionLogicServiceMutationTests
{
    private readonly IPersonIdentityResolver _resolver;
    private readonly ILegalDirectiveClassifier _classifier;
    private readonly IManualReviewerPanel _panel;
    private readonly IAuditLogger _auditLogger;
    private readonly DecisionLogicService _service;

    public DecisionLogicServiceMutationTests()
    {
        _resolver = Substitute.For<IPersonIdentityResolver>();
        _classifier = Substitute.For<ILegalDirectiveClassifier>();
        _panel = Substitute.For<IManualReviewerPanel>();
        _auditLogger = Substitute.For<IAuditLogger>();
        var logger = Substitute.For<ILogger<DecisionLogicService>>();
        _service = new DecisionLogicService(_resolver, _classifier, _panel, _auditLogger, logger);
    }

    private static Persona P(int id, string name) => new() { ParteId = id, Nombre = name };

    private static List<Persona> Persons(int n) =>
        Enumerable.Range(1, n).Select(i => P(i, $"P{i}")).ToList();

    // ===================== ResolvePersonIdentitiesAsync =====================

    [Fact]
    public async Task Resolve_NullPersons_ReturnsEmptySuccess()
    {
        var result = await _service.ResolvePersonIdentitiesAsync(null!, cancellationToken: TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(0);
        await _resolver.DidNotReceive().ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolve_EmptyPersons_ReturnsEmptySuccess()
    {
        var result = await _service.ResolvePersonIdentitiesAsync(new List<Persona>(), cancellationToken: TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(0);
        await _resolver.DidNotReceive().ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolve_CancelledBeforeStart_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await _service.ResolvePersonIdentitiesAsync(Persons(2), cancellationToken: cts.Token);
        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task Resolve_AllSucceed_ReturnsDeduplicated()
    {
        var resolved = new List<Persona> { P(1, "A"), P(2, "B") };
        _resolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result<Persona>.Success((Persona)ci[0]));
        _resolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<Persona>>.Success(resolved));

        var result = await _service.ResolvePersonIdentitiesAsync(Persons(2), cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(resolved);
    }

    [Fact]
    public async Task Resolve_PerPersonFailure_IsSkipped()
    {
        // Person 1 fails (continue), person 2 succeeds -> only P2 is forwarded to dedup.
        _resolver.ResolveIdentityAsync(Arg.Is<Persona>(p => p.ParteId == 1), Arg.Any<CancellationToken>())
            .Returns(Result<Persona>.WithFailure("bad p1"));
        _resolver.ResolveIdentityAsync(Arg.Is<Persona>(p => p.ParteId == 2), Arg.Any<CancellationToken>())
            .Returns(ci => Result<Persona>.Success((Persona)ci[0]));
        List<Persona>? forwarded = null;
        _resolver.DeduplicatePersonsAsync(Arg.Do<List<Persona>>(l => forwarded = l), Arg.Any<CancellationToken>())
            .Returns(ci => Result<List<Persona>>.Success((List<Persona>)ci[0]));

        var result = await _service.ResolvePersonIdentitiesAsync(Persons(2), cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        forwarded.ShouldNotBeNull();
        forwarded!.Count.ShouldBe(1);
        forwarded[0].ParteId.ShouldBe(2);
    }

    [Fact]
    public async Task Resolve_SuccessWithNullValue_NotAdded()
    {
        _resolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>())
            .Returns(Result<Persona>.Success(null!));
        List<Persona>? forwarded = null;
        _resolver.DeduplicatePersonsAsync(Arg.Do<List<Persona>>(l => forwarded = l), Arg.Any<CancellationToken>())
            .Returns(ci => Result<List<Persona>>.Success((List<Persona>)ci[0]));

        var result = await _service.ResolvePersonIdentitiesAsync(Persons(2), cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        forwarded.ShouldNotBeNull();
        forwarded!.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Resolve_DeduplicationFailure_ReturnsExactError()
    {
        _resolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result<Persona>.Success((Persona)ci[0]));
        _resolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<Persona>>.WithFailure("dedup boom"));

        var result = await _service.ResolvePersonIdentitiesAsync(Persons(2), cancellationToken: TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Failed to deduplicate persons: dedup boom");
    }

    [Fact]
    public async Task Resolve_SuccessNullDedupValue_ReturnsEmptyList()
    {
        _resolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result<Persona>.Success((Persona)ci[0]));
        _resolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<Persona>>.Success(null!));

        var result = await _service.ResolvePersonIdentitiesAsync(Persons(2), cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Resolve_ResolverThrows_ReturnsWrappedError()
    {
        _resolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<Persona>>>(_ => throw new InvalidOperationException("boom"));

        var result = await _service.ResolvePersonIdentitiesAsync(Persons(1), cancellationToken: TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Error processing person identities: boom");
    }

    [Fact]
    public async Task Resolve_PartialCancellationDedupSuccess_WarnsWithConfidenceAndRatio()
    {
        // Cancel after exactly one of four persons is resolved -> partial path with dedup success.
        using var cts = new CancellationTokenSource();
        _resolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>())
            .Returns(ci => { cts.Cancel(); return Result<Persona>.Success((Persona)ci[0]); });
        var deduped = new List<Persona> { P(1, "P1") };
        _resolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<Persona>>.Success(deduped));

        var result = await _service.ResolvePersonIdentitiesAsync(Persons(4), cancellationToken: cts.Token);

        result.HasWarnings.ShouldBeTrue();
        result.Confidence.ShouldBe(0.25, 0.0001);          // completed/total = 1/4
        result.MissingDataRatio.ShouldBe(0.75, 0.0001);    // (total-completed)/total = 3/4
        result.Value.ShouldBeSameAs(deduped);
        result.Warnings.ShouldContain("Operation was cancelled. Resolved 1 of 4 persons.");
    }

    [Fact]
    public async Task Resolve_PartialCancellationDedupFailure_WarnsWithDedupFailedSuffix()
    {
        using var cts = new CancellationTokenSource();
        _resolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>())
            .Returns(ci => { cts.Cancel(); return Result<Persona>.Success((Persona)ci[0]); });
        _resolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<Persona>>.WithFailure("dedup down"));

        var result = await _service.ResolvePersonIdentitiesAsync(Persons(2), cancellationToken: cts.Token);

        result.HasWarnings.ShouldBeTrue();
        result.Confidence.ShouldBe(0.5, 0.0001);           // 1/2
        result.MissingDataRatio.ShouldBe(0.5, 0.0001);
        result.Value!.Count.ShouldBe(1);                   // un-deduplicated resolved persons preserved
        result.Warnings.ShouldContain("Operation was cancelled. Resolved 1 of 2 persons (deduplication failed).");
    }

    [Fact]
    public async Task Resolve_ResolverReturnsCancelledMidway_PartialDedupSuccess()
    {
        // P1 resolves, P2's resolver RETURNS cancelled -> partial path with dedup success.
        _resolver.ResolveIdentityAsync(Arg.Is<Persona>(p => p.ParteId == 1), Arg.Any<CancellationToken>())
            .Returns(ci => Result<Persona>.Success((Persona)ci[0]));
        _resolver.ResolveIdentityAsync(Arg.Is<Persona>(p => p.ParteId == 2), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<Persona>());
        var deduped = new List<Persona> { P(1, "P1") };
        _resolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<Persona>>.Success(deduped));

        var result = await _service.ResolvePersonIdentitiesAsync(Persons(2), cancellationToken: TestContext.Current.CancellationToken);

        result.HasWarnings.ShouldBeTrue();
        result.Confidence.ShouldBe(0.5, 0.0001);
        result.MissingDataRatio.ShouldBe(0.5, 0.0001);
        result.Value.ShouldBeSameAs(deduped);
        result.Warnings.ShouldContain("Operation was cancelled by resolver. Resolved 1 of 2 persons.");
    }

    [Fact]
    public async Task Resolve_ResolverReturnsCancelledNoWork_ReturnsCancelled()
    {
        // First person's resolver returns cancelled, nothing resolved yet -> Count==0 -> plain Cancelled.
        _resolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<Persona>());

        var result = await _service.ResolvePersonIdentitiesAsync(Persons(2), cancellationToken: TestContext.Current.CancellationToken);

        result.IsCancelled().ShouldBeTrue();
        result.HasWarnings.ShouldBeFalse();
    }

    [Fact]
    public async Task Resolve_ResolverCancelledMidway_DedupFailure_FailedSuffix()
    {
        _resolver.ResolveIdentityAsync(Arg.Is<Persona>(p => p.ParteId == 1), Arg.Any<CancellationToken>())
            .Returns(ci => Result<Persona>.Success((Persona)ci[0]));
        _resolver.ResolveIdentityAsync(Arg.Is<Persona>(p => p.ParteId == 2), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<Persona>());
        _resolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<Persona>>.WithFailure("dedup down"));

        var result = await _service.ResolvePersonIdentitiesAsync(Persons(2), cancellationToken: TestContext.Current.CancellationToken);

        result.HasWarnings.ShouldBeTrue();
        result.Warnings.ShouldContain("Operation was cancelled by resolver. Resolved 1 of 2 persons (deduplication failed).");
    }

    [Fact]
    public async Task Resolve_ResolverCancelledMidway_DedupCancelled_CancelledSuffix()
    {
        _resolver.ResolveIdentityAsync(Arg.Is<Persona>(p => p.ParteId == 1), Arg.Any<CancellationToken>())
            .Returns(ci => Result<Persona>.Success((Persona)ci[0]));
        _resolver.ResolveIdentityAsync(Arg.Is<Persona>(p => p.ParteId == 2), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<Persona>());
        _resolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<List<Persona>>());

        var result = await _service.ResolvePersonIdentitiesAsync(Persons(2), cancellationToken: TestContext.Current.CancellationToken);

        result.HasWarnings.ShouldBeTrue();
        result.Warnings.ShouldContain("Operation was cancelled by resolver. Resolved 1 of 2 persons (deduplication cancelled).");
    }

    [Fact]
    public async Task Resolve_DedupCancelledAfterFullResolve_WarnsIncomplete()
    {
        _resolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result<Persona>.Success((Persona)ci[0]));
        _resolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<List<Persona>>());

        var result = await _service.ResolvePersonIdentitiesAsync(Persons(2), cancellationToken: TestContext.Current.CancellationToken);

        result.HasWarnings.ShouldBeTrue();
        result.Confidence.ShouldBe(1.0, 0.0001);          // completed==total
        result.MissingDataRatio.ShouldBe(0.0, 0.0001);
        result.Value!.Count.ShouldBe(2);                   // un-deduplicated resolved persons preserved
        result.Warnings.ShouldContain("Operation was cancelled during deduplication. Resolved 2 of 2 persons (deduplication incomplete).");
    }

    [Fact]
    public async Task Resolve_DedupCancelled_PartialResolve_KillsConfidenceRatioArithmetic()
    {
        // P2 fails to resolve (continue), P1 & P3 succeed -> completed(2) < total(3); dedup then cancelled.
        // completed<total is required to kill the confidence `/`->`*` mutant (clamping makes completed==total equivalent).
        _resolver.ResolveIdentityAsync(Arg.Is<Persona>(p => p.ParteId == 2), Arg.Any<CancellationToken>())
            .Returns(Result<Persona>.WithFailure("p2 bad"));
        _resolver.ResolveIdentityAsync(Arg.Is<Persona>(p => p.ParteId != 2), Arg.Any<CancellationToken>())
            .Returns(ci => Result<Persona>.Success((Persona)ci[0]));
        _resolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<List<Persona>>());

        var result = await _service.ResolvePersonIdentitiesAsync(Persons(3), cancellationToken: TestContext.Current.CancellationToken);

        result.HasWarnings.ShouldBeTrue();
        result.Confidence.ShouldBe(2.0 / 3.0, 0.0001);     // completed/total = 2/3
        result.MissingDataRatio.ShouldBe(1.0 / 3.0, 0.0001); // (total-completed)/total = 1/3
        result.Value!.Count.ShouldBe(2);
        result.Warnings.ShouldContain("Operation was cancelled during deduplication. Resolved 2 of 3 persons (deduplication incomplete).");
    }

    [Fact]
    public async Task Resolve_AllFailThenDedupCancelled_NoWorkReturnsCancelled()
    {
        // Every person fails to resolve -> resolvedPersons empty -> dedup(empty) cancelled -> Count==0 path.
        _resolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>())
            .Returns(Result<Persona>.WithFailure("all bad"));
        _resolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<List<Persona>>());

        var result = await _service.ResolvePersonIdentitiesAsync(Persons(2), cancellationToken: TestContext.Current.CancellationToken);

        result.IsCancelled().ShouldBeTrue();   // Count==0 -> "no work" -> plain Cancelled (not warnings)
        result.HasWarnings.ShouldBeFalse();
    }

    [Fact]
    public async Task Resolve_BetweenIterationCancel_DedupCancelled_CancelledSuffix()
    {
        using var cts = new CancellationTokenSource();
        _resolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>())
            .Returns(ci => { cts.Cancel(); return Result<Persona>.Success((Persona)ci[0]); });
        _resolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<List<Persona>>());

        var result = await _service.ResolvePersonIdentitiesAsync(Persons(2), cancellationToken: cts.Token);

        result.HasWarnings.ShouldBeTrue();
        result.Warnings.ShouldContain("Operation was cancelled. Resolved 1 of 2 persons (deduplication cancelled).");
    }

    // ===================== ClassifyLegalDirectivesAsync =====================

    [Fact]
    public async Task Classify_NullText_ReturnsEmptySuccess()
    {
        var result = await _service.ClassifyLegalDirectivesAsync(null!, cancellationToken: TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(0);
        await _classifier.DidNotReceive().ClassifyDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Classify_WhitespaceText_ReturnsEmptySuccess()
    {
        var result = await _service.ClassifyLegalDirectivesAsync("   ", cancellationToken: TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Classify_InstrumentDetectionFailure_ContinuesAndClassifies()
    {
        _classifier.DetectLegalInstrumentsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<string>>.WithFailure("detect boom"));
        var actions = new List<ComplianceAction> { new() { ActionType = ComplianceActionKind.Block } };
        _classifier.ClassifyDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente?>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<ComplianceAction>>.Success(actions));

        var result = await _service.ClassifyLegalDirectivesAsync("text", cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Classify_InstrumentDetectionCancelled_ReturnsCancelled()
    {
        _classifier.DetectLegalInstrumentsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<List<string>>());

        var result = await _service.ClassifyLegalDirectivesAsync("text", cancellationToken: TestContext.Current.CancellationToken);

        result.IsCancelled().ShouldBeTrue();
        await _classifier.DidNotReceive().ClassifyDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Classify_DirectiveClassificationFailure_ReturnsExactError()
    {
        _classifier.DetectLegalInstrumentsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<string>>.Success(new List<string>()));
        _classifier.ClassifyDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente?>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<ComplianceAction>>.WithFailure("classify boom"));

        var result = await _service.ClassifyLegalDirectivesAsync("text", cancellationToken: TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Failed to classify legal directives: classify boom");
    }

    [Fact]
    public async Task Classify_DirectiveClassificationCancelled_ReturnsCancelled()
    {
        _classifier.DetectLegalInstrumentsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<string>>.Success(new List<string>()));
        _classifier.ClassifyDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente?>(), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<List<ComplianceAction>>());

        var result = await _service.ClassifyLegalDirectivesAsync("text", cancellationToken: TestContext.Current.CancellationToken);

        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task Classify_Success_ReturnsActions()
    {
        _classifier.DetectLegalInstrumentsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<string>>.Success(new List<string> { "Oficio" }));
        var actions = new List<ComplianceAction> { new() { ActionType = ComplianceActionKind.Block, AccountNumber = "123" } };
        _classifier.ClassifyDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente?>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<ComplianceAction>>.Success(actions));

        var result = await _service.ClassifyLegalDirectivesAsync("text", cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);
        result.Value[0].ActionType.ShouldBe(ComplianceActionKind.Block);
    }

    [Fact]
    public async Task Classify_ClassifierThrows_ReturnsWrappedError()
    {
        _classifier.DetectLegalInstrumentsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<List<string>>>>(_ => throw new InvalidOperationException("boom"));

        var result = await _service.ClassifyLegalDirectivesAsync("text", cancellationToken: TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Error classifying legal directives: boom");
    }

    // ===================== ProcessDecisionLogicAsync =====================

    [Fact]
    public async Task Process_NullPersons_ReturnsExactFailure()
    {
        var result = await _service.ProcessDecisionLogicAsync(null!, "text", null, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Persons list cannot be null");
    }

    [Fact]
    public async Task Process_NullDocumentText_ReturnsExactFailure()
    {
        var result = await _service.ProcessDecisionLogicAsync(Persons(1), null!, null, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Document text cannot be null");
    }

    [Fact]
    public async Task Process_CancelledBeforeStart_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await _service.ProcessDecisionLogicAsync(Persons(1), "text", null, cts.Token);
        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task Process_IdentityResolutionFailure_ReturnsExactError()
    {
        // ResolvePersonIdentitiesAsync fails via dedup failure.
        _resolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result<Persona>.Success((Persona)ci[0]));
        _resolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<Persona>>.WithFailure("dedup boom"));

        var result = await _service.ProcessDecisionLogicAsync(Persons(1), "text", null, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Identity resolution failed: Failed to deduplicate persons: dedup boom");
    }

    [Fact]
    public async Task Process_ClassificationFailureNoPartial_ReturnsExactError()
    {
        _resolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result<Persona>.Success((Persona)ci[0]));
        _resolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result<List<Persona>>.Success((List<Persona>)ci[0]));
        _classifier.DetectLegalInstrumentsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<string>>.Success(new List<string>()));
        _classifier.ClassifyDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente?>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<ComplianceAction>>.WithFailure("classify boom"));

        var result = await _service.ProcessDecisionLogicAsync(Persons(1), "text", null, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Legal classification failed: Failed to classify legal directives: classify boom");
    }

    [Fact]
    public async Task Process_Success_CombinesPersonsAndActions()
    {
        _resolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result<Persona>.Success((Persona)ci[0]));
        _resolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result<List<Persona>>.Success((List<Persona>)ci[0]));
        _classifier.DetectLegalInstrumentsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<string>>.Success(new List<string>()));
        var actions = new List<ComplianceAction> { new() { ActionType = ComplianceActionKind.Block } };
        _classifier.ClassifyDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente?>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<ComplianceAction>>.Success(actions));

        var result = await _service.ProcessDecisionLogicAsync(Persons(2), "text", null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.ResolvedPersons.Count.ShouldBe(2);
        result.Value.ComplianceActions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Process_PartialIdentity_ClassifyCancelled_PreservesPartialWithWarnings()
    {
        // Cancel during identity resolution (shared token) -> resolve returns partial WithWarnings;
        // the now-cancelled token makes classify return Cancelled -> the partial-preservation branch.
        using var cts = new CancellationTokenSource();
        _resolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>())
            .Returns(ci => { cts.Cancel(); return Result<Persona>.Success((Persona)ci[0]); });
        var deduped = new List<Persona> { P(1, "P1") };
        _resolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<Persona>>.Success(deduped));

        var result = await _service.ProcessDecisionLogicAsync(Persons(2), "text", null, cts.Token);

        result.HasWarnings.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.ResolvedPersons.Count.ShouldBe(1);
        result.Value.ComplianceActions.Count.ShouldBe(0);
        result.Confidence.ShouldBe(0.5, 0.0001);
        result.Warnings.ShouldContain("Operation was cancelled. Resolved 1 of 2 persons.");
        result.Warnings.ShouldContain("Legal classification was cancelled.");
    }

    // ===================== IdentifyAndQueueReviewCasesAsync =====================

    [Fact]
    public async Task Identify_NullFileId_ReturnsExactFailure()
    {
        var result = await _service.IdentifyAndQueueReviewCasesAsync(
            "  ", new UnifiedMetadataRecord(), new ClassificationResult(), cancellationToken: TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("FileId cannot be null or empty");
    }

    [Fact]
    public async Task Identify_NullMetadata_ReturnsExactFailure()
    {
        var result = await _service.IdentifyAndQueueReviewCasesAsync(
            "F1", null!, new ClassificationResult(), cancellationToken: TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Metadata cannot be null");
    }

    [Fact]
    public async Task Identify_NullClassification_ReturnsExactFailure()
    {
        var result = await _service.IdentifyAndQueueReviewCasesAsync(
            "F1", new UnifiedMetadataRecord(), null!, cancellationToken: TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Classification cannot be null");
    }

    [Fact]
    public async Task Identify_PanelFailure_ReturnsExactError()
    {
        _panel.IdentifyReviewCasesAsync(Arg.Any<string>(), Arg.Any<UnifiedMetadataRecord>(), Arg.Any<ClassificationResult>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.WithFailure("panel boom"));

        var result = await _service.IdentifyAndQueueReviewCasesAsync(
            "F1", new UnifiedMetadataRecord(), new ClassificationResult(), cancellationToken: TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Failed to identify review cases: panel boom");
    }

    [Fact]
    public async Task Identify_PanelCancelled_ReturnsCancelled()
    {
        _panel.IdentifyReviewCasesAsync(Arg.Any<string>(), Arg.Any<UnifiedMetadataRecord>(), Arg.Any<ClassificationResult>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<List<ReviewCase>>());

        var result = await _service.IdentifyAndQueueReviewCasesAsync(
            "F1", new UnifiedMetadataRecord(), new ClassificationResult(), cancellationToken: TestContext.Current.CancellationToken);

        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task Identify_SuccessNullValue_ReturnsEmptyList()
    {
        _panel.IdentifyReviewCasesAsync(Arg.Any<string>(), Arg.Any<UnifiedMetadataRecord>(), Arg.Any<ClassificationResult>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.Success(null!));

        var result = await _service.IdentifyAndQueueReviewCasesAsync(
            "F1", new UnifiedMetadataRecord(), new ClassificationResult(), cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Identify_PanelThrows_ReturnsWrappedError()
    {
        _panel.IdentifyReviewCasesAsync(Arg.Any<string>(), Arg.Any<UnifiedMetadataRecord>(), Arg.Any<ClassificationResult>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<List<ReviewCase>>>>(_ => throw new InvalidOperationException("boom"));

        var result = await _service.IdentifyAndQueueReviewCasesAsync(
            "F1", new UnifiedMetadataRecord(), new ClassificationResult(), cancellationToken: TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Error identifying review cases: boom");
    }

    // ===================== ProcessReviewDecisionAsync =====================

    private static ReviewDecision Decision() => new()
    {
        DecisionId = "DEC-1",
        CaseId = "CASE-1",
        DecisionType = DecisionType.Approve,
        ReviewerId = "REV-1",
        ReviewedAt = new DateTime(2026, 1, 1),
    };

    [Fact]
    public async Task ProcessReview_NullCaseId_ReturnsExactFailure()
    {
        var result = await _service.ProcessReviewDecisionAsync("  ", Decision(), TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("CaseId cannot be null or empty");
    }

    [Fact]
    public async Task ProcessReview_NullDecision_ReturnsExactFailure()
    {
        var result = await _service.ProcessReviewDecisionAsync("CASE-1", null!, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Decision cannot be null");
    }

    [Fact]
    public async Task ProcessReview_SubmitFailure_ReturnsExactError()
    {
        _panel.SubmitReviewDecisionAsync(Arg.Any<string>(), Arg.Any<ReviewDecision>(), Arg.Any<CancellationToken>())
            .Returns(Result.WithFailure("submit boom"));

        var result = await _service.ProcessReviewDecisionAsync("CASE-1", Decision(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Failed to process review decision: submit boom");
    }

    [Fact]
    public async Task ProcessReview_SubmitCancelled_ReturnsCancelled()
    {
        _panel.SubmitReviewDecisionAsync(Arg.Any<string>(), Arg.Any<ReviewDecision>(), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled());

        var result = await _service.ProcessReviewDecisionAsync("CASE-1", Decision(), TestContext.Current.CancellationToken);

        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task ProcessReview_Success_ReturnsSuccess()
    {
        _panel.SubmitReviewDecisionAsync(Arg.Any<string>(), Arg.Any<ReviewDecision>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await _service.ProcessReviewDecisionAsync("CASE-1", Decision(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _panel.Received(1).SubmitReviewDecisionAsync("CASE-1", Arg.Any<ReviewDecision>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessReview_SubmitThrows_ReturnsWrappedError()
    {
        _panel.SubmitReviewDecisionAsync(Arg.Any<string>(), Arg.Any<ReviewDecision>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result>>(_ => throw new InvalidOperationException("boom"));

        var result = await _service.ProcessReviewDecisionAsync("CASE-1", Decision(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Error processing review decision: boom");
    }

    [Fact]
    public async Task ProcessReview_CancelledBeforeStart_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await _service.ProcessReviewDecisionAsync("CASE-1", Decision(), cts.Token);
        result.IsCancelled().ShouldBeTrue();
        await _panel.DidNotReceive().SubmitReviewDecisionAsync(Arg.Any<string>(), Arg.Any<ReviewDecision>(), Arg.Any<CancellationToken>());
    }
}
