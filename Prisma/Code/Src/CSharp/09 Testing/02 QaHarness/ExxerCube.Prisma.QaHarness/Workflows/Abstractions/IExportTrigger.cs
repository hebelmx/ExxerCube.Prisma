// <copyright file="IExportTrigger.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Workflows.Abstractions;

/// <summary>
/// Abstraction over the mechanism that initiates an export run in the processing pipeline.
/// </summary>
/// <remarks>
/// The concrete production adapter (<c>AdaptiveExporterExportTrigger</c>) is registered by
/// <c>AddQaHarness()</c>. This interface is <b>optional</b> — when not registered in DI the
/// <c>ExportWorkflow</c> still awaits the <c>ExportCompletedEvent</c> stream, but records that
/// no trigger adapter was available in its outputs.
/// </remarks>
public interface IExportTrigger
{
    /// <summary>
    /// Initiates an export operation in the pipeline.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the trigger call.</param>
    Task TriggerExportAsync(CancellationToken cancellationToken = default);
}
