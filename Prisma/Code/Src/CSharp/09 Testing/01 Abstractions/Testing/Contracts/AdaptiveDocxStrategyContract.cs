using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="IAdaptiveDocxStrategy"/> — the first true
/// multi-implementation contract: inherited by the mock blueprint plus all five
/// concrete strategies (Structured / Contextual / TableBased / Search / Complement),
/// every one running these tests unchanged (ADR-005).
/// </summary>
/// <remarks>
/// <para>
/// The 13 <c>*_Liskov</c>-suffixed tests are the bodies that are byte-identical across
/// all five strategy twins (<c>{Strategy}LiskovTests</c>, the executable truth before
/// this refactor) modulo two things: the sample-document constant (parameterised via
/// <see cref="ValidDocxText"/> / <see cref="IncompatibleDocxText"/>) and the
/// SUT-construction line (<c>new {Strategy}(_logger)</c> → <see cref="CreateSut"/>).
/// Names are preserved including the suffix because the suite is Stryker-hardened and
/// kill power is keyed to them (plan §6.1).
/// </para>
/// <para>
/// <strong>Deliberately kept implementation-side</strong> (in each deriving strategy
/// class, never lifted here) because their bodies/assertions genuinely diverge per
/// strategy — they are implementation richness, not behaviour every correct strategy
/// must exhibit (ADR-005 §5):
/// </para>
/// <list type="bullet">
///   <item><description><c>ExtractAsync_ShouldReturnExtractedFields_WhenDataFoundInDocument_Liskov</c>
///   — each strategy asserts different field detail (exact vs <c>ShouldContain</c>).</description></item>
///   <item><description><c>ExtractAsync_ShouldExtract{MexicanNames|FromParagraphContext|FromTableRows|UsingKeywordSearch|FromMultiplePatterns}_Liskov</c>
///   — strategy-specific extraction behaviour with distinct method names.</description></item>
///   <item><description><c>ExtractAsync_ShouldExtractAccountInformation_Liskov</c>
///   — Structured asserts <c>NumeroCuenta</c>, the others assert <c>CLABE</c>.</description></item>
///   <item><description><c>GetConfidenceAsync_ShouldReturnHighConfidence_WhenDocumentMatchesStrategy_Liskov</c>
///   — the high-confidence threshold (and Structured's exact <c>== 90</c>) is per-strategy
///   (confidence-tier tests are not contract-grade, ADR-005 §5).</description></item>
/// </list>
/// <para>
/// <strong>Triage of blueprint-only tests</strong> (master plan §6.2): the mock blueprint
/// documented six tests no twin executed. The perf test
/// (<c>CanExtractAsync_ShouldBeFasterThanExtractAsync</c>), the medium/low confidence-tier
/// tests, and the two <c>LiskovSubstitution_*</c> meta-tests are NOT contract-grade — they
/// stay on the blueprint instance (<c>MockAdaptiveDocxStrategyContractTests</c>). The
/// blueprint's <c>ExtractAsync_ShouldReturnEmptyExtractedFields_WhenNoDataFound</c> (empty
/// document → non-null empty fields) was a never-validated assumption that <em>contradicts</em>
/// the executable truth: all five strategies return <c>null</c> for an empty document. The
/// validated contract — empty document → <c>null</c> — is pinned here as
/// <see cref="ExtractAsync_ShouldReturnNullOrEmpty_WhenNoDataFound_Liskov"/> (explicit
/// contract decision per plan risk 2).
/// </para>
/// </remarks>
public abstract class AdaptiveDocxStrategyContract
{
    /// <summary>An empty document — shared by every implementation.</summary>
    protected const string EmptyDocx = "";

    /// <summary>Creates the implementation under test. Called once per test.</summary>
    /// <returns>The <see cref="IAdaptiveDocxStrategy"/> implementation to verify.</returns>
    protected abstract IAdaptiveDocxStrategy CreateSut();

