// <copyright file="FusionOutputValidator.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.IO;
using System.Text.Json;

namespace ExxerCube.Prisma.QaHarness.Validators.Pipeline;

/// <summary>
/// Validates that a <c>.fusion.json</c> handoff artefact produced by the Athena Extractor is
/// present in the specified directory and contains at least the key fields required for the
/// Reconciliator to process it (<c>FileId</c>, <c>CorrelationId</c>).
/// </summary>
/// <remarks>
/// <para>
/// Subject: the directory path to scan for <c>*.fusion.json</c> files (searched recursively).
/// If the directory is absent or contains no fusion files, a Critical finding is raised.
/// </para>
/// <para>
/// "Key fields populated" means the JSON object at the top level has <c>FileId</c> and
/// <c>CorrelationId</c> properties whose values are non-null and non-empty strings.
/// </para>
/// </remarks>
public sealed class FusionOutputValidator : IDomainValidator<string>
{
    /// <inheritdoc/>
    public string ValidatorId => "FUSION-OUTPUT";

    /// <inheritdoc/>
    public string Description =>
        "Observes whether at least one '.fusion.json' file is present in the specified " +
        "shared-storage directory and whether the first file found contains the required " +
        "FileId and CorrelationId fields with non-empty values.";

    /// <inheritdoc/>
    /// <param name="subject">
    /// Absolute path to the shared-storage directory that the Athena Extractor writes
    /// <c>*.fusion.json</c> handoff artefacts into.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel long-running validation steps.</param>
    public async Task<ValidationResult> ValidateAsync(
        string subject,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return NonConformant("FUSION-00", FindingSeverity.Critical,
                "Validation cancelled before it started.", Observed: null, Expected: null);
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            return NonConformant("FUSION-01", FindingSeverity.Critical,
                "Subject directory path is null or empty.",
                Observed: "(null/empty)", Expected: "Non-empty directory path");
        }

        var findings = new List<ValidationFinding>();

        // ── 1. Directory existence ─────────────────────────────────────────
        if (!Directory.Exists(subject))
        {
            findings.Add(new ValidationFinding(
                "FUSION-02", FindingSeverity.Critical,
                "Shared-storage directory does not exist.",
                Observed: subject, Expected: "Existing directory"));
            return new ValidationResult(ValidatorId, IsConformant: false, findings);
        }

        // ── 2. At least one .fusion.json file ─────────────────────────────
        var fusionFiles = Directory.GetFiles(subject, "*.fusion.json", SearchOption.AllDirectories);
        if (fusionFiles.Length == 0)
        {
            findings.Add(new ValidationFinding(
                "FUSION-03", FindingSeverity.Critical,
                "No '.fusion.json' files were found in the shared-storage directory.",
                Observed: "0 fusion files", Expected: "At least 1 .fusion.json file"));
            return new ValidationResult(ValidatorId, IsConformant: false, findings);
        }

        findings.Add(new ValidationFinding(
            "FUSION-INFO", FindingSeverity.Info,
            $"{fusionFiles.Length} '.fusion.json' file(s) found in shared storage.",
            Observed: $"{fusionFiles.Length} files",
            Expected: "At least 1 file"));

        // ── 3. Validate key fields in the first fusion file ────────────────
        var firstFile = fusionFiles[0];
        try
        {
            var json = await File.ReadAllTextAsync(firstFile, cancellationToken)
                .ConfigureAwait(false);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            CheckStringField(root, "FileId", firstFile, "FUSION-04", findings);
            CheckStringField(root, "CorrelationId", firstFile, "FUSION-05", findings);
        }
        catch (JsonException ex)
        {
            findings.Add(new ValidationFinding(
                "FUSION-06", FindingSeverity.Critical,
                $"First .fusion.json file is not valid JSON: {ex.Message}",
                Observed: $"File: {Path.GetFileName(firstFile)}",
                Expected: "Valid JSON object"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            findings.Add(new ValidationFinding(
                "FUSION-07", FindingSeverity.Critical,
                $"Error reading .fusion.json file: {ex.Message}",
                Observed: $"File: {Path.GetFileName(firstFile)}",
                Expected: "Readable JSON file"));
        }

        var isConformant = !findings.Any(f =>
            f.Severity == FindingSeverity.Critical || f.Severity == FindingSeverity.Major);

        return new ValidationResult(ValidatorId, isConformant, findings);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void CheckStringField(
        JsonElement root, string propertyName, string filePath,
        string ruleId, List<ValidationFinding> findings)
    {
        if (!root.TryGetProperty(propertyName, out var prop) ||
            prop.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(prop.GetString()))
        {
            findings.Add(new ValidationFinding(
                ruleId, FindingSeverity.Major,
                $"Required field '{propertyName}' is absent or empty in the .fusion.json file.",
                Observed: root.TryGetProperty(propertyName, out var obs)
                    ? (obs.GetString() ?? "(null)")
                    : "(absent)",
                Expected: $"Non-empty string for '{propertyName}'"));
        }
    }

    private ValidationResult NonConformant(
        string ruleId, FindingSeverity severity, string description,
        string? Observed, string? Expected) =>
        new(ValidatorId, IsConformant: false,
            [new ValidationFinding(ruleId, severity, description, Observed, Expected)]);
}
