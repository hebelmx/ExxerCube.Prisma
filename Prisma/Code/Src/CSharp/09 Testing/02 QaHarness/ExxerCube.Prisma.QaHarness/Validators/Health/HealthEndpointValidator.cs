// <copyright file="HealthEndpointValidator.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.Text.Json;

namespace ExxerCube.Prisma.QaHarness.Validators.Health;

/// <summary>
/// The subject supplied to <see cref="HealthEndpointValidator"/>.
/// Carries the raw JSON body returned by the <c>/health</c> endpoint, plus the HTTP status code.
/// </summary>
/// <param name="StatusCode">The HTTP status code of the health response (200, 503, etc.).</param>
/// <param name="ResponseBody">
/// The raw JSON response body from the health endpoint (Microsoft.Extensions.Diagnostics.HealthChecks
/// format). May be <see langword="null"/> or empty when the endpoint returned no body.
/// </param>
/// <param name="RequiredEntryNames">
/// Optional list of health check entry names that must be present in the <c>entries</c> section of
/// the response.  When empty the validator only checks overall <c>status == "Healthy"</c>.
/// </param>
public sealed record HealthEndpointSubject(
    int StatusCode,
    string? ResponseBody,
    IReadOnlyList<string>? RequiredEntryNames = null);

/// <summary>
/// Validates a health-endpoint response for Prisma services.  Accepts a
/// <see cref="HealthEndpointSubject"/> containing the HTTP status code, the raw JSON body, and an
/// optional list of required health-check entry names.
/// </summary>
/// <remarks>
/// Conformance means: HTTP 200, top-level <c>status</c> field equals <c>"Healthy"</c>, and every
/// entry listed in <see cref="HealthEndpointSubject.RequiredEntryNames"/> is present in the
/// <c>entries</c> object.  A <c>"Degraded"</c> status produces a Minor finding (service is
/// responsive but not fully healthy).  An HTTP 503 or <c>"Unhealthy"</c> produces a Major finding.
/// </remarks>
public sealed class HealthEndpointValidator : IDomainValidator<HealthEndpointSubject>
{
    /// <inheritdoc/>
    public string ValidatorId => "HEALTH-ENDPOINT";

    /// <inheritdoc/>
    public string Description =>
        "Observes the HTTP status code and JSON body returned by the /health endpoint. " +
        "Checks that the overall status is 'Healthy', that the HTTP code is 200, and that " +
        "each required health-check entry name is present.";

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
                Observed: "(empty)", Expected: "Non-empty JSON body"));
            return new ValidationResult(ValidatorId, IsConformant: false, findings);
        }

        // ── 3. Parse JSON ─────────────────────────────────────────────────
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(subject.ResponseBody);
        }
        catch (JsonException ex)
        {
            findings.Add(new ValidationFinding(
                "HEALTH-04", FindingSeverity.Critical,
                $"Health endpoint response body is not valid JSON: {ex.Message}",
                Observed: subject.ResponseBody[..Math.Min(200, subject.ResponseBody.Length)],
                Expected: "Valid JSON object"));
            return new ValidationResult(ValidatorId, IsConformant: false, findings);
        }

        using (doc)
        {
            // ── 4. Top-level status field ──────────────────────────────────
            if (!doc.RootElement.TryGetProperty("status", out var statusProp))
            {
                findings.Add(new ValidationFinding(
                    "HEALTH-05", FindingSeverity.Major,
                    "Health response JSON does not contain a 'status' field.",
                    Observed: "(absent)", Expected: "\"status\": \"Healthy\""));
            }
            else
            {
                var statusValue = statusProp.GetString() ?? string.Empty;
                if (!string.Equals(statusValue, "Healthy", StringComparison.OrdinalIgnoreCase))
                {
                    var severity = string.Equals(statusValue, "Degraded", StringComparison.OrdinalIgnoreCase)
                        ? FindingSeverity.Minor
                        : FindingSeverity.Major;

                    findings.Add(new ValidationFinding(
                        "HEALTH-05", severity,
                        $"Health endpoint status is not 'Healthy'.",
                        Observed: statusValue,
                        Expected: "Healthy"));
                }
            }

            // ── 5. Required entry names ────────────────────────────────────
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

        var isConformant = !findings.Any(f =>
            f.Severity == FindingSeverity.Critical || f.Severity == FindingSeverity.Major);

        return new ValidationResult(ValidatorId, isConformant, findings);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ValidationResult NonConformant(
        string ruleId, FindingSeverity severity, string description,
        string? Observed, string? Expected) =>
        new(ValidatorId, IsConformant: false,
            [new ValidationFinding(ruleId, severity, description, Observed, Expected)]);
}
