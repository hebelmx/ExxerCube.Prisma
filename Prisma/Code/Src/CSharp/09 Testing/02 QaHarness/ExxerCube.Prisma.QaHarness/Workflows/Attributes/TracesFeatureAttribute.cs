// <copyright file="TracesFeatureAttribute.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Workflows.Attributes;

/// <summary>
/// Annotates an <see cref="IWorkflow"/> or <c>IDomainValidator</c> class to declare that
/// its execution provides evidence for the specified product feature.
/// Multiple attributes may be applied to a single class.
/// </summary>
/// <remarks>
/// The <c>DefaultWorkflowRunner</c> reads these attributes via reflection after each run
/// and calls <c>ITraceabilityMap.AddEntry</c> to record the linkage automatically.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class TracesFeatureAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="TracesFeatureAttribute"/> with the
    /// specified feature identifier.
    /// </summary>
    /// <param name="id">
    /// The stable feature identifier (e.g. <c>"F-OCR-01"</c>).
    /// </param>
    public TracesFeatureAttribute(string id)
    {
        Id = id;
    }

    /// <summary>
    /// Gets the feature identifier this artefact traces to.
    /// </summary>
    public string Id { get; }
}
