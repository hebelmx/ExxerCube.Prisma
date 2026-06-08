namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Adaptive.Strategies;

using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;
using ExxerCube.Prisma.Testing.Abstractions;
using Meziantou.Extensions.Logging.Xunit.v3;

/// <summary>
/// Mutation-killing tests for <see cref="SearchExtractionStrategy"/>.
/// </summary>
/// <remarks>
/// Written against the Stryker.NET survivor map. The existing <c>SearchExtractionStrategyLiskovTests</c> use
/// loose assertions (<c>ShouldContain</c>, <c>if (ContainsKey)</c> guards, confidence only <c>&gt;= 70</c>),
/// leaving the (keywordCount, extractionSuccess) confidence tuple, the CanExtractAsync &gt;=2 boundary, the
/// currency-normalization switch, the <c>amount &gt; 0</c> filter, the CLABE validation / standalone pattern,
/// the bank value extraction, and the exact extended-field values unpinned. These tests pin exact values so
/// the corresponding string/equality/boolean mutants become observable.
/// </remarks>
public sealed class SearchExtractionStrategyMutationKillingTests
{
    private readonly SearchExtractionStrategy _strategy;

    public SearchExtractionStrategyMutationKillingTests(ITestOutputHelper output)
    {
        var logger = XUnitLogger.CreateLogger<SearchExtractionStrategy>(output);
        _strategy = new SearchExtractionStrategy(logger);
    }

    private const string Expediente = "A/AS1-2505-088637-PHM";

    private Task<ExtractedFields?> ExtractAsync(string text)
        => _strategy.ExtractAsync(text, TestContext.Current.CancellationToken);

    private Task<int> ConfidenceAsync(string text)
        => _strategy.GetConfidenceAsync(text, TestContext.Current.CancellationToken);

    private Task<bool> CanExtractAsync(string text)
        => _strategy.CanExtractAsync(text, TestContext.Current.CancellationToken);

    // =====================================================================
    // GetConfidenceAsync tuple switch (L166-172):
    //   (>=5, >=2) => 75, (>=3, >=1) => 65, (>=2, >=1) => 50, _ => 0.
    // PrimaryKeywords (15): expediente oficio aseguramiento precautorio pgr fgr
    // procuraduría fiscalía autoridad causa solicitud cuenta clabe banco monto.
    // extractionSuccess = hasExpediente + hasCausa + hasAccion (0..3).
    // =====================================================================

    [Fact]
    public async Task GetConfidence_FiveKeywordsTwoExtractions_Returns75()
    {
        // keywords: expediente, oficio, banco, monto, cuenta = 5.
        // extraction: expediente + causa("lavado de dinero") = 2 (no accion keyword).
        var confidence = await ConfidenceAsync(
            $"expediente {Expediente} oficio banco monto cuenta investigación por lavado de dinero");

        confidence.ShouldBe(75);
    }

    [Fact]
    public async Task GetConfidence_ThreeKeywordsOneExtraction_Returns65()
    {
        // keywords: expediente, banco, monto = 3. extraction: expediente = 1.
        var confidence = await ConfidenceAsync($"expediente {Expediente} banco monto");

        confidence.ShouldBe(65);
    }

    [Fact]
    public async Task GetConfidence_TwoKeywordsOneExtraction_Returns50()
    {
        // keywords: expediente, banco = 2. extraction: expediente = 1.
        var confidence = await ConfidenceAsync($"expediente {Expediente} banco");

        confidence.ShouldBe(50);
    }

