using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Export.Adaptive;

/// <summary>
/// Seeds initial template definitions from hardcoded exporters (SiroXmlExporter, ExcelLayoutGenerator).
/// This enables zero-downtime migration from hardcoded to adaptive templates.
/// </summary>
public class TemplateSeeder
{
    private readonly TemplateDbContext _dbContext;
    private readonly ILogger<TemplateSeeder> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateSeeder"/> class.
    /// </summary>
    /// <param name="dbContext">The template database context.</param>
    /// <param name="logger">The logger.</param>
    public TemplateSeeder(TemplateDbContext dbContext, ILogger<TemplateSeeder> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Seeds all initial templates (Excel, XML, DOCX) if they don't already exist.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SeedAllTemplatesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting template seeding process");

        await SeedExcelTemplateAsync(cancellationToken);
        await SeedXmlTemplateAsync(cancellationToken);

        _logger.LogInformation("Template seeding process completed");
    }

    /// <summary>
    /// Seeds the Excel template definition extracted from ExcelLayoutGenerator.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SeedExcelTemplateAsync(CancellationToken cancellationToken = default)
    {
        const string templateType = "Excel";
        const string version = "1.0.0";

        // Check if template already exists
        var existing = await _dbContext.Templates
            .FirstOrDefaultAsync(t => t.TemplateType == templateType && t.Version == version, cancellationToken);

        if (existing != null)
        {
            _logger.LogInformation("Excel template {Version} already exists, skipping seed", version);
            return;
        }

        _logger.LogInformation("Seeding Excel template {Version}", version);

        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = templateType,
            Version = version,
            Description = "SIRO Registration Excel Layout (migrated from ExcelLayoutGenerator)",
            EffectiveDate = DateTime.UtcNow,
            IsActive = true,
            FieldMappings = new List<FieldMapping>
            {
                // Column 1: NumeroExpediente
                new FieldMapping(
                    sourceFieldPath: "Expediente.NumeroExpediente",
                    targetField: "NumeroExpediente",
                    isRequired: true,
                    dataType: "string")
                {
                    DisplayOrder = 1
                },

                // Column 2: NumeroOficio
                new FieldMapping(
                    sourceFieldPath: "Expediente.NumeroOficio",
                    targetField: "NumeroOficio",
                    isRequired: true,
                    dataType: "string")
                {
                    DisplayOrder = 2
                },

                // Column 3: SolicitudSiara
                new FieldMapping(
                    sourceFieldPath: "Expediente.SolicitudSiara",
                    targetField: "SolicitudSiara",
                    isRequired: false,
                    dataType: "string")
                {
                    DisplayOrder = 3
                },

                // Column 4: Folio
                new FieldMapping(
                    sourceFieldPath: "Expediente.Folio",
                    targetField: "Folio",
                    isRequired: false,
                    dataType: "int")
                {
                    DisplayOrder = 4
                },

                // Column 5: OficioYear
                new FieldMapping(
                    sourceFieldPath: "Expediente.OficioYear",
                    targetField: "OficioYear",
                    isRequired: false,
                    dataType: "int")
                {
                    DisplayOrder = 5
                },

                // Column 6: AreaClave
                new FieldMapping(
                    sourceFieldPath: "Expediente.AreaClave",
                    targetField: "AreaClave",
                    isRequired: false,
                    dataType: "int")
                {
                    DisplayOrder = 6
                },

                // Column 7: AreaDescripcion
                new FieldMapping(
                    sourceFieldPath: "Expediente.AreaDescripcion",
                    targetField: "AreaDescripcion",
                    isRequired: false,
                    dataType: "string")
                {
                    DisplayOrder = 7
                },

                // Column 8: FechaPublicacion (formatted as yyyy-MM-dd)
                new FieldMapping(
                    sourceFieldPath: "Expediente.FechaPublicacion",
                    targetField: "FechaPublicacion",
                    isRequired: false,
                    dataType: "datetime")
                {
                    DisplayOrder = 8,
                    Format = "yyyy-MM-dd"
                },

                // Column 9: DiasPlazo
                new FieldMapping(
                    sourceFieldPath: "Expediente.DiasPlazo",
                    targetField: "DiasPlazo",
                    isRequired: false,
                    dataType: "int")
                {
                    DisplayOrder = 9
                },

                // Column 10: AutoridadNombre
                new FieldMapping(
                    sourceFieldPath: "Expediente.AutoridadNombre",
                    targetField: "AutoridadNombre",
                    isRequired: false,
                    dataType: "string")
                {
                    DisplayOrder = 10
                },

                // Column 11: RFC (from first SolicitudPartes)
                new FieldMapping(
                    sourceFieldPath: "Expediente.SolicitudPartes[0].Rfc",
                    targetField: "RFC",
                    isRequired: false,
                    dataType: "string")
                {
                    DisplayOrder = 11,
                    DefaultValue = string.Empty
                },

                // Column 12: NombreCompleto (composite field - requires custom transformation)
                // Note: This is a composite of Nombre + Paterno + Materno
                // For now, we'll map just the Nombre field
                // Full composite transformation will be added in Phase 8
                new FieldMapping(
                    sourceFieldPath: "Expediente.SolicitudPartes[0].Nombre",
                    targetField: "NombreCompleto",
                    isRequired: false,
                    dataType: "string")
                {
                    DisplayOrder = 12,
                    DefaultValue = string.Empty
                }
            }
        };

