namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Unit tests for <see cref="PersonIdentityResolverService"/>.
/// </summary>
public class PersonIdentityResolverServiceTests
{
    private readonly ILogger<PersonIdentityResolverService> _logger;
    private readonly PersonIdentityResolverService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersonIdentityResolverServiceTests"/> class.
    /// </summary>
    public PersonIdentityResolverServiceTests()
    {
        _logger = Substitute.For<ILogger<PersonIdentityResolverService>>();
        _service = new PersonIdentityResolverService(_logger);
    }

    /// <summary>
    /// Tests that identity resolution generates RFC variants correctly.
    /// </summary>
    [Fact]
    public async Task ResolveIdentityAsync_WithRfc_GeneratesRfcVariants()
    {
        // Arrange
        var person = new Persona
        {
            ParteId = 1,
            Nombre = "Juan",
            Paterno = "Perez",
            Materno = "Garcia",
            Rfc = "PEGJ850101ABC"
        };

        // Act
        var result = await _service.ResolveIdentityAsync(person, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.RfcVariants.ShouldNotBeEmpty();
        result.Value.RfcVariants.ShouldContain("PEGJ850101ABC");
        result.Value.RfcVariants.ShouldContain("PEG-850101-ABC");
    }

    /// <summary>
    /// Tests that identity resolution normalizes name components.
    /// </summary>
    [Fact]
    public async Task ResolveIdentityAsync_WithExtraWhitespace_NormalizesNames()
    {
        // Arrange
        var person = new Persona
        {
            ParteId = 1,
            Nombre = "  Juan   Carlos  ",
            Paterno = "  Perez  ",
            Materno = "  Garcia  "
        };

        // Act
        var result = await _service.ResolveIdentityAsync(person, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Nombre.ShouldBe("Juan Carlos");
        result.Value.Paterno.ShouldBe("Perez");
        result.Value.Materno.ShouldBe("Garcia");
    }

    /// <summary>
    /// Tests that deduplication removes duplicate persons by RFC.
    /// </summary>
    [Fact]
    public async Task DeduplicatePersonsAsync_WithDuplicateRfcs_RemovesDuplicates()
    {
        // Arrange
        var persons = new List<Persona>
        {
            new Persona { ParteId = 1, Nombre = "Juan", Rfc = "PEGJ850101ABC" },
            new Persona { ParteId = 2, Nombre = "Juan", Rfc = "PEG-850101-ABC" }, // Same RFC, different format
            new Persona { ParteId = 3, Nombre = "Maria", Rfc = "MARG900202XYZ" }
        };

        // Act
        var result = await _service.DeduplicatePersonsAsync(persons, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2); // Should remove one duplicate
    }

    /// <summary>
    /// Tests that deduplication removes duplicate persons by name when RFC is not available.
    /// </summary>
    [Fact]
    public async Task DeduplicatePersonsAsync_WithDuplicateNames_RemovesDuplicates()
    {
        // Arrange
        var persons = new List<Persona>
        {
            new Persona { ParteId = 1, Nombre = "Juan", Paterno = "Perez", Materno = "Garcia" },
            new Persona { ParteId = 2, Nombre = "Juan", Paterno = "Perez", Materno = "Garcia" }, // Duplicate
            new Persona { ParteId = 3, Nombre = "Maria", Paterno = "Lopez" }
        };

        // Act
        var result = await _service.DeduplicatePersonsAsync(persons, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2); // Should remove one duplicate
    }

    /// <summary>
    /// Tests that RFC variants are generated correctly for different formats.
    /// </summary>
    [Fact]
    public void GenerateRfcVariants_WithDifferentFormats_ReturnsAllVariants()
    {
        // Arrange
        var rfc = "PEGJ850101ABC";

        // Act
        var variants = _service.GenerateRfcVariants(rfc);

        // Assert
        variants.ShouldNotBeEmpty();
        variants.ShouldContain("PEGJ850101ABC");
        variants.ShouldContain("PEG-850101-ABC");
        variants.ShouldContain("PEG 850101 ABC");
    }

    /// <summary>
    /// Tests that RFC variants handle hyphenated input.
    /// </summary>
    [Fact]
    public void GenerateRfcVariants_WithHyphenatedRfc_GeneratesVariants()
    {
        // Arrange
        var rfc = "PEG-850101-ABC";

        // Act
        var variants = _service.GenerateRfcVariants(rfc);

        // Assert
        variants.ShouldNotBeEmpty();
        variants.ShouldContain("PEG-850101-ABC");
        variants.ShouldContain("PEG850101ABC");
    }

    /// <summary>
    /// Tests that empty RFC returns empty variants list.
    /// </summary>
    [Fact]
    public void GenerateRfcVariants_WithEmptyRfc_ReturnsEmptyList()
    {
        // Arrange
        var rfc = string.Empty;

        // Act
        var variants = _service.GenerateRfcVariants(rfc);

        // Assert
        variants.ShouldBeEmpty();
    }

    /// <summary>
    /// Tests that FindByRfcAsync returns null when person not found (placeholder implementation).
    /// </summary>
    [Fact]
    public async Task FindByRfcAsync_WithValidRfc_ReturnsNull()
    {
        // Arrange
        var rfc = "PEGJ850101ABC";

        // Act
        var result = await _service.FindByRfcAsync(rfc, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull(); // Placeholder implementation returns null
    }
}

