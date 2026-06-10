namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Mutation-killing tests for <see cref="PersonIdentityResolverService"/>. The service is deterministic
/// (only an <see cref="ILogger{T}"/>): RFC-variant generation, name normalization, and HashSet-based
/// deduplication. The DB lookup in <c>FindByRfcAsync</c> is a documented stub. Pins exact values so the
/// substring/length-boundary/`char.IsLetter`/dedup-path mutants die.
/// </summary>
public class PersonIdentityResolverServiceMutationTests
{
    private readonly PersonIdentityResolverService _service;

    public PersonIdentityResolverServiceMutationTests(ITestOutputHelper output) =>
        _service = new PersonIdentityResolverService(XUnitLogger.CreateLogger<PersonIdentityResolverService>(output));

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Constructor_NullLogger_Throws() =>
        Should.Throw<ArgumentNullException>(() => new PersonIdentityResolverService(null!)).ParamName.ShouldBe("logger");

    // =================== GenerateRfcVariants ===================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateRfcVariants_BlankInput_ReturnsEmpty(string? rfc) =>
        _service.GenerateRfcVariants(rfc!).ShouldBeEmpty();

    [Fact]
    public void GenerateRfcVariants_ThirteenChar_ProducesTrimHyphenAndSpaced()
    {
        // "PEGJ850101ABC": middle letter at index 3 ('J') is dropped → "PEG-850101-ABC" / "PEG 850101 ABC".
        var variants = _service.GenerateRfcVariants("PEGJ850101ABC");

        variants.ShouldContain("PEGJ850101ABC");
        variants.ShouldContain("PEG-850101-ABC"); // substring(0,3)+substring(4,6)+substring(10)
        variants.ShouldContain("PEG 850101 ABC");
    }

    [Fact]
    public void GenerateRfcVariants_TwelveChar_HyphenatesKeepingAllChars()
    {
        // "PEG850101ABC" (12): no middle letter dropped → substring(0,3)+substring(3,6)+substring(9).
        var variants = _service.GenerateRfcVariants("PEG850101ABC");

        variants.ShouldContain("PEG850101ABC");
        variants.ShouldContain("PEG-850101-ABC");
        variants.ShouldContain("PEG 850101 ABC");
    }

    [Fact]
    public void GenerateRfcVariants_WithSeparators_AddsCleanedVariant()
    {
        // Input already hyphenated → the separator-stripped "cleaned" form is a distinct variant.
        var variants = _service.GenerateRfcVariants("PEG-850101-ABC");

        variants.ShouldContain("PEG-850101-ABC"); // the trimmed original
        variants.ShouldContain("PEG850101ABC");    // cleaned (kills the Replace("-"/" "/".") calls)
    }

    [Fact]
    public void GenerateRfcVariants_ThirteenChar_NonLetterAtIndex3_NoHyphenatedVariant()
    {
        // cleaned[3] is a digit → char.IsLetter guard false → no 13-char hyphen/space variants.
        var variants = _service.GenerateRfcVariants("PEG1850101ABC"); // 13 chars, index 3 = '1'

        variants.ShouldContain("PEG1850101ABC");
        variants.ShouldNotContain("PEG-850101-ABC");
        variants.ShouldNotContain("PEG 850101 ABC");
    }

    [Fact]
    public void GenerateRfcVariants_BelowTwelveChars_NoFormattedVariants()
    {
        // length < 12 → only the trimmed original (cleaned == original, so not re-added).
        var variants = _service.GenerateRfcVariants("ABC123");

        variants.ShouldHaveSingleItem();
        variants[0].ShouldBe("ABC123");
    }

    [Fact]
    public void GenerateRfcVariants_SpaceSeparated_StripsSpaces()
    {
        // Kills the Replace(" ", "") call: without it, cleaned keeps the spaces and equals the input,
        // so the compact "PEG850101ABC" variant is never produced.
        var variants = _service.GenerateRfcVariants("PEG 850101 ABC");

        variants.ShouldContain("PEG850101ABC");
    }

    [Fact]
    public void GenerateRfcVariants_DotSeparated_StripsDots()
    {
        // Kills the Replace(".", "") call.
        var variants = _service.GenerateRfcVariants("PEG.850101.ABC");

        variants.ShouldContain("PEG850101ABC");
    }

    // =================== ResolveIdentityAsync ===================

    [Fact]
    public async Task ResolveIdentity_Cancelled_ReturnsExactFailure()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _service.ResolveIdentityAsync(new Persona(), cts.Token);

