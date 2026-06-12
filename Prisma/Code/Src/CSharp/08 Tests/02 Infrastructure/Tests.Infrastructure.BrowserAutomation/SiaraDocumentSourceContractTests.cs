namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Implementation instance of <see cref="ExxerCube.Prisma.Testing.Contracts.SiaraDocumentSourceContract"/>
/// for the production <see cref="SiaraDocumentSource"/> (ADR-005, ADR-010, MVP-PATH 1.2).
/// </summary>
/// <remarks>
/// The SUT is the real discovery source over mocked collaborators that model the happy path (see
/// <see cref="SiaraDocumentSourceTestFactory"/>), so the universal contract — Result/cancellation semantics
/// and the non-null-list-of-non-blank-ids guarantee — runs with no live browser. The warm-session,
/// fail-closed, and listing mechanics live in <see cref="SiaraDocumentSourceTests"/>.
/// </remarks>
public sealed class SiaraDocumentSourceContractTests : ExxerCube.Prisma.Testing.Contracts.SiaraDocumentSourceContract
{
    /// <summary>Initializes the implementation instance with the real source over healthy mocks.</summary>
    public SiaraDocumentSourceContractTests()
        : base(SiaraDocumentSourceTestFactory.CreateSource())
    {
    }
}
