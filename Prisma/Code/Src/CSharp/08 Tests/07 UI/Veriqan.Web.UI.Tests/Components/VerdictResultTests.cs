using System.Collections.Generic;
using Bunit;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Web.UI.Components.Shared;
using ExxerCube.Prisma.Veriqan.Web.UI.Models;
using ExxerCube.Prisma.Veriqan.Web.UI.Services;
using MudBlazor;
using MudBlazor.Services;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Tests.Components;

/// <summary>
/// Render tests for <see cref="VerdictResult"/> (VLD-S4b) — a reusable, parameterized
/// presentational component fed a single <see cref="DemoStatementCase"/>. These prove the
/// component renders correctly whether the case came from the live pipeline (post VLD-S4a
/// mapper enrichment) or the canned <c>DemoDataService</c>, without wiring it into any page.
/// </summary>
public sealed class VerdictResultTests
{
    private static Bunit.BunitContext CreateContext()
    {
        var ctx = new Bunit.BunitContext();
        // MudBlazor components (e.g. MudChip's key-interceptor) invoke JS interop from
        // OnAfterRenderAsync. bUnit's default "Strict" JSInterop mode treats any unconfigured
        // call as a fatal render error; "Loose" makes unconfigured calls return a default value
        // instead, which is the standard bUnit setup for testing MudBlazor components headless.
        ctx.JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        // VUX-S5: the consolidated tier tables render MudTooltip (Confianza column header + the
        // optional check-grid cells), and MudBlazor 8.x renders tooltips through a popover — that
        // throws "Missing <MudPopoverProvider />" unless one is registered against the shared
        // IPopoverService in this BunitContext. The real app gets this from its layout; MudPopoverProvider
        // has no ChildContent/Body parameter so it cannot be added via ctx.RenderTree.Add (that API
        // requires a wrapping parameter) — rendering it as its own root component registers it with
        // the context's IPopoverService, which is all MudTooltip needs.
        ctx.Render<MudPopoverProvider>();
        return ctx;
    }

    private static DemoFinding Fail(
        string checkId,
        bool isVisual,
        ChecklistTier tier = ChecklistTier.Condusef,
        string label = "Some check",
        string dofNumeral = "Art. 1")
        => new()
        {
            CheckId = checkId,
            Verdict = FindingVerdict.Fail,
            Technique = TechniqueClass.Deterministic,
            Severity = FindingSeverity.Critical,
            Label = label,
            Expected = "expected-value",
            Observed = "observed-value",
            DofNumeral = dofNumeral,
            Tier = tier,
            IsVisual = isVisual,
        };

    private static DemoFinding InsufficientData(string checkId)
        => new()
        {
            CheckId = checkId,
            Verdict = FindingVerdict.InsufficientData,
            Technique = TechniqueClass.Deterministic,
            Severity = FindingSeverity.Warning,
            Label = "Insufficient data check",
            DofNumeral = "Art. 2",
            Tier = ChecklistTier.Bank,
        };

    private static DemoStatementCase BuildCase(
        VerdictSignal signal,
        IReadOnlyList<DemoFinding> findings,
        IReadOnlyDictionary<int, byte[]>? markedPagePngs = null,
        ExxerCube.Prisma.Veriqan.Domain.Enums.BlockReason? blockReason = null,
        string? blockDetail = null,
        string? markedPdfPath = null,
        DemoExtractedFields? extractedFields = null)
    {
        var passCount = findings.Count == 0 ? 12 : 0;
        return new DemoStatementCase
        {
            CaseName = $"Caso {signal}",
            FileName = "estado-cuenta.pdf",
            Signal = signal,
            CondusefTierVerdict = signal,
            BankTierVerdict = signal,
            TotalChecks = findings.Count + passCount,
            PassCount = passCount,
            FailCount = findings.Count(f => f.Verdict == FindingVerdict.Fail),
            InsufficientDataCount = findings.Count(f => f.Verdict == FindingVerdict.InsufficientData),
            Findings = findings,
            ProcessingDuration = TimeSpan.FromSeconds(1.23),
            MarkedPagePngs = markedPagePngs ?? new Dictionary<int, byte[]>(),
            BlockReason = blockReason,
            BlockDetail = blockDetail,
            MarkedPdfPath = markedPdfPath,
            ExtractedFields = extractedFields ?? new DemoExtractedFields(),
        };
    }

