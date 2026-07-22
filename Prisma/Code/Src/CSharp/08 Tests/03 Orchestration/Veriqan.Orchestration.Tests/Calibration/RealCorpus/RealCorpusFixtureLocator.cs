using System.Text.Json;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration.RealCorpus;

/// <summary>
/// One row of the out-of-repo real-corpus index (<c>corpus-index.json</c>, RC1.S1), built by
/// <c>scripts/veriqan-corpus/build_corpus_index.py</c>. Only neutral account labels
/// (<c>A</c>/<c>B</c>/<c>C</c>) ever appear in <see cref="Id"/> / <see cref="AccountLabel"/>;
/// <see cref="RelativePath"/> may contain the real staging folder names (they carry a real
/// account's last-4 digits as a folder-name suffix) because the index — and this record's
/// values at test run time — never enter git (see <c>scripts/veriqan-corpus/README.md</c>,
/// "Hard PII rules"). Never hardcode or log one of those folder names verbatim in committed
/// source; read it from the index at run time only.
/// </summary>
/// <param name="Id">Neutral specimen id, e.g. <c>"A-2026-02"</c> or <c>"defect-good"</c>.</param>
/// <param name="RelativePath">Path relative to the corpus root, e.g. <c>"account-A-priority/2026-02.pdf"</c>.</param>
/// <param name="Sha256">Expected sha256 hex digest of the file, for the integrity check.</param>
/// <param name="Product"><c>"checking"</c> | <c>"credit_card"</c> | <c>"defect"</c>.</param>
/// <param name="Period">Statement period (<c>"yyyy-MM"</c>) for account entries; <see langword="null"/> for defects.</param>
/// <param name="DefectKind">Defect variant name for defect entries; <see langword="null"/> for account entries.</param>
/// <param name="AccountLabel">Neutral account label (<c>A</c>/<c>B</c>/<c>C</c>) for account entries; <see langword="null"/> for defects.</param>
public sealed record RealCorpusEntry(
    string Id,
    string RelativePath,
    string Sha256,
    string Product,
    string? Period,
    string? DefectKind,
    string? AccountLabel);

/// <summary>
/// A successfully-resolved real corpus: the root directory, the loaded index entries, and the
/// resolved (but not yet verified-to-exist) anonymization-map path.
/// </summary>
/// <param name="RootDir">Absolute path to the staging root (contains <c>corpus-index.json</c>).</param>
/// <param name="Entries">Every entry from <c>corpus-index.json</c>.</param>
/// <param name="MapPath">Resolved path to the anonymization map JSON (existence not guaranteed).</param>
public sealed record RealCorpusFixture(
    string RootDir,
    IReadOnlyList<RealCorpusEntry> Entries,
    string MapPath);

/// <summary>
/// RC1.S1 — resolves the out-of-repo real-corpus staging root + index, so that RealCorpus tests
/// can run locally against the anonymized 16-PDF corpus while staying a clean, graceful skip in
/// CI (which never has the corpus staged). Mirrors the existing
/// <see cref="ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration.CalibrationDriverTests"/>
/// "no hardcoded absolute path, graceful <c>Assert.Skip</c> when absent" pattern.
/// </summary>
/// <remarks>
/// Neither the default root nor the default map path is committed with real digits: both are
/// built from <see cref="Environment.SpecialFolder.UserProfile"/> + literal path segments that
/// contain no PII (<c>"vec-corpus-staging"</c>, <c>"vec-anonymization-map.json"</c>).
/// </remarks>
public static class RealCorpusFixtureLocator
{
    /// <summary>Environment variable that overrides the corpus staging root.</summary>
    public const string RootEnvVar = "VERIQAN_REAL_CORPUS_ROOT";

    /// <summary>Environment variable that overrides the anonymization-map path.</summary>
    public const string MapEnvVar = "VERIQAN_REAL_CORPUS_MAP";

    /// <summary>
    /// Resolves the corpus root: <see cref="RootEnvVar"/> if set, else
    /// <c>~/Downloads/vec-corpus-staging</c>. Does not check existence.
    /// </summary>
    public static string ResolveRoot()
    {
        var fromEnv = Environment.GetEnvironmentVariable(RootEnvVar);
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, "Downloads", "vec-corpus-staging");
    }

    /// <summary>
    /// Resolves the anonymization-map path: <see cref="MapEnvVar"/> if set, else the sibling
    /// default <c>~/Downloads/vec-anonymization-map.json</c>. Does not check existence.
    /// </summary>
    public static string ResolveMapPath()
    {
        var fromEnv = Environment.GetEnvironmentVariable(MapEnvVar);
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, "Downloads", "vec-anonymization-map.json");
    }

    /// <summary>
    /// Attempts to resolve the corpus root and load <c>corpus-index.json</c>.
    /// Returns <see langword="null"/> (never throws) when the root directory or the index file
    /// is absent — the caller is expected to <c>Assert.Skip</c> in that case, per the RC1.S1
    /// "CI has no corpus and must stay green" requirement. A malformed (present but unparsable)
    /// index DOES throw — that is a real local-setup bug, not an absence, and must fail loudly.
    /// </summary>
    public static RealCorpusFixture? TryLoad()
    {
        var root = ResolveRoot();
        if (!Directory.Exists(root))
            return null;

        var indexPath = Path.Combine(root, "corpus-index.json");
        if (!File.Exists(indexPath))
            return null;

        using var stream = File.OpenRead(indexPath);
        using var doc = JsonDocument.Parse(stream);

        var entries = new List<RealCorpusEntry>();
        foreach (var el in doc.RootElement.GetProperty("files").EnumerateArray())
        {
            entries.Add(new RealCorpusEntry(
                Id: el.GetProperty("id").GetString() ?? string.Empty,
                RelativePath: el.GetProperty("relativePath").GetString() ?? string.Empty,
                Sha256: el.GetProperty("sha256").GetString() ?? string.Empty,
                Product: el.GetProperty("product").GetString() ?? string.Empty,
                Period: el.TryGetProperty("period", out var periodEl) ? periodEl.GetString() : null,
                DefectKind: el.TryGetProperty("defectKind", out var defectEl) ? defectEl.GetString() : null,
                AccountLabel: el.TryGetProperty("accountLabel", out var accountEl) ? accountEl.GetString() : null));
        }

        return new RealCorpusFixture(root, entries, ResolveMapPath());
    }
}
