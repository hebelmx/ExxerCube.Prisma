// <copyright file="HtmlReportWriter.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.IO;
using System.Text;

namespace ExxerCube.Prisma.QaHarness.Reporting;

/// <summary>
/// Produces an HTML report by wrapping the Markdown output from
/// <see cref="MarkdownReportWriter"/> in a minimal HTML shell with inline CSS.
/// </summary>
/// <remarks>
/// No external dependencies are required — the HTML is built entirely from
/// <see cref="StringBuilder"/> using inline CSS. The Markdown content is embedded
/// inside a &lt;pre&gt; block (fidelity over rendering; a future enhancement could
/// integrate a proper Markdown-to-HTML converter if available in the solution).
/// </remarks>
public sealed class HtmlReportWriter : IReportWriter
{
    /// <inheritdoc/>
    public string Format => "html";

    /// <inheritdoc/>
    public async Task<Result<string>> WriteAsync(
        HarnessRunSummary summary,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Result<string>.WithFailure("WriteAsync was cancelled.");

        ArgumentNullException.ThrowIfNull(summary);
        if (string.IsNullOrWhiteSpace(outputPath))
            return Result<string>.WithFailure("outputPath must not be null or empty.");

        try
        {
            var html = Render(summary);

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(outputPath, html, Encoding.UTF8, cancellationToken)
                .ConfigureAwait(false);

            return Result<string>.WithSuccess(outputPath);
        }
        catch (OperationCanceledException)
        {
            return Result<string>.WithFailure("HTML report write was cancelled.");
        }
        catch (Exception ex)
        {
            return Result<string>.WithFailure($"Failed to write HTML report: {ex.Message}");
        }
    }

    /// <summary>
    /// Renders a <see cref="HarnessRunSummary"/> to an HTML string without writing to disk.
    /// </summary>
    /// <param name="summary">The harness run summary to render.</param>
    /// <returns>The rendered HTML document as a string.</returns>
    public static string Render(HarnessRunSummary summary)
    {
        var markdownContent = MarkdownReportWriter.Render(summary);
        var escaped = HtmlEncode(markdownContent);

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        sb.AppendLine($"  <title>QA Harness Report — {summary.RunId}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: system-ui, -apple-system, sans-serif; background: #f8f9fa; color: #212529; margin: 0; padding: 2rem; }");
        sb.AppendLine("    .container { max-width: 1100px; margin: 0 auto; background: #fff; border: 1px solid #dee2e6; border-radius: 8px; padding: 2rem; }");
        sb.AppendLine("    h1 { color: #1a1a2e; border-bottom: 2px solid #dee2e6; padding-bottom: 0.5rem; }");
        sb.AppendLine("    .meta { background: #f0f4ff; border-left: 4px solid #4a6cf7; padding: 1rem; margin-bottom: 1.5rem; border-radius: 4px; font-size: 0.9rem; }");
        sb.AppendLine("    .notice { background: #fff3cd; border: 1px solid #ffc107; padding: 0.75rem; border-radius: 4px; margin-bottom: 1.5rem; font-size: 0.85rem; }");
        sb.AppendLine("    pre { background: #f1f3f5; padding: 1.5rem; border-radius: 6px; overflow-x: auto; font-size: 0.85rem; line-height: 1.6; white-space: pre-wrap; word-break: break-word; }");
        sb.AppendLine("    footer { margin-top: 2rem; font-size: 0.75rem; color: #6c757d; text-align: center; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");
        sb.AppendLine("    <h1>ExxerCube.Prisma — QA Harness Report</h1>");
        sb.AppendLine("    <div class=\"meta\">");
        sb.AppendLine($"      <strong>Run ID:</strong> {summary.RunId}&nbsp;&nbsp;");
        sb.AppendLine($"      <strong>Version:</strong> {summary.ProductVersion}&nbsp;&nbsp;");
        sb.AppendLine($"      <strong>Generated:</strong> {summary.FinishedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"notice\">");
        sb.AppendLine("      <strong>Note:</strong> This report records <em>observed data only</em>.");
        sb.AppendLine("      No pass/fail verdicts are rendered by the harness. A separate QA agent interprets the findings.");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <pre>");
        sb.Append(escaped);
        sb.AppendLine("    </pre>");
        sb.AppendLine("    <footer>Generated by ExxerCube.Prisma QA Harness</footer>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string HtmlEncode(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
}
