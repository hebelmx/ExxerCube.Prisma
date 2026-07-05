using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Unit tests for <see cref="FieldEscalationLadderRegistry"/>'s default ladder table (E2.2:
/// <see cref="FieldKind.PaymentDueDate"/> is the first field with a non-empty ladder).
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

    [Theory]
    [MemberData(nameof(EveryFieldKindExceptPaymentDueDate))]
    public void GetLadder_EveryOtherField_IsStillPositionalOnly(FieldKind fieldKind)
    {
        var ladder = _registry.GetLadder(fieldKind);

        ladder.Rungs.ShouldBeEmpty();
        ladder.Validator.ShouldBeNull();
        ladder.ConfidenceFloor.ShouldBe(0.0);
    }

    public static IEnumerable<object[]> EveryFieldKindExceptPaymentDueDate() =>
        Enum.GetValues<FieldKind>()
            .Where(k => k != FieldKind.PaymentDueDate)
            .Select(k => new object[] { k });
}
