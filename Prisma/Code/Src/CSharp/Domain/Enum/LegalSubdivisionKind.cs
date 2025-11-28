// <copyright file="LegalSubdivisionKind.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// SmartEnum for CNBV subdivision codes.
/// </summary>
public sealed class LegalSubdivisionKind : EnumModel
{
    /// <summary>Subdivision not determined.</summary>
    public static readonly LegalSubdivisionKind Unknown = new(0, "Unknown", "Desconocido");

    /// <summary>A/AS Especial Aseguramiento.</summary>
    public static readonly LegalSubdivisionKind A_AS = new(1, "A_AS", "A/AS Especial Aseguramiento");

    /// <summary>A/DE Especial Desembargo.</summary>
    public static readonly LegalSubdivisionKind A_DE = new(2, "A_DE", "A/DE Especial Desembargo");

    /// <summary>A/TF Especial Transferencia.</summary>
    public static readonly LegalSubdivisionKind A_TF = new(3, "A_TF", "A/TF Especial Transferencia");

    /// <summary>A/IN Especial Informativo - documentación.</summary>
    public static readonly LegalSubdivisionKind A_IN = new(4, "A_IN", "A/IN Especial Informativo - documentación");

    /// <summary>J/AS Judicial Aseguramiento.</summary>
    public static readonly LegalSubdivisionKind J_AS = new(5, "J_AS", "J/AS Judicial Aseguramiento");

    /// <summary>J/DE Judicial Desembargo.</summary>
    public static readonly LegalSubdivisionKind J_DE = new(6, "J_DE", "J/DE Judicial Desembargo");

    /// <summary>J/IN Judicial Informativo - documentación.</summary>
    public static readonly LegalSubdivisionKind J_IN = new(7, "J_IN", "J/IN Judicial Informativo - documentación");

    /// <summary>H/IN Hacendario Informativo - documentación.</summary>
    public static readonly LegalSubdivisionKind H_IN = new(8, "H_IN", "H/IN Hacendario Informativo - documentación");

    /// <summary>E/AS Operaciones ilícitas Aseguramiento.</summary>
    public static readonly LegalSubdivisionKind E_AS = new(9, "E_AS", "E/AS Operaciones ilícitas Aseguramiento");

    /// <summary>E/DE Operaciones ilícitas Desembargo.</summary>
    public static readonly LegalSubdivisionKind E_DE = new(10, "E_DE", "E/DE Operaciones ilícitas Desembargo");

    /// <summary>E/IN Operaciones ilícitas Informativo - documentación.</summary>
    public static readonly LegalSubdivisionKind E_IN = new(11, "E_IN", "E/IN Operaciones ilícitas Informativo - documentación");

    /// <summary>Subdivision outside the known list.</summary>
    public static readonly LegalSubdivisionKind Other = new(999, "Other", "Otro");

    /// <summary>
    /// Initializes a new instance of the <see cref="LegalSubdivisionKind"/> class.
    /// </summary>
    public LegalSubdivisionKind()
    {
    }

    private LegalSubdivisionKind(int value, string name, string displayName)
        : base(value, name, displayName)
    {
    }

    /// <summary>
    /// Creates a LegalSubdivisionKind from an integer value.
    /// </summary>
    /// <param name="value">Stored integer value.</param>
    public static LegalSubdivisionKind FromValue(int value) => FromValue<LegalSubdivisionKind>(value);

    /// <summary>
    /// Creates a LegalSubdivisionKind from a name.
    /// </summary>
    /// <param name="name">Internal name.</param>
    public static LegalSubdivisionKind FromName(string name) => FromName<LegalSubdivisionKind>(name);

    /// <summary>
    /// Implicit conversion to int for storage/serialization.
    /// </summary>
    /// <param name="value">The LegalSubdivisionKind to convert.</param>
    public static implicit operator int(LegalSubdivisionKind value) => value.Value;

    /// <summary>
    /// Implicit conversion from int for convenience.
    /// </summary>
    /// <param name="value">Stored integer value.</param>
    public static implicit operator LegalSubdivisionKind(int value) => FromValue(value);
}
