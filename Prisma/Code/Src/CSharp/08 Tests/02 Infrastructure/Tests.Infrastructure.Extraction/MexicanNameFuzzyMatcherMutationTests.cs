using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Matching;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Mutation-killing tests for <see cref="MexicanNameFuzzyMatcher"/>. The class is otherwise only
/// covered by the flaky <c>Tests.Infrastructure.Extraction.Teseract</c> suite (out of scope for
/// the deterministic Stryker run), so these are written fresh in this project.
///
/// Pins every observable branch:
/// - <c>IsMatch</c> guards (null/whitespace), the non-name exact-ordinal path, the
///   non-Mexican normalized-exact path, the both-Mexican fuzzy <c>&gt;= 85</c> path, and the two
///   short-circuit <c>||</c> operators that route between those paths.
/// - <c>GetSimilarityScore</c> exact values (0 guard, 100 after normalize) which pin
///   diacritic-removal, lower-casing and whitespace collapse/trim.
/// - <c>IsNameField</c> special-char (<c>$</c>/<c>/</c>) exclusion, the <c>0.80</c> letter-ratio
///   boundary and its arithmetic, and a name-positive.
/// - <c>IsLikelyMexicanName</c> (private, via <c>IsMatch</c>) accent / set-membership / <c>ez</c>-<c>es</c>
///   ending branches, each isolated with a near-but-different pair so the result flips when the
///   branch is removed.
/// - <c>MatchThreshold</c>.
/// </summary>
public class MexicanNameFuzzyMatcherMutationTests
{
    private readonly MexicanNameFuzzyMatcher _matcher = new();

