using System;
using System.Collections.Generic;
using System.Linq;
using FuzzySharp;
using FuzzySharp.Extractor;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;

namespace ExxerCube.Prisma.Web.UI.Components.Shared.Navigation;

internal sealed record NavigationSection(string Title, IReadOnlyList<NavigationLink> Links);

internal sealed record NavigationLink(
    string Title,
    string Href,
    string Icon,
    string Description,
    NavLinkMatch Match = NavLinkMatch.All,
    string[]? Tags = null,
    string[]? RequiredRoles = null,
    bool RequiresAuthentication = false,
    string? Policy = null,
    bool IncludeInPrimaryNavigation = true,
    string? DevelopmentSamplePath = null,
    bool IsExample = false)
{
    public string? RolesCsv => RequiredRoles is { Length: > 0 } ? string.Join(',', RequiredRoles) : null;

    public bool RequiresAuthorization => RequiresAuthentication || (RequiredRoles is { Length: > 0 }) || !string.IsNullOrWhiteSpace(Policy);

    public string EffectiveDevelopmentHref => DevelopmentSamplePath ?? Href;
}

internal static class NavigationRegistry
{
    private const int DefaultFuzzyCutoff = 60;

    public static NavigationLink HomeLink { get; } = new(
        Title: "Home",
        Href: "/",
        Icon: Icons.Material.Filled.Home,
        Description: "Return to the ExxerCube Prisma landing page.",
        Match: NavLinkMatch.All,
        Tags: new[] { "overview", "landing" },
        IncludeInPrimaryNavigation: false);

    public static IReadOnlyList<NavigationSection> PrimarySections { get; } =
    [
        new NavigationSection(
            "Document Processing",
            new[]
            {
                new NavigationLink(
                    "Upload & Process",
                    "/document-processing",
                    Icons.Material.Filled.DocumentScanner,
                    "Upload legal documents, run OCR, and monitor real-time processing updates.",
                    Match: NavLinkMatch.All,
                    Tags: new[] { "ocr", "upload", "documents", "pipeline" }),
                new NavigationLink(
                    "Processing Dashboard",
                    "/document-processing-dashboard",
                    Icons.Material.Filled.CloudDownload,
                    "Inspect automation downloads, ingestion metrics, and browser activity.",
                    Match: NavLinkMatch.All,
                    Tags: new[] { "downloads", "ingestion", "browser automation" }),
                new NavigationLink(
                    "Browser Automation",
                    "/browser-automation",
                    Icons.Material.Filled.Language,
                    "Automated browser navigation and document download from Gutenberg and Internet Archive.",
                    Match: NavLinkMatch.All,
                    Tags: new[] { "browser", "automation", "download", "playwright", "gutenberg", "archive" }),
                new NavigationLink(
                    "Analytics Dashboard",
                    "/dashboard",
                    Icons.Material.Filled.Analytics,
                    "Visualize throughput, confidence trends, and processing health.",
                    Match: NavLinkMatch.All,
                    Tags: new[] { "analytics", "metrics", "observability" }),
            }),
        new NavigationSection(
            "Review & Compliance",
            new[]
            {
                new NavigationLink(
                    "Manual Review",
                    "/manual-review",
                    Icons.Material.Filled.Assignment,
                    "Triage low-confidence extractions and assign cases to reviewers.",
                    Match: NavLinkMatch.All,
                    Tags: new[] { "review", "cases", "queue" },
                    RequiredRoles: new[] { "Reviewer", "Admin" }),
                new NavigationLink(
                    "SLA Monitoring",
                    "/sla-dashboard",
                    Icons.Material.Filled.Schedule,
                    "Track SLA commitments and escalations in real time.",
                    Match: NavLinkMatch.All,
                    Tags: new[] { "sla", "timelines", "compliance" }),
                new NavigationLink(
                    "Review Case Detail",
                    "/manual-review/{CaseId}",
                    Icons.Material.Filled.Description,
                    "Direct link to a case detail view (replace {CaseId} with the actual id).",
                    Match: NavLinkMatch.Prefix,
                    Tags: new[] { "case detail", "annotations" },
                    RequiredRoles: new[] { "Reviewer", "Admin" },
                    IncludeInPrimaryNavigation: false,
                    DevelopmentSamplePath: "/manual-review/SAMPLE-CASE-12345"),
            }),
        new NavigationSection(
            "Export & Audit",
            new[]
            {
                new NavigationLink(
                    "Export Management",
                    "/export-management",
                    Icons.Material.Filled.FileDownload,
                    "Curate export batches, monitor delivery pipelines, and requeue failures.",
                    Match: NavLinkMatch.All,
                    Tags: new[] { "export", "delivery", "files" },
                    RequiresAuthentication: true),
                new NavigationLink(
                    "Audit Trail",
                    "/audit-trail",
                    Icons.Material.Filled.History,
                    "Search, filter, and export audit events for compliance reviews.",
                    Match: NavLinkMatch.All,
                    Tags: new[] { "audit", "history", "compliance" },
                    RequiresAuthentication: true),
                new NavigationLink(
                    "Audit Trail (Timeline)",
                    "/audit/viewer",
                    Icons.Material.Filled.Timeline,
                    "Alternate visualization of audit events with export shortcuts.",
                    Match: NavLinkMatch.All,
                    Tags: new[] { "audit", "timeline" },
                    RequiresAuthentication: true,
                    IncludeInPrimaryNavigation: false),
            }),
        new NavigationSection(
            "Administration",
            new[]
            {
                new NavigationLink(
                    "Database Migration",
                    "/admin/database-migration",
                    Icons.Material.Filled.Storage,
                    "Apply and monitor schema migrations from the UI.",
                    Match: NavLinkMatch.All,
                    Tags: new[] { "database", "migration" },
                    RequiredRoles: new[] { "Administrator" }),
                new NavigationLink(
                    "Connection String",
                    "/admin/connection-string",
                    Icons.Material.Filled.Settings,
                    "Manage secure storage for upstream system connection details.",
                    Match: NavLinkMatch.All,
                    Tags: new[] { "configuration", "administration" },
                    RequiredRoles: new[] { "Administrator" }),
            }),
    ];

