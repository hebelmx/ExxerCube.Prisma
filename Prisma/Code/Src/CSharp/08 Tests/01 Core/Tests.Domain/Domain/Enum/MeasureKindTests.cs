using ExxerCube.Prisma.Domain.Enum;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.Domain.Domain.Enum;

public class MeasureKindTests
{
    [Fact]
    public void FromValue_ShouldReturnKnownMeasure()
    {
        MeasureKind.FromValue(1).ShouldBe(MeasureKind.Block);
        MeasureKind.FromValue(999).ShouldBe(MeasureKind.Other);
    }

    [Fact]
    public void ImplicitConversion_ShouldRoundtripInt()
    {
        int stored = MeasureKind.TransferFunds;
        stored.ShouldBe(3);

        MeasureKind roundtrip = stored;
        roundtrip.ShouldBe(MeasureKind.TransferFunds);
    }
}
