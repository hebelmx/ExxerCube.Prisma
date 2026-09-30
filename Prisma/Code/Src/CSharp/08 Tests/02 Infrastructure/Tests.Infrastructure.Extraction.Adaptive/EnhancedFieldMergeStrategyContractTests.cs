namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Adaptive;

using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;
using ExxerCube.Prisma.Testing.Contracts;
using Meziantou.Extensions.Logging.Xunit.v3;

/// <summary>
/// Implementation instance of <see cref="FieldMergeStrategyContract"/> for
/// <see cref="EnhancedFieldMergeStrategy"/> (ADR-005).
/// </summary>
/// <remarks>
/// Supersedes <c>EnhancedFieldMergeStrategyLiskovTests</c>: its 16 test bodies were
/// lifted verbatim into the contract base and run here through inheritance, alongside
/// the restored design-checklist tests. Implementation-pinning tests remain in
/// <c>EnhancedFieldMergeStrategyMutationKillingTests</c> (never merged into a contract).
/// </remarks>
public sealed class EnhancedFieldMergeStrategyContractTests : FieldMergeStrategyContract
{
    /// <summary>
    /// Initializes the implementation instance with a real
    /// <see cref="EnhancedFieldMergeStrategy"/> wired to the xUnit output logger.
    /// </summary>
    /// <param name="output">xUnit test output sink for the strategy's logger.</param>
    public EnhancedFieldMergeStrategyContractTests(ITestOutputHelper output)
        : base(new EnhancedFieldMergeStrategy(XUnitLogger.CreateLogger<EnhancedFieldMergeStrategy>(output)))
    {
    }
}
