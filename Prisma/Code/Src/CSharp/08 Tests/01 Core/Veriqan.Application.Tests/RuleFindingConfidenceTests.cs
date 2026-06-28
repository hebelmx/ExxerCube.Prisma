using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Veriqan.Application.Tests;

/// <summary>
/// Unit tests for the <see cref="RuleFinding.Confidence"/> property (Story 4.1 — Epic 4).
/// Covers: default value, and that all three factories leave the default in place
/// (the engine stamps the actual value centrally; rules and factories do not set it).
/// </summary>
public sealed class RuleFindingConfidenceTests
{
    // -----------------------------------------------------------------------
    // Default value — all factories default Confidence to 1.0
    // -----------------------------------------------------------------------

    [Fact]
    public void Pass_Factory_Confidence_DefaultsToOne()
    {
        var finding = RuleFinding.Pass("CL-01", TechniqueClass.Deterministic, "1.0.0");

        finding.Confidence.ShouldBe(1.0, "Pass factory must default Confidence to 1.0");
    }

    [Fact]
    public void Fail_Factory_Confidence_DefaultsToOne()
    {
        var finding = RuleFinding.Fail(
            "CL-02",
            TechniqueClass.Deterministic,
            FindingSeverity.Critical,
            "1.0.0");

        finding.Confidence.ShouldBe(1.0, "Fail factory must default Confidence to 1.0");
    }

    [Fact]
    public void InsufficientData_Factory_Confidence_DefaultsToOne()
    {
        var finding = RuleFinding.InsufficientData(
            "CL-03",
            TechniqueClass.Deterministic,
            "1.0.0",
            reason: "unit test");

        finding.Confidence.ShouldBe(1.0, "InsufficientData factory must default Confidence to 1.0");
    }

    // -----------------------------------------------------------------------
    // Init property — can be overridden via `with` expression (as the engine does)
    // -----------------------------------------------------------------------

    [Fact]
    public void Confidence_WithExpression_OverridesDefault()
    {
        var finding = RuleFinding.Pass("CL-04", TechniqueClass.Deterministic, "1.0.0")
            with { Confidence = 0.82 };

        finding.Confidence.ShouldBe(0.82, "with-expression must override Confidence");
        // Other properties must be unchanged
        finding.Verdict.ShouldBe(FindingVerdict.Pass);
        finding.CheckId.ShouldBe("CL-04");
    }

    [Fact]
    public void Confidence_Range_AcceptsZeroToOne()
    {
        var atZero = RuleFinding.Pass("CL-05", TechniqueClass.Deterministic, "1.0.0")
            with { Confidence = 0.0 };
        var atOne = RuleFinding.Pass("CL-06", TechniqueClass.Deterministic, "1.0.0")
            with { Confidence = 1.0 };

        atZero.Confidence.ShouldBe(0.0);
        atOne.Confidence.ShouldBe(1.0);
    }
}
