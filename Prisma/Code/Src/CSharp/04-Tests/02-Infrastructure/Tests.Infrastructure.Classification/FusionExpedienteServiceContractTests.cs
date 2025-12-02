namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// ITDD contract tests for <see cref="IFusionExpediente"/> interface.
/// These tests define the expected behavior that ANY implementation of IFusionExpediente must satisfy.
/// </summary>
/// <remarks>
/// Contract Test Philosophy:
/// - Tests focus on WHAT the interface should do, not HOW it's implemented
/// - All assertions use expected values from FusionCoefficients defaults
/// - Tests validate business rules, not implementation details
/// - Covers all fusion decisions: AllAgree, FuzzyAgreement, WeightedVoting, Conflict, AllSourcesNull
/// </remarks>
public class FusionExpedienteServiceContractTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private readonly FusionCoefficients _defaultCoefficients = new();
    private readonly ILogger<FusionExpedienteServiceContractTests> _logger = XUnitLogger.CreateLogger<FusionExpedienteServiceContractTests>(output);

    #region Happy Path Tests - All Sources Agree

    [Fact]
    public async Task FuseAsync_AllSourcesAgreeExactly_ReturnsHighConfidenceAllAgree()
    {
        _logger.LogInformation("=== TEST START: AllSourcesAgreeExactly ===");

        // Arrange - All 3 sources have identical data
        var xmlExpediente = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");
        var pdfExpediente = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");
        var docxExpediente = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");

        var xmlMetadata = CreateHighQualityMetadata(SourceType.XML_HandFilled, regexMatches: 5, totalFields: 15, violations: 0);
        var pdfMetadata = CreateHighQualityMetadata(SourceType.PDF_OCR_CNBV, regexMatches: 3, totalFields: 3, violations: 0);
        var docxMetadata = CreateHighQualityMetadata(SourceType.DOCX_OCR_Authority, regexMatches: 3, totalFields: 3, violations: 0);

        _logger.LogInformation("Test data: All 3 sources agree on NumeroExpediente='{Num}', AreaDescripcion='{Area}'",
            "A/AS1-1111-222222-AAA", "ASEGURAMIENTO");

        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.FuseAsync(
            xmlExpediente, pdfExpediente, docxExpediente,
            xmlMetadata, pdfMetadata, docxMetadata,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var fusion = result.Value;

        _logger.LogInformation("Fusion result - OverallConfidence: {Conf:F4}, NextAction: {Action}, Conflicting fields: {Count}",
            fusion.OverallConfidence, fusion.NextAction, fusion.ConflictingFields.Count);

        fusion.ShouldNotBeNull();
        fusion.FusedExpediente.ShouldNotBeNull();
        fusion.FusedExpediente.NumeroExpediente.ShouldBe("A/AS1-1111-222222-AAA");
        fusion.FusedExpediente.AreaDescripcion.ShouldBe("ASEGURAMIENTO");

        // When all agree, confidence should be very high
        fusion.OverallConfidence.ShouldBeGreaterThanOrEqualTo(_defaultCoefficients.AutoProcessThreshold);
        fusion.NextAction.ShouldBe(NextAction.AutoProcess);
        fusion.ConflictingFields.ShouldBeEmpty();

        // Field-level fusion decision
        _logger.LogInformation("NumeroExpediente fusion: Decision={Dec}, Value={Val}",
            fusion.FieldResults["NumeroExpediente"].Decision, fusion.FieldResults["NumeroExpediente"].Value);
        _logger.LogInformation("AreaDescripcion fusion: Decision={Dec}, Value={Val}",
            fusion.FieldResults["AreaDescripcion"].Decision, fusion.FieldResults["AreaDescripcion"].Value);

        fusion.FieldResults["NumeroExpediente"].Decision.ShouldBe(FusionDecision.AllAgree);
        fusion.FieldResults["AreaDescripcion"].Decision.ShouldBe(FusionDecision.AllAgree);

        _logger.LogInformation("=== TEST PASSED ===");
    }

    #endregion Happy Path Tests - All Sources Agree

    #region Fuzzy Agreement Tests

    [Fact]
    public async Task FuseAsync_MinorTypoInName_ReturnsFuzzyAgreement()
    {
        // Arrange - Name fields have minor typos (fuzzy match)
        var xmlExpediente = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");
        xmlExpediente.AutoridadNombre = "SUBDELEGACION 8 SAN ANGEL";

        var pdfExpediente = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");
        pdfExpediente.AutoridadNombre = "SUBDELEGACIÓN 8 SAN ANGEL"; // Accent difference

        var docxExpediente = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");
        docxExpediente.AutoridadNombre = "SUBDELEGACION 8 SAN ÁNGEL"; // Accent on different letter

        var xmlMetadata = CreateHighQualityMetadata(SourceType.XML_HandFilled);
        var pdfMetadata = CreateHighQualityMetadata(SourceType.PDF_OCR_CNBV);
        var docxMetadata = CreateHighQualityMetadata(SourceType.DOCX_OCR_Authority);

        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.FuseAsync(
            xmlExpediente, pdfExpediente, docxExpediente,
            xmlMetadata, pdfMetadata, docxMetadata,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var fusion = result.Value;

        fusion.FieldResults["AutoridadNombre"].Decision.ShouldBe(FusionDecision.FuzzyAgreement);
        fusion.FieldResults["AutoridadNombre"].FuzzySimilarity.ShouldNotBeNull();
        fusion.FieldResults["AutoridadNombre"].FuzzySimilarity!.Value.ShouldBeGreaterThanOrEqualTo(_defaultCoefficients.FuzzyMatchThreshold);
        fusion.FieldResults["AutoridadNombre"].Value.ShouldNotBeNullOrWhiteSpace();
    }

    #endregion Fuzzy Agreement Tests

    #region Weighted Voting Tests

    [Fact]
    public async Task FuseAsync_TwoSourcesAgreeOneDisagrees_ReturnsWeightedVoting()
    {
        _logger.LogInformation("=== TEST START: TwoSourcesAgreeOneDisagrees ===");

        // Arrange - XML and PDF agree, DOCX disagrees
        var xmlExpediente = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");
        var pdfExpediente = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");
        var docxExpediente = CreateTestExpediente("A/AS1-1111-222222-BBB", "HACENDARIO"); // Different!

        var xmlMetadata = CreateHighQualityMetadata(SourceType.XML_HandFilled);
        var pdfMetadata = CreateHighQualityMetadata(SourceType.PDF_OCR_CNBV);
        var docxMetadata = CreateLowQualityMetadata(SourceType.DOCX_OCR_Authority); // Lower quality

        _logger.LogInformation("Test data: XML/PDF agree on AAA (high quality), DOCX disagrees with BBB (low quality)");
        _logger.LogInformation("Expected: WeightedVoting should select AAA (2 high-quality sources beat 1 low-quality)");

        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.FuseAsync(
            xmlExpediente, pdfExpediente, docxExpediente,
            xmlMetadata, pdfMetadata, docxMetadata,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var fusion = result.Value;

        var numeroField = fusion.FieldResults["NumeroExpediente"];
        _logger.LogInformation("NumeroExpediente fusion result:");
        _logger.LogInformation("  Decision: {Decision} (expected: WeightedVoting)", numeroField.Decision);
        _logger.LogInformation("  Value: {Value} (expected: AAA)", numeroField.Value);
        _logger.LogInformation("  WinningSource: {Source}", numeroField.WinningSource);
        _logger.LogInformation("  Confidence: {Conf:F4}", numeroField.Confidence);

        // PDF (0.85 base) + XML (0.60 base) should win over DOCX (0.70 base with low quality)
        fusion.FusedExpediente.NumeroExpediente.ShouldBe("A/AS1-1111-222222-AAA");
        fusion.FieldResults["NumeroExpediente"].Decision.ShouldBe(FusionDecision.WeightedVoting);
        fusion.FieldResults["NumeroExpediente"].WinningSource.ShouldNotBeNull();
        fusion.ConflictingFields.ShouldContain("NumeroExpediente");

        _logger.LogInformation("=== TEST PASSED ===");
    }

    [Fact]
    public async Task FuseAsync_HighQualityPDFVsLowQualityXML_PDFWins()
    {
        // Arrange - PDF has high OCR confidence, XML has violations
        var xmlExpediente = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");
        var pdfExpediente = CreateTestExpediente("A/AS1-1111-222222-BBB", "HACENDARIO");
        Expediente? docxExpediente = null; // Only 2 sources

        var xmlMetadata = CreateLowQualityMetadata(SourceType.XML_HandFilled);
        var pdfMetadata = CreateHighQualityMetadata(SourceType.PDF_OCR_CNBV);
        var docxMetadata = CreateEmptyMetadata(SourceType.DOCX_OCR_Authority);

        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.FuseAsync(
            xmlExpediente, pdfExpediente, docxExpediente,
            xmlMetadata, pdfMetadata, docxMetadata,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var fusion = result.Value;

        // PDF should win due to higher quality
        fusion.FusedExpediente.NumeroExpediente.ShouldBe("A/AS1-1111-222222-BBB");
        fusion.FieldResults["NumeroExpediente"].WinningSource.ShouldBe(SourceType.PDF_OCR_CNBV);
    }

    #endregion Weighted Voting Tests

    #region Conflict Tests

    [Fact]
    public async Task FuseAsync_AllThreeSourcesDisagree_ReturnsConflict()
    {
        _logger.LogInformation("=== TEST START: AllThreeSourcesDisagree ===");

        // Arrange - All 3 sources have different values
        var xmlExpediente = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");
        var pdfExpediente = CreateTestExpediente("A/AS1-1111-222222-BBB", "HACENDARIO");
        var docxExpediente = CreateTestExpediente("A/AS1-1111-222222-CCC", "PENAL");

        var xmlMetadata = CreateHighQualityMetadata(SourceType.XML_HandFilled);
        var pdfMetadata = CreateHighQualityMetadata(SourceType.PDF_OCR_CNBV);
        var docxMetadata = CreateHighQualityMetadata(SourceType.DOCX_OCR_Authority);

        _logger.LogInformation("Test data: All 3 sources disagree (AAA, BBB, CCC) - all high quality");
        _logger.LogInformation("Expected: Conflict (no clear winner when all sources disagree with equal quality)");

        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.FuseAsync(
            xmlExpediente, pdfExpediente, docxExpediente,
            xmlMetadata, pdfMetadata, docxMetadata,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var fusion = result.Value;

        var numeroField = fusion.FieldResults["NumeroExpediente"];
        _logger.LogInformation("NumeroExpediente fusion result:");
        _logger.LogInformation("  Decision: {Decision} (expected: Conflict)", numeroField.Decision);
        _logger.LogInformation("  Value: {Value}", numeroField.Value);
        _logger.LogInformation("  RequiresManualReview: {Review}", numeroField.RequiresManualReview);
        _logger.LogInformation("  Confidence: {Conf:F4}", numeroField.Confidence);
        _logger.LogInformation("Overall - NextAction: {Action} (expected: ManualReviewRequired), OverallConfidence: {Conf:F4}",
            fusion.NextAction, fusion.OverallConfidence);

        fusion.FieldResults["NumeroExpediente"].Decision.ShouldBe(FusionDecision.Conflict);
        fusion.FieldResults["NumeroExpediente"].RequiresManualReview.ShouldBeTrue();
        fusion.ConflictingFields.ShouldContain("NumeroExpediente");
        fusion.NextAction.ShouldBe(NextAction.ManualReviewRequired);
        // OverallConfidence averages ALL fields, so it can be high even with one conflict
        // NextAction correctly returns ManualReviewRequired when ANY field has a conflict

        _logger.LogInformation("=== TEST PASSED ===");
    }

    #endregion Conflict Tests

    #region Null Handling Tests

    [Fact]
    public async Task FuseAsync_AllSourcesNull_ReturnsFailure()
    {
        // Arrange
        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.FuseAsync(
            null, null, null,
            CreateEmptyMetadata(SourceType.XML_HandFilled),
            CreateEmptyMetadata(SourceType.PDF_OCR_CNBV),
            CreateEmptyMetadata(SourceType.DOCX_OCR_Authority),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("At least one source");
    }

    [Fact]
    public async Task FuseAsync_TwoSourcesNullOneValid_ReturnsValidFusion()
    {
        _logger.LogInformation("=== TEST START: TwoSourcesNullOneValid ===");

        // Arrange - Only XML is available
        var xmlExpediente = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");
        var xmlMetadata = CreateHighQualityMetadata(SourceType.XML_HandFilled);

        _logger.LogInformation("Test data: Only XML source available, PDF and DOCX are null");
        _logger.LogInformation("Expected: ManualReviewRequired (single XML source has ~0.67 reliability < 0.70 threshold)");

        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.FuseAsync(
            xmlExpediente, null, null,
            xmlMetadata,
            CreateEmptyMetadata(SourceType.PDF_OCR_CNBV),
            CreateEmptyMetadata(SourceType.DOCX_OCR_Authority),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var fusion = result.Value;

        _logger.LogInformation("Fusion result - NextAction: {Action} (expected: ManualReviewRequired), OverallConfidence: {Conf:F4}",
            fusion.NextAction, fusion.OverallConfidence);

        fusion.FusedExpediente.ShouldNotBeNull();
        fusion.FusedExpediente.NumeroExpediente.ShouldBe("A/AS1-1111-222222-AAA");
        // Single XML source has ~0.67 reliability, which is < 0.70 ManualReviewThreshold
        fusion.NextAction.ShouldBe(NextAction.ManualReviewRequired); // Single low-reliability source

        _logger.LogInformation("=== TEST PASSED ===");
    }

    #endregion Null Handling Tests

    #region Field-Level Fusion Tests

    [Fact]
    public async Task FuseFieldAsync_ExactMatch_ReturnsAllAgree()
    {
        _logger.LogInformation("=== TEST START: FuseFieldAsync_ExactMatch ===");

        // Arrange
        var candidates = new List<FieldCandidate>
        {
            new() { Value = "ASEGURAMIENTO", Source = SourceType.XML_HandFilled, SourceReliability = 0.75 },
            new() { Value = "ASEGURAMIENTO", Source = SourceType.PDF_OCR_CNBV, SourceReliability = 0.90 },
            new() { Value = "ASEGURAMIENTO", Source = SourceType.DOCX_OCR_Authority, SourceReliability = 0.80 }
        };

        _logger.LogInformation("Test data: All 3 sources have exact match 'ASEGURAMIENTO' with reliabilities: 0.75, 0.90, 0.80");
        _logger.LogInformation("Expected: AllAgree decision with confidence >= 0.85 (average of reliabilities)");

        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.FuseFieldAsync("AreaDescripcion", candidates, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        _logger.LogInformation("FuseField result:");
        _logger.LogInformation("  Decision: {Decision} (expected: AllAgree)", result.Value.Decision);
        _logger.LogInformation("  Value: {Value} (expected: ASEGURAMIENTO)", result.Value.Value);
        _logger.LogInformation("  Confidence: {Conf:F4} (expected: ~0.8167, average of 0.75, 0.90, 0.80)", result.Value.Confidence);

        result.Value.Decision.ShouldBe(FusionDecision.AllAgree);
        result.Value.Value.ShouldBe("ASEGURAMIENTO");
        // Average of 0.75, 0.90, 0.80 = 0.8166...
        result.Value.Confidence.ShouldBeGreaterThanOrEqualTo(0.80);

        _logger.LogInformation("=== TEST PASSED ===");
    }

    [Fact]
    public async Task FuseFieldAsync_AllNull_ReturnsAllSourcesNull()
    {
        // Arrange
        var candidates = new List<FieldCandidate>
        {
            new() { Value = null, Source = SourceType.XML_HandFilled, SourceReliability = 0.75 },
            new() { Value = null, Source = SourceType.PDF_OCR_CNBV, SourceReliability = 0.90 },
            new() { Value = null, Source = SourceType.DOCX_OCR_Authority, SourceReliability = 0.80 }
        };

        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.FuseFieldAsync("OptionalField", candidates, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Decision.ShouldBe(FusionDecision.AllSourcesNull);
        result.Value.Value.ShouldBeNull();
    }

    #endregion Field-Level Fusion Tests

    #region Helper Methods

    private IFusionExpediente CreateSystemUnderTest()
    {
        // Use real logger instead of mock for better debugging
        var logger = XUnitLogger.CreateLogger<FusionExpedienteService>(_output);
        var coefficients = new FusionCoefficients(); // Use default coefficients
        return new FusionExpedienteService(logger, coefficients);
    }

    private static Expediente CreateTestExpediente(string numeroExpediente, string areaDescripcion)
    {
        return new Expediente
        {
            NumeroExpediente = numeroExpediente,
            AreaDescripcion = areaDescripcion,
            NumeroOficio = "123/ABC/-4444444444/2025",
            SolicitudSiara = "TEST/2025/000001",
            AutoridadNombre = "TEST AUTORIDAD"
        };
    }

    private static ExtractionMetadata CreateHighQualityMetadata(
        SourceType source,
        int regexMatches = 5,
        int totalFields = 15,
        int violations = 0)
    {
        return new ExtractionMetadata
        {
            Source = source,
            RegexMatches = regexMatches,
            TotalFieldsExtracted = totalFields,
            CatalogValidations = 2,
            PatternViolations = violations,
            MeanConfidence = source == SourceType.XML_HandFilled ? null : 0.85,
            MinConfidence = source == SourceType.XML_HandFilled ? null : 0.75,
            QualityIndex = source == SourceType.XML_HandFilled ? null : 0.80
        };
    }

    private static ExtractionMetadata CreateLowQualityMetadata(SourceType source)
    {
        return new ExtractionMetadata
        {
            Source = source,
            RegexMatches = 1,
            TotalFieldsExtracted = 5,
            CatalogValidations = 0,
            PatternViolations = 3,
            MeanConfidence = source == SourceType.XML_HandFilled ? null : 0.55,
            MinConfidence = source == SourceType.XML_HandFilled ? null : 0.40,
            QualityIndex = source == SourceType.XML_HandFilled ? null : 0.50
        };
    }

    private static ExtractionMetadata CreateEmptyMetadata(SourceType source)
    {
        return new ExtractionMetadata
        {
            Source = source,
            RegexMatches = 0,
            TotalFieldsExtracted = 0,
            CatalogValidations = 0,
            PatternViolations = 0
        };
    }

    #endregion Helper Methods
}