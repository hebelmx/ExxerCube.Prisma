// <copyright file="ApplicationStartupResult.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Hosting;

/// <summary>
/// Describes the outcome of an <see cref="IApplicationHostController.StartAsync"/> call.
/// </summary>
/// <param name="IsHealthy">
/// <see langword="true"/> when all started processes passed their health checks
/// (<c>/health/live</c> returned <c>200 Healthy</c>).
/// </param>
/// <param name="BaseAddress">
/// The Kestrel base URI of the primary hosted process (Web UI or Orion), or
/// <see langword="null"/> when startup failed before a port was bound.
/// </param>
/// <param name="StartupDurationMs">
/// Wall-clock milliseconds elapsed from the first <c>StartAsync</c> call to the
/// moment all health probes succeeded (or failure was determined).
/// </param>
/// <param name="FailureReason">
/// Human-readable description of why startup failed, or <see langword="null"/>
/// when <paramref name="IsHealthy"/> is <see langword="true"/>.
/// </param>
public sealed record ApplicationStartupResult(
    bool IsHealthy,
    Uri? BaseAddress,
    long StartupDurationMs,
    string? FailureReason);
