namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

using ExxerCube.Prisma.Domain.Enums;

/// <summary>
/// Mutation-killing tests for <see cref="FieldMatcherService{T}"/>.
/// </summary>
/// <remarks>
/// Written against the Stryker.NET survivor map (baseline 30.71%: Survived 29, NoCoverage 68). The existing
/// <c>FieldMatcherServiceTests</c> cover the public happy paths with a single DOCX source type, but never
/// populate <c>AdditionalFields</c> (so the whole <c>CollectAdditional</c>/<c>MergeAdditionalFields</c>
/// pipeline is dark), never use PDF/XML/unknown sources (so the <c>GetSourceType</c>/<c>ToOrigin</c> switches
/// are unpinned), never exercise <c>AccionSolicitada</c>, and assert the unified record / completeness only
/// shallowly. These tests pin those exact behaviors.
///
/// Note: <c>MergeAdditionalFields</c>' conflict/fill branch is unreachable through this service — both
/// <c>xmlFields</c> and <c>ocrFields</c> are produced by the same <c>CollectAdditional</c> filter over the
/// same sources, so they are always identical and no conflict can arise. Those mutants are equivalent and
/// are intentionally left; we pin the reachable behavior (merged contents, empty/Origin-key exclusion,
/// always-empty conflicts).
/// </remarks>
public sealed class FieldMatcherServiceMutationKillingTests
{
    private static (FieldMatcherService<TSource> Service, IFieldExtractor<TSource> Extractor) Make<TSource>()
    {
        var extractor = Substitute.For<IFieldExtractor<TSource>>();
        var matchingPolicy = new MatchingPolicyService(
            Options.Create(new MatchingPolicyOptions()), Substitute.For<ILogger<MatchingPolicyService>>());
        var namePolicy = new NameMatchingPolicy(
            new StaticOptionsMonitor<NameMatchingOptions>(new NameMatchingOptions()),
            Substitute.For<ILogger<NameMatchingPolicy>>());
        var service = new FieldMatcherService<TSource>(
            extractor, matchingPolicy, namePolicy, Substitute.For<ILogger<FieldMatcherService<TSource>>>());
        return (service, extractor);
    }

    // ---------------------------------------------------------------------
    // AdditionalFields pipeline — CollectAdditional + MergeAdditionalFields (L291-352),
    // the dominant NoCoverage cluster. Pins: non-empty merged, whitespace skipped (L300),
    // "Origin" key excluded (L303/L307), conflicts always empty.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task MatchFieldsAsync_AdditionalFields_MergesNonEmptyAndExcludesOriginAndBlanks()
    {
        var (svc, ext) = Make<DocxSource>();
        var fields = new ExtractedFields
        {
            AdditionalFields =
            {
                ["Email"] = "contacto@banco.mx",
                ["Telefono"] = "5551234",
                ["Blank"] = "   ",
                ["Origin"] = "XML"
            }
        };
        ext.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(fields));

        var result = await svc.MatchFieldsAsync(
            new List<DocxSource> { new("a.docx") }, new[] { new FieldDefinition("Expediente") });

