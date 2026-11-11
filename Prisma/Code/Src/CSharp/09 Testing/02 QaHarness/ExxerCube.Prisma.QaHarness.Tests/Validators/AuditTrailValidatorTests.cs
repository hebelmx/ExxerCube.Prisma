// <copyright file="AuditTrailValidatorTests.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using ExxerCube.Prisma.QaHarness.Validators;
using ExxerCube.Prisma.QaHarness.Validators.Pipeline;

namespace ExxerCube.Prisma.QaHarness.Tests.Validators;

/// <summary>
/// Fast unit tests for <see cref="AuditTrailValidator"/>.
/// Uses in-memory <see cref="AuditRow"/> lists — no Docker, SQL Server, or network required.
/// </summary>
public sealed class AuditTrailValidatorTests
{
    private static readonly AuditTrailValidator Sut = new();

    // ── Conformant: all rows have ProcessId ──────────────────────────────────

    /// <summary>When all rows have a non-empty ProcessId, the result is conformant.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_AllRowsHaveProcessId_IsConformant()
    {
        var rows = new List<AuditRow>
        {
            new("AUDIT-001", "FILE-123", "orion-downloader", "Ingestion"),
            new("AUDIT-002", "FILE-123", "athena-extractor", "Extraction"),
            new("AUDIT-003", "FILE-123", "reconciliator", "Reconciliation"),
        };

        var result = await Sut.ValidateAsync(rows, TestContext.Current.CancellationToken);

        result.ValidatorId.ShouldBe("AUDIT-TRAIL");
        result.IsConformant.ShouldBeTrue();
        result.Findings.ShouldNotContain(f =>
            f.Severity == FindingSeverity.Critical || f.Severity == FindingSeverity.Major);
    }

    // ── Non-conformant: empty collection ─────────────────────────────────────

    /// <summary>An empty collection produces a Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_EmptyCollection_IsNotConformant_WithCriticalFinding()
    {
        var result = await Sut.ValidateAsync([], TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f =>
            f.Severity == FindingSeverity.Critical && f.RuleId == "AUDIT-02");
    }

    // ── Observational (Minor): rows missing ProcessId ────────────────────────
    // ProcessId is nullable by design for legacy/pre-migration rows.  The validator
    // reports a Minor observation — it does NOT set IsConformant=false.

    /// <summary>
    /// A row with null ProcessId is a Minor observation; IsConformant remains true because
    /// ProcessId is nullable-by-design for pre-migration rows (AuditTrailValidator MAJOR 2 fix).
    /// The AuditId still appears in the finding Observed field.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_OneRowMissingProcessId_IsConformant_WithMinorObservation()
    {
        var rows = new List<AuditRow>
        {
            new("AUDIT-001", "FILE-123", "orion-downloader", "Ingestion"),
            new("AUDIT-002", "FILE-123", null,               "Extraction"),  // nullable by design
        };

        var result = await Sut.ValidateAsync(rows, TestContext.Current.CancellationToken);

        // IsConformant must be true — null ProcessId is an observation, not a hard failure.
        result.IsConformant.ShouldBeTrue();
        var finding = result.Findings.FirstOrDefault(f =>
            f.Severity == FindingSeverity.Minor && f.RuleId == "AUDIT-03");
        finding.ShouldNotBeNull();
        finding!.Observed!.ShouldContain("AUDIT-002");
        finding.Expected.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>Whitespace-only ProcessId is also treated as missing — Minor observation only.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_EmptyStringProcessId_IsConformant_WithMinorObservation()
    {
        var rows = new List<AuditRow>
        {
            new("AUDIT-001", "FILE-456", "  ", "Ingestion"),  // whitespace-only ProcessId
        };

        var result = await Sut.ValidateAsync(rows, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeTrue();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Minor && f.RuleId == "AUDIT-03");
        result.Findings.ShouldNotContain(f => f.Severity == FindingSeverity.Major);
    }

    // ── Observational: all rows missing ProcessId ─────────────────────────────

    /// <summary>
    /// When all rows lack ProcessId (all pre-migration), a single Minor observation lists all
    /// AuditIds. The result is still conformant — no Major/Critical findings.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_AllRowsMissingProcessId_IsConformant_MinorObservationListsAll()
    {
        var rows = new List<AuditRow>
        {
            new("AUDIT-A", "FILE-789", null, "Ingestion"),
            new("AUDIT-B", "FILE-789", null, "Extraction"),
            new("AUDIT-C", "FILE-789", null, "Reconciliation"),
        };

        var result = await Sut.ValidateAsync(rows, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeTrue();
        var finding = result.Findings.FirstOrDefault(f =>
            f.Severity == FindingSeverity.Minor && f.RuleId == "AUDIT-03");
        finding.ShouldNotBeNull();
        // Observed should mention all three AuditIds
        finding!.Observed!.ShouldContain("AUDIT-A");
        finding!.Observed!.ShouldContain("AUDIT-B");
        finding!.Observed!.ShouldContain("AUDIT-C");
    }

    // ── Informational finding ────────────────────────────────────────────────

    /// <summary>An Info finding describing row count and stages is always added on non-empty collections.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_NonEmpty_AlwaysHasInfoFinding_WithRowCount()
    {
        var rows = new List<AuditRow>
        {
            new("AUDIT-001", "FILE-1", "process-a", "Ingestion"),
            new("AUDIT-002", "FILE-1", "process-b", "Extraction"),
        };

        var result = await Sut.ValidateAsync(rows, TestContext.Current.CancellationToken);

        var info = result.Findings.FirstOrDefault(f => f.Severity == FindingSeverity.Info);
        info.ShouldNotBeNull();
        info!.Observed!.ShouldContain("2 rows");
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    /// <summary>A pre-cancelled token yields IsConformant=false with a Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_CancelledToken_IsNotConformant()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await Sut.ValidateAsync([], cts.Token);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Critical);
    }

    // ── Null subject guard ────────────────────────────────────────────────────

    /// <summary>A null collection should produce a Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_NullSubject_IsNotConformant_WithCriticalFinding()
    {
        var result = await Sut.ValidateAsync(null!, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Critical);
    }
}
