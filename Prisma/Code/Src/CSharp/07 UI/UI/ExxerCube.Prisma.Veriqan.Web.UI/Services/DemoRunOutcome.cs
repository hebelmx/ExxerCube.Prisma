namespace ExxerCube.Prisma.Veriqan.Web.UI.Services;

/// <summary>
/// Result of a single <see cref="IDemoRunner.RunAsync"/> call: the resolved demo-shaped
/// case, plus whether it came from a real live pipeline run or a canned fallback.
/// </summary>
/// <param name="Case">The demo-shaped statement case to render.</param>
/// <param name="IsLive">
/// <see langword="true"/> when <paramref name="Case"/> was produced by a real
/// <c>IVerificationPipeline.ProcessAsync</c> run; <see langword="false"/> when it is a canned
/// <see cref="DemoDataService"/> fixture (live mode disabled, or live run failed/timed out and
/// fell back).
/// </param>
public sealed record DemoRunOutcome(DemoStatementCase Case, bool IsLive);
