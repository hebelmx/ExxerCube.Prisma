using System.Globalization;
using System.Text;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// The three field-extraction tracks compared by the S4-A eval harness
/// (<see cref="LlmExtractionEvalHarness"/>).
/// </summary>
public enum EvalTrack
{
    /// <summary>Real Tesseract OCR + <c>AdaptiveTxtFieldExtractor</c> (the bar to beat).</summary>
    Deterministic,

    /// <summary>LLM extraction over OCR text (live Ollama, text model).</summary>
    LlmText,

    /// <summary>LLM extraction over page images (live Ollama, vision model).</summary>
    LlmVision,
}

/// <summary>
/// The gold fields measured per PRP1 fixture, per the CNBV XML companion.
/// <see cref="ParteCount"/> is the headline field (count of <c>SolicitudPartes</c>).
/// </summary>
public enum EvalField
{
    NumeroExpediente,
    NumeroOficio,
    AutoridadNombre,
    ParteCount,
}

/// <summary>
/// Match outcome for one (fixture, track, field) evaluation.
/// </summary>
public enum EvalMatchStatus
{
    /// <summary>Normalized gold and candidate are equal.</summary>
    Matched,

    /// <summary>Both gold and candidate are present but normalize to different values.</summary>
    Mismatch,

    /// <summary>The track was attempted but returned no value for this field.</summary>
    Missing,

    /// <summary>The track was not attempted at all for this fixture/field (e.g. Ollama down,
    /// OCR text unavailable, no page images, or the track structurally does not support the
    /// field — e.g. the deterministic track never extracts <c>SolicitudPartes</c>).</summary>
    TrackSkipped,
}

/// <summary>
/// One (fixture, track, field) evaluation record — the atomic unit the metric engine produces
/// and the eval harness serializes into the committed baseline artifact.
/// </summary>
public sealed record FieldEvalResult(
    string FixtureId,
    EvalTrack Track,
    EvalField Field,
    string? GoldRaw,
    string? GoldNormalized,
    string? CandidateRaw,
    string? CandidateNormalized,
    EvalMatchStatus Status,
    string? SkipReason = null);

/// <summary>Per-field, per-track accuracy aggregated across fixtures that have gold for that field.</summary>
public sealed record FieldAccuracy(EvalField Field, EvalTrack Track, int Matches, int Evaluable, double Accuracy);

/// <summary>Per-track coverage: fraction of evaluations where the track actually produced a candidate.</summary>
public sealed record TrackCoverage(EvalTrack Track, int NonNullCandidates, int TotalEvaluations, double Coverage);

/// <summary>
/// PURE metric-computation engine for the S4-A LLM/hybrid extraction eval harness
/// (<see cref="LlmExtractionEvalHarness"/>).
/// </summary>
/// <remarks>
/// This class has NO dependency on Ollama, Tesseract, <see cref="System.Net.Http.HttpClient"/>, or
/// the file system — it only normalizes and compares already-obtained gold/candidate strings, which
/// is what makes it independently unit-testable (see <c>LlmExtractionMetricsTests</c>, S4-A story 2)
/// without any live service.
/// <para>
/// Normalization rules (per <c>spec-llm-hybrid-extractor-S4A.md</c>):
/// <list type="bullet">
///   <item><description><see cref="EvalField.AutoridadNombre"/>: trim + collapse whitespace + case-insensitive.</description></item>
///   <item><description><see cref="EvalField.NumeroExpediente"/> / <see cref="EvalField.NumeroOficio"/>:
///   digit/format-normalized (only letters and digits survive, uppercased) so separator differences
///   (<c>/</c>, <c>-</c>, spaces) never cause a false mismatch.</description></item>
///   <item><description><see cref="EvalField.ParteCount"/>: plain integer equality (see
///   <see cref="EvaluateParteCount"/>).</description></item>
/// </list>
/// </para>
/// </remarks>
public static class LlmExtractionMetrics
{
    /// <summary>Trims, collapses internal whitespace, and upper-invariants a string field (authority name).</summary>
    public static string? NormalizeAuthority(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return CollapseWhitespace(raw.Trim()).ToUpperInvariant();
    }

