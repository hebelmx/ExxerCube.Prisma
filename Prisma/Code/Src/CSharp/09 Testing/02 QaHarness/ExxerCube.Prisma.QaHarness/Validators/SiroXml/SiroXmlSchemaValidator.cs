// <copyright file="SiroXmlSchemaValidator.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace ExxerCube.Prisma.QaHarness.Validators.SiroXml;

/// <summary>
/// Validates that a SIRO XML document is well-formed and, when an <see cref="XmlSchemaSet"/> is
/// supplied, conforms to the provided XSD schema. Accepts a file path (<see cref="string"/>) as
/// the subject. Malformed or missing files are represented as <see cref="FindingSeverity.Critical"/>
/// findings — never thrown.
/// </summary>
public sealed class SiroXmlSchemaValidator : IDomainValidator<string>
{
    private readonly XmlSchemaSet? _schemaSet;

    /// <summary>
    /// Initialises the validator without a schema set (well-formed XML check only).
    /// </summary>
    public SiroXmlSchemaValidator()
    {
    }

    /// <summary>
    /// Initialises the validator with an explicit schema set used for XSD conformance checks.
    /// </summary>
    /// <param name="schemaSet">
    /// An <see cref="XmlSchemaSet"/> containing the SIRO XSD(s).  Pass <see langword="null"/> to
    /// skip schema validation and perform a well-formedness check only.
    /// </param>
    public SiroXmlSchemaValidator(XmlSchemaSet? schemaSet)
    {
        _schemaSet = schemaSet;
    }

    /// <inheritdoc/>
    public string ValidatorId => "SIRO-SCHEMA";

    /// <inheritdoc/>
    public string Description =>
        "Observes whether a SIRO XML file is well-formed and, when an XSD schema set is " +
        "provided, whether it conforms to that schema. Schema violations are reported as " +
        "individual findings with the line/position of each error.";

    /// <inheritdoc/>
    /// <param name="subject">Absolute or repo-relative path to a <c>.siro.xml</c> file.</param>
    /// <param name="cancellationToken">Token used to cancel long-running validation steps.</param>
    public async Task<ValidationResult> ValidateAsync(
        string subject,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return NonConformant("SIRO-SCHEMA-00", FindingSeverity.Critical,
                "Validation cancelled before it started.", Observed: null, Expected: null);
        }

        var findings = new List<ValidationFinding>();

        // ── 1. File existence ──────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(subject) || !File.Exists(subject))
        {
            findings.Add(new ValidationFinding(
                "SIRO-SCHEMA-01", FindingSeverity.Critical,
                "SIRO XML file does not exist or path is empty.",
                Observed: subject ?? "(null)", Expected: "Existing file path"));
            return new ValidationResult(ValidatorId, IsConformant: false, findings);
        }

        // ── 2. Read content ────────────────────────────────────────────────
        string content;
        try
        {
            content = await File.ReadAllTextAsync(subject, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            findings.Add(new ValidationFinding(
                "SIRO-SCHEMA-02", FindingSeverity.Critical,
                $"Unable to read SIRO XML file: {ex.Message}",
                Observed: "Read error", Expected: "Readable file"));
            return new ValidationResult(ValidatorId, IsConformant: false, findings);
        }

        // ── 3. Well-formedness check (parse) ───────────────────────────────
        XDocument doc;
        try
        {
            doc = XDocument.Parse(content, LoadOptions.SetLineInfo);
        }
        catch (XmlException ex)
        {
            findings.Add(new ValidationFinding(
                "SIRO-SCHEMA-03", FindingSeverity.Critical,
                $"SIRO XML is not well-formed: {ex.Message}",
                Observed: $"Line {ex.LineNumber}, Position {ex.LinePosition}",
                Expected: "Well-formed XML"));
            return new ValidationResult(ValidatorId, IsConformant: false, findings);
        }

        // ── 4. XSD schema conformance (optional) ──────────────────────────
        if (_schemaSet is not null && _schemaSet.Count > 0)
        {
            var schemaErrors = new List<string>();
            doc.Validate(_schemaSet, (_, e) =>
            {
                schemaErrors.Add($"Line {e.Exception?.LineNumber}, Position {e.Exception?.LinePosition}: {e.Message}");
            });

            foreach (var error in schemaErrors)
            {
                findings.Add(new ValidationFinding(
                    "SIRO-SCHEMA-04", FindingSeverity.Major,
                    $"XSD schema violation: {error}",
                    Observed: error, Expected: "No schema violations"));
            }
        }
        else
        {
            // No schema provided — record informational note.
            findings.Add(new ValidationFinding(
                "SIRO-SCHEMA-INFO", FindingSeverity.Info,
                "No XSD schema set provided; only well-formedness was checked.",
                Observed: "No schema", Expected: "XSD schema validation (optional)"));
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
