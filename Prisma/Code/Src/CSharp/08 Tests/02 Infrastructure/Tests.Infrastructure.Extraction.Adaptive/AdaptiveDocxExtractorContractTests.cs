namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Adaptive;

using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;
using ExxerCube.Prisma.Testing.Abstractions;
using ExxerCube.Prisma.Testing.Contracts;
using Meziantou.Extensions.Logging.Xunit.v3;

/// <summary>
/// Implementation instance of <see cref="AdaptiveDocxExtractorContract"/> for
/// <see cref="AdaptiveDocxExtractor"/> (ADR-005).
/// </summary>
/// <remarks>
/// Supersedes <c>AdaptiveDocxExtractorLiskovTests</c>: its 12 test bodies were lifted
/// verbatim into the contract base and run here through inheritance, alongside the three
/// restored blueprint behaviours (default-mode, the two cross-method consistency tests).
/// The SUT is the real orchestrator wired with all five strategies. Implementation-pinning
/// tests remain in <c>AdaptiveDocxExtractorMutationKillingTests</c> (never merged into a
/// contract).
/// </remarks>
public sealed class AdaptiveDocxExtractorContractTests : AdaptiveDocxExtractorContract
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<AdaptiveDocxExtractor> _logger;

    /// <summary>Initializes the implementation instance with the xUnit output logger.</summary>
    /// <param name="output">xUnit test output sink for the orchestrator and its strategies.</param>
    public AdaptiveDocxExtractorContractTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = XUnitLogger.CreateLogger<AdaptiveDocxExtractor>(output);
    }

    /// <inheritdoc />
    protected override IAdaptiveDocxExtractor CreateSut()
        => new AdaptiveDocxExtractor(TestHelpers.CreateAllStrategies(_output), _logger);
}
