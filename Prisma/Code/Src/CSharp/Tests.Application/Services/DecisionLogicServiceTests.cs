using ExxerCube.Prisma.Domain.Enums;

namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Unit tests for <see cref="DecisionLogicService"/>.
/// </summary>
public class DecisionLogicServiceTests
{
    private readonly IPersonIdentityResolver _personIdentityResolver;
    private readonly ILegalDirectiveClassifier _legalDirectiveClassifier;
    private readonly IManualReviewerPanel _manualReviewerPanel;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<DecisionLogicService> _logger;
    private readonly DecisionLogicService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionLogicServiceTests"/> class.
    /// </summary>
    public DecisionLogicServiceTests()
    {
        _personIdentityResolver = Substitute.For<IPersonIdentityResolver>();
        _legalDirectiveClassifier = Substitute.For<ILegalDirectiveClassifier>();
        _manualReviewerPanel = Substitute.For<IManualReviewerPanel>();
        _auditLogger = Substitute.For<IAuditLogger>();
        _logger = Substitute.For<ILogger<DecisionLogicService>>();
        _service = new DecisionLogicService(_personIdentityResolver, _legalDirectiveClassifier, _manualReviewerPanel, _auditLogger, _logger);
    }

    /// <summary>
    /// Tests that ResolvePersonIdentitiesAsync resolves and deduplicates persons correctly.
    /// </summary>
    [Fact]
    public async Task ResolvePersonIdentitiesAsync_WithValidPersons_ReturnsResolvedPersons()
    {
        // Arrange
        var persons = new List<Persona>
        {
            new Persona { ParteId = 1, Nombre = "Juan", Rfc = "PEGJ850101ABC" },
            new Persona { ParteId = 2, Nombre = "Maria", Rfc = "MARG900202XYZ" }
        };

        var resolvedPerson1 = new Persona { ParteId = 1, Nombre = "Juan", Rfc = "PEGJ850101ABC" };
        var resolvedPerson2 = new Persona { ParteId = 2, Nombre = "Maria", Rfc = "MARG900202XYZ" };
        var resolvedList = new List<Persona> { resolvedPerson1, resolvedPerson2 };

        _personIdentityResolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<Persona>.Success(resolvedPerson1), Result<Persona>.Success(resolvedPerson2));

        _personIdentityResolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<List<Persona>>.Success(resolvedList));

        // Act
        var result = await _service.ResolvePersonIdentitiesAsync(persons, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2);
    }

    /// <summary>
    /// Tests that ClassifyLegalDirectivesAsync classifies directives correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyLegalDirectivesAsync_WithValidText_ReturnsComplianceActions()
    {
        // Arrange
        var documentText = "Se ordena el BLOQUEO de la cuenta 1234567890";
        var actions = new List<ComplianceAction>
        {
            new ComplianceAction { ActionType = ComplianceActionType.Block, AccountNumber = "1234567890", Confidence = 80 }
        };

        _legalDirectiveClassifier.DetectLegalInstrumentsAsync(Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<List<string>>.Success(new List<string>()));

        _legalDirectiveClassifier.ClassifyDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<List<ComplianceAction>>.Success(actions));

        // Act
        var result = await _service.ClassifyLegalDirectivesAsync(documentText, null, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(1);
        result.Value[0].ActionType.ShouldBe(ComplianceActionType.Block);
    }

    /// <summary>
    /// Tests that ProcessDecisionLogicAsync orchestrates the complete workflow correctly.
    /// </summary>
    [Fact]
    public async Task ProcessDecisionLogicAsync_WithValidInputs_ReturnsCompleteResult()
    {
        // Arrange
        var persons = new List<Persona>
        {
            new Persona { ParteId = 1, Nombre = "Juan", Rfc = "PEGJ850101ABC" }
        };

        var documentText = "Se ordena el BLOQUEO de la cuenta 1234567890";
        var expediente = new Expediente { NumeroExpediente = "EXP-001", NumeroOficio = "OF-001" };

        var resolvedPerson = new Persona { ParteId = 1, Nombre = "Juan", Rfc = "PEGJ850101ABC" };
        var resolvedList = new List<Persona> { resolvedPerson };
        var actions = new List<ComplianceAction>
        {
            new ComplianceAction { ActionType = ComplianceActionType.Block, Confidence = 80 }
        };

        _personIdentityResolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<Persona>.Success(resolvedPerson));

        _personIdentityResolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<List<Persona>>.Success(resolvedList));

        _legalDirectiveClassifier.DetectLegalInstrumentsAsync(Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<List<string>>.Success(new List<string>()));

        _legalDirectiveClassifier.ClassifyDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<List<ComplianceAction>>.Success(actions));

        // Act
        var result = await _service.ProcessDecisionLogicAsync(persons, documentText, expediente, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ResolvedPersons.Count.ShouldBe(1);
        result.Value.ComplianceActions.Count.ShouldBe(1);
        result.Value.ComplianceActions[0].ActionType.ShouldBe(ComplianceActionType.Block);
    }

    /// <summary>
    /// Tests that ResolvePersonIdentitiesAsync handles empty list correctly.
    /// </summary>
    [Fact]
    public async Task ResolvePersonIdentitiesAsync_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var persons = new List<Persona>();

        // Act
        var result = await _service.ResolvePersonIdentitiesAsync(persons, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(0);
    }

    /// <summary>
    /// Tests that ClassifyLegalDirectivesAsync handles empty text correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyLegalDirectivesAsync_WithEmptyText_ReturnsEmptyList()
    {
        // Arrange
        var documentText = string.Empty;

        // Act
        var result = await _service.ClassifyLegalDirectivesAsync(documentText, null, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(0);
    }

    /// <summary>
    /// Tests that ProcessDecisionLogicAsync handles identity resolution failure correctly.
    /// </summary>
    [Fact]
    public async Task ProcessDecisionLogicAsync_WithIdentityResolutionFailure_ReturnsFailure()
    {
        // Arrange
        var persons = new List<Persona> { new Persona { ParteId = 1, Nombre = "Juan" } };
        var documentText = "Test text";

        _personIdentityResolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<Persona>.WithFailure("Identity resolution failed"));

        // Act
        var result = await _service.ProcessDecisionLogicAsync(persons, documentText, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldContain("Identity resolution failed");
    }

    /// <summary>
    /// Tests that ProcessDecisionLogicAsync handles classification failure correctly.
    /// </summary>
    [Fact]
    public async Task ProcessDecisionLogicAsync_WithClassificationFailure_ReturnsFailure()
    {
        // Arrange
        var persons = new List<Persona> { new Persona { ParteId = 1, Nombre = "Juan" } };
        var documentText = "Test text";

        var resolvedPerson = new Persona { ParteId = 1, Nombre = "Juan" };
        var resolvedList = new List<Persona> { resolvedPerson };

        _personIdentityResolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<Persona>.Success(resolvedPerson));

        _personIdentityResolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<List<Persona>>.Success(resolvedList));

        _legalDirectiveClassifier.DetectLegalInstrumentsAsync(Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<List<string>>.Success(new List<string>()));

        _legalDirectiveClassifier.ClassifyDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<List<ComplianceAction>>.WithFailure("Classification failed"));

        // Act
        var result = await _service.ProcessDecisionLogicAsync(persons, documentText, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldContain("Classification failed");
    }
}

