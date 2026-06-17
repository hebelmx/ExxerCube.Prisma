using System;

namespace ExxerCube.Prisma.Veriqan.Application.Binding;

/// <summary>
/// <b>Replaced in Story 3.1.</b>
/// <see cref="VerificationContext.StatementModel"/> now carries
/// <see cref="ExxerCube.Prisma.Veriqan.Domain.Extraction.StatementModel"/>.
/// This class is retained only to avoid unnecessary churn in git history and may
/// be removed in a future cleanup pass once all callers have migrated.
/// </summary>
/// <remarks>
/// DO NOT add members here.  Use <see cref="ExxerCube.Prisma.Veriqan.Domain.Extraction.StatementModel"/> instead.
/// </remarks>
[Obsolete("Replaced by ExxerCube.Prisma.Veriqan.Domain.Extraction.StatementModel (Story 3.1). This class has no members and will be removed.")]
public sealed class StatementModelPlaceholder
{
    // Empty tombstone. See replacement type above.
}
