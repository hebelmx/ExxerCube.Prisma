// <copyright file="ArticleValidationResult.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.ValueObjects;

using ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Represents the result of validating an Expediente against Article 4 and Article 17 CNBV regulations.
/// Article 4 specifies required fields per operation type (100-104).
/// Article 17 specifies grounds for legal rejection.
/// </summary>
public class ArticleValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the Expediente satisfies all Article 4 required fields for its operation type.
    /// </summary>
    public bool PassesArticle4 { get; set; }

    /// <summary>
    /// Gets or sets the list of missing required field names per Article 4.
    /// Empty if PassesArticle4 = true.
    /// </summary>
    public List<string> MissingRequiredFields { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of Article 17 rejection reasons detected.
    /// Empty if the requirement is legally valid and compliant.
    /// Non-empty list indicates the bank may legally reject this requirement.
    /// </summary>
    public List<RejectionReason> RejectionReasons { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether the Expediente is legally rejectable under Article 17.
    /// True if RejectionReasons is non-empty.
    /// </summary>
    public bool IsRejectable { get; set; }

    /// <summary>
    /// Gets or sets detailed notes explaining validation failures or rejection grounds.
    /// Used for human review and CNBV notification.
    /// </summary>
    public string? ValidationNotes { get; set; }

    /// <summary>
    /// Gets the validation state for this article validation result.
    /// </summary>
    public ValidationState Validation { get; } = new();
}
