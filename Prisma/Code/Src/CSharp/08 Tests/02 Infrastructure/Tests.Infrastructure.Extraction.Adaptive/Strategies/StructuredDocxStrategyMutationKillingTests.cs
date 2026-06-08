namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Adaptive.Strategies;

using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;
using ExxerCube.Prisma.Testing.Abstractions;
using Meziantou.Extensions.Logging.Xunit.v3;

/// <summary>
/// Mutation-killing tests for <see cref="StructuredDocxStrategy"/>.
/// </summary>
/// <remarks>
/// Written against the Stryker.NET survivor map (baseline 94.97%). The existing
/// <c>StructuredDocxStrategyLiskovTests</c> already pin most exact values via one rich sample document,
/// so the gaps are narrow: the confidence ladder is only exercised at its 90 (3+ labels) and 0 (no labels)
/// rungs — never at 1 label (50) or 2 labels (75) — the <see cref="StructuredDocxStrategy.CanExtractAsync"/>
/// whitespace branch is unpinned, and the monetary default-currency arm ("MXN" when the regex captures no
/// currency token) is never hit because the sample always spells out "MXN". These tests pin those exact
/// values plus a handful of additional extractor branches (alternate currencies, the "de" date format, date
/// dedup, a standalone CLABE) so the corresponding string/equality/boolean mutants become observable.
/// </remarks>
public sealed class StructuredDocxStrategyMutationKillingTests
{
    private readonly StructuredDocxStrategy _strategy;

    public StructuredDocxStrategyMutationKillingTests(ITestOutputHelper output)
    {
        var logger = XUnitLogger.CreateLogger<StructuredDocxStrategy>(output);
        _strategy = new StructuredDocxStrategy(logger);
    }

    private Task<ExtractedFields?> ExtractAsync(string text)
        => _strategy.ExtractAsync(text, TestContext.Current.CancellationToken);

    private Task<int> ConfidenceAsync(string text)
        => _strategy.GetConfidenceAsync(text, TestContext.Current.CancellationToken);

    // =====================================================================
    // Confidence ladder (L166-172): labelCount switch { >=3 => 90, 2 => 75, 1 => 50, _ => 0 }.
    // The Liskov tests only cover the 90 and 0 rungs. These pin the 1/2/3 boundaries.
    // =====================================================================

    [Fact]
    public async Task GetConfidence_OneStandardLabel_Returns50()
    {
        var confidence = await ConfidenceAsync("Causa: Lavado de dinero");

        confidence.ShouldBe(50);
    }

    [Fact]
    public async Task GetConfidence_TwoStandardLabels_Returns75()
    {
        var confidence = await ConfidenceAsync("Causa: x\nAutoridad: PGR");

        confidence.ShouldBe(75);
    }

    [Fact]
    public async Task GetConfidence_ThreeStandardLabels_Returns90()
    {
        var confidence = await ConfidenceAsync("Causa: x\nAutoridad: PGR\nOficio: 1");

        confidence.ShouldBe(90);
    }

    [Fact]
    public async Task GetConfidence_FourStandardLabels_Returns90_StaysAtCeiling()
    {
        // Pins the >=3 (not ==3) lower bound: a 4th label must still yield 90.
        var confidence = await ConfidenceAsync("Causa: x\nAutoridad: PGR\nOficio: 1\nExpediente: E");

        confidence.ShouldBe(90);
    }

    [Fact]
    public async Task GetConfidence_NoStandardLabels_ReturnsZero()
    {
        var confidence = await ConfidenceAsync("plain prose with no labels at all");

        confidence.ShouldBe(0);
    }

    [Fact]
    public async Task GetConfidence_Whitespace_ReturnsZero()
    {
        // Covers the IsNullOrWhiteSpace early-return branch (L156-159).
        var confidence = await ConfidenceAsync("   ");

        confidence.ShouldBe(0);
    }

    // =====================================================================
    // CanExtractAsync whitespace branch (L141-143): must be false.
    // =====================================================================

    [Fact]
    public async Task CanExtract_Whitespace_ReturnsFalse()
    {
        var canExtract = await _strategy.CanExtractAsync("   ", TestContext.Current.CancellationToken);

        canExtract.ShouldBeFalse();
    }

