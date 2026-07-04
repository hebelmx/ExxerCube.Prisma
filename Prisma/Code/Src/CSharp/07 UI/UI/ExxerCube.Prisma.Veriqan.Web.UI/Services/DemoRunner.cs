using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using ExxerCube.Prisma.Veriqan.Web.UI.Options;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Services;

/// <summary>
/// Default <see cref="IDemoRunner"/> implementation: decides live-vs-canned per submission,
/// per <see cref="DemoOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>NEVER use <c>.ConfigureAwait(false)</c> anywhere in this class.</b> This runner is called
/// from the Blazor Server circuit's await chain (VLD-S5) before a <c>StateHasChanged()</c> call —
/// a prior page in this repo crashed its circuit exactly this way (dropping the SynchronizationContext
/// mid-flight via <c>ConfigureAwait(false)</c> in an interactive component). Keep every await here
/// on the default (captured) context.
/// </para>
/// </remarks>
public sealed class DemoRunner : IDemoRunner
{
    /// <summary>
    /// Context key used for all demo submissions — mirrors <c>LivePipelineWiringProofTests</c>
    /// and <c>VecChecklistDemoE2ETests</c> (resolves the <c>Demo_Bank_(Iqubica)</c> reference
    /// bundle sub-directory and the Mar-Abr 2026 period).
    /// </summary>
    private static readonly StatementContextKey DefaultContextKey =
        new("Demo Bank (Iqubica)", PeriodLabel: "Mar-Abr 2026");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IVerificationOutcomeMapper _mapper;
    private readonly DemoDataService _demoDataService;
    private readonly IOptions<DemoOptions> _demoOptions;
    private readonly IOptions<PdfExtractionOptions> _pdfExtractionOptions;
    private readonly ILogger<DemoRunner> _logger;

    /// <summary>Initializes a new <see cref="DemoRunner"/>.</summary>
    public DemoRunner(
        IServiceScopeFactory scopeFactory,
        IVerificationOutcomeMapper mapper,
        DemoDataService demoDataService,
        IOptions<DemoOptions> demoOptions,
        IOptions<PdfExtractionOptions> pdfExtractionOptions,
        ILogger<DemoRunner> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _demoDataService = demoDataService ?? throw new ArgumentNullException(nameof(demoDataService));
        _demoOptions = demoOptions ?? throw new ArgumentNullException(nameof(demoOptions));
        _pdfExtractionOptions = pdfExtractionOptions ?? throw new ArgumentNullException(nameof(pdfExtractionOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<DemoRunOutcome>> RunAsync(
        byte[] pdf,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<DemoRunOutcome>();

        if (pdf is null)
            return Result<DemoRunOutcome>.WithFailure("PDF bytes cannot be null.");

        if (string.IsNullOrWhiteSpace(fileName))
            return Result<DemoRunOutcome>.WithFailure("File name cannot be null or empty.");

        // ------------------------------------------------------------------
        // Global size guard — applies BEFORE both the live-mode branch and any scope/pipeline
        // access, regardless of LiveModeEnabled (an oversized upload is rejected even in canned
        // mode; there is no reason to accept it either way, and this keeps the guard simple and
        // provably first — see RunAsync_OversizedPdf_PipelineNeverInvoked).
        // ------------------------------------------------------------------
        var maxSizeBytes = _pdfExtractionOptions.Value.MaxSizeBytes;
        if (pdf.LongLength > maxSizeBytes)
        {
            return Result<DemoRunOutcome>.WithFailure(
                $"PDF size ({pdf.LongLength} bytes) exceeds the maximum allowed size " +
                $"({maxSizeBytes} bytes).");
        }

        var demoOptions = _demoOptions.Value;

        if (!demoOptions.LiveModeEnabled)
        {
            return Result<DemoRunOutcome>.WithSuccess(
                new DemoRunOutcome(ResolveCannedCase(fileName), IsLive: false));
        }

        Result<DemoRunOutcome> liveResult;
        try
        {
            liveResult = await RunLiveAsync(pdf, fileName, demoOptions.LiveTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The CALLER's own token was cancelled — surface as a Cancelled Result, never a
            // thrown exception, even if it escaped from a path other than the explicitly-handled
            // pipeline.ProcessAsync await inside RunLiveAsync (e.g. the mapper or the async scope
            // disposal via `await using`).
            return ResultExtensions.Cancelled<DemoRunOutcome>();
        }
        catch (Exception ex)
        {
            // Catches everything else, INCLUDING any OperationCanceledException that is not the
            // caller's own cancellation (e.g. one that escapes from the mapper or the scope
            // disposal rather than the pipeline call). The "never throws out of RunAsync" contract
            // must hold for the whole live path, not just the single awaited pipeline call.
            _logger.LogWarning(
                ex, "Live verification pipeline threw for {FileName}.", fileName);
            liveResult = Result<DemoRunOutcome>.WithFailure(
                $"Live verification pipeline threw: {ex.Message}");
        }

        if (liveResult.IsSuccess)
            return liveResult;

        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<DemoRunOutcome>();

        if (demoOptions.FallbackOnFailure)
        {
            _logger.LogWarning(
                "Live verification failed for {FileName}: {Error}. Falling back to canned demo case.",
                fileName, liveResult.Error ?? "<none>");
            return Result<DemoRunOutcome>.WithSuccess(
                new DemoRunOutcome(ResolveCannedCase(fileName), IsLive: false));
        }

        // FallbackOnFailure == false: propagate the failure as a failed Result (never an exception).
        return liveResult;
    }

    /// <summary>
    /// Resolves <see cref="IVerificationPipeline"/> from a FRESH scope (created per call — the
    /// pipeline is registered scoped) and drives it with a timeout linked to
    /// <paramref name="callerToken"/>.
    /// </summary>
    private async Task<Result<DemoRunOutcome>> RunLiveAsync(
        byte[] pdf,
        string fileName,
        TimeSpan liveTimeout,
        CancellationToken callerToken)
    {
        using var timeoutCts = new CancellationTokenSource(liveTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeoutCts.Token);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();

        var submission = new StatementSubmission(
            Pdf: pdf,
            FileName: fileName,
            ContextKey: DefaultContextKey);

        Result<VerificationOutcome> pipelineResult;
        try
        {
            pipelineResult = await pipeline.ProcessAsync(submission, linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            if (callerToken.IsCancellationRequested)
                return ResultExtensions.Cancelled<DemoRunOutcome>();

            // The caller did not cancel — the linked token must have tripped via the timeout.
            return Result<DemoRunOutcome>.WithFailure(
                $"Live verification pipeline timed out after {liveTimeout}.");
        }

        if (pipelineResult.IsFailure)
        {
            return Result<DemoRunOutcome>.WithFailure(
                pipelineResult.Error ?? "Live verification pipeline failed.");
        }

        var mappedCase = _mapper.Map(pipelineResult.Value!, fileName);
        return Result<DemoRunOutcome>.WithSuccess(new DemoRunOutcome(mappedCase, IsLive: true));
    }

    private DemoStatementCase ResolveCannedCase(string fileName) =>
        _demoDataService.GetByFileName(fileName)
        ?? _demoDataService.GetBySignal(VerdictSignal.Green)
        ?? _demoDataService.GetAllCases()[0];
}
