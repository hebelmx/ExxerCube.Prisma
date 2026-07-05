using System;
using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// <see cref="IFieldStageProvider"/> that supplies no higher-stage implementations for any
/// <see cref="FieldKind"/>.
/// </summary>
/// <remarks>
/// Was the DI default through VERIQAN-E1; superseded in production by
/// <see cref="DefaultFieldStageProvider"/> as of E2.2 (see <c>VeriqanExtractionExtensions</c>).
/// Kept as a standalone implementation for tests that need to exercise a ladder with no
/// implementation available for its rung (e.g. the "no stage implementation is registered for a
/// referenced rung" escalation-stop path), and for any future scenario needing a deliberately
/// inert stage provider — combined with an all-<see cref="FieldEscalationLadder.PositionalOnly"/>
/// ladder table, returning an empty list here is behavior-neutral.
/// </remarks>
public sealed class EmptyFieldStageProvider : IFieldStageProvider
{
    /// <inheritdoc />
    public IReadOnlyList<IFieldResolutionStage<TValue>> GetHigherStages<TValue>(FieldKind fieldKind)
        => Array.Empty<IFieldResolutionStage<TValue>>();
}
