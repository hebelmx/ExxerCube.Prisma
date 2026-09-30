using System;
using System.Collections.Generic;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Infrastructure.Export.Adaptive;

/// <summary>
/// Static factory that produces the built-in <see cref="TemplateDefinition"/> for the
/// "Datos Carga de Oficio" Excel layout (24 columns, FR-A demo requirement).
/// </summary>
/// <remarks>
/// <para>
/// This built-in definition is used by <see cref="DatosCargaOficioLayoutGenerator"/> when
/// <c>ITemplateRepository</c> returns <see langword="null"/> (no DB-seeded override).
/// </para>
/// <para>
/// Fixed-value columns carry their value in <see cref="FieldMapping.DefaultValue"/> with an
/// empty <see cref="FieldMapping.SourceFieldPath"/> so <c>TemplateFieldMapper</c> falls back
/// to the default without reflection. Extracted columns point into
/// <see cref="DatosCargaOficioProjection"/> via dot-notation paths.
/// </para>
/// </remarks>
public static class DatosCargaOficioTemplate
{
    /// <summary>The template-type key used with <c>ITemplateRepository.GetLatestTemplateAsync</c>.</summary>
    public const string TemplateType = "DatosCargaOficio";

    /// <summary>Gets the built-in default 24-column template definition.</summary>
    public static TemplateDefinition Default { get; } = Build();

    private static TemplateDefinition Build() => new()
    {
        TemplateId = "datos-carga-oficio-v1",
        TemplateType = TemplateType,
        Version = "1.0.0",
        Name = "Datos Carga de Oficio",
        Description = "Plantilla de 24 columnas para carga de oficios CNBV (FR-A demo requirement).",
        IsActive = true,
        EffectiveDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        CreatedAt = new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc),
        CreatedBy = "System",
        FieldMappings = new List<FieldMapping>
        {
            // 1 – Procedencia (fixed)
            Fixed(1,  "Procedencia",                          "C.N.B.V. JUZGADOS"),

            // 2 – Numero de expediente (extracted)
            Extracted(2,  "Numero de expediente",             "NumeroExpediente"),

            // 3 – Oficio (extracted)
            Extracted(3,  "Oficio",                           "NumeroOficio"),

            // 4 – Fecha de registro (calculated — same as FechaRecepcion for demo; ISO date)
            DateExtracted(4, "Fecha de registro",             "FechaRegistro"),

            // 5 – Fecha de recepción (calculated; ISO date)
            DateExtracted(5, "Fecha de recepción",            "FechaRecepcion"),

            // 6 – Días (extracted)
            Extracted(6,  "Días",                             "Dias"),

            // 7 – Fecha estimada de conclusión (calculated; ISO date)
            DateExtracted(7, "Fecha estimada de conclusión",  "FechaEstimadaConclusion"),

            // 8 – Estatus (fixed)
            Fixed(8,  "Estatus",                              "registrado"),

            // 9 – Tipo de asunto (derived — computed by projection)
            Extracted(9,  "Tipo de asunto",                   "TipoAsunto"),

            // 10 – Grupo (fixed)
            Fixed(10, "Grupo",                                "CNBV - Filiales"),

            // 11 – Área remitente (fixed)
            Fixed(11, "Área remitente",                       "COMISION NACIONAL BANCARIA Y DE VALORES"),

            // 12 – Subdivisión (extracted — Name property of SmartEnum → "A_AS" etc.)
            Extracted(12, "Subdivisión",                      "Subdivision"),

            // 13 – Entidad Financiera (fixed)
            Fixed(13, "Entidad Financiera",                   "Banco"),

            // 14 – Descripción (composed — Paterno + Materno + Nombre, in projection)
            Extracted(14, "Descripción",                      "Descripcion"),

            // 15 – Nombre del remitente (extracted)
            Extracted(15, "Nombre del remitente",             "Remitente"),

            // 16 – Origen (fixed)
            Fixed(16, "Origen",                               "Oficio"),

            // 17 – Tipo de documento (fixed)
            Fixed(17, "Tipo de documento",                    "Oficio"),

            // 18 – Medio de seguimiento (fixed)
            Fixed(18, "Medio de seguimiento",                 "Carta"),

            // 19 – Nombre Abogado Interno (fixed — demo placeholder)
            Fixed(19, "Nombre Abogado Interno",               "Airam Zepol Zepol"),

            // 20 – Nombre abogado responsable (fixed — demo placeholder)
            Fixed(20, "Nombre abogado responsable",           "Nauj Zerep Zerep"),

            // 21 – Despacho (fixed — demo placeholder)
            Fixed(21, "Despacho",                             "El abogado justo"),

            // 22 – Estado (fixed)
            Fixed(22, "Estado",                               "CIUDAD DE MEXICO"),

            // 23 – Ciudad (fixed)
            Fixed(23, "Ciudad",                               "MEXICO"),

            // 24 – Zona (fixed)
            Fixed(24, "Zona",                                 "METROPOLITANO"),
        },
    };

    // -----------------------------------------------------------------
    // Private helpers to reduce constructor noise
    // -----------------------------------------------------------------

    private static FieldMapping Fixed(int order, string header, string value) => new()
    {
        MappingId = $"dcoa-{order:D2}",
        DisplayOrder = order,
        TargetField = header,
        DisplayName = header,
        SourceFieldPath = string.Empty,   // no extraction needed; DefaultValue provides value
        DefaultValue = value,
        DataType = "string",
        IsRequired = false,
        IsNullable = true,
    };

    private static FieldMapping Extracted(int order, string header, string sourcePath) => new()
    {
        MappingId = $"dcoa-{order:D2}",
        DisplayOrder = order,
        TargetField = header,
        DisplayName = header,
        SourceFieldPath = sourcePath,
        DefaultValue = string.Empty,
        DataType = "string",
        IsRequired = false,
        IsNullable = true,
    };

    private static FieldMapping DateExtracted(int order, string header, string sourcePath) => new()
    {
        MappingId = $"dcoa-{order:D2}",
        DisplayOrder = order,
        TargetField = header,
        DisplayName = header,
        SourceFieldPath = sourcePath,
        DefaultValue = string.Empty,
        DataType = "DateTime",
        Format = "yyyy-MM-dd",
        IsRequired = false,
        IsNullable = true,
    };
}
