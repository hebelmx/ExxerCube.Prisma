namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Fusion/reconciliation completed successfully (XML + PDF + DOCX merged).
/// </summary>
public record FusionCompletedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the source file.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the unique identifier for the resulting Expediente.
    /// </summary>
    public Guid ExpedienteId { get; init; }

    /// <summary>
    /// Gets the number of fields successfully fused.
    /// </summary>
    public int FieldsFused { get; init; }

    /// <summary>
    /// Gets the number of conflicts detected during fusion.
    /// </summary>
    public int ConflictsDetected { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FusionCompletedEvent"/> class.
    /// </summary>
    public FusionCompletedEvent()
    {
        EventType = nameof(FusionCompletedEvent);
    }
}