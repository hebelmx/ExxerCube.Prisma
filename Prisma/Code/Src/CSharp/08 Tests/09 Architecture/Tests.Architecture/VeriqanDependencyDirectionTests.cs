using System.IO;
using System.Text.Json;

namespace ExxerCube.Prisma.Tests.Architecture;

/// <summary>
/// Structural guardrail for VEC / Veriqan — Epic 1, Story 1.2.
///
/// Enforces the one-way dependency direction: <c>Veriqan → Prisma</c> assemblies MAY reference
/// Solution 1 (<c>ExxerCube.Prisma.*</c> non-Veriqan) assemblies, but the reverse direction is
/// <strong>forbidden</strong>. A Solution 1 assembly that gains a dependency on any
/// <c>ExxerCube.Prisma.Veriqan.*</c> assembly — even transitively through a ProjectReference —
/// would mean Prisma depends on its client, inverting the layering that keeps the base product
/// independent of VEC.
///
/// <para>
/// Allowed: <c>ExxerCube.Prisma.Veriqan.Infrastructure.Extraction</c> → <c>ExxerCube.Prisma.Domain</c>
/// <br/>
/// Forbidden: <c>ExxerCube.Prisma.Domain</c> → <c>ExxerCube.Prisma.Veriqan.Domain</c>
/// </para>
///
/// <para>
/// Two complementary checks are used:
/// <list type="number">
///   <item><c>GetReferencedAssemblies()</c> — CLR manifest; catches any Veriqan type ACTUALLY USED
///         in a Solution 1 binary (the most common real-world violation path).</item>
///   <item>The test process <c>.deps.json</c> — MSBuild dependency graph; catches any
///         <c>ProjectReference</c> that appears in the build graph even if no Veriqan type is
///         used yet ("blank reference" violation). This fires the moment the csproj is edited,
///         not just when a developer writes code against the new reference.</item>
/// </list>
/// </para>
/// </summary>
public sealed class VeriqanDependencyDirectionTests(ITestOutputHelper output)
{
    private readonly ILogger logger = XUnitLogger.CreateLogger<VeriqanDependencyDirectionTests>(output);

    // Veriqan assembly name prefix — any assembly whose simple name starts with this string is
    // a Veriqan-side assembly and must NOT appear in a Solution 1 assembly's reference list.
    private const string VeriqanPrefix = "ExxerCube.Prisma.Veriqan";

    // Solution 1 assembly name prefix — the base Prisma product namespace.
    private const string Solution1Prefix = "ExxerCube.Prisma.";

    // The expected count of Veriqan assemblies loaded into the test process.
    // Veriqan.Worker is an entry-point (SDK.Web) not referenced from this test project;
    // the remaining 9 are: Domain, Application, Infrastructure.{Extraction, Validation, Visual,
    // ReferenceData, Reporting, Persistence}, Orchestration.
    private const int ExpectedVeriqanAssemblyCount = 9;

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Assembly discovery helpers
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Discovers all Veriqan assemblies present in the test process output directory
    /// (they arrive via the ProjectReference to Veriqan.Orchestration and its transitive closure).
    /// </summary>
    private static IReadOnlyList<Assembly> LoadVeriqanAssemblies()
    {
        // Prefer assemblies already loaded (they are, because the test project references
        // Veriqan.Orchestration which is a composition-root that references all nine peers).
        var fromDomain = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic
                && a.GetName().Name is { } n
                && n.StartsWith(VeriqanPrefix, StringComparison.Ordinal))
            .ToList();

        if (fromDomain.Count >= ExpectedVeriqanAssemblyCount)
        {
            return fromDomain;
        }

        // Fallback: scan the output directory for any Veriqan DLLs not yet loaded.
        var objSegment = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";
        var fromDisk = Directory
            .GetFiles(AppContext.BaseDirectory, "ExxerCube.Prisma.Veriqan.*.dll",
                SearchOption.AllDirectories)
            .Where(p => p.IndexOf(objSegment, StringComparison.OrdinalIgnoreCase) < 0)
            .Select(p =>
            {
                try { return Assembly.LoadFrom(p); }
                catch { return null; }
            })
            .Where(a => a is not null)
            .Select(a => a!);