    [Fact]
    public async Task GetConfidence_KeywordsButNoExtraction_ReturnsZero()
    {
        // keywords: oficio, banco = 2 but extraction = 0 -> falls through to _ => 0.
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
    // CanExtractAsync (L139-143): keywordCount >= 2.
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
    // SearchMonetaryAmounts currency switch (L379-387) + amount > 0 (L389).
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

    [Fact]
    public async Task ExtractAsync_Monto_ZeroAmount_IsRejected()
    {
        // amount > 0 (L389): a $0.00 amount must NOT be added. A core field keeps the
        // result non-null. The `> 0` -> `>= 0` mutant would add the zero amount.
        var result = await ExtractAsync($"expediente {Expediente} por importe de $0.00 MXN");

        result.ShouldNotBeNull();
        result.Montos.Count.ShouldBe(0);
    }

    // =====================================================================
    // SearchAccountInformation — CLABE (L402-422) and Bank (L424-441).
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_Clabe_StandaloneEighteenDigits_NoKeyword()
    {
        // No "clabe" keyword -> pattern[0] fails, the standalone pattern[1] (\b\d{18}\b) matches.
        // Pins pattern[1] (L405), the Success/Count guard (L411) and the length-18 validation (L415).
        var result = await ExtractAsync($"expediente {Expediente} ref 012345678901234567 end");

        result.ShouldNotBeNull();
        result.AdditionalFields["CLABE"].ShouldBe("012345678901234567");
    }

    [Fact]
    public async Task ExtractAsync_Bank_ExactName_FromInstitucionPrefix()
    {
        // pattern[0] `(?:banco|institución).{0,20}?(BANAMEX|...)` captures Groups[1]="BANAMEX";
        // match.Value includes the "institución " prefix. Pins the Groups[1] selection (L436)
        // and the "Banco" key (L437) with an exact (not ShouldContain) assertion.
        var result = await ExtractAsync($"expediente {Expediente} institución BANAMEX");

        result.ShouldNotBeNull();
        result.AdditionalFields["Banco"].ShouldBe("BANAMEX");
    }

    // =====================================================================
    // SearchExtendedFields — exact values (L290-328).
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_NumeroOficio_Exact()
    {
        var result = await ExtractAsync("oficio número 214-1-18714972/2025");

        result.ShouldNotBeNull();
        result.AdditionalFields["NumeroOficio"].ShouldBe("214-1-18714972/2025");
    }

    [Fact]
    public async Task ExtractAsync_AutoridadNombre_Exact()
    {
        var result = await ExtractAsync($"expediente {Expediente} autoridad competente PGR");

        result.ShouldNotBeNull();
        result.AdditionalFields["AutoridadNombre"].ShouldBe("PGR");
    }

    [Fact]
    public async Task ExtractAsync_Rfc_UppercasedExact()
    {
        var result = await ExtractAsync($"expediente {Expediente} RFC GALJ850101XXX");

        result.ShouldNotBeNull();
        result.AdditionalFields["RFC"].ShouldBe("GALJ850101XXX");
    }

    // =====================================================================
    // hasAnyData bookkeeping (L70/78/86) + no-data null return (L107).
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
        var result = await ExtractAsync("investigación por lavado de dinero");

        result.ShouldNotBeNull();
        result.Causa.ShouldNotBeNull();
        result.Causa.ShouldContain("lavado de dinero");
    }

    [Fact]
    public async Task ExtractAsync_AccionOnly_ReturnsNonNull()
    {
        var result = await ExtractAsync("se solicita el aseguramiento precautorio");

        result.ShouldNotBeNull();
        result.AccionSolicitada.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExtractAsync_Causa_CleanupStopsAtConforme()
    {
        // The Causa cleanup (L242) strips a trailing "conforme ..." clause. The string-mutation
        // of that cleanup regex would leave "conforme" in the value.
        var result = await ExtractAsync("causa de lavado conforme a derecho aplicable");

        result.ShouldNotBeNull();
        result.Causa.ShouldNotBeNull();
        result.Causa.ShouldNotContain("conforme");
        result.Causa.ShouldContain("lavado");
    }

    [Fact]
    public async Task ExtractAsync_NoExtractableData_ReturnsNull()
    {
        var result = await ExtractAsync("oficio banco solamente aqui");

        result.ShouldBeNull();
    }

    // =====================================================================
    // SearchDates (L330-356): numeric, "de" long format, fecha-proximity, dedup.
    // A core field keeps the result non-null (Fechas are not counted by the guard).
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
