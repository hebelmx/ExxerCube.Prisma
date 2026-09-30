// <copyright file="HostingMode.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Hosting;

/// <summary>
/// Selects which Prisma application processes the <see cref="IApplicationHostController"/> starts.
/// </summary>
public enum HostingMode
{
    /// <summary>Start only the Blazor Web UI (wraps <c>PrismaWebApplicationFactory</c>).</summary>
    WebUiOnly,

    /// <summary>
    /// Start the three-process pipeline: Orion Worker, Athena Worker, and the
    /// Reconciliator Worker.  Suitable for ingestion and end-to-end pipeline tests.
    /// </summary>
    ThreeProcessPipeline,

    /// <summary>Start the Web UI and the three-process pipeline together.</summary>
    All,
}