    [Fact]
    public async Task RendersLiveMappedRedCase_ShowsRibbonCounts()
    {
        await using var ctx = CreateContext();
        var findings = new List<DemoFinding>
        {
            Fail("CL-35", isVisual: true, tier: ChecklistTier.Bank),
            Fail("LAW-TYPO-BOLD", isVisual: true, tier: ChecklistTier.Condusef),
            Fail("CL-21", isVisual: false, tier: ChecklistTier.Both),
        };
        var demoCase = BuildCase(VerdictSignal.Red, findings);

        var cut = ctx.Render<VerdictResult>(builder =>
            builder.Add(c => c.Case, demoCase));

        cut.Markup.ShouldContain("2 VISUAL");
        cut.Markup.ShouldContain("1 DATA");
    }

    [Fact]
    public async Task RendersGreenCaseWithoutThrowing()
    {
        await using var ctx = CreateContext();
        var demoCase = BuildCase(VerdictSignal.Green, []);

        var cut = ctx.Render<VerdictResult>(builder =>
            builder.Add(c => c.Case, demoCase));

        cut.Markup.ShouldContain("GREEN");
        cut.Markup.ShouldContain("Sin incumplimientos");
    }

    [Fact]
    public async Task RendersBlockedCaseWithoutThrowing()
    {
        await using var ctx = CreateContext();
        var demoCase = BuildCase(
            VerdictSignal.Blocked,
            [],
            blockReason: ExxerCube.Prisma.Veriqan.Domain.Enums.BlockReason.EncryptedDocument,
            blockDetail: "Documento cifrado");

        var cut = ctx.Render<VerdictResult>(builder =>
            builder.Add(c => c.Case, demoCase));

        cut.Markup.ShouldContain("BLOCKED");
    }

    [Fact]
    public async Task InsufficientDataFindings_RenderGreyNotHidden()
    {
        await using var ctx = CreateContext();
        var findings = new List<DemoFinding> { InsufficientData("CL-99") };
        var demoCase = BuildCase(VerdictSignal.Yellow, findings);

        var cut = ctx.Render<VerdictResult>(builder =>
            builder.Add(c => c.Case, demoCase));

        cut.Markup.ShouldContain("CL-99");
        cut.Markup.ShouldContain("Datos insuficientes");
    }

    [Fact]
    public async Task EmptyMarkedPagePngs_ShowsPlaceholderNotCrash()
    {
        await using var ctx = CreateContext();
        var demoCase = BuildCase(VerdictSignal.Red, [Fail("CL-21", isVisual: false)]);

        var cut = ctx.Render<VerdictResult>(builder =>
            builder.Add(c => c.Case, demoCase));

        demoCase.MarkedPagePngs.Count.ShouldBe(0);
        cut.Markup.ShouldContain("En producción, el pipeline genera una imagen");
    }

    [Fact]
    public async Task TierChips_UseExactDesignPhrases()
    {
        await using var ctx = CreateContext();
        var findings = new List<DemoFinding>
        {
            Fail("CL-21", isVisual: false, tier: ChecklistTier.Both),
            Fail("LAW-1", isVisual: true, tier: ChecklistTier.Condusef),
            Fail("CL-1", isVisual: true, tier: ChecklistTier.Bank),
        };
        var demoCase = BuildCase(VerdictSignal.Red, findings);

        var cut = ctx.Render<VerdictResult>(builder =>
            builder.Add(c => c.Case, demoCase));

        // The CONDUSEF (RED) table renders a Tier chip with these two phrases (VUX-S5: the
        // tier-grouped tables from RedCase). The Bank (YELLOW) table has no Tier column (all
        // rows are implicitly Bank) — its CheckId chip is styled via TierChipStyle(Bank) instead
        // of carrying the TierChipText phrase; that color-ramp coverage is asserted separately
        // in TierChips_UseOwnerSpecified3ColorRamp below.
        cut.Markup.ShouldContain("Falla checklist del banco Y ley CONDUSEF");
        cut.Markup.ShouldContain("Falla ley CONDUSEF — mejora sugerida al checklist del banco");
    }

    [Fact]
    public async Task TierChips_UseOwnerSpecified3ColorRamp()
    {
        await using var ctx = CreateContext();
        var findings = new List<DemoFinding>
        {
            Fail("CL-21", isVisual: false, tier: ChecklistTier.Both),
            Fail("LAW-1", isVisual: true, tier: ChecklistTier.Condusef),
            Fail("CL-1", isVisual: true, tier: ChecklistTier.Bank),
        };
        var demoCase = BuildCase(VerdictSignal.Red, findings);

        var cut = ctx.Render<VerdictResult>(builder =>
            builder.Add(c => c.Case, demoCase));

        cut.Markup.ShouldContain("background-color:#F44336;color:#fff;");
        cut.Markup.ShouldContain("background-color:#FF9800;color:#000;");
        cut.Markup.ShouldContain("background-color:#FBC02D;color:#000;");
    }

