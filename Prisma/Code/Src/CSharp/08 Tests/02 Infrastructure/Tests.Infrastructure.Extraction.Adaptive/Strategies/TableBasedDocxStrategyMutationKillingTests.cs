namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Adaptive.Strategies;

using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;
using ExxerCube.Prisma.Testing.Abstractions;
using Meziantou.Extensions.Logging.Xunit.v3;

/// <summary>
/// Mutation-killing tests for <see cref="TableBasedDocxStrategy"/>.
/// </summary>
/// <remarks>
/// Written against the Stryker.NET survivor map (baseline 63.19%: Survived 45, NoCoverage 15). The existing
/// <c>TableBasedDocxStrategyLiskovTests</c> verify the <see cref="IAdaptiveDocxStrategy"/> contract but use
/// loose assertions — <c>if (AdditionalFields.ContainsKey(...))</c> guards, <c>ShouldBeGreaterThan(0)</c>,
/// and confidence ranges — so the field-mapping values, the confidence ladder (95/85/60/0) and its boundary
/// comparisons, the currency-normalization switch, the <c>hasAnyData</c> bookkeeping, and the <c>Fecha</c>
/// dedup are never pinned to exact values. These tests assert exact values so each string/equality/boolean/
/// logical mutant becomes observable.
/// </remarks>
public sealed class TableBasedDocxStrategyMutationKillingTests
{
    private readonly TableBasedDocxStrategy _strategy;

    public TableBasedDocxStrategyMutationKillingTests(ITestOutputHelper output)
    {
        var logger = XUnitLogger.CreateLogger<TableBasedDocxStrategy>(output);
        _strategy = new TableBasedDocxStrategy(logger);
    }

    private Task<ExtractedFields?> ExtractAsync(string text)
        => _strategy.ExtractAsync(text, TestContext.Current.CancellationToken);

    // =====================================================================
    // FieldMappings — exact value mapping (L30-45).
    // The Liskov tests guard every extended field with `if (ContainsKey)`, so
    // a mutation that breaks the mapping (key or mapped value -> "") survives.
    // These assert the exact destination field/value with no guard.
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_MapsExtendedFields_ToExactAdditionalFieldKeys()
    {
        const string docx =
            "| Oficio    | 214-1-18714972/2025 |\n" +
            "| Autoridad | PGR                 |\n" +
            "| RFC       | GALJ850101XXX       |\n" +
            "| CLABE     | 012345678901234567  |\n" +
            "| Banco     | BANAMEX             |\n" +
            "| Nombre    | Juan Carlos GARCIA  |";

        var result = await ExtractAsync(docx);

        result.ShouldNotBeNull();
        // Oficio -> NumeroOficio (L37)
        result.AdditionalFields["NumeroOficio"].ShouldBe("214-1-18714972/2025");
        // Autoridad -> AutoridadNombre (L39)
        result.AdditionalFields["AutoridadNombre"].ShouldBe("PGR");
        // RFC -> RFC (L40)
        result.AdditionalFields["RFC"].ShouldBe("GALJ850101XXX");
        // CLABE -> CLABE (L41)
        result.AdditionalFields["CLABE"].ShouldBe("012345678901234567");
        // Banco -> Banco (L42)
        result.AdditionalFields["Banco"].ShouldBe("BANAMEX");
        // Nombre -> NombreCompleto (L43)
        result.AdditionalFields["NombreCompleto"].ShouldBe("Juan Carlos GARCIA");
    }

    [Fact]
    public async Task ExtractAsync_MapsAlternateKeys_ToCoreFields()
    {
        // Exercises the alternate-key rows the Liskov sample never uses:
        // "No. Expediente" (L31) and "Causa Legal" (L34).
        const string docx =
            "| No. Expediente | EXP-ALT-1 |\n" +
            "| Causa Legal    | CL-ALT-1  |";

        var result = await ExtractAsync(docx);

        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("EXP-ALT-1");
        result.Causa.ShouldBe("CL-ALT-1");
    }

    [Fact]
    public async Task ExtractAsync_MapsNumeroDeExpedienteAndNoOficio_AlternateKeys()
    {
        const string docx =
            "| Número de Expediente | EXP-ALT-2 |\n" +
            "| No. Oficio           | OFI-ALT-2 |";

        var result = await ExtractAsync(docx);

        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("EXP-ALT-2");
        result.AdditionalFields["NumeroOficio"].ShouldBe("OFI-ALT-2");
    }

    [Fact]
    public async Task ExtractAsync_MapsFechaKey_AddsToFechas()
    {
        // "Fecha" -> "Fecha" mapped value (L45); Liskov never asserts Fechas.
        var result = await ExtractAsync("| Fecha | 15/11/2025 |\n| Expediente | E1 |");

        result.ShouldNotBeNull();
        result.Fechas.ShouldContain("15/11/2025");
    }

