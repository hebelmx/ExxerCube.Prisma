// <copyright file="HealthEndpointValidatorTests.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using ExxerCube.Prisma.QaHarness.Validators;
using ExxerCube.Prisma.QaHarness.Validators.Health;

namespace ExxerCube.Prisma.QaHarness.Tests.Validators;

/// <summary>
/// Fast unit tests for <see cref="HealthEndpointValidator"/>.
/// Uses planted JSON strings — no Docker, HTTP, or network access required.
/// </summary>
public sealed class HealthEndpointValidatorTests
{
    private static readonly HealthEndpointValidator Sut = new();

    // ── Planted JSON fixtures ─────────────────────────────────────────────────

    private const string HealthyBody = """
        {
          "status": "Healthy",
          "totalDuration": "00:00:00.0123456",
          "entries": {
            "database": { "status": "Healthy", "duration": "00:00:00.0050000" },
            "storage":  { "status": "Healthy", "duration": "00:00:00.0010000" }
          }
        }
        """;

    private const string DegradedBody = """
        {
          "status": "Degraded",
          "totalDuration": "00:00:00.5000000",
          "entries": {
            "database": { "status": "Healthy" },
            "storage":  { "status": "Degraded", "description": "Storage latency high" }
          }
        }
        """;

    private const string UnhealthyBody = """
        {
          "status": "Unhealthy",
          "totalDuration": "00:00:05.0000000",
          "entries": {
            "database": { "status": "Unhealthy", "description": "Connection refused" }
          }
        }
        """;

    // ── Conformant: plain-text "Healthy" (real ASP.NET Core default) ────────────
    // The default app.MapHealthChecks("/health") with no ResponseWriter override emits
    // plain text "Healthy" / "Degraded" / "Unhealthy", NOT JSON.

    /// <summary>HTTP 200 + plain-text body "Healthy" → conformant (real production path).</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_PlainTextHealthy_IsConformant()
    {
        var subject = new HealthEndpointSubject(StatusCode: 200, ResponseBody: "Healthy");

        var result = await Sut.ValidateAsync(subject, TestContext.Current.CancellationToken);

        result.ValidatorId.ShouldBe("HEALTH-ENDPOINT");
        result.IsConformant.ShouldBeTrue();
        result.Findings.ShouldNotContain(f =>
            f.Severity == FindingSeverity.Critical || f.Severity == FindingSeverity.Major);
    }

    /// <summary>HTTP 200 + plain-text body "Degraded" → Minor finding, IsConformant stays true.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_PlainTextDegraded_IsConformant_WithMinorFinding()
    {
        var subject = new HealthEndpointSubject(StatusCode: 200, ResponseBody: "Degraded");

        var result = await Sut.ValidateAsync(subject, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeTrue();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Minor);
        result.Findings.ShouldNotContain(f =>
            f.Severity == FindingSeverity.Critical || f.Severity == FindingSeverity.Major);
    }

    /// <summary>HTTP 200 + plain-text body "Unhealthy" → Major finding, IsConformant=false.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_PlainTextUnhealthy_IsNotConformant_WithMajorFinding()
    {
        var subject = new HealthEndpointSubject(StatusCode: 200, ResponseBody: "Unhealthy");

        var result = await Sut.ValidateAsync(subject, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Major);
    }

    // ── Conformant: Healthy + 200 (JSON format) ───────────────────────────────

    /// <summary>HTTP 200 + status=Healthy JSON body with no required entries → conformant, no Major/Critical findings.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_HealthyStatus_IsConformant()
    {
        var subject = new HealthEndpointSubject(StatusCode: 200, ResponseBody: HealthyBody);

        var result = await Sut.ValidateAsync(subject, TestContext.Current.CancellationToken);

        result.ValidatorId.ShouldBe("HEALTH-ENDPOINT");
        result.IsConformant.ShouldBeTrue();
        result.Findings.ShouldNotContain(f =>
            f.Severity == FindingSeverity.Critical || f.Severity == FindingSeverity.Major);
    }

