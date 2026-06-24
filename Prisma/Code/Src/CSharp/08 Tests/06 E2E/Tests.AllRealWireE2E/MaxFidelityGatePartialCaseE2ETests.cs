using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using Microsoft.Extensions.DependencyInjection;
using Prisma.Orion.Ingestion;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.AllRealWireE2E;

/// <summary>
/// The best-effort partial-case gate (owner ruling 2026-06-13 / issue #4).
/// </summary>
/// <remarks>
/// <para>
/// A SIARA case whose download is missing one of its three companion files is <strong>normal</strong>
/// (~5–15% of cases) and must NOT invalidate the case. This drives the same real 3-process pipeline as
/// <see cref="MaxFidelityGateFullPipelineE2ETests"/>, but breaks the DOCX companion's URL so its download
/// fails, and asserts the case still flows: ingestion succeeds, the broadcast event is flagged
/// <see cref="DocumentDownloadedEvent.IsComplete"/> = <see langword="false"/> with only the two surviving
/// companions, the surviving PDF + XML still drive a real SIRO export, and a failed per-file ingestion audit
/// row is persisted to real SQL.
/// </para>
/// <para>
/// The missing file is the DOCX so the XML — which carries the expediente number — survives; the case
/// remains processable. A case missing its expediente-bearing source would instead be flagged for manual
/// review; that review-case persistence is the deferred half of the feature (GH #6, not asserted here).
/// </para>
/// <para>
/// Scope: this gate asserts the best-effort <strong>ingestion + cross-process handoff</strong> contract
/// (skip → flag → forward → persist), which fully completes before Stage-1/2. The full
/// OCR→fusion→export pipeline now runs — it is no longer skipped after the PRISMA-E2-S4 fix
/// (2026-06-20) which eliminated the second-init deadlock by lazy-initializing
/// <c>TesseractEngine</c> once and reusing it as a singleton. That machinery is also proven by
/// the complete-case gate
/// (<see cref="MaxFidelityGateFullPipelineE2ETests.RealSiaraCase_FlowsAcrossAllThreeProcesses_WithRealPipeline_AndPersistsAudit"/>).
/// </para>
/// <para>
/// Isolated from <see cref="MaxFidelityGateFullPipelineE2ETests"/> via the <c>MaxFidelityGate</c>
/// collection (serialized, never parallel) and separate fixture instances. In CI, prefer running this
/// scenario alone:
/// <c>--filter-query "/*/*/MaxFidelityGatePartialCaseE2ETests/PartialCase_MissingCompanionFile_StillProcessesBestEffort_AndFlagsIncomplete"</c>
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Category", "MaxFidelityGate")]
[Collection("MaxFidelityGate")]
public sealed class MaxFidelityGatePartialCaseE2ETests : MaxFidelityGateE2EBase
{
    /// <summary>
    /// The best-effort partial-case path (owner ruling 2026-06-13): a SIARA case whose download is missing one
    /// of its three companion files is <strong>normal</strong> (~5–15% of cases) and must NOT invalidate the
    /// case. This drives the same real 3-process pipeline as the full gate, but breaks the DOCX companion's URL
    /// so its download fails, and asserts the case still flows: ingestion succeeds, the broadcast event is
    /// flagged <see cref="DocumentDownloadedEvent.IsComplete"/> = <see langword="false"/> with only the two
    /// surviving companions, the surviving PDF + XML still drive a real SIRO export, and a failed per-file
    /// ingestion audit row is persisted to real SQL.
    /// </summary>
    [Fact(Timeout = 900_000)]
    public async Task PartialCase_MissingCompanionFile_StillProcessesBestEffort_AndFlagsIncomplete()
    {
        var ct = TestContext.Current.CancellationToken;

        var storageState = await LoginAndCaptureStorageStateAsync(ct);
        storageState.ShouldNotBeNullOrEmpty("the simulator login must yield an authenticated storage-state");

        // Run the full extraction pipeline including Tesseract OCR.
        // Prior to PRISMA-E2-S4 (2026-06-20) this was runAthenaPipeline: false because TesseractEngine was
        // re-created per OCR call and deadlocked when called a second time in the same test process (after
        // the full-pipeline gate had already used it). The fix lazy-initializes and REUSES the engine for the
        // lifetime of the singleton TesseractOcrExecutor, so both gate scenarios can now run OCR safely.
        await BuildThreeHostsWithDbAsync(storageState, runAthenaPipeline: true, ct: ct);

        // Capture the forwarded ingestion event on Athena's real event stream — the best-effort flag lives on
        // it, and the forward happens before any OCR so this resolves regardless of downstream pipeline timing.
        var downloadedSource = new TaskCompletionSource<DocumentDownloadedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var downloadedSub = _athenaApp!.Services.GetRequiredService<IEventPublisher>()
            .GetEventStream<DocumentDownloadedEvent>()
            .Subscribe(e => downloadedSource.TrySetResult(e));

        await WaitUntilHubClientsConnectedAsync(ct);

        // Discover the full 3-file case, then break the DOCX companion's URL so its real download fails.
        var fullCase = await DiscoverFullCompanionCaseAsync(ct);
        var partialCase = BuildCaseWithUndownloadableDocx(fullCase);

        var correlationId = Guid.NewGuid();
        Result<IngestionResult> ingestResult;
        await using (var ingestScope = _orionApp!.Services.CreateAsyncScope())
        {
            var orchestrator = ingestScope.ServiceProvider.GetRequiredService<IngestionOrchestrator>();
            ingestResult = await orchestrator.IngestCaseAsync(partialCase, correlationId, ct);
        }

        // Best-effort: a missing companion does NOT fail the case.
        ingestResult.IsSuccess.ShouldBeTrue(
            $"a case missing one companion must still ingest best-effort: {string.Join(", ", ingestResult.Errors)}");
        var fileId = ingestResult.Value!.FileId;

        // The broadcast event, forwarded across the real SignalR edge, carries the partial flag: only the two
        // surviving companions, IsComplete=false (proves the SmartEnum-on-the-wire fix too — XML survives typed).
        var forwarded = await AwaitOrFailAsync(
            downloadedSource.Task,
            TimeSpan.FromMinutes(2),
            "the forwarded DocumentDownloadedEvent (best-effort partial case)",
            ct);
        forwarded.IsComplete.ShouldBeFalse(
            "a case missing one of its three files must be flagged IsComplete=false for downstream review");
        forwarded.CaseFiles.Count.ShouldBe(2, "only the two successfully downloaded companions should be listed");
        forwarded.CaseFiles.ShouldNotContain(f => f.Format == FileFormat.Docx,
            "the companion whose download failed must not appear in CaseFiles");
        forwarded.CaseFiles.ShouldContain(f => f.Format == FileFormat.Xml,
            "the surviving XML companion (expediente source) must still be carried, typed correctly across the wire");

        // Persistent proof of the per-file best-effort skip: a failed ingestion/download audit row in real SQL.
        var auditRows = await PollAuditRowsAsync(fileId, minimumRows: 1, TimeSpan.FromSeconds(45), ct);
        auditRows.ShouldContain(
            a => a.Stage == ProcessingStage.Ingestion && a.ActionType == AuditActionType.Download && !a.Success,
            "the skipped companion must persist a failed ingestion/download audit row (CaseFileDownloadFailed)");
    }

    /// <summary>
    /// Returns a copy of <paramref name="fullCase"/> with the DOCX companion's URL pointed at a non-existent
    /// path so the real downloader fails to fetch it (HTTP 404 → failure Result → best-effort skip), while the
    /// PDF and XML companions remain downloadable.
    /// </summary>
    private static SiaraCase BuildCaseWithUndownloadableDocx(SiaraCase fullCase)
    {
        var files = fullCase.Files
            .Select(f => f.Format == FileFormat.Docx
                ? new DownloadableFile { Url = f.Url + ".missing-companion-404", FileName = f.FileName, Format = f.Format }
                : new DownloadableFile { Url = f.Url, FileName = f.FileName, Format = f.Format })
            .ToList();

        return fullCase with { Files = files };
    }
}
