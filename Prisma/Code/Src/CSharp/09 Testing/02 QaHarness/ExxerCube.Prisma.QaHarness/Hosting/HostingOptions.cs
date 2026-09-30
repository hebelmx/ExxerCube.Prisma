// <copyright file="HostingOptions.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Hosting;

/// <summary>
/// Controls how the <see cref="IApplicationHostController"/> starts Prisma application processes.
/// </summary>
/// <param name="Mode">Which application processes to start (Web UI only, three-process pipeline, or all).</param>
/// <param name="SqlConnectionString">
/// The ADO.NET connection string injected into the hosted processes.
/// When <see langword="null"/>, each host uses its default configuration.
/// Typically sourced from <c>EnvironmentProvisioningResult.SqlConnectionString</c>.
/// </param>
/// <param name="SharedStoragePath">
/// Override for the shared document storage directory used by all processes.
/// When <see langword="null"/>, the host's default path is used.
/// </param>
/// <param name="SiaraStorageState">
/// Path to a Playwright browser storage-state JSON file containing a pre-authenticated
/// SIARA session.  Required by the ingestion workflow; <see langword="null"/> is safe for
/// non-ingestion scenarios.
/// </param>
/// <param name="DisableAutonomousWatchLoop">
/// When <see langword="true"/> (default), the <c>SiaraWatchLoop</c> autonomous polling is
/// disabled so that tests control ingest timing explicitly.
/// </param>
public sealed record HostingOptions(
    HostingMode Mode,
    string? SqlConnectionString = null,
    string? SharedStoragePath = null,
    string? SiaraStorageState = null,
    bool DisableAutonomousWatchLoop = true);
