using ExxerCube.Prisma.Domain.Enum;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.Domain.Domain.Enum;

public class DocumentItemKindTests
{
    [Fact]
    public void FromValue_ShouldReturnKnownItem()
    {
        DocumentItemKind.FromValue(1).ShouldBe(DocumentItemKind.EstadoCuenta);
        DocumentItemKind.FromValue(999).ShouldBe(DocumentItemKind.Other);
    }

    [Fact]
    public void ImplicitConversion_ShouldRoundtripInt()
    {
        int stored = DocumentItemKind.Contrato;
        stored.ShouldBe(2);

        DocumentItemKind roundtrip = stored;
        roundtrip.ShouldBe(DocumentItemKind.Contrato);
    }
}
