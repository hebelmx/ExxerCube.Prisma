using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Ocr;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Unit tests for <see cref="DefaultFieldStageProvider"/>'s generic dispatch (E2.2, extended
/// E7.S7.2/S7.3 for <see cref="FieldKind.Product"/>).
/// </summary>
public sealed class DefaultFieldStageProviderTests
{
    private readonly DefaultFieldStageProvider _provider =
        new(Substitute.For<IHeaderProductOcrEngine>(), NullLoggerFactory.Instance);

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

    [Fact]
    public void GetHigherStages_ProductAsString_ReturnsOneHeaderImageOcrStage()
    {
        var stages = _provider.GetHigherStages<string>(FieldKind.Product);

        stages.Count.ShouldBe(1);
        stages[0].Stage.ShouldBe(StageId.HeaderImageOcr);
        stages[0].ShouldBeOfType<HeaderImageOcrStage>();
    }

    [Fact]
    public void GetHigherStages_ProductWithWrongTValue_ReturnsEmpty()
    {
        // Defensive: FieldKind.Product is always resolved as string in production, but the
        // provider must not throw or mis-dispatch if ever asked with a different TValue.
        var stages = _provider.GetHigherStages<DateOnly>(FieldKind.Product);

        stages.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(FieldKind.Tasa)]
    [InlineData(FieldKind.ClientName)]
    [InlineData(FieldKind.PeriodCutDate)]
    [InlineData(FieldKind.Movements)]
    public void GetHigherStages_EveryOtherField_ReturnsEmpty(FieldKind fieldKind)
    {
        _provider.GetHigherStages<string>(fieldKind).ShouldBeEmpty();
    }
}