    // ---------------------------------------------------------------------------------------------
    // IsMatch — null / whitespace guard (line 59 `||`)
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(null, "Pérez")]
    [InlineData("Pérez", null)]
    [InlineData("", "Pérez")]
    [InlineData("Pérez", "")]
    [InlineData("   ", "Pérez")]
    [InlineData("Pérez", "   ")]
    [InlineData(null, null)]
    public void IsMatch_NullOrWhitespaceEither_ReturnsFalse(string? value1, string? value2)
    {
        // Each side independently trips the guard (kills `||`->`&&` and either-operand removal).
        _matcher.IsMatch(value1, value2).ShouldBeFalse();
    }

    [Fact]
    public void IsMatch_BothValidMexicanIdentical_ReturnsTrue()
    {
        // A clear true so the guard's `return false` can't be mutated to always-false.
        _matcher.IsMatch("Pérez", "Pérez").ShouldBeTrue();
    }

    // ---------------------------------------------------------------------------------------------
    // IsMatch — non-name exact-ordinal path (lines 66-69)
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void IsMatch_NonNameFieldsExactlyEqual_ReturnsTrue()
    {
        // Two identical RFCs are non-name fields -> exact ordinal compare -> equal -> true.
        _matcher.IsMatch("XAXX010101000", "XAXX010101000").ShouldBeTrue();
    }

    [Theory]
    [InlineData("XAXX010101000", "XAXX010101001")] // RFC differs by one char
    [InlineData("1234567890", "1234567891")]        // account number
    [InlineData("A/AS1-2505-088637-PHM", "A/AS1-2505-088638-PHM")] // expediente
    [InlineData("$100,000.00", "$100,000.01")]      // amount
    [InlineData("2025-01-01", "2025-01-02")]        // date
    public void IsMatch_NonNameFieldsNotExactlyEqual_ReturnsFalse(string value1, string value2)
    {
        // Non-name fields must NOT fuzzy match; near-misses stay false.
        _matcher.IsMatch(value1, value2).ShouldBeFalse();
    }

    [Fact]
    public void IsMatch_OneSideNonNameFieldNearlyMatchingName_ReturnsFalse()
    {
        // A name vs a non-name string ("Lopez Garcia/" has a '/') must take the exact-ordinal path.
        // If the `!IsNameField(v1) || !IsNameField(v2)` `||` is mutated to `&&`, this would fall
        // through to the fuzzy path (both look Mexican via "lopez") and the ~96 ratio would wrongly
        // return true. The raw strings differ, so the correct answer is false.
        _matcher.IsMatch("Lopez Garcia", "Lopez Garcia/").ShouldBeFalse();
    }

    // ---------------------------------------------------------------------------------------------
    // IsMatch — non-Mexican normalized-exact path (lines 73-76)
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void IsMatch_NonMexicanNamesNormalizeEqual_ReturnsTrue()
    {
        // Both look like names but are non-Mexican -> normalized exact compare. Same letters, only
        // case differs -> normalized equal -> true (kills the branch's `return false` / Equals).
        _matcher.IsMatch("Smith", "smith").ShouldBeTrue();
    }

    [Theory]
    [InlineData("Smith", "Smyth")] // one-letter spelling variation
    [InlineData("John", "Jon")]    // English name variation
    public void IsMatch_NonMexicanNamesNotEqual_ReturnsFalse(string value1, string value2)
    {
        // Non-Mexican names require a normalized EXACT match (no fuzzy), so near-misses are false.
        _matcher.IsMatch(value1, value2).ShouldBeFalse();
    }

    [Fact]
    public void IsMatch_OneMexicanOneNonMexicanNearMatch_ReturnsFalse()
    {
        // "González" is Mexican; "Gonzalex" (ends "ex", not in set, no accent) is NOT Mexican.
        // Correct: `!mex(v1) || !mex(v2)` is true -> normalized-exact -> "gonzalez" != "gonzalex"
        // -> false. If that `||` is mutated to `&&`, it would fall to the fuzzy path (~88 >= 85)
        // and wrongly return true. Asserting false kills the `||`->`&&` mutant on line 73.
        _matcher.IsMatch("González", "Gonzalex").ShouldBeFalse();
    }

    // ---------------------------------------------------------------------------------------------
    // IsMatch — both-Mexican fuzzy path + threshold (lines 79-86)
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Pérez", "Perez", "accent variation")]
    [InlineData("González", "Gonzales", "accent + spelling variation (~88 >= 85)")]
    [InlineData("González", "Gonzalez", "accent variation")]
    [InlineData("José", "Jose", "accent variation")]
    [InlineData("Rodríguez", "Rodriguez", "accent variation")]
    [InlineData("Pérez García", "Perez Garcia", "full name accent variations")]
    [InlineData("José María González", "Jose Maria Gonzales", "multi-token accent variations")]
    public void IsMatch_MexicanVariationsAboveThreshold_ReturnsTrue(string value1, string value2, string reason)
    {
        _matcher.IsMatch(value1, value2).ShouldBe(true, reason);
    }

    [Fact]
    public void IsMatch_BothMexicanButBelowThreshold_ReturnsFalse()
    {
        // Two distinct Mexican surnames: both pass IsLikelyMexicanName (surname set / "ez" ending)
        // so the fuzzy path is taken, but the ratio is well below 85 -> false. Kills the constant
        // `85`->`0` mutant (which would make this true) and confirms the threshold is meaningful.
        _matcher.IsMatch("Pérez", "Gómez").ShouldBeFalse();
    }

    // ---------------------------------------------------------------------------------------------
    // IsLikelyMexicanName branches (private — exercised via IsMatch).
    // Each pair is Mexican ONLY via the targeted branch and differs after normalization, so the
    // fuzzy result (true) flips to the normalized-exact result (false) when the branch is removed.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void IsMatch_MexicanViaAccentOnly_ReturnsTrue()
    {
        // "Cárdenas"/"Cárdenaz" -> normalize "cardenas"/"cardenaz" (~88, differ by one char).
        // Neither is in the name sets and neither ends in ez/es ("as"/"az"); both qualify as
        // Mexican ONLY through the accent ('á') detector. Removing the accent branch -> both
        // non-Mexican -> normalized-exact -> false. (8-letter so the single-char delta stays >= 85.)
        _matcher.IsMatch("Cárdenas", "Cárdenaz").ShouldBeTrue();
    }

    [Fact]
    public void IsMatch_MexicanViaEnyeAccentOnly_ReturnsTrue()
    {
        // "Peña"/"Peñas" -> normalize "pena"/"penas" (~89, differ). Mexican ONLY via 'ñ'.
        // Kills removal of the ñ entry from the accent set.
        _matcher.IsMatch("Peña", "Peñas").ShouldBeTrue();
    }

    [Fact]
    public void IsMatch_MexicanViaLeadingAccentAtIndexZero_ReturnsTrue()
    {
        // "Ávila"/"Ávilas" -> normalize "avila"/"avilas" (~91, differ by one char). The accent 'Á'
        // sits at index 0 and is the ONLY Mexican signal (neither is in the sets; "la"/"as"
        // endings). With `IndexOfAny(...) >= 0` index 0 counts as found; mutating to `> 0` would
        // miss the index-0 accent -> both non-Mexican -> normalized-exact -> false. Kills L159
        // `>=`->`>`.
        _matcher.IsMatch("Ávila", "Ávilas").ShouldBeTrue();
    }

    [Fact]
    public void IsMatch_MexicanViaNameSetOnly_ReturnsTrue()
    {
        // "Cristian"/"Christian" are BOTH in SpanishGivenNames (no accent; end "an", not ez/es),
        // and normalize differently (inserted 'h', ~94). Mexican ONLY via the set-membership
        // branch -> removing `SpanishGivenNames.Contains(...)` makes both non-Mexican -> false.
        _matcher.IsMatch("Cristian", "Christian").ShouldBeTrue();
    }

    [Fact]
    public void IsMatch_MexicanViaEzEsEndingOnly_ReturnsTrue()
    {
        // "Gutierrez"/"Gutierres" -> normalize "gutierrez"/"gutierres" (~88, differ by one char).
        // Neither is in the sets and neither has an accent; v1 is Mexican only via the "ez" ending,
        // v2 only via "es". Removing either ending operand makes one side non-Mexican ->
        // normalized-exact -> false, so this single pair kills both the "ez" and "es" removals.
        // (9-letter so the single-char delta stays >= 85.)
        _matcher.IsMatch("Gutierrez", "Gutierres").ShouldBeTrue();
    }

    [Fact]
    public void IsMatch_CaseInsensitiveMexican_ReturnsTrue()
    {
        // Pins the ToLowerInvariant in NormalizeForComparison (and OrdinalIgnoreCase set lookups).
        _matcher.IsMatch("PÉREZ", "perez").ShouldBeTrue();
        _matcher.IsMatch("González", "GONZALEZ").ShouldBeTrue();
    }

    // ---------------------------------------------------------------------------------------------
    // GetSimilarityScore — exact values pin normalize / diacritics / whitespace / casing
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(null, "Pérez")]
    [InlineData("Pérez", null)]
    [InlineData("", "Pérez")]
    [InlineData("Pérez", "")]
    [InlineData("   ", "Pérez")]
    public void GetSimilarityScore_NullOrWhitespace_ReturnsZero(string? value1, string? value2)
    {
        _matcher.GetSimilarityScore(value1, value2).ShouldBe(0);
    }

    [Fact]
    public void GetSimilarityScore_AccentVsPlain_Returns100()
    {
        // "Pérez" and "Perez" normalize identically ONLY if diacritics are removed. A perfect 100
        // proves RemoveDiacritics ran; skipping it would leave "pérez" != "perez" (< 100).
        _matcher.GetSimilarityScore("Pérez", "Perez").ShouldBe(100);
    }

    [Fact]
    public void GetSimilarityScore_CollapsesInternalWhitespace_Returns100()
    {
        // Double space vs single space normalize equal ONLY if the `\s+` collapse runs.
        _matcher.GetSimilarityScore("José  María", "José María").ShouldBe(100);
    }

    [Fact]
    public void GetSimilarityScore_TrimsOuterWhitespace_Returns100()
    {
        // Leading/trailing spaces normalize away ONLY if Trim runs.
        _matcher.GetSimilarityScore(" Pérez ", "Perez").ShouldBe(100);
    }

    [Fact]
    public void GetSimilarityScore_DifferentNames_LessThan100()
    {
        // A non-identity value pins that Fuzz.Ratio isn't replaced by a constant 100.
        _matcher.GetSimilarityScore("Pérez", "Gómez").ShouldBeLessThan(100);
    }

    [Fact]
    public void GetSimilarityScore_SpaceVsNoSpace_LessThan100()
    {
        // "Jose Maria" normalizes to "jose maria"; "JoseMaria" to "josemaria". They differ ONLY by
        // the internal space, so the score is < 100. If the whitespace-collapse replacement
        // (`" "`) is mutated to `""`, "jose maria" would collapse to "josemaria" and the score
        // would jump to 100 -> this assertion kills that L199 replacement mutant.
        _matcher.GetSimilarityScore("Jose Maria", "JoseMaria").ShouldBeLessThan(100);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void GetSimilarityScore_BothEmptyOrWhitespace_ReturnsZero(string value1, string value2)
    {
        // The null/whitespace guard returns 0. If its `return 0` block is removed, the method would
        // fall through to Fuzz.Ratio of two normalized-empty strings; asserting 0 pins the guard.
        _matcher.GetSimilarityScore(value1, value2).ShouldBe(0);
    }

    // ---------------------------------------------------------------------------------------------
    // IsNameField — special-char exclusion, all-digits, 0.80 ratio boundary
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Pérez", "single accented surname")]
    [InlineData("Pérez García", "full Mexican name")]
    [InlineData("García-López", "hyphenated compound surname is still a name")]
    public void IsNameField_RealNames_ReturnsTrue(string value, string reason)
    {
        _matcher.IsNameField(value).ShouldBe(true, reason);
    }

    [Fact]
    public void IsNameField_ContainsSlashDespiteHighLetterRatio_ReturnsFalse()
    {
        // "abcdefgh/i" is 90% letters but the '/' marks a non-name field. Kills the inner
        // `Contains('/')` check (and the '/' operand of the outer guard) — the letter-ratio gate
        // alone would otherwise pass it (0.9 >= 0.80).
        _matcher.IsNameField("abcdefgh/i").ShouldBeFalse();
    }

    [Fact]
    public void IsNameField_ContainsDollarDespiteHighLetterRatio_ReturnsFalse()
    {
        // Same idea for '$'. Kills the `Contains('$')` checks.
        _matcher.IsNameField("abcdefgh$i").ShouldBeFalse();
    }

    [Fact]
    public void IsNameField_Exactly80PercentLetters_ReturnsTrue()
    {
        // "abcd." = 4 letters / 5 chars = exactly 0.80. With `>= 0.80` this is a name (true).
        // Mutating `>=`->`>` flips it to false; the `(double)` cast removal makes it integer 0;
        // both are killed here.
        _matcher.IsNameField("abcd.").ShouldBeTrue();
    }

    [Fact]
    public void IsNameField_JustBelow80PercentLetters_ReturnsFalse()
    {
        // "abc.." = 3 letters / 5 chars = 0.60 < 0.80 -> not a name. Kills `0.80`->`0`
        // (would be true) and the `/`->`*` arithmetic mutant (3*5=15 >= 0.80 -> true).
        _matcher.IsNameField("abc..").ShouldBeFalse();
    }

    [Fact]
    public void IsNameField_NoLetters_ReturnsFalse()
    {
        // Zero letters -> ratio 0 < 0.80. Reinforces the `0.80`->`0` kill.
        _matcher.IsNameField(".....").ShouldBeFalse();
    }

    [Fact]
    public void IsNameField_MostlyLettersWithOneDigit_ReturnsTrue()
    {
        // "Juan2" is 4/5 = 0.80 letters and is NOT all-digits, so the all-digits guard
        // (`trimmedValue.All(char.IsDigit)`) is false and it falls through to the letter-ratio ->
        // true. Mutating `All` to `Any` would make `Any(char.IsDigit)` true (the '2') -> wrongly
        // return false. Asserting true kills the L141 `All()`->`Any()` mutant. (Note: the all-digits
        // *return* block itself is dead code — AmountPattern matches every pure-digit string first.)
        _matcher.IsNameField("Juan2").ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsNameField_NullOrWhitespace_ReturnsFalse(string? value)
    {
        _matcher.IsNameField(value).ShouldBeFalse();
    }

    // ---------------------------------------------------------------------------------------------
    // MatchThreshold
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MatchThreshold_Is85()
    {
        _matcher.MatchThreshold.ShouldBe(85);
    }
}
