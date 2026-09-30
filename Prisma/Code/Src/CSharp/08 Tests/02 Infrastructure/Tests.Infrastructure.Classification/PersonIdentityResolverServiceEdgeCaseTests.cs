namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Edge case tests for <see cref="PersonIdentityResolverService"/> covering null handling, cancellation, and exceptions.
/// </summary>
public class PersonIdentityResolverServiceEdgeCaseTests
{
    private readonly ILogger<PersonIdentityResolverService> _logger;
    private readonly PersonIdentityResolverService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersonIdentityResolverServiceEdgeCaseTests"/> class.
    /// </summary>
    public PersonIdentityResolverServiceEdgeCaseTests()
    {
        _logger = Substitute.For<ILogger<PersonIdentityResolverService>>();
        _service = new PersonIdentityResolverService(_logger);
    }

    /// <summary>
    /// Tests that ResolveIdentityAsync handles null person input correctly.
    /// </summary>
    [Fact]
    public async Task ResolveIdentityAsync_WithNullPerson_ReturnsFailure()
    {
        // Act
        var result = await _service.ResolveIdentityAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Person cannot be null");
    }

    /// <summary>
    /// Tests that ResolveIdentityAsync handles cancellation token correctly.
    /// </summary>
    [Fact]
    public async Task ResolveIdentityAsync_WithCancellationRequested_ReturnsCancelled()
    {
        // Arrange
        var person = new Persona { ParteId = 1, Nombre = "Juan" };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _service.ResolveIdentityAsync(person, cts.Token);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("cancelled");
    }

    /// <summary>
    /// Tests that DeduplicatePersonsAsync handles null list input correctly.
    /// </summary>
    [Fact]
    public async Task DeduplicatePersonsAsync_WithNullList_ReturnsFailure()
    {
        // Act
        var result = await _service.DeduplicatePersonsAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Persons list cannot be null");
    }

    /// <summary>
    /// Tests that DeduplicatePersonsAsync handles cancellation token correctly.
    /// </summary>
    [Fact]
    public async Task DeduplicatePersonsAsync_WithCancellationRequested_ReturnsCancelled()
    {
        // Arrange
        var persons = new List<Persona> { new Persona { ParteId = 1, Nombre = "Juan" } };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _service.DeduplicatePersonsAsync(persons, cts.Token);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("cancelled");
    }

    /// <summary>
    /// Tests that FindByRfcAsync handles null RFC input correctly.
    /// </summary>
    [Fact]
    public async Task FindByRfcAsync_WithNullRfc_ReturnsFailure()
    {
        // Act
        var result = await _service.FindByRfcAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("RFC cannot be null or empty");
    }

    /// <summary>
    /// Tests that FindByRfcAsync handles empty RFC input correctly.
    /// </summary>
    [Fact]
    public async Task FindByRfcAsync_WithEmptyRfc_ReturnsFailure()
    {
        // Act
        var result = await _service.FindByRfcAsync(string.Empty, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("RFC cannot be null or empty");
    }

    /// <summary>
    /// Tests that FindByRfcAsync handles whitespace-only RFC input correctly.
    /// </summary>
    [Fact]
    public async Task FindByRfcAsync_WithWhitespaceRfc_ReturnsFailure()
    {
        // Act
        var result = await _service.FindByRfcAsync("   ", TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("RFC cannot be null or empty");
    }

    /// <summary>
    /// Tests that FindByRfcAsync handles cancellation token correctly.
    /// </summary>
    [Fact]
    public async Task FindByRfcAsync_WithCancellationRequested_ReturnsCancelled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _service.FindByRfcAsync("PEGJ850101ABC", cts.Token);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("cancelled");
    }

    /// <summary>
    /// Tests that GenerateRfcVariants handles null RFC input correctly.
    /// </summary>
    [Fact]
    public void GenerateRfcVariants_WithNullRfc_ReturnsEmptyList()
    {
        // Act
        var variants = _service.GenerateRfcVariants(null!);

        // Assert
        variants.ShouldBeEmpty();
    }

    /// <summary>
    /// Tests that ResolveIdentityAsync handles person with all null name fields correctly.
    /// </summary>
    [Fact]
    public async Task ResolveIdentityAsync_WithAllNullNames_HandlesGracefully()
    {
        // Arrange
        var person = new Persona
        {
            ParteId = 1,
            Nombre = null!,
            Paterno = null,
            Materno = null,
            Rfc = null
        };

        // Act
        var result = await _service.ResolveIdentityAsync(person, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that DeduplicatePersonsAsync handles empty list correctly.
    /// </summary>
    [Fact]
    public async Task DeduplicatePersonsAsync_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var persons = new List<Persona>();

        // Act
        var result = await _service.DeduplicatePersonsAsync(persons, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldBeEmpty();
    }
}