    /// <summary>HTTP 200 + Healthy + all required entries present → conformant.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_HealthyWithRequiredEntries_IsConformant()
    {
        var subject = new HealthEndpointSubject(
            StatusCode: 200,
            ResponseBody: HealthyBody,
            RequiredEntryNames: ["database", "storage"]);

        var result = await Sut.ValidateAsync(subject, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeTrue();
    }

    // ── Non-conformant: Degraded ──────────────────────────────────────────────

    /// <summary>HTTP 200 + status=Degraded → Minor finding (not Major/Critical), still IsConformant=true (no Critical/Major).</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_DegradedStatus_IsConformant_WithMinorFinding()
    {
        // Degraded = Minor severity, so IsConformant remains true by the rule
        // (only Critical/Major block conformance).
        var subject = new HealthEndpointSubject(StatusCode: 200, ResponseBody: DegradedBody);

        var result = await Sut.ValidateAsync(subject, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeTrue();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Minor);
        result.Findings.ShouldNotContain(f =>
            f.Severity == FindingSeverity.Critical || f.Severity == FindingSeverity.Major);
    }

    // ── Non-conformant: Unhealthy ─────────────────────────────────────────────

    /// <summary>HTTP 503 + status=Unhealthy → Major finding on status field + Major on HTTP code.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_UnhealthyStatus_IsNotConformant_WithMajorFindings()
    {
        var subject = new HealthEndpointSubject(StatusCode: 503, ResponseBody: UnhealthyBody);

        var result = await Sut.ValidateAsync(subject, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Major);
    }

    // ── Non-conformant: wrong HTTP code ──────────────────────────────────────

    /// <summary>HTTP 401 + Healthy body → Major finding for wrong HTTP code.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_WrongHttpCode_IsNotConformant_WithMajorFinding()
    {
        var subject = new HealthEndpointSubject(StatusCode: 401, ResponseBody: HealthyBody);

        var result = await Sut.ValidateAsync(subject, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        var finding = result.Findings.FirstOrDefault(f => f.RuleId == "HEALTH-02");
        finding.ShouldNotBeNull();
        finding!.Observed.ShouldBe("HTTP 401");
        finding.Expected.ShouldBe("HTTP 200");
    }

    // ── Non-conformant: missing required entry ────────────────────────────────

    /// <summary>A required entry name not present in the response → Major finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_MissingRequiredEntry_IsNotConformant_WithMajorFinding()
    {
        var subject = new HealthEndpointSubject(
            StatusCode: 200,
            ResponseBody: HealthyBody,
            RequiredEntryNames: ["database", "missing-check"]);

        var result = await Sut.ValidateAsync(subject, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f =>
            f.RuleId.Contains("missing-check") && f.Severity == FindingSeverity.Major);
    }

    // ── Non-conformant: empty body ────────────────────────────────────────────

    /// <summary>An empty response body produces a Major finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_EmptyBody_IsNotConformant_WithMajorFinding()
    {
        var subject = new HealthEndpointSubject(StatusCode: 200, ResponseBody: string.Empty);

        var result = await Sut.ValidateAsync(subject, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f =>
            f.Severity == FindingSeverity.Major && f.RuleId == "HEALTH-03");
    }

    // ── Non-conformant: malformed JSON ────────────────────────────────────────

    /// <summary>Malformed JSON body produces a Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_MalformedJson_IsNotConformant_WithCriticalFinding()
    {
        var subject = new HealthEndpointSubject(StatusCode: 200, ResponseBody: "{ not json");

        var result = await Sut.ValidateAsync(subject, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Critical);
    }

    // ── Non-conformant: missing status field ─────────────────────────────────

    /// <summary>JSON that lacks a 'status' field produces a Major finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_MissingStatusField_IsNotConformant_WithMajorFinding()
    {
        const string noStatus = """{ "entries": {} }""";
        var subject = new HealthEndpointSubject(StatusCode: 200, ResponseBody: noStatus);

        var result = await Sut.ValidateAsync(subject, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f =>
            f.RuleId == "HEALTH-05" && f.Severity == FindingSeverity.Major);
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    /// <summary>A pre-cancelled token yields IsConformant=false with a Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_CancelledToken_IsNotConformant()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var subject = new HealthEndpointSubject(StatusCode: 200, ResponseBody: HealthyBody);
        var result = await Sut.ValidateAsync(subject, cts.Token);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Critical);
    }

    // ── Null subject guard ────────────────────────────────────────────────────

    /// <summary>A null subject should produce a Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_NullSubject_IsNotConformant_WithCriticalFinding()
    {
        var result = await Sut.ValidateAsync(null!, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Critical);
    }

    // ── Findings carry Observed / Expected ───────────────────────────────────

    /// <summary>Each non-info finding should carry non-null Observed and Expected when applicable.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_WrongHttpCode_FindingCarriesObservedAndExpected()
    {
        var subject = new HealthEndpointSubject(StatusCode: 503, ResponseBody: UnhealthyBody);
        var result = await Sut.ValidateAsync(subject, TestContext.Current.CancellationToken);

        var httpFinding = result.Findings.First(f => f.RuleId == "HEALTH-02");
        httpFinding.Observed.ShouldNotBeNullOrWhiteSpace();
        httpFinding.Expected.ShouldNotBeNullOrWhiteSpace();
    }
}