        result.IsSuccess.ShouldBeTrue();
        result.Value!.AdditionalMerged["Email"].ShouldBe("contacto@banco.mx");
        result.Value.AdditionalMerged["Telefono"].ShouldBe("5551234");
        result.Value.AdditionalMerged.ShouldNotContainKey("Blank");   // whitespace skipped (L300)
        result.Value.AdditionalMerged.ShouldNotContainKey("Origin");  // Origin key excluded (L303/L307)
        result.Value.AdditionalConflicts.ShouldBeEmpty();
    }

    [Fact]
    public async Task MatchFieldsAsync_NoAdditionalFields_LeavesMergedEmpty()
    {
        var (svc, ext) = Make<DocxSource>();
        ext.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(new ExtractedFields { Expediente = "EXP" }));

        var result = await svc.MatchFieldsAsync(
            new List<DocxSource> { new("a.docx") }, new[] { new FieldDefinition("Expediente") });

        result.IsSuccess.ShouldBeTrue();
        result.Value!.AdditionalMerged.ShouldBeEmpty();
        result.Value.AdditionalConflicts.ShouldBeEmpty();
    }

    // ---------------------------------------------------------------------
    // GetSourceType (L254-262) + ToOrigin (L265-272). Two agreeing sources of each
    // type so the policy preserves both SourceType and Origin on the FieldMatchResult.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task MatchFieldsAsync_DocxSources_TagsSourceTypeDocxAndOriginDocx()
    {
        var (svc, ext) = Make<DocxSource>();
        ext.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(new ExtractedFields { Expediente = "EXP" }));

        var result = await svc.MatchFieldsAsync(
            new List<DocxSource> { new("a.docx"), new("b.docx") }, new[] { new FieldDefinition("Expediente") });

        var match = result.Value!.FieldMatches["Expediente"];
        match.SourceType.ShouldBe("DOCX");
        match.Origin.ShouldBe(FieldOrigin.Docx);
    }

    [Fact]
    public async Task MatchFieldsAsync_PdfSources_TagsSourceTypePdfAndOriginPdfOcr()
    {
        var (svc, ext) = Make<PdfSource>();
        ext.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(new ExtractedFields { Expediente = "EXP" }));

        var result = await svc.MatchFieldsAsync(
            new List<PdfSource> { new("a.pdf"), new("b.pdf") }, new[] { new FieldDefinition("Expediente") });

        var match = result.Value!.FieldMatches["Expediente"];
        match.SourceType.ShouldBe("PDF");
        match.Origin.ShouldBe(FieldOrigin.PdfOcr);
    }

    [Fact]
    public async Task MatchFieldsAsync_XmlSources_TagsSourceTypeXmlAndOriginXml()
    {
        var (svc, ext) = Make<XmlSource>();
        ext.ExtractFieldsAsync(Arg.Any<XmlSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(new ExtractedFields { Expediente = "EXP" }));

        var result = await svc.MatchFieldsAsync(
            new List<XmlSource> { new("a.xml"), new("b.xml") }, new[] { new FieldDefinition("Expediente") });

        var match = result.Value!.FieldMatches["Expediente"];
        match.SourceType.ShouldBe("XML");
        match.Origin.ShouldBe(FieldOrigin.Xml);
    }

    [Fact]
    public async Task MatchFieldsAsync_UnknownSourceType_TagsUnknownAndOriginUnknown()
    {
        // A source type that is not Docx/Pdf/Xml falls through GetSourceType to "UNKNOWN"
        // and ToOrigin to FieldOrigin.Unknown.
        var (svc, ext) = Make<string>();
        ext.ExtractFieldsAsync(Arg.Any<string>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(new ExtractedFields { Expediente = "EXP" }));

        var result = await svc.MatchFieldsAsync(
            new List<string> { "s1", "s2" }, new[] { new FieldDefinition("Expediente") });

        var match = result.Value!.FieldMatches["Expediente"];
        match.SourceType.ShouldBe("UNKNOWN");
        match.Origin.ShouldBe(FieldOrigin.Unknown);
    }

    // ---------------------------------------------------------------------
    // GetFieldValue — AccionSolicitada + its snake_case alias (L229).
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("AccionSolicitada")]
    [InlineData("accion_solicitada")]
    public async Task MatchFieldsAsync_AccionSolicitada_IsExtractedAndMatched(string fieldName)
    {
        var (svc, ext) = Make<DocxSource>();
        ext.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(new ExtractedFields { AccionSolicitada = "Aseguramiento" }));

        var result = await svc.MatchFieldsAsync(
            new List<DocxSource> { new("a.docx") }, new[] { new FieldDefinition(fieldName) });

        result.IsSuccess.ShouldBeTrue();
        result.Value!.FieldMatches[fieldName].MatchedValue.ShouldBe("Aseguramiento");
    }

    // ---------------------------------------------------------------------
    // OverallAgreement is the AVERAGE of per-field agreement levels (L126-127):
    // one perfectly-agreeing field (1.0) + one conflicting field (0.5) -> 0.75 (not Min=0.5).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task MatchFieldsAsync_OverallAgreement_IsAverageOfFieldAgreements()
    {
        var (svc, ext) = Make<DocxSource>();
        ext.ExtractFieldsAsync(Arg.Is<DocxSource>(s => s.FilePath == "a.docx"), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(new ExtractedFields { Expediente = "EXP", Causa = "C1" }));
        ext.ExtractFieldsAsync(Arg.Is<DocxSource>(s => s.FilePath == "b.docx"), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(new ExtractedFields { Expediente = "EXP", Causa = "C2" }));

        var result = await svc.MatchFieldsAsync(
            new List<DocxSource> { new("a.docx"), new("b.docx") },
            new[] { new FieldDefinition("Expediente"), new FieldDefinition("Causa") });

        result.IsSuccess.ShouldBeTrue();
        result.Value!.ConflictingFields.ShouldContain("Causa");      // Causa disagrees (0.5)
        result.Value.ConflictingFields.ShouldNotContain("Expediente"); // Expediente agrees (1.0)
        result.Value.OverallAgreement.ShouldBe(0.75f);               // average(1.0, 0.5)
    }

    [Fact]
    public async Task MatchFieldsAsync_AllFieldsMissing_SucceedsWithoutComputingAverage()
    {
        // FieldMatches is empty here; the OverallAgreement average must be guarded by Count>0
        // (a >=0 mutant would call Average() on an empty list and throw -> failure).
        var (svc, ext) = Make<DocxSource>();
        ext.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(new ExtractedFields()));

        var result = await svc.MatchFieldsAsync(
            new List<DocxSource> { new("a.docx") }, new[] { new FieldDefinition("Expediente") });

        result.IsSuccess.ShouldBeTrue();
        result.Value!.FieldMatches.ShouldBeEmpty();
        result.Value.MissingFields.ShouldContain("Expediente");
        result.Value.OverallAgreement.ShouldBe(0f);
    }

    // ---------------------------------------------------------------------
    // GenerateUnifiedRecordAsync — the matched values are actually applied onto
    // ExtractedFields (L169-172 loop + L274-289 ApplyMatchedFieldToExtractedFields).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GenerateUnifiedRecordAsync_AppliesMatchedCoreFieldsOntoExtractedFields()
    {
        var (svc, _) = Make<DocxSource>();
        var matched = new MatchedFields
        {
            FieldMatches = new Dictionary<string, FieldMatchResult>
            {
                ["Expediente"] = new FieldMatchResult("Expediente", "EXP-1", 1.0f, "DOCX"),
                ["Causa"] = new FieldMatchResult("Causa", "Investigación", 1.0f, "DOCX"),
                ["AccionSolicitada"] = new FieldMatchResult("AccionSolicitada", "Aseguramiento", 1.0f, "DOCX")
            }
        };

        var result = await svc.GenerateUnifiedRecordAsync(matched);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.ExtractedFields.Expediente.ShouldBe("EXP-1");
        result.Value.ExtractedFields.Causa.ShouldBe("Investigación");
        result.Value.ExtractedFields.AccionSolicitada.ShouldBe("Aseguramiento");
    }

    // ---------------------------------------------------------------------
    // ValidateCompletenessAsync — null required list short-circuits to success (L202-203),
    // and a present-but-empty matched value still counts as missing (L207 + L211 message).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ValidateCompletenessAsync_NullRequiredFields_ReturnsSuccess()
    {
        var (svc, _) = Make<DocxSource>();

        var result = await svc.ValidateCompletenessAsync(new MatchedFields(), null!);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateCompletenessAsync_RequiredFieldPresentButEmpty_ReturnsFailure()
    {
        var (svc, _) = Make<DocxSource>();
        var matched = new MatchedFields
        {
            FieldMatches = new Dictionary<string, FieldMatchResult>
            {
                ["Expediente"] = new FieldMatchResult("Expediente", "", 1.0f, "DOCX")
            }
        };

        var result = await svc.ValidateCompletenessAsync(matched, new List<string> { "Expediente" });

        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("Expediente");
    }

    [Fact]
    public async Task ValidateCompletenessAsync_NullMatchedFields_ReturnsFailure()
    {
        var (svc, _) = Make<DocxSource>();

        var result = await svc.ValidateCompletenessAsync(null!, new List<string> { "Expediente" });

        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("cannot be null");
    }

    [Fact]
    public async Task ValidateCompletenessAsync_MultipleMissing_JoinsNamesWithCommaSeparator()
    {
        // Two missing fields force the ", " join separator to appear (a single missing field
        // can't distinguish "" from ", ").
        var (svc, _) = Make<DocxSource>();

        var result = await svc.ValidateCompletenessAsync(
            new MatchedFields(), new List<string> { "Expediente", "Causa" });

        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("Expediente, Causa");
    }

    // ---------------------------------------------------------------------
    // A source whose extraction yields a null ExtractedFields is skipped (L78-80);
    // removing the guard would feed null into GetFieldValue and fault the whole match.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task MatchFieldsAsync_SourceReturnsNullFields_IsSkippedAndOthersStillMatch()
    {
        var (svc, ext) = Make<DocxSource>();
        ext.ExtractFieldsAsync(Arg.Is<DocxSource>(s => s.FilePath == "null.docx"), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(null!));
        ext.ExtractFieldsAsync(Arg.Is<DocxSource>(s => s.FilePath == "ok.docx"), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(new ExtractedFields { Expediente = "EXP" }));

        var result = await svc.MatchFieldsAsync(
            new List<DocxSource> { new("null.docx"), new("ok.docx") }, new[] { new FieldDefinition("Expediente") });

        result.IsSuccess.ShouldBeTrue();
        result.Value!.FieldMatches["Expediente"].MatchedValue.ShouldBe("EXP");
    }
}
