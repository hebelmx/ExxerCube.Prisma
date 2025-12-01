namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Unit tests for <see cref="LegalDirectiveClassifierService"/>.
/// </summary>
public class LegalDirectiveClassifierServiceTests
{
    private readonly ILogger<LegalDirectiveClassifierService> _logger;
    private readonly LegalDirectiveClassifierService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="LegalDirectiveClassifierServiceTests"/> class.
    /// </summary>
    public LegalDirectiveClassifierServiceTests()
    {
        _logger = Substitute.For<ILogger<LegalDirectiveClassifierService>>();
        _service = new LegalDirectiveClassifierService(_logger);
    }

    /// <summary>
    /// Tests that block directives are classified correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyDirectivesAsync_WithBlockDirective_ReturnsBlockAction()
    {
        // Arrange
        var documentText = "Se ordena el BLOQUEO de la cuenta 1234567890 por un monto de $1,000,000.00";

        // Act
        var result = await _service.ClassifyDirectivesAsync(documentText, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain(a => a.ActionType == ComplianceActionKind.Block);
        var blockAction = result.Value.First(a => a.ActionType == ComplianceActionKind.Block);
        blockAction.Confidence.ShouldBeGreaterThan(60);
    }

    /// <summary>
    /// Tests that unblock directives are classified correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyDirectivesAsync_WithUnblockDirective_ReturnsUnblockAction()
    {
        // Arrange
        var documentText = "Se ordena el DESBLOQUEO de la cuenta 1234567890";

        // Act
        var result = await _service.ClassifyDirectivesAsync(documentText, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain(a => a.ActionType == ComplianceActionKind.Unblock);
    }

    /// <summary>
    /// Tests that document directives are classified correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyDirectivesAsync_WithDocumentDirective_ReturnsDocumentAction()
    {
        // Arrange
        var documentText = "Se solicita la DOCUMENTACIÓN correspondiente al expediente";

        // Act
        var result = await _service.ClassifyDirectivesAsync(documentText, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain(a => a.ActionType == ComplianceActionKind.Document);
    }

    /// <summary>
    /// Tests that transfer directives are classified correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyDirectivesAsync_WithTransferDirective_ReturnsTransferAction()
    {
        // Arrange
        var documentText = "Se ordena la TRANSFERENCIA de fondos por un monto de $500,000.00";

        // Act
        var result = await _service.ClassifyDirectivesAsync(documentText, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain(a => a.ActionType == ComplianceActionKind.Transfer);
        var transferAction = result.Value.First(a => a.ActionType == ComplianceActionKind.Transfer);
        transferAction.Amount.ShouldBe(500000.00m);
    }

    /// <summary>
    /// Tests that information directives are classified correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyDirectivesAsync_WithInformationDirective_ReturnsInformationAction()
    {
        // Arrange
        var documentText = "Se solicita INFORMACIÓN sobre las operaciones realizadas";

        // Act
        var result = await _service.ClassifyDirectivesAsync(documentText, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain(a => a.ActionType == ComplianceActionKind.Information);
    }

    /// <summary>
    /// Tests that unknown directives are classified as Ignore.
    /// </summary>
    [Fact]
    public async Task ClassifyDirectivesAsync_WithUnknownDirective_ReturnsIgnoreAction()
    {
        // Arrange
        var documentText = "Texto sin directivas específicas";

        // Act
        var result = await _service.ClassifyDirectivesAsync(documentText, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain(a => a.ActionType == ComplianceActionKind.Ignore);
    }

    /// <summary>
    /// Tests that legal instruments are detected correctly.
    /// </summary>
    [Fact]
    public async Task DetectLegalInstrumentsAsync_WithAcuerdo_ReturnsAcuerdo()
    {
        // Arrange
        var documentText = "De conformidad con el Acuerdo 105/2021";

        // Act
        var result = await _service.DetectLegalInstrumentsAsync(documentText, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain("Acuerdo 105/2021");
    }

    /// <summary>
    /// Tests that multiple legal instruments are detected.
    /// </summary>
    [Fact]
    public async Task DetectLegalInstrumentsAsync_WithMultipleInstruments_ReturnsAll()
    {
        // Arrange
        var documentText = "De conformidad con el Acuerdo 105/2021 y la Ley 123/2020";

        // Act
        var result = await _service.DetectLegalInstrumentsAsync(documentText, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBeGreaterThanOrEqualTo(2);
        result.Value.ShouldContain("Acuerdo 105/2021");
        result.Value.ShouldContain("Ley 123/2020");
    }

    /// <summary>
    /// Tests that MapToComplianceActionAsync maps block directive correctly.
    /// </summary>
    [Fact]
    public async Task MapToComplianceActionAsync_WithBlockDirective_ReturnsBlockAction()
    {
        // Arrange
        var directiveText = "BLOQUEO de cuenta 1234567890";
        var expediente = new Expediente { NumeroExpediente = "EXP-001", NumeroOficio = "OF-001" };

        // Act
        var result = await _service.MapToComplianceActionAsync(directiveText, expediente, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ActionType.ShouldBe(ComplianceActionKind.Block);
        result.Value.ExpedienteOrigen.ShouldBe("EXP-001");
        result.Value.OficioOrigen.ShouldBe("OF-001");
        result.Value.AccountNumber.ShouldBe("1234567890");
    }

    /// <summary>
    /// Tests that account number is extracted from directive text.
    /// </summary>
    [Fact]
    public async Task MapToComplianceActionAsync_WithAccountNumber_ExtractsAccountNumber()
    {
        // Arrange
        var directiveText = "BLOQUEO de la cuenta 9876543210";

        // Act
        var result = await _service.MapToComplianceActionAsync(directiveText, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.AccountNumber.ShouldBe("9876543210");
    }

    /// <summary>
    /// Tests that amount is extracted from directive text.
    /// </summary>
    [Fact]
    public async Task MapToComplianceActionAsync_WithAmount_ExtractsAmount()
    {
        // Arrange
        var directiveText = "TRANSFERENCIA por un monto de $1,234,567.89";

        // Act
        var result = await _service.MapToComplianceActionAsync(directiveText, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Amount.ShouldBe(1234567.89m);
    }
}

