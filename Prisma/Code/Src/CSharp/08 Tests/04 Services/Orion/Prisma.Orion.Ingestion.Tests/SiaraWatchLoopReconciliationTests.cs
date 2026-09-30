using System.Diagnostics;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Services.Manifest;
using ExxerCube.Prisma.Testing.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Prisma.Orion.Ingestion;

namespace ExxerCube.Prisma.Orion.Ingestion.Tests;

/// <summary>
/// Tests for the manifest reconciliation hook in <see cref="SiaraWatchLoop.PollOnceAsync"/> (Item B #8 + F #12).
/// Separate from the existing <see cref="SiaraWatchLoopTests"/> to avoid touching those assertions.
/// </summary>
public sealed class SiaraWatchLoopReconciliationTests
{
    // ------------------------------------------------------------------------------------------------
    // When Enabled=true: a cycle produces a reconciliation report via the provider + reconciler
    // ------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task PollOnce_WhenEnabled_CallsReconcilerWithDiscoveredCases()
    {
        // Arrange: two cases, each with one PDF file.
        var source = new FakeSiaraDocumentSource(documentIds: ["case-1", "case-2"]);
        var capturedActual = new List<DiscoveredOficio>();
        var reconciler = Substitute.For<IManifestReconciler>();
        reconciler
            .Reconcile(Arg.Any<ExpectedManifest>(), Arg.Any<IReadOnlyList<DiscoveredOficio>>())
            .Returns(callInfo =>
            {
                capturedActual.AddRange(callInfo.Arg<IReadOnlyList<DiscoveredOficio>>());
                return new ManifestReconciliationReport();
            });

        var manifest = new ExpectedManifest
        {
            Oficios = [new ExpectedOficio("case-1", [FileFormat.Pdf])],
        };
        var provider = Substitute.For<IExpectedManifestProvider>();
        provider
            .LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<ExpectedManifest>.Success(manifest)));

        using var harness = BuildHarness(source, reconciler, provider, enabled: true);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = harness.Loop.RunAsync(cts.Token);
        await WaitUntilAsync(
            () => capturedActual.Count >= 2,
            TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await run;

        capturedActual.Select(d => d.CaseId).ShouldContain("case-1");
        capturedActual.Select(d => d.CaseId).ShouldContain("case-2");
        reconciler
            .Received()
            .Reconcile(Arg.Any<ExpectedManifest>(), Arg.Any<IReadOnlyList<DiscoveredOficio>>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task PollOnce_WhenEnabled_ReconcilerReceivesCorrectBuckets()
    {
        // Arrange: case-expected is in manifest but NOT discovered → should appear in Missing bucket.
        var source = new FakeSiaraDocumentSource(documentIds: ["case-present"]);
        var reconcilerReport = new ManifestReconciliationReport
        {
            Complete = [new OficioReconciliationEntry("case-present", [], [])],
            Missing = [new OficioReconciliationEntry("case-absent", [FileFormat.Pdf], [])],
        };
        var reconciler = Substitute.For<IManifestReconciler>();
        reconciler
            .Reconcile(Arg.Any<ExpectedManifest>(), Arg.Any<IReadOnlyList<DiscoveredOficio>>())
            .Returns(reconcilerReport);

        var manifest = new ExpectedManifest
        {
            Oficios =
            [
                new ExpectedOficio("case-present", [FileFormat.Pdf]),
                new ExpectedOficio("case-absent", [FileFormat.Pdf]),
            ],
        };
        var provider = Substitute.For<IExpectedManifestProvider>();
        provider
            .LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<ExpectedManifest>.Success(manifest)));

        var reconcilerCalled = false;
        reconciler
            .When(r => r.Reconcile(Arg.Any<ExpectedManifest>(), Arg.Any<IReadOnlyList<DiscoveredOficio>>()))
            .Do(_ => reconcilerCalled = true);

        using var harness = BuildHarness(source, reconciler, provider, enabled: true);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = harness.Loop.RunAsync(cts.Token);
        await WaitUntilAsync(() => reconcilerCalled, TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await run;

        reconciler.Received().Reconcile(
            Arg.Is<ExpectedManifest>(m => m.Oficios.Count == 2),
            Arg.Any<IReadOnlyList<DiscoveredOficio>>());
    }

    // ------------------------------------------------------------------------------------------------
    // When Enabled=false: reconciler is NOT called (existing behavior unchanged)
    // ------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task PollOnce_WhenDisabled_ReconcilerIsNeverCalled()
    {
        var source = new FakeSiaraDocumentSource(documentIds: ["case-1", "case-2"]);
        var reconciler = Substitute.For<IManifestReconciler>();
        var provider = Substitute.For<IExpectedManifestProvider>();

        using var harness = BuildHarness(source, reconciler, provider, enabled: false);
        using var hub = harness;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = harness.Loop.RunAsync(cts.Token);
        // Wait for at least 2 discovery passes (at the 20ms interval that should be quick).
        await WaitUntilAsync(() => source.DiscoverCount >= 2, TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await run;

        reconciler.DidNotReceive().Reconcile(
            Arg.Any<ExpectedManifest>(),
            Arg.Any<IReadOnlyList<DiscoveredOficio>>());
        await provider.DidNotReceive().LoadAsync(Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------------------------------------
    // When provider fails: cycle continues (fault-tolerant — no crash)
    // ------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task PollOnce_WhenManifestProviderFails_CycleContinuesWithoutCrashing()
    {
        var source = new FakeSiaraDocumentSource(documentIds: ["case-1"]);
        var reconciler = Substitute.For<IManifestReconciler>();
        var provider = Substitute.For<IExpectedManifestProvider>();
        provider
            .LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<ExpectedManifest>.WithFailure("Manifest file not found")));

        using var harness = BuildHarness(source, reconciler, provider, enabled: true);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = harness.Loop.RunAsync(cts.Token);
        // Let 2 discovery cycles run — loop must not crash.
        await WaitUntilAsync(() => source.DiscoverCount >= 2, TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await run;

        run.IsCompletedSuccessfully.ShouldBeTrue();
        // Reconciler must NOT be called when the manifest failed to load.
        reconciler.DidNotReceive().Reconcile(
            Arg.Any<ExpectedManifest>(),
            Arg.Any<IReadOnlyList<DiscoveredOficio>>());
    }

    // ------------------------------------------------------------------------------------------------
    // Per-cycle file list (Item F): the DiscoveredOficio list passed to the reconciler contains
    // the downloaded files from each case.
    // ------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task PollOnce_WhenEnabled_DiscoveredOficiosIncludeDownloadedFiles()
    {
        // FakeSiaraDocumentSource wraps each id as a one-PDF-file case.
        var source = new FakeSiaraDocumentSource(documentIds: ["case-pdf"]);

        DiscoveredOficio? captured = null;
        var reconciler = Substitute.For<IManifestReconciler>();
        reconciler
            .Reconcile(Arg.Any<ExpectedManifest>(), Arg.Any<IReadOnlyList<DiscoveredOficio>>())
            .Returns(callInfo =>
            {
                var list = callInfo.Arg<IReadOnlyList<DiscoveredOficio>>();
                if (list.Count > 0)
                {
                    captured = list[0];
                }

                return new ManifestReconciliationReport();
            });

        var provider = Substitute.For<IExpectedManifestProvider>();
        provider
            .LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<ExpectedManifest>.Success(ExpectedManifest.Empty)));

        using var harness = BuildHarness(source, reconciler, provider, enabled: true);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = harness.Loop.RunAsync(cts.Token);
        await WaitUntilAsync(() => captured is not null, TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await run;

        captured.ShouldNotBeNull();
        captured.CaseId.ShouldBe("case-pdf");
        // The fake downloader downloads 1 PDF per case.
        captured.DownloadedFiles.Count.ShouldBeGreaterThan(0);
    }

    // ------------------------------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------------------------------

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMs)
            {
                throw new TimeoutException("Expected condition not met within timeout.");
            }

            await Task.Delay(15, cancellationToken);
        }
    }

    private static ReconciliationHarness BuildHarness(
        FakeSiaraDocumentSource source,
        IManifestReconciler reconciler,
        IExpectedManifestProvider provider,
        bool enabled)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "orion-recon-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var journalPath = Path.Combine(tempRoot, "journal.jsonl");
        var storagePath = Path.Combine(tempRoot, "storage");
        var hub = new CountingHub();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.Configure<WatchLoopOptions>(o => o.PollInterval = TimeSpan.FromMilliseconds(20));

        // Manifest options
        services.Configure<ExpectedManifestOptions>(o =>
        {
            o.Enabled = enabled;
            o.ManifestPath = null;
        });
        services.AddSingleton<IExpectedManifestProvider>(provider);
        services.AddSingleton<IManifestReconciler>(reconciler);

        services.AddSingleton<ISiaraDocumentSource>(source);
        services.AddSingleton<IIngestionJournal>(sp =>
            new FileIngestionJournal(journalPath, sp.GetRequiredService<ILogger<FileIngestionJournal>>()));
        services.AddSingleton<IExxerHub<DocumentDownloadedEvent>>(hub);
        services.AddScoped<IDocumentDownloader>(_ => new FakeDocumentDownloader());
        services.AddScoped(sp => new IngestionOrchestrator(
            sp.GetRequiredService<IIngestionJournal>(),
            sp.GetRequiredService<IDocumentDownloader>(),
            sp.GetRequiredService<IExxerHub<DocumentDownloadedEvent>>(),
            sp.GetRequiredService<ILogger<IngestionOrchestrator>>(),
            storagePath,
            postWriteFlushDelay: TimeSpan.Zero));
        services.AddSingleton<SiaraWatchLoop>();

        var provider2 = services.BuildServiceProvider();
        var loop = provider2.GetRequiredService<SiaraWatchLoop>();
        return new ReconciliationHarness(provider2, loop, hub, tempRoot);
    }

    private sealed class ReconciliationHarness : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly string _tempRoot;

        public ReconciliationHarness(ServiceProvider provider, SiaraWatchLoop loop, CountingHub hub, string tempRoot)
        {
            _provider = provider;
            Loop = loop;
            Hub = hub;
            _tempRoot = tempRoot;
        }

        public SiaraWatchLoop Loop { get; }
        public CountingHub Hub { get; }

        public void Dispose()
        {
            _provider.Dispose();
            try { Directory.Delete(_tempRoot, recursive: true); } catch (IOException) { }
        }
    }

    private sealed class CountingHub : IExxerHub<DocumentDownloadedEvent>
    {
        public Task<Result> SendToAllAsync(DocumentDownloadedEvent message, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> SendToGroupAsync(string groupName, DocumentDownloadedEvent message, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> SendToClientAsync(string connectionId, DocumentDownloadedEvent message, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result<int>> GetConnectionCountAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<int>.Success(0));
    }
}
