namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Blueprint instance of <see cref="BrowserSessionContextContract"/> (ADR-005 §6): the mock-backed
/// inheritor that exists from design time. Kept forever; its SUT comes from
/// <see cref="BrowserSessionContextMockFactory"/>.
/// </summary>
public sealed class MockBrowserSessionContextContractTests : BrowserSessionContextContract
{
    /// <summary>Initializes the blueprint instance with the contract-conforming mock.</summary>
    public MockBrowserSessionContextContractTests()
        : base(BrowserSessionContextMockFactory.CreateContractConformingMock())
    {
    }
}
