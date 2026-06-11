using System.Globalization;
using System.Text;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using NSubstitute;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Builds the contract-conforming NSubstitute mock that backs the blueprint instance
/// of <see cref="FusionExpedienteContract"/>.
/// </summary>
/// <remarks>
/// A <em>reference fusion engine</em> implementing the documented decision ladder: per field it
/// detects exact agreement (AllAgree), accent/case-only agreement (FuzzyAgreement), a plurality or
/// higher-reliability winner among disagreeing sources (WeightedVoting), three mutually-distinct
/// values (Conflict), or no values at all (AllSourcesNull) — and routes the overall result to
/// AutoProcess / ManualReviewRequired exactly as the interface documents. Source reliability is a
/// simplified base-weight × quality-factor model; the precise coefficient arithmetic is the real
/// implementation's concern, so the contract only asserts the decisions and the threshold clauses.
/// </remarks>
public static class FusionExpedienteMockFactory
{
    /// <summary>
    /// Creates an <see cref="IFusionExpediente"/> mock that satisfies every test in
    /// <see cref="FusionExpedienteContract"/>.
    /// </summary>
    /// <returns>The configured mock.</returns>
    public static IFusionExpediente CreateContractConformingMock()
    {
        var mock = Substitute.For<IFusionExpediente>();

        mock.FuseAsync(
                Arg.Any<Expediente?>(), Arg.Any<Expediente?>(), Arg.Any<Expediente?>(),
                Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Fuse(
                call.ArgAt<Expediente?>(0), call.ArgAt<Expediente?>(1), call.ArgAt<Expediente?>(2),
                call.ArgAt<ExtractionMetadata>(3), call.ArgAt<ExtractionMetadata>(4), call.ArgAt<ExtractionMetadata>(5)));

        mock.FuseFieldAsync(Arg.Any<string>(), Arg.Any<List<FieldCandidate>>(), Arg.Any<CancellationToken>())
            .Returns(call => FuseField(call.ArgAt<List<FieldCandidate>>(1)));

        return mock;
    }

    private sealed record Source(Expediente Expediente, SourceType Type, double Reliability);

    private static Result<FusionResult> Fuse(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        ExtractionMetadata xmlMeta, ExtractionMetadata pdfMeta, ExtractionMetadata docxMeta)
    {
        var sources = new List<Source>();
        if (xml is not null)
        {
            sources.Add(new Source(xml, SourceType.XML_HandFilled, Reliability(SourceType.XML_HandFilled, xmlMeta)));
        }

        if (pdf is not null)
        {
            sources.Add(new Source(pdf, SourceType.PDF_OCR_CNBV, Reliability(SourceType.PDF_OCR_CNBV, pdfMeta)));
        }

        if (docx is not null)
        {
            sources.Add(new Source(docx, SourceType.DOCX_OCR_Authority, Reliability(SourceType.DOCX_OCR_Authority, docxMeta)));
        }

        if (sources.Count == 0)
        {
            return Result<FusionResult>.WithFailure("At least one source must be provided.");
        }

        var fusion = new FusionResult { FusedExpediente = new Expediente() };

        var fields = new (string Name, Func<Expediente, string?> Selector, Action<Expediente, string?> Setter)[]
        {
            ("NumeroExpediente", e => e.NumeroExpediente, (e, v) => e.NumeroExpediente = v!),
            ("AreaDescripcion", e => e.AreaDescripcion, (e, v) => e.AreaDescripcion = v!),
            ("AutoridadNombre", e => e.AutoridadNombre, (e, v) => e.AutoridadNombre = v!),
            ("NumeroOficio", e => e.NumeroOficio, (e, v) => e.NumeroOficio = v!),
        };

        var confidences = new List<double>();

        foreach (var field in fields)
        {
            var fieldResult = FuseAcross(sources, field.Selector);
            fusion.FieldResults[field.Name] = fieldResult;
            field.Setter(fusion.FusedExpediente, fieldResult.Value);
            confidences.Add(fieldResult.Confidence);

            if (fieldResult.Decision == FusionDecision.WeightedVoting || fieldResult.Decision == FusionDecision.Conflict)
            {
                fusion.ConflictingFields.Add(field.Name);
            }
        }

        fusion.OverallConfidence = confidences.Count > 0 ? confidences.Average() : 0.0;
        fusion.NextAction = DetermineNextAction(fusion, sources);

        return Result<FusionResult>.WithSuccess(fusion);
    }

