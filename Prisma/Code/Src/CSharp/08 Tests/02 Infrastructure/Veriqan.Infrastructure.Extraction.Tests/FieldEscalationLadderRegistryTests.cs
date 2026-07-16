using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Unit tests for <see cref="FieldEscalationLadderRegistry"/>'s default ladder table (E2.2:
/// <see cref="FieldKind.PaymentDueDate"/> is the first field with a non-empty ladder; E7.S7.2/S7.3
/// adds <see cref="FieldKind.Product"/>; B2 gives Tasa/Cat and the 11 RESUMEN/NIVEL/DESGLOSE money
/// fields an empty-rung ladder carrying a plausibility validator).
/// </summary>
public sealed class FieldEscalationLadderRegistryTests
{
    private readonly FieldEscalationLadderRegistry _registry = new();

    /// <summary>The 11 RESUMEN/NIVEL/DESGLOSE money fields B2 gives a <see cref="MoneyMagnitudeValidator"/>.</summary>
    private static readonly FieldKind[] MoneyFields =
    [
        FieldKind.AdeudoPeriodoAnterior,
        FieldKind.CargosRegularesNoMeses,
        FieldKind.CargosComprasAMesesCapital,
        FieldKind.MontoIntereses,
        FieldKind.MontoComisiones,
        FieldKind.IvaInteresesYComisiones,
        FieldKind.PagosYAbonos,
        FieldKind.SaldoCargosRegulares,
        FieldKind.SaldoCargosAMeses,
        FieldKind.TotalCargos,
        FieldKind.TotalAbonos,
    ];

    [Fact]
    public void GetLadder_PaymentDueDate_HasOneFuzzyLabelStatusGateRung()
    {
        var ladder = _registry.GetLadder(FieldKind.PaymentDueDate);

        ladder.Rungs.Count.ShouldBe(1);
        ladder.Rungs[0].Stage.ShouldBe(StageId.FuzzyLabel);
        ladder.Rungs[0].Trigger.ShouldBe(EscalationTrigger.StatusGate);
        ladder.Validator.ShouldBeOfType<PaymentDueDatePlausibilityValidator>();
        ladder.ConfidenceFloor.ShouldBe(0.0);
    }

    [Fact]
    public void GetLadder_Product_HasOneHeaderImageOcrStatusGateRung()
    {
        var ladder = _registry.GetLadder(FieldKind.Product);

        ladder.Rungs.Count.ShouldBe(1);
        ladder.Rungs[0].Stage.ShouldBe(StageId.HeaderImageOcr);
        ladder.Rungs[0].Trigger.ShouldBe(EscalationTrigger.StatusGate);
        ladder.Validator.ShouldBeNull();
        ladder.ConfidenceFloor.ShouldBe(0.0);
    }

    [Fact]
    public void GetLadder_Tasa_EmptyRungsWithTasaPlausibilityValidator()
    {
        var ladder = _registry.GetLadder(FieldKind.Tasa);

        ladder.Rungs.ShouldBeEmpty();
        ladder.Validator.ShouldBeOfType<TasaPlausibilityValidator>();
        ladder.ConfidenceFloor.ShouldBe(0.0);
    }

    [Fact]
    public void GetLadder_Cat_EmptyRungsWithCatPlausibilityValidator()
    {
        var ladder = _registry.GetLadder(FieldKind.Cat);

        ladder.Rungs.ShouldBeEmpty();
        ladder.Validator.ShouldBeOfType<CatPlausibilityValidator>();
        ladder.ConfidenceFloor.ShouldBe(0.0);
    }

    [Theory]
    [MemberData(nameof(MoneyFieldsData))]
    public void GetLadder_MoneyField_EmptyRungsWithMoneyMagnitudeValidator(FieldKind fieldKind)
    {
        var ladder = _registry.GetLadder(fieldKind);

        ladder.Rungs.ShouldBeEmpty();
        ladder.Validator.ShouldBeOfType<MoneyMagnitudeValidator>();
        ladder.ConfidenceFloor.ShouldBe(0.0);
    }

    [Theory]
    [MemberData(nameof(EveryFieldKindWithoutARegisteredLadder))]
    public void GetLadder_EveryOtherField_IsStillPositionalOnlyWithNoValidator(FieldKind fieldKind)
    {
        var ladder = _registry.GetLadder(fieldKind);

        ladder.Rungs.ShouldBeEmpty();
        ladder.Validator.ShouldBeNull();
        ladder.ConfidenceFloor.ShouldBe(0.0);
    }

    public static IEnumerable<object[]> MoneyFieldsData() => MoneyFields.Select(k => new object[] { k });

    public static IEnumerable<object[]> EveryFieldKindWithoutARegisteredLadder()
    {
        var registered = new HashSet<FieldKind>(MoneyFields) { FieldKind.PaymentDueDate, FieldKind.Product, FieldKind.Tasa, FieldKind.Cat };
        return Enum.GetValues<FieldKind>().Where(k => !registered.Contains(k)).Select(k => new object[] { k });
    }
}