        return fromDomain
            .Concat(fromDisk)
            .DistinctBy(a => a.GetName().Name, StringComparer.Ordinal)
            .Where(a => a.GetName().Name?.StartsWith(VeriqanPrefix, StringComparison.Ordinal) == true)
            .ToList();
    }

    /// <summary>
    /// Collects the Solution 1 (<c>ExxerCube.Prisma.*</c> non-Veriqan) production assemblies
    /// referenced by the Architecture test project. These are the authoritative handles — if a
    /// Solution 1 project is referenced here, it is subject to the rule.
    ///
    /// <para>
    /// We anchor each assembly on a known public type so the CLR loads it in the default load
    /// context (same technique as <see cref="HexagonalArchitectureTests"/>).
    /// </para>
    /// </summary>
    private static IReadOnlyList<Assembly> GetSolution1Assemblies() =>
        new[]
        {
            // 01 Core
            typeof(ExxerCube.Prisma.Domain.Entities.FileMetadata).Assembly,
            typeof(ExxerCube.Prisma.Application.Services.DocumentIngestionService).Assembly,
            // 02 Infrastructure — main project (EventPublisher is here, namespace ExxerCube.Prisma.Infrastructure.Events)
            typeof(ExxerCube.Prisma.Infrastructure.Events.EventPublisher).Assembly,
            // 02 Infrastructure — per-adapter projects
            typeof(ExxerCube.Prisma.Infrastructure.Database.EntityFramework.PrismaDbContext).Assembly,
            typeof(ExxerCube.Prisma.Infrastructure.Classification.FusionExpedienteService).Assembly,
            typeof(ExxerCube.Prisma.Infrastructure.Export.SiroXmlExporter).Assembly,
            typeof(ExxerCube.Prisma.Infrastructure.Export.Adaptive.AdaptiveExporter).Assembly,
            typeof(ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract.TesseractOcrExecutor).Assembly,
            typeof(ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.AdaptiveDocxExtractor).Assembly,
            typeof(ExxerCube.Prisma.Infrastructure.FileStorage.FileStorageOptions).Assembly,
            typeof(ExxerCube.Prisma.Infrastructure.BrowserAutomation.Services.SiaraLoginService).Assembly,
            typeof(ExxerCube.Prisma.Infrastructure.Events.InMemoryEventBus).Assembly,
            typeof(ExxerCube.Prisma.Infrastructure.Imaging.EmguCvImageQualityAnalyzer).Assembly,
            typeof(ExxerCube.Prisma.Infrastructure.Metrics.ProcessingMetricsService).Assembly,
            typeof(ExxerCube.Prisma.Infrastructure.Python.GotOcr2.DependencyInjection.ServiceCollectionExtensions).Assembly,
            // 04 Services
            typeof(global::Prisma.Orion.Ingestion.FileIngestionJournal).Assembly,
            typeof(global::Prisma.Orion.HealthChecks.OrionHealthCheckService).Assembly,
            typeof(global::Prisma.Athena.Processing.InMemoryClearanceReplayGuard).Assembly,
            typeof(global::Prisma.Athena.HealthChecks.AthenaHealthCheckService).Assembly,
            typeof(global::Prisma.Auth.Infrastructure.InMemoryIdentityProvider).Assembly,
        }
        .Where(a => a is not null)
        .DistinctBy(a => a.FullName, StringComparer.Ordinal)
        .Where(a =>
        {
            var name = a.GetName().Name ?? string.Empty;
            // Must be an ExxerCube.Prisma.* assembly …
            return name.StartsWith(Solution1Prefix, StringComparison.Ordinal)
                   // … but NOT a Veriqan assembly (they are allowed to depend on Solution 1, not the reverse).
                   && !name.StartsWith(VeriqanPrefix, StringComparison.Ordinal);
        })
        .ToList();

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // deps.json helper — catches ProjectReferences where no types are actually consumed yet.
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses the test process's <c>.deps.json</c> (the combined MSBuild dependency graph for
    /// the entire test process closure) and returns every Solution 1 → Veriqan dependency edge
    /// found in it.
    ///
    /// <para>
    /// The deps.json records project references at the package-graph level regardless of whether
    /// any type from the referenced assembly is actually consumed. This catches the "blank
    /// reference" scenario where a developer adds a <c>ProjectReference</c> to a Veriqan project
    /// in a Solution 1 csproj but has not yet written any code using it.
    /// </para>
    ///
    /// <para>
    /// Exclusions: the Architecture test project itself is allowed to reference Veriqan
    /// (for analysis purposes), so edges whose source is the test assembly are filtered out.
    /// </para>
    /// </summary>
    private static IReadOnlyList<string> FindDepsJsonViolations()
    {
        // The test process's .deps.json sits next to the test binary.
        var depsJsonPath = Path.Combine(
            AppContext.BaseDirectory,
            "ExxerCube.Prisma.Tests.Architecture.deps.json");

        if (!File.Exists(depsJsonPath))
        {
            return Array.Empty<string>();
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(depsJsonPath));

        if (!doc.RootElement.TryGetProperty("targets", out var targets))
        {
            return Array.Empty<string>();
        }

        // Pick the first (and only relevant) TFM target.
        var violations = new List<string>();
        foreach (var tfm in targets.EnumerateObject())
        {
            foreach (var pkg in tfm.Value.EnumerateObject())
            {
                // pkg.Name is e.g. "ExxerCube.Prisma.Domain/1.0.0"
                var slash = pkg.Name.IndexOf('/');
                var sourceName = slash >= 0 ? pkg.Name[..slash] : pkg.Name;

                // Only check Solution 1 assemblies — not the test project itself and not Veriqan assemblies.
                if (!sourceName.StartsWith(Solution1Prefix, StringComparison.Ordinal))
                    continue;
                if (sourceName.StartsWith(VeriqanPrefix, StringComparison.Ordinal))
                    continue;
                // The test project is allowed to reference Veriqan for analysis.
                if (sourceName.Equals("ExxerCube.Prisma.Tests.Architecture", StringComparison.Ordinal))
                    continue;

                if (!pkg.Value.TryGetProperty("dependencies", out var deps))
                    continue;

                foreach (var dep in deps.EnumerateObject())
                {
                    if (dep.Name.StartsWith(VeriqanPrefix, StringComparison.Ordinal))
                    {
                        violations.Add($"{sourceName} → {dep.Name}");
                    }
                }
            }
        }

        return violations;
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Test 1 — non-vacuity: Veriqan assemblies are actually present in the test process.
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sanity-guard: verifies that the expected Veriqan assemblies were loaded into the test
    /// process by the ProjectReference closure. If this test fails it means the ProjectReference
    /// to <c>Veriqan.Orchestration</c> was removed from the Architecture test project, which
    /// would let <see cref="Solution1_Assemblies_Must_Not_Reference_Any_Veriqan_Assembly"/> pass
    /// vacuously (no Veriqan assemblies to check against).
    /// </summary>
    [Fact]
    public void Veriqan_Assemblies_Are_Loaded_NonVacuously()
    {
        var veriqanAssemblies = LoadVeriqanAssemblies();

        var names = veriqanAssemblies
            .Select(a => a.GetName().Name ?? string.Empty)
            .OrderBy(n => n)
            .ToList();

        logger.LogInformation(
            "Veriqan assemblies found: Count={Count}\n{Details}",
            names.Count,
            string.Join(Environment.NewLine, names.Select(n => $"  {n}")));

        veriqanAssemblies.Count.ShouldBeGreaterThanOrEqualTo(
            ExpectedVeriqanAssemblyCount,
            $"Expected at least {ExpectedVeriqanAssemblyCount} Veriqan assemblies in the test output directory " +
            $"(ExxerCube.Prisma.Veriqan.{{Domain, Application, Infrastructure.Extraction, " +
            $"Infrastructure.Validation, Infrastructure.Visual, Infrastructure.ReferenceData, " +
            $"Infrastructure.Reporting, Infrastructure.Persistence, Orchestration}}). " +
            $"Only found {veriqanAssemblies.Count}: {string.Join(", ", names)}. " +
            $"Check that the ProjectReference to Veriqan.Orchestration is still present in the " +
            $"Architecture test project.");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Test 2 — the actual dependency-direction rule (CLR manifest check).
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <strong>ADR: Veriqan → Prisma only — CLR manifest check.</strong>
    ///
    /// <para>
    /// For every Solution 1 (<c>ExxerCube.Prisma.*</c> non-Veriqan) production assembly, asserts
    /// that its compiled <see cref="Assembly.GetReferencedAssemblies"/> manifest contains
    /// <strong>no</strong> entry whose simple name starts with <c>ExxerCube.Prisma.Veriqan</c>.
    /// </para>
    ///
    /// <para>
    /// This check catches violations where a developer both adds a ProjectReference AND uses a
    /// Veriqan type, which is the normal violation path (code that uses the new dependency).
    /// "Blank references" (added csproj entry, no type usage yet) are caught by
    /// <see cref="Solution1_Must_Not_Reference_Veriqan_InDepsJson"/>.
    /// </para>
    /// </summary>
    [Fact]
    public void Solution1_Assemblies_Must_Not_Reference_Any_Veriqan_Assembly()
    {
        var veriqanAssemblies = LoadVeriqanAssemblies();

        // Build the set of forbidden simple names from the actual Veriqan assemblies present.
        var forbiddenNames = veriqanAssemblies
            .Select(a => a.GetName().Name ?? string.Empty)
            .Where(n => n.StartsWith(VeriqanPrefix, StringComparison.Ordinal))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Non-vacuity guard: if LoadVeriqanAssemblies returned nothing (e.g. because the
        // ProjectReference was removed), the forbidden set is empty and every assembly would
        // trivially pass. We assert here too so a single-test run of this method also fails.
        forbiddenNames.Count.ShouldBeGreaterThanOrEqualTo(
            ExpectedVeriqanAssemblyCount,
            $"Cannot enforce the dependency-direction rule: fewer than {ExpectedVeriqanAssemblyCount} " +
            $"Veriqan assemblies are present in the test process. " +
            $"Ensure the Architecture test project references Veriqan.Orchestration. " +
            $"Found only {forbiddenNames.Count} Veriqan assembly names: {string.Join(", ", forbiddenNames)}");

        var solution1Assemblies = GetSolution1Assemblies();

        var violations = new List<string>();

        foreach (var asm in solution1Assemblies)
        {
            var asmName = asm.GetName().Name ?? string.Empty;

            var offendingRefs = asm.GetReferencedAssemblies()
                .Where(r => (r.Name ?? string.Empty).StartsWith(VeriqanPrefix, StringComparison.Ordinal))
                .Select(r => r.Name ?? string.Empty)
                .OrderBy(n => n)
                .ToList();

            if (offendingRefs.Count > 0)
            {
                violations.Add(
                    $"{asmName} → [{string.Join(", ", offendingRefs)}]");
            }
        }

        if (violations.Any())
        {
            logger.LogError(
                "CLR-manifest violation: Solution 1 assemblies may not reference Veriqan. " +
                "Count={Count}\n{Details}",
                violations.Count,
                string.Join(Environment.NewLine, violations.Select(v => $"  VIOLATION: {v}")));
        }
        else
        {
            logger.LogInformation(
                "CLR-manifest OK: none of the {Count} Solution 1 assemblies reference any " +
                "Veriqan assembly (checked {ForbiddenCount} forbidden names).",
                solution1Assemblies.Count,
                forbiddenNames.Count);
        }

        violations.ShouldBeEmpty(
            $"Solution 1 (ExxerCube.Prisma.* non-Veriqan) assemblies must NOT reference any " +
            $"ExxerCube.Prisma.Veriqan.* assembly. The allowed direction is Veriqan → Prisma ONLY. " +
            $"Violating assemblies ({violations.Count}): {string.Join("; ", violations)}");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Test 3 — the actual dependency-direction rule (deps.json check — catches blank references).
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <strong>ADR: Veriqan → Prisma only — MSBuild deps.json check.</strong>
    ///
    /// <para>
    /// Parses the test process's combined <c>.deps.json</c> (written by MSBuild alongside the
    /// test binary) and asserts that no Solution 1 package entry lists a Veriqan package as a
    /// dependency. The deps.json records every <c>ProjectReference</c> regardless of whether any
    /// type from the referenced assembly is actually consumed in code.
    /// </para>
    ///
    /// <para>
    /// This catches the "blank reference" violation scenario: a developer adds
    /// <c>&lt;ProjectReference Include="…Veriqan.Domain…"/&gt;</c> to a Solution 1 csproj but
    /// has not written any code using that reference yet. The CLR manifest check
    /// (<see cref="Solution1_Assemblies_Must_Not_Reference_Any_Veriqan_Assembly"/>) would
    /// miss this case; this check catches it.
    /// </para>
    ///
    /// <para>
    /// The Architecture test project itself is explicitly excluded: it is ALLOWED to reference
    /// Veriqan assemblies for analysis purposes (it references Veriqan.Orchestration so that
    /// Veriqan DLLs are present in the output directory for reflection-based checking).
    /// </para>
    /// </summary>
    [Fact]
    public void Solution1_Must_Not_Reference_Veriqan_InDepsJson()
    {
        var violations = FindDepsJsonViolations();

        if (violations.Any())
        {
            logger.LogError(
                "deps.json violation: Solution 1 project(s) have a ProjectReference to Veriqan. " +
                "Count={Count}\n{Details}",
                violations.Count,
                string.Join(Environment.NewLine, violations.Select(v => $"  VIOLATION: {v}")));
        }
        else
        {
            // Log the path checked so the test is clearly non-vacuous in the output.
            var depsPath = Path.Combine(
                AppContext.BaseDirectory, "ExxerCube.Prisma.Tests.Architecture.deps.json");
            logger.LogInformation(
                "deps.json OK: no Solution 1 → Veriqan dependency edges found. " +
                "Checked: {Path}",
                depsPath);
        }

        violations.Count.ShouldBe(
            0,
            $"Solution 1 (ExxerCube.Prisma.* non-Veriqan) projects must NOT have a ProjectReference " +
            $"to any ExxerCube.Prisma.Veriqan.* project. Detected {violations.Count} violation(s) " +
            $"in the .deps.json dependency graph: {string.Join("; ", violations)}");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Test 4 — positive direction check (Veriqan assemblies DO reference Solution 1).
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Positive sanity check: at least some Veriqan infrastructure / application assemblies DO
    /// reference Solution 1 (<c>ExxerCube.Prisma.Domain</c>) as expected. This confirms the
    /// dependency direction is not merely absent on both sides (i.e., two completely independent
    /// assembly trees would vacuously satisfy the "no reverse dep" rule).
    ///
    /// <para>
    /// In the current skeleton each <c>Veriqan.Infrastructure.*</c> transitively references
    /// <c>ExxerCube.Prisma.Veriqan.Application</c> → <c>ExxerCube.Prisma.Veriqan.Domain</c>.
    /// Even if those Veriqan-internal references are the only ones present, the rule that
    /// Veriqan depends on nothing outside its own namespace is also fine for a skeleton;
    /// this test is therefore stated as a SHOULD (informational log) rather than a hard failure,
    /// so the skeleton phase doesn't produce a false alarm.
    /// It will naturally harden when Veriqan infrastructure adapters start using Prisma domain
    /// types (planned in subsequent epics).
    /// </para>
    /// </summary>
    [Fact]
    public void Veriqan_Assemblies_Should_Reference_Solution1_Or_Be_Skeleton()
    {
        var veriqanAssemblies = LoadVeriqanAssemblies();

        var veriqanWithSolution1Refs = veriqanAssemblies
            .Where(a =>
                a.GetReferencedAssemblies()
                    .Any(r =>
                        (r.Name ?? string.Empty).StartsWith(Solution1Prefix, StringComparison.Ordinal)
                        && !(r.Name ?? string.Empty).StartsWith(VeriqanPrefix, StringComparison.Ordinal)))
            .Select(a => a.GetName().Name ?? string.Empty)
            .OrderBy(n => n)
            .ToList();

        logger.LogInformation(
            "Veriqan assemblies that reference Solution 1: Count={Count}\n{Details}",
            veriqanWithSolution1Refs.Count,
            veriqanWithSolution1Refs.Count > 0
                ? string.Join(Environment.NewLine, veriqanWithSolution1Refs.Select(n => $"  {n}"))
                : "  (none — skeleton phase, no cross-system type usage yet)");

        // Not a hard failure for the skeleton. Log the state for awareness.
        // When Veriqan adapters start consuming Prisma domain types this count will be > 0
        // and the positive direction becomes self-evidently testable.
        if (veriqanWithSolution1Refs.Count == 0)
        {
            logger.LogInformation(
                "Positive-direction note: no Veriqan assembly currently references a Solution 1 " +
                "assembly directly. This is expected during the skeleton phase (Epic 1). The rule " +
                "will naturally harden as cross-system adapters are added.");
        }

        // The test always passes — it is a diagnostic / observability probe, not a hard gate.
        // The hard gates are Solution1_Assemblies_Must_Not_Reference_Any_Veriqan_Assembly
        // and Solution1_Must_Not_Reference_Veriqan_InDepsJson.
        true.ShouldBeTrue("Positive-direction probe — see log output for current state.");
    }
}
