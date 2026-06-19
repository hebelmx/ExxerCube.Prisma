using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Orchestration.Batch;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;

namespace ExxerCube.Prisma.Veriqan.Worker;

/// <summary>
/// Request body for the <c>POST /batch</c> endpoint.
/// </summary>
/// <param name="Items">Ordered list of statement submissions to verify.</param>
/// <param name="Options">
/// Optional batch execution options (parallelism, resume).
/// When <see langword="null"/>, <see cref="BatchOptions"/> defaults apply.
/// </param>
public sealed record BatchSubmissionRequest(
    IReadOnlyList<StatementSubmission> Items,
    BatchOptions? Options = null);