    public static IReadOnlyList<NavigationLink> ExampleLinks { get; } =
    [
        new NavigationLink(
            "Counter",
            "/counter",
            Icons.Material.Filled.Add,
            "Simple counter example for quick shell tests.",
            Match: NavLinkMatch.All,
            Tags: new[] { "example", "demo" },
            IsExample: true),
        new NavigationLink(
            "Weather",
            "/weather",
            Icons.Material.Filled.List,
            "Sample forecast data feed.",
            Match: NavLinkMatch.All,
            Tags: new[] { "example", "data" },
            IsExample: true),
        new NavigationLink(
            "Auth Required",
            "/auth",
            Icons.Material.Filled.Lock,
            "Quick check to confirm the user identity context.",
            Match: NavLinkMatch.All,
            Tags: new[] { "auth", "identity" },
            RequiresAuthentication: true,
            IsExample: true),
    ];

    public static IReadOnlyList<NavigationLink> AllLinks { get; } =
        new[] { HomeLink }
            .Concat(PrimarySections.SelectMany(section => section.Links))
            .Concat(ExampleLinks)
            .DistinctBy(link => link.Href, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static readonly IReadOnlyList<(NavigationLink Link, string Corpus)> SearchCorpus =
        AllLinks.Select(link => (link, BuildSearchCorpus(link))).ToList();

    private static readonly IReadOnlyList<string> SearchCorpusTexts =
        SearchCorpus.Select(entry => entry.Corpus).ToList();

    public static IReadOnlyList<NavigationLink> PopularLinks { get; } =
        AllLinks
            .Where(link => link.IncludeInPrimaryNavigation && !link.IsExample)
            .Take(6)
            .ToList();

    public static IEnumerable<NavigationLink> GetSuggestions(string? searchTerm, int maxResults = 6)
    {
        var normalized = (searchTerm ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return PopularLinks.Take(maxResults);
        }

        var matches = Process.ExtractTop(
            normalized,
            SearchCorpusTexts,
            limit: maxResults,
            cutoff: DefaultFuzzyCutoff);

        List<ExtractedResult<string>> matchList;
        if (matches is null)
        {
            matchList = new List<ExtractedResult<string>>();
        }
        else
        {
            matchList = matches.ToList();
        }

        if (matchList.Count == 0)
        {
            return PopularLinks.Take(maxResults);
        }

        var orderedMatches = matchList
            .OrderByDescending(match => match.Score)
            .ThenBy(match => SearchCorpus[match.Index].Link.Title, StringComparer.OrdinalIgnoreCase);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var recommendations = new List<NavigationLink>(maxResults);

        foreach (var match in orderedMatches)
        {
            var link = SearchCorpus[match.Index].Link;
            if (seen.Add(link.Href))
            {
                recommendations.Add(link);
                if (recommendations.Count >= maxResults)
                {
                    break;
                }
            }
        }

        return recommendations;
    }

    private static string BuildSearchCorpus(NavigationLink link)
    {
        var tags = link.Tags is { Length: > 0 } ? string.Join(' ', link.Tags) : string.Empty;
        return string.Join(
            ' ',
            link.Title,
            link.Description,
            link.Href,
            tags).ToLowerInvariant();
    }
}