    private static FieldFusionResult FuseAcross(List<Source> sources, Func<Expediente, string?> selector)
    {
        var candidates = sources
            .Select(s => (Value: selector(s.Expediente), s.Type, s.Reliability))
            .Where(c => !string.IsNullOrWhiteSpace(c.Value))
            .ToList();

        if (candidates.Count == 0)
        {
            return new FieldFusionResult { Decision = FusionDecision.AllSourcesNull, Value = null, Confidence = 0.0 };
        }

        var exactGroups = candidates.GroupBy(c => c.Value).ToList();

        if (exactGroups.Count == 1)
        {
            return new FieldFusionResult
            {
                Decision = FusionDecision.AllAgree,
                Value = candidates[0].Value,
                Confidence = 0.95,
                ContributingSources = candidates.Select(c => c.Type).ToList(),
            };
        }

        if (candidates.GroupBy(c => Normalize(c.Value)).Count() == 1)
        {
            return new FieldFusionResult
            {
                Decision = FusionDecision.FuzzyAgreement,
                Value = candidates[0].Value,
                Confidence = 0.85,
                FuzzySimilarity = 0.9,
                ContributingSources = candidates.Select(c => c.Type).ToList(),
            };
        }

        // Disagreement. A value backed by >= 2 sources wins via weighted voting.
        var plurality = exactGroups
            .Where(g => g.Count() >= 2)
            .OrderByDescending(g => g.Sum(c => c.Reliability))
            .FirstOrDefault();

        if (plurality is not null)
        {
            var winner = plurality.OrderByDescending(c => c.Reliability).First();
            return new FieldFusionResult
            {
                Decision = FusionDecision.WeightedVoting,
                Value = plurality.Key,
                Confidence = 0.75,
                WinningSource = winner.Type,
                ContributingSources = candidates.Select(c => c.Type).ToList(),
            };
        }

        // All values distinct: two sources → higher reliability wins; three+ → irreconcilable conflict.
        if (candidates.Count == 2)
        {
            var winner = candidates.OrderByDescending(c => c.Reliability).First();
            return new FieldFusionResult
            {
                Decision = FusionDecision.WeightedVoting,
                Value = winner.Value,
                Confidence = 0.70,
                WinningSource = winner.Type,
                ContributingSources = candidates.Select(c => c.Type).ToList(),
            };
        }

        var highest = candidates.OrderByDescending(c => c.Reliability).First();
        return new FieldFusionResult
        {
            Decision = FusionDecision.Conflict,
            Value = highest.Value,
            Confidence = 0.30,
            RequiresManualReview = true,
            ContributingSources = candidates.Select(c => c.Type).ToList(),
            ConflictingValues = candidates.Select(c => (c.Type, c.Value)).ToList(),
        };
    }

    private static NextAction DetermineNextAction(FusionResult fusion, List<Source> sources)
    {
        if (fusion.FieldResults.Values.Any(f => f.RequiresManualReview))
        {
            return NextAction.ManualReviewRequired;
        }

        // A single low-reliability source cannot be auto-processed.
        if (sources.Count == 1 && sources[0].Reliability < 0.70)
        {
            return NextAction.ManualReviewRequired;
        }

        if (fusion.ConflictingFields.Count == 0 && fusion.OverallConfidence >= 0.85)
        {
            return NextAction.AutoProcess;
        }

        return NextAction.ReviewRecommended;
    }

    private static Result<FieldFusionResult> FuseField(List<FieldCandidate> candidates)
    {
        var present = candidates.Where(c => !string.IsNullOrWhiteSpace(c.Value)).ToList();

        if (present.Count == 0)
        {
            return Result<FieldFusionResult>.WithSuccess(new FieldFusionResult
            {
                Decision = FusionDecision.AllSourcesNull,
                Value = null,
                Confidence = 0.0,
            });
        }

        var exactGroups = present.GroupBy(c => c.Value).ToList();

        if (exactGroups.Count == 1)
        {
            return Result<FieldFusionResult>.WithSuccess(new FieldFusionResult
            {
                Decision = FusionDecision.AllAgree,
                Value = present[0].Value,
                Confidence = present.Average(c => c.SourceReliability),
                ContributingSources = present.Select(c => c.Source).ToList(),
            });
        }

        var winner = present.OrderByDescending(c => c.SourceReliability).First();
        return Result<FieldFusionResult>.WithSuccess(new FieldFusionResult
        {
            Decision = FusionDecision.WeightedVoting,
            Value = winner.Value,
            Confidence = 0.70,
            WinningSource = winner.Source,
            ContributingSources = present.Select(c => c.Source).ToList(),
        });
    }

    private static double Reliability(SourceType source, ExtractionMetadata metadata)
    {
        var baseReliability = source == SourceType.XML_HandFilled ? 0.60
            : source == SourceType.PDF_OCR_CNBV ? 0.85
            : source == SourceType.DOCX_OCR_Authority ? 0.70
            : 0.50;

        var qualityFactor = metadata.PatternViolations > 0 ? 0.6 : 1.0;
        return baseReliability * qualityFactor;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }
}