    /// <summary>
    /// A document this strategy can extract from (confidence &gt; 0, non-null result).
    /// </summary>
    protected abstract string ValidDocxText { get; }

    /// <summary>
    /// A document this strategy cannot extract from (confidence 0, null result).
    /// </summary>
    protected abstract string IncompatibleDocxText { get; }

    /// <summary>The exact value <see cref="IAdaptiveDocxStrategy.StrategyName"/> must return.</summary>
    protected abstract string ExpectedStrategyName { get; }

    //
    // Contract: StrategyName Property
    //

    /// <summary>Contract: the strategy exposes a non-empty, well-known name.</summary>
    [Fact]
    public void StrategyName_ShouldReturnNonEmptyString_Liskov()
    {
        // Arrange
        var strategy = CreateSut();

        // Act
        var strategyName = strategy.StrategyName;

        // Assert - Contract: Must return non-empty strategy name
        strategyName.ShouldNotBeNullOrWhiteSpace();
        strategyName.ShouldBe(ExpectedStrategyName);
    }

    //
    // Contract: ExtractAsync
    //

    /// <summary>Contract: returns null when the strategy cannot extract meaningful data.</summary>
    [Fact]
    public async Task ExtractAsync_ShouldReturnNull_WhenStrategyCannotExtract_Liskov()
    {
        // Arrange
        var strategy = CreateSut();

        // Act
        var result = await strategy.ExtractAsync(IncompatibleDocxText, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return null when strategy cannot extract meaningful data
        result.ShouldBeNull();
    }

    /// <summary>Contract: an empty document yields null (validated truth, not the blueprint's empty-fields assumption).</summary>
    [Fact]
    public async Task ExtractAsync_ShouldReturnNullOrEmpty_WhenNoDataFound_Liskov()
    {
        // Arrange
        var strategy = CreateSut();

        // Act
        var result = await strategy.ExtractAsync(EmptyDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: May return null when document is empty
        result.ShouldBeNull();
    }

    /// <summary>Contract: a pre-cancelled token surfaces as <see cref="OperationCanceledException"/>.</summary>
    [Fact]
    public async Task ExtractAsync_ShouldHandleCancellation_Liskov()
    {
        // Arrange
        var strategy = CreateSut();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Contract: Must respect cancellation token
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await strategy.ExtractAsync(ValidDocxText, cts.Token));
    }

    /// <summary>Contract: monetary amounts carry currency, a positive value and original text.</summary>
    [Fact]
    public async Task ExtractAsync_ShouldExtractMonetaryAmountsWithCurrency_Liskov()
    {
        // Arrange
        var strategy = CreateSut();

        // Act
        var result = await strategy.ExtractAsync(ValidDocxText, TestContext.Current.CancellationToken);

        // Assert - Contract: Must extract monetary amounts with currency and original text
        result.ShouldNotBeNull();
        result.Montos.Count.ShouldBeGreaterThan(0);
        result.Montos.ShouldAllBe(m => !string.IsNullOrWhiteSpace(m.Currency));
        result.Montos.ShouldAllBe(m => m.Value > 0);
        result.Montos.ShouldAllBe(m => !string.IsNullOrWhiteSpace(m.OriginalText));
    }

    //
    // Contract: CanExtractAsync
    //

    /// <summary>Contract: returns true for a document the strategy can process.</summary>
    [Fact]
    public async Task CanExtractAsync_ShouldReturnTrue_WhenStrategyCanHandleDocument_Liskov()
    {
        // Arrange
        var strategy = CreateSut();

        // Act
        var canExtract = await strategy.CanExtractAsync(ValidDocxText, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return true when strategy can process document
        canExtract.ShouldBeTrue();
    }

    /// <summary>Contract: returns false for a document the strategy cannot process.</summary>
    [Fact]
    public async Task CanExtractAsync_ShouldReturnFalse_WhenStrategyCannotHandleDocument_Liskov()
    {
        // Arrange
        var strategy = CreateSut();

        // Act
        var canExtract = await strategy.CanExtractAsync(IncompatibleDocxText, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return false when strategy cannot process document
        canExtract.ShouldBeFalse();
    }

    //
    // Contract: GetConfidenceAsync
    //

    /// <summary>Contract: confidence is 0 when the strategy cannot extract.</summary>
    [Fact]
    public async Task GetConfidenceAsync_ShouldReturnZero_WhenStrategyCannotExtract_Liskov()
    {
        // Arrange
        var strategy = CreateSut();

        // Act
        var confidence = await strategy.GetConfidenceAsync(IncompatibleDocxText, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return 0 when strategy cannot extract
        confidence.ShouldBe(0);
    }

    /// <summary>Contract: confidence is a score within [0, 100].</summary>
    [Fact]
    public async Task GetConfidenceAsync_ShouldReturnScoreBetween0And100_Liskov()
    {
        // Arrange
        var strategy = CreateSut();

        // Act
        var confidence = await strategy.GetConfidenceAsync(ValidDocxText, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return confidence score between 0 and 100
        confidence.ShouldBeInRange(0, 100);
    }

    //
    // Contract: Cross-Method Consistency
    //

    /// <summary>Contract: CanExtract = false implies confidence = 0.</summary>
    [Fact]
    public async Task StrategyContract_WhenCanExtractReturnsFalse_ConfidenceShouldBeZero_Liskov()
    {
        // Arrange
        var strategy = CreateSut();

        // Act
        var canExtract = await strategy.CanExtractAsync(IncompatibleDocxText, TestContext.Current.CancellationToken);
        var confidence = await strategy.GetConfidenceAsync(IncompatibleDocxText, TestContext.Current.CancellationToken);

        // Assert - Contract: CanExtract = false implies Confidence = 0
        canExtract.ShouldBeFalse();
        confidence.ShouldBe(0);
    }

    /// <summary>Contract: CanExtract = true implies confidence &gt; 0.</summary>
    [Fact]
    public async Task StrategyContract_WhenCanExtractReturnsTrue_ConfidenceShouldBePositive_Liskov()
    {
        // Arrange
        var strategy = CreateSut();

        // Act
        var canExtract = await strategy.CanExtractAsync(ValidDocxText, TestContext.Current.CancellationToken);
        var confidence = await strategy.GetConfidenceAsync(ValidDocxText, TestContext.Current.CancellationToken);

        // Assert - Contract: CanExtract = true implies Confidence > 0
        canExtract.ShouldBeTrue();
        confidence.ShouldBeGreaterThan(0);
    }

    /// <summary>Contract: confidence = 0 implies ExtractAsync returns null.</summary>
    [Fact]
    public async Task StrategyContract_WhenConfidenceIsZero_ExtractAsyncShouldReturnNull_Liskov()
    {
        // Arrange
        var strategy = CreateSut();

        // Act
        var confidence = await strategy.GetConfidenceAsync(IncompatibleDocxText, TestContext.Current.CancellationToken);
        var result = await strategy.ExtractAsync(IncompatibleDocxText, TestContext.Current.CancellationToken);

        // Assert - Contract: Confidence = 0 implies ExtractAsync returns null
        confidence.ShouldBe(0);
        result.ShouldBeNull();
    }

    /// <summary>Contract: confidence &gt; 0 implies ExtractAsync returns data (not null).</summary>
    [Fact]
    public async Task StrategyContract_WhenConfidenceIsPositive_ExtractAsyncShouldReturnData_Liskov()
    {
        // Arrange
        var strategy = CreateSut();

        // Act
        var confidence = await strategy.GetConfidenceAsync(ValidDocxText, TestContext.Current.CancellationToken);
        var result = await strategy.ExtractAsync(ValidDocxText, TestContext.Current.CancellationToken);

        // Assert - Contract: Confidence > 0 implies ExtractAsync returns data (not null)
        confidence.ShouldBeGreaterThan(0);
        result.ShouldNotBeNull();
    }
}
