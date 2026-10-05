namespace ExxerCube.Prisma.Tests.Domain.Services;

using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Services;
using ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Unit tests for <see cref="FusionMetadataRecordBuilder.From"/>: the per-source values a reviewer compares.
/// </summary>
public sealed class FusionMetadataRecordBuilderTests
{
    [Fact]
    public void From_ConflictWonByASourceMissingFromTheConflictList_IncludesTheWinnersValue()
    {
        // Seen on the hero case: fusion listed the XML and DOCX values as the conflict and picked the OCR'd PDF;
        // the PDF's own value lived only in the fused value, so the comparison's PDF column was empty.
        var record = FusionMetadataRecordBuilder.From(Fusion(new FieldFusionResult
        {
            Value = "AGAFADAFSON2/2025/000085",
            Confidence = 0.95,
            Decision = FusionDecision.Conflict,
            WinningSource = SourceType.PDF_OCR_CNBV,
            ContributingSources = new List<SourceType> { SourceType.PDF_OCR_CNBV },
            ConflictingValues = new List<(SourceType Source, string? Value)>
            {
                (SourceType.XML_HandFilled, "333/ccc/-666666666/2025"),
                (SourceType.DOCX_OCR_Authority, "333/ccc/-666666666/2025"),
            },
        }));

        record.MatchedFields!.FieldMatches["NumeroOficio"].AllValues
            .Select(v => (v.SourceType, v.Value))
            .ShouldBe(new[]
            {
                ("XML_HandFilled", (string?)"333/ccc/-666666666/2025"),
                ("DOCX_OCR_Authority", (string?)"333/ccc/-666666666/2025"),
                ("PDF_OCR_CNBV", (string?)"AGAFADAFSON2/2025/000085"),
            });
    }

    [Fact]
    public void From_ConflictListAlreadyHoldsTheWinner_DoesNotDuplicateIt()
    {
        var record = FusionMetadataRecordBuilder.From(Fusion(new FieldFusionResult
        {
            Value = "214-1-55555555/2025",
            Confidence = 0.55,
            Decision = FusionDecision.Conflict,
            WinningSource = SourceType.XML_HandFilled,
            ConflictingValues = new List<(SourceType Source, string? Value)>
            {
                (SourceType.XML_HandFilled, "214-1-55555555/2025"),
                (SourceType.PDF_OCR_CNBV, "214-1-55555558/2025"),
            },
        }));

        record.MatchedFields!.FieldMatches["NumeroOficio"].AllValues.Count.ShouldBe(2);
    }

    [Fact]
    public void From_FieldNoSourceProvided_IsLeftOut()
    {
        var record = FusionMetadataRecordBuilder.From(Fusion(new FieldFusionResult { Decision = FusionDecision.AllSourcesNull }));

        record.MatchedFields!.FieldMatches.ShouldBeEmpty();
    }

    private static FusionResult Fusion(FieldFusionResult numeroOficio) => new()
    {
        FieldResults = new Dictionary<string, FieldFusionResult> { ["NumeroOficio"] = numeroOficio },
        ConflictingFields = numeroOficio.Decision == FusionDecision.Conflict ? new List<string> { "NumeroOficio" } : new List<string>(),
    };
}
