using ExxerCube.Prisma.Infrastructure.Export.Adaptive;
using ExxerCube.Prisma.Testing.Contracts;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Tests.Infrastructure.Export.Adaptive;

/// <summary>
/// Implementation instance of <see cref="TemplateFieldMapperContract"/> for
/// <see cref="TemplateFieldMapper"/> (ADR-005).
/// </summary>
/// <remarks>
/// Supersedes <c>TemplateFieldMapperTests</c>: its 22 zero-drift test bodies were lifted
/// verbatim into the contract base and run here through inheritance. Implementation-pinning
/// tests remain in <c>TemplateFieldMapperMutationTests</c> (never merged into a contract).
/// </remarks>
public sealed class TemplateFieldMapperContractTests : TemplateFieldMapperContract
{
    /// <summary>
    /// Initializes the implementation instance with a real <see cref="TemplateFieldMapper"/>
    /// wired to the xUnit output logger.
    /// </summary>
    /// <param name="output">xUnit test output sink for the mapper's logger.</param>
    public TemplateFieldMapperContractTests(ITestOutputHelper output)
        : base(new TemplateFieldMapper(XUnitLogger.CreateLogger<TemplateFieldMapper>(output)))
    {
    }
}
