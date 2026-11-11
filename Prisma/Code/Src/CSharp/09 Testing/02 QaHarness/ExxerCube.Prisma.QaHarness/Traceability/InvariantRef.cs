// <copyright file="InvariantRef.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Traceability;

/// <summary>
/// A reference to a system invariant, used to link QA artefacts to an
/// architectural or domain rule that must always hold.
/// </summary>
/// <param name="Id">The invariant identifier (e.g. <c>"INV-RESULT-01"</c>).</param>
/// <param name="Description">A concise statement of the invariant.</param>
public sealed record InvariantRef(string Id, string Description);
