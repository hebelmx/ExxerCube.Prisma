// <copyright file="CapabilityStatus.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Provisioning;

/// <summary>
/// Reports whether a named infrastructure capability is available after provisioning.
/// </summary>
/// <param name="Capability">The capability identifier (e.g. <c>"Docker"</c>, <c>"Playwright"</c>, <c>"SiaraSimulator"</c>).</param>
/// <param name="Available">
/// <see langword="true"/> when the capability is fully operational;
/// <see langword="false"/> when the harness detected it is absent or unhealthy.
/// </param>
/// <param name="Detail">Optional human-readable detail describing why the capability is unavailable, or <see langword="null"/> when available.</param>
public sealed record CapabilityStatus(
    string Capability,
    bool Available,
    string? Detail);
