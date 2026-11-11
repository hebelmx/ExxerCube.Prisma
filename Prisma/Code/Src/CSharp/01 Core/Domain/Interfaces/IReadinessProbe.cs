// <copyright file="IReadinessProbe.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Reports whether a worker's processing loop has actually started and is ready to do work.
/// </summary>
/// <remarks>
/// Drives the readiness probe (<c>/health/ready</c>) so it reflects real startup state — the worker has
/// subscribed/entered its poll loop — rather than mere object construction. Each hosted worker registers the
/// component it actually drives (the Athena Extractor pipeline, the Orion SIARA watch loop) as the
/// readiness probe; the per-service <c>IHealthCheckService</c> reads it.
/// </remarks>
public interface IReadinessProbe
{
    /// <summary>
    /// Gets a value indicating whether the worker has started and is ready to process work.
    /// </summary>
    bool IsReady { get; }
}
