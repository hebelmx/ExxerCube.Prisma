namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// TDD tests for <see cref="ITextComparer.FindBestMatch"/> method.
/// These tests specify the expected behavior for dictionary-based fuzzy phrase matching.
/// </summary>
/// <remarks>
/// Testing Strategy:
/// - Fuzzy matching with varied phrasing (e.g., "aseguramiento" vs "aseguramiento de los fondos")
/// - Multiple directive detection in single document
/// - Typo tolerance (≥85% similarity threshold)
/// - Case insensitivity
/// - Sliding window matching for long phrases
/// - No match scenarios (below threshold)
/// </remarks>
public class TextComparerFindBestMatchTests
{
    private readonly ITextComparer _textComparer;
    private readonly ILogger<LevenshteinTextComparer> _logger;

    public TextComparerFindBestMatchTests()
    {
        _logger = Substitute.For<ILogger<LevenshteinTextComparer>>();
        _textComparer = new LevenshteinTextComparer(_logger);
    }

    #region Fuzzy Phrase Matching Tests

    /// <summary>
    /// Tests that FindBestMatch finds exact phrase match with 100% similarity.
    /// </summary>
    [Fact]
    public void FindBestMatch_ExactPhrase_Returns100PercentSimilarity()
    {
        // Arrange
        var phrase = "aseguramiento de fondos";
        var text = "Por medio del presente, se ordena el aseguramiento de fondos en la cuenta 12345.";
        var threshold = 0.85;

        // Act
        var result = _textComparer.FindBestMatch(phrase, text, threshold);

        // Assert
        result.ShouldNotBeNull("Exact phrase should be found");
        result.MatchedText.ShouldBe("aseguramiento de fondos");
        result.Similarity.ShouldBe(1.0, 0.01, "Exact match should have 100% similarity");
        result.StartIndex.ShouldBeGreaterThanOrEqualTo(0);
        result.Length.ShouldBe(phrase.Length);
    }

    /// <summary>
    /// Tests that FindBestMatch handles slight variations in phrasing (the audit's key example).
    /// Example: Search for "aseguramiento de fondos" should match "aseguramiento de los fondos".
    /// </summary>
    [Fact]
    public void FindBestMatch_SlightlyVariedPhrase_FindsMatch()
    {
        // Arrange
        var phrase = "aseguramiento de fondos";
        var text = "Por medio del presente, se ordena el aseguramiento de los fondos en la cuenta 12345.";
        var threshold = 0.85;

        // Act
        var result = _textComparer.FindBestMatch(phrase, text, threshold);

        // Assert
        result.ShouldNotBeNull("Phrase with minor variation should be found");
        result.MatchedText.ShouldContain("aseguramiento");
        result.Similarity.ShouldBeGreaterThanOrEqualTo(threshold, "Should meet minimum threshold");
        result.Similarity.ShouldBeLessThan(1.0, "Should not be perfect match due to 'de los' variation");
    }

    /// <summary>
    /// Tests case-insensitive matching.
    /// </summary>
    [Fact]
    public void FindBestMatch_CaseInsensitive_FindsMatch()
    {
        // Arrange
        var phrase = "bloqueo de cuenta";
        var text = "SE ORDENA EL BLOQUEO DE CUENTA BANCARIA NÚMERO 98765.";
        var threshold = 0.85;

        // Act
        var result = _textComparer.FindBestMatch(phrase, text, threshold);

        // Assert
        result.ShouldNotBeNull("Case-insensitive match should be found");
        result.Similarity.ShouldBeGreaterThanOrEqualTo(threshold);
    }

    #endregion

    #region Typo Tolerance Tests

    /// <summary>
    /// Tests that FindBestMatch tolerates minor typos (1-2 character differences).
    /// Example: "aseguramento" (typo) should match "aseguramiento" (correct).
    /// </summary>
    [Fact]
    public void FindBestMatch_WithMinorTypo_FindsMatch()
    {
        // Arrange
        var phrase = "aseguramento de fondos"; // Missing 'i' in aseguramiento
        var text = "Se ordena el aseguramiento de fondos en la cuenta.";
        var threshold = 0.85;

        // Act
        var result = _textComparer.FindBestMatch(phrase, text, threshold);

        // Assert
        result.ShouldNotBeNull("Minor typo should still match with fuzzy logic");
        result.MatchedText.ShouldContain("aseguramiento");
        result.Similarity.ShouldBeGreaterThanOrEqualTo(threshold);
    }

    /// <summary>
    /// Tests that FindBestMatch rejects matches with too many typos (below threshold).
    /// </summary>
    [Fact]
    public void FindBestMatch_WithManyTypos_ReturnsNull()
    {
        // Arrange
        var phrase = "aseguramiento de fondos";
        var text = "Se ordena el sequestro de dineros en la cuenta."; // Very different phrasing
        var threshold = 0.85;

        // Act
        var result = _textComparer.FindBestMatch(phrase, text, threshold);

        // Assert
        result.ShouldBeNull("Match below threshold should return null");
    }

    #endregion

