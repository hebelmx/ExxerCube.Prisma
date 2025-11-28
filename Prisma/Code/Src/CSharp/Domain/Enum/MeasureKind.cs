// <copyright file="MeasureKind.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// SmartEnum for legal measure intent (bloqueo, desbloqueo, transferencia, etc.).
/// </summary>
public sealed class MeasureKind : EnumModel
{
    /// <summary>Measure intent not determined.</summary>
    public static readonly MeasureKind Unknown = new(0, "Unknown", "Desconocido");

    /// <summary>Block/aseguramiento of assets or accounts.</summary>
    public static readonly MeasureKind Block = new(1, "Block", "Bloqueo/Aseguramiento");

    /// <summary>Unblock/desembargo of assets or accounts.</summary>
    public static readonly MeasureKind Unblock = new(2, "Unblock", "Desbloqueo/Desembargo");

    /// <summary>Transfer of funds between accounts.</summary>
    public static readonly MeasureKind TransferFunds = new(3, "TransferFunds", "Transferencia de Fondos");

    /// <summary>Documentation request.</summary>
    public static readonly MeasureKind Documentation = new(4, "Documentation", "Documentación");

    /// <summary>General information request.</summary>
    public static readonly MeasureKind Information = new(5, "Information", "Información");

    /// <summary>Future or unmatched measure type.</summary>
    public static readonly MeasureKind Other = new(999, "Other", "Otro");

    /// <summary>
    /// Initializes a new instance of the <see cref="MeasureKind"/> class.
    /// Parameterless for EF/materialization.
    /// </summary>
    public MeasureKind()
    {
    }

    private MeasureKind(int value, string name, string displayName)
        : base(value, name, displayName)
    {
    }

    /// <summary>
    /// Creates a MeasureKind from an integer value.
    /// </summary>
    public static MeasureKind FromValue(int value) => FromValue<MeasureKind>(value);

    /// <summary>
    /// Creates a MeasureKind from a name.
    /// </summary>
    public static MeasureKind FromName(string name) => FromName<MeasureKind>(name);

    /// <summary>
    /// Implicit conversion to int for storage/serialization.
    /// </summary>
    public static implicit operator int(MeasureKind value) => value.Value;

    /// <summary>
    /// Implicit conversion from int for convenience.
    /// </summary>
    public static implicit operator MeasureKind(int value) => FromValue(value);
}
