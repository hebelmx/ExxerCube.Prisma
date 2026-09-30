// <copyright file="ExcelExportValidator.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.IO;
using ClosedXML.Excel;

namespace ExxerCube.Prisma.QaHarness.Validators.Export;

/// <summary>
/// Validates that an Excel workbook produced by the Prisma export pipeline contains the
/// 24-column "Datos-Carga-Oficio" header set on the first worksheet, in the correct column order.
/// Accepts a file path (<see cref="string"/>) as the subject — the workbook is opened with
/// ClosedXML inside <see cref="ValidateAsync"/>. Malformed or missing files produce
/// <see cref="FindingSeverity.Critical"/> findings and never throw.
/// </summary>
public sealed class ExcelExportValidator : IDomainValidator<string>
{
    /// <summary>
    /// The 24 expected column headers in display order (left to right).
    /// Sourced from <c>DatosCargaOficioLayoutGeneratorTests.ExpectedHeaders</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> ExpectedHeaders =
    [
        "Procedencia",
        "Numero de expediente",
        "Oficio",
        "Fecha de registro",
        "Fecha de recepción",
        "Días",
        "Fecha estimada de conclusión",
        "Estatus",
        "Tipo de asunto",
        "Grupo",
        "Área remitente",
        "Subdivisión",
        "Entidad Financiera",
        "Descripción",
        "Nombre del remitente",
        "Origen",
        "Tipo de documento",
        "Medio de seguimiento",
        "Nombre Abogado Interno",
        "Nombre abogado responsable",
        "Despacho",
        "Estado",
        "Ciudad",
        "Zona",
    ];

    /// <inheritdoc/>
    public string ValidatorId => "EXCEL-EXPORT";

    /// <inheritdoc/>
    public string Description =>
        "Observes whether the first worksheet of the supplied Excel workbook contains " +
        "the 24 required 'Datos-Carga-Oficio' column headers in the correct column order.";

    /// <inheritdoc/>
    /// <param name="subject">Absolute or repo-relative path to an <c>.xlsx</c> workbook file.</param>
    /// <param name="cancellationToken">Token checked before validation begins.</param>
    public Task<ValidationResult> ValidateAsync(
        string subject,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(NonConformant("EXCEL-EXPORT-00", FindingSeverity.Critical,
                "Validation cancelled before it started.", Observed: null, Expected: null));
        }

        return Task.FromResult(ValidateInternal(subject));
    }

    private ValidationResult ValidateInternal(string subject)
    {
        var findings = new List<ValidationFinding>();

        // ── 1. File existence ──────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(subject) || !File.Exists(subject))
        {
            findings.Add(new ValidationFinding(
                "EXCEL-EXPORT-01", FindingSeverity.Critical,
                "Excel workbook file does not exist or path is empty.",
                Observed: subject ?? "(null)", Expected: "Existing .xlsx file path"));
            return new ValidationResult(ValidatorId, IsConformant: false, findings);
        }

        // ── 2. Open workbook ───────────────────────────────────────────────
        IXLWorksheet ws;
        XLWorkbook? wb = null;
        try
        {
            wb = new XLWorkbook(subject);
            ws = wb.Worksheet(1);
        }
        catch (Exception ex)
        {
            wb?.Dispose();
            findings.Add(new ValidationFinding(
                "EXCEL-EXPORT-02", FindingSeverity.Critical,
                $"Excel workbook could not be opened: {ex.Message}",
                Observed: "Open error", Expected: "Valid .xlsx workbook"));
            return new ValidationResult(ValidatorId, IsConformant: false, findings);
        }

        try
        {
            // ── 3. Column count ────────────────────────────────────────────
            var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
            if (lastCol < ExpectedHeaders.Count)
            {
                findings.Add(new ValidationFinding(
                    "EXCEL-EXPORT-03", FindingSeverity.Major,
                    "The first worksheet has fewer columns than the 24 required header columns.",
                    Observed: $"{lastCol} columns used",
                    Expected: $"At least {ExpectedHeaders.Count} columns"));
            }

            // ── 4. Per-column header check ─────────────────────────────────
            for (var i = 0; i < ExpectedHeaders.Count; i++)
            {
                var colNumber = i + 1;
                var actual = ws.Cell(1, colNumber).GetString();
                var expected = ExpectedHeaders[i];

                if (actual != expected)
                {
                    findings.Add(new ValidationFinding(
                        $"EXCEL-EXPORT-04-C{colNumber:D2}", FindingSeverity.Major,
                        $"Column {colNumber} header does not match the expected Datos-Carga-Oficio label.",
                        Observed: string.IsNullOrEmpty(actual) ? "(empty)" : actual,
                        Expected: expected));
                }
            }
        }
        finally
        {
            wb.Dispose();
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
