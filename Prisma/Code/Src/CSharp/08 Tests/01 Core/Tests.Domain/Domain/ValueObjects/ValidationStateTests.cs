using ExxerCube.Prisma.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.Domain.Domain.ValueObjects;

public class ValidationStateTests
{
    [Fact]
    public void Require_ShouldTrackMissingFieldsAndExposeValidity()
    {
        var state = new ValidationState();

        state.Require(false, "NumeroExpediente");
        state.Require(true, "Ignored");
        state.Require(false, "FundamentoLegal");

        state.IsValid.ShouldBeFalse();
        state.Missing.ShouldContain("NumeroExpediente");
        state.Missing.ShouldContain("FundamentoLegal");
        state.Missing.Count.ShouldBe(2);
    }
}
