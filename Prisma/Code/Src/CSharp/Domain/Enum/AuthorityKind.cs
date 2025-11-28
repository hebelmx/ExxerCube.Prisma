// <copyright file="AuthorityKind.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// SmartEnum for authority categories issuing directives.
/// </summary>
public sealed class AuthorityKind : EnumModel
{
    /// <summary>Authority not determined.</summary>
    public static readonly AuthorityKind Unknown = new(0, "Unknown", "Desconocido");

    /// <summary>Comisión Nacional Bancaria y de Valores.</summary>
    public static readonly AuthorityKind CNBV = new(1, "CNBV", "Comisión Nacional Bancaria y de Valores");

    /// <summary>Unidad de Inteligencia Financiera.</summary>
    public static readonly AuthorityKind UIF = new(2, "UIF", "Unidad de Inteligencia Financiera");

    /// <summary>Court or judicial authority.</summary>
    public static readonly AuthorityKind Juzgado = new(3, "Juzgado", "Autoridad Judicial");

    /// <summary>Hacienda/SAT or fiscal authority.</summary>
    public static readonly AuthorityKind Hacienda = new(4, "Hacienda", "Autoridad Fiscal");

    /// <summary>Authority outside the known list.</summary>
    public static readonly AuthorityKind Other = new(999, "Other", "Otra");

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorityKind"/> class.
    /// </summary>
    public AuthorityKind()
    {
    }

    private AuthorityKind(int value, string name, string displayName)
        : base(value, name, displayName)
    {
    }

    /// <summary>
    /// Creates an AuthorityKind from an integer value.
    /// </summary>
    /// <param name="value">Stored integer value.</param>
    public static AuthorityKind FromValue(int value) => FromValue<AuthorityKind>(value);

    /// <summary>
    /// Creates an AuthorityKind from a name.
    /// </summary>
    /// <param name="name">Internal name (e.g., CNBV).</param>
    public static AuthorityKind FromName(string name) => FromName<AuthorityKind>(name);

    /// <summary>
    /// Implicit conversion to int for storage/serialization.
    /// </summary>
    /// <param name="value">The AuthorityKind to convert.</param>
    public static implicit operator int(AuthorityKind value) => value.Value;

    /// <summary>
    /// Implicit conversion from int for convenience.
    /// </summary>
    /// <param name="value">Stored integer value.</param>
    public static implicit operator AuthorityKind(int value) => FromValue(value);
}
