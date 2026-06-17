namespace ExxerCube.Prisma.Veriqan.Domain.Enums;

/// <summary>
/// Identifies a discrete reference-data capability that one or more checklist checks depend on.
/// Each capability maps to a section of the <see cref="ReferenceData.VecReferenceBundle"/>.
/// When that section is absent or empty in the bundle the corresponding capability is
/// <see cref="ReferenceCapabilityStatus.InsufficientData"/> and checks that require it
/// must emit <c>INSUFFICIENT_DATA</c> rather than producing a false pass or fail.
/// </summary>
public enum ReferenceCapability
{
    /// <summary>
    /// TASA (interest-rate) table — maps to <c>InterestRates</c>.
    /// Required by rate-comparison checks (e.g. checklist items that verify the printed CAT/TASA).
    /// </summary>
    Rate = 1,

    /// <summary>
    /// Tolerance bands — maps to <c>ToleranceConfig</c>.
    /// Required by numeric near-equal checks (currency, points, rewards-pesos comparisons).
    /// </summary>
    Tolerances = 2,

    /// <summary>
    /// Prior-month closing values — maps to <c>PriorStatements</c>.
    /// Required by cross-period balance checks (items 17, 36, 40).
    /// </summary>
    PriorStatement = 3,

    /// <summary>
    /// Ground-truth transaction detail — maps to <c>ExpectedTransactions</c>.
    /// Required by transaction reconciliation checks (items 45, 58).
    /// </summary>
    ExpectedTransactions = 4,

    /// <summary>
    /// Promotional inserts — maps to <c>Promotions</c>.
    /// Required by promotional-insert presence and validity checks (item 49).
    /// </summary>
    Promotions = 5,

    /// <summary>
    /// Mandatory legends — maps to <c>MandatoryLegends</c>.
    /// Required by legend-presence checks (item 46).
    /// </summary>
    Legends = 6,

    /// <summary>
    /// Sequential catalog images — maps to <c>SequentialImages</c>.
    /// Required by ordered-image presence checks (item 47).
    /// </summary>
    CatalogImages = 7,
}
