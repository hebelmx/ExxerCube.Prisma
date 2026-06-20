// <copyright file="HealthEndpointValidator.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.Text.Json;

namespace ExxerCube.Prisma.QaHarness.Validators.Health;

/// <summary>
/// The subject supplied to <see cref="HealthEndpointValidator"/>.
/// Carries the raw response body returned by the <c>/health</c> endpoint, plus the HTTP status
/// code. The body may be plain text (<c>Healthy</c> / <c>Degraded</c> / <c>Unhealthy</c>)
/// as emitted by the default ASP.NET Core health-check middleware, or the Microsoft JSON format
/// emitted when a custom <c>ResponseWriter</c> is configured.
/// </summary>
/// <param name="StatusCode">The HTTP status code of the health response (200, 503, etc.).</param>
/// <param name="ResponseBody">
/// The raw response body from the health endpoint. May be <see langword="null"/> or empty when
/// the endpoint returned no body. The validator handles both plain-text bodies
/// (<c>"Healthy"</c> / <c>"Degraded"</c> / <c>"Unhealthy"</c>) and JSON bodies that contain a
/// top-level <c>"status"</c> field (Microsoft.Extensions.Diagnostics.HealthChecks format).
/// </param>
/// <param name="RequiredEntryNames">
/// Optional list of health check entry names that must be present in the <c>entries</c> section of
/// a JSON-format response.  When empty the validator only checks overall status == "Healthy".
/// Entry-name checks are silently skipped when the response is plain-text.
/// </param>
public sealed record HealthEndpointSubject(
    int StatusCode,
    string? ResponseBody,
    IReadOnlyList<string>? RequiredEntryNames = null);

/// <summary>
/// Validates a health-endpoint response for Prisma services.  Accepts a
/// <see cref="HealthEndpointSubject"/> containing the HTTP status code, the raw response body,
/// and an optional list of required health-check entry names.
/// </summary>
/// <remarks>
/// <para>
/// The validator handles two body formats:
/// <list type="bullet">
///   <item><description>
///     <b>Plain text</b> — the default ASP.NET Core health-check response
///     (<c>app.MapHealthChecks</c> with no <c>ResponseWriter</c> override). The body is one of
///     <c>Healthy</c>, <c>Degraded</c>, or <c>Unhealthy</c>.
///   </description></item>
///   <item><description>
///     <b>JSON</b> — the Microsoft <c>HealthCheckOptions.ResponseWriter</c> JSON format, which
///     includes a top-level <c>"status"</c> field and an optional <c>"entries"</c> object.
///   </description></item>
/// </list>
/// </para>
/// <para>
/// Conformance means: HTTP 200 AND status text (from either format) equals <c>"Healthy"</c>.
/// A <c>"Degraded"</c> status produces a Minor finding.  An HTTP 503 or <c>"Unhealthy"</c>
/// produces a Major finding.  Required entry-name checks are only applied for JSON-format
/// responses.
/// </para>
/// </remarks>
public sealed class HealthEndpointValidator : IDomainValidator<HealthEndpointSubject>
{
    /// <inheritdoc/>
    public string ValidatorId => "HEALTH-ENDPOINT";

    /// <inheritdoc/>
    public string Description =>
        "Observes the HTTP status code and response body returned by the /health endpoint. " +
        "Handles both plain-text bodies (Healthy/Degraded/Unhealthy) and Microsoft JSON format. " +
        "Checks that the overall status is 'Healthy', that the HTTP code is 200, and that " +
        "each required health-check entry name is present (JSON format only).";

    /// <inheritdoc/>
    public Task<ValidationResult> ValidateAsync(
        HealthEndpointSubject subject,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(NonConformant("HEALTH-00", FindingSeverity.Critical,
                "Validation cancelled before it started.", Observed: null, Expected: null));
        }

        if (subject is null)
        {
            return Task.FromResult(NonConformant("HEALTH-01", FindingSeverity.Critical,
                "Validation subject is null.", Observed: "(null)", Expected: "HealthEndpointSubject"));
        }

