namespace ExxerCube.Prisma.Veriqan.Domain.Enums;

/// <summary>
/// Processing lifecycle status of a <see cref="Entities.VerificationJob"/>.
/// </summary>
public enum VerificationJobStatus
{
    /// <summary>Job has been received and is awaiting engine allocation.</summary>
    Pending = 0,

    /// <summary>Verification engine is actively processing the job.</summary>
    Processing = 1,

    /// <summary>All checks have completed and findings are available.</summary>
    Completed = 2,

    /// <summary>The job failed due to an unrecoverable engine error.</summary>
    Failed = 3,

    /// <summary>The job was cancelled before completion.</summary>
    Cancelled = 4,
}
