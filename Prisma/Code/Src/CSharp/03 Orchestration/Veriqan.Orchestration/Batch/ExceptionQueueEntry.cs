using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Batch;

/// <summary>
/// Represents a submission that could not be processed successfully, placed on the
/// exception queue so the batch can continue without aborting.
/// </summary>
/// <param name="Submission">The original statement submission that failed.</param>
/// <param name="ErrorMessage">
/// Human-readable error description — from <c>Result.Error</c> for pipeline failures,
/// or from <see cref="System.Exception.Message"/> for unexpected exceptions.
/// </param>
/// <param name="IsException">
/// <see langword="true"/> when the entry originates from an unhandled .NET exception;
/// <see langword="false"/> when the pipeline returned a <c>Result</c> failure.
/// </param>
public sealed record ExceptionQueueEntry(
    StatementSubmission Submission,
    string ErrorMessage,
    bool IsException);
