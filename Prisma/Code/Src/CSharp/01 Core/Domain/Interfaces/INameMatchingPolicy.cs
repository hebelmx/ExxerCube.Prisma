namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Specialized <see cref="IMatchingPolicy"/> for person/authority NAME fields: fuzzy, alias- and
/// accent-aware matching with conservative thresholds. Used to route name fields to name-specific matching
/// so that genuine disagreements (homonyms) across XML/OCR/DOCX sources are surfaced as conflicts rather than
/// silently treated as agreeing. A marker abstraction so the Application layer can depend on it without
/// referencing the Infrastructure implementation.
/// </summary>
public interface INameMatchingPolicy : IMatchingPolicy
{
}
