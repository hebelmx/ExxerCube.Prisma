// <copyright file="ConflictingSourceValue.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.ValueObjects;

using ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Represents a single source's contribution to a cross-document field conflict (alertamiento).
/// Carries the source type (XML/DOCX/PDF/OCR) and the value that source provided for the field.
/// </summary>
public sealed class ConflictingSourceValue
{
    /// <summary>
    /// Gets the document source type that provided this value.
    /// </summary>
    public SourceType Source { get; }

    /// <summary>
    /// Gets the value provided by this source for the conflicting field.
    /// <see langword="null"/> when the source had no value for the field.
    /// </summary>
    public string? Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConflictingSourceValue"/> class.
    /// </summary>
    /// <param name="source">The source type.</param>
    /// <param name="value">The value provided by this source, or <see langword="null"/>.</param>
    public ConflictingSourceValue(SourceType source, string? value)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Value = value;
    }
}
