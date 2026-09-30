namespace ExxerCube.Prisma.Tests.Infrastructure.Export;

/// <summary>
/// Mutation-hardening tests for <see cref="CriterionMapperService"/> — maps compliance requirements to the
/// SIRO criteria dictionary. Pins the cancellation/null guards, the per-requirement criterion key + nested
/// field map, the summary entries (TotalRequirements / MappedAt ISO-UTC), and the generic catch path.
/// </summary>
public class CriterionMapperServiceTests
{
    private static CriterionMapperService Mapper()
        => new(XUnitLogger.CreateLogger<CriterionMapperService>());

    private static ComplianceRequirement Requirement(string id, string descripcion, string tipo, bool obligatorio) => new()
    {
        RequerimientoId = id,
        Descripcion = descripcion,
        Tipo = tipo,
        EsObligatorio = obligatorio,
    };

    private static Dictionary<string, object> Nested(Dictionary<string, object> root, string key)
        => (Dictionary<string, object>)root[key];

    // ---------------------------------------------------------------------
    // Guards
    // ---------------------------------------------------------------------

    /// <summary>Verifies the pre-start cancellation guard short-circuits to a Cancelled result.</summary>
    [Fact]
    public async Task MapToSiroCriteriaAsync_CancelledToken_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await Mapper().MapToSiroCriteriaAsync(new List<ComplianceRequirement>(), cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>Verifies null requirements yields the exact validation failure message.</summary>
    [Fact]
    public async Task MapToSiroCriteriaAsync_NullRequirements_ReturnsFailure()
    {
        var result = await Mapper().MapToSiroCriteriaAsync(null!, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Requirements cannot be null");
    }

    // ---------------------------------------------------------------------
    // Mapping behaviour
    // ---------------------------------------------------------------------

    /// <summary>Verifies an empty list still succeeds with a zero count and a timestamp but no criterion rows.</summary>
    [Fact]
    public async Task MapToSiroCriteriaAsync_EmptyList_SucceedsWithSummaryOnly()
    {
        var result = await Mapper().MapToSiroCriteriaAsync(new List<ComplianceRequirement>(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var dict = result.Value!;
        dict["TotalRequirements"].ShouldBe(0);
        dict.ContainsKey("MappedAt").ShouldBeTrue();
        dict.Keys.ShouldNotContain(k => k.StartsWith("Criterion_"));
    }

    /// <summary>Verifies a single requirement maps to a "Criterion_{id}" row with every nested field exact.</summary>
    [Fact]
    public async Task MapToSiroCriteriaAsync_SingleRequirement_MapsAllNestedFields()
    {
        var requirements = new List<ComplianceRequirement>
        {
            Requirement("R1", "Bloquear la cuenta", "Bloqueo", obligatorio: true),
        };

        var result = await Mapper().MapToSiroCriteriaAsync(requirements, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var dict = result.Value!;
        dict.ContainsKey("Criterion_R1").ShouldBeTrue();
        var nested = Nested(dict, "Criterion_R1");
        nested["RequerimientoId"].ShouldBe("R1");
        nested["Descripcion"].ShouldBe("Bloquear la cuenta");
        nested["Tipo"].ShouldBe("Bloqueo");
        nested["EsObligatorio"].ShouldBe(true);
    }

    /// <summary>Verifies the non-mandatory flag is preserved as false (not coerced).</summary>
    [Fact]
    public async Task MapToSiroCriteriaAsync_NonMandatoryRequirement_PreservesFalse()
    {
        var requirements = new List<ComplianceRequirement>
        {
            Requirement("R9", "Informar movimientos", "Informacion", obligatorio: false),
        };

        var result = await Mapper().MapToSiroCriteriaAsync(requirements, TestContext.Current.CancellationToken);

        Nested(result.Value!, "Criterion_R9")["EsObligatorio"].ShouldBe(false);
    }

    /// <summary>Verifies every requirement gets its own keyed row and TotalRequirements equals the count.</summary>
    [Fact]
    public async Task MapToSiroCriteriaAsync_MultipleRequirements_AllMappedAndCounted()
    {
        var requirements = new List<ComplianceRequirement>
        {
            Requirement("R1", "uno", "Bloqueo", true),
            Requirement("R2", "dos", "Documentacion", false),
            Requirement("R3", "tres", "Transferencia", true),
        };

        var result = await Mapper().MapToSiroCriteriaAsync(requirements, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var dict = result.Value!;
        dict["TotalRequirements"].ShouldBe(3);
        dict.ContainsKey("Criterion_R1").ShouldBeTrue();
        dict.ContainsKey("Criterion_R2").ShouldBeTrue();
        dict.ContainsKey("Criterion_R3").ShouldBeTrue();
        Nested(dict, "Criterion_R2")["Tipo"].ShouldBe("Documentacion");
    }

    /// <summary>Verifies the criterion key is exactly "Criterion_" + the RequerimientoId.</summary>
    [Fact]
    public async Task MapToSiroCriteriaAsync_CriterionKey_UsesPrefixedRequerimientoId()
    {
        var requirements = new List<ComplianceRequirement>
        {
            Requirement("ABC-7", "x", "Bloqueo", true),
        };

        var result = await Mapper().MapToSiroCriteriaAsync(requirements, TestContext.Current.CancellationToken);

        result.Value!.ContainsKey("Criterion_ABC-7").ShouldBeTrue();
    }

    /// <summary>Verifies MappedAt is an ISO-8601 UTC timestamp (yyyy-MM-ddTHH:mm:ssZ).</summary>
    [Fact]
    public async Task MapToSiroCriteriaAsync_MappedAt_IsIsoUtcTimestamp()
    {
        var result = await Mapper().MapToSiroCriteriaAsync(new List<ComplianceRequirement>(), TestContext.Current.CancellationToken);

        var mappedAt = (string)result.Value!["MappedAt"];
        mappedAt.ShouldMatch(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$");
    }

    // ---------------------------------------------------------------------
    // Generic catch path
    // ---------------------------------------------------------------------

    /// <summary>Verifies a null element in the list is converted to a failure result (generic catch), not thrown.</summary>
    [Fact]
    public async Task MapToSiroCriteriaAsync_NullElement_ReturnsFailure()
    {
        var requirements = new List<ComplianceRequirement> { null! };

        var result = await Mapper().MapToSiroCriteriaAsync(requirements, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("Error mapping to SIRO criteria");
    }
}
