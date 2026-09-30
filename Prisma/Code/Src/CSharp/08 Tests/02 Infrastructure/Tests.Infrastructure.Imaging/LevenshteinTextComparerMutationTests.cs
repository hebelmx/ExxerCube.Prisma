using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Infrastructure.Imaging;

namespace ExxerCube.Prisma.Tests.Infrastructure.Imaging;

/// <summary>
/// Mutation-killing tests for <see cref="LevenshteinTextComparer"/>.
/// The class had no direct test coverage; these pin the exact behaviour of all five public
/// methods (edit distance, fuzzy ratio, similarity, quality score, best-match) so that
/// operator/boolean/statement-removal mutants are observable.
/// All hand-computed values are documented inline.
/// </summary>
public class LevenshteinTextComparerMutationTests
{
    private readonly LevenshteinTextComparer _comparer;

    public LevenshteinTextComparerMutationTests()
    {
        _comparer = new LevenshteinTextComparer(
            Substitute.For<ILogger<LevenshteinTextComparer>>());
    }

    // ---------------------------------------------------------------------
    // CalculateEditDistance
    // ---------------------------------------------------------------------

    [Fact]
    public void CalculateEditDistance_EmptySource_ReturnsTargetLength()
    {
        // source empty -> target.Length (5)
        _comparer.CalculateEditDistance("", "hello").ShouldBe(5);
    }

    [Fact]
    public void CalculateEditDistance_EmptySourceAndNullTarget_ReturnsZero()
    {
        // source empty, target null -> target?.Length ?? 0 == 0
        _comparer.CalculateEditDistance("", null!).ShouldBe(0);
    }

    [Fact]
    public void CalculateEditDistance_EmptyTarget_ReturnsSourceLength()
    {
        // target empty -> source.Length (3)
        _comparer.CalculateEditDistance("cat", "").ShouldBe(3);
    }

    [Fact]
    public void CalculateEditDistance_IdenticalStrings_ReturnsZero()
    {
        _comparer.CalculateEditDistance("hello", "hello").ShouldBe(0);
    }

    [Fact]
    public void CalculateEditDistance_SingleSubstitution_ReturnsOne()
    {
        // cat -> car : substitute t->r
        _comparer.CalculateEditDistance("cat", "car").ShouldBe(1);
    }

    [Fact]
    public void CalculateEditDistance_SingleInsertion_ReturnsOne()
    {
        // cat -> cats : insert s
        _comparer.CalculateEditDistance("cat", "cats").ShouldBe(1);
    }

    [Fact]
    public void CalculateEditDistance_SingleDeletion_ReturnsOne()
    {
        // cats -> cat : delete s
        _comparer.CalculateEditDistance("cats", "cat").ShouldBe(1);
    }

    [Fact]
    public void CalculateEditDistance_KittenSitting_ReturnsThree()
    {
        // classic Levenshtein example: kitten -> sitting == 3
        _comparer.CalculateEditDistance("kitten", "sitting").ShouldBe(3);
    }

    [Fact]
    public void CalculateEditDistance_ShortTargetForcesColumnInit_ReturnsThree()
    {
        // "abc" vs "x": result 3 depends on the first-column deletion baseline
        // (distance[i,0] = i). matrix walk: d[3,1] = 3.
        _comparer.CalculateEditDistance("abc", "x").ShouldBe(3);
    }

    [Fact]
    public void CalculateEditDistance_ShortSourceForcesRowInit_ReturnsThree()
    {
        // "x" vs "abc": result 3 depends on the first-row insertion baseline
        // (distance[0,j] = j).
        _comparer.CalculateEditDistance("x", "abc").ShouldBe(3);
    }

    // ---------------------------------------------------------------------
    // CalculateFuzzyRatio
    // ---------------------------------------------------------------------

    [Fact]
    public void CalculateFuzzyRatio_BothEmpty_Returns100()
    {
        _comparer.CalculateFuzzyRatio("", "").ShouldBe(100);
    }

