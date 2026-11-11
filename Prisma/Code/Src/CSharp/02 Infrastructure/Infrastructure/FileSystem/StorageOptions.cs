namespace ExxerCube.Prisma.Infrastructure.FileSystem;

/// <summary>
/// Deployment-time configuration for the shared document storage volume (MVP-PATH 1.3, ADR-011). Bind from
/// the <c>Storage</c> configuration section. Each process in the 3-process split configures its own
/// <see cref="BasePath"/> pointing at the same shared volume — the absolute mount path may differ per
/// process, which is exactly why the cross-process event carries only a storage-relative path.
/// </summary>
public sealed class StorageOptions
{
    /// <summary>The configuration section these options bind to.</summary>
    public const string SectionName = "Storage";

    /// <summary>
    /// Gets or sets the absolute base path of the shared storage volume as mounted in this process (for
    /// example <c>/mnt/storage</c> or <c>D:\shared\storage</c>). When blank, path resolution fails closed
    /// (the resolver returns a failure the caller logs and continues on) — so a misconfigured or
    /// single-service deployment never crashes.
    /// </summary>
    public string BasePath { get; set; } = string.Empty;
}