        result.Error.ShouldBe("Operation was cancelled.");
    }

    [Fact]
    public async Task ResolveIdentity_NullPerson_ReturnsExactFailure()
    {
        var result = await _service.ResolveIdentityAsync(null!, Ct);

        result.Error.ShouldBe("Person cannot be null.");
    }

    [Fact]
    public async Task ResolveIdentity_WithRfc_PopulatesVariants_AndNormalizesNames()
    {
        var person = new Persona
        {
            Nombre = "  Juan   Carlos ",
            Paterno = "Perez\tLopez",
            Materno = "Garcia",
            Rfc = "PEGJ850101ABC",
        };

        var result = await _service.ResolveIdentityAsync(person, Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.RfcVariants.Count.ShouldBeGreaterThan(0);
        // NormalizeName collapses runs of whitespace to a single space and trims.
        result.Value.Nombre.ShouldBe("Juan Carlos");
        result.Value.Paterno.ShouldBe("Perez Lopez");
        result.Value.Materno.ShouldBe("Garcia");
    }

    [Fact]
    public async Task ResolveIdentity_NoRfc_DoesNotPopulateVariants_ButNormalizes()
    {
        var person = new Persona { Nombre = " Ana ", Rfc = "" };

        var result = await _service.ResolveIdentityAsync(person, Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.RfcVariants.ShouldBeEmpty(); // blank RFC → the variant-generation block is skipped
        result.Value.Nombre.ShouldBe("Ana");
    }

    // =================== DeduplicatePersonsAsync ===================

    private static Persona P(string nombre = "", string paterno = "", string materno = "", string rfc = "") =>
        new() { Nombre = nombre, Paterno = paterno, Materno = materno, Rfc = rfc };

    [Fact]
    public async Task Deduplicate_Cancelled_ReturnsExactFailure()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _service.DeduplicatePersonsAsync(new List<Persona>(), cts.Token);

        result.Error.ShouldBe("Operation was cancelled.");
    }

    [Fact]
    public async Task Deduplicate_NullList_ReturnsExactFailure()
    {
        var result = await _service.DeduplicatePersonsAsync(null!, Ct);

        result.Error.ShouldBe("Persons list cannot be null.");
    }

    [Fact]
    public async Task Deduplicate_SameRfcExact_CollapsesToOne()
    {
        var result = await _service.DeduplicatePersonsAsync(
            new List<Persona> { P(rfc: "PEGJ850101ABC"), P(rfc: "PEGJ850101ABC") }, Ct);

        result.Value!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Deduplicate_SameRfcDifferentFormat_CollapsesViaNormalizedRfc()
    {
        // "PEGJ850101ABC" and "PEG-850101-ABC" normalize to the same value (middle letter dropped) → dedup.
        var result = await _service.DeduplicatePersonsAsync(
            new List<Persona> { P(rfc: "PEGJ850101ABC"), P(rfc: "PEG-850101-ABC") }, Ct);

        result.Value!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Deduplicate_DifferentRfc_KeepsBoth()
    {
        var result = await _service.DeduplicatePersonsAsync(
            new List<Persona> { P(rfc: "PEGJ850101ABC"), P(rfc: "XXXX991231ZZZ") }, Ct);

        result.Value!.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Deduplicate_NoRfcSameName_CollapsesByName()
    {
        var result = await _service.DeduplicatePersonsAsync(
            new List<Persona> { P("Juan", "Perez", "Garcia"), P("juan", "perez", "garcia") }, Ct);

        // GetNormalizedName upper-cases → case-insensitive name match → dedup.
        result.Value!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Deduplicate_NoRfcDifferentName_KeepsBoth()
    {
        var result = await _service.DeduplicatePersonsAsync(
            new List<Persona> { P("Juan", "Perez"), P("Maria", "Lopez") }, Ct);

        result.Value!.Count.ShouldBe(2);
    }

    // GetNormalizedName must include EVERY name part in the dedup key. Two persons differing in only one
    // part must stay distinct — this kills the per-part `if (!IsNullOrWhiteSpace(...))` guards + the
    // parts.Add(...) statements (negating/removing any one would drop that part from the key and wrongly merge).

    [Fact]
    public async Task Deduplicate_NoRfc_DiffersOnlyInNombre_KeepsBoth()
    {
        var result = await _service.DeduplicatePersonsAsync(
            new List<Persona> { P("Juan", "Perez", "Garcia"), P("Pedro", "Perez", "Garcia") }, Ct);

        result.Value!.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Deduplicate_NoRfc_DiffersOnlyInPaterno_KeepsBoth()
    {
        var result = await _service.DeduplicatePersonsAsync(
            new List<Persona> { P("Juan", "Perez", "Garcia"), P("Juan", "Lopez", "Garcia") }, Ct);

        result.Value!.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Deduplicate_NoRfc_DiffersOnlyInMaterno_KeepsBoth()
    {
        var result = await _service.DeduplicatePersonsAsync(
            new List<Persona> { P("Juan", "Perez", "Garcia"), P("Juan", "Perez", "Sanchez") }, Ct);

        result.Value!.Count.ShouldBe(2);
    }

    // =================== FindByRfcAsync (stub) ===================

    [Fact]
    public async Task FindByRfc_Cancelled_ReturnsExactFailure()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _service.FindByRfcAsync("PEGJ850101ABC", cts.Token);

        result.Error.ShouldBe("Operation was cancelled.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FindByRfc_BlankRfc_ReturnsExactFailure(string? rfc)
    {
        var result = await _service.FindByRfcAsync(rfc!, Ct);

        result.Error.ShouldBe("RFC cannot be null or empty.");
    }

    [Fact]
    public async Task FindByRfc_ValidRfc_ReturnsSuccessNull_StubBehavior()
    {
        var result = await _service.FindByRfcAsync("PEGJ850101ABC", Ct);

        // Documented stub: DB integration deferred → success with null (not-found) value.
        result.IsFailure.ShouldBeFalse();
        result.Value.ShouldBeNull();
    }
}
