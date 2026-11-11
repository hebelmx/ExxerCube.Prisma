// <copyright file="RequirementRef.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Traceability;

/// <summary>
/// A reference to a single PRD requirement, used to link QA artefacts back to
/// the product specification.
/// </summary>
/// <param name="Id">The requirement identifier (e.g. <c>"REQ-A1-01"</c>).</param>
/// <param name="Title">A short, human-readable title for the requirement.</param>
/// <param name="Section">
/// Optional PRD section reference (e.g. <c>"§3.1"</c>) for locating the requirement
/// in the source document.
/// </param>
public sealed record RequirementRef(string Id, string Title, string? Section = null);