    [Fact]
    public async Task FailFindings_RenderInCaseFindingsOrder_NotResorted()
    {
        await using var ctx = CreateContext();
        // Deliberately out of severity order: this order must be preserved to match the hero's
        // numbered callouts (MarkedPdfGenerator assigns numbers in finding order).
        var findings = new List<DemoFinding>
        {
            Fail("CL-ZZZ", isVisual: false, label: "Z check"),
            Fail("CL-AAA", isVisual: true, label: "A check"),
        };
        var demoCase = BuildCase(VerdictSignal.Red, findings);

        var cut = ctx.Render<VerdictResult>(builder =>
            builder.Add(c => c.Case, demoCase));

        var markup = cut.Markup;
        var zIndex = markup.IndexOf("CL-ZZZ", StringComparison.Ordinal);
        var aIndex = markup.IndexOf("CL-AAA", StringComparison.Ordinal);
        zIndex.ShouldBeGreaterThanOrEqualTo(0);
        aIndex.ShouldBeGreaterThan(zIndex);
    }

    /// <summary>
    /// VUX-S4a (supersedes VLD-S5c FIX 4): an uncatalogued finding (engineering gap, not a
    /// compliance signal) must be hidden ENTIRELY from the audience-facing rail — not merely
    /// re-badged with a neutral chip. In front of a legal audience it must render nothing: no
    /// CheckId, no law-shaped tier chip, no raw DofNumeral, and not even the "Sin catalogar" chip.
    /// The neutral-chip branch remains in the component as a dead-safe fallback but is unreachable
    /// because uncatalogued findings are filtered out of both rails in OnParametersSet.
    /// </summary>
    [Fact]
    public async Task VerdictResult_UncataloguedFinding_IsHiddenFromRail()
    {
        await using var ctx = CreateContext();
        var findings = new List<DemoFinding>
        {
            Fail(
                "CL-UNKNOWN",
                isVisual: false,
                tier: ChecklistTier.Condusef,
                label: RealCheckLedger.UncataloguedLabel,
                dofNumeral: "Art. 99"),
        };
        var demoCase = BuildCase(VerdictSignal.Red, findings);

        var cut = ctx.Render<VerdictResult>(builder =>
            builder.Add(c => c.Case, demoCase));

        cut.Markup.ShouldNotContain("CL-UNKNOWN");
        cut.Markup.ShouldNotContain("Sin catalogar");
        cut.Markup.ShouldNotContain("Falla ley CONDUSEF — mejora sugerida al checklist del banco");
        cut.Markup.ShouldNotContain("Art. 99");
    }

    /// <summary>
    /// VUX-S4a regression (found by adversarial review): filtering uncatalogued findings out of the
    /// rendered rail must NOT let the "Sin incumplimientos" (no-violations) success message appear on
    /// a case that actually has failures. A RED case whose ONLY finding is an uncatalogued
    /// engineering gap has an empty rendered rail but a non-zero aggregate FailCount — the
    /// no-violations state is gated on the aggregate Case counts (mirroring the ribbon), so it must
    /// stay hidden. Without the aggregate gate, "Sin incumplimientos" rendered directly under a RED
    /// banner — the exact contradiction this hardening pass exists to prevent for a legal audience.
    /// </summary>
    [Fact]
    public async Task VerdictResult_RedCaseWithOnlyUncataloguedFinding_DoesNotClaimNoViolations()
    {
        await using var ctx = CreateContext();
        var findings = new List<DemoFinding>
        {
            Fail(
                "CL-UNKNOWN",
                isVisual: false,
                tier: ChecklistTier.Condusef,
                label: RealCheckLedger.UncataloguedLabel,
                dofNumeral: "Art. 99"),
        };
        var demoCase = BuildCase(VerdictSignal.Red, findings);

        var cut = ctx.Render<VerdictResult>(builder =>
            builder.Add(c => c.Case, demoCase));

        cut.Markup.ShouldContain("RED");
        // The overall no-violations claim ("Sin incumplimientos.") must not appear — checked with
        // the trailing period so this does not collide with the tier tables' own accurate,
        // narrower per-tier empty-state alerts (e.g. "Sin incumplimientos CONDUSEF.", which is a
        // true statement about the catalogued CONDUSEF table being empty, not an overall claim).
        cut.Markup.ShouldNotContain("Sin incumplimientos.");
        cut.Markup.ShouldNotContain("CL-UNKNOWN"); // still hidden from the rail
    }

