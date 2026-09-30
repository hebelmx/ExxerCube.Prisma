// <copyright file="EnvironmentProvisioningResult.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Provisioning;

/// <summary>
/// Carries the outcome of a completed environment provisioning pass.
/// Passed into <see cref="IEnvironmentProvisioner.TeardownAsync"/> so the provisioner
/// can clean up exactly what it created.
/// </summary>
/// <param name="SqlConnectionString">
/// The ADO.NET connection string for the provisioned SQL Server instance,
/// or <see langword="null"/> when SQL provisioning was skipped or failed.
/// </param>
/// <param name="OllamaEndpoint">
/// The base URL of the Ollama service (e.g. <c>http://localhost:11434</c>),
/// or <see langword="null"/> when Ollama was not started.
/// </param>
/// <param name="CorpusPath">
/// Absolute path to the directory containing the seeded or restored document corpus.
/// Always set; may point to the static Fixtures directory when corpus generation was skipped.
/// </param>
/// <param name="CorpusStatus">
/// Describes how the corpus was populated (generated, restored from fixtures, or absent).
/// </param>
/// <param name="DockerAvailable">
/// <see langword="true"/> when the Docker daemon was reachable at provisioning time.
/// </param>
/// <param name="CapabilityStatuses">
/// Per-capability availability report produced by the <see cref="IEnvironmentProvisioner"/> implementation.
/// </param>
public sealed record EnvironmentProvisioningResult(
    string? SqlConnectionString,
    string? OllamaEndpoint,
    string CorpusPath,
    CorpusStatus CorpusStatus,
    bool DockerAvailable,
    IReadOnlyList<CapabilityStatus> CapabilityStatuses);
