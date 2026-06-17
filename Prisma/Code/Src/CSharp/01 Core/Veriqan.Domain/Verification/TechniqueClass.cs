namespace ExxerCube.Prisma.Veriqan.Domain.Verification;

/// <summary>
/// Classifies the algorithmic technique used by a validation rule to produce its finding.
/// Recorded on every <see cref="RuleFinding"/> so downstream consumers can assess the
/// confidence level and auditability of each check result.
/// </summary>
public enum TechniqueClass
{
    /// <summary>
    /// Pure arithmetic or string-equality comparison: fully reproducible, zero randomness.
    /// The vast majority of checklist checks (CL-xx) use this class.
    /// </summary>
    Deterministic = 0,

    /// <summary>
    /// Lightweight computer-vision heuristics (e.g. perceptual image hashing, SSIM),
    /// not backed by a trained ML model.  Results are stable given the same inputs but
    /// may differ from human judgement.
    /// </summary>
    LightweightCv = 1,

    /// <summary>
    /// A trained machine-learning model (e.g. VLM, classifier). Results may vary with
    /// temperature or model version; provenance fields carry the model identifier.
    /// </summary>
    Ml = 2,
}