    [Fact]
    public void CalculateFuzzyRatio_SourceEmptyTargetNot_ReturnsZero()
    {
        // distinguishes the both-empty (100) guard from the one-empty (0) guard
        _comparer.CalculateFuzzyRatio("", "abc").ShouldBe(0);
    }

    [Fact]
    public void CalculateFuzzyRatio_TargetEmptySourceNot_ReturnsZero()
    {
        _comparer.CalculateFuzzyRatio("abc", "").ShouldBe(0);
    }

    [Fact]
    public void CalculateFuzzyRatio_Identical_Returns100()
    {
        _comparer.CalculateFuzzyRatio("hello", "hello").ShouldBe(100);
    }

    [Fact]
    public void CalculateFuzzyRatio_NoCommonChars_ReturnsZero()
    {
        _comparer.CalculateFuzzyRatio("abc", "xyz").ShouldBe(0);
    }

    [Fact]
    public void CalculateFuzzyRatio_HalfMatch_Returns50()
    {
        // SequenceMatcher ratio = 2*M/T. "abcdef" vs "abcxyz": matched block "abc"=3,
        // T=12 -> 2*3/12 = 0.5 -> 50.
        _comparer.CalculateFuzzyRatio("abcdef", "abcxyz").ShouldBe(50);
    }

    // ---------------------------------------------------------------------
    // CalculateSimilarity
    // ---------------------------------------------------------------------

    [Fact]
    public void CalculateSimilarity_BothEmpty_ReturnsOne()
    {
        _comparer.CalculateSimilarity("", "").ShouldBe(1.0);
    }

    [Fact]
    public void CalculateSimilarity_Identical_ReturnsOne()
    {
        _comparer.CalculateSimilarity("hello", "hello").ShouldBe(1.0);
    }

    [Fact]
    public void CalculateSimilarity_OneSubstitution_ReturnsTwoThirds()
    {
        // cat vs car: editDistance 1, maxLength 3 -> 1 - 1/3 = 0.6667
        _comparer.CalculateSimilarity("cat", "car").ShouldBe(1.0 - 1.0 / 3.0, 1e-9);
    }

    [Fact]
    public void CalculateSimilarity_CompletelyDifferentSameLength_ReturnsZero()
    {
        // abc vs xyz: editDistance 3, maxLength 3 -> 1 - 3/3 = 0
        _comparer.CalculateSimilarity("abc", "xyz").ShouldBe(0.0);
    }

    [Fact]
    public void CalculateSimilarity_OneEmpty_ReturnsZero()
    {
        // abc vs "" : editDistance 3, maxLength 3 -> 0 (not the both-empty 1.0 path)
        _comparer.CalculateSimilarity("abc", "").ShouldBe(0.0);
    }

    [Fact]
    public void CalculateSimilarity_DifferentLengths_UsesMaxLengthDenominator()
    {
        // "cat" vs "cats": editDistance 1, maxLength 4 -> 1 - 1/4 = 0.75
        _comparer.CalculateSimilarity("cat", "cats").ShouldBe(0.75, 1e-9);
    }

    // ---------------------------------------------------------------------
    // CalculateQualityScore
    // ---------------------------------------------------------------------

    [Fact]
    public void CalculateQualityScore_NullOrWhitespace_ReturnsZero()
    {
        _comparer.CalculateQualityScore("   ").ShouldBe(0);
    }

    [Fact]
    public void CalculateQualityScore_EmptyString_ReturnsZero()
    {
        _comparer.CalculateQualityScore("").ShouldBe(0);
    }

    [Fact]
    public void CalculateQualityScore_TwoBalancedWords_ReturnsExactScore()
    {
        // text = "abcde fghijk" (len 12)
        // alphanumeric: 11/12 -> 0.916667*40 = 36.666667
        // whitespace: 1/12=0.083333 -> 1 - |0.083333-0.175|/0.175 = 0.476190 *20 = 9.523810
        // special: 0 -> score 1 *20 = 20
        // words ["abcde","fghijk"] avg 5.5 -> wordLengthScore 1 *10 = 10
        // validLength: both in [2,15] -> ratio 1 *10 = 10
        // total = 86.190476
        _comparer.CalculateQualityScore("abcde fghijk").ShouldBe(86.190476, 0.0001);
    }

