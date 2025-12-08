using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Matching;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract;

/// <summary>
/// TDD tests for MexicanNameFuzzyMatcher.
/// CRITICAL: Fuzzy matching ONLY for Mexican names with common variations.
/// Examples that SHOULD fuzzy match:
/// - Pérez/Perez (accent variations)
/// - González/Gonzales/Gonzalez (accent + spelling variations)
/// - Christian/Cristian (spelling variations)
/// - José/Jose (accent variations)
///
/// MUST NOT fuzzy match (require exact):
/// - RFCs (XAXX010101000 vs XAXX010101001 = different)
/// - CURPs (exact identifier)
/// - Account numbers (1234567890 vs 1234567891 = different)
/// - Expedientes (A/AS1-2505-088637-PHM = exact)
/// - Amounts ($100,000.00 vs $100,000.01 = different)
/// - Dates (2025-01-01 vs 2025-01-02 = different)
/// </summary>
public sealed class MexicanNameFuzzyMatcherTests
{
    [Theory]
    [InlineData("Pérez", "Perez", true, "accent variation should match")]
    [InlineData("González", "Gonzales", true, "accent + spelling variation should match")]
    [InlineData("González", "Gonzalez", true, "accent variation should match")]
    [InlineData("José", "Jose", true, "accent variation should match")]
    [InlineData("María", "Maria", true, "accent variation should match")]
    [InlineData("Rodríguez", "Rodriguez", true, "accent variation should match")]
    [InlineData("Christian", "Cristian", true, "spelling variation should match")]
    [InlineData("Cristián", "Christian", true, "accent + spelling variation should match")]
    public void IsMatch_MexicanNameVariations_ReturnsTrue(string name1, string name2, bool expected, string reason)
    {
        // Arrange
        var matcher = new MexicanNameFuzzyMatcher();

        // Act
        var result = matcher.IsMatch(name1, name2);

        // Assert
        result.Should().Be(expected, reason);
    }

    [Theory]
    [InlineData("XAXX010101000", "XAXX010101001", false, "RFCs must match exactly")]
    [InlineData("XAXX010101000", "YAXX010101000", false, "RFCs with different letters must not match")]
    [InlineData("1234567890", "1234567891", false, "account numbers must match exactly")]
    [InlineData("A/AS1-2505-088637-PHM", "A/AS1-2505-088638-PHM", false, "expedientes must match exactly")]
    [InlineData("$100,000.00", "$100,000.01", false, "amounts must match exactly")]
    [InlineData("2025-01-01", "2025-01-02", false, "dates must match exactly")]
    public void IsMatch_NonNameFields_RequiresExactMatch(string value1, string value2, bool expected, string reason)
    {
        // Arrange
        var matcher = new MexicanNameFuzzyMatcher();

        // Act
        var result = matcher.IsMatch(value1, value2);

        // Assert
        result.Should().Be(expected, reason);
    }

    [Theory]
    [InlineData("Smith", "Smyth", false, "non-Mexican name variations should not match")]
    [InlineData("John", "Jon", false, "English name variations should not match")]
    [InlineData("García", "Garcia", true, "Mexican name accent variation should match")]
    [InlineData("Hernández", "Hernandez", true, "Mexican name accent variation should match")]
    public void IsMatch_MexicanVsNonMexicanNames_OnlyMatchesMexican(string name1, string name2, bool expected, string reason)
    {
        // Arrange
        var matcher = new MexicanNameFuzzyMatcher();

        // Act
        var result = matcher.IsMatch(name1, name2);

        // Assert
        result.Should().Be(expected, reason);
    }

