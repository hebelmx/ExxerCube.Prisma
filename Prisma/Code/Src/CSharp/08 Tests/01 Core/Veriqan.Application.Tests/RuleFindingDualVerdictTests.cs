using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;

namespace ExxerCube.Prisma.Veriqan.Application.Tests;

/// <summary>
/// Unit tests for the dual-verdict contract on <see cref="RuleFinding"/> (Story 9.1).
/// Covers: factory default (no-divergence) invariants, the
/// <see cref="RuleFinding.TenantProfileVerdict"/> alias, diverged findings,
/// and <see cref="FindingVerdict.InsufficientData"/> distinctness.
/// </summary>
public sealed class RuleFindingDualVerdictTests
{
    // -----------------------------------------------------------------------
    // Pass factory — default no-divergence invariants
    // -----------------------------------------------------------------------

    [Fact]
    public void Pass_Factory_LegalBaselineVerdict_EqualsEffectiveVerdict()
    {
        var finding = RuleFinding.Pass("CL-01", TechniqueClass.Deterministic, "1.0.0");

        finding.LegalBaselineVerdict.ShouldBe(FindingVerdict.Pass);
        finding.Verdict.ShouldBe(FindingVerdict.Pass);
        finding.LegalBaselineVerdict.ShouldBe(finding.Verdict);
    }

    [Fact]
    public void Pass_Factory_TenantProfileVerdict_EqualsVerdict()
    {
        var finding = RuleFinding.Pass("CL-01", TechniqueClass.Deterministic, "1.0.0");

        finding.TenantProfileVerdict.ShouldBe(finding.Verdict);
    }

    // -----------------------------------------------------------------------
    // Fail factory — default no-divergence invariants
    // -----------------------------------------------------------------------

    [Fact]
    public void Fail_Factory_LegalBaselineVerdict_EqualsEffectiveVerdict()
    {
        var finding = RuleFinding.Fail(
            "CL-02",
            TechniqueClass.Deterministic,
            FindingSeverity.Critical,
            "1.0.0");

        finding.LegalBaselineVerdict.ShouldBe(FindingVerdict.Fail);
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        finding.LegalBaselineVerdict.ShouldBe(finding.Verdict);
    }

    [Fact]
    public void Fail_Factory_TenantProfileVerdict_EqualsVerdict()
    {
        var finding = RuleFinding.Fail(
            "CL-02",
            TechniqueClass.Deterministic,
            FindingSeverity.Critical,
            "1.0.0");

        finding.TenantProfileVerdict.ShouldBe(finding.Verdict);
    }

    // -----------------------------------------------------------------------
    // InsufficientData factory — default no-divergence invariants
    // -----------------------------------------------------------------------

    [Fact]
    public void InsufficientData_Factory_LegalBaselineVerdict_EqualsEffectiveVerdict()
    {
        var finding = RuleFinding.InsufficientData(
            "CL-03",
            TechniqueClass.Deterministic,
            "1.0.0",
            reason: "no rate data");

        finding.LegalBaselineVerdict.ShouldBe(FindingVerdict.InsufficientData);
        finding.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        finding.LegalBaselineVerdict.ShouldBe(finding.Verdict);
    }

    [Fact]
    public void InsufficientData_Factory_TenantProfileVerdict_EqualsVerdict()
    {
        var finding = RuleFinding.InsufficientData(
            "CL-03",
            TechniqueClass.Deterministic,
            "1.0.0");

        finding.TenantProfileVerdict.ShouldBe(finding.Verdict);
    }

    // -----------------------------------------------------------------------
    // InsufficientData distinctness
    // -----------------------------------------------------------------------

    [Fact]
    public void InsufficientData_IsDistinctFrom_Pass()
    {
        var insuff = RuleFinding.InsufficientData("CL-04", TechniqueClass.Deterministic, "1.0.0");
        var pass = RuleFinding.Pass("CL-04", TechniqueClass.Deterministic, "1.0.0");

        insuff.Verdict.ShouldNotBe(pass.Verdict);
        insuff.LegalBaselineVerdict.ShouldNotBe(FindingVerdict.Pass);
    }

