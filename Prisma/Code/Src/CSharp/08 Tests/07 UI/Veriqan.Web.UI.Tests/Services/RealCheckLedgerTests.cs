using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Web.UI.Services;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Tests.Services;

/// <summary>
/// Verifies <see cref="RealCheckLedger"/> loads the embedded ledger resource and maps entries
/// (tier, label, DOF-numeral/brand display, visual/brand flags) per the VLD-S4a rules.
/// </summary>
public sealed class RealCheckLedgerTests
{
    [Fact]
    public void Constructor_LoadsEmbeddedResource_DoesNotThrow()
    {
        var exception = Record.Exception(() => new RealCheckLedger());

        exception.ShouldBeNull();
    }

    [Fact]
    public void TryGet_KnownLawCheck_ReturnsCondusefTierVisualEntry()
    {
        var sut = new RealCheckLedger();

        var found = sut.TryGet("LAW-TYPO-BOLD", out var entry);

        found.ShouldBeTrue();
        entry.ShouldNotBeNull();
        entry.Tier.ShouldBe(ChecklistTier.Condusef);
        entry.IsVisual.ShouldBeTrue();
        entry.IsBrand.ShouldBeFalse();
        entry.DofNumeralDisplay.ShouldContain("Acuerdo");
        entry.Label.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryGet_BrandCheck_ReturnsBrandTagInsteadOfLawShapedNumeral()
    {
        var sut = new RealCheckLedger();

        var found = sut.TryGet("CL-35", out var entry);

        found.ShouldBeTrue();
        entry.ShouldNotBeNull();
        entry.Tier.ShouldBe(ChecklistTier.Bank);
        entry.IsVisual.ShouldBeTrue();
        entry.IsBrand.ShouldBeTrue();
        entry.DofNumeralDisplay.ShouldBe(RealCheckLedger.BrandTag);
    }

    [Fact]
    public void TryGet_BothTierCheck_ReturnsBothTier()
    {
        var sut = new RealCheckLedger();

        var found = sut.TryGet("CL-34", out var entry);

        found.ShouldBeTrue();
        entry.ShouldNotBeNull();
        entry.Tier.ShouldBe(ChecklistTier.Both);
    }

    [Fact]
    public void TryGet_UnknownCheckId_ReturnsFalse()
    {
        var sut = new RealCheckLedger();

        var found = sut.TryGet("CL-DOESNOTEXIST", out var entry);

        found.ShouldBeFalse();
        entry.ShouldBeNull();
    }

    [Fact]
    public void TryGet_VerifyTierCheck_MapsConservativelyToCondusef()
    {
        // CL-41 in the ledger has tier "VERIFY" (owner-unconfirmed) — must map to the
        // conservative Condusef default per ChecklistTier's documented convention.
        var sut = new RealCheckLedger();

        var found = sut.TryGet("CL-41", out var entry);

        found.ShouldBeTrue();
        entry.ShouldNotBeNull();
        entry.Tier.ShouldBe(ChecklistTier.Condusef);
    }
}
