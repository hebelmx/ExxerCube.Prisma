namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Adaptive.Strategies;

using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;
using ExxerCube.Prisma.Testing.Abstractions;
using Meziantou.Extensions.Logging.Xunit.v3;

/// <summary>
/// Mutation-killing tests for <see cref="ComplementExtractionStrategy"/>.
/// </summary>
/// <remarks>
/// Written against the Stryker.NET survivor map. The existing <c>ComplementExtractionStrategyLiskovTests</c>
/// use loose assertions (<c>ShouldContain</c>, <c>if (ContainsKey)</c> guards, confidence only <c>&gt;= 70</c>),
/// leaving the (keywordCount, extractionScore) confidence tuple, the CanExtractAsync &gt;=2 boundary, the
/// currency switch, the Causa/Accion cleanup regexes, and the exact extended-field/account values unpinned.
/// These tests assert exact values so the corresponding string/equality/boolean mutants become observable.
/// </remarks>
public sealed class ComplementExtractionStrategyMutationKillingTests
{
    private readonly ComplementExtractionStrategy _strategy;

    public ComplementExtractionStrategyMutationKillingTests(ITestOutputHelper output)
    {
        var logger = XUnitLogger.CreateLogger<ComplementExtractionStrategy>(output);
        _strategy = new ComplementExtractionStrategy(logger);
    }

    private const string Expediente = "A/AS1-2505-088637-PHM";

    private Task<ExtractedFields?> ExtractAsync(string text)
        => _strategy.ExtractAsync(text, TestContext.Current.CancellationToken);

    private Task<int> ConfidenceAsync(string text)
        => _strategy.GetConfidenceAsync(text, TestContext.Current.CancellationToken);

    private Task<bool> CanExtractAsync(string text)
        => _strategy.CanExtractAsync(text, TestContext.Current.CancellationToken);

    // =====================================================================
    // GetConfidenceAsync tuple switch (L164-170):
    //   (>=6, >=2) => 85, (>=4, >=2) => 75, (>=2, >=1) => 60, _ => 0.
    // AssessmentKeywords (14): expediente oficio autoridad causa solicitud
    // aseguramiento precautorio cuenta clabe banco monto fecha procuraduría pgr.
    // extractionScore = hasExpediente + hasCausa + hasAccion (0..3).
    // =====================================================================

    [Fact]
    public async Task GetConfidence_SixKeywordsTwoExtractions_Returns85()
    {
        // keywords: expediente, oficio, banco, monto, causa, fecha = 6.
        // extraction: expediente + causa("de lavado de dinero") = 2.
        var confidence = await ConfidenceAsync(
            $"expediente {Expediente} oficio banco monto causa de lavado de dinero fecha");

        confidence.ShouldBe(85);
    }

    [Fact]
    public async Task GetConfidence_FourKeywordsTwoExtractions_Returns75()
    {
        // keywords: expediente, banco, monto, causa = 4. extraction: expediente + causa = 2.
        var confidence = await ConfidenceAsync($"expediente {Expediente} banco monto causa de fraude");

        confidence.ShouldBe(75);
    }

    [Fact]
    public async Task GetConfidence_TwoKeywordsOneExtraction_Returns60()
    {
        // keywords: expediente, banco = 2. extraction: expediente = 1.
        var confidence = await ConfidenceAsync($"expediente {Expediente} banco");

        confidence.ShouldBe(60);
    }

    [Fact]
    public async Task GetConfidence_KeywordsButNoExtraction_ReturnsZero()
    {
        // keywords: oficio, banco = 2 but extraction = 0 -> _ => 0.
        var confidence = await ConfidenceAsync("oficio banco solamente aqui");

        confidence.ShouldBe(0);
    }

    [Fact]
    public async Task GetConfidence_NoKeywords_ReturnsZero()
    {
        var confidence = await ConfidenceAsync("plain prose with nothing relevant at all");

        confidence.ShouldBe(0);
    }

    [Fact]
    public async Task GetConfidence_Whitespace_ReturnsZero()
    {
        var confidence = await ConfidenceAsync("   ");

        confidence.ShouldBe(0);
    }

    // =====================================================================
    // CanExtractAsync (L138-141): keywordCount >= 2.
    // =====================================================================

