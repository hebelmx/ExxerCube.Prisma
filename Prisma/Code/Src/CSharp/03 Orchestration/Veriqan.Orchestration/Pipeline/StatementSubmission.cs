using ExxerCube.Prisma.Veriqan.Application.Ports;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;

/// <summary>
/// Immutable submission request carrying a raw PDF and the lookup key used to
/// resolve the reference-data bundle and product for the verification run.
/// </summary>
/// <param name="Pdf">Raw PDF bytes to verify.</param>
/// <param name="FileName">Original file name (informational).</param>
/// <param name="ContextKey">Statement context key for bundle + product lookup.</param>
public sealed record StatementSubmission(
    byte[] Pdf,
    string FileName,
    StatementContextKey ContextKey);