    // =====================================================================
    // hasAnyData bookkeeping (L97, L103, L109).
    // A single core field with no AdditionalFields/Montos must still return a
    // non-null result. If `hasAnyData = true` is mutated to `false`, the
    // "no data extracted" guard (L137) fires and returns null instead.
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_ExpedienteOnly_ReturnsNonNull()
    {
        var result = await ExtractAsync("| Expediente | EXP-ONLY |");

        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("EXP-ONLY");
    }

    [Fact]
    public async Task ExtractAsync_CausaOnly_ReturnsNonNull()
    {
        var result = await ExtractAsync("| Causa | CAUSA-ONLY |");

        result.ShouldNotBeNull();
        result.Causa.ShouldBe("CAUSA-ONLY");
    }

    [Fact]
    public async Task ExtractAsync_AccionSolicitadaOnly_ReturnsNonNull()
    {
        var result = await ExtractAsync("| Acción Solicitada | ACC-ONLY |");

        result.ShouldNotBeNull();
        result.AccionSolicitada.ShouldBe("ACC-ONLY");
    }

    [Fact]
    public async Task ExtractAsync_OnlyUnmappedKeys_ReturnsNull()
    {
        // Table rows exist (so ParseTableRows returns non-empty), but no key maps
        // to a domain field: hasAnyData stays false, AdditionalFields/Montos empty
        // -> the "no data extracted" branch (L137-140) returns null.
        var result = await ExtractAsync("| Foo | Bar |\n| Baz | Qux |");

        result.ShouldBeNull();
    }

    // =====================================================================
    // Fecha dedup (L118-120): `if (!Fechas.Contains(value)) Fechas.Add(value)`.
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_DuplicateFecha_IsDeduplicated()
    {
        // A core field (Expediente) is included because a Fecha-only document hits the
        // "no data extracted" guard (the Fecha case sets neither hasAnyData nor a counted
        // collection). The dedup logic (L118) is still exercised by the two Fecha rows.
        var result = await ExtractAsync("| Expediente | E1 |\n| Fecha | 01/01/2025 |\n| Fecha | 01/01/2025 |");

        result.ShouldNotBeNull();
        result.Fechas.Count.ShouldBe(1);
        result.Fechas.ShouldContain("01/01/2025");
    }

    [Fact]
    public async Task ExtractAsync_DistinctFechas_AreBothKept()
    {
        var result = await ExtractAsync("| Expediente | E1 |\n| Fecha | 01/01/2025 |\n| Fecha | 02/02/2025 |");

        result.ShouldNotBeNull();
        result.Fechas.Count.ShouldBe(2);
    }

    // =====================================================================
    // ExtractMonetaryAmount currency normalization switch (L267-277).
    // The regex only ever captures one of {MXN, USD, EUR, M.N., pesos, ""}.
    // Each arm's result string is pinned so the "" / StartsWith mutations die.
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_Monto_Mxn_NormalizesToMxn()
    {
        var result = await ExtractAsync("| Monto | $1,234.56 MXN |");

        result.ShouldNotBeNull();
        result.Montos.Count.ShouldBe(1);
        result.Montos[0].Currency.ShouldBe("MXN");
        result.Montos[0].Value.ShouldBe(1234.56m);
        result.Montos[0].OriginalText.ShouldContain("1,234.56");
    }

    [Fact]
    public async Task ExtractAsync_Monto_Moneda_Nacional_NormalizesToMxn()
    {
        // "M.N." arm (L272).
        var result = await ExtractAsync("| Monto | $500.00 M.N. |");

        result.ShouldNotBeNull();
        result.Montos[0].Currency.ShouldBe("MXN");
        result.Montos[0].Value.ShouldBe(500.00m);
    }

    [Fact]
    public async Task ExtractAsync_Monto_Pesos_NormalizesToMxn()
    {
        // "pesos" arm (L273).
        var result = await ExtractAsync("| Monto | $500.00 pesos |");

        result.ShouldNotBeNull();
        result.Montos[0].Currency.ShouldBe("MXN");
    }

    [Fact]
    public async Task ExtractAsync_Monto_Usd_NormalizesToUsd()
    {
        // StartsWith("USD") arm (L274) — a non-MXN currency proves the arm is real.
        var result = await ExtractAsync("| Monto | $500.00 USD |");

        result.ShouldNotBeNull();
        result.Montos[0].Currency.ShouldBe("USD");
    }

    [Fact]
    public async Task ExtractAsync_Monto_Eur_NormalizesToEur()
    {
        // StartsWith("EUR") arm (L275).
        var result = await ExtractAsync("| Monto | $500.00 EUR |");

        result.ShouldNotBeNull();
        result.Montos[0].Currency.ShouldBe("EUR");
    }

