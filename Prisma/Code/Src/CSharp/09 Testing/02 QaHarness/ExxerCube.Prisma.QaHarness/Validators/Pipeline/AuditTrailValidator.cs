// <copyright file="AuditTrailValidator.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Validators.Pipeline;

/// <summary>
/// A lightweight projection of an audit record sufficient for the harness validator.
/// Keeps the validator data-source-agnostic: callers project SQL rows, in-memory lists, or
/// JSON-deserialized records into this type before calling <see cref="AuditTrailValidator"/>.
/// </summary>
/// <param name="AuditId">Unique identifier for the audit entry.</param>
/// <param name="FileId">The document/file identifier this row is associated with, or <see langword="null"/>.</param>
/// <param name="ProcessId">
/// The process/actor identity that produced this record.  Nullable by design — rows written
/// before migration <c>20260613122859_AddAuditProcessId</c> do not carry a ProcessId.
/// The three pipeline workers (Orion, Athena, Reconciliator) populate this field per
/// PRISMA-E2-S5.  A null value is reported as a Minor observation, not a conformance failure.
/// </param>
/// <param name="Stage">A descriptive label for the processing stage (e.g. "Ingestion", "Extraction").</param>
public sealed record AuditRow(
    string AuditId,
    string? FileId,
    string? ProcessId,
    string Stage);

/// <summary>
/// Validates an in-memory collection of <see cref="AuditRow"/> records against the audit-trail
/// completeness rules: at least one row must be present.
/// </summary>
/// <remarks>
/// <para>
/// The subject is kept data-source-agnostic (<see cref="IReadOnlyList{T}"/> of <see cref="AuditRow"/>)
/// so a fast test can pass an in-memory list and an integration test can pass SQL-projected rows,
/// without the validator knowing about EF Core or SQL Server.
/// </para>
/// <para>
/// <b>ProcessId nullability:</b> <see cref="AuditRow.ProcessId"/> is nullable because the
/// underlying domain column was added by migration <c>20260613122859_AddAuditProcessId</c> and
/// pre-migration rows have a null value.  The validator reports a <see cref="FindingSeverity.Minor"/>
/// observation when rows are missing a ProcessId; it does NOT drive <c>IsConformant</c> to
/// <see langword="false"/> solely on that basis.  Only structural problems (zero rows when rows
/// were expected) produce a Critical/Major finding that marks the result non-conformant.
/// </para>
/// </remarks>
public sealed class AuditTrailValidator : IDomainValidator<IReadOnlyList<AuditRow>>
{
    /// <inheritdoc/>
    public string ValidatorId => "AUDIT-TRAIL";

    /// <inheritdoc/>
    public string Description =>
        "Observes whether a collection of audit rows is non-empty. Rows missing a ProcessId " +
        "are reported as a Minor observation (ProcessId is nullable for legacy/pre-migration rows; " +
        "the 3 workers populate it per PRISMA-E2-S5) and do not affect conformance.";

    /// <inheritdoc/>
    /// <param name="subject">
    /// The collection of audit rows to validate. An empty collection produces a
    /// <see cref="FindingSeverity.Critical"/> finding.
    /// </param>
    /// <param name="cancellationToken">Token checked before validation begins.</param>
    public Task<ValidationResult> ValidateAsync(
        IReadOnlyList<AuditRow> subject,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(NonConformant("AUDIT-00", FindingSeverity.Critical,
                "Validation cancelled before it started.", Observed: null, Expected: null));
        }

        if (subject is null)
        {
            return Task.FromResult(NonConformant("AUDIT-01", FindingSeverity.Critical,
                "Audit row collection is null.", Observed: "(null)", Expected: "Non-null collection"));
        }

        return Task.FromResult(ValidateInternal(subject));
    }

    private ValidationResult ValidateInternal(IReadOnlyList<AuditRow> rows)
    {
        var findings = new List<ValidationFinding>();

        // ── 1. At least one row must be present ────────────────────────────
        if (rows.Count == 0)
        {
            findings.Add(new ValidationFinding(
                "AUDIT-02", FindingSeverity.Critical,
                "Audit trail is empty — no rows were persisted for this operation.",
                Observed: "0 rows", Expected: "At least 1 audit row"));
            return new ValidationResult(ValidatorId, IsConformant: false, findings);
        }

        // ── 2. Observe rows missing ProcessId (Minor — nullable by design) ─
        // ProcessId was added by migration 20260613122859_AddAuditProcessId.  Pre-migration rows
        // legitimately have a null value.  This is reported as a Minor observation so the QA agent
        // is aware, but it does NOT drive IsConformant to false.
        var missingProcessId = rows
            .Where(r => string.IsNullOrWhiteSpace(r.ProcessId))
            .ToList();

        if (missingProcessId.Count > 0)
        {
            var ids = string.Join(", ", missingProcessId.Select(r => r.AuditId));
            findings.Add(new ValidationFinding(
                "AUDIT-03", FindingSeverity.Minor,
                $"{missingProcessId.Count} audit row(s) have a null or empty ProcessId. " +
                "ProcessId is nullable for legacy/pre-migration rows; the 3 pipeline workers " +
                "populate it per PRISMA-E2-S5. This is an observation, not a conformance failure.",
                Observed: $"Rows without ProcessId: [{ids}]",
                Expected: "ProcessId populated by Orion/Athena/Reconciliator workers (new rows)"));
        }

        // ── 3. Informational: row count and distinct stages ────────────────
        var stages = rows.Select(r => r.Stage).Distinct().OrderBy(s => s).ToList();
        findings.Add(new ValidationFinding(
            "AUDIT-INFO", FindingSeverity.Info,
            $"Audit trail contains {rows.Count} row(s) across stage(s): {string.Join(", ", stages)}.",
            Observed: $"{rows.Count} rows, stages: [{string.Join(", ", stages)}]",
            Expected: null));

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
