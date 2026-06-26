using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Classification;

/// <summary>
/// Service for classifying documents into regulatory categories using deterministic rule-based classification.
/// </summary>
public class FileClassifierService : IFileClassifier
{
    /// <summary>
    /// Score assigned to a category when no keyword matched. When every category sits at this floor
    /// simultaneously the document carries no classification signal and should be reported as Unknown.
    /// </summary>
    private const int NoMatchFloor = 10;

    private readonly ILogger<FileClassifierService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileClassifierService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public FileClassifierService(ILogger<FileClassifierService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<ClassificationResult>> ClassifyAsync(
        ExtractedMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Classifying document based on metadata");

            var scores = new ClassificationScores();
            var expediente = metadata.Expediente;
            var areaDescripcion = expediente?.AreaDescripcion ?? string.Empty;
            var numeroExpediente = expediente?.NumeroExpediente ?? string.Empty;
            var legalReferences = metadata.LegalReferences ?? Array.Empty<string>();
            var allText = string.Join(" ", legalReferences);

            // Level 1 Classification - Deterministic rules based on keywords and patterns.
            // Story 2.3 (option b): TieneAseguramiento boolean is forwarded as a high-weight fast
            // path. We fold it into ClassifyLevel1 rather than re-routing Stage-4 DI to
            // ExpedienteClasifierService (option a) to keep the change surface minimal and preserve
            // the existing keyword path as a complementary signal.
            var tieneAseguramiento = expediente?.TieneAseguramiento == true;
            ClassifyLevel1(areaDescripcion, numeroExpediente, allText, scores, tieneAseguramiento);

            // Level 2 Classification - Subcategories based on metadata
            var level2 = ClassifyLevel2(areaDescripcion, numeroExpediente, allText);

            // Calculate overall confidence (average of all scores)
            var confidence = CalculateConfidence(scores);

            // Determine Level 1 category (highest score)
            var level1 = DetermineLevel1Category(scores);

            var result = new ClassificationResult
            {
                Level1 = level1,
                Level2 = level2,
                Scores = scores,
                Confidence = confidence
            };

            _logger.LogDebug("Document classified as {Level1}/{Level2} with confidence {Confidence}%", level1, level2, confidence);
            return Task.FromResult(Result<ClassificationResult>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying document");
            return Task.FromResult(Result<ClassificationResult>.WithFailure($"Error classifying document: {ex.Message}", default(ClassificationResult), ex));
        }
    }

    // ── Story 2.4: Classifier–Generator Reconciliation (2026-06-25) ──────────────────────────────────
    // Source: AAAV2_refactored/core/ (legal_catalog.py, variation_engine.py, catalogs/authorities.json)
    // Generator req-types → classifier categories:
    //   fiscal        → Aseguramiento (via "embargo" in fiscal motivacion)
    //   aseguramiento → Aseguramiento (via "aseguramiento precautorio")
    //   judicial      → Informacion   (via "información sobre cuentas")
    //   informacion   → Informacion   (via "información bancaria")
    //   pld           → OperacionesIlicitas   ← primary gap closed by this story
    //
    // Accent strategy: RemoveDiacritics() is applied once to combinedText (see line below).
    // This normalises ALL Spanish accented chars ("ilícita"→"ilicita", "información"→"informacion",
    // "documentación"→"documentacion") before matching. Keywords stay ASCII-uppercase throughout.
    // Cross-category accent gaps fixed by normalisation alone (no new keywords needed):
    //   Informacion  : "información" (informacion/judicial motivacion) → now matches "INFORMACION"
    //   Documentacion: "documentación" (PHRASE_VARIATIONS synonym)    → now matches "DOCUMENTACION"
    //
    // OperacionesIlicitas (pld) gaps closed — 6 new 90-tier keywords; 70-tier broadened:
    //   Generator phrase                          Old match    Added keyword
    //   "recursos de procedencia ilícita"         none      →  "PROCEDENCIA ILICITA"
    //   "operaciones inusuales" (instructions)    none      →  "OPERACIONES INUSUALES"
    //   "operaciones sospechosas" (variation)     none      →  "OPERACIONES SOSPECHOSAS"
    //   "operaciones irregulares" (instructions)  none      →  "OPERACIONES IRREGULARES"
    //   "inteligencia financiera" (UIF area name) none      →  "INTELIGENCIA FINANCIERA"
    //   "LFPIORPI" (pld legal articles)           none      →  "LFPIORPI"
    //   "ilícita" (fem. gender form of ilícito)   none      →  70-tier "ILICITO" → "ILICIT" (prefix)
    // ─────────────────────────────────────────────────────────────────────────────────────────────────
    private static void ClassifyLevel1(string areaDescripcion, string numeroExpediente, string allText, ClassificationScores scores, bool tieneAseguramiento)
    {
        var combinedText = RemoveDiacritics($"{areaDescripcion} {numeroExpediente} {allText}".ToUpperInvariant());

        // Aseguramiento (Asset Seizure)
        // Story 2.3: tieneAseguramiento is a structured boolean from the fused Expediente
        // (XML companion → FusionExpedienteService → Extractor→Reconciliator handoff).
        // It is more trustworthy than substring matching and shares the 90-point tier.
        // Only TieneAseguramiento exists on Expediente as a typed boolean — no other
        // Tiene*/boolean fields map to a document category at this time.
        if (tieneAseguramiento ||
            combinedText.Contains("ASEGURAMIENTO", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("EMBARGO", StringComparison.OrdinalIgnoreCase) ||
            numeroExpediente.Contains("/AS", StringComparison.OrdinalIgnoreCase))
        {
            scores.AseguramientoScore = 90;
        }
        else if (combinedText.Contains("ASEGURAR", StringComparison.OrdinalIgnoreCase))
        {
            scores.AseguramientoScore = 70;
        }
        else
        {
            scores.AseguramientoScore = 10;
        }

        // Desembargo (Asset Release)
        if (combinedText.Contains("DESEMBARGO", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("LIBERAR", StringComparison.OrdinalIgnoreCase))
        {
            scores.DesembargoScore = 90;
        }
        else if (combinedText.Contains("DESEMBARGAR", StringComparison.OrdinalIgnoreCase))
        {
            scores.DesembargoScore = 70;
        }
        else
        {
            scores.DesembargoScore = 10;
        }

        // Documentacion (Documentation Request)
        if (combinedText.Contains("DOCUMENTACION", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("DOCUMENTO", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("SOLICITUD DOCUMENTAL", StringComparison.OrdinalIgnoreCase))
        {
            scores.DocumentacionScore = 90;
        }
        else if (combinedText.Contains("DOCUMENTAR", StringComparison.OrdinalIgnoreCase))
        {
            scores.DocumentacionScore = 70;
        }
        else
        {
            scores.DocumentacionScore = 10;
        }

        // Informacion (Information Request)
        if (combinedText.Contains("INFORMACION", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("INFORMAR", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("REPORTE", StringComparison.OrdinalIgnoreCase))
        {
            scores.InformacionScore = 90;
        }
        else if (combinedText.Contains("INFORMATIVO", StringComparison.OrdinalIgnoreCase))
        {
            scores.InformacionScore = 70;
        }
        else
        {
            scores.InformacionScore = 10;
        }

        // Transferencia (Transfer)
        if (combinedText.Contains("TRANSFERENCIA", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("TRANSFERIR", StringComparison.OrdinalIgnoreCase))
        {
            scores.TransferenciaScore = 90;
        }
        else if (combinedText.Contains("TRANSFER", StringComparison.OrdinalIgnoreCase))
        {
            scores.TransferenciaScore = 70;
        }
        else
        {
            scores.TransferenciaScore = 10;
        }

        // OperacionesIlicitas (Illicit Operations / PLD)
        // 90-tier: original 3 keywords + 6 new keywords from Story 2.4 reconciliation
        if (combinedText.Contains("OPERACIONES ILICITAS", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("PROCEDENCIA ILICITA", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("OPERACIONES INUSUALES", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("OPERACIONES SOSPECHOSAS", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("OPERACIONES IRREGULARES", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("INTELIGENCIA FINANCIERA", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("LFPIORPI", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("LAVADO", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("FINANCIAMIENTO TERRORISMO", StringComparison.OrdinalIgnoreCase))
        {
            scores.OperacionesIlicitasScore = 90;
        }
        else if (combinedText.Contains("ILICIT", StringComparison.OrdinalIgnoreCase))
        {
            // "ILICIT" prefix matches "ILICITO", "ILICITA", "ILICITAS", "ILICITOS" after
            // diacritic normalisation (Story 2.4: previously only "ILICITO" was checked,
            // missing the feminine form "ilícita" emitted by the pld motivacion template).
            scores.OperacionesIlicitasScore = 70;
        }
        else
        {
            scores.OperacionesIlicitasScore = 10;
        }
    }

    private static ClassificationLevel2? ClassifyLevel2(string areaDescripcion, string numeroExpediente, string allText)
    {
        var combinedText = RemoveDiacritics($"{areaDescripcion} {numeroExpediente} {allText}".ToUpperInvariant());

        // Especial (Special)
        if (combinedText.Contains("ESPECIAL", StringComparison.OrdinalIgnoreCase) ||
            numeroExpediente.Contains("/AS", StringComparison.OrdinalIgnoreCase))
        {
            return ClassificationLevel2.Especial;
        }

        // Judicial (Judicial)
        if (combinedText.Contains("JUDICIAL", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("JUEZ", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("TRIBUNAL", StringComparison.OrdinalIgnoreCase))
        {
            return ClassificationLevel2.Judicial;
        }

        // Hacendario (Tax-related)
        if (combinedText.Contains("HACENDARIO", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("SAT", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("SHCP", StringComparison.OrdinalIgnoreCase))
        {
            return ClassificationLevel2.Hacendario;
        }

        return null;
    }

    /// <summary>
    /// Computes the classification confidence as a <b>clarity/separation score</b> on the
    /// interval 0–100.  This value measures how unambiguously a single category dominates
    /// the keyword scoring results.  It is <b>NOT</b> a calibrated probability — a score of
    /// 90 does not mean "90 % likely to be correct"; it means "one category matched strongly
    /// with no competing signal."
    /// </summary>
    /// <remarks>
    /// <para><b>Scoring model context:</b><br/>
    /// <see cref="ClassifyLevel1"/> assigns each of the six categories exactly one of three
    /// discrete scores: 90 (strong keyword/boolean match), 70 (weak/partial match), or
    /// <see cref="NoMatchFloor"/> (10, no match).  <see cref="CalculateConfidence"/> then
    /// maps these raw category scores to a single integer that expresses the <em>separation</em>
    /// between the winning category and the rest.</para>
    ///
    /// <para><b>Confidence bands (with current 90/70/10 scoring model):</b></para>
    /// <list type="table">
    ///   <listheader><term>Returned value</term><description>Meaning</description></listheader>
    ///   <item>
    ///     <term>0</term>
    ///     <description>Zero coverage — every category sits at <see cref="NoMatchFloor"/>
    ///     simultaneously; no keyword matched anywhere.  The document should be reported as
    ///     <c>Unknown</c>.  Introduced by Story 2.2.</description>
    ///   </item>
    ///   <item>
    ///     <term>70</term>
    ///     <description>Weak-match dominance — exactly one category scored 70 while all others
    ///     scored 10 (score difference = 60 ≥ 60 → high-clarity branch, but capped at
    ///     <c>Math.Min(100, 70) = 70</c>).  Also the ceiling for the ambiguous/competing
    ///     branch when multiple categories carry signal and none dominates
    ///     (<c>Math.Min(70, averageScore)</c>).</description>
    ///   </item>
    ///   <item>
    ///     <term>90</term>
    ///     <description>Strong-match dominance — exactly one category scored 90 while all
    ///     others scored 10 (score difference = 80 ≥ 60 → <c>Math.Min(100, 90) = 90</c>).
    ///     This is the expected value for a cleanly matched document and the most common
    ///     passing value for the export gate (threshold 70).</description>
    ///   </item>
    ///   <item>
    ///     <term>&lt; 70 (e.g. 33)</term>
    ///     <description>Ambiguous/competing — two or more categories carry signal (one at 90,
    ///     another at 70) but neither dominates sufficiently (score difference &lt; 40).  The
    ///     result is <c>Math.Min(70, (int)average)</c>; for the common 90+70+10×4 distribution
    ///     this yields 33.  These documents should be flagged for manual review.</description>
    ///   </item>
    /// </list>
    ///
    /// <para><b>Dead-code note — the ≥ 40 &amp;&amp; &lt; 60 branch:</b><br/>
    /// The intermediate branch (<c>scoreDifference ≥ 40 &amp;&amp; &lt; 60 → Math.Min(85, maxScore)</c>)
    /// is structurally present but <em>currently unreachable</em> with the 90/70/10 scoring model
    /// because possible differences are only 0, 20, 60, or 80 — none falls in [40, 59].  It is
    /// preserved for forward compatibility with a richer scoring model.  If the scoring model is
    /// ever extended (e.g. to allow intermediate scores), this branch will activate automatically.</para>
    ///
    /// <para><b>Monotonicity guarantee (single-dominant-category case):</b><br/>
    /// For documents where exactly one category carries signal (all others at the floor), the
    /// returned confidence is strictly ordered: strong match (90) &gt; weak match (70) &gt;
    /// no match (0).  Pinned by test <c>Confidence_ScalesMonotonicallyWithMatchStrength</c>.</para>
    ///
    /// <para><b>Downstream consumer:</b><br/>
    /// <c>ReconciliationOrchestrator</c> uses a threshold of 70 to gate Stage 5 export.  Any
    /// ambiguous/competing result (confidence &lt; 70) is routed to manual review.</para>
    ///
    /// <para><b>TODO(2.6):</b> migrate return type to the <c>Confidence</c> value object
    /// (Story 2.6) to unify the int-based and double-based confidence representations currently
    /// present in <c>ClassificationResult</c> and <c>ExpedienteClassificationResult</c>.</para>
    /// </remarks>
    /// <param name="scores">The raw per-category keyword scores produced by
    /// <see cref="ClassifyLevel1"/>.</param>
    /// <returns>
    /// An integer in the range 0–100 representing classification clarity, where 0 = no keyword
    /// signal, 70 = weak single-category match, 90 = strong single-category match, and values
    /// below 70 indicate ambiguous multi-category competition.
    /// </returns>
    // TODO(2.6): migrate return type to Confidence value object
    private static int CalculateConfidence(ClassificationScores scores)
    {
        var scoresArray = new[]
        {
            scores.AseguramientoScore,
            scores.DesembargoScore,
            scores.DocumentacionScore,
            scores.InformacionScore,
            scores.TransferenciaScore,
            scores.OperacionesIlicitasScore
        };

        var maxScore = scoresArray.Max();

        // Band 0 — zero coverage: every category sits at the no-match floor simultaneously.
        // No keyword matched anywhere; confidence is 0 (not the floor score 10).
        // Introduced by Story 2.2 to prevent a spurious (Aseguramiento, 10) win.
        if (maxScore == NoMatchFloor)
        {
            return 0;
        }

        var averageScore = (int)scoresArray.Average();

        // Separation score: gap between the top category and the next-best category.
        // Large gap = one category dominates clearly = high confidence.
        // Small gap = two or more categories compete = low confidence.
        // Note: Where(s => s != maxScore) removes ALL occurrences of maxScore before
        // finding the runner-up, so a two-way tie at 90 still yields a difference of 80
        // (both 90s are excluded, leaving only the 10-floor values).
        var scoreDifference = maxScore - scoresArray.Where(s => s != maxScore).DefaultIfEmpty(0).Max();

        if (scoreDifference >= 60)
        {
            // High-clarity band: one category dominates with a large separation.
            // With the 90/70/10 model: a single 90 yields diff=80, a single 70 yields diff=60.
            // Returns the winner's raw score, capped at 100.
            // Resulting values: 90 (for a strong-match winner) or 70 (for a weak-match winner).
            return Math.Min(100, maxScore);
        }
        else if (scoreDifference >= 40)
        {
            // Moderate-clarity band: winner leads by 40–59 points.
            // NOTE: Currently unreachable with the 90/70/10 scoring model (possible diffs are
            // 0, 20, 60, 80 — none falls in [40, 59]).  Preserved for a future richer model.
            // Returns the winner's raw score, capped at 85.
            return Math.Min(85, maxScore);
        }
        else
        {
            // Ambiguous/competing band: score difference < 40, meaning two or more categories
            // carry comparable signal.  Confidence is the average score, capped at 70.
            // With the 90/70/10 model: a strong + weak tie (diff=20) yields avg≈33,
            // so confidence = Math.Min(70, 33) = 33 — well below the export gate threshold.
            return Math.Min(70, averageScore);
        }
    }

    private static ClassificationLevel1 DetermineLevel1Category(ClassificationScores scores)
    {
        var scoresDict = new System.Collections.Generic.Dictionary<ClassificationLevel1, int>
        {
            { ClassificationLevel1.Aseguramiento, scores.AseguramientoScore },
            { ClassificationLevel1.Desembargo, scores.DesembargoScore },
            { ClassificationLevel1.Documentacion, scores.DocumentacionScore },
            { ClassificationLevel1.Informacion, scores.InformacionScore },
            { ClassificationLevel1.Transferencia, scores.TransferenciaScore },
            { ClassificationLevel1.OperacionesIlicitas, scores.OperacionesIlicitasScore }
        };

        // No-signal guard: all categories tied at the floor → no keyword matched anywhere.
        // Return Unknown instead of letting insertion-order pick a spurious winner.
        if (scoresDict.Values.Max() == NoMatchFloor)
        {
            return ClassificationLevel1.Unknown;
        }

        return scoresDict.OrderByDescending(kvp => kvp.Value).First().Key;
    }

    /// <summary>
    /// Strips diacritical marks (combining accents) from <paramref name="text"/> so that
    /// keyword matching is accent-insensitive. For example "ILÍCITA" → "ILICITA" and
    /// "INFORMACIÓN" → "INFORMACION". Applied once to <c>combinedText</c> before all
    /// <c>Contains</c> checks; keywords remain ASCII-uppercase and need no accented variants.
    /// </summary>
    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}

