// <copyright file="TraceabilitySourceKind.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Traceability;

/// <summary>
/// Identifies the kind of QA artefact that originated a <see cref="TraceabilityEntry"/>.
/// </summary>
public enum TraceabilitySourceKind
{
    /// <summary>The entry was produced by an <c>IWorkflow</c> execution.</summary>
    Workflow,

    /// <summary>The entry was produced by an <c>IDomainValidator</c> execution.</summary>
    Validator,

    /// <summary>The entry was added manually by the QA agent or a test author.</summary>
    Manual,
}
