// <copyright file="TracesInvariantAttribute.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Workflows.Attributes;

/// <summary>
/// Annotates an <see cref="IWorkflow"/> or <c>IDomainValidator</c> class to declare that
/// its execution verifies the specified system invariant.
/// Multiple attributes may be applied to a single class.
/// </summary>
/// <remarks>
/// The <c>DefaultWorkflowRunner</c> reads these attributes via reflection after each run
/// and calls <c>ITraceabilityMap.AddEntry</c> to record the linkage automatically.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class TracesInvariantAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="TracesInvariantAttribute"/> with the
    /// specified invariant identifier.
    /// </summary>
    /// <param name="id">
    /// The stable invariant identifier (e.g. <c>"INV-RESULT-01"</c>).
    /// </param>
    public TracesInvariantAttribute(string id)
    {
        Id = id;
    }

    /// <summary>
    /// Gets the invariant identifier this artefact traces to.
    /// </summary>
    public string Id { get; }
}