    [Fact]
    public async Task CanExtract_OneKeyword_ReturnsFalse()
    {
        var canExtract = await CanExtractAsync("expediente solo");

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
    // ExtractMonetaryAmounts currency switch (L363-371).
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_Monto_Mxn()
    {
        var result = await ExtractAsync("$1,234.56 MXN");

        result.ShouldNotBeNull();
        result.Montos[0].Currency.ShouldBe("MXN");
        result.Montos[0].Value.ShouldBe(1234.56m);
    }

    [Fact]
    public async Task ExtractAsync_Monto_ModenaNacional_NormalizesToMxn()
    {
        var result = await ExtractAsync("$500.00 M.N.");

        result.ShouldNotBeNull();
        result.Montos[0].Currency.ShouldBe("MXN");
    }

    [Fact]
    public async Task ExtractAsync_Monto_Pesos_NormalizesToMxn()
    {
        var result = await ExtractAsync("$500.00 pesos");

        result.ShouldNotBeNull();
        result.Montos[0].Currency.ShouldBe("MXN");
    }

    [Fact]
    public async Task ExtractAsync_Monto_Usd()
    {
        var result = await ExtractAsync("$500.00 USD");

        result.ShouldNotBeNull();
        result.Montos[0].Currency.ShouldBe("USD");
    }

    [Fact]
    public async Task ExtractAsync_Monto_Eur()
    {
        var result = await ExtractAsync("$500.00 EUR");

        result.ShouldNotBeNull();
        result.Montos[0].Currency.ShouldBe("EUR");
    }

    [Fact]
    public async Task ExtractAsync_Monto_NoCurrency_DefaultsToMxn()
    {
        var result = await ExtractAsync("$750.00 aprox");

        result.ShouldNotBeNull();
        result.Montos[0].Currency.ShouldBe("MXN");
        result.Montos[0].Value.ShouldBe(750.00m);
    }

    // =====================================================================
    // Extended fields + account info exact values (L271-420).
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_NumeroOficio_Exact()
    {
        var result = await ExtractAsync("Oficio: 214-1-18714972/2025");

        result.ShouldNotBeNull();
        result.AdditionalFields["NumeroOficio"].ShouldBe("214-1-18714972/2025");
    }

    [Fact]
    public async Task ExtractAsync_AutoridadNombre_Exact()
    {
        var result = await ExtractAsync("Autoridad emisora PGR");

        result.ShouldNotBeNull();
        result.AdditionalFields["AutoridadNombre"].ShouldBe("PGR");
    }

    [Fact]
    public async Task ExtractAsync_Rfc_UppercasedExact()
    {
        var result = await ExtractAsync("RFC: galj850101xxx");

        result.ShouldNotBeNull();
        result.AdditionalFields["RFC"].ShouldBe("GALJ850101XXX");
    }

    [Fact]
    public async Task ExtractAsync_Clabe_StandaloneEighteenDigits_NoKeyword()
    {
        var result = await ExtractAsync($"expediente {Expediente} ref 012345678901234567 end");

        result.ShouldNotBeNull();
        result.AdditionalFields["CLABE"].ShouldBe("012345678901234567");
    }

    [Fact]
    public async Task ExtractAsync_Banco_LabeledExact()
    {
        var result = await ExtractAsync("Banco: BANAMEX");

        result.ShouldNotBeNull();
        result.AdditionalFields["Banco"].ShouldBe("BANAMEX");
    }

    // =====================================================================
    // hasAnyData bookkeeping (L69/77/85) + no-data null return (L106).
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_ExpedienteOnly_ReturnsNonNull()
    {
        var result = await ExtractAsync($"expediente {Expediente}");

        result.ShouldNotBeNull();
        result.Expediente.ShouldBe(Expediente);
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
        var result = await ExtractAsync("se solicita el aseguramiento precautorio inmediato");

        result.ShouldNotBeNull();
        result.AccionSolicitada.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExtractAsync_NoExtractableData_ReturnsNull()
    {
        // Keywords present (oficio, cuenta) but nothing extractable: no oficio number, no "Banco"
        // label, no authority, no CLABE, no monto, no core field -> the no-data guard returns null.
        var result = await ExtractAsync("oficio cuenta sin datos relevantes");

        result.ShouldBeNull();
    }

    // =====================================================================
    // Cleanup regexes: Causa "conforme" (L233), Accion "de la cuenta" / word stops (L262-263).
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_Causa_CleanupStopsAtConforme()
    {
        var result = await ExtractAsync("causa de lavado conforme a derecho aplicable");

        result.ShouldNotBeNull();
        result.Causa.ShouldNotBeNull();
        result.Causa.ShouldNotContain("conforme");
        result.Causa.ShouldContain("lavado");
    }

    [Fact]
    public async Task ExtractAsync_Accion_CleanupStopsAtDeLaCuenta()
    {
        // "bloqueo" has no "en"/"con" substring, so the L263 word-stop cleanup does not further
        // truncate it; this isolates the "de la cuenta" cleanup (L262).
        var result = await ExtractAsync("se solicita el bloqueo de la cuenta bancaria");

        result.ShouldNotBeNull();
        result.AccionSolicitada.ShouldNotBeNull();
        result.AccionSolicitada.ShouldNotContain("cuenta");
        result.AccionSolicitada.ShouldContain("bloqueo");
    }

    // =====================================================================
    // ExtractDates (L321-347): numeric, "de" long format, dedup.
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_Dates_NumericFormat()
    {
        var result = await ExtractAsync($"expediente {Expediente} 15/11/2025");

        result.ShouldNotBeNull();
        result.Fechas.ShouldContain("15/11/2025");
    }

    [Fact]
    public async Task ExtractAsync_Dates_LongDeFormat()
    {
        var result = await ExtractAsync($"expediente {Expediente} 15 de noviembre de 2025");

        result.ShouldNotBeNull();
        result.Fechas.ShouldContain("15 de noviembre de 2025");
    }

    [Fact]
    public async Task ExtractAsync_Dates_DuplicateNumericDate_IsDeduplicated()
    {
        var result = await ExtractAsync($"expediente {Expediente} 15/11/2025 y 15/11/2025");

        result.ShouldNotBeNull();
        result.Fechas.Count(f => f == "15/11/2025").ShouldBe(1);
    }
}
