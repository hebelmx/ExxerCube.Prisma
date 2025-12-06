namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// TDD tests for dictionary-driven <see cref="ISemanticAnalyzer"/>.
/// Tests verify the NEW implementation that fixes the audit gap.
/// </summary>
/// <remarks>
/// Testing Strategy (Per Audit Remediation Plan):
/// - Dictionary-based classification using ITextComparer.FindBestMatch
/// - Returns SemanticAnalysis object (fixes the audit gap - not List&lt;ComplianceAction&gt;)
/// - Handles varied phrasing ("aseguramiento de fondos" vs "aseguramiento de los fondos")
/// - Detects multiple directives in single document
/// - Tolerates typos (≥85% similarity threshold)
/// - Populates rich domain objects (RequiereBloqueo, RequiereDesbloqueo, etc.)
///
/// Implementation: Uses REAL SemanticAnalyzerService (not mocks)
/// - Production ITextComparer (LevenshteinTextComparer)
/// - Real ClassificationDictionary (100+ Spanish legal phrases)
/// - Real fuzzy phrase matching algorithm
/// </remarks>
public class LegalDirectiveClassifierDictionaryTests
{
    private readonly ILogger<SemanticAnalyzerService> _logger;
    private readonly ITextComparer _textComparer;
    private readonly ISemanticAnalyzer _semanticAnalyzer;

    public LegalDirectiveClassifierDictionaryTests()
    {
        // Use REAL implementations (not mocks) for integration-style testing
        var loggerTextComparer = Substitute.For<ILogger<LevenshteinTextComparer>>();
        _logger = Substitute.For<ILogger<SemanticAnalyzerService>>();

        _textComparer = new LevenshteinTextComparer(loggerTextComparer);
        _semanticAnalyzer = new SemanticAnalyzerService(_textComparer, _logger);
    }

    #region Return Type Tests (Critical Audit Gap)

