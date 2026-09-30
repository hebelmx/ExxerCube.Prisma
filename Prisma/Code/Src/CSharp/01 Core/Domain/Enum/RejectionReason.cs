// <copyright file="RejectionReason.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Represents the legal grounds for rejecting a CNBV requirement (Article 17 validation).
/// Banks may reject requirements that fail to meet legal formalities or lack required information.
/// </summary>
/// <remarks>
/// Legal basis: Disposiciones SIARA Article 17 - Grounds for Rejection.
/// When a requirement is rejected, the bank must notify CNBV within 24 hours via SIARA system,
/// citing the specific Article 17 subsection(s) that apply.
///
/// Article 17 specifies 6 grounds for rejection:
/// I.   No legal authority citation
/// II.  Missing or incomplete authority signature
/// III. Lack of specificity in requested information
/// IV.  Request exceeds legal authority's jurisdiction
/// V.   Missing required data per Article 4
/// VI.  Technical impossibility of compliance
/// </remarks>
public class RejectionReason : EnumModel
{
    /// <summary>
    /// Article 17.I - No legal authority citation.
    /// The requirement fails to cite the specific legal article authorizing the request.
    /// </summary>
    public static readonly RejectionReason NoLegalAuthorityCitation
        = new(1, "NoLegalAuthorityCitation", "Falta citar fundamento legal");

    /// <summary>
    /// Article 17.II - Missing or incomplete authority signature.
    /// The requirement lacks proper signature or seal from the issuing authority.
    /// </summary>
    public static readonly RejectionReason MissingSignature
        = new(2, "MissingSignature", "Falta firma de autoridad");

    /// <summary>
    /// Article 17.III - Lack of specificity in requested information.
    /// The requirement is too vague or ambiguous to execute (e.g., "all accounts" without date range).
    /// </summary>
    public static readonly RejectionReason LackOfSpecificity
        = new(3, "LackOfSpecificity", "Falta especificidad");

    /// <summary>
    /// Article 17.IV - Request exceeds legal authority's jurisdiction.
    /// The issuing authority does not have legal competence for the requested action.
    /// </summary>
    public static readonly RejectionReason ExceedsJurisdiction
        = new(4, "ExceedsJurisdiction", "Excede jurisdicción");

    /// <summary>
    /// Article 17.V - Missing required data per Article 4.
    /// The requirement omits mandatory fields specified in Article 4 for the operation type.
    /// </summary>
    public static readonly RejectionReason MissingRequiredData
        = new(5, "MissingRequiredData", "Falta información requerida");

    /// <summary>
    /// Article 17.VI - Technical impossibility of compliance.
    /// Compliance is technically infeasible (e.g., account doesn't exist, request predates bank records).
    /// </summary>
    public static readonly RejectionReason TechnicalImpossibility
        = new(6, "TechnicalImpossibility", "Imposibilidad técnica");

    /// <summary>
    /// Initializes a new instance of the <see cref="RejectionReason"/> class.
    /// Parameterless constructor required by EF Core for entity materialization.
    /// </summary>
    public RejectionReason()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RejectionReason"/> class with specified values.
    /// </summary>
    /// <param name="value">The rejection reason code.</param>
    /// <param name="name">The internal name.</param>
    /// <param name="displayName">The display name.</param>
    private RejectionReason(int value, string name, string displayName = "")
        : base(value, name, displayName)
    {
    }

    /// <summary>
    /// Creates a RejectionReason instance from an integer value.
    /// </summary>
    /// <param name="value">The rejection reason code.</param>
    /// <returns>Matching RejectionReason instance.</returns>
    public static RejectionReason FromValue(int value) => FromValue<RejectionReason>(value);

    /// <summary>
    /// Creates a RejectionReason instance from a name.
    /// </summary>
    /// <param name="name">The internal name.</param>
    /// <returns>Matching RejectionReason instance.</returns>
    public static RejectionReason FromName(string name) => FromName<RejectionReason>(name);

    /// <summary>
    /// Creates a RejectionReason instance from a display name.
    /// </summary>
    /// <param name="displayName">The display name.</param>
    /// <returns>Matching RejectionReason instance.</returns>
    public static RejectionReason FromDisplayName(string displayName) => FromDisplayName<RejectionReason>(displayName);

    /// <summary>
    /// Implicit conversion to int for database storage and comparisons.
    /// </summary>
    /// <param name="type">The RejectionReason to convert.</param>
    public static implicit operator int(RejectionReason type) => type.Value;

    /// <summary>
    /// Implicit conversion from int to RejectionReason.
    /// </summary>
    /// <param name="value">The rejection reason code.</param>
    public static implicit operator RejectionReason(int value) => FromValue(value);
}
