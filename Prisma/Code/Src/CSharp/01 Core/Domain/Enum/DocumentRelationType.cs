// <copyright file="DocumentRelationType.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Represents the relationship type of a document to previous requirements.
/// Used to handle reminders, scope expansions, and clarifications without duplicating processing.
/// </summary>
public enum DocumentRelationType
{
    /// <summary>
    /// Standard new requirement - process as new record.
    /// </summary>
    NewRequirement = 0,

    /// <summary>
    /// Recordatorio - Reminder of previous request.
    /// Do not duplicate processing, link to existing record.
    /// Keywords: "RECORDATORIO DEL OFICIO"
    /// </summary>
    Recordatorio = 1,

    /// <summary>
    /// Alcance - Scope expansion of previous requirement.
    /// Create new record but link to original requirement.
    /// Keywords: "ALCANCE AL OFICIO", "AMPLÍA"
    /// </summary>
    Alcance = 2,

    /// <summary>
    /// Precisión - Clarification or correction of previous requirement.
    /// Update existing record, do not create new one.
    /// Keywords: "PRECISIÓN", "ACLARA", "CORRIGE"
    /// </summary>
    Precision = 3
}
