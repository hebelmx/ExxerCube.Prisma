namespace ExxerCube.Prisma.Tests.Domain.Services;

using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Services;
using ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Unit tests for <see cref="FieldConflictAlertBuilder.From"/>.
/// This is the mutation-killable core of Item C (alertamiento).
/// </summary>
/// <remarks>
/// Tests follow ITDD (ADR-005): all four cases independently kill distinct mutants on
/// the Decision filter (Conflict/WeightedVoting gate), the source-value mapping,
/// the AgreementLevel mapping, and the multi-field branching.
/// </remarks>
public sealed class FieldConflictAlertBuilderTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>Builds a <see cref="FusionResult"/> with the given field entries.</summary>
    private static FusionResult FusionWith(
        Dictionary<string, (FusionDecision decision, double confidence, List<(SourceType source, string? value)> conflicts)> fields)
    {
        var result = new FusionResult();
        foreach (var (name, (decision, confidence, conflicts)) in fields)
        {
            var ffr = new FieldFusionResult
            {
                Decision = decision,
                Confidence = confidence,
            };
            foreach (var (src, val) in conflicts)
            {
                ffr.ConflictingValues.Add((src, val));
            }

            result.FieldResults[name] = ffr;
        }

        return result;
    }

    // -----------------------------------------------------------------------
    // TC-1: null fusion result → empty list
    // -----------------------------------------------------------------------

    /// <summary>
    /// When fusionResult is null, From returns an empty list without throwing.
    /// Kills the null-guard mutant.
    /// </summary>
    [Fact]
    public void From_NullFusionResult_ReturnsEmpty()
    {
        var alerts = FieldConflictAlertBuilder.From(null);
        alerts.ShouldNotBeNull();
        alerts.Count.ShouldBe(0);
    }

    // -----------------------------------------------------------------------
    // TC-2: All fields agree → empty list
    // -----------------------------------------------------------------------

    /// <summary>
    /// When all fields have AllAgree / FuzzyAgreement / AllSourcesNull / BestEffort decisions,
    /// From returns an empty list (no alertamiento).
    /// Kills the Decision filter mutant (removing the Conflict/WeightedVoting guard collapses all → alerts).
    /// </summary>
    [Fact]
    public void From_AllFieldsAgree_ReturnsEmpty()
    {
        var fusion = FusionWith(new Dictionary<string, (FusionDecision, double, List<(SourceType, string?)>)>
        {
            ["NumeroExpediente"] = (FusionDecision.AllAgree, 0.95, new()),
            ["NumeroOficio"] = (FusionDecision.FuzzyAgreement, 0.88, new()),
            ["FechaMedida"] = (FusionDecision.AllSourcesNull, 0.0, new()),
            ["Institucion"] = (FusionDecision.BestEffort, 0.55, new()),
        });

        var alerts = FieldConflictAlertBuilder.From(fusion);

        alerts.ShouldNotBeNull();
        alerts.Count.ShouldBe(0, "all-agree decisions must produce zero alertamientos");
    }

    // -----------------------------------------------------------------------
    // TC-3: One field in WeightedVoting → one alert with all source values
    // -----------------------------------------------------------------------

    /// <summary>
    /// When exactly one field has a WeightedVoting decision (sources disagree, winner selected by weight),
    /// From returns exactly one alert that carries all source values + the field's Confidence as AgreementLevel.
    /// Kills: Decision filter (WeightedVoting must trigger), source-value mapping, AgreementLevel mapping.
    /// </summary>
    [Fact]
    public void From_OneFieldWeightedVoting_ReturnsOneAlertWithAllSources()
    {
        const double confidence = 0.72;

        var fusion = FusionWith(new Dictionary<string, (FusionDecision, double, List<(SourceType, string?)>)>
        {
            ["NumeroExpediente"] = (FusionDecision.AllAgree, 0.95, new()),
            ["Titular"] = (
                FusionDecision.WeightedVoting,
                confidence,
                new List<(SourceType, string?)>
                {
                    (SourceType.XML_HandFilled, "Juan García"),
                    (SourceType.PDF_OCR_CNBV, "Juan Garcia López"),
                }),
        });

        var alerts = FieldConflictAlertBuilder.From(fusion);

        alerts.Count.ShouldBe(1, "one WeightedVoting field must produce exactly one alert");

        var alert = alerts[0];
        alert.FieldName.ShouldBe("Titular");
        alert.AgreementLevel.ShouldBe((float)confidence, "AgreementLevel must equal the field's Confidence");

        alert.ConflictingValues.Count.ShouldBe(2, "both source values must be present in the alert");

        var xmlEntry = alert.ConflictingValues.Single(v => v.Source == SourceType.XML_HandFilled);
        xmlEntry.Value.ShouldBe("Juan García");

        var pdfEntry = alert.ConflictingValues.Single(v => v.Source == SourceType.PDF_OCR_CNBV);
        pdfEntry.Value.ShouldBe("Juan Garcia López");
    }

    // -----------------------------------------------------------------------
    // TC-4: One field in Conflict → one alert
    // -----------------------------------------------------------------------

    /// <summary>
    /// When exactly one field has a Conflict decision (irreconcilable, 0.0 confidence),
    /// From returns exactly one alert. Kills: Decision filter (Conflict must trigger).
    /// </summary>
    [Fact]
    public void From_OneFieldConflict_ReturnsOneAlertWithZeroAgreement()
    {
        var fusion = FusionWith(new Dictionary<string, (FusionDecision, double, List<(SourceType, string?)>)>
        {
            ["RFC"] = (
                FusionDecision.Conflict,
                0.0,
                new List<(SourceType, string?)>
                {
                    (SourceType.XML_HandFilled, "ABCD820101ABC"),
                    (SourceType.PDF_OCR_CNBV, "ABCD820101XYZ"),
                    (SourceType.DOCX_OCR_Authority, null),
                }),
        });

        var alerts = FieldConflictAlertBuilder.From(fusion);

        alerts.Count.ShouldBe(1, "one Conflict field must produce exactly one alert");

        var alert = alerts[0];
        alert.FieldName.ShouldBe("RFC");
        alert.AgreementLevel.ShouldBe(0.0f, "Conflict decisions have 0.0 confidence → 0.0 AgreementLevel");
        alert.ConflictingValues.Count.ShouldBe(3);

        // Null value from DOCX source must be preserved
        var docxEntry = alert.ConflictingValues.Single(v => v.Source == SourceType.DOCX_OCR_Authority);
        docxEntry.Value.ShouldBeNull("a null source value must be preserved in the alert");
    }

    // -----------------------------------------------------------------------
    // TC-5: Multiple conflicting fields → multiple alerts, ordered by FieldName
    // -----------------------------------------------------------------------

    /// <summary>
    /// When multiple fields carry Conflict or WeightedVoting decisions,
    /// From returns one alert per conflicting field.
    /// Alerts are sorted by FieldName so the output is deterministic.
    /// Kills: multi-field loop (a single-iteration implementation would return only the first).
    /// </summary>
    [Fact]
    public void From_MultipleConflictingFields_ReturnsOneAlertPerField_Sorted()
    {
        var fusion = FusionWith(new Dictionary<string, (FusionDecision, double, List<(SourceType, string?)>)>
        {
            ["NumeroOficio"] = (FusionDecision.AllAgree, 0.97, new()),
            ["Titular"] = (
                FusionDecision.WeightedVoting,
                0.71,
                new List<(SourceType, string?)> { (SourceType.XML_HandFilled, "A"), (SourceType.PDF_OCR_CNBV, "B") }),
            ["RFC"] = (
                FusionDecision.Conflict,
                0.0,
                new List<(SourceType, string?)> { (SourceType.XML_HandFilled, "X"), (SourceType.PDF_OCR_CNBV, "Y") }),
            ["FechaMedida"] = (
                FusionDecision.Conflict,
                0.0,
                new List<(SourceType, string?)> { (SourceType.XML_HandFilled, "2026-01-01"), (SourceType.DOCX_OCR_Authority, "01/01/2026") }),
        });

        var alerts = FieldConflictAlertBuilder.From(fusion);

        // 3 fields disagree (Titular + RFC + FechaMedida); NumeroOficio agrees → no alert
        alerts.Count.ShouldBe(3, "exactly three conflicting fields must each produce one alert");

        // Sorted by FieldName ascending
        alerts[0].FieldName.ShouldBe("FechaMedida");
        alerts[1].FieldName.ShouldBe("RFC");
        alerts[2].FieldName.ShouldBe("Titular");

        // Spot-check source counts
        alerts[0].ConflictingValues.Count.ShouldBe(2);
        alerts[1].ConflictingValues.Count.ShouldBe(2);
        alerts[2].ConflictingValues.Count.ShouldBe(2);
    }

    // -----------------------------------------------------------------------
    // TC-6: Empty FieldResults → empty list
    // -----------------------------------------------------------------------

    /// <summary>
    /// A FusionResult with an empty FieldResults dictionary returns an empty alert list.
    /// </summary>
    [Fact]
    public void From_EmptyFieldResults_ReturnsEmpty()
    {
        var fusion = new FusionResult();
        var alerts = FieldConflictAlertBuilder.From(fusion);
        alerts.Count.ShouldBe(0);
    }
}
