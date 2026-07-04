using System.Globalization;
using ExxerCube.Prisma.Domain.Entities;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Txt.Llm;

/// <summary>
/// PURE static mapper: <see cref="LlmExpedienteDto"/> → <see cref="Expediente"/>.
/// Null-safe throughout; no I/O; deterministic.
/// </summary>
public static class LlmExpedienteMapper
{
    /// <summary>
    /// Maps a validated <see cref="LlmExpedienteDto"/> to a new <see cref="Expediente"/> instance.
    /// Fields absent in the DTO remain at their default values.
    /// </summary>
    /// <param name="dto">The DTO to map. Must not be null.</param>
    /// <returns>A populated <see cref="Expediente"/> with provenance recorded in <c>AdditionalFields</c>.</returns>
    public static Expediente ToExpediente(LlmExpedienteDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var expediente = new Expediente();
        var abstained = new List<string>();

        // NumeroExpediente — field-level abstention. A plausible WRONG value is worse than an
        // abstention: only set the field when it passes the shared pure guard in
        // LlmExtractionGate; otherwise leave the Expediente default untouched and record the
        // abstention (never surface the malformed raw value anywhere).
        if (LlmExtractionGate.IsPlausibleExpediente(dto.Expediente))
        {
            expediente.NumeroExpediente = dto.Expediente!.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(dto.Expediente))
        {
            abstained.Add("NumeroExpediente");
        }

        if (!string.IsNullOrWhiteSpace(dto.Solicitante))
        {
            expediente.NombreSolicitante = dto.Solicitante.Trim();
        }

        // Map primary RFC from the top-level field (may also appear inside a parte).
        if (LlmExtractionGate.IsPlausibleRfc(dto.Rfc))
        {
            expediente.AdditionalFields["Rfc"] = dto.Rfc!.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(dto.Rfc))
        {
            abstained.Add("Rfc");
        }

        if (LlmExtractionGate.IsPlausibleCurp(dto.Curp))
        {
            expediente.AdditionalFields["Curp"] = dto.Curp!.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(dto.Curp))
        {
            abstained.Add("Curp");
        }

        if (!string.IsNullOrWhiteSpace(dto.Cuenta))
        {
            expediente.AdditionalFields["Cuenta"] = dto.Cuenta.Trim();
        }

        if (LlmExtractionGate.IsPlausibleMonto(dto.Monto))
        {
            LlmExtractionGate.TryParseMonto(dto.Monto, out var monto);
            expediente.AdditionalFields["Monto"] = monto.ToString(CultureInfo.InvariantCulture);
        }
        else if (!string.IsNullOrWhiteSpace(dto.Monto))
        {
            abstained.Add("Monto");
        }

        // NumeroOficio / AutoridadNombre — field-level abstention (S4-B). A plausible WRONG
        // value is worse than an abstention: only set the field when it passes the shared
        // pure guard in LlmExtractionGate; otherwise leave the Expediente default untouched.
        if (LlmExtractionGate.IsPlausibleNumeroOficio(dto.NumeroOficio))
        {
            expediente.NumeroOficio = dto.NumeroOficio!.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(dto.NumeroOficio))
        {
            abstained.Add("NumeroOficio");
        }

        if (LlmExtractionGate.IsPlausibleAutoridadNombre(dto.AutoridadNombre))
        {
            expediente.AutoridadNombre = dto.AutoridadNombre!.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(dto.AutoridadNombre))
        {
            abstained.Add("AutoridadNombre");
        }

        // Map partes → SolicitudPartes.
        if (dto.Partes is { Length: > 0 })
        {
            for (var i = 0; i < dto.Partes.Length; i++)
            {
                var parteDto = dto.Partes[i];
                var parte = new SolicitudParte
                {
                    ParteId = i + 1,
                    Nombre = parteDto.Nombre?.Trim() ?? string.Empty,
                    Rfc = parteDto.Rfc?.Trim(),
                    Curp = parteDto.Curp?.Trim() ?? string.Empty,
                    Caracter = parteDto.Caracter?.Trim() ?? string.Empty,
                };

                if (!string.IsNullOrWhiteSpace(parteDto.FechaNacimiento)
                    && DateOnly.TryParse(parteDto.FechaNacimiento, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var fechaNac))
                {
                    parte.FechaNacimiento = fechaNac;
                }

                expediente.SolicitudPartes.Add(parte);
            }
        }

        // Abstention provenance (consumed by ExtractionReconciler to flag unrecovered fields for
        // manual review). Never contains the malformed raw value — only the canonical field name.
        if (abstained.Count > 0)
        {
            expediente.AdditionalFields["_AbstainedFields"] = string.Join(",", abstained);
        }

        // Provenance marker (consumed by the reconciliator in S2 to know this came from LLM text extraction).
        expediente.AdditionalFields["_ExtractionSource"] = "llm-text";

        return expediente;
    }
}