    [Fact]
    public async Task CanExtract_StandardLabelPresent_ReturnsTrue()
    {
        var canExtract = await _strategy.CanExtractAsync("Oficio: 214-1-18714972/2025", TestContext.Current.CancellationToken);

        canExtract.ShouldBeTrue();
    }

    // =====================================================================
    // ExtractMonetaryAmounts currency handling (L351-361).
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_Monto_NoCurrencyToken_DefaultsToMxn()
    {
        // Default-currency arm (L361): regex captures no currency group.
        var result = await ExtractAsync("Oficio: 1\nMonto: $500.00");

        result.ShouldNotBeNull();
        result.Montos.ShouldContain(m => m.Currency == "MXN" && m.Value == 500.00m);
    }

    [Fact]
    public async Task ExtractAsync_Monto_Usd_PreservesCurrency()
    {
        var result = await ExtractAsync("Oficio: 1\nMonto: $5,000.00 USD");

        result.ShouldNotBeNull();
        result.Montos.ShouldContain(m => m.Currency == "USD" && m.Value == 5000.00m);
    }

    [Fact]
    public async Task ExtractAsync_Monto_Cad_PreservesCurrency()
    {
        var result = await ExtractAsync("Oficio: 1\nMonto: $250.00 CAD");

        result.ShouldNotBeNull();
        result.Montos.ShouldContain(m => m.Currency == "CAD");
    }

    [Fact]
    public async Task ExtractAsync_Monto_StripsThousandSeparators()
    {
        var result = await ExtractAsync("Oficio: 1\nMonto: $1,234,567.00 MXN");

        result.ShouldNotBeNull();
        result.Montos.ShouldContain(m => m.Value == 1_234_567.00m);
    }

    // =====================================================================
    // ExtractExpediente — uppercased, both labeled and bare patterns (L184-204).
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_Expediente_IsUppercased()
    {
        var result = await ExtractAsync("Expediente No.: a/as1-2505-088637-phm");

        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
    }

    [Fact]
    public async Task ExtractAsync_Expediente_BarePatternWithoutLabel()
    {
        // The second pattern matches a bare expediente code with no "Expediente" prefix.
        // Format is Letter/Letters+Digit-Digits-Digits-Letters (e.g. AS1, not AS-1).
        var result = await ExtractAsync("Reference X/YZ9-1111-222222-QRS in the file");

        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("X/YZ9-1111-222222-QRS");
    }

    // =====================================================================
    // ExtractDates (L317-342): "de" long format + dedup.
    // =====================================================================

    // A core field (Expediente) keeps the result non-null: the Fecha collection is not counted
    // by the "no data extracted" guard, so a date-only document would otherwise return null.
    private const string ExpedientePrefix = "Expediente No.: A/AS1-2505-088637-PHM\n";

    [Fact]
    public async Task ExtractAsync_Dates_LongDeFormat()
    {
        var result = await ExtractAsync(ExpedientePrefix + "Fecha: 15 de noviembre de 2025");

        result.ShouldNotBeNull();
        result.Fechas.ShouldContain("15 de noviembre de 2025");
    }

    [Fact]
    public async Task ExtractAsync_Dates_NumericFormat()
    {
        var result = await ExtractAsync(ExpedientePrefix + "Fecha: 15/11/2025");

        result.ShouldNotBeNull();
        result.Fechas.ShouldContain("15/11/2025");
    }

    [Fact]
    public async Task ExtractAsync_Dates_DuplicateNumericDate_IsDeduplicated()
    {
        var result = await ExtractAsync(ExpedientePrefix + "Emitido 15/11/2025, recibido 15/11/2025");

        result.ShouldNotBeNull();
        result.Fechas.Count(f => f == "15/11/2025").ShouldBe(1);
    }

    // =====================================================================
    // ExtractAccountInformation (L424-451): standalone CLABE / account / bank.
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_Clabe_EighteenDigits()
    {
        var result = await ExtractAsync("Oficio: 1\nCLABE 012345678901234567 referenced");

        result.ShouldNotBeNull();
        result.AdditionalFields["CLABE"].ShouldBe("012345678901234567");
    }