    [Fact]
    public async Task ExtractAsync_Monto_NoCurrency_DefaultsToMxn()
    {
        // Default arm (L277): regex captures no currency token.
        var result = await ExtractAsync("| Monto | $750.00 |");

        result.ShouldNotBeNull();
        result.Montos[0].Currency.ShouldBe("MXN");
        result.Montos[0].Value.ShouldBe(750.00m);
    }

    [Fact]
    public async Task ExtractAsync_Monto_StripsThousandSeparators()
    {
        var result = await ExtractAsync("| Monto | $1,000,000.00 MXN |");

        result.ShouldNotBeNull();
        result.Montos[0].Value.ShouldBe(1_000_000.00m);
    }

    // =====================================================================
    // GetConfidenceAsync ladder (L184-205) — exact boundary values so the
    // `>=`/`>` equality mutants and the `&&`/`||` logical mutants die.
    // =====================================================================

    [Fact]
    public async Task GetConfidence_StrongTable_Returns95_AtExactly20Pipes()
    {
        // 20 pipes (5+5+5+3+2), header line(s) present, separator line present.
        const string docx =
            "| a | b | c | d |\n" +
            "| a | b | c | d |\n" +
            "| a | b | c | d |\n" +
            "|----|----|\n" +
            "| x |";

        var confidence = await _strategy.GetConfidenceAsync(docx, TestContext.Current.CancellationToken);

        // Pins: pipeCount>=20 boundary (kills `>20`) and the 95 literal.
        confidence.ShouldBe(95);
    }

    [Fact]
    public async Task GetConfidence_ModerateTable_Returns85_AtExactly10Pipes()
    {
        // 10 pipes (5+5), header present, NO separator -> 85 (not 95).
        const string docx =
            "| a | b | c | d |\n" +
            "| e | f | g | h |";

        var confidence = await _strategy.GetConfidenceAsync(docx, TestContext.Current.CancellationToken);

        // Pins: pipeCount>=10 boundary (kills `>10`) and the 85 literal.
        confidence.ShouldBe(85);
    }

    [Fact]
    public async Task GetConfidence_HeaderAndSeparatorButFewPipes_Returns85_NotPromotedTo95()
    {
        // 13 pipes (<20) but header + separator present. With the real `&&` chain
        // this is 85. The `&&`->`||` mutants on L190 would promote it to 95.
        const string docx =
            "| a | b | c | d |\n" +
            "| e | f | g | h |\n" +
            "|---|---|";

        var confidence = await _strategy.GetConfidenceAsync(docx, TestContext.Current.CancellationToken);

        confidence.ShouldBe(85);
    }

    [Fact]
    public async Task GetConfidence_ManyPipesButNoHeaderLine_Returns60_NotPromotedTo85()
    {
        // 10 pipes spread two-per-line (no line has 3 pipes -> hasTableHeader=false),
        // no separator. Real -> 60. The hasTableHeader regex->"" mutant (L185) would
        // make every line a "header" and promote this to 85.
        const string docx = "a|b|c\na|b|c\na|b|c\na|b|c\na|b|c";

        var confidence = await _strategy.GetConfidenceAsync(docx, TestContext.Current.CancellationToken);

        confidence.ShouldBe(60);
    }

    [Fact]
    public async Task GetConfidence_MinimalTable_Returns60_AtExactly4Pipes()
    {
        // 4 pipes, header present, no separator -> 60.
        var confidence = await _strategy.GetConfidenceAsync("| a | b | c |", TestContext.Current.CancellationToken);

        // Pins: pipeCount>=4 boundary (kills `>4`) and the 60 literal.
        confidence.ShouldBe(60);
    }

    [Fact]
    public async Task GetConfidence_BelowMinimum_ReturnsZero_AtThreePipes()
    {
        // 3 pipes (< 4) -> 0.
        var confidence = await _strategy.GetConfidenceAsync("| a | b |", TestContext.Current.CancellationToken);

        confidence.ShouldBe(0);
    }

    [Fact]
    public async Task GetConfidence_Whitespace_ReturnsZero()
    {
        // Covers the IsNullOrWhiteSpace early-return branch (L178-181).
        var confidence = await _strategy.GetConfidenceAsync("   ", TestContext.Current.CancellationToken);

        confidence.ShouldBe(0);
    }

    // =====================================================================
    // CanExtractAsync whitespace branch (L163-165): must be false.
    // =====================================================================

    [Fact]
    public async Task CanExtract_Whitespace_ReturnsFalse()
    {
        var canExtract = await _strategy.CanExtractAsync("   ", TestContext.Current.CancellationToken);

        canExtract.ShouldBeFalse();
    }

    [Fact]
    public async Task CanExtract_PipeDelimited_ReturnsTrue()
    {
        var canExtract = await _strategy.CanExtractAsync("| a | b |", TestContext.Current.CancellationToken);

        canExtract.ShouldBeTrue();
    }
}
