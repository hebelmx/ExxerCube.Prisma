using ExxerCube.Prisma.Domain.Enum;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.Domain.Domain.Enum;

public class ComplianceActionKindTests
{
    [Fact]
    public void FromValue_Returns_Known_Kind()
    {
        ComplianceActionKind.FromValue(0).ShouldBe(ComplianceActionKind.Block);
        ComplianceActionKind.FromValue(1).ShouldBe(ComplianceActionKind.Unblock);
        ComplianceActionKind.FromValue(3).ShouldBe(ComplianceActionKind.Transfer);
    }

    [Fact]
    public void FromValue_Unknown_Returns_Other()
    {
        ComplianceActionKind.FromValue(1234).ShouldBe(ComplianceActionKind.InvalidValue<ComplianceActionKind>());
    }

    [Fact]
    public void Implicit_Conversions_Roundtrip()
    {
        int stored = ComplianceActionKind.Information;
        ComplianceActionKind roundtrip = stored;
        roundtrip.ShouldBe(ComplianceActionKind.Information);
    }
}