        return Task.FromResult(ValidateInternal(subject));
    }

    private ValidationResult ValidateInternal(HealthEndpointSubject subject)
    {
        var findings = new List<ValidationFinding>();

        // ── 1. HTTP status code ────────────────────────────────────────────
        if (subject.StatusCode != 200)
        {
            var severity = subject.StatusCode == 503
                ? FindingSeverity.Major
                : FindingSeverity.Critical;

            findings.Add(new ValidationFinding(
                "HEALTH-02", severity,
                "Health endpoint did not return HTTP 200.",
                Observed: $"HTTP {subject.StatusCode}",
                Expected: "HTTP 200"));
        }

        // ── 2. Response body present ───────────────────────────────────────
        if (string.IsNullOrWhiteSpace(subject.ResponseBody))
        {
            findings.Add(new ValidationFinding(
                "HEALTH-03", FindingSeverity.Major,
                "Health endpoint response body is absent or empty.",
                Observed: "(empty)", Expected: "Non-empty body (plain text or JSON)"));
            return new ValidationResult(ValidatorId, IsConformant: false, findings);
        }

        // ── 3. Detect body format and extract status ───────────────────────
        // The default ASP.NET Core health-check middleware (MapHealthChecks with no
        // ResponseWriter override) emits plain text: "Healthy", "Degraded", or "Unhealthy".
        // Custom ResponseWriter configurations emit JSON with a "status" field.
        var trimmedBody = subject.ResponseBody.Trim();
        string statusValue;
        bool isPlainText;

        if (IsKnownPlainTextStatus(trimmedBody))
        {
            // Plain-text path — body IS the status word.
            statusValue = trimmedBody;
            isPlainText = true;
        }
        else
        {
            // Attempt JSON parse.
            isPlainText = false;
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(subject.ResponseBody);
            }
            catch (JsonException ex)
            {
                findings.Add(new ValidationFinding(
                    "HEALTH-04", FindingSeverity.Critical,
                    $"Health endpoint response body is neither a known plain-text status nor valid JSON: {ex.Message}",
                    Observed: subject.ResponseBody[..Math.Min(200, subject.ResponseBody.Length)],
                    Expected: "Plain-text 'Healthy'/'Degraded'/'Unhealthy' or valid JSON object with 'status' field"));
                return new ValidationResult(ValidatorId, IsConformant: false, findings);
            }

            using (doc)
            {
                // ── 3a. Top-level status field ─────────────────────────────
                if (!doc.RootElement.TryGetProperty("status", out var statusProp))
                {
                    findings.Add(new ValidationFinding(
                        "HEALTH-05", FindingSeverity.Major,
                        "Health response JSON does not contain a 'status' field.",
                        Observed: "(absent)", Expected: "\"status\": \"Healthy\""));

                    // Without a status we cannot check entries either; return early.
                    return new ValidationResult(
                        ValidatorId,
                        IsConformant: !findings.Any(f =>
                            f.Severity == FindingSeverity.Critical || f.Severity == FindingSeverity.Major),
                        findings);
                }

                statusValue = statusProp.GetString() ?? string.Empty;

                // ── 3b. Required entry names (JSON only) ───────────────────
                var requiredEntries = subject.RequiredEntryNames ?? Array.Empty<string>();
                if (requiredEntries.Count > 0)
                {
                    if (!doc.RootElement.TryGetProperty("entries", out var entriesProp) ||
                        entriesProp.ValueKind != JsonValueKind.Object)
                    {
                        findings.Add(new ValidationFinding(
                            "HEALTH-06", FindingSeverity.Major,
                            "Health response JSON does not contain an 'entries' object but entry checks were requested.",
                            Observed: "(absent)", Expected: "'entries' object with named health checks"));
                    }
                    else
                    {
                        foreach (var entryName in requiredEntries)
                        {
                            if (!entriesProp.TryGetProperty(entryName, out _))
                            {
                                findings.Add(new ValidationFinding(
                                    $"HEALTH-07-{entryName}", FindingSeverity.Major,
                                    $"Required health check entry '{entryName}' is missing from the response.",
                                    Observed: "(absent)", Expected: $"Entry '{entryName}' present"));
                            }
                        }
                    }
                }
            }
        }

        // ── 4. Evaluate status value ───────────────────────────────────────
        if (!string.Equals(statusValue, "Healthy", StringComparison.OrdinalIgnoreCase))
        {
            var severity = string.Equals(statusValue, "Degraded", StringComparison.OrdinalIgnoreCase)
                ? FindingSeverity.Minor
                : FindingSeverity.Major;

            var format = isPlainText ? "plain-text" : "JSON";
            findings.Add(new ValidationFinding(
                "HEALTH-05", severity,
                $"Health endpoint status is not 'Healthy' ({format} body).",
                Observed: statusValue,
                Expected: "Healthy"));
        }

        var isConformant = !findings.Any(f =>
            f.Severity == FindingSeverity.Critical || f.Severity == FindingSeverity.Major);

        return new ValidationResult(ValidatorId, isConformant, findings);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsKnownPlainTextStatus(string trimmed) =>
        string.Equals(trimmed, "Healthy",   StringComparison.OrdinalIgnoreCase) ||
        string.Equals(trimmed, "Degraded",  StringComparison.OrdinalIgnoreCase) ||
        string.Equals(trimmed, "Unhealthy", StringComparison.OrdinalIgnoreCase);

    private ValidationResult NonConformant(
        string ruleId, FindingSeverity severity, string description,
        string? Observed, string? Expected) =>
        new(ValidatorId, IsConformant: false,
            [new ValidationFinding(ruleId, severity, description, Observed, Expected)]);
}
