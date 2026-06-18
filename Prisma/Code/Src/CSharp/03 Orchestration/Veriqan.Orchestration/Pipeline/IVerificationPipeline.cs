using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;

/// <summary>
/// Orchestrates the full end-to-end VEC verification pipeline for a single PDF statement:
/// ingestion → field extraction → context binding → rule engine → verdict aggregation.
/// </summary>
/// <remarks>
/// The pipeline is <b>scoped</b> — each request gets its own instance so that
/// scoped infrastructure services (e.g. <c>VeriqanDbContext</c>) are properly bounded.
/// Use the batch processor (<c>IBatchProcessor</c>) to process multiple statements with bounded
/// concurrency; it creates a fresh scope per item automatically.
/// </remarks>
public interface IVerificationPipeline
{
    /// <summary>
    /// Processes a single statement submission through all verification stages and returns
    /// the aggregated outcome.
    /// </summary>
    /// <param name="submission">The PDF bytes, file name, and context key for the run.</param>
    /// <param name="ct">Cancellation token propagated to every stage.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> containing the <see cref="VerificationOutcome"/>
    /// (even when the verdict is <c>Blocked</c> — that is a valid business outcome, not a failure);
    /// a cancelled result when <paramref name="ct"/> is signalled;
    /// or a failure result when an unrecoverable pipeline error occurs (e.g. invalid PDF, missing data).
    /// </returns>
    Task<Result<VerificationOutcome>> ProcessAsync(
        StatementSubmission submission,
        CancellationToken ct = default);
}
