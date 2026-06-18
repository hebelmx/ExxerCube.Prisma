using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Tolerances;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Visual.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// Enforcement / registry test for Story 9.2 (NFR-7 auditability).
/// Resolves ALL registered <see cref="IVecValidationRule"/> instances (from both
/// <c>Veriqan.Infrastructure.Validation</c> and <c>Veriqan.Infrastructure.Visual</c>)
/// via the Scrutor DI registration and asserts that every rule has a non-empty
/// <see cref="IVecValidationRule.DofNumeral"/>.
/// </summary>
/// <remarks>
/// <para>
/// This test is the enforcement gate referenced in the Story 9.2 acceptance criteria:
/// "Any rule missing a non-empty DOF numeral citation fails the build/test."
/// Adding a new rule without a <c>DofNumeral</c> will cause this test to fail before
/// the feature can be merged.
/// </para>
/// <para>
/// This test lives in <c>Veriqan.Orchestration.Tests</c> because it is the only test
/// project that references both <c>Veriqan.Infrastructure.Validation</c> and
/// <c>Veriqan.Infrastructure.Visual</c>, enabling discovery of all 35 rules in a
/// single service container.
/// </para>
/// </remarks>
public sealed class DofNumeralRegistryTests
{
    // -----------------------------------------------------------------------
    // Test 1: Enforcement — every rule from both assemblies has a non-empty numeral
    // -----------------------------------------------------------------------

