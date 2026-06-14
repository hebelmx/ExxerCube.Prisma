using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.Siara;

/// <summary>
/// The real SIARA document discovery source (MVP-PATH 1.2): the "list" half of ingestion. It rides the
/// same credential-free SIARA auth seam (ADR-010) the downloader uses, navigates SIARA, and returns the
/// ids of the documents SIARA currently presents so the watch loop knows what to pull.
/// </summary>
/// <remarks>
/// <para>
/// Railway-Oriented and fail-closed: it resolves the configured <see cref="ISiaraSessionProvider"/>
/// (<see cref="ISiaraSessionProviderResolver"/>), keeps <strong>one warm session</strong> across watch-loop
/// cycles (acquire on the first call; re-validate via <see cref="ISiaraSessionProvider.EnsureValidAsync"/>
/// on subsequent calls, re-acquiring only if it has died), hydrates this scope's browser with the session's
/// storage-state, navigates to SIARA, and lists the downloadable files. Any failed step yields a failure
/// result — never a silently-empty success.
/// </para>
/// <para>
/// This instance is <strong>scoped</strong> and is meant to live for the lifetime of the watch loop's
/// single long-lived discovery scope, so the browser and session stay warm. It releases the session on
/// disposal (scope teardown). The browser is launched at most once for the passthrough mode (whose
/// acquisition attaches to an existing browser); the login modes launch their own browser inside
/// acquisition, so this source does not pre-launch for them.
/// </para>
/// </remarks>
public sealed class SiaraDocumentSource : ISiaraDocumentSource, IAsyncDisposable
{
    private static readonly string[] FilePatterns = ["*.pdf", "*.xml", "*.docx"];

    private readonly ISiaraSessionProviderResolver _resolver;
    private readonly IBrowserAutomationAgent _agent;
    private readonly IBrowserSessionContext _sessionContext;
    private readonly INavigationTarget _navigationTarget;
    private readonly SiaraAuthOptions _authOptions;
    private readonly ILogger<SiaraDocumentSource> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private ISiaraSessionProvider? _provider;
    private SiaraSession? _session;
    private bool _browserLaunched;

    /// <summary>Initializes a new instance of the <see cref="SiaraDocumentSource"/> class.</summary>
    /// <param name="resolver">The fail-closed resolver for the configured SIARA session provider.</param>
    /// <param name="agent">The browser automation agent (navigation + file discovery).</param>
    /// <param name="sessionContext">The browser session capability used to hydrate the authenticated state.</param>
    /// <param name="navigationTarget">The SIARA navigation target (base URL + file discovery).</param>
    /// <param name="authOptions">The SIARA auth options (per-mode acquisition parameters).</param>
    /// <param name="logger">The logger instance.</param>
    public SiaraDocumentSource(
        ISiaraSessionProviderResolver resolver,
        IBrowserAutomationAgent agent,
        IBrowserSessionContext sessionContext,
        [FromKeyedServices("siara")] INavigationTarget navigationTarget,
        IOptions<SiaraAuthOptions> authOptions,
        ILogger<SiaraDocumentSource> logger)
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
    public async Task<Result<IReadOnlyList<SiaraCase>>> DiscoverCasesAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<IReadOnlyList<SiaraCase>>();
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var filesResult = await RetrieveFilesLockedAsync(cancellationToken).ConfigureAwait(false);
            if (filesResult.IsCancelled())
            {
                return ResultExtensions.Cancelled<IReadOnlyList<SiaraCase>>();
            }

            if (filesResult.IsFailure || filesResult.Value is null)
            {
                return Result<IReadOnlyList<SiaraCase>>.WithFailure(filesResult.Errors);
            }

            var cases = SiaraCaseGrouping.GroupByCase(filesResult.Value);

            _logger.LogInformation(
                "SIARA case discovery grouped {FileCount} file(s) into {CaseCount} case(s)",
                filesResult.Value.Count,
                cases.Count);

