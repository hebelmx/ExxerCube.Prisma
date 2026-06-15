namespace Prisma.Orion.Ingestion;

/// <summary>
/// Configuration for the expected-case manifest (Listado) used for per-cycle reconciliation
/// (Item B #8 + F #12). Bind from the <c>ExpectedManifest</c> configuration section.
/// </summary>
/// <remarks>
/// When <see cref="Enabled"/> is <see langword="false"/> (the default), the watch loop behaves
/// exactly as before — no manifest is loaded, no reconciliation runs, and no report is produced.
/// Set <see cref="Enabled"/> to <see langword="true"/> and supply a valid <see cref="ManifestPath"/>
/// to activate the reconciliation feature.
/// </remarks>
public sealed class ExpectedManifestOptions
{
    /// <summary>The configuration section these options bind to.</summary>
    public const string SectionName = "ExpectedManifest";

    /// <summary>
    /// Gets or sets a value indicating whether per-cycle manifest reconciliation is enabled.
    /// Defaults to <see langword="false"/> so all existing behavior is unchanged until explicitly
    /// opted in.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the absolute (or current-directory-relative) path of the expected manifest
    /// JSON file. When blank or <see langword="null"/>, the provider returns a graceful failure
    /// (cycle continues with a warning — never crashes).
    /// </summary>
    public string? ManifestPath { get; set; }
}
