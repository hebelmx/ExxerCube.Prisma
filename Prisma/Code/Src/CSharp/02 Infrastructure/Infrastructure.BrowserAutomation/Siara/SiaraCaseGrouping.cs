using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.Siara;

/// <summary>
/// Pure, stateless helper that groups a flat list of <see cref="DownloadableFile"/> entries into
/// <see cref="SiaraCase"/> bundles, one bundle per distinct SIARA case folder (MVP-PATH 2.1).
/// </summary>
/// <remarks>
/// SIARA serves companion files at URLs shaped <c>/document_store/{caseId}/{fileName}</c>.
/// This helper extracts the case id from the path segment immediately before the file name and
/// groups all files that share the same case id into one <see cref="SiaraCase"/>. It is intentionally
/// free of I/O, DI, and side-effects so it can be exercised by pure unit tests without any browser
/// infrastructure.
/// </remarks>
public static class SiaraCaseGrouping
{
    /// <summary>
    /// Groups <paramref name="files"/> by the case id derived from each file's URL and returns one
    /// <see cref="SiaraCase"/> per distinct case id, in the order the first file of each case appears
    /// in the input.
    /// </summary>
    /// <param name="files">The flat list of downloadable files discovered from SIARA.</param>
    /// <returns>
    /// A read-only list of <see cref="SiaraCase"/> instances, one per distinct case id. Never throws;
    /// files whose URL cannot be parsed fall back to a single-file case keyed by the file's
    /// <see cref="DownloadableFile.FileName"/> (or the raw URL if the file name is also blank).
    /// </returns>
    public static IReadOnlyList<SiaraCase> GroupByCase(IReadOnlyList<DownloadableFile> files)
    {
        if (files is null || files.Count == 0)
        {
            return Array.Empty<SiaraCase>();
        }

        // Preserve insertion order: LinkedList<> on the ordered key set, Dictionary<> for O(1) append.
        var orderedKeys = new List<string>();
        var groups = new Dictionary<string, List<DownloadableFile>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var caseId = ExtractCaseId(file);

            if (!groups.TryGetValue(caseId, out var bucket))
            {
                bucket = new List<DownloadableFile>();
                groups[caseId] = bucket;
                orderedKeys.Add(caseId);
            }

            bucket.Add(file);
        }

        var result = new List<SiaraCase>(orderedKeys.Count);
        foreach (var key in orderedKeys)
        {
            result.Add(new SiaraCase
            {
                CaseId = key,
                Files = groups[key].AsReadOnly(),
            });
        }

        return result;
    }

    /// <summary>
    /// Derives the case id from a <see cref="DownloadableFile"/>. Never throws.
    /// </summary>
    private static string ExtractCaseId(DownloadableFile file)
    {
        var url = file.Url;

        if (!string.IsNullOrWhiteSpace(url))
        {
            // Try Uri parsing first (works for absolute URLs like https://host/document_store/CASE1/a.pdf).
            string[] segments;
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                // uri.Segments gives [ "/", "document_store/", "CASE1/", "a.pdf" ]
                // Strip trailing slashes and filter blank entries.
                segments = uri.Segments
                    .Select(s => s.TrimEnd('/'))
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToArray();
            }
            else
            {
                // Relative or malformed — split on '/' and filter blanks.
                segments = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
            }

            // We need at least 2 usable segments: [..., caseId, fileName].
            if (segments.Length >= 2)
            {
                return segments[segments.Length - 2];
            }
        }

        // Fallback: use FileName (preferred), else the raw URL, else a static sentinel.
        if (!string.IsNullOrWhiteSpace(file.FileName))
        {
            return file.FileName;
        }

        return string.IsNullOrWhiteSpace(url) ? "__unknown__" : url;
    }
}
