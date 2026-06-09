using ExxerCube.Prisma.Infrastructure.Extraction.Ocr;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Mutation-killing tests for <see cref="AdditionalFieldsReconciler"/> (static XML-over-OCR merge).
/// Pins: XML values seed the merge; OCR fills missing keys; a same-key normalized-different (both
/// non-blank) pair is recorded as a conflict; a blank-XML / non-blank-OCR pair is overridden by OCR;
/// case-insensitive equality is NOT a conflict; the <c>Normalize</c> trim/blank rule.
/// </summary>
public class AdditionalFieldsReconcilerMutationTests
{
    private static Dictionary<string, string?> D(params (string k, string? v)[] kv)
    {
        var d = new Dictionary<string, string?>();
        foreach (var (k, v) in kv) d[k] = v;
        return d;
    }

    [Fact]
    public void Merge_XmlOnly_SeedsMergedWithXmlValues()
    {
        var result = AdditionalFieldsReconciler.Merge(D(("A", "x"), ("B", "y")), D());

        result.Merged["A"].ShouldBe("x");
        result.Merged["B"].ShouldBe("y");
        result.Conflicts.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_OcrKeyNotInXml_Added()
    {
        var result = AdditionalFieldsReconciler.Merge(D(("A", "x")), D(("C", "z")));

        result.Merged["C"].ShouldBe("z");
        result.Merged.Count.ShouldBe(2);
        result.Conflicts.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_SameKeyDifferentNonBlankValues_RecordsConflict()
    {
        var result = AdditionalFieldsReconciler.Merge(D(("A", "xmlval")), D(("A", "ocrval")));

        result.Conflicts.ShouldBe(new[] { "A" });
        // XML value is kept in Merged (XML preference); OCR does not overwrite a non-blank XML value.
        result.Merged["A"].ShouldBe("xmlval");
    }

    [Fact]
    public void Merge_SameKeyCaseInsensitiveEqual_NoConflict()
    {
        // existingNormalized "ABC" vs ocr "abc" compared OrdinalIgnoreCase -> equal -> no conflict.
        var result = AdditionalFieldsReconciler.Merge(D(("A", "ABC")), D(("A", "abc")));

        result.Conflicts.ShouldBeEmpty();
        result.Merged["A"].ShouldBe("ABC");
    }

    [Fact]
    public void Merge_SameKeyDifferingOnlyByWhitespace_NoConflict()
    {
        // Normalize trims existing ("ABC " -> "ABC"); ocr "ABC" -> equal -> no conflict.
        var result = AdditionalFieldsReconciler.Merge(D(("A", "ABC ")), D(("A", "ABC")));

        result.Conflicts.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_BlankXmlNonBlankOcr_OcrOverridesNoConflict()
    {
        // existing blank, ocr non-blank -> else-if branch overrides merged[key] with the raw OCR value.
        var result = AdditionalFieldsReconciler.Merge(D(("A", "   ")), D(("A", "ocrval")));

        result.Conflicts.ShouldBeEmpty();
        result.Merged["A"].ShouldBe("ocrval");
    }

    [Fact]
    public void Merge_NonBlankXmlBlankOcr_KeepsXmlNoConflict()
    {
        // ocrValue blank -> conflict guard fails (needs non-blank ocr); else-if needs blank existing -> false.
        // Net: XML value kept, no conflict, no override.
        var result = AdditionalFieldsReconciler.Merge(D(("A", "xmlval")), D(("A", "   ")));

        result.Conflicts.ShouldBeEmpty();
        result.Merged["A"].ShouldBe("xmlval");
    }

    [Fact]
    public void Merge_NullValues_NormalizedWithoutThrowing()
    {
        // Normalize must short-circuit null via IsNullOrWhiteSpace BEFORE calling .Trim(). If the
        // ternary condition is mutated to always-false, Normalize(null) becomes null.Trim() -> NRE.
        // Exercise both Normalize call sites with null: a null OCR value (Normalize(kvp.Value)) and a
        // null XML value re-read as `existing` (Normalize(existing)).
        var result = AdditionalFieldsReconciler.Merge(D(("A", null)), D(("A", "ocrval"), ("Z", null)));

        result.Merged["A"].ShouldBe("ocrval"); // blank(null) XML overridden by non-blank OCR
        result.Merged.ShouldContainKey("Z");   // null OCR value added under new key
        result.Conflicts.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_BothBlankSameKey_NoConflictNoOverride()
    {
        // existing blank but ocr also blank -> override guard (ocr non-blank) false -> stays blank, no conflict.
        var result = AdditionalFieldsReconciler.Merge(D(("A", "")), D(("A", "  ")));

        result.Conflicts.ShouldBeEmpty();
        result.Merged["A"].ShouldBe(string.Empty);
    }
}
