// <copyright file="ProvisioningOptions.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Provisioning;

/// <summary>
/// Configures the environment provisioning performed by <see cref="IEnvironmentProvisioner.ProvisionAsync"/>.
/// All properties have safe defaults suitable for a single-machine developer run.
/// </summary>
/// <param name="StartSqlContainer">
/// When <see langword="true"/> (default), the provisioner starts a Testcontainers SQL Server instance.
/// Set to <see langword="false"/> when an external SQL connection string is pre-supplied.
/// </param>
/// <param name="StartOllamaContainer">
/// When <see langword="true"/>, the provisioner starts a Testcontainers Ollama instance.
/// Defaults to <see langword="false"/> because Ollama is not required for the core MVP workflows.
/// </param>
/// <param name="SeedCorpus">
/// When <see langword="true"/> (default), the provisioner calls the corpus seeder to ensure
/// a document set is available in <paramref name="CorpusOutputPath"/>.
/// </param>
/// <param name="CorpusOutputPath">
/// Override for the directory where the seeded corpus is written.
/// When <see langword="null"/>, the provisioner uses the default SIARA simulator data directory.
/// </param>
/// <param name="CorpusDocumentCount">
/// Target number of documents for the Python generator to produce. Defaults to <c>10</c>.
/// Ignored when the generator is unavailable and the seeder falls back to static Fixtures.
/// </param>
/// <param name="RepoRoot">
/// Explicit absolute path to the repository root directory.  When non-<see langword="null"/>
/// this value takes highest precedence over the <c>PRISMA_REPO_ROOT</c> environment variable
/// and the automatic walk-up locator used by the provisioner.
/// </param>
public sealed record ProvisioningOptions(
    bool StartSqlContainer = true,
    bool StartOllamaContainer = false,
    bool SeedCorpus = true,
    string? CorpusOutputPath = null,
    int CorpusDocumentCount = 10,
    string? RepoRoot = null);
