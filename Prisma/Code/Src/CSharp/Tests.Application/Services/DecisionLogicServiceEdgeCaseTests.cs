namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Edge case tests for <see cref="DecisionLogicService"/> covering null handling, cancellation, and exception scenarios.
/// </summary>
public class DecisionLogicServiceEdgeCaseTests
{
    private readonly IPersonIdentityResolver _personIdentityResolver;
    private readonly ILegalDirectiveClassifier _legalDirectiveClassifier;
    private readonly IManualReviewerPanel _manualReviewerPanel;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<DecisionLogicService> _logger;
    private readonly DecisionLogicService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionLogicServiceEdgeCaseTests"/> class with mocked collaborators.
    /// </summary>
    public DecisionLogicServiceEdgeCaseTests()
    {
        _personIdentityResolver = Substitute.For<IPersonIdentityResolver>();
        _legalDirectiveClassifier = Substitute.For<ILegalDirectiveClassifier>();
        _manualReviewerPanel = Substitute.For<IManualReviewerPanel>();
        _auditLogger = Substitute.For<IAuditLogger>();
        _logger = Substitute.For<ILogger<DecisionLogicService>>();
        _service = new DecisionLogicService(_personIdentityResolver, _legalDirectiveClassifier, _manualReviewerPanel, _auditLogger, _logger);
    }

    /// <summary>
    /// Tests that ResolvePersonIdentitiesAsync handles null persons list correctly.
    /// </summary>
    /// <returns>A task that completes after asserting null input yields an empty result set.</returns>
    [Fact]
    public async Task ResolvePersonIdentitiesAsync_WithNullPersons_ReturnsEmptyList()
    {
        // Act
        var result = await _service.ResolvePersonIdentitiesAsync(null!, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldBeEmpty();
    }

    /// <summary>
    /// Tests that ResolvePersonIdentitiesAsync handles cancellation token correctly when no work is completed.
    /// </summary>
    /// <returns>A task that completes after cancellation is propagated.</returns>
    [Fact]
    public async Task ResolvePersonIdentitiesAsync_WithCancellationRequested_HandlesGracefully()
    {
        // Arrange
        var persons = new List<Persona> { new Persona { ParteId = 1, Nombre = "Juan" } };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _service.ResolvePersonIdentitiesAsync(persons, cancellationToken: cts.Token);

        // Assert
        // Service MUST respect cancellation token and propagate cancellation signal
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Tests that ResolvePersonIdentitiesAsync preserves partial results when cancellation occurs during batch processing (P1 Enhancement).
    /// </summary>
    /// <returns>A task that completes after asserting partial results are preserved.</returns>
    [Fact]
    public async Task ResolvePersonIdentitiesAsync_WithCancellationDuringProcessing_PreservesPartialResults()
    {
        // Arrange
        var persons = new List<Persona>
        {
            new Persona { ParteId = 1, Nombre = "Juan", Rfc = "PEGJ850101ABC" },
            new Persona { ParteId = 2, Nombre = "Maria", Rfc = "MARG900202XYZ" },
            new Persona { ParteId = 3, Nombre = "Pedro", Rfc = "PEDR950303DEF" }
        };

        var resolvedPerson1 = new Persona { ParteId = 1, Nombre = "Juan", Rfc = "PEGJ850101ABC" };
        var resolvedPerson2 = new Persona { ParteId = 2, Nombre = "Maria", Rfc = "MARG900202XYZ" };

        var cts = new CancellationTokenSource();
        var callCount = 0;

        // Mock: First two resolutions succeed, then cancellation is requested
        _personIdentityResolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                // Cancel after second person is resolved (before third iteration)
                if (callCount == 2)
                {
                    cts.Cancel();
                }
                return callCount switch
                {
                    1 => Result<Persona>.Success(resolvedPerson1),
                    2 => Result<Persona>.Success(resolvedPerson2),
                    _ => ResultExtensions.Cancelled<Persona>()
                };
            });

        // Mock deduplication for partial results
        _personIdentityResolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<Persona>>.Success(new List<Persona> { resolvedPerson1, resolvedPerson2 }));

        // Act
        var result = await _service.ResolvePersonIdentitiesAsync(persons, cancellationToken: cts.Token);