            return Result<IReadOnlyList<SiaraCase>>.Success(cases);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<IReadOnlyList<SiaraCase>>();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Acquires the SIARA session on the first call and keeps it warm thereafter: re-validates the existing
    /// session and only re-acquires when it has died (fail-closed if it cannot be re-acquired).
    /// </summary>
    private async Task<Result<SiaraSession>> EnsureWarmSessionAsync(CancellationToken cancellationToken)
    {
        // Fail closed: if the configured auth mode has no registered provider, never proceed unauthenticated.
        var providerResult = _resolver.Resolve();
        if (providerResult.IsCancelled())
        {
            return ResultExtensions.Cancelled<SiaraSession>();
        }

        if (providerResult.IsFailure || providerResult.Value is null)
        {
            return Result<SiaraSession>.WithFailure(providerResult.Errors);
        }

        _provider = providerResult.Value;

        // Keep the existing session warm rather than re-acquiring every cycle (ADR-010: one acquisition,
        // headless for hours). If re-validation fails, the session has died — re-acquire it.
        if (_session is not null)
        {
            var ensured = await _provider.EnsureValidAsync(_session, cancellationToken).ConfigureAwait(false);
            if (ensured.IsCancelled())
            {
                return ResultExtensions.Cancelled<SiaraSession>();
            }

            if (ensured.IsSuccess && ensured.Value is not null)
            {
                _session = ensured.Value;
                return Result<SiaraSession>.Success(_session);
            }

            _logger.LogWarning(
                "Warm SIARA discovery session {SessionId} could not be re-validated; re-acquiring. {Error}",
                _session.SessionId,
                ensured.Error);
            _session = null;
        }

        return await AcquireSessionAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<SiaraSession>> AcquireSessionAsync(CancellationToken cancellationToken)
    {
        // SessionPassthrough attaches a storage-state to an EXISTING browser, so one must be launched before
        // acquisition (and only once — the adapter's launch is not idempotent). The login modes launch their
        // own browser inside AcquireAsync, so we do not pre-launch for them.
        if (_authOptions.AuthMode == SiaraAuthMode.SessionPassthrough && !_browserLaunched)
        {
            var launch = await _agent.LaunchBrowserAsync(cancellationToken).ConfigureAwait(false);
            if (launch.IsCancelled())
            {
                return ResultExtensions.Cancelled<SiaraSession>();
            }

            if (launch.IsFailure)
            {
                return Result<SiaraSession>.WithFailure(launch.Errors);
            }

            _browserLaunched = true;
        }

        var sessionResult = await _provider!.AcquireAsync(BuildSessionRequest(), cancellationToken).ConfigureAwait(false);
        if (sessionResult.IsCancelled())
        {
            return ResultExtensions.Cancelled<SiaraSession>();
        }

        if (sessionResult.IsFailure || sessionResult.Value is null)
        {
            return Result<SiaraSession>.WithFailure(sessionResult.Errors);
        }

        _session = sessionResult.Value;
        return Result<SiaraSession>.Success(_session);
    }

    /// <summary>
    /// Acquires a warm session (if not already held), hydrates the browser, navigates, and retrieves the raw
    /// file list from SIARA. Must be called while the caller holds <see cref="_gate"/>.
    /// </summary>
    private async Task<Result<IReadOnlyList<DownloadableFile>>> RetrieveFilesLockedAsync(CancellationToken cancellationToken)
    {
        var sessionResult = await EnsureWarmSessionAsync(cancellationToken).ConfigureAwait(false);
        if (sessionResult.IsCancelled())
        {
            return ResultExtensions.Cancelled<IReadOnlyList<DownloadableFile>>();
        }

        if (sessionResult.IsFailure || sessionResult.Value is null)
        {
            return Result<IReadOnlyList<DownloadableFile>>.WithFailure(sessionResult.Errors);
        }

        var session = sessionResult.Value;

        // Hydrate the browser with the credential-free authenticated storage-state the session captured, so
        // the scrape runs authenticated regardless of which mode acquired it.
        if (!string.IsNullOrWhiteSpace(session.StorageStateRef))
        {
            var hydrate = await _sessionContext.LoadStorageStateAsync(session.StorageStateRef, cancellationToken).ConfigureAwait(false);
            if (hydrate.IsCancelled())
            {
                return ResultExtensions.Cancelled<IReadOnlyList<DownloadableFile>>();
            }

            if (hydrate.IsFailure)
            {
                return Result<IReadOnlyList<DownloadableFile>>.WithFailure(hydrate.Errors);
            }
        }

        var navigate = await _agent.NavigateToAsync(_navigationTarget.BaseUrl, cancellationToken).ConfigureAwait(false);
        if (navigate.IsCancelled())
        {
            return ResultExtensions.Cancelled<IReadOnlyList<DownloadableFile>>();
        }

        if (navigate.IsFailure)
        {
            return Result<IReadOnlyList<DownloadableFile>>.WithFailure(navigate.Errors);
        }

        var filesResult = await _navigationTarget.RetrieveDocumentsAsync(_agent, cancellationToken).ConfigureAwait(false);
        if (filesResult.IsCancelled())
        {
            return ResultExtensions.Cancelled<IReadOnlyList<DownloadableFile>>();
        }

        if (filesResult.IsFailure || filesResult.Value is null)
        {
            return Result<IReadOnlyList<DownloadableFile>>.WithFailure(filesResult.Errors);
        }

        _logger.LogInformation(
            "SIARA discovery retrieved {Count} file(s) on session {SessionId} for actor {ActorId}",
            filesResult.Value.Count,
            session.SessionId,
            session.AcquiredBy.ActorId);

        return Result<IReadOnlyList<DownloadableFile>>.Success(filesResult.Value);
    }

    private SiaraSessionRequest BuildSessionRequest() => new()
    {
        // Per-mode fields; each provider reads only what its mode needs. No raw credentials are carried.
        ExistingContextEndpoint = _authOptions.Passthrough.StorageStateRef,
        MaxWaitForHuman = _authOptions.Interactive.MaxWaitForHuman,
        RequestedBy = "siara-document-discovery",
    };

    /// <summary>Releases the warm session on scope teardown so it never leaks; the browser is torn down by the scope.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_provider is not null && _session is not null)
        {
            // Use a fresh token: release must run even during shutdown.
            var release = await _provider.ReleaseAsync(_session, CancellationToken.None).ConfigureAwait(false);
            if (release.IsFailure)
            {
                _logger.LogWarning(
                    "Failed to release warm SIARA discovery session {SessionId}: {Error}",
                    _session.SessionId,
                    release.Error);
            }

            _session = null;
        }

        _gate.Dispose();
    }
}
