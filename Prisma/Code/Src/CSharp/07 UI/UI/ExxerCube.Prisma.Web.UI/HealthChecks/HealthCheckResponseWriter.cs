using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ExxerCube.Prisma.Web.UI.HealthChecks;

/// <summary>
/// Writes a structured JSON response body for ASP.NET Core health-check endpoints.
/// </summary>
/// <remarks>
/// Plug in via <see cref="HealthCheckOptions.ResponseWriter"/>.
/// Output shape:
/// <code>
/// {
///   "status": "Healthy",
///   "totalDuration": "00:00:00.0123456",
///   "entries": {
///     "prisma-db": { "status": "Healthy", "description": "Database is reachable", "duration": "00:00:00.0120000" }
///   }
/// }
/// </code>
/// Uses only <c>System.Text.Json</c> — no additional NuGet packages required.
/// </remarks>
internal static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Delegate-compatible writer for <see cref="HealthCheckOptions.ResponseWriter"/>.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="report">The health report produced by the framework.</param>
    /// <returns>A task representing the async write operation.</returns>
    public static async Task WriteJsonResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.ToString(),
            entries = report.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.ToString(),
                    exception = e.Value.Exception?.Message
                })
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, s_options),
            cancellationToken: context.RequestAborted)
            .ConfigureAwait(false);
    }
}
