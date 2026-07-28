using System.Reflection;
using System.Reflection.Emit;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// Tests for <see cref="EngineVersionResolver"/> — the provenance-version resolution helper
/// extracted from <see cref="VerificationPipeline"/> (O7: MinVer wiring). Covers the three
/// resolution branches (informational version preferred, assembly-version fallback, unknown
/// fallback) plus the DB column length-cap guard.
/// </summary>
public sealed class EngineVersionResolverTests
{
    /// <summary>
    /// The real <c>Veriqan.Orchestration</c> assembly is built with MinVer, so it carries an
    /// <see cref="AssemblyInformationalVersionAttribute"/>. <see cref="EngineVersionResolver.Resolve"/>
    /// must return that attribute's value verbatim (not the 4-part <see cref="AssemblyName.Version"/>,
    /// which MinVer pins to <c>MAJOR.0.0.0</c> and would therefore be indistinguishable across builds).
    /// </summary>
    [Fact]
    public void Resolve_InformationalVersionAttributePresent_PrefersInformationalVersion()
    {
        var assembly = typeof(VerificationPipeline).Assembly;
        var expected = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // Guard the test's own premise: if this ever fails, MinVer stopped stamping the assembly
        // (e.g. MinVerSkip, a design-time build, or the PackageReference was removed) and the
        // test below would otherwise pass vacuously.
        expected.ShouldNotBeNullOrWhiteSpace();

        var resolved = EngineVersionResolver.Resolve(assembly);

        resolved.ShouldBe(expected);
        resolved.ShouldNotBe(assembly.GetName().Version?.ToString(),
            "the informational version must win over the constant MAJOR.0.0.0 assembly version");
    }

    /// <summary>
    /// When an assembly carries no <see cref="AssemblyInformationalVersionAttribute"/>,
    /// <see cref="EngineVersionResolver.Resolve"/> must fall back to the 4-part assembly version.
    /// Uses a dynamically emitted assembly (no attributes at all) rather than a real repo assembly,
    /// so the absence of the attribute is guaranteed rather than incidental.
    /// </summary>
    [Fact]
    public void Resolve_NoInformationalVersionAttribute_FallsBackToAssemblyVersion()
    {
        var name = new AssemblyName("EngineVersionResolverTests.Fallback") { Version = new Version(2, 3, 0, 0) };
        var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);

        var resolved = EngineVersionResolver.Resolve(assembly);

        resolved.ShouldBe("2.3.0.0");
    }

    /// <summary>
    /// The <c>EngineVersion</c> DB column (<c>JobVerdictConfiguration</c> / <c>FindingConfiguration</c>,
    /// 02 Infrastructure/Veriqan.Infrastructure.Persistence) is capped at 50 characters. A resolved
    /// version string longer than the configured cap must be truncated, never passed through — an
    /// uncapped write would throw a SQL truncation error at persistence time.
    /// </summary>
    [Fact]
    public void Resolve_InformationalVersionExceedsMaxLength_TruncatesToMaxLength()
    {
        const string longInformationalVersion = "1.2.3-a-very-long-prerelease-identifier-that-exceeds-the-cap+deadbeef";
        var name = new AssemblyName("EngineVersionResolverTests.Truncation");
        var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);

        var attributeCtor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!;
        var attributeBuilder = new CustomAttributeBuilder(attributeCtor, [longInformationalVersion]);
        assembly.SetCustomAttribute(attributeBuilder);

        const int cap = 10;
        var resolved = EngineVersionResolver.Resolve(assembly, cap);

        resolved.Length.ShouldBe(cap);
        resolved.ShouldBe(longInformationalVersion[..cap]);
    }

    /// <summary>
    /// A null assembly is a programming error, not a runtime data condition — must throw
    /// <see cref="ArgumentNullException"/> immediately rather than deferring to a NullReferenceException.
    /// </summary>
    [Fact]
    public void Resolve_NullAssembly_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => EngineVersionResolver.Resolve(null!));
    }
}