    [Theory]
    [InlineData("Pérez García", "Perez Garcia", true, "full name with accent variations should match")]
    [InlineData("José María González", "Jose Maria Gonzales", true, "full name with multiple accent variations should match")]
    [InlineData("Juan Pérez", "Juan Perez", true, "partial accent variation should match")]
    public void IsMatch_FullNames_MatchesWithVariations(string name1, string name2, bool expected, string reason)
    {
        // Arrange
        var matcher = new MexicanNameFuzzyMatcher();

        // Act
        var result = matcher.IsMatch(name1, name2);

        // Assert
        result.Should().Be(expected, reason);
    }

    [Fact]
    public void IsMatch_CaseSensitive_IgnoresCase()
    {
        // Arrange
        var matcher = new MexicanNameFuzzyMatcher();

        // Act & Assert
        matcher.IsMatch("PÉREZ", "perez").Should().BeTrue("should ignore case");
        matcher.IsMatch("González", "GONZALEZ").Should().BeTrue("should ignore case");
        matcher.IsMatch("pérez", "PEREZ").Should().BeTrue("should ignore case");
    }

    [Theory]
    [InlineData("Pérez", "Perez", 100, "perfect match with accent normalization")]
    [InlineData("González", "Gonzales", 85, "high similarity with spelling variation (best-effort OCR: 88%)")]
    [InlineData("Smith", "Smyth", 80, "high similarity (best-effort OCR: 80%)")]
    public void GetSimilarityScore_MexicanNames_ReturnsCorrectScore(string name1, string name2, int minExpectedScore, string reason)
    {
        // Arrange
        var matcher = new MexicanNameFuzzyMatcher();

        // Act
        var score = matcher.GetSimilarityScore(name1, name2);

        // Assert
        score.Should().BeGreaterThanOrEqualTo(minExpectedScore, reason);
    }

    [Fact]
    public void MatchThreshold_Returns85Percent()
    {
        // Arrange
        var matcher = new MexicanNameFuzzyMatcher();

        // Act
        var threshold = matcher.MatchThreshold;

        // Assert
        threshold.Should().Be(85, "Mexican name fuzzy matching uses 85% similarity threshold (best-effort OCR)");
    }

    [Theory]
    [InlineData("Pérez", true, "single Mexican surname")]
    [InlineData("González", true, "Mexican surname")]
    [InlineData("José", true, "Mexican first name")]
    [InlineData("XAXX010101000", false, "RFC pattern")]
    [InlineData("1234567890", false, "numeric account")]
    [InlineData("A/AS1-2505-088637-PHM", false, "expediente pattern")]
    [InlineData("$100,000.00", false, "amount pattern")]
    public void IsNameField_DetectsNameVsNonNameFields(string value, bool expected, string reason)
    {
        // Arrange
        var matcher = new MexicanNameFuzzyMatcher();

        // Act
        var result = matcher.IsNameField(value);

        // Assert
        result.Should().Be(expected, reason);
    }

    [Fact]
    public void IsMatch_NullOrEmpty_ReturnsFalse()
    {
        // Arrange
        var matcher = new MexicanNameFuzzyMatcher();

        // Act & Assert
        matcher.IsMatch(null!, "Pérez").Should().BeFalse("null should not match");
        matcher.IsMatch("Pérez", null!).Should().BeFalse("null should not match");
        matcher.IsMatch("", "Pérez").Should().BeFalse("empty should not match");
        matcher.IsMatch("Pérez", "").Should().BeFalse("empty should not match");
        matcher.IsMatch(null!, null!).Should().BeFalse("both null should not match");
    }

    [Theory]
    [InlineData("Pérez López", "Perez Lopez", true, "compound surname with accents")]
    [InlineData("De la Cruz", "De La Cruz", true, "compound surname with case variation")]
    [InlineData("del Valle", "Del Valle", true, "compound surname with case variation")]
    public void IsMatch_CompoundSurnames_HandlesCorrectly(string name1, string name2, bool expected, string reason)
    {
        // Arrange
        var matcher = new MexicanNameFuzzyMatcher();

        // Act
        var result = matcher.IsMatch(name1, name2);

        // Assert
        result.Should().Be(expected, reason);
    }
}
