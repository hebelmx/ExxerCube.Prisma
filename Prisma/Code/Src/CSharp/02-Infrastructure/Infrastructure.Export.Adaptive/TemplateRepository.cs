using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive.Data;
using IndQuestResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Export.Adaptive;

/// <summary>
/// Repository implementation for loading and managing bank export template definitions from database.
/// </summary>
public class TemplateRepository : ITemplateRepository
{
    private readonly TemplateDbContext _dbContext;
    private readonly ILogger<TemplateRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The template database context.</param>
    /// <param name="logger">The logger instance.</param>
    public TemplateRepository(TemplateDbContext dbContext, ILogger<TemplateRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<TemplateDefinition?> GetTemplateAsync(
        string templateType,
        string version,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting template: {TemplateType} v{Version}", templateType, version);

        return await _dbContext.Templates
            .Include(t => t.FieldMappings)
            .FirstOrDefaultAsync(t =>
                t.TemplateType == templateType &&
                t.Version == version,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TemplateDefinition?> GetLatestTemplateAsync(
        string templateType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting latest active template for: {TemplateType}", templateType);

        var now = DateTime.UtcNow;

        return await _dbContext.Templates
            .Include(t => t.FieldMappings)
            .Where(t =>
                t.TemplateType == templateType &&
                t.IsActive &&
                t.EffectiveDate <= now &&
                (t.ExpirationDate == null || t.ExpirationDate > now))
            .OrderByDescending(t => t.EffectiveDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TemplateDefinition>> GetAllTemplateVersionsAsync(
        string templateType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting all versions for template: {TemplateType}", templateType);

        var templates = await _dbContext.Templates
            .Include(t => t.FieldMappings)
            .Where(t => t.TemplateType == templateType)
            .OrderByDescending(t => t.Version)
            .ToListAsync(cancellationToken);

        return templates.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<Result> SaveTemplateAsync(
        TemplateDefinition template,
        CancellationToken cancellationToken = default)
    {
        if (template == null)
        {
            return Result.Failure("Template cannot be null");
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(template.TemplateType))
        {
            return Result.Failure("TemplateType is required");
        }

        if (string.IsNullOrWhiteSpace(template.Version))
        {
            return Result.Failure("Version is required");
        }

        if (template.FieldMappings == null || template.FieldMappings.Count == 0)
        {
            return Result.Failure("FieldMappings are required");
        }

        _logger.LogInformation("Saving template: {TemplateType} v{Version}", template.TemplateType, template.Version);

        try
        {
            // Check if template already exists (either by TemplateId or by TemplateType+Version)
            var existingById = await _dbContext.Templates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TemplateId == template.TemplateId, cancellationToken);

            var existingByTypeVersion = await _dbContext.Templates
                .AsNoTracking()
                .FirstOrDefaultAsync(t =>
                    t.TemplateType == template.TemplateType &&
                    t.Version == template.Version,
                    cancellationToken);

            // If a template with this TemplateId already exists, fail (no updates via SaveTemplateAsync)
            if (existingById != null)
            {
                _logger.LogWarning("Template with ID {TemplateId} already exists", template.TemplateId);
                return Result.Failure($"Template with ID {template.TemplateId} already exists");
            }

            // If a template with this TemplateType+Version combo already exists, fail (duplicate)
            if (existingByTypeVersion != null)
            {
                _logger.LogWarning("Template {TemplateType} v{Version} already exists", template.TemplateType, template.Version);
                return Result.Failure($"Template {template.TemplateType} v{template.Version} already exists");
            }

            // Insert new template
            template.CreatedAt = DateTime.UtcNow;
            template.ModifiedAt = DateTime.UtcNow;

            await _dbContext.Templates.AddAsync(template, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully saved template: {TemplateType} v{Version}", template.TemplateType, template.Version);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving template: {TemplateType} v{Version}", template.TemplateType, template.Version);
            return Result.Failure($"Failed to save template: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteTemplateAsync(
        string templateType,
        string version,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateType))
        {
            return Result.Failure("TemplateType is required");
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            return Result.Failure("Version is required");
        }

        _logger.LogInformation("Deleting template: {TemplateType} v{Version}", templateType, version);

        try
        {
            var template = await _dbContext.Templates
                .FirstOrDefaultAsync(t =>
                    t.TemplateType == templateType &&
                    t.Version == version,
                    cancellationToken);

            if (template == null)
            {
                _logger.LogWarning("Template not found for deletion: {TemplateType} v{Version}", templateType, version);
                return Result.Failure($"Template not found: {templateType} v{version}");
            }

            // Prevent deletion of active templates
            if (template.IsActive)
            {
                _logger.LogWarning("Cannot delete active template: {TemplateType} v{Version}", templateType, version);
                return Result.Failure($"Cannot delete active template: {templateType} v{version}. Deactivate it first.");
            }

            _dbContext.Templates.Remove(template);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully deleted template: {TemplateType} v{Version}", templateType, version);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting template: {TemplateType} v{Version}", templateType, version);
            return Result.Failure($"Failed to delete template: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result> ActivateTemplateAsync(
        string templateType,
        string version,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateType))
        {
            return Result.Failure("TemplateType is required");
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            return Result.Failure("Version is required");
        }

        _logger.LogInformation("Activating template: {TemplateType} v{Version}", templateType, version);

        try
        {
            // Find the template to activate
            var templateToActivate = await _dbContext.Templates
                .FirstOrDefaultAsync(t =>
                    t.TemplateType == templateType &&
                    t.Version == version,
                    cancellationToken);

            if (templateToActivate == null)
            {
                _logger.LogWarning("Template not found for activation: {TemplateType} v{Version}", templateType, version);
                return Result.Failure($"Template not found: {templateType} v{version}");
            }

            // Deactivate all other templates of the same type
            var allTemplates = await _dbContext.Templates
                .Where(t => t.TemplateType == templateType)
                .ToListAsync(cancellationToken);

            foreach (var template in allTemplates)
            {
                template.IsActive = false;
                template.ModifiedAt = DateTime.UtcNow;
            }

            // Activate the specified template
            templateToActivate.IsActive = true;
            templateToActivate.ModifiedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully activated template: {TemplateType} v{Version}", templateType, version);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating template: {TemplateType} v{Version}", templateType, version);
            return Result.Failure($"Failed to activate template: {ex.Message}");
        }
    }
}