    #region Sliding Window and Long Text Tests

    /// <summary>
    /// Tests that FindBestMatch uses sliding window to find best match in long text.
    /// </summary>
    [Fact]
    public void FindBestMatch_LongDocument_FindsBestMatchInMiddle()
    {
        // Arrange
        var phrase = "congelamiento de recursos";
        var text = @"
            CONSIDERANDO:
            Que la autoridad competente ha determinado que procede ordenar medidas cautelares.

            RESUELVE:
            Primero: Se ordena el congelamiento de recursos económicos de la cuenta bancaria.
            Segundo: La presente resolución es de carácter definitivo.
        ";
        var threshold = 0.85;

        // Act
        var result = _textComparer.FindBestMatch(phrase, text, threshold);

        // Assert
        result.ShouldNotBeNull("Should find match in middle of long text");
        result.MatchedText.ShouldContain("congelamiento");
        result.StartIndex.ShouldBeGreaterThan(0, "Match should not be at start");
    }

    /// <summary>
    /// Tests that FindBestMatch returns the BEST match when multiple similar phrases exist.
    /// </summary>
    [Fact]
    public void FindBestMatch_MultipleMatches_ReturnsBestOne()
    {
        // Arrange
        var phrase = "bloqueo de cuenta";
        var text = @"
            Se ordena el bloqueo temporal.
            Asimismo, se ordena el bloqueo de cuenta bancaria número 12345.
        ";
        var threshold = 0.80;

        // Act
        var result = _textComparer.FindBestMatch(phrase, text, threshold);

        // Assert
        result.ShouldNotBeNull();
        result.MatchedText.ShouldContain("cuenta"); // Should prefer more complete match
        result.Similarity.ShouldBeGreaterThan(0.90); // Best match should have high similarity
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests FindBestMatch with empty text.
    /// </summary>
    [Fact]
    public void FindBestMatch_EmptyText_ReturnsNull()
    {
        // Arrange
        var phrase = "bloqueo de cuenta";
        var text = string.Empty;
        var threshold = 0.85;

        // Act
        var result = _textComparer.FindBestMatch(phrase, text, threshold);

        // Assert
        result.ShouldBeNull("Empty text should return null");
    }

    /// <summary>
    /// Tests FindBestMatch with null text.
    /// </summary>
    [Fact]
    public void FindBestMatch_NullText_ReturnsNull()
    {
        // Arrange
        var phrase = "bloqueo de cuenta";
        string? text = null;
        var threshold = 0.85;

        // Act
        var result = _textComparer.FindBestMatch(phrase, text!, threshold);

        // Assert
        result.ShouldBeNull("Null text should return null");
    }

    /// <summary>
    /// Tests FindBestMatch with custom threshold.
    /// </summary>
    [Fact]
    public void FindBestMatch_CustomThreshold_RespectsThreshold()
    {
        // Arrange
        var phrase = "aseguramiento de fondos";
        var text = "Se ordena el aseguramiento de los recursos financieros."; // Moderate variation
        var strictThreshold = 0.95; // Very strict

        // Act
        var result = _textComparer.FindBestMatch(phrase, text, strictThreshold);

        // Assert
        // This might return null with strict threshold, depending on similarity score
        if (result != null)
        {
            result.Similarity.ShouldBeGreaterThanOrEqualTo(strictThreshold);
        }
    }

    #endregion

    #region TextMatchResult Properties Tests

    /// <summary>
    /// Tests that TextMatchResult provides correct position information.
    /// </summary>
    [Fact]
    public void FindBestMatch_Result_ProvidesCorrectPositionInfo()
    {
        // Arrange
        var phrase = "desbloqueo de cuenta";
        var text = "Por lo anterior, se ordena el desbloqueo de cuenta inmediato.";
        var threshold = 0.85;

        // Act
        var result = _textComparer.FindBestMatch(phrase, text, threshold);

        // Assert
        result.ShouldNotBeNull();
        result.StartIndex.ShouldBeGreaterThanOrEqualTo(0);
        result.Length.ShouldBeGreaterThan(0);
        result.EndIndex.ShouldBe(result.StartIndex + result.Length);

        // Verify we can extract the matched text using position info
        var extractedText = text.Substring(result.StartIndex, result.Length);
        extractedText.ShouldBe(result.MatchedText);
    }

    /// <summary>
    /// Tests that TextMatchResult provides similarity percentage.
    /// </summary>
    [Fact]
    public void FindBestMatch_Result_ProvidesSimilarityPercentage()
    {
        // Arrange
        var phrase = "transferencia de fondos";
        var text = "Se autoriza la transferencia de fondos por $100,000.";
        var threshold = 0.85;

        // Act
        var result = _textComparer.FindBestMatch(phrase, text, threshold);

        // Assert
        result.ShouldNotBeNull();
        result.SimilarityPercentage.ShouldBe(result.Similarity * 100.0, 0.01);
        result.SimilarityPercentage.ShouldBeGreaterThanOrEqualTo(threshold * 100.0);
    }

    #endregion
}
