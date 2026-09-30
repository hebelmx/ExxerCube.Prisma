using System;
using System.Collections.Generic;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Infrastructure.Export.Adaptive;

/// <summary>
/// Flat projection computed from a <see cref="UnifiedMetadataRecord"/> containing the
/// extracted, calculated, and derived columns for the "Datos Carga de Oficio" layout.
/// </summary>
/// <remarks>
/// <para>
/// Fixed columns (Procedencia, Estatus, Grupo, etc.) are carried entirely in the
/// <see cref="DatosCargaOficioTemplate"/> <see cref="ExxerCube.Prisma.Domain.ValueObjects.FieldMapping.DefaultValue"/>
/// and are NOT represented here — they never need to be extracted by reflection.
/// </para>
/// <para>
/// Property names must match the <see cref="DatosCargaOficioTemplate"/> <c>SourceFieldPath</c>
/// entries exactly (case-sensitive, dot-notation used by <c>TemplateFieldMapper</c>).
/// </para>
/// </remarks>
public sealed class DatosCargaOficioProjection
{
    /// <summary>Gets or sets the case number (col 2).</summary>
    public string NumeroExpediente { get; set; } = string.Empty;

    /// <summary>Gets or sets the oficio number (col 3).</summary>
    public string NumeroOficio { get; set; } = string.Empty;

    /// <summary>Gets or sets the registration date in ISO format (col 4).</summary>
    public DateTime FechaRegistro { get; set; }

    /// <summary>Gets or sets the reception date in ISO format (col 5).</summary>
    public DateTime FechaRecepcion { get; set; }

    /// <summary>Gets or sets the compliance days (col 6).</summary>
    public int Dias { get; set; }

    /// <summary>Gets or sets the estimated conclusion date in ISO format (col 7).</summary>
    public DateTime FechaEstimadaConclusion { get; set; }

    /// <summary>Gets or sets the derived tipo-de-asunto value (col 9).</summary>
    public string TipoAsunto { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subdivision code (col 12) in the client layout's slash form
    /// (e.g., "A/AS", "J/IN"), derived from the <see cref="LegalSubdivisionKind"/> Name
    /// so TemplateFieldMapper can extract it via plain string reflection.
    /// </summary>
    public string Subdivision { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the composed description "Paterno Materno Nombre" from the first
    /// <see cref="SolicitudParte"/> (col 14).
    /// </summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Gets or sets the authority name used as the signing functionary (col 15).</summary>
    public string Remitente { get; set; } = string.Empty;

    // -----------------------------------------------------------------
    // Builder
    // -----------------------------------------------------------------

    /// <summary>
    /// Creates a <see cref="DatosCargaOficioProjection"/> from the given unified metadata record.
    /// </summary>
    /// <param name="record">The unified metadata record (must have a non-null Expediente).</param>
    /// <param name="complianceActions">
    /// The compliance actions from the classification result (may be null or empty).
    /// </param>
    /// <returns>A populated projection ready to be fed to <c>ITemplateFieldMapper</c>.</returns>
    public static DatosCargaOficioProjection From(
        UnifiedMetadataRecord record,
        IReadOnlyList<ComplianceAction>? complianceActions = null)
    {
        var exp = record.Expediente!;           // callers must guard for null before calling From

        return new DatosCargaOficioProjection
        {
            NumeroExpediente = exp.NumeroExpediente,
            NumeroOficio = exp.NumeroOficio,
            FechaRegistro = exp.FechaRegistro != default ? exp.FechaRegistro : exp.FechaRecepcion,
            FechaRecepcion = exp.FechaRecepcion,
            Dias = exp.DiasPlazo,
            FechaEstimadaConclusion = exp.FechaEstimadaConclusion,
            TipoAsunto = DeriveTipoAsunto(exp, complianceActions),
            Subdivision = exp.Subdivision is null ? string.Empty : exp.Subdivision.Name.Replace('_', '/'),
            Descripcion = BuildDescripcion(exp.SolicitudPartes),
            Remitente = exp.AutoridadNombre,
        };
    }

    // -----------------------------------------------------------------
    // Pure derivation helpers (unit-testable, mutation-killable)
    // -----------------------------------------------------------------

    /// <summary>
    /// Maps the compliance signal to one of the 5 "Tipo de asunto" values used in the Excel layout.
    /// Priority order: explicit action kind → TieneAseguramiento/AreaDescripcion → INFORMACIÓN fallback.
    /// </summary>
    /// <param name="expediente">The fused expediente.</param>
    /// <param name="actions">Compliance actions from classification (may be null/empty).</param>
    /// <returns>One of: EMBARGO, DESEMBARGO, DOCUMENTACIÓN, TRANSFERENCIAS, INFORMACIÓN.</returns>
    public static string DeriveTipoAsunto(
        Expediente expediente,
        IReadOnlyList<ComplianceAction>? actions)
    {
        // 1. Look at the first compliance action's kind (highest-fidelity signal from classifier)
        if (actions is { Count: > 0 })
        {
            var primaryKind = actions[0].ActionType;

            if (primaryKind == ComplianceActionKind.Block)
            {
                return "EMBARGO";
            }

            if (primaryKind == ComplianceActionKind.Unblock)
            {
                return "DESEMBARGO";
            }

            if (primaryKind == ComplianceActionKind.Document)
            {
                return "DOCUMENTACIÓN";
            }

            if (primaryKind == ComplianceActionKind.Transfer)
            {
                return "TRANSFERENCIAS";
            }

            if (primaryKind == ComplianceActionKind.Information)
            {
                return "INFORMACIÓN";
            }
        }

        // 2. Fall back to TieneAseguramiento + AreaDescripcion heuristics
        if (expediente.TieneAseguramiento)
        {
            return "EMBARGO";
        }

        var area = expediente.AreaDescripcion?.ToUpperInvariant() ?? string.Empty;

        if (area.Contains("DESEMBARGO", StringComparison.Ordinal))
        {
            return "DESEMBARGO";
        }

        if (area.Contains("DOCUMENTACI", StringComparison.Ordinal))
        {
            return "DOCUMENTACIÓN";
        }

        if (area.Contains("TRANSFERENCIA", StringComparison.Ordinal))
        {
            return "TRANSFERENCIAS";
        }

        // 3. Default
        return "INFORMACIÓN";
    }

    /// <summary>
    /// Builds the "Descripción" column value as "Paterno Materno Nombre" from the first party.
    /// Returns an empty string when no parties are present.
    /// </summary>
    private static string BuildDescripcion(IReadOnlyList<SolicitudParte>? partes)
    {
        if (partes is not { Count: > 0 })
        {
            return string.Empty;
        }

        var p = partes[0];
        var parts = new List<string>(3);

        if (!string.IsNullOrWhiteSpace(p.Paterno))  { parts.Add(p.Paterno); }
        if (!string.IsNullOrWhiteSpace(p.Materno))  { parts.Add(p.Materno); }
        if (!string.IsNullOrWhiteSpace(p.Nombre))   { parts.Add(p.Nombre); }

        return string.Join(" ", parts);
    }
}
