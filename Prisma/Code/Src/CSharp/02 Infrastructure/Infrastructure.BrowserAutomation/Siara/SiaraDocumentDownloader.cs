using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.Siara;

/// <summary>
/// The real SIARA document downloader (MVP-PATH 1.1): adapts the existing browser-automation scraper
/// (<see cref="INavigationTarget"/> + <see cref="IBrowserAutomationAgent"/>) into the
/// <see cref="IDocumentDownloader"/> port the Orion worker consumes, behind the credential-free SIARA auth
/// seam (ADR-010).
/// </summary>
/// <remarks>
/// <para>
/// Railway-Oriented and fail-closed: it resolves the configured <see cref="ISiaraSessionProvider"/>
/// (<see cref="ISiaraSessionProviderResolver"/>), acquires a credential-free <see cref="SiaraSession"/>,
/// hydrates this scope's browser with the session's storage-state, navigates to SIARA, finds the requested
/// document among the links SIARA presents, and downloads it. Any failed step yields a failure result —
/// never an empty-but-successful download. The returned <see cref="DownloadedDocument"/> carries the
/// trustworthy <see cref="SiaraActor"/> and session id that authorized the pull, realizing per-document
/// non-repudiation (ADR-010 P2).
/// </para>
/// <para>
/// The session is always released in a finally so an acquired session never leaks, even on a mid-pull
/// failure. Browser teardown is left to the DI scope (the Playwright adapter is disposed at scope end);
/// the launch/attach lifecycle versus the provider's own attach is exercised end-to-end against the
/// cookie-faithful simulator (the 1.1 integration test).
/// </para>
/// </remarks>
public sealed class SiaraDocumentDownloader : IDocumentDownloader
{
    private readonly ISiaraSessionProviderResolver _resolver;
    private readonly IBrowserAutomationAgent _agent;
    private readonly IBrowserSessionContext _sessionContext;
    private readonly INavigationTarget _navigationTarget;
    private readonly SiaraAuthOptions _authOptions;
    private readonly ILogger<SiaraDocumentDownloader> _logger;

