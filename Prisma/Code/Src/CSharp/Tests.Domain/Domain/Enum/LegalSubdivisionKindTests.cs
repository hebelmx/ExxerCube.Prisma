using ExxerCube.Prisma.Domain.Enum;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.Domain.Domain.Enum;

public class LegalSubdivisionKindTests
{
    [Fact]
    public void FromValue_ShouldReturnKnownSubdivision()
    {
        LegalSubdivisionKind.FromValue(1).ShouldBe(LegalSubdivisionKind.A_AS);
        LegalSubdivisionKind.FromValue(999).ShouldBe(LegalSubdivisionKind.Other);
    }

    [Fact]
    public void ImplicitConversion_ShouldRoundtripInt()
    {
        int stored = LegalSubdivisionKind.J_IN;
        stored.ShouldBe(7);

        LegalSubdivisionKind roundtrip = stored;
        roundtrip.ShouldBe(LegalSubdivisionKind.J_IN);
    }
}
