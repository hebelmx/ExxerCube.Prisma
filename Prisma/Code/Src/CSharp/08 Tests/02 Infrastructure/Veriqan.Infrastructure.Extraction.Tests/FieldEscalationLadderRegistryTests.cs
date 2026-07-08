using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Unit tests for <see cref="FieldEscalationLadderRegistry"/>'s default ladder table (E2.2:
/// <see cref="FieldKind.PaymentDueDate"/> is the first field with a non-empty ladder; E7.S7.2/S7.3
/// adds <see cref="FieldKind.Product"/>).
/// </summary>
public sealed class FieldEscalationLadderRegistryTests
{
    private readonly FieldEscalationLadderRegistry _registry = new();

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

    [Theory]
    [MemberData(nameof(EveryFieldKindExceptPaymentDueDateAndProduct))]
    public void GetLadder_EveryOtherField_IsStillPositionalOnly(FieldKind fieldKind)
    {
        var ladder = _registry.GetLadder(fieldKind);

        ladder.Rungs.ShouldBeEmpty();
        ladder.Validator.ShouldBeNull();
        ladder.ConfidenceFloor.ShouldBe(0.0);
    }

    public static IEnumerable<object[]> EveryFieldKindExceptPaymentDueDateAndProduct() =>
        Enum.GetValues<FieldKind>()
            .Where(k => k != FieldKind.PaymentDueDate && k != FieldKind.Product)
            .Select(k => new object[] { k });
}
