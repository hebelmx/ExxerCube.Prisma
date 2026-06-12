using System.Diagnostics;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Testing.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Prisma.Orion.Ingestion;

namespace ExxerCube.Prisma.Orion.Ingestion.Tests;

/// <summary>
/// Behavioral tests for <see cref="SiaraWatchLoop"/> (MVP-PATH 1.2). Rather than mock the scope plumbing,
/// these run the loop over a <strong>real</strong> <see cref="ServiceProvider"/> wired with the reference
/// fakes (<see cref="FakeSiaraDocumentSource"/> + <see cref="FakeDocumentDownloader"/>), the real
/// <see cref="IngestionOrchestrator"/>, and the real file-based journal — so they exercise the actual
/// per-document scope creation and the SHA-256 idempotency path, with no live browser.
/// </summary>
public sealed class SiaraWatchLoopTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunAsync_IngestsEachDiscoveredDocument_InItsOwnScope()
    {
        var source = new FakeSiaraDocumentSource(documentIds: ["doc-a", "doc-b", "doc-c"]);
        using var harness = BuildHarness(source);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = harness.Loop.RunAsync(cts.Token);
        await WaitUntilAsync(() => harness.Hub.SentCount >= 3, TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await run;

        // Three distinct documents → three ingests, each broadcast exactly once.
        harness.Hub.SentCount.ShouldBe(3);
        // A fresh DI scope (hence a fresh downloader) per document — never one captured instance.
        harness.DownloaderCreations.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunAsync_RediscoveringSameDocuments_IsIdempotent()
    {
        var source = new FakeSiaraDocumentSource(documentIds: ["doc-a", "doc-b", "doc-c"]);
        using var harness = BuildHarness(source);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = harness.Loop.RunAsync(cts.Token);
        // Let the first cycle ingest, then ensure several more discovery passes run over the same ids.
        await WaitUntilAsync(() => harness.Hub.SentCount >= 3, TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => source.DiscoverCount >= 3, TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await run;

        // The SHA-256 journal makes re-discovery a no-op: still exactly three broadcasts despite many cycles.
        harness.Hub.SentCount.ShouldBe(3);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunAsync_WhenCancelled_StopsPromptly()
    {
        var source = new FakeSiaraDocumentSource(documentIds: ["doc-a", "doc-b"]);
        using var harness = BuildHarness(source, pollInterval: TimeSpan.FromMilliseconds(50));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = harness.Loop.RunAsync(cts.Token);
        await WaitUntilAsync(() => harness.Hub.SentCount >= 2, TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        // The loop must observe cancellation and complete (no hang); the test token guards against a deadlock.
        await run;
        run.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunAsync_WhenEveryDownloadFails_KeepsCyclingWithoutCrashing()
    {
        var source = new FakeSiaraDocumentSource(documentIds: ["doc-a", "doc-b", "doc-c"]);
        using var harness = BuildHarness(source, downloaderFailsClosed: true);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = harness.Loop.RunAsync(cts.Token);
        // A per-document failure must not abort the loop — it should keep discovering on later cycles.
        await WaitUntilAsync(() => source.DiscoverCount >= 2, TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await run;

        harness.Hub.SentCount.ShouldBe(0);
        source.DiscoverCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunAsync_WhenDiscoveryFails_KeepsCyclingWithoutCrashing()
    {
        var source = new FakeSiaraDocumentSource(failClosed: true);
        using var harness = BuildHarness(source);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = harness.Loop.RunAsync(cts.Token);
        // A failed discovery pass must not take the loop down — it should retry on the next cycle.
        await WaitUntilAsync(() => source.DiscoverCount >= 2, TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await run;

        harness.Hub.SentCount.ShouldBe(0);
        source.DiscoverCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    // ------------------------------------------------------------------------------------------------

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken, int timeoutMs = 5000)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.ElapsedMilliseconds > timeoutMs)
            {
                throw new TimeoutException("The expected watch-loop condition was not met within the timeout.");
            }

            await Task.Delay(15, cancellationToken);
        }
    }

    private static Harness BuildHarness(
        FakeSiaraDocumentSource source,
        bool downloaderFailsClosed = false,
        TimeSpan? pollInterval = null)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "orion-watchloop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var journalPath = Path.Combine(tempRoot, "journal.jsonl");
        var storagePath = Path.Combine(tempRoot, "storage");

        var hub = new CountingExxerHub();
        var creations = new int[1];

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.Configure<WatchLoopOptions>(o => o.PollInterval = pollInterval ?? TimeSpan.FromMilliseconds(20));
        services.AddSingleton<ISiaraDocumentSource>(source);
        services.AddSingleton<IIngestionJournal>(sp =>
            new FileIngestionJournal(journalPath, sp.GetRequiredService<ILogger<FileIngestionJournal>>()));
        services.AddSingleton<IExxerHub<DocumentDownloadedEvent>>(hub);
        services.AddScoped<IDocumentDownloader>(_ =>
        {
            Interlocked.Increment(ref creations[0]);
            return new FakeDocumentDownloader(failClosed: downloaderFailsClosed);
        });
        services.AddScoped(sp => new IngestionOrchestrator(
            sp.GetRequiredService<IIngestionJournal>(),
            sp.GetRequiredService<IDocumentDownloader>(),
            sp.GetRequiredService<IExxerHub<DocumentDownloadedEvent>>(),
            sp.GetRequiredService<ILogger<IngestionOrchestrator>>(),
            storagePath));
        services.AddSingleton<SiaraWatchLoop>();

        var provider = services.BuildServiceProvider();
        var loop = provider.GetRequiredService<SiaraWatchLoop>();
        return new Harness(provider, loop, hub, creations, tempRoot);
    }

    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly int[] _creations;
        private readonly string _tempRoot;

        public Harness(ServiceProvider provider, SiaraWatchLoop loop, CountingExxerHub hub, int[] creations, string tempRoot)
        {
            _provider = provider;
            Loop = loop;
            Hub = hub;
            _creations = creations;
            _tempRoot = tempRoot;
        }

        public SiaraWatchLoop Loop { get; }

        public CountingExxerHub Hub { get; }

        public int DownloaderCreations => Volatile.Read(ref _creations[0]);

        public void Dispose()
        {
            _provider.Dispose();
            try
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort temp cleanup; the OS reclaims it regardless.
            }
        }
    }

    /// <summary>Thread-safe counting <see cref="IExxerHub{T}"/> test double — the broadcast oracle.</summary>
    private sealed class CountingExxerHub : IExxerHub<DocumentDownloadedEvent>
    {
        private int _sent;

        public int SentCount => Volatile.Read(ref _sent);

        public Task<Result> SendToAllAsync(DocumentDownloadedEvent message, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _sent);
            return Task.FromResult(Result.Success());
        }

        public Task<Result> SendToGroupAsync(string groupName, DocumentDownloadedEvent message, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> SendToClientAsync(string connectionId, DocumentDownloadedEvent message, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result<int>> GetConnectionCountAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<int>.Success(0));
    }
}
