// <copyright file="DocumentRelationType.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Relationship type for follow-up documents per Article 5 of Disposiciones SIARA.
/// Determines how to handle document in R29 reporting and workflow.
/// </summary>
public enum DocumentRelationType
{
    /// <summary>
    /// New requirement (standard case).
    /// Creates new R29 record with new NumeroOficio.
    /// </summary>
    NewRequirement = 0,

    /// <summary>
    /// Recordatorio - Reminder of previous request (Article 5).
    /// Keyword: "recordatorio del oficio número..."
    /// Does NOT create new R29 record.
    /// Updates FechaSolicitud to reminder date.
    /// Does NOT change response deadline.
    /// </summary>
    Recordatorio = 1,

    /// <summary>
    /// Alcance - Scope expansion (Article 5).
    /// Keywords: "alcance al oficio número...", "amplía información solicitada"
    /// Creates NEW R29 record.
    /// References original NumeroOficio.
    /// May add accounts, extend date range, or add subjects.
    /// </summary>
    Alcance = 2,

    /// <summary>
    /// Precisión - Clarification of ambiguous prior request (Article 5).
    /// Keywords: "precisión", "aclara", "corrige"
    /// Updates EXISTING R29 record.
    /// Keeps original NumeroOficio.
    /// Corrects subject names, account numbers, dates, etc.
    /// Documents correction in notes.
    /// </summary>
    Precision = 3,
}
