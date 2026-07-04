using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Services;

/// <summary>
/// Maps a real <see cref="VerificationOutcome"/> (produced by <c>IVerificationPipeline</c>)
/// onto the demo-shaped <see cref="DemoStatementCase"/> view model consumed by the demo pages.
/// </summary>
/// <remarks>
/// Pulled forward from VLD-S4 because <see cref="DemoRunner"/> (VLD-S2) needs the seam for its
/// live-success path. See <see cref="VerificationOutcomeMapper"/> for the current (minimal)
/// implementation and what VLD-S4 is expected to add.
/// </remarks>
public interface IVerificationOutcomeMapper
{
    /// <summary>
    /// Maps <paramref name="outcome"/> to a <see cref="DemoStatementCase"/> for the given
    /// submitted <paramref name="fileName"/>.
    /// </summary>
    DemoStatementCase Map(VerificationOutcome outcome, string fileName);
}
