// <copyright file="RequirementType.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Represents CNBV (Comisión Nacional Bancaria y de Valores) requirement types
/// based on R29-2911 regulations and Disposiciones SIARA (Sept 4, 2018).
/// Values match official CNBV type codes from legal documentation.
/// </summary>
/// <remarks>
/// Legal basis documented in Prisma/Docs/Legal/SmartEnum_RequirementTypes.md.
/// Each type has specific keyword patterns for automated classification.
/// Unknown types are stored in RequirementTypeDictionary for dynamic extension.
/// </remarks>
public class RequirementType : EnumModel
{
    /// <summary>
    /// Solicitud de Información (Information Request) - Type 100.
    /// Legal Basis: Article 142 LIC (Ley de Instituciones de Crédito).
    /// Keywords: "solicita información", "estados de cuenta".
    /// Authority: Judicial, Fiscal, Administrative.
    /// </summary>
    public static readonly RequirementType InformationRequest
        = new(100, "InformationRequest", "Solicitud de Información");

    /// <summary>
    /// Aseguramiento/Bloqueo (Seizure/Freezing) - Type 101.
    /// Legal Basis: Article 2(V)(b) - Immediate blocking of accounts.
    /// Keywords: "asegurar", "bloquear", "embargar".
    /// Authority: Judicial, UIF, FGR.
    /// Response Time: SAME DAY execution required.
    /// </summary>
    public static readonly RequirementType Aseguramiento
        = new(101, "Aseguramiento", "Aseguramiento/Bloqueo");

    /// <summary>
    /// Desbloqueo (Unblocking) - Type 102.
    /// Legal Basis: R29 Type 102 - Release of previously blocked funds.
    /// Keywords: "desbloquear", "liberar".
    /// Authority: Judicial, UIF.
    /// Response Time: 1-2 days typical.
    /// </summary>
    public static readonly RequirementType Desbloqueo
        = new(102, "Desbloqueo", "Desbloqueo");

    /// <summary>
    /// Transferencia Electrónica (Electronic Transfer) - Type 103.
    /// Legal Basis: R29 Type 103 - Transfer frozen funds to government account.
    /// Keywords: "transferir", "CLABE".
    /// Authority: FGR, SAT, UIF.
    /// Response Time: 2-5 days (after unblocking).
    /// </summary>
    public static readonly RequirementType Transferencia
        = new(103, "Transferencia", "Transferencia Electrónica");

    /// <summary>
    /// Situación de Fondos (Cashier's Check / Put at Disposal) - Type 104.
    /// Legal Basis: R29 Type 104 - Issue cashier's check to authority.
    /// Keywords: "cheque de caja", "poner a disposición".
    /// Authority: Judicial.
    /// Response Time: 3-5 days (physical instrument).
    /// </summary>
    public static readonly RequirementType SituacionFondos
        = new(104, "SituacionFondos", "Situación de Fondos");

    /// <summary>
    /// Unknown requirement type - Type 999.
    /// Used for unrecognized requirements at classification time.
    /// Triggers lookup in RequirementTypeDictionary for dynamic types.
    /// Allows system evolution without code changes when new legal requirements emerge.
    /// </summary>
    public static readonly RequirementType Unknown
        = new(999, "Unknown", "Desconocido");

    /// <summary>
    /// Initializes a new instance of the <see cref="RequirementType"/> class.
    /// Parameterless constructor required by EF Core for entity materialization.
    /// </summary>
    public RequirementType()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequirementType"/> class with specified values.
    /// </summary>
    /// <param name="value">The CNBV official type code.</param>
    /// <param name="name">The internal name (English).</param>
    /// <param name="displayName">The official Spanish name per CNBV regulations.</param>
    private RequirementType(int value, string name, string displayName = "")
        : base(value, name, displayName)
    {
    }

    // Convenience methods for cleaner API

    /// <summary>
    /// Creates a RequirementType instance from an integer value.
    /// </summary>
    /// <param name="value">The CNBV type code (100, 101, 102, 103, 104, or 999 for Unknown).</param>
    /// <returns>Matching RequirementType or Unknown if not found.</returns>
    public static RequirementType FromValue(int value) => FromValue<RequirementType>(value);

    /// <summary>
    /// Creates a RequirementType instance from a name.
    /// </summary>
    /// <param name="name">The internal name (e.g., "InformationRequest", "Aseguramiento").</param>
    /// <returns>Matching RequirementType or Unknown if not found.</returns>
    public static RequirementType FromName(string name) => FromName<RequirementType>(name);

    /// <summary>
    /// Creates a RequirementType instance from a display name.
    /// </summary>
    /// <param name="displayName">The Spanish display name (e.g., "Solicitud de Información").</param>
    /// <returns>Matching RequirementType or Unknown if not found.</returns>
    public static RequirementType FromDisplayName(string displayName) => FromDisplayName<RequirementType>(displayName);

    /// <summary>
    /// Implicit conversion to int for database storage and comparisons.
    /// </summary>
    /// <param name="type">The RequirementType to convert.</param>
    public static implicit operator int(RequirementType type) => type.Value;

    /// <summary>
    /// Implicit conversion from int to RequirementType.
    /// </summary>
    /// <param name="value">The CNBV type code.</param>
    public static implicit operator RequirementType(int value) => FromValue(value);
}
