namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Blueprint instance of <see cref="FileTypeIdentifierContract"/> (ADR-005 worked example):
/// the mock-backed inheritor that exists from design time, before any implementation.
/// </summary>
/// <remarks>
/// The SUT comes from <see cref="FileTypeIdentifierMockFactory"/> — never from inline
/// per-test stubbing. If a new contract test cannot be satisfied by the factory's mock,
/// the factory must be extended in the same change (a red blueprint means the design
/// specification itself is incomplete). This class is kept forever (ADR-005 §6).
/// </remarks>
public sealed class MockFileTypeIdentifierContractTests : FileTypeIdentifierContract
{
    /// <summary>
    /// Initializes the blueprint instance with the contract-conforming mock.
    /// </summary>
    public MockFileTypeIdentifierContractTests()
        : base(FileTypeIdentifierMockFactory.CreateContractConformingMock())
    {
    }
}