    /// <summary>
    /// ALL registered <see cref="IVecValidationRule"/> implementations (from both
    /// <c>Veriqan.Infrastructure.Validation</c> and <c>Veriqan.Infrastructure.Visual</c>)
    /// must declare a non-empty <see cref="IVecValidationRule.DofNumeral"/>.
    /// </summary>
    /// <remarks>
    /// This is the architecture/registry enforcement test required by Story 9.2 AC-1.
    /// A rule that returns <c>null</c>, empty, or whitespace-only from <c>DofNumeral</c>
    /// will fail this assertion, causing the test suite to fail before merge.
    /// </remarks>
    [Fact]
    public void AllRegisteredRules_HaveNonEmptyDofNumeral()
    {
        // Arrange — build a container with BOTH assemblies' rules registered
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddVeriqanValidation();   // Scrutor scan: Veriqan.Infrastructure.Validation
        services.AddVeriqanVisual();       // Scrutor scan: Veriqan.Infrastructure.Visual

        using var sp = services.BuildServiceProvider();

        var rules = sp.GetServices<IVecValidationRule>().ToList();

        // Sanity: we must discover all 35 rules (28 validation + 7 visual)
        rules.Count.ShouldBeGreaterThanOrEqualTo(35,
            $"Expected at least 35 registered rules (28 Validation + 7 Visual). " +
            $"Found {rules.Count}. A rule may be missing from DI registration or the Scrutor scan path changed.");

        // Enforcement: every rule must have a non-empty DOF numeral
        var violations = rules
            .Where(r => string.IsNullOrWhiteSpace(r.DofNumeral))
            .Select(r => r.CheckId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        violations.ShouldBeEmpty(
            $"The following rule(s) are missing a non-empty DofNumeral (Story 9.2 / NFR-7): " +
            string.Join(", ", violations) + ". " +
            "Add 'public string DofNumeral => \"Acuerdo §N\";' to each rule class.");
    }

    // -----------------------------------------------------------------------
    // Test 2: Coverage map via engine — all 35 entries queryable and non-empty
    // -----------------------------------------------------------------------

    /// <summary>
    /// The coverage map returned by <see cref="IVecValidationEngine.GetCoverageMap"/>
    /// (with both assemblies registered) covers all rules and has no empty numerals.
    /// This verifies the queryable surface consumed later by E13 reporting.
    /// </summary>
    [Fact]
    public void GetCoverageMap_AllRulesFromBothAssemblies_NonEmptyNumerals()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddVeriqanValidation();
        services.AddVeriqanVisual();

        using var sp = services.BuildServiceProvider();
        var engine = sp.GetRequiredService<IVecValidationEngine>();

        // Act
        var map = engine.GetCoverageMap();

        // Assert: correct count
        map.Count.ShouldBeGreaterThanOrEqualTo(35,
            $"Coverage map must contain at least 35 entries. Found {map.Count}.");

        // Assert: no empty numerals
        var emptyEntries = map
            .Where(e => string.IsNullOrWhiteSpace(e.DofNumeral))
            .Select(e => e.CheckId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        emptyEntries.ShouldBeEmpty(
            $"Coverage map contains entries with empty DofNumeral: " +
            string.Join(", ", emptyEntries));

        // Assert: no duplicate CheckIds in the map
        var duplicates = map
            .GroupBy(e => e.CheckId, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        duplicates.ShouldBeEmpty(
            $"Coverage map has duplicate CheckId entries: " +
            string.Join(", ", duplicates));

        // Assert: sorted by CheckId (matches RunAsync determinism contract — NFR-5)
        var checkIds = map.Select(e => e.CheckId).ToList();
        var sorted = checkIds.OrderBy(id => id, StringComparer.Ordinal).ToList();
        checkIds.ShouldBe(sorted, "Coverage map must be sorted by CheckId (NFR-5 determinism)");
    }

    // -----------------------------------------------------------------------
    // Test 3: Spot-checks for specific mappings from the gap document
    // -----------------------------------------------------------------------

    /// <summary>
    /// Spot-checks that a representative sample of rules carry the expected DOF
    /// section citation as derived from the LAW-VS-CHECKLIST-GAP-2026-06-17.md mapping.
    /// If a numeral is changed without updating the gap doc, this test catches it.
    /// </summary>
    [Fact]
    public void GetCoverageMap_SpotChecks_MatchLawVsChecklistGapMapping()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddVeriqanValidation();
        services.AddVeriqanVisual();

        using var sp = services.BuildServiceProvider();
        var engine = sp.GetRequiredService<IVecValidationEngine>();

        var map = engine.GetCoverageMap()
            .ToDictionary(e => e.CheckId, e => e.DofNumeral, StringComparer.Ordinal);

        // §1 — logo (CL-33)
        map["CL-33"].ShouldBe("Acuerdo §1");

        // §2 — pagination (CL-31)
        map["CL-31"].ShouldBe("Acuerdo §2");

        // §4 — QR/CFDI fiscal block (CL-50..53)
        map["CL-50"].ShouldBe("Acuerdo §4");
        map["CL-51"].ShouldBe("Acuerdo §4");
        map["CL-52"].ShouldBe("Acuerdo §4");
        map["CL-53"].ShouldBe("Acuerdo §4");

        // §7 — resumen de cargos y abonos (CL-17..21)
        map["CL-17"].ShouldBe("Acuerdo §7");
        map["CL-18"].ShouldBe("Acuerdo §7");
        map["CL-19"].ShouldBe("Acuerdo §7");
        map["CL-20"].ShouldBe("Acuerdo §7");
        map["CL-21"].ShouldBe("Acuerdo §7");

        // §9 — CAT (CL-10)
        map["CL-10"].ShouldBe("Acuerdo §9");

        // §11 — compara tu tarjeta (CL-32)
        map["CL-32"].ShouldBe("Acuerdo §11");

        // §13 — nivel de uso (CL-22..26, CL-40, CL-41)
        map["CL-22"].ShouldBe("Acuerdo §13");
        map["CL-23"].ShouldBe("Acuerdo §13");
        map["CL-24"].ShouldBe("Acuerdo §13");
        map["CL-25"].ShouldBe("Acuerdo §13");
        map["CL-26"].ShouldBe("Acuerdo §13");
        map["CL-40"].ShouldBe("Acuerdo §13");
        map["CL-41"].ShouldBe("Acuerdo §13");

        // §15 — número de cuenta pág 2+ (CL-34)
        map["CL-34"].ShouldBe("Acuerdo §15");

        // §18 — programas de beneficios (CL-36, 37, 39, 49)
        map["CL-36"].ShouldBe("Acuerdo §18");
        map["CL-37"].ShouldBe("Acuerdo §18");
        map["CL-39"].ShouldBe("Acuerdo §18");
        map["CL-49"].ShouldBe("Acuerdo §18");

        // §22 — desglose de movimientos (CL-42..45, ITEM-58)
        map["CL-42"].ShouldBe("Acuerdo §22");
        map["CL-43"].ShouldBe("Acuerdo §22");
        map["CL-44"].ShouldBe("Acuerdo §22");
        map["CL-45"].ShouldBe("Acuerdo §22");
        map["ITEM-58"].ShouldBe("Acuerdo §22");

        // Annexe typography rules (CL-28, CL-35)
        map["CL-28"].ShouldBe("Acuerdo Anexo — Tipografía");
        map["CL-35"].ShouldBe("Acuerdo Anexo — Tipografía");
    }

    // -----------------------------------------------------------------------
    // Test 4 (Story 9.3a): Every rule exposes a defined Classification value
    // -----------------------------------------------------------------------

    /// <summary>
    /// ALL registered <see cref="IVecValidationRule"/> implementations must expose a
    /// <see cref="RuleClassification"/> value that is defined in the enum (not an out-of-range
    /// integer). This is the enforcement gate for Story 9.3a AC-1.
    /// </summary>
    [Fact]
    public void AllRegisteredRules_HaveDefinedClassification()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddVeriqanValidation();
        services.AddVeriqanVisual();

        using var sp = services.BuildServiceProvider();
        var rules = sp.GetServices<IVecValidationRule>().ToList();

        rules.Count.ShouldBeGreaterThanOrEqualTo(35,
            $"Expected at least 35 registered rules. Found {rules.Count}.");

        var violations = rules
            .Where(r => !Enum.IsDefined(typeof(RuleClassification), r.Classification))
            .Select(r => r.CheckId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        violations.ShouldBeEmpty(
            $"The following rule(s) have an undefined Classification value: " +
            string.Join(", ", violations));
    }

    // -----------------------------------------------------------------------
    // Test 5 (Story 9.3a): Tolerance-bearing rules must NOT be BaselineLocked
    // -----------------------------------------------------------------------

    /// <summary>
    /// Any rule that has a registered legal tolerance (i.e. <see cref="ILegalToleranceProvider.Has"/>
    /// returns <see langword="true"/>) must NOT be classified as
    /// <see cref="RuleClassification.BaselineLocked"/>. BaselineLocked rules carry no tunable
    /// tolerance; a rule that has both a tolerance and BaselineLocked is a configuration error.
    /// </summary>
    [Fact]
    public void AllToleranceBearingRules_AreNotBaselineLocked()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddVeriqanValidation();
        services.AddVeriqanVisual();

        using var sp = services.BuildServiceProvider();
        var toleranceProvider = sp.GetRequiredService<ILegalToleranceProvider>();
        var rules = sp.GetServices<IVecValidationRule>().ToList();

        var violations = rules
            .Where(r => toleranceProvider.Has(r.CheckId)
                        && r.Classification == RuleClassification.BaselineLocked)
            .Select(r => $"{r.CheckId}(Classification={r.Classification})")
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        violations.ShouldBeEmpty(
            $"The following rules have a registered tolerance but are BaselineLocked. " +
            $"Tolerance-bearing rules should be TenantTightenableOnly or TenantOverridable: " +
            string.Join(", ", violations));
    }
}
