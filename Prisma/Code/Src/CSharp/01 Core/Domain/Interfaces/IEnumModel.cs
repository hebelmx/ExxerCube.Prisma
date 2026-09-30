// <copyright file="IEnumModel.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Represents a base interface for enumerations with value, name, and display name.
/// </summary>
public interface IEnumModel
{
    /// <summary>
    /// Gets or sets the integer value of the enumeration.
    /// </summary>
    int Value { get; set; }

    /// <summary>
    /// Gets or sets the name of the enumeration.
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// Gets or sets the display name of the enumeration.
    /// </summary>
    string DisplayName { get; set; }

    /// <summary>
    /// Gets the invalid enumeration instance.
    /// </summary>
    public IEnumModel Invalid { get; }
}
