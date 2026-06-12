namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Reference-fake instance of <see cref="DocumentDownloaderContract"/> (ADR-005 §6): proves the
/// <see cref="FakeDocumentDownloader"/> honors the port contract, so it is a trustworthy stand-in for the
/// ingestion-orchestrator and watch-loop tests before a live browser exists.
/// </summary>
public sealed class FakeDocumentDownloaderContractTests : DocumentDownloaderContract
{
    /// <summary>Initializes the reference-fake instance.</summary>
    public FakeDocumentDownloaderContractTests()
        : base(new FakeDocumentDownloader())
    {
    }
}