    [Fact]
    public void CalculateQualityScore_HeavySpecialChars_ClampsSpecialScoreToZero()
    {
        // text = "ab!@#$%^&*" (len 10)
        // alphanumeric: 2/10=0.2 *40 = 8
        // whitespace: 0 -> 1-|0-0.175|/0.175 = 0 *20 = 0
        // special: 8/10=0.8 -> Max(0, 1 - 0.8/0.1)=Max(0,-7)=0 *20 = 0
        // words ["ab!@#$%^&*"] len 10 avg 10 -> 1-Min(1,|10-5.5|/5.5)=0.181818 *10 = 1.818182
        // validLength: len 10 in [2,15] -> ratio 1 *10 = 10
        // total = 19.818182
        _comparer.CalculateQualityScore("ab!@#$%^&*").ShouldBe(19.818182, 0.0001);
    }

    [Fact]
    public void CalculateQualityScore_SingleCharWords_FailValidLengthAndWhitespaceClamp()
    {
        // text = "a b c" (len 5)
        // alphanumeric: 3/5=0.6 *40 = 24
        // whitespace: 2/5=0.4 -> 1-|0.4-0.175|/0.175 = 1-1.285714 = -0.285714 -> clamp 0 *20 = 0
        // special: 0 -> 1 *20 = 20
        // words ["a","b","c"] avg 1 -> 1-Min(1,|1-5.5|/5.5)=0.181818 *10 = 1.818182
        // validLength: all length 1 (< 2) -> 0 ratio *10 = 0
        // total = 45.818182
        _comparer.CalculateQualityScore("a b c").ShouldBe(45.818182, 0.0001);
    }

    [Fact]
    public void CalculateQualityScore_LowSpecialCharRatio_GivesPartialSpecialScore()
    {
        // text = "abcdefgh ijklmnopqr." (len 20) - one special char (5% < 10%) so the
        // specialCharScore lands in (0,1), making the specialCharRatio division observable
        // (a '/' -> '*' mutant would inflate the ratio, clamp the score to 0, and drop 10 pts).
        // alphanumeric: 18/20=0.9 *40 = 36
        // whitespace: 1/20=0.05 -> 1-|0.05-0.175|/0.175 = 0.285714 *20 = 5.714286
        // special: 1/20=0.05 -> Max(0,1-0.05/0.1)=0.5 *20 = 10
        // words ["abcdefgh"(8),"ijklmnopqr."(11)] avg 9.5 -> 0.272727 *10 = 2.727273
        // validLength: both in [2,15] -> 1 *10 = 10
        // total = 64.441558
        _comparer.CalculateQualityScore("abcdefgh ijklmnopqr.").ShouldBe(64.441558, 0.0001);
    }

    [Fact]
    public void CalculateQualityScore_WordLengthExactlyTwo_CountsAsValid()
    {
        // single 2-char word: w.Length >= 2 includes it (ratio 1). A '>= 2' -> '> 2' mutant
        // would exclude it and drop the 10-pt valid-length contribution.
        // alphanumeric 2/2=1 *40=40; whitespace 0 -> 0; special 0 -> 1*20=20;
        // word "ab" avg 2 -> 1-Min(1,|2-5.5|/5.5)=0.363636 *10=3.636364; validLength 1*10=10
        // total = 73.636364
        _comparer.CalculateQualityScore("ab").ShouldBe(73.636364, 0.0001);
    }

    [Fact]
    public void CalculateQualityScore_WordLengthExactlyFifteen_CountsAsValid()
    {
        // single 15-char word: w.Length <= 15 includes it. A '<= 15' -> '< 15' mutant
        // would exclude it and drop the 10-pt valid-length contribution.
        // alphanumeric 15/15=1 *40=40; whitespace 0 -> 0; special 0 -> 20;
        // word avg 15 -> 1-Min(1,|15-5.5|/5.5)=1-1=0 *10=0; validLength 1*10=10
        // total = 70
        _comparer.CalculateQualityScore("abcdefghijklmno").ShouldBe(70.0, 0.0001);
    }

