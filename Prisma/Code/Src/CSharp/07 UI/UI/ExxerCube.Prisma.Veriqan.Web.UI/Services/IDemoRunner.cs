using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Services;

/// <summary>
/// Decides, per submitted PDF, whether to run the real Veriqan verification pipeline or fall
/// back to a canned <see cref="DemoDataService"/> case, per <see cref="Options.DemoOptions"/>.
/// </summary>
/// <remarks>
/// Registered <b>scoped</b> (see <c>Program.cs</c>) because it is itself resolved from the
/// Blazor circuit's own DI scope; it opens a further, independent scope per
/// <see cref="RunAsync"/> call to resolve the scoped <c>IVerificationPipeline</c>.
/// </remarks>
public interface IDemoRunner
{
    /// <summary>
    /// Runs (or simulates) verification for the given PDF bytes and returns a demo-shaped
    /// outcome. Never throws for expected failure modes (oversized PDF, live pipeline
    /// failure/timeout) — all such cases surface as a failed <see cref="Result{T}"/> or a
    /// canned fallback, per the configured <see cref="Options.DemoOptions"/>.
    /// </summary>
    /// <param name="pdf">Raw PDF bytes submitted by the user.</param>
    /// <param name="fileName">Original file name (used for canned-case lookup and pipeline submission).</param>
    /// <param name="cancellationToken">Token propagated to every downstream call.</param>
    Task<Result<DemoRunOutcome>> RunAsync(
        byte[] pdf,
        string fileName,
        CancellationToken cancellationToken = default);
}
