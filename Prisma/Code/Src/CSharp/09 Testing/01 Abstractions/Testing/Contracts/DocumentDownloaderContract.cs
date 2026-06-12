using System.Reflection;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults.Operations;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="IDocumentDownloader"/> — every implementation (the reference fake
/// and the production <c>SiaraDocumentDownloader</c>) must pass these tests unchanged (ADR-005, ADR-010).
/// </summary>
/// <remarks>
/// <para>
/// Uses the ADR-005 §3 default <strong>injected-<see cref="Sut"/></strong> mechanism. The class is
/// <c>abstract</c>, so xUnit does not discover it; each inherited <c>[Fact]</c> runs once per deriving
/// class.
/// </para>
/// <para>
/// Scope rule (ADR-005 §5): only behavior <em>any</em> correct downloader must exhibit — Railway-Oriented
/// Result semantics (failure not throw), null/blank-id handling, cancellation on a pre-cancelled token,
/// the success-carries-provenance guarantee (content plus the trustworthy actor and session that
/// authorized the pull, ADR-010 P2), and the security invariant that the returned document exposes no
/// raw-credential members. Mode-specific acquisition/scrape mechanics stay in the SUT's own test project.
/// </para>
/// </remarks>
public abstract class DocumentDownloaderContract
{
    /// <summary>Initializes the contract with the downloader under test.</summary>
    /// <param name="sut">The <see cref="IDocumentDownloader"/> implementation to verify.</param>
    protected DocumentDownloaderContract(IDocumentDownloader sut)
    {
        ArgumentNullException.ThrowIfNull(sut);
        Sut = sut;
    }

    /// <summary>Gets the downloader under test.</summary>
    protected IDocumentDownloader Sut { get; }

    /// <summary>
    /// Returns a document id the SUT is configured to download successfully. Implementations whose fixture
    /// presents a particular document override this to name it.
    /// </summary>
    /// <returns>A document id that resolves to a successful download.</returns>
    protected virtual string ValidDocumentId() => "contract-document";

    /// <summary>Contract: a pre-cancelled token yields a Cancelled Result — never an exception.</summary>
    [Fact]
    public async Task DownloadAsync_PreCancelledToken_ReturnsCancelled()
    {
        var cancelled = new CancellationToken(canceled: true);

        var result = await Sut.DownloadAsync(ValidDocumentId(), cancelled);

        result.ShouldNotBeNull();
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>Contract: a null or blank document id yields a failure Result — never a throw, never a null Result.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DownloadAsync_BlankDocumentId_ReturnsFailure(string? documentId)
    {
        var result = await Sut.DownloadAsync(documentId!, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Contract: a valid id yields a success Result wrapping the document content together with the
    /// provenance that authorized the pull (the trustworthy actor and session), so per-document
    /// non-repudiation is realized (ADR-010 P2).
    /// </summary>
    [Fact]
    public async Task DownloadAsync_ValidDocumentId_ReturnsDocumentWithProvenance()
    {
        var result = await Sut.DownloadAsync(ValidDocumentId(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Content.ShouldNotBeEmpty();
        result.Value.DocumentId.ShouldNotBeNullOrEmpty();
        result.Value.SourceUrl.ShouldNotBeNullOrEmpty();
        result.Value.SessionId.ShouldNotBeNullOrEmpty();
        result.Value.AcquiredBy.ShouldNotBeNull();
        result.Value.AcquiredBy.ActorId.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Contract: the <see cref="DownloadedDocument"/> type exposes no raw-credential members — the
    /// never-store-credentials guarantee, enforced by reflection (ADR-010 §9).
    /// </summary>
    [Fact]
    public void DownloadedDocument_TypeExposesNoCredentialMembers()
    {
        string[] forbidden = ["password", "passwd", "pwd", "username", "userid", "credential", "secret"];

        var members = typeof(DownloadedDocument)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name);

        foreach (var name in members)
        {
            foreach (var token in forbidden)
            {
                name.Contains(token, StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
                    $"DownloadedDocument must not expose a credential-like member ('{name}' matched '{token}').");
            }
        }
    }
}
