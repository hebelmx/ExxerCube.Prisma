namespace ExxerCube.Prisma.Veriqan.Application.Binding;

/// <summary>
/// Placeholder for the structured statement model that will be produced by the
/// OCR / extraction pipeline in <b>Epic 3</b>.
/// </summary>
/// <remarks>
/// <para>
/// Story 2.3 wires the context-binding layer (bundle → product → <see cref="VerificationContext"/>)
/// but intentionally does <em>not</em> implement statement extraction — that is scope for Epic 3.
/// </para>
/// <para>
/// <see cref="VerificationContext.StatementModel"/> holds a nullable reference of this type.
/// When non-null it will eventually carry extracted field values, section boundaries, and
/// the raw OCR text needed by checklist checks.
/// For now all context objects set this to <see langword="null"/>.
/// </para>
/// <para>
/// TODO (Epic 3): replace or expand this type with a real <c>ExtractedStatementModel</c>
/// containing field values, section offsets, and confidence scores.
/// </para>
/// </remarks>
public sealed class StatementModelPlaceholder
{
    // Intentionally empty — this type serves only as a typed slot in VerificationContext.
    // Epic 3 will add members here (extracted fields, section boundaries, raw OCR output, etc.).
}