    [Fact]
    public async Task ExtractAsync_Banco_IsUppercased()
    {
        var result = await ExtractAsync("Oficio: 1\nBanco: banamex\n");

        result.ShouldNotBeNull();
        result.AdditionalFields["Banco"].ShouldBe("BANAMEX");
    }

    [Fact]
    public async Task ExtractAsync_NumeroCuenta_ExactKeyAndValue()
    {
        // The Liskov test asserts NumeroCuenta only inside an `if (ContainsKey)` guard,
        // so the account regex (L436), the "NumeroCuenta" key (L440) and the Success/Count
        // guard (L438) are never pinned unguarded.
        var result = await ExtractAsync("Cuenta: 1234567890123456");

        result.ShouldNotBeNull();
        result.AdditionalFields.ShouldContainKey("NumeroCuenta");
        result.AdditionalFields["NumeroCuenta"].ShouldBe("1234567890123456");
    }

    // =====================================================================
    // ExtractMexicanNames bare pattern (L385-417): pattern[1] is reached only
    // when there is no "Nombre:" label (pattern[0] requires the literal label).
    // NOTE: the method assigns nombre=Groups[1], paterno=Groups[2], materno=Groups[3]
    // for BOTH patterns, but pattern[1] captures in (caps)(caps)(MixedCase) order — so
    // the named parts are positionally assigned, not semantically. This test pins the
    // ACTUAL current behavior (it documents, not endorses, the assignment). The two
    // exactly-3-char captures also pin the `paterno.Length >= 3 && materno.Length >= 3`
    // lower bound (kills the >=3 -> >3 mutants).
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_MexicanName_BarePatternWithoutNombreLabel()
    {
        // "ABC DEF Abc" -> Groups[1]="ABC", Groups[2]="DEF", Groups[3]="Abc".
        var result = await ExtractAsync("Oficio: 1\nABC DEF Abc found here");

        result.ShouldNotBeNull();
        result.AdditionalFields["Nombre"].ShouldBe("ABC");      // Groups[1], not uppercased
        result.AdditionalFields["Paterno"].ShouldBe("DEF");     // Groups[2], uppercased
        result.AdditionalFields["Materno"].ShouldBe("ABC");     // Groups[3], uppercased
        result.AdditionalFields["NombreCompleto"].ShouldBe("ABC DEF ABC");
    }

    // =====================================================================
    // hasAnyData bookkeeping (L72-93): a single core field returns non-null.
    // =====================================================================

    [Fact]
    public async Task ExtractAsync_ExpedienteOnly_ReturnsNonNull()
    {
        var result = await ExtractAsync("Expediente No.: A/AS1-2505-088637-PHM");

        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
    }

    [Fact]
    public async Task ExtractAsync_CausaOnly_ReturnsNonNull()
    {
        var result = await ExtractAsync("Causa: Lavado de dinero");

        result.ShouldNotBeNull();
        result.Causa.ShouldBe("Lavado de dinero");
    }

    [Fact]
    public async Task ExtractAsync_AccionSolicitadaOnly_ReturnsNonNull()
    {
        // Pins the hasAnyData = true bookkeeping in the Accion branch (L91).
        var result = await ExtractAsync("Acción Solicitada: Aseguramiento precautorio");

        result.ShouldNotBeNull();
        result.AccionSolicitada.ShouldBe("Aseguramiento precautorio");
    }

    [Fact]
    public async Task ExtractAsync_MexicanName_ShortMaterno_IsRejected()
    {
        // grp3 (-> materno) is only 2 chars, so `paterno.Length >= 3 && materno.Length >= 3`
        // is false and no name is set. The `&&` -> `||` mutant (L404) would accept it.
        // A real Oficio keeps the document non-null.
        var result = await ExtractAsync("Oficio: 214-1-18714972/2025\nABC DEF Ab end");

        result.ShouldNotBeNull();
        result.AdditionalFields.ContainsKey("Paterno").ShouldBeFalse();
    }
}
