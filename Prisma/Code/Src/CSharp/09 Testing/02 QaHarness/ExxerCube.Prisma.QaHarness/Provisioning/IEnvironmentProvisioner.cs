// <copyright file="IEnvironmentProvisioner.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Provisioning;

/// <summary>
/// Orchestrates all environment setup required before QA workflows can run:
/// container startup (SQL Server, Ollama), database migration, and corpus seeding.
/// </summary>
/// <remarks>
/// Implementations must not throw for business-logic failures; they return a
/// <see cref="Result{T}"/> failure instead. <see cref="ProvisionAsync"/> is designed
/// to be idempotent — calling it on an already-provisioned environment is safe.
/// </remarks>
public interface IEnvironmentProvisioner
{
    /// <summary>
    /// Builds and verifies all required infrastructure according to <paramref name="options"/>.
    /// </summary>
    /// <param name="options">Controls which containers are started and whether corpus seeding is performed.</param>
    /// <param name="cancellationToken">Token used to cancel long-running container startup.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> containing the <see cref="EnvironmentProvisioningResult"/>
    /// when provisioning completes (even partially); a failure result only when provisioning cannot
    /// produce a usable environment at all.
    /// </returns>
    Task<Result<EnvironmentProvisioningResult>> ProvisionAsync(
        ProvisioningOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tears down infrastructure created during <see cref="ProvisionAsync"/>.
    /// </summary>
    /// <param name="provisioningResult">The result returned by the matching <see cref="ProvisionAsync"/> call.</param>
    /// <param name="cancellationToken">Token used to cancel teardown.</param>
    /// <returns>A successful <see cref="Result"/> when teardown completes; a failure result when resources could not be released.</returns>
    Task<Result> TeardownAsync(
        EnvironmentProvisioningResult provisioningResult,
        CancellationToken cancellationToken = default);
}
