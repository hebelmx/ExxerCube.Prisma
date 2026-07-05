using System;
using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Default <see cref="IFieldStageProvider"/> that supplies no higher-stage implementations for
/// any <see cref="FieldKind"/>.
/// </summary>
/// <remarks>
/// This is the DI default until a later epic (VERIQAN-E2.2+) registers a provider backed by real
/// fuzzy/semantic/LLM stages. Combined with every <see cref="FieldEscalationLadder"/> being empty
/// (VERIQAN-E1), returning an empty list here is behavior-neutral: no field ever escalates past
/// its positional (stage-1) result.
/// </remarks>
public sealed class EmptyFieldStageProvider : IFieldStageProvider
{
    /// <inheritdoc />
    public IReadOnlyList<IFieldResolutionStage<TValue>> GetHigherStages<TValue>(FieldKind fieldKind)
        => Array.Empty<IFieldResolutionStage<TValue>>();
}
