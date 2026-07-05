using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Resolves the concrete higher-stage implementations available for a given
/// <see cref="FieldKind"/>, so <see cref="FieldResolutionOrchestrator"/> does not need to know
/// how those stages are constructed or wired.
/// </summary>
/// <remarks>
/// This is the plumbing seam a later epic (VERIQAN-E2.2+) uses to hand the orchestrator real
/// fuzzy/semantic/LLM stage implementations without changing <see cref="FieldResolutionOrchestrator"/>
/// itself. As of this chunk (VERIQAN-E2 foundation), the only registered implementation is
/// <see cref="EmptyFieldStageProvider"/>, which returns no stages for any field — combined with
/// every <see cref="FieldEscalationLadder"/> still being empty (VERIQAN-E1), this keeps behavior
/// unchanged until an actual fallback stage is registered.
/// </remarks>
public interface IFieldStageProvider
{
    /// <summary>
    /// Returns the higher-stage implementations registered for <paramref name="fieldKind"/>,
    /// keyed implicitly by each stage's own <see cref="IFieldResolutionStage{TValue}.Stage"/>.
    /// </summary>
    /// <typeparam name="TValue">The type of the field's value.</typeparam>
    /// <param name="fieldKind">Which field the caller is resolving.</param>
    /// <returns>
    /// The stages available for <paramref name="fieldKind"/>, or an empty list when none are
    /// registered (the case for every field as of this chunk).
    /// </returns>
    IReadOnlyList<IFieldResolutionStage<TValue>> GetHigherStages<TValue>(FieldKind fieldKind);
}
