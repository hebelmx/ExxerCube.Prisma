// <copyright file="TracesRequirementAttribute.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Workflows.Attributes;

/// <summary>
/// Annotates an <see cref="IWorkflow"/> or <c>IDomainValidator</c> class to declare that
/// its execution provides evidence for the specified PRD requirement.
/// Multiple attributes may be applied to a single class.
/// </summary>
/// <remarks>
/// The <c>DefaultWorkflowRunner</c> reads these attributes via reflection after each run
/// and calls <c>ITraceabilityMap.AddEntry</c> to record the linkage automatically.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class TracesRequirementAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="TracesRequirementAttribute"/> with the
    /// specified requirement identifier.
    /// </summary>
    /// <param name="id">
    /// The stable PRD requirement identifier (e.g. <c>"REQ-A1-01"</c>).
    /// </param>
    public TracesRequirementAttribute(string id)
    {
        Id = id;
    }

    /// <summary>
    /// Gets the PRD requirement identifier this artefact traces to.
    /// </summary>
    public string Id { get; }
}
