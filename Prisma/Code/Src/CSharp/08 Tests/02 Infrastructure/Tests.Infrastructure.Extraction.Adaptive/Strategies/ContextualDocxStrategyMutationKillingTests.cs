namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Adaptive.Strategies;

using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;
using ExxerCube.Prisma.Testing.Abstractions;
using Meziantou.Extensions.Logging.Xunit.v3;

/// <summary>
/// Mutation-killing tests for <see cref="ContextualDocxStrategy"/>.
/// </summary>
/// <remarks>
/// Written against the Stryker.NET survivor map. The existing <c>ContextualDocxStrategyLiskovTests</c> use
/// loose assertions (<c>ShouldContain</c>, <c>if (ContainsKey)</c> guards, confidence only asserted
/// <c>&gt;= 70</c>), so the keyword-count confidence ladder (80/70/50/0 at &gt;=6/&gt;=4/&gt;=2 keywords), the
/// <see cref="ContextualDocxStrategy.CanExtractAsync"/> &gt;=2 boundary, the currency-normalization switch,
/// the Causa/Accion cleanup regexes, the extended-field/account values, and the per-branch hasAnyData
/// bookkeeping are never pinned to exact values. These tests assert exact values so the corresponding
/// string/equality/boolean mutants become observable.
/// </remarks>
public sealed class ContextualDocxStrategyMutationKillingTests
{
    private readonly ContextualDocxStrategy _strategy;

    public ContextualDocxStrategyMutationKillingTests(ITestOutputHelper output)
    {
        var logger = XUnitLogger.CreateLogger<ContextualDocxStrategy>(output);
        _strategy = new ContextualDocxStrategy(logger);
    }

    private Task<ExtractedFields?> ExtractAsync(string text)
        => _strategy.ExtractAsync(text, TestContext.Current.CancellationToken);

    private Task<int> ConfidenceAsync(string text)
        => _strategy.GetConfidenceAsync(text, TestContext.Current.CancellationToken);

    private Task<bool> CanExtractAsync(string text)
        => _strategy.CanExtractAsync(text, TestContext.Current.CancellationToken);

    // =====================================================================
    // Keyword-count confidence ladder (L158-164): >=6 => 80, >=4 => 70, >=2 => 50, _ => 0.
    // ContextualKeywords (12): expediente oficio autoridad causa solicitud aseguramiento
    // precautorio cuenta clabe banco monto fecha.
    // =====================================================================

    [Fact]
    public async Task GetConfidence_OneKeyword_ReturnsZero()
    {
        // count 1 (< 2) -> 0.
        var confidence = await ConfidenceAsync("expediente");

        confidence.ShouldBe(0);
    }

    [Fact]
    public async Task GetConfidence_TwoKeywords_Returns50()
    {
        var confidence = await ConfidenceAsync("expediente oficio");

        confidence.ShouldBe(50);
    }

    [Fact]
    public async Task GetConfidence_FourKeywords_Returns70()
    {
        var confidence = await ConfidenceAsync("expediente oficio autoridad causa");

        confidence.ShouldBe(70);
    }

    [Fact]
    public async Task GetConfidence_SixKeywords_Returns80()
    {
        var confidence = await ConfidenceAsync("expediente oficio autoridad causa solicitud aseguramiento");

        confidence.ShouldBe(80);
    }

    [Fact]
    public async Task GetConfidence_NoKeywords_ReturnsZero()
    {
        var confidence = await ConfidenceAsync("plain prose with nothing relevant");

        confidence.ShouldBe(0);
    }

    [Fact]
    public async Task GetConfidence_Whitespace_ReturnsZero()
    {
        var confidence = await ConfidenceAsync("   ");

        confidence.ShouldBe(0);
    }

    // =====================================================================
    // CanExtractAsync (L139-142): keywordCount >= 2.
    // =====================================================================

    [Fact]
    public async Task CanExtract_OneKeyword_ReturnsFalse()
    {
        var canExtract = await CanExtractAsync("expediente");

        canExtract.ShouldBeFalse();
    }

    [Fact]
    public async Task CanExtract_TwoKeywords_ReturnsTrue()
    {
        var canExtract = await CanExtractAsync("expediente oficio");

        canExtract.ShouldBeTrue();
    }

    [Fact]
    public async Task CanExtract_Whitespace_ReturnsFalse()
    {
        var canExtract = await CanExtractAsync("   ");

        canExtract.ShouldBeFalse();
    }

    // =====================================================================
    // ExtractMonetaryAmounts currency normalization switch (L312-320).
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_Monto_Mxn()
    {
        var result = await ExtractAsync("monto $1,234.56 MXN");

        result.ShouldNotBeNull();
        result.Montos[0].Currency.ShouldBe("MXN");
        result.Montos[0].Value.ShouldBe(1234.56m);
    }

    [Fact]
    public async Task ExtractAsync_Monto_ModenaNacional_NormalizesToMxn()
    {
        var result = await ExtractAsync("monto $500.00 M.N.");

        result.ShouldNotBeNull();
        result.Montos[0].Currency.ShouldBe("MXN");
    }

    [Fact]
    public async Task ExtractAsync_Monto_Pesos_NormalizesToMxn()
    {
        var result = await ExtractAsync("monto $500.00 pesos");

        result.ShouldNotBeNull();
        result.Montos[0].Currency.ShouldBe("MXN");
    }

