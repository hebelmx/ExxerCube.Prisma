using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Classification;

namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Tests that <see cref="FusionExpedienteService.FuseAsync"/> delegates <c>FechaEstimadaConclusion</c>
/// calculation to the injected <see cref="IBusinessDayCalculator"/> when one is provided, and falls back
/// to the built-in weekend-only logic when none is injected.
/// </summary>
public sealed class FusionExpedienteBusinessDayCalculatorTests
{
    private readonly ITestOutputHelper _output;

    public FusionExpedienteBusinessDayCalculatorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ExtractionMetadata FlatMeta() => new()
    {
        MeanConfidence = null,
        QualityIndex = null,
        TotalFieldsExtracted = 0,
        RegexMatches = 0,
        PatternViolations = 0,
    };

    /// <summary>
    /// Helper: creates a minimal Expediente whose FechaRecepcion + DiasPlazo combination
    /// will trigger the FechaEstimadaConclusion calculation path.
    /// </summary>
    private static Expediente MakeExpediente(DateTime fechaRecepcion, int diasPlazo) => new()
    {
        FechaRecepcion = fechaRecepcion,
        DiasPlazo = diasPlazo,
    };

    // ──────────────────────────────────────────────────────────────────────────────────
    // Without calculator — weekend-only fallback is preserved
    // ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FuseAsync_NoCalculator_FechaEstimadaConclusion_UsesWeekendOnlyFallback()
    {
        // Arrange: Wednesday 11 Jun 2025 + 1 business day = Thursday 12 Jun (no holiday, no weekend)
        var service = new FusionExpedienteService(
            XUnitLogger.CreateLogger<FusionExpedienteService>(_output),
            coefficients: null,
            businessDayCalculator: null);

        var xml = MakeExpediente(new DateTime(2025, 6, 11), diasPlazo: 1);

        var result = await service.FuseAsync(
            xmlExpediente: xml,
            pdfExpediente: null,
            docxExpediente: null,
            xmlMetadata: FlatMeta(),
            pdfMetadata: FlatMeta(),
            docxMetadata: FlatMeta(),
            Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.FusedExpediente.FechaEstimadaConclusion.ShouldBe(new DateTime(2025, 6, 12));
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    // With calculator — holiday-aware path is taken
    // ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FuseAsync_WithCalculator_FechaEstimadaConclusion_IsHolidayAware()
    {
        // Arrange: Mon 15 Sep 2025 + 1 business day
        // Weekend-only result: Tue 16 Sep (Día de la Independencia — a working day per weekend-only)
        // Holiday-aware result: Wed 17 Sep (the holiday calculator skips Tue 16 Sep)
        var calculator = Substitute.For<IBusinessDayCalculator>();
        calculator
            .AddBusinessDays(new DateTime(2025, 9, 15), 1)
            .Returns(new DateTime(2025, 9, 17)); // Holiday-aware answer

        var service = new FusionExpedienteService(
            XUnitLogger.CreateLogger<FusionExpedienteService>(_output),
            coefficients: null,
            businessDayCalculator: calculator);

        var xml = MakeExpediente(new DateTime(2025, 9, 15), diasPlazo: 1);

        var result = await service.FuseAsync(
            xmlExpediente: xml,
            pdfExpediente: null,
            docxExpediente: null,
            xmlMetadata: FlatMeta(),
            pdfMetadata: FlatMeta(),
            docxMetadata: FlatMeta(),
            Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.FusedExpediente.FechaEstimadaConclusion.ShouldBe(new DateTime(2025, 9, 17));
        calculator.Received(1).AddBusinessDays(new DateTime(2025, 9, 15), 1);
    }

    [Fact]
    public async Task FuseAsync_WithRealCalculator_HolidayShiftsDeadline()
    {
        // Use the real MexicoBusinessDayCalculator (not a mock) to verify end-to-end wiring.
        // Mon 15 Sep 2025 + 1 bd → holiday-aware: Wed 17 Sep (Tue 16 = Día de la Independencia)
        var realCalculator = new ExxerCube.Prisma.Infrastructure.Calendar.MexicoBusinessDayCalculator();
        var service = new FusionExpedienteService(
            XUnitLogger.CreateLogger<FusionExpedienteService>(_output),
            coefficients: null,
            businessDayCalculator: realCalculator);

        var xml = MakeExpediente(new DateTime(2025, 9, 15), diasPlazo: 1);

        var result = await service.FuseAsync(
            xmlExpediente: xml,
            pdfExpediente: null,
            docxExpediente: null,
            xmlMetadata: FlatMeta(),
            pdfMetadata: FlatMeta(),
            docxMetadata: FlatMeta(),
            Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.FusedExpediente.FechaEstimadaConclusion.ShouldBe(new DateTime(2025, 9, 17),
            "Real MexicoBusinessDayCalculator must skip Día de la Independencia (Tue 16 Sep 2025)");
    }

    [Fact]
    public async Task FuseAsync_NoDiasPlazo_FechaEstimadaConclusion_IsDefault()
    {
        // DiasPlazo = 0: calculation must not run; FechaEstimadaConclusion stays default(DateTime)
        var calculator = Substitute.For<IBusinessDayCalculator>();
        var service = new FusionExpedienteService(
            XUnitLogger.CreateLogger<FusionExpedienteService>(_output),
            coefficients: null,
            businessDayCalculator: calculator);

        var xml = MakeExpediente(new DateTime(2025, 9, 15), diasPlazo: 0);

        await service.FuseAsync(
            xmlExpediente: xml,
            pdfExpediente: null,
            docxExpediente: null,
            xmlMetadata: FlatMeta(),
            pdfMetadata: FlatMeta(),
            docxMetadata: FlatMeta(),
            Ct);

        calculator.DidNotReceive().AddBusinessDays(Arg.Any<DateTime>(), Arg.Any<int>());
    }
}