    /// <summary>
    /// Digit/format-normalizes a case-reference-shaped field (expediente/oficio numbers): keeps only
    /// letters and digits, upper-invariants them, and drops separators (<c>/</c>, <c>-</c>, spaces) so
    /// two differently-formatted but semantically identical references compare equal.
    /// </summary>
    public static string? NormalizeCaseReference(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var sb = new StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToUpperInvariant(c));
            }
        }

        return sb.Length == 0 ? null : sb.ToString();
    }

    /// <summary>Dispatches to the correct normalization rule for a given <see cref="EvalField"/>.</summary>
    public static string? Normalize(EvalField field, string? raw) => field switch
    {
        EvalField.AutoridadNombre => NormalizeAuthority(raw),
        EvalField.NumeroExpediente => NormalizeCaseReference(raw),
        EvalField.NumeroOficio => NormalizeCaseReference(raw),
        EvalField.ParteCount => raw?.Trim().NullIfEmpty(),
        _ => raw,
    };

    /// <summary>
    /// Evaluates one string-valued (gold, candidate) pair for a given fixture/track/field.
    /// Pass <paramref name="skipReason"/> when the track was not attempted at all (Ollama down,
    /// no OCR text available, no page images, etc.) — this yields <see cref="EvalMatchStatus.TrackSkipped"/>
    /// regardless of the gold/candidate values.
    /// </summary>
    public static FieldEvalResult Evaluate(
        string fixtureId,
        EvalTrack track,
        EvalField field,
        string? gold,
        string? candidate,
        string? skipReason = null)
    {
        ArgumentNullException.ThrowIfNull(fixtureId);

        var goldNorm = Normalize(field, gold);

        if (skipReason is not null)
        {
            return new FieldEvalResult(fixtureId, track, field, gold, goldNorm, candidate, null, EvalMatchStatus.TrackSkipped, skipReason);
        }

        var candNorm = Normalize(field, candidate);

        var status = string.IsNullOrEmpty(candNorm)
            ? EvalMatchStatus.Missing
            : string.Equals(goldNorm, candNorm, StringComparison.Ordinal)
                ? EvalMatchStatus.Matched
                : EvalMatchStatus.Mismatch;

        return new FieldEvalResult(fixtureId, track, field, gold, goldNorm, candidate, candNorm, status);
    }

    /// <summary>
    /// Evaluates the <see cref="EvalField.ParteCount"/> field (plain integer equality — the headline
    /// field is a count of <c>SolicitudPartes</c> nodes, not a formatted string).
    /// </summary>
    public static FieldEvalResult EvaluateParteCount(
        string fixtureId,
        EvalTrack track,
        int? gold,
        int? candidate,
        string? skipReason = null)
    {
        ArgumentNullException.ThrowIfNull(fixtureId);

        var goldStr = gold?.ToString(CultureInfo.InvariantCulture);

        if (skipReason is not null)
        {
            return new FieldEvalResult(fixtureId, track, EvalField.ParteCount, goldStr, goldStr, null, null, EvalMatchStatus.TrackSkipped, skipReason);
        }

        var candStr = candidate?.ToString(CultureInfo.InvariantCulture);

        var status = candidate is null
            ? EvalMatchStatus.Missing
            : gold.HasValue && gold.Value == candidate.Value
                ? EvalMatchStatus.Matched
                : EvalMatchStatus.Mismatch;

        return new FieldEvalResult(fixtureId, track, EvalField.ParteCount, goldStr, goldStr, candStr, candStr, status);
    }

    /// <summary>
    /// Aggregates per-(field, track) accuracy = matches / fixtures-with-gold. Results whose gold value
    /// is null/empty are excluded from the denominator (there is nothing to grade against).
    /// </summary>
    public static IReadOnlyList<FieldAccuracy> AggregateAccuracy(IEnumerable<FieldEvalResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var list = new List<FieldAccuracy>();
        foreach (var group in results
                     .Where(r => !string.IsNullOrEmpty(r.GoldNormalized))
                     .GroupBy(r => (r.Field, r.Track)))
        {
            var evaluable = group.Count();
            var matches = group.Count(r => r.Status == EvalMatchStatus.Matched);
            var accuracy = evaluable == 0 ? 0d : (double)matches / evaluable;
            list.Add(new FieldAccuracy(group.Key.Field, group.Key.Track, matches, evaluable, accuracy));
        }

        return list;
    }

    /// <summary>
    /// Aggregates per-track coverage = evaluations that actually produced a non-null candidate
    /// (i.e. not <see cref="EvalMatchStatus.Missing"/> nor <see cref="EvalMatchStatus.TrackSkipped"/>)
    /// divided by the total number of evaluations recorded for that track.
    /// </summary>
    public static IReadOnlyList<TrackCoverage> AggregateCoverage(IEnumerable<FieldEvalResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var list = new List<TrackCoverage>();
        foreach (var group in results.GroupBy(r => r.Track))
        {
            var total = group.Count();
            var nonNull = group.Count(r => r.Status != EvalMatchStatus.Missing && r.Status != EvalMatchStatus.TrackSkipped);
            var coverage = total == 0 ? 0d : (double)nonNull / total;
            list.Add(new TrackCoverage(group.Key, nonNull, total, coverage));
        }

        return list;
    }

    private static string CollapseWhitespace(string s)
    {
        var sb = new StringBuilder(s.Length);
        var lastWasSpace = false;
        foreach (var c in s)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                }

                lastWasSpace = true;
            }
            else
            {
                sb.Append(c);
                lastWasSpace = false;
            }
        }

        return sb.ToString();
    }
}

file static class NullableStringExtensions
{
    internal static string? NullIfEmpty(this string s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
