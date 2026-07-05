using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Services;

/// <summary>
/// Maps a real <see cref="VerificationOutcome"/> (produced by <c>IVerificationPipeline</c>)
/// onto the demo-shaped <see cref="Models.DemoStatementCase"/> view model consumed by the demo pages.
/// </summary>
/// <remarks>
/// Pulled forward from VLD-S4 because <see cref="DemoRunner"/> (VLD-S2) needs the seam for its
/// live-success path. See <see cref="VerificationOutcomeMapper"/> (VLD-S4a) for the
/// ledger-enriched implementation: real tiers, honest labels, and DOF-numeral/brand citations
/// sourced from the check ledger, plus per-finding visual/locator passthrough.
/// </remarks>
public interface IVerificationOutcomeMapper
{
    /// <summary>
    /// Maps <paramref name="outcome"/> to a <see cref="Models.DemoStatementCase"/> for the given
    /// submitted <paramref name="fileName"/>, enriching findings from the real check ledger and
    /// attaching the marked-page hero PNGs.
    /// </summary>
    /// <param name="outcome">The pipeline's verification outcome. <see langword="null"/> is a failure, not a throw.</param>
    /// <param name="markedPagePngs">
    /// In-memory marked-page hero PNGs, keyed by 1-based page number. Pass an empty dictionary
    /// when no hero PNGs are available yet (e.g. before VLD-S5 wires the rasterization chain).
    /// A <see langword="null"/> dictionary is treated as empty.
    /// </param>
    /// <param name="fileName">The submitted file name. <see langword="null"/> is a failure, not a throw.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Result<Models.DemoStatementCase> Map(
        VerificationOutcome outcome,
        IReadOnlyDictionary<int, byte[]> markedPagePngs,
        string fileName,
        CancellationToken cancellationToken = default);
}