    /// <summary>
    /// CRITICAL TEST: Verifies that AnalyzeDirectivesAsync returns SemanticAnalysis (not List&lt;ComplianceAction&gt;).
    /// This is the core audit finding - the service must populate the domain model correctly.
    /// </summary>
    [Fact]
    public async Task AnalyzeDirectivesAsync_ReturnsSemanticAnalysis_NotListOfComplianceActions()
    {
        // Arrange
        var documentText = "Se ordena el BLOQUEO de la cuenta 1234567890 por un monto de $1,000,000.00";

        // Act
        var result = await _semanticAnalyzer.AnalyzeDirectivesAsync(documentText, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var semanticAnalysis = result.Value;

        // The returned type should be SemanticAnalysis, not List<ComplianceAction>
        semanticAnalysis.ShouldNotBeNull();
        semanticAnalysis.ShouldBeOfType<SemanticAnalysis>();
    }

    #endregion

    #region Dictionary-Based Classification Tests

    /// <summary>
    /// Tests that the service uses dictionary for "Block" classification.
    /// Should populate RequiereBloqueo with details.
    /// </summary>
    [Fact]
    public async Task AnalyzeDirectivesAsync_BlockDirective_PopulatesRequiereBloqueo()
    {
        // Arrange
        var documentText = "Se ordena el aseguramiento de fondos en la cuenta 1234567890.";

        // Act
        var result = await _semanticAnalyzer.AnalyzeDirectivesAsync(documentText, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var semanticAnalysis = result.Value;

        // Should populate RequiereBloqueo (not return primitive ComplianceAction)
        semanticAnalysis.RequiereBloqueo.ShouldNotBeNull();
        semanticAnalysis.RequiereBloqueo!.EsRequerido.ShouldBeTrue();
        semanticAnalysis.RequiereBloqueo.Confidence.ShouldBeGreaterThanOrEqualTo(0.85);
    }

    /// <summary>
    /// Tests varied phrasing: "aseguramiento de fondos" should match "aseguramiento de los fondos".
    /// This is the exact example from the audit.
    /// </summary>
    [Fact]
    public async Task AnalyzeDirectivesAsync_VariedPhrasing_StillClassifiesCorrectly()
    {
        // Arrange
        var documentText = "Por medio del presente, se ordena el aseguramiento de los fondos en la cuenta 12345.";

        // Act
        var result = await _semanticAnalyzer.AnalyzeDirectivesAsync(documentText, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var semanticAnalysis = result.Value;
        semanticAnalysis.RequiereBloqueo.ShouldNotBeNull();
        semanticAnalysis.RequiereBloqueo!.EsRequerido.ShouldBeTrue();
        semanticAnalysis.RequiereBloqueo.Confidence.ShouldBeGreaterThanOrEqualTo(0.85);
    }

    /// <summary>
    /// Tests typo tolerance: minor typos should still classify correctly.
    /// </summary>
    [Fact]
    public async Task AnalyzeDirectivesAsync_WithTypo_StillClassifies()
    {
        // Arrange
        var documentText = "Se ordena el bloqeo de cuenta."; // Typo: "bloqeo" instead of "bloqueo"

        // Act
        var result = await _semanticAnalyzer.AnalyzeDirectivesAsync(documentText, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var semanticAnalysis = result.Value;
        semanticAnalysis.RequiereBloqueo.ShouldNotBeNull();
        semanticAnalysis.RequiereBloqueo!.EsRequerido.ShouldBeTrue();
    }

    #endregion

    #region Multiple Directives Tests

    /// <summary>
    /// Tests that a single document can have multiple directives detected.
    /// Example: Both Block AND Document requirements.
    /// </summary>
    [Fact]
    public async Task AnalyzeDirectivesAsync_MultipleDirectives_PopulatesMultipleRequirements()
    {
        // Arrange
        var documentText = @"
            RESUELVE:
            Primero: Se ordena el bloqueo de cuenta bancaria número 12345.
            Segundo: Se solicita la documentación correspondiente al expediente.
        ";

        // Act
        var result = await _semanticAnalyzer.AnalyzeDirectivesAsync(documentText, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var semanticAnalysis = result.Value;

        // Both requirements should be populated
        semanticAnalysis.RequiereBloqueo.ShouldNotBeNull();
        semanticAnalysis.RequiereBloqueo!.EsRequerido.ShouldBeTrue();

        semanticAnalysis.RequiereDocumentacion.ShouldNotBeNull();
        semanticAnalysis.RequiereDocumentacion!.EsRequerido.ShouldBeTrue();
    }

    #endregion

    #region Unblock Directive Tests

    /// <summary>
    /// Tests that Unblock directives populate RequiereDesbloqueo.
    /// </summary>
    [Fact]
    public async Task AnalyzeDirectivesAsync_UnblockDirective_PopulatesRequiereDesbloqueo()
    {
        // Arrange
        var documentText = "Se ordena el desbloqueo de la cuenta bancaria 98765.";

        // Act
        var result = await _semanticAnalyzer.AnalyzeDirectivesAsync(documentText, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var semanticAnalysis = result.Value;

        semanticAnalysis.RequiereDesbloqueo.ShouldNotBeNull();
        semanticAnalysis.RequiereDesbloqueo!.EsRequerido.ShouldBeTrue();
        semanticAnalysis.RequiereDesbloqueo.Confidence.ShouldBeGreaterThanOrEqualTo(0.85);
    }

    #endregion

    #region Document Directive Tests

    /// <summary>
    /// Tests that Document directives populate RequiereDocumentacion.
    /// </summary>
    [Fact]
    public async Task AnalyzeDirectivesAsync_DocumentDirective_PopulatesRequiereDocumentacion()
    {
        // Arrange
        var documentText = "Se solicita la presentación de documentación.";

        // Act
        var result = await _semanticAnalyzer.AnalyzeDirectivesAsync(documentText, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var semanticAnalysis = result.Value;

        semanticAnalysis.RequiereDocumentacion.ShouldNotBeNull();
        semanticAnalysis.RequiereDocumentacion!.EsRequerido.ShouldBeTrue();
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests that no match results in SemanticAnalysis with all nulls.
    /// </summary>
    [Fact]
    public async Task AnalyzeDirectivesAsync_NoMatch_ReturnsEmptySemanticAnalysis()
    {
        // Arrange
        var documentText = "Este es un documento sin directivas específicas.";

        // Act
        var result = await _semanticAnalyzer.AnalyzeDirectivesAsync(documentText, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var semanticAnalysis = result.Value;

        // All requirements should be null
        semanticAnalysis.RequiereBloqueo.ShouldBeNull();
        semanticAnalysis.RequiereDesbloqueo.ShouldBeNull();
        semanticAnalysis.RequiereDocumentacion.ShouldBeNull();
        semanticAnalysis.RequiereTransferencia.ShouldBeNull();
        semanticAnalysis.RequiereInformacionGeneral.ShouldBeNull();
    }

    #endregion

    #region Integration with Expediente Context

    /// <summary>
    /// Tests that the service can use Expediente context for better classification.
    /// </summary>
    [Fact]
    public async Task AnalyzeDirectivesAsync_WithExpedienteContext_EnhancesClassification()
    {
        // Arrange
        var documentText = "Se ordena el aseguramiento de fondos.";
        var expediente = new Expediente
        {
            NumeroExpediente = "12345/AS/2024"
        };

        // Act
        var result = await _semanticAnalyzer.AnalyzeDirectivesAsync(documentText, expediente, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var semanticAnalysis = result.Value;
        semanticAnalysis.RequiereBloqueo.ShouldNotBeNull();

        // Confidence might be higher due to expediente context (e.g., "AS" in number suggests "Aseguramiento")
        semanticAnalysis.RequiereBloqueo!.Confidence.ShouldBeGreaterThanOrEqualTo(0.85);
    }

    #endregion
}
