using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Unit tests for <see cref="DefaultFieldStageProvider"/>'s generic dispatch (E2.2).
/// </summary>
public sealed class DefaultFieldStageProviderTests
{
    private readonly DefaultFieldStageProvider _provider = new();

    [Fact]
    public void GetHigherStages_PaymentDueDateAsDateOnly_ReturnsOneFuzzyLabelStage()
    {
        var stages = _provider.GetHigherStages<DateOnly>(FieldKind.PaymentDueDate);

        stages.Count.ShouldBe(1);
        stages[0].Stage.ShouldBe(StageId.FuzzyLabel);
        stages[0].ShouldBeOfType<FuzzyLabelStage<DateOnly>>();
    }

    [Fact]
    public void GetHigherStages_PaymentDueDateWithWrongTValue_ReturnsEmpty()
    {
        // Defensive: FieldKind.PaymentDueDate is always resolved as DateOnly in production, but
        // the provider must not throw or mis-dispatch if ever asked with a different TValue.
        var stages = _provider.GetHigherStages<string>(FieldKind.PaymentDueDate);

        stages.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(FieldKind.Product)]
    [InlineData(FieldKind.Tasa)]
    [InlineData(FieldKind.ClientName)]
    [InlineData(FieldKind.PeriodCutDate)]
    [InlineData(FieldKind.Movements)]
    public void GetHigherStages_EveryOtherField_ReturnsEmpty(FieldKind fieldKind)
    {
        _provider.GetHigherStages<string>(fieldKind).ShouldBeEmpty();
    }
}
