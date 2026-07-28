using System;
using System.Reflection;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;

/// <summary>
/// Resolves the VEC engine's provenance version string stamped on every
/// <see cref="ExxerCube.Prisma.Veriqan.Domain.Entities.Finding"/> and <see cref="ExxerCube.Prisma.Veriqan.Domain.Entities.JobVerdict"/>
/// row (NFR-7, VERIQAN-E3-S4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not <see cref="Assembly.GetName()"/>'s <c>Version</c>:</b> MinVer (the repo's git-tag-driven
/// SemVer generator, O7) deliberately pins <c>AssemblyVersion</c> to <c>MAJOR.0.0.0</c> for binding
/// stability across patch/pre-release builds, so it can never discriminate between two builds of the
/// same major version — every build would stamp the same constant (historically <c>1.0.0.0</c>).
/// MinVer instead stamps the FULL computed SemVer plus build metadata (e.g.
/// <c>1.4.0-rc.1.547+4a933ce8</c>) onto <see cref="AssemblyInformationalVersionAttribute"/>, so that
/// attribute — not the assembly version — is the real per-build provenance source.
/// </para>
/// <para>
/// Resolution order: prefer the informational-version attribute; fall back to the 4-part assembly
/// version when the attribute is absent or blank (e.g. some test hosts / non-MinVer builds); fall
/// back to the literal <c>"unknown"</c> when neither is available.
/// </para>
/// </remarks>
internal static class EngineVersionResolver
{
    /// <summary>
    /// Default cap applied to the resolved version string, mirroring the <c>EngineVersion</c>
    /// column's <c>HasMaxLength(50)</c> constraint on both <c>JobVerdictConfiguration</c> and
    /// <c>FindingConfiguration</c> (02 Infrastructure/Veriqan.Infrastructure.Persistence).
    /// </summary>
    internal const int DefaultMaxLength = 50;

    /// <summary>
    /// Literal fallback returned when neither the informational-version attribute nor the
    /// assembly version yields a usable string.
    /// </summary>
    internal const string UnknownVersion = "unknown";

    /// <summary>
    /// Resolves and length-caps the provenance version string for <paramref name="assembly"/>.
    /// </summary>
    /// <param name="assembly">
    /// The assembly whose version provenance is being resolved (typically
    /// <c>typeof(VerificationPipeline).Assembly</c> — the deployed <c>Veriqan.Orchestration</c> build).
    /// </param>
    /// <param name="maxLength">
    /// Maximum length of the returned string. Defaults to <see cref="DefaultMaxLength"/> (50),
    /// matching the DB column caps. Callers must never widen this without also widening the
    /// corresponding EF Core column configuration and migration.
    /// </param>
    /// <returns>
    /// The informational version (preferred), the 4-part assembly version (fallback), or
    /// <see cref="UnknownVersion"/> (final fallback) — truncated to <paramref name="maxLength"/>
    /// characters when necessary.
    /// </returns>
    internal static string Resolve(Assembly assembly, int maxLength = DefaultMaxLength)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        var candidate = !string.IsNullOrWhiteSpace(informationalVersion)
            ? informationalVersion
            : assembly.GetName().Version?.ToString();

        if (string.IsNullOrWhiteSpace(candidate))
            return UnknownVersion;

        return candidate.Length > maxLength ? candidate[..maxLength] : candidate;
    }
}