    [Fact]
    public void FindBestMatch_DuplicatePhrase_ReturnsFirstOccurrence()
    {
        // "hello world" appears twice; the strict '>' best-tracking keeps the FIRST 1.0 match
        // (StartIndex 0). A '>' -> '>=' mutant would let the later equal match overwrite it
        // (StartIndex 12).
        var result = _comparer.FindBestMatch("hello world", "hello world hello world");

        result.ShouldNotBeNull();
        result!.Similarity.ShouldBe(1.0);
        result.StartIndex.ShouldBe(0);
    }

    [Fact]
    public void FindBestMatch_ThresholdEqualsSimilarity_ReturnsMatch()
    {
        // exact match -> similarity 1.0; with threshold 1.0 the '>=' comparison accepts it.
        // A '>=' -> '>' mutant would reject the boundary and return null.
        var result = _comparer.FindBestMatch("hello world", "hello world", 1.0);

        result.ShouldNotBeNull();
        result!.Similarity.ShouldBe(1.0);
    }

    [Fact]
    public void FindBestMatch_MatchAfterLeadingWords_ResolvesCorrectStartIndex()
    {
        // single-word phrase matching the 3rd word forces FindSubstringIndex's cumulative
        // offset loop to run twice (wordPosition 2). The in-loop 'currentPos < 0' guard must
        // stay a not-found check; a '< 0' -> '> 0' mutant fires on the positive running
        // offset, returns -1, and FindBestMatch then yields null instead of the index-6 match.
        var result = _comparer.FindBestMatch("world", "aa bb world");

        result.ShouldNotBeNull();
        result!.StartIndex.ShouldBe(6);
        result.MatchedText.ShouldBe("world");
        result.Similarity.ShouldBe(1.0);
    }

    // ---------------------------------------------------------------------
    // FindBestMatch
    // ---------------------------------------------------------------------

    [Fact]
    public void FindBestMatch_NullOrWhitespacePhrase_ReturnsNull()
    {
        _comparer.FindBestMatch("   ", "hello world").ShouldBeNull();
    }

    [Fact]
    public void FindBestMatch_NullOrWhitespaceText_ReturnsNull()
    {
        _comparer.FindBestMatch("hello", "   ").ShouldBeNull();
    }

    [Fact]
    public void FindBestMatch_PhraseLongerThanText_ReturnsNull()
    {
        _comparer.FindBestMatch("hello world", "hi").ShouldBeNull();
    }

    [Fact]
    public void FindBestMatch_ExactMatchAtStart_ReturnsFullMatch()
    {
        var result = _comparer.FindBestMatch("hello world", "hello world");

        result.ShouldNotBeNull();
        result!.Similarity.ShouldBe(1.0);
        result.StartIndex.ShouldBe(0);
        result.Length.ShouldBe("hello world".Length);
        result.MatchedText.ShouldBe("hello world");
    }

    [Fact]
    public void FindBestMatch_ExactMatchMidText_ReturnsCorrectStartIndex()
    {
        // "hello world" begins at index 4 in "say hello world now"
        var result = _comparer.FindBestMatch("hello world", "say hello world now");

        result.ShouldNotBeNull();
        result!.Similarity.ShouldBe(1.0);
        result.StartIndex.ShouldBe(4);
        result.MatchedText.ShouldBe("hello world");
    }

    [Fact]
    public void FindBestMatch_BelowThreshold_ReturnsNull()
    {
        // no meaningful overlap -> best similarity well under default 0.85
        _comparer.FindBestMatch("hello world", "goodbye planet").ShouldBeNull();
    }

    [Fact]
    public void FindBestMatch_LowThresholdAllowsPartial_ReturnsMatch()
    {
        // a partial overlap that is below 0.85 but above 0.4 is accepted when
        // the threshold is lowered -> pins the threshold comparison.
        var strict = _comparer.FindBestMatch("hello world", "hello there", 0.85);
        var lenient = _comparer.FindBestMatch("hello world", "hello there", 0.4);

        strict.ShouldBeNull();
        lenient.ShouldNotBeNull();
    }
}
