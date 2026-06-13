namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Extraction (Quality → OCR → Fusion) completed; the fused expediente has been persisted to shared storage
/// and is ready for the Reconciliator (Classification → Export). This is the cross-process handoff event of
/// the second edge of the Three-Actors split — Extractor (Athena) → Reconciliator — analogous to
/// <see cref="DocumentDownloadedEvent"/> on the Downloader → Extractor edge (MVP-PATH 1.4, ADR-011).
/// </summary>
/// <remarks>
/// Following the owner-chosen <em>shared-storage reference</em> contract, the event carries only a
/// storage-<em>relative</em> <see cref="Path"/> to the serialized fused expediente (the two processes may
/// mount the shared volume at different absolute paths). The Reconciliator resolves and loads it via
/// <see cref="ExxerCube.Prisma.Domain.Interfaces.IExpedienteHandoffStore"/>; the raw document never crosses
/// this edge (data minimization, MVP A5). The light provenance counts are for logging/telemetry only.
/// </remarks>
public record ExtractionCompletedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the source file (stable across the whole pipeline).
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the storage-relative path to the serialized fused expediente handoff artifact (for example
    /// <c>2026/06/12/{fileId}.fusion.json</c>, relative to the shared storage base both the Extractor and
    /// Reconciliator processes mount). The Extractor stamps this; the Reconciliator resolves it against its
    /// own configured base. Empty for in-process / single-service flows.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of fields the upstream fusion stage reconciled (provenance/telemetry only).
    /// </summary>
    public int FieldsFused { get; init; }

    /// <summary>
    /// Gets the number of conflicts the upstream fusion stage detected (provenance/telemetry only).
    /// </summary>
    public int ConflictsDetected { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractionCompletedEvent"/> class.
    /// </summary>
    public ExtractionCompletedEvent()
    {
        EventType = nameof(ExtractionCompletedEvent);
    }
}