    [Fact]
    public async Task ExtractAsync_Monto_Usd()
    {
        var result = await ExtractAsync("monto $500.00 USD");

        result.ShouldNotBeNull();
        result.Montos[0].Currency.ShouldBe("USD");
    }

    [Fact]
    public async Task ExtractAsync_Monto_Eur()
    {
        var result = await ExtractAsync("monto $500.00 EUR");

        result.ShouldNotBeNull();
        result.Montos[0].Currency.ShouldBe("EUR");
    }

    [Fact]
    public async Task ExtractAsync_Monto_NoCurrency_DefaultsToMxn()
    {
        var result = await ExtractAsync("monto $750.00 estimado");

        result.ShouldNotBeNull();
        result.Montos[0].Currency.ShouldBe("MXN");
        result.Montos[0].Value.ShouldBe(750.00m);
    }

    // =====================================================================
    // hasAnyData bookkeeping (L70/78/86): single core field -> non-null.
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_ExpedienteOnly_ReturnsNonNull()
    {
        var result = await ExtractAsync("en el expediente A/AS1-2505-088637-PHM");

        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
    }

    [Fact]
    public async Task ExtractAsync_CausaOnly_ReturnsNonNull()
    {
        var result = await ExtractAsync("causa de lavado de dinero");

        result.ShouldNotBeNull();
        result.Causa.ShouldNotBeNull();
        result.Causa.ShouldContain("lavado de dinero");
    }

    [Fact]
    public async Task ExtractAsync_AccionOnly_ReturnsNonNull()
    {
        var result = await ExtractAsync("se solicita el aseguramiento precautorio urgente");

        result.ShouldNotBeNull();
        result.AccionSolicitada.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExtractAsync_NoExtractableData_ReturnsNull()
    {
        var result = await ExtractAsync("plain prose with no fields whatsoever here");

        result.ShouldBeNull();
    }

    // =====================================================================
    // Causa cleanup (L215-216): stop at "conforme".
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_Causa_StopsAtConforme()
    {
        var result = await ExtractAsync("causa de lavado conforme a derecho aplicable");

        result.ShouldNotBeNull();
        result.Causa.ShouldBe("lavado");
    }

    // =====================================================================
    // Accion cleanup (L241-242): stop at "de la cuenta" / "identificada".
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_Accion_StopsAtDeLaCuenta()
    {
        var result = await ExtractAsync("se solicita el aseguramiento de la cuenta bancaria");

        result.ShouldNotBeNull();
        result.AccionSolicitada.ShouldBe("aseguramiento");
    }

    [Fact]
    public async Task ExtractAsync_Accion_StopsAtIdentificada()
    {
        var result = await ExtractAsync("se solicita el bloqueo total identificada con clabe");

        result.ShouldNotBeNull();
        result.AccionSolicitada.ShouldBe("bloqueo total");
    }

    // =====================================================================
    // Extended fields + account info exact values (L253-268, L331-349).
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_NumeroOficio_Exact()
    {
        var result = await ExtractAsync("oficio 214-1-18714972/2025");

        result.ShouldNotBeNull();
        result.AdditionalFields["NumeroOficio"].ShouldBe("214-1-18714972/2025");
    }

    [Fact]
    public async Task ExtractAsync_AutoridadNombre_UppercasedExact()
    {
        var result = await ExtractAsync("autoridad emisora pgr");

        result.ShouldNotBeNull();
        result.AdditionalFields["AutoridadNombre"].ShouldBe("PGR");
    }

    [Fact]
    public async Task ExtractAsync_Clabe_Exact()
    {
        var result = await ExtractAsync("oficio 214-1-18714972/2025 con clabe 012345678901234567");

        result.ShouldNotBeNull();
        result.AdditionalFields["CLABE"].ShouldBe("012345678901234567");
    }

    [Fact]
    public async Task ExtractAsync_Banco_UppercasedExact()
    {
        var result = await ExtractAsync("oficio 214-1-18714972/2025 en banamex.");

        result.ShouldNotBeNull();
        result.AdditionalFields["Banco"].ShouldBe("BANAMEX");
    }

    // =====================================================================
    // ExtractDates (L271-296): "de" long format, numeric, dedup.
    // A NumeroOficio keeps the result non-null (Fechas are not counted by the guard).
    // =====================================================================

    private const string OficioPrefix = "oficio 214-1-18714972/2025 ";

    [Fact]
    public async Task ExtractAsync_Dates_NumericFormat()
    {
        var result = await ExtractAsync(OficioPrefix + "fecha 15/11/2025");

        result.ShouldNotBeNull();
        result.Fechas.ShouldContain("15/11/2025");
    }

    [Fact]
    public async Task ExtractAsync_Dates_LongDeFormat()
    {
        var result = await ExtractAsync(OficioPrefix + "fecha 15 de noviembre de 2025");

        result.ShouldNotBeNull();
        result.Fechas.ShouldContain("15 de noviembre de 2025");
    }

    [Fact]
    public async Task ExtractAsync_Dates_DuplicateNumericDate_IsDeduplicated()
    {
        var result = await ExtractAsync(OficioPrefix + "emitido 15/11/2025, recibido 15/11/2025");

        result.ShouldNotBeNull();
        result.Fechas.Count(f => f == "15/11/2025").ShouldBe(1);
    }
}
