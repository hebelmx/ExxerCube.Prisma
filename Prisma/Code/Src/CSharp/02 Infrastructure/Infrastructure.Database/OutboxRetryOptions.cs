// <copyright file="OutboxRetryOptions.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Infrastructure.Database;

/// <summary>
/// Configuration options for <see cref="Services.OutboxRetryWorker"/>.
/// Bind from <c>appsettings.json</c> section <c>"OutboxRetry"</c>.
/// </summary>
public sealed class OutboxRetryOptions
{
    /// <summary>Configuration section name used with <see cref="IConfiguration.GetSection"/>.</summary>
    public const string SectionName = "OutboxRetry";

    /// <summary>
    /// Gets or sets the maximum number of re-publish attempts before an outbox event is
    /// moved to dead-letter status. Defaults to <see cref="Services.OutboxRetryWorker.DefaultMaxRetries"/> (5).
    /// </summary>
    public int MaxRetries { get; set; } = Services.OutboxRetryWorker.DefaultMaxRetries;

    /// <summary>
    /// Gets or sets the interval between outbox scan ticks.
    /// Defaults to <see cref="Services.OutboxRetryWorker.DefaultScanInterval"/> (30 seconds).
    /// </summary>
    public TimeSpan ScanInterval { get; set; } = Services.OutboxRetryWorker.DefaultScanInterval;

    /// <summary>
    /// Gets or sets the maximum number of outbox events processed per tick.
    /// Defaults to 50.
    /// </summary>
    public int BatchSize { get; set; } = 50;
}
