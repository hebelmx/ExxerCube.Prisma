using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using FuzzySharp;
using IndQuestResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Infrastructure.Classification;

/// <summary>
/// Matching policy for names that uses fuzzy metrics with conservative thresholds and alias awareness.
/// </summary>
public sealed class NameMatchingPolicy : INameMatchingPolicy
{
    private readonly NameMatchingOptions _options;
    private readonly ILogger<NameMatchingPolicy> _logger;
    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.Compiled);
    private readonly Dictionary<string, HashSet<string>> _aliasMap;

    /// <summary>
    /// Initializes a new instance with options and logging support.
    /// </summary>
    /// <param name="options">Options monitor for thresholds and aliases.</param>
    /// <param name="logger">Logger instance.</param>
    public NameMatchingPolicy(IOptionsMonitor<NameMatchingOptions> options, ILogger<NameMatchingPolicy> logger)
    {
        _options = options.CurrentValue;
        _logger = logger;
        _aliasMap = BuildAliasMap(_options.Aliases);
    }

    /// <summary>
    /// Selects the best candidate value using fuzzy scoring and alias awareness.
    /// </summary>
    public Task<Result<FieldMatchResult>> SelectBestValueAsync(string fieldName, List<FieldValue> values)
    {
        if (values == null || values.Count == 0)
        {
            return Task.FromResult(Result<FieldMatchResult>.WithFailure("No values to match"));
        }

        var normalized = values
            .Where(v => v?.Value != null)
            .Select(v => (Original: v!, Normalized: Normalize(v!.Value!), Raw: v!.RawValue))
            .ToList();

        if (normalized.Count == 0)
        {
            return Task.FromResult(Result<FieldMatchResult>.WithFailure("No non-empty values to match"));
        }

        // Overall agreement is the WEAKEST score among DISTINCT pairs (the most disagreement); a value is never
        // compared with itself (that diagonal always scores 1.0 and would mask every real conflict). A single
        // comparable value trivially agrees.
        var overallAgreement = 1.0;
        for (var i = 0; i < normalized.Count; i++)
        {
            for (var j = i + 1; j < normalized.Count; j++)
            {
                overallAgreement = Math.Min(overallAgreement, ScorePair(normalized[i].Normalized, normalized[j].Normalized));
            }
        }

        // Winner is the medoid: the value with the highest total agreement with the OTHERS (excluding itself),
        // so the consensus value wins over an outlier rather than blindly taking the first.
        var winnerIndex = 0;
        var bestAffinity = double.NegativeInfinity;
        for (var i = 0; i < normalized.Count; i++)
        {
            var affinity = 0.0;
            for (var j = 0; j < normalized.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }
                affinity += ScorePair(normalized[i].Normalized, normalized[j].Normalized);
            }
            if (affinity > bestAffinity)
            {
                bestAffinity = affinity;
                winnerIndex = i;
            }
        }

        var winner = normalized[winnerIndex];
        var conflict = overallAgreement < _options.ConflictThreshold;
        var review = !conflict && overallAgreement < _options.AcceptThreshold;

        var result = new FieldMatchResult(fieldName, winner.Original.Value, (float)overallAgreement, winner.Original.SourceType, winner.Original.Origin, winner.Raw)
        {
            AllValues = values,
            HasConflict = conflict,
            AgreementLevel = (float)overallAgreement
        };

        if (review || conflict)
        {
            _logger.LogInformation("Name match for {Field} requires review/conflict (score {Score:F2})", fieldName, overallAgreement);
        }

        return Task.FromResult(Result<FieldMatchResult>.Success(result));
    }

    /// <summary>
    /// Calculates the overall agreement among provided values as the WEAKEST score across distinct pairs
    /// (so a single disagreeing value among otherwise-agreeing ones is surfaced as low agreement / a conflict).
    /// </summary>
    public Task<Result<float>> CalculateAgreementLevelAsync(List<FieldValue> values)
    {
        if (values == null || values.Count <= 1)
        {
            return Task.FromResult(Result<float>.WithFailure("Not enough values to compare"));
        }

        var normalized = values
            .Where(v => v?.Value != null)
            .Select(v => Normalize(v!.Value!))
            .ToList();

        if (normalized.Count <= 1)
        {
            return Task.FromResult(Result<float>.WithFailure("Not enough comparable values"));
        }

        // Weakest DISTINCT pair (exclude the diagonal: a value compared with itself always scores 1.0).
        double worst = 1.0;
        for (var i = 0; i < normalized.Count; i++)
        {
            for (var j = i + 1; j < normalized.Count; j++)
            {
                worst = Math.Min(worst, ScorePair(normalized[i], normalized[j]));
            }
        }

        return Task.FromResult(Result<float>.Success((float)worst));
    }

    /// <summary>
    /// Determines whether the values conflict below a given threshold.
    /// </summary>
    public Task<Result<bool>> HasConflictAsync(List<FieldValue> values, float threshold = 0.5f)
    {
        var agreement = CalculateAgreementLevelAsync(values).Result;
        if (agreement.IsFailure)
        {
            return Task.FromResult(Result<bool>.WithFailure(agreement.Error ?? "Agreement calculation failed"));
        }
        return Task.FromResult(Result<bool>.Success(agreement.Value < threshold));
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = WhitespaceRegex.Replace(value, " ").Trim();
        var upper = trimmed.ToUpperInvariant();
        var sb = new StringBuilder();
        foreach (var c in upper.Normalize(NormalizationForm.FormD))
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private double ScorePair(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.Ordinal))
        {
            return 1.0;
        }

        if (IsAlias(a, b))
        {
            return 1.0;
        }

        var tokenScore = Fuzz.TokenSortRatio(a, b) / 100.0;
        var jaroScore = Fuzz.Ratio(a, b) / 100.0;
        return (tokenScore + jaroScore) / 2.0;
    }

    private bool IsAlias(string a, string b)
    {
        if (_aliasMap.TryGetValue(a, out var set) && set.Contains(b)) return true;
        if (_aliasMap.TryGetValue(b, out var set2) && set2.Contains(a)) return true;
        return false;
    }

    private static Dictionary<string, HashSet<string>> BuildAliasMap(IEnumerable<string> aliases)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in aliases ?? Array.Empty<string>())
        {
            var parts = entry.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var p in parts)
            {
                if (!map.TryGetValue(p, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    map[p] = set;
                }
                foreach (var q in parts.Where(q => !string.Equals(q, p, StringComparison.OrdinalIgnoreCase)))
                {
                    set.Add(q);
                }
            }
        }
        return map;
    }
}
