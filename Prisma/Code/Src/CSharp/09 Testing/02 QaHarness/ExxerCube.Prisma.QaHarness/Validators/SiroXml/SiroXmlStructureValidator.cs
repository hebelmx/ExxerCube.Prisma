// <copyright file="SiroXmlStructureValidator.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.IO;
using System.Xml.Linq;

namespace ExxerCube.Prisma.QaHarness.Validators.SiroXml;

/// <summary>
/// Validates that a SIRO XML document contains the required structural elements and uses the
/// correct namespace. Accepts a file path (<see cref="string"/>) as the subject; the file is
/// read and parsed inside <see cref="ValidateAsync"/>. Malformed or missing files are represented
/// as <see cref="FindingSeverity.Critical"/> findings — never thrown.
/// </summary>
public sealed class SiroXmlStructureValidator : IDomainValidator<string>
{
    /// <summary>The SIRO regulatory XML namespace URI.</summary>
    public const string SiroNamespace = "http://siro.regulatory.namespace";

    /// <summary>Required elements that every SIRO XML document must contain (by local name).</summary>
    private static readonly string[] RequiredElements =
    [
        "SiroResponse",
        "NumeroExpediente",
        "NumeroOficio",
    ];

    /// <inheritdoc/>
    public string ValidatorId => "SIRO-STRUCT";

    /// <inheritdoc/>
    public string Description =>
        "Observes whether a SIRO XML file contains the required root element, " +
        "regulatory namespace, and mandatory child elements (NumeroExpediente, NumeroOficio).";

    /// <inheritdoc/>
    /// <param name="subject">Absolute or repo-relative path to a <c>.siro.xml</c> file.</param>
    /// <param name="cancellationToken">Token used to cancel long-running validation steps.</param>
    public async Task<ValidationResult> ValidateAsync(
        string subject,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return NonConformant("SIRO-STRUCT-00", FindingSeverity.Critical,
                "Validation cancelled before it started.", null, null);
        }

        var findings = new List<ValidationFinding>();

        // ── 1. File existence ──────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(subject))
        {
            findings.Add(new ValidationFinding(
                "SIRO-STRUCT-01", FindingSeverity.Critical,
                "Subject path is null or empty; cannot locate SIRO XML file.",
                Observed: subject, Expected: "A non-empty file path"));
            return new ValidationResult(ValidatorId, IsConformant: false, findings);
        }

        if (!File.Exists(subject))
        {
            findings.Add(new ValidationFinding(
                "SIRO-STRUCT-01", FindingSeverity.Critical,
                "SIRO XML file does not exist at the specified path.",
                Observed: subject, Expected: "Existing file path"));
            return new ValidationResult(ValidatorId, IsConformant: false, findings);
        }

        // ── 2. Parse XML ───────────────────────────────────────────────────
        XDocument doc;
        try
        {
            var content = await File.ReadAllTextAsync(subject, cancellationToken)
                .ConfigureAwait(false);
            doc = XDocument.Parse(content);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            findings.Add(new ValidationFinding(
                "SIRO-STRUCT-02", FindingSeverity.Critical,
                $"SIRO XML file could not be parsed: {ex.Message}",
                Observed: "Parse error", Expected: "Well-formed XML"));
            return new ValidationResult(ValidatorId, IsConformant: false, findings);
        }

        // ── 3. Root element name ───────────────────────────────────────────
        var root = doc.Root;
        if (root is null)
        {
            findings.Add(new ValidationFinding(
                "SIRO-STRUCT-03", FindingSeverity.Critical,
                "XML document has no root element.",
                Observed: "(none)", Expected: "SiroResponse"));
            return new ValidationResult(ValidatorId, IsConformant: false, findings);
        }

        if (root.Name.LocalName != "SiroResponse")
        {
            findings.Add(new ValidationFinding(
                "SIRO-STRUCT-03", FindingSeverity.Major,
                "Root element local name does not match the required 'SiroResponse'.",
                Observed: root.Name.LocalName, Expected: "SiroResponse"));
        }

        // ── 4. Namespace ───────────────────────────────────────────────────
        if (root.Name.NamespaceName != SiroNamespace)
        {
            findings.Add(new ValidationFinding(
                "SIRO-STRUCT-04", FindingSeverity.Major,
                "Root element namespace does not match the SIRO regulatory namespace.",
                Observed: root.Name.NamespaceName, Expected: SiroNamespace));
        }

        // ── 5. Required descendant elements ───────────────────────────────
        foreach (var localName in RequiredElements.Skip(1)) // SiroResponse already checked above
        {
            var present = doc.Descendants()
                .Any(e => e.Name.LocalName == localName);
            if (!present)
            {
                findings.Add(new ValidationFinding(
                    $"SIRO-STRUCT-05-{localName}", FindingSeverity.Major,
                    $"Required element '{localName}' is absent from the SIRO XML document.",
                    Observed: "(absent)", Expected: $"<{localName}> element present"));
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