    /// <summary>Initializes a new instance of the <see cref="SiaraDocumentDownloader"/> class.</summary>
    /// <param name="resolver">The fail-closed resolver for the configured SIARA session provider.</param>
    /// <param name="agent">The browser automation agent (navigation + download).</param>
    /// <param name="sessionContext">The browser session capability used to hydrate the authenticated state.</param>
    /// <param name="navigationTarget">The SIARA navigation target (base URL + file discovery).</param>
    /// <param name="authOptions">The SIARA auth options (per-mode acquisition parameters).</param>
    /// <param name="logger">The logger instance.</param>
    public SiaraDocumentDownloader(
        ISiaraSessionProviderResolver resolver,
        IBrowserAutomationAgent agent,
        IBrowserSessionContext sessionContext,
        [FromKeyedServices("siara")] INavigationTarget navigationTarget,
        IOptions<SiaraAuthOptions> authOptions,
        ILogger<SiaraDocumentDownloader> logger)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(sessionContext);
        ArgumentNullException.ThrowIfNull(navigationTarget);
        ArgumentNullException.ThrowIfNull(authOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _resolver = resolver;
        _agent = agent;
        _sessionContext = sessionContext;
        _navigationTarget = navigationTarget;
        _authOptions = authOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<DownloadedDocument>> DownloadAsync(string documentId, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<DownloadedDocument>();
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            return Result<DownloadedDocument>.WithFailure("Document id cannot be null or empty.");
        }

        // Fail closed: if the configured auth mode has no registered provider, never proceed unauthenticated.
        var providerResult = _resolver.Resolve();
        if (providerResult.IsCancelled())
        {
            return ResultExtensions.Cancelled<DownloadedDocument>();
        }

        if (providerResult.IsFailure || providerResult.Value is null)
        {
            return Result<DownloadedDocument>.WithFailure(providerResult.Errors);
        }

        var provider = providerResult.Value;

        // SessionPassthrough attaches a storage-state to an EXISTING browser, so one must be launched
        // before acquisition; the login modes launch their own browser inside AcquireAsync (so we do not
        // pre-launch for them — the adapter's launch is not idempotent and a second launch would leak).
        if (_authOptions.AuthMode == SiaraAuthMode.SessionPassthrough)
        {
            var launch = await _agent.LaunchBrowserAsync(cancellationToken).ConfigureAwait(false);
            if (launch.IsCancelled())
            {
                return ResultExtensions.Cancelled<DownloadedDocument>();
            }

            if (launch.IsFailure)
            {
                return Result<DownloadedDocument>.WithFailure(launch.Errors);
            }
        }

        var sessionResult = await provider.AcquireAsync(BuildSessionRequest(documentId), cancellationToken).ConfigureAwait(false);
        if (sessionResult.IsCancelled())
        {
            return ResultExtensions.Cancelled<DownloadedDocument>();
        }

        if (sessionResult.IsFailure || sessionResult.Value is null)
        {
            return Result<DownloadedDocument>.WithFailure(sessionResult.Errors);
        }

        var session = sessionResult.Value;

        try
        {
            return await PullDocumentAsync(session, documentId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Always release so an acquired session never leaks, even on a mid-pull failure. Use a fresh
            // token: release must run even when the caller's token has been cancelled.
            var release = await provider.ReleaseAsync(session, CancellationToken.None).ConfigureAwait(false);
            if (release.IsFailure)
            {
                _logger.LogWarning(
                    "Failed to release SIARA session {SessionId}: {Error}",
                    session.SessionId,
                    release.Error);
            }
        }
    }

    private async Task<Result<DownloadedDocument>> PullDocumentAsync(
        SiaraSession session,
        string documentId,
        CancellationToken cancellationToken)
    {
        // The browser is already launched (passthrough pre-launches above; the login modes launch inside
        // AcquireAsync). Hydrate it with the credential-free authenticated storage-state the session
        // captured, so the scrape runs authenticated regardless of which mode acquired it.
        if (!string.IsNullOrWhiteSpace(session.StorageStateRef))
        {
            var hydrate = await _sessionContext.LoadStorageStateAsync(session.StorageStateRef, cancellationToken).ConfigureAwait(false);
            if (hydrate.IsCancelled())
            {
                return ResultExtensions.Cancelled<DownloadedDocument>();
            }

            if (hydrate.IsFailure)
            {
                return Result<DownloadedDocument>.WithFailure(hydrate.Errors);
            }
        }

        var navigate = await _agent.NavigateToAsync(_navigationTarget.BaseUrl, cancellationToken).ConfigureAwait(false);
        if (navigate.IsCancelled())
        {
            return ResultExtensions.Cancelled<DownloadedDocument>();
        }

        if (navigate.IsFailure)
        {
            return Result<DownloadedDocument>.WithFailure(navigate.Errors);
        }

        var filesResult = await _navigationTarget.RetrieveDocumentsAsync(_agent, cancellationToken).ConfigureAwait(false);
        if (filesResult.IsCancelled())
        {
            return ResultExtensions.Cancelled<DownloadedDocument>();
        }

        if (filesResult.IsFailure || filesResult.Value is null)
        {
            return Result<DownloadedDocument>.WithFailure(filesResult.Errors);
        }

        var file = SelectDocument(filesResult.Value, documentId);
        if (file is null)
        {
            return Result<DownloadedDocument>.WithFailure(
                $"Document '{documentId}' was not found among the {filesResult.Value.Count} file(s) SIARA presented.");
        }

        var downloadResult = await _agent.DownloadFileAsync(file.Url, cancellationToken).ConfigureAwait(false);
        if (downloadResult.IsCancelled())
        {
            return ResultExtensions.Cancelled<DownloadedDocument>();
        }

        if (downloadResult.IsFailure || downloadResult.Value is null)
        {
            return Result<DownloadedDocument>.WithFailure(downloadResult.Errors);
        }

        var downloaded = downloadResult.Value;
        if (downloaded.Content.Length == 0)
        {
            return Result<DownloadedDocument>.WithFailure($"SIARA returned empty content for document '{documentId}'.");
        }

        _logger.LogInformation(
            "Downloaded SIARA document {DocumentId} ({Size} bytes) authorized by actor {ActorId} on session {SessionId}",
            documentId,
            downloaded.Content.Length,
            session.AcquiredBy.ActorId,
            session.SessionId);

        return Result<DownloadedDocument>.Success(new DownloadedDocument
        {
            Content = downloaded.Content,
            DocumentId = documentId,
            SourceUrl = file.Url,
            Format = file.Format,
            AcquiredBy = session.AcquiredBy,
            SessionId = session.SessionId,
        });
    }

    /// <summary>
    /// Selects the SIARA file the caller asked for: an exact file-name match, else a file whose name or URL
    /// contains the id, else — when the id is itself an absolute URL — the document at that URL.
    /// </summary>
    private static DownloadableFile? SelectDocument(IReadOnlyList<DownloadableFile> files, string documentId)
    {
        var exact = files.FirstOrDefault(f => string.Equals(f.FileName, documentId, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var contains = files.FirstOrDefault(f =>
            f.FileName.Contains(documentId, StringComparison.OrdinalIgnoreCase) ||
            f.Url.Contains(documentId, StringComparison.OrdinalIgnoreCase));
        if (contains is not null)
        {
            return contains;
        }

        return Uri.IsWellFormedUriString(documentId, UriKind.Absolute)
            ? new DownloadableFile { Url = documentId, FileName = documentId, Format = FileFormat.Unknown }
            : null;
    }

    private SiaraSessionRequest BuildSessionRequest(string documentId) => new()
    {
        // Per-mode fields; each provider reads only what its mode needs. No raw credentials are carried.
        ExistingContextEndpoint = _authOptions.Passthrough.StorageStateRef,
        MaxWaitForHuman = _authOptions.Interactive.MaxWaitForHuman,
        RequestedBy = documentId,
    };
}
