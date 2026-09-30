// <copyright file="ComplianceActionKind.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// SmartEnum for compliance actions derived from legal directives.
/// </summary>
public sealed class ComplianceActionKind : EnumModel
{
    /// <summary>Action not determined.</summary>
    public static readonly ComplianceActionKind Unknown = new(-1, "Unknown", "Desconocido");

    /// <summary>Block/aseguramiento of assets or accounts.</summary>
    public static readonly ComplianceActionKind Block = new(0, "Block", "Bloqueo/Aseguramiento");

    /// <summary>Unblock/desembargo of assets or accounts.</summary>
    public static readonly ComplianceActionKind Unblock = new(1, "Unblock", "Desbloqueo/Desembargo");

    /// <summary>Document request.</summary>
    public static readonly ComplianceActionKind Document = new(2, "Document", "Documentación");

    /// <summary>Transfer of funds.</summary>
    public static readonly ComplianceActionKind Transfer = new(3, "Transfer", "Transferencia");

    /// <summary>Information request.</summary>
    public static readonly ComplianceActionKind Information = new(4, "Information", "Información");

    /// <summary>Ignore or no-op action.</summary>
    public static readonly ComplianceActionKind Ignore = new(5, "Ignore", "Ignorar");

    /// <summary>Future or unmatched action type.</summary>
    public static readonly ComplianceActionKind Other = new(999, "Other", "Otro");

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplianceActionKind"/> class.
    /// Parameterless for EF/materialization.
    /// </summary>
    public ComplianceActionKind()
    {
    }

    private ComplianceActionKind(int value, string name, string displayName)
        : base(value, name, displayName)
    {
    }

    /// <summary>
    /// Creates a ComplianceActionKind from an integer value.
    /// </summary>
    public static ComplianceActionKind FromValue(int value) => FromValue<ComplianceActionKind>(value);

    /// <summary>
    /// Creates a ComplianceActionKind from a name.
    /// </summary>
    public static ComplianceActionKind FromName(string name) => FromName<ComplianceActionKind>(name);

    /// <summary>
    /// Implicit conversion to int for storage/serialization.
    /// </summary>
    public static implicit operator int(ComplianceActionKind value) => value.Value;

    /// <summary>
    /// Implicit conversion from int for convenience.
    /// </summary>
    public static implicit operator ComplianceActionKind(int value) => FromValue(value);
}