        _dbContext.Templates.Add(template);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully seeded Excel template {Version} with {FieldCount} fields",
            version, template.FieldMappings.Count);
    }

    /// <summary>
    /// Seeds the XML template definition extracted from SiroXmlExporter.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SeedXmlTemplateAsync(CancellationToken cancellationToken = default)
    {
        const string templateType = "XML";
        const string version = "1.0.0";

        // Check if template already exists
        var existing = await _dbContext.Templates
            .FirstOrDefaultAsync(t => t.TemplateType == templateType && t.Version == version, cancellationToken);

        if (existing != null)
        {
            _logger.LogInformation("XML template {Version} already exists, skipping seed", version);
            return;
        }

        _logger.LogInformation("Seeding XML template {Version}", version);

        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = templateType,
            Version = version,
            Description = "SIRO Response XML Structure (migrated from SiroXmlExporter)",
            EffectiveDate = DateTime.UtcNow,
            IsActive = true,
            FieldMappings = new List<FieldMapping>
            {
                // Required fields (from SiroXmlExporter validation)
                new FieldMapping(
                    sourceFieldPath: "Expediente.NumeroExpediente",
                    targetField: "NumeroExpediente",
                    isRequired: true,
                    dataType: "string")
                {
                    DisplayOrder = 1
                },

                new FieldMapping(
                    sourceFieldPath: "Expediente.NumeroOficio",
                    targetField: "NumeroOficio",
                    isRequired: true,
                    dataType: "string")
                {
                    DisplayOrder = 2
                },

                // Core expediente fields
                new FieldMapping(
                    sourceFieldPath: "Expediente.SolicitudSiara",
                    targetField: "SolicitudSiara",
                    isRequired: false,
                    dataType: "string")
                {
                    DisplayOrder = 3
                },

                new FieldMapping(
                    sourceFieldPath: "Expediente.Folio",
                    targetField: "Folio",
                    isRequired: false,
                    dataType: "int")
                {
                    DisplayOrder = 4
                },

                new FieldMapping(
                    sourceFieldPath: "Expediente.OficioYear",
                    targetField: "OficioYear",
                    isRequired: false,
                    dataType: "int")
                {
                    DisplayOrder = 5
                },

                new FieldMapping(
                    sourceFieldPath: "Expediente.AreaClave",
                    targetField: "AreaClave",
                    isRequired: false,
                    dataType: "int")
                {
                    DisplayOrder = 6
                },

                new FieldMapping(
                    sourceFieldPath: "Expediente.AreaDescripcion",
                    targetField: "AreaDescripcion",
                    isRequired: false,
                    dataType: "string")
                {
                    DisplayOrder = 7
                },

                new FieldMapping(
                    sourceFieldPath: "Expediente.FechaPublicacion",
                    targetField: "FechaPublicacion",
                    isRequired: false,
                    dataType: "datetime")
                {
                    DisplayOrder = 8,
                    Format = "yyyy-MM-dd"
                },

                new FieldMapping(
                    sourceFieldPath: "Expediente.DiasPlazo",
                    targetField: "DiasPlazo",
                    isRequired: false,
                    dataType: "int")
                {
                    DisplayOrder = 9
                },

                new FieldMapping(
                    sourceFieldPath: "Expediente.AutoridadNombre",
                    targetField: "AutoridadNombre",
                    isRequired: false,
                    dataType: "string")
                {
                    DisplayOrder = 10
                },

                // Optional fields
                new FieldMapping(
                    sourceFieldPath: "Expediente.AutoridadEspecificaNombre",
                    targetField: "AutoridadEspecificaNombre",
                    isRequired: false,
                    dataType: "string")
                {
                    DisplayOrder = 11
                },

                new FieldMapping(
                    sourceFieldPath: "Expediente.NombreSolicitante",
                    targetField: "NombreSolicitante",
                    isRequired: false,
                    dataType: "string")
                {
                    DisplayOrder = 12
                },

                // Legal references
                new FieldMapping(
                    sourceFieldPath: "Expediente.Referencia",
                    targetField: "Referencia",
                    isRequired: false,
                    dataType: "string")
                {
                    DisplayOrder = 13
                },

                new FieldMapping(
                    sourceFieldPath: "Expediente.Referencia1",
                    targetField: "Referencia1",
                    isRequired: false,
                    dataType: "string")
                {
                    DisplayOrder = 14
                },

                new FieldMapping(
                    sourceFieldPath: "Expediente.Referencia2",
                    targetField: "Referencia2",
                    isRequired: false,
                    dataType: "string")
                {
                    DisplayOrder = 15
                }

                // Note: SolicitudPartes and SolicitudEspecificas are collections
                // Collection handling will be added in Phase 8 (Advanced Features)
                // For now, we focus on simple field mappings
            }
        };

        _dbContext.Templates.Add(template);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully seeded XML template {Version} with {FieldCount} fields",
            version, template.FieldMappings.Count);
    }
}