    [Fact]
    public async Task RedCase_RendersCondusefTableWithConfidencePercentage()
    {
        await using var ctx = CreateContext();
        var findings = new List<DemoFinding>
        {
            Fail("CL-21", isVisual: false, tier: ChecklistTier.Condusef, label: "Aritmética"),
        };
        var demoCase = BuildCase(VerdictSignal.Red, findings);

        var cut = ctx.Render<VerdictResult>(builder =>
            builder.Add(c => c.Case, demoCase));

        cut.Markup.ShouldContain("Incumplimiento CONDUSEF (RED)");
        cut.Markup.ShouldContain("CL-21");
        cut.Markup.ShouldContain("100%");
    }

    [Fact]
    public async Task BankTierFail_AppearsInYellowTable_NotCondusefTable()
    {
        await using var ctx = CreateContext();
        var findings = new List<DemoFinding>
        {
            Fail("CL-35", isVisual: true, tier: ChecklistTier.Bank, label: "Tipo de fuente"),
        };
        var demoCase = BuildCase(VerdictSignal.Yellow, findings);

        var cut = ctx.Render<VerdictResult>(builder =>
            builder.Add(c => c.Case, demoCase));

        cut.Markup.ShouldContain("Oportunidades de mejora del banco (YELLOW)");
        cut.Markup.ShouldContain("CL-35");
        // The CONDUSEF table has no Bank-tier rows — its empty-state alert must render instead.
        cut.Markup.ShouldContain("Sin incumplimientos CONDUSEF.");
    }

    [Fact]
    public async Task ShowCheckGrid_True_RendersCheckGridCells()
    {
        await using var ctx = CreateContext();
        var findings = new List<DemoFinding> { Fail("CL-21", isVisual: false) };
        var demoCase = BuildCase(VerdictSignal.Red, findings);

        var cut = ctx.Render<VerdictResult>(builder =>
        {
            builder.Add(c => c.Case, demoCase);
            builder.Add(c => c.ShowCheckGrid, true);
        });

        cut.Markup.ShouldContain("vec-check-cell");
        cut.Markup.ShouldContain("Grilla de verificación");
    }

    [Fact]
    public async Task ShowCheckGrid_DefaultFalse_DoesNotRenderCheckGrid()
    {
        await using var ctx = CreateContext();
        var findings = new List<DemoFinding> { Fail("CL-21", isVisual: false) };
        var demoCase = BuildCase(VerdictSignal.Red, findings);

        var cut = ctx.Render<VerdictResult>(builder =>
            builder.Add(c => c.Case, demoCase));

        cut.Markup.ShouldNotContain("vec-check-cell");
    }

    [Fact]
    public async Task ShowExtractedFields_True_RendersBankNameFromCase()
    {
        await using var ctx = CreateContext();
        var demoCase = BuildCase(
            VerdictSignal.Yellow,
            [],
            extractedFields: new DemoExtractedFields { BankName = "Banco Demo S.A." });

        var cut = ctx.Render<VerdictResult>(builder =>
        {
            builder.Add(c => c.Case, demoCase);
            builder.Add(c => c.ShowExtractedFields, true);
        });

        cut.Markup.ShouldContain("Datos del documento");
        cut.Markup.ShouldContain("Banco Demo S.A.");
    }

    [Fact]
    public async Task Hero_FallsBackToPdfIframe_WhenMarkedPdfPathSetAndPngsEmpty()
    {
        await using var ctx = CreateContext();
        var demoCase = BuildCase(
            VerdictSignal.Red,
            [Fail("CL-21", isVisual: false)],
            markedPagePngs: new Dictionary<int, byte[]>(),
            markedPdfPath: "exports/demo-marked.pdf");

        var cut = ctx.Render<VerdictResult>(builder =>
            builder.Add(c => c.Case, demoCase));

        demoCase.MarkedPagePngs.Count.ShouldBe(0);
        cut.Markup.ShouldContain("vec-pdf-frame");
        cut.Markup.ShouldContain("exports/demo-marked.pdf");
    }
}
