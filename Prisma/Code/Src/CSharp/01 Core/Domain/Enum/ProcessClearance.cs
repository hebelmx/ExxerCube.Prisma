namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// The pipeline stage a process is cleared to initiate work in (MVP-PATH 1.5, A5).
/// Carried in the clearance token on every cross-process handoff event.
/// </summary>
public enum ProcessClearance
{
    /// <summary>Orion Downloader — may push DocumentDownloadedEvent to the Extractor.</summary>
    Download = 0,

    /// <summary>Athena Extractor — may push ExtractionCompletedEvent to the Reconciliator.</summary>
    Extract = 1,

    /// <summary>Reconciliator — final stage; does not push to any further process hub.</summary>
    Reconcile = 2,
}