        // Assert - Should return partial results with warnings, not cancelled
        result.HasWarnings.ShouldBeTrue("Partial results should have warnings");
        result.IsSuccess.ShouldBeTrue("Partial results should be treated as success with warnings");
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2, "Should return 2 resolved persons");
        result.Warnings.ShouldNotBeEmpty("Should have warnings about cancellation");
        result.Warnings.ShouldContain(w => w.Contains("cancelled") && w.Contains("2") && w.Contains("3"));
        result.Confidence.ShouldBeGreaterThan(0.0);
        result.Confidence.ShouldBeLessThan(1.0);
        result.MissingDataRatio.ShouldBeGreaterThan(0.0);
    }

    /// <summary>
    /// Tests that ClassifyLegalDirectivesAsync handles null document text correctly.
    /// </summary>
    /// <returns>A task that completes after asserting null text yields an empty result.</returns>
    [Fact]
    public async Task ClassifyLegalDirectivesAsync_WithNullDocumentText_ReturnsEmptyList()
    {
        // Act
        var result = await _service.ClassifyLegalDirectivesAsync(null!, null, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldBeEmpty();
    }

    /// <summary>
    /// Tests that ClassifyLegalDirectivesAsync handles cancellation token correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyLegalDirectivesAsync_WithCancellationRequested_HandlesGracefully()
    {
        // Arrange
        var documentText = "Se ordena el BLOQUEO";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _service.ClassifyLegalDirectivesAsync(documentText, null, cancellationToken: cts.Token);

        // Assert
        // Service MUST respect cancellation token and propagate cancellation signal
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Tests that ProcessDecisionLogicAsync handles null persons list correctly.
    /// </summary>
    /// <returns>A task that completes after asserting null persons are rejected.</returns>
    [Fact]
    public async Task ProcessDecisionLogicAsync_WithNullPersons_HandlesGracefully()
    {
        // Arrange
        var documentText = "Se ordena el BLOQUEO";

        // Act
        var result = await _service.ProcessDecisionLogicAsync(null!, documentText, null, TestContext.Current.CancellationToken);

        // Assert
        // Should handle null gracefully - either return failure or empty result
        // Current implementation might throw, so we test the behavior
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that ProcessDecisionLogicAsync handles null document text correctly.
    /// </summary>
    /// <returns>A task that completes after asserting null text is rejected.</returns>
    [Fact]
    public async Task ProcessDecisionLogicAsync_WithNullDocumentText_HandlesGracefully()
    {
        // Arrange
        var persons = new List<Persona> { new Persona { ParteId = 1, Nombre = "Juan" } };

        // Act
        var result = await _service.ProcessDecisionLogicAsync(persons, null!, null, TestContext.Current.CancellationToken);

        // Assert
        // Should handle null gracefully
        result.IsFailure.ShouldBeTrue(); // Or IsSuccess with empty actions, depending on implementation
    }

    /// <summary>
    /// Tests that ProcessDecisionLogicAsync handles cancellation token correctly.
    /// </summary>
    /// <returns>A task that completes after asserting cancellation is propagated.</returns>
    [Fact]
    public async Task ProcessDecisionLogicAsync_WithCancellationRequested_ReturnsFailure()
    {
        // Arrange
        var persons = new List<Persona> { new Persona { ParteId = 1, Nombre = "Juan" } };
        var documentText = "Se ordena el BLOQUEO";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _service.ProcessDecisionLogicAsync(persons, documentText, null, cts.Token);

        // Assert
        // Service MUST respect cancellation token and propagate cancellation signal
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Tests that ResolvePersonIdentitiesAsync handles exception from resolver correctly.
    /// </summary>
    /// <returns>A task that completes after asserting resolver exceptions are handled.</returns>
    [Fact]
    public async Task ResolvePersonIdentitiesAsync_WithResolverException_HandlesGracefully()
    {
        // Arrange
        var persons = new List<Persona> { new Persona { ParteId = 1, Nombre = "Juan" } };

        _personIdentityResolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>())
            .Returns(Result<Persona>.WithFailure("Resolver exception", default(Persona), new Exception("Test exception")));

        _personIdentityResolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<Persona>>.Success(new List<Persona>()));

        // Act
        var result = await _service.ResolvePersonIdentitiesAsync(persons, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        // Service should handle exceptions gracefully by continuing with other persons (not cancellation)
        // When individual resolution fails, it logs and continues, resulting in empty list after deduplication
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldBeEmpty();
    }

    /// <summary>
    /// Tests that ClassifyLegalDirectivesAsync handles exception from classifier correctly.
    /// </summary>
    /// <returns>A task that completes after asserting classifier exceptions are surfaced.</returns>
    [Fact]
    public async Task ClassifyLegalDirectivesAsync_WithClassifierException_ReturnsFailure()
    {
        // Arrange
        var documentText = "Se ordena el BLOQUEO";

        _legalDirectiveClassifier.DetectLegalInstrumentsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<string>>.Success(new List<string>()));

        _legalDirectiveClassifier.ClassifyDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<ComplianceAction>>.WithFailure("Classifier exception", default(List<ComplianceAction>), new Exception("Test exception")));

        // Act
        var result = await _service.ClassifyLegalDirectivesAsync(documentText, null, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Failed to classify legal directives");
    }
}
