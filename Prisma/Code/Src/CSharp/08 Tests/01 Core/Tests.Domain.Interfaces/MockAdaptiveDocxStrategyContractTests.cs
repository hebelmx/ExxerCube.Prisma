namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

using ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Blueprint instance of <see cref="AdaptiveDocxStrategyContract"/> — the mock-backed
/// inheritor that encodes the design specification (ADR-005 §6).
/// </summary>
/// <remarks>
/// <para>
/// Converted from the original standalone <c>IAdaptiveDocxStrategyContractTests</c>
/// (Tests.Domain) in Phase 3 of the ITDD refactor: its 13 shared design tests now live in
/// the contract base (run here against <see cref="AdaptiveDocxStrategyMockFactory"/>), and
/// its inline <c>.Returns(...)</c> stubbing moved into that factory.
/// </para>
/// <para>
/// The five tests below were documented by the blueprint but executed by no real strategy
/// twin (master plan §6.2). They are kept here, on the blueprint instance, because they are
/// <strong>not contract-grade</strong> (ADR-005 §5):
/// </para>
/// <list type="bullet">
///   <item><description><see cref="CanExtractAsync_ShouldBeFasterThanExtractAsync"/> — a
///   performance/timing expectation, not a behavioural contract.</description></item>
///   <item><description><see cref="GetConfidenceAsync_ShouldReturnMediumConfidence_ForFallbackStrategy"/>
///   and <see cref="GetConfidenceAsync_ShouldReturnLowConfidence_ForBackupStrategy"/> —
///   confidence-tier expectations that describe <em>different</em> strategies; no single real
///   strategy returns medium <em>and</em> low.</description></item>
///   <item><description><see cref="LiskovSubstitution_AnyImplementationMustSatisfyExtractContract"/>
///   and <see cref="LiskovSubstitution_ConfidenceScoresAreComparable"/> — meta-tests about
///   substitutability that are inherently expressed with two configurable mocks.</description></item>
/// </list>
/// <para>
/// Kept forever — a contract test this factory's mock cannot satisfy means the design spec
/// itself is incomplete.
/// </para>
/// </remarks>
public sealed class MockAdaptiveDocxStrategyContractTests : AdaptiveDocxStrategyContract
{
    private const string ValidStructuredDocx = @"
        Expediente No.: A/AS1-2505-088637-PHM
        Oficio: 214-1-18714972/2025
        Autoridad: PGR
        Causa: Lavado de dinero
        Acción Solicitada: Aseguramiento precautorio
        Nombre: Juan Carlos GARCÍA LÓPEZ
        RFC: GALJ850101XXX
        Cuenta: 0123456789012345
        Banco: BANAMEX
        Monto: $100,000.00 MXN
        Fecha: 15/11/2025
    ";

    private const string Incompatible = "This is just random text with no structure.";

    /// <inheritdoc />
    protected override IAdaptiveDocxStrategy CreateSut()
        => AdaptiveDocxStrategyMockFactory.CreateContractConformingMock();

    /// <inheritdoc />
    protected override string ValidDocxText => ValidStructuredDocx;

    /// <inheritdoc />
    protected override string IncompatibleDocxText => Incompatible;

    /// <inheritdoc />
    protected override string ExpectedStrategyName => AdaptiveDocxStrategyMockFactory.StrategyName;

    //
    // Blueprint-only design notes (no real-SUT twin; not contract-grade — see remarks)
    //

    [Fact]
    public async Task CanExtractAsync_ShouldBeFasterThanExtractAsync()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();

        // Configure CanExtractAsync to be fast
        strategy.CanExtractAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        // Act
        var canExtractTask = strategy.CanExtractAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: CanExtractAsync should be a fast preliminary check
        canExtractTask.IsCompleted.ShouldBeTrue(); // Should complete synchronously for fast checks
        await canExtractTask;
    }

    [Fact]
    public async Task GetConfidenceAsync_ShouldReturnMediumConfidence_ForFallbackStrategy()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();

        // Complement strategy should return medium confidence (always available)
        strategy.StrategyName.Returns("Complement");
        strategy.GetConfidenceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(50);

        // Act
        var confidence = await strategy.GetConfidenceAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Fallback/complement strategies should return medium confidence (31-60)
        confidence.ShouldBeInRange(31, 60);
    }

    [Fact]
    public async Task GetConfidenceAsync_ShouldReturnLowConfidence_ForBackupStrategy()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();

        strategy.GetConfidenceAsync(EmptyDocx, Arg.Any<CancellationToken>())
            .Returns(20);

        // Act
        var confidence = await strategy.GetConfidenceAsync(EmptyDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Backup strategies should return low confidence (1-30)
        confidence.ShouldBeInRange(1, 30);
    }

    [Fact]
    public async Task LiskovSubstitution_AnyImplementationMustSatisfyExtractContract()
    {
        // Arrange - Create two different mock strategies
        var strategy1 = Substitute.For<IAdaptiveDocxStrategy>();
        var strategy2 = Substitute.For<IAdaptiveDocxStrategy>();

        var fields1 = new ExtractedFields { Expediente = "Strategy1Result" };
        var fields2 = new ExtractedFields { Expediente = "Strategy2Result" };

        strategy1.ExtractAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(fields1);
        strategy2.ExtractAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(fields2);

        // Act
        var result1 = await strategy1.ExtractAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);
        var result2 = await strategy2.ExtractAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Liskov: Any implementation can be substituted
        result1.ShouldNotBeNull();
        result2.ShouldNotBeNull();
        // Both satisfy the contract, even with different results
    }

    [Fact]
    public async Task LiskovSubstitution_ConfidenceScoresAreComparable()
    {
        // Arrange
        var structuredStrategy = Substitute.For<IAdaptiveDocxStrategy>();
        var complementStrategy = Substitute.For<IAdaptiveDocxStrategy>();

        structuredStrategy.StrategyName.Returns("StructuredDocx");
        structuredStrategy.GetConfidenceAsync(ValidStructuredDocx, Arg.Any<CancellationToken>())
            .Returns(90);

        complementStrategy.StrategyName.Returns("Complement");
        complementStrategy.GetConfidenceAsync(ValidStructuredDocx, Arg.Any<CancellationToken>())
            .Returns(50);

        // Act
        var confidence1 = await structuredStrategy.GetConfidenceAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);
        var confidence2 = await complementStrategy.GetConfidenceAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Liskov: Confidence scores are comparable across implementations
        confidence1.ShouldBeGreaterThan(confidence2);
        // This allows orchestrator to select best strategy
    }
}