    [Fact]
    public void InsufficientData_IsDistinctFrom_Fail()
    {
        var insuff = RuleFinding.InsufficientData("CL-05", TechniqueClass.Deterministic, "1.0.0");
        var fail = RuleFinding.Fail("CL-05", TechniqueClass.Deterministic, FindingSeverity.Critical, "1.0.0");

        insuff.Verdict.ShouldNotBe(fail.Verdict);
        insuff.LegalBaselineVerdict.ShouldNotBe(FindingVerdict.Fail);
    }

    // -----------------------------------------------------------------------
    // Diverged finding: legal floor = Pass, effective (tenant) = Fail
    // -----------------------------------------------------------------------

    [Fact]
    public void Fail_Factory_WithLegalBaselineOverride_ExposesPassBaseline_AndFailEffective()
    {
        // Realistic case (Story 9.1 design): statement PASSES the legal floor
        // but FAILS a stricter tenant threshold.
        var finding = RuleFinding.Fail(
            "CL-RATE-01",
            TechniqueClass.Deterministic,
            FindingSeverity.Critical,
            "1.0.0",
            expected: "≤28%",
            observed: "29%",
            legalBaselineVerdict: FindingVerdict.Pass);   // ← divergence point

        finding.Verdict.ShouldBe(FindingVerdict.Fail);            // effective = Fail
        finding.TenantProfileVerdict.ShouldBe(FindingVerdict.Fail);
        finding.LegalBaselineVerdict.ShouldBe(FindingVerdict.Pass); // legal floor = Pass
        finding.LegalBaselineVerdict.ShouldNotBe(finding.Verdict); // divergence confirmed
    }

    [Fact]
    public void Pass_Factory_WithLegalBaselineOverride_ExposesFailBaseline_AndPassEffective()
    {
        // Edge case: effective verdict is Pass (tenant is lenient) but legal floor is Fail.
        var finding = RuleFinding.Pass(
            "CL-SOME-01",
            TechniqueClass.Deterministic,
            "1.0.0",
            observed: "some-value",
            legalBaselineVerdict: FindingVerdict.Fail);

        finding.Verdict.ShouldBe(FindingVerdict.Pass);
        finding.TenantProfileVerdict.ShouldBe(FindingVerdict.Pass);
        finding.LegalBaselineVerdict.ShouldBe(FindingVerdict.Fail);
    }

    // -----------------------------------------------------------------------
    // Determinism: identical construction produces equal records
    // -----------------------------------------------------------------------

    [Fact]
    public void EqualInputs_ProduceEqualRecords()
    {
        var a = RuleFinding.Fail("CL-99", TechniqueClass.Deterministic, FindingSeverity.Critical, "2.0.0",
            expected: "X", observed: "Y");
        var b = RuleFinding.Fail("CL-99", TechniqueClass.Deterministic, FindingSeverity.Critical, "2.0.0",
            expected: "X", observed: "Y");

        a.ShouldBe(b);
        a.LegalBaselineVerdict.ShouldBe(b.LegalBaselineVerdict);
        a.TenantProfileVerdict.ShouldBe(b.TenantProfileVerdict);
    }

    [Fact]
    public void DivergentEqualInputs_ProduceEqualRecords()
    {
        var a = RuleFinding.Fail("CL-88", TechniqueClass.Deterministic, FindingSeverity.Critical, "1.0.0",
            legalBaselineVerdict: FindingVerdict.Pass);
        var b = RuleFinding.Fail("CL-88", TechniqueClass.Deterministic, FindingSeverity.Critical, "1.0.0",
            legalBaselineVerdict: FindingVerdict.Pass);

        a.ShouldBe(b);
        a.LegalBaselineVerdict.ShouldBe(b.LegalBaselineVerdict);
    }
}
