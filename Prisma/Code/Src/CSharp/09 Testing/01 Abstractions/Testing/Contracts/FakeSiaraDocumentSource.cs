using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Hand-written, in-memory reference fake of <see cref="ISiaraDocumentSource"/> (ADR-005 §6): honest
/// Railway-Oriented logic, no mocking framework. It returns a deterministic, configurable set of document
/// ids without a live browser or a real SIARA — so it unblocks the watch-loop tests.
/// </summary>
/// <remarks>
/// Construct with <c>failClosed: true</c> to model the fail-closed path (resolver/provider/auth failure)
/// where the source returns a failure rather than a list. The id list can be reconfigured between calls via
/// <see cref="SetDocumentIds"/> to model SIARA presenting different documents across watch-loop cycles.
/// </remarks>
public sealed class FakeSiaraDocumentSource : ISiaraDocumentSource
{
    private static readonly IReadOnlyList<string> DefaultIds =
    [
        "https://siara.fake/documents/fake-doc-1.pdf",
        "https://siara.fake/documents/fake-doc-2.pdf",
    ];

    private readonly bool _failClosed;
    private readonly object _gate = new();
    private IReadOnlyList<string> _documentIds;
    private int _discoverCount;

    /// <summary>Initializes the fake.</summary>
    /// <param name="failClosed">When <see langword="true"/>, every (non-cancelled) call fails closed.</param>
    /// <param name="documentIds">The ids to present; defaults to two synthetic SIARA document URLs.</param>
    public FakeSiaraDocumentSource(bool failClosed = false, IReadOnlyList<string>? documentIds = null)
    {
        _failClosed = failClosed;
        _documentIds = documentIds ?? DefaultIds;
    }

    /// <summary>Gets the number of times <see cref="DiscoverDocumentIdsAsync"/> has been invoked.</summary>
    public int DiscoverCount
    {
        get { lock (_gate) { return _discoverCount; } }
    }

    /// <summary>Replaces the set of document ids the fake presents on subsequent calls.</summary>
    /// <param name="documentIds">The new ids to present.</param>
    public void SetDocumentIds(IReadOnlyList<string> documentIds)
    {
        ArgumentNullException.ThrowIfNull(documentIds);
        lock (_gate) { _documentIds = documentIds; }
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<string>>> DiscoverDocumentIdsAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<IReadOnlyList<string>>());
        }

        IReadOnlyList<string> ids;
        lock (_gate)
        {
            _discoverCount++;
            ids = _documentIds;
        }

        if (_failClosed)
        {
            return Task.FromResult(Result<IReadOnlyList<string>>.WithFailure(
                "Fake discovery source configured to fail closed (no authenticated SIARA session)."));
        }

        return Task.FromResult(Result<IReadOnlyList<string>>.Success(ids));
    }

    /// <inheritdoc />
    /// <remarks>
    /// The fake synthesizes one <see cref="SiaraCase"/> per document id (treating each id as its own case).
    /// This is the simplest deterministic behaviour that satisfies the contract; tests that need multi-file
    /// cases should use the real <c>SiaraCaseGrouping</c> helper or provide a custom fake.
    /// </remarks>
    public Task<Result<IReadOnlyList<SiaraCase>>> DiscoverCasesAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<IReadOnlyList<SiaraCase>>());
        }

        IReadOnlyList<string> ids;
        lock (_gate)
        {
            ids = _documentIds;
        }

        if (_failClosed)
        {
            return Task.FromResult(Result<IReadOnlyList<SiaraCase>>.WithFailure(
                "Fake discovery source configured to fail closed (no authenticated SIARA session)."));
        }

        // One synthetic case per id, carrying a single placeholder file so the case bundle is non-empty.
        var cases = ids
            .Select(id => new SiaraCase
            {
                CaseId = id,
                Files = new[]
                {
                    new DownloadableFile { Url = id, FileName = id, Format = FileFormat.Unknown },
                },
            })
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<SiaraCase>>.Success((IReadOnlyList<SiaraCase>)cases));
    }
}
