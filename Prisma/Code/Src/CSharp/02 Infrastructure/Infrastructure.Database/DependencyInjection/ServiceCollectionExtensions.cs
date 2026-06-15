using ExxerCube.Prisma.Application.Services;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Calendar;
using ExxerCube.Prisma.Infrastructure.Database.Repositories;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExxerCube.Prisma.Infrastructure.Database.DependencyInjection;

/// <summary>
/// Extension methods for configuring database dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds database services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="configuration">The configuration instance (optional, for SLA options).</param>
    /// <param name="registerEventPersistence">
    /// When <see langword="true"/> (default) registers the <see cref="Services.EventPersistenceWorker"/>
    /// hosted service, which subscribes to the local Rx domain-event stream (<see cref="IEventPublisher"/>)
    /// to persist the event-store trail. Lean processes that wire the database only for audit (e.g. the
    /// Orion Downloader in the 3-process split) have no local event stream and do not register
    /// <see cref="IEventPublisher"/> — they must pass <see langword="false"/>, otherwise the worker fails to
    /// construct and the host cannot start.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDatabaseServices(
        this IServiceCollection services,
        string connectionString,
        IConfiguration? configuration = null,
        bool registerEventPersistence = true)
    {
        services.AddDbContext<PrismaDbContext>(options =>
            options.UseSqlServer(connectionString));
        // Expose the EF Core context through the internal abstraction for consumers that depend on the interface.
        services.AddScoped<IPrismaDbContext, PrismaDbContext>();
        services.AddScoped(typeof(IRepository<,>), typeof(EfCoreRepository<,>));

        services.AddScoped<IDownloadTracker, DownloadTrackerService>();
        services.AddScoped<IFileMetadataLogger, FileMetadataLoggerService>();

        // Register queued audit processor as singleton and hosted service (manages the channel and processes queue)
        services.AddSingleton<Services.QueuedAuditProcessorService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<Services.QueuedAuditProcessorService>());

        // Register queued audit logger as scoped (uses the singleton processor's channel)
        services.AddScoped<IAuditLogger>(sp =>
        {
            var processorService = sp.GetRequiredService<Services.QueuedAuditProcessorService>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Services.QueuedAuditLoggerService>>();
            return new Services.QueuedAuditLoggerService(processorService, scopeFactory, logger);
        });

        // Register holiday-aware business-day calculator (Mexico federal holidays via PublicHoliday package).
        // Registered as singleton — MexicoPublicHoliday is stateless calendar math, one instance per host.
        // TryAddSingleton so a composition root that already registered IBusinessDayCalculator (e.g. when
        // AddClassificationServices runs first) is not overwritten with a second instance.
        services.TryAddSingleton<IBusinessDayCalculator, MexicoBusinessDayCalculator>();

        // Register SLA metrics collector (singleton for metrics consistency)
        services.AddSingleton<SLAMetricsCollector>();

        // Register SLAEnforcerService as implementation — inject IBusinessDayCalculator for holiday awareness.
        services.AddScoped<SLAEnforcerService>(sp =>
        {
            var dbContext = sp.GetRequiredService<PrismaDbContext>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SLAEnforcerService>>();
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SLAOptions>>();
            var metricsCollector = sp.GetRequiredService<SLAMetricsCollector>();
            var calculator = sp.GetRequiredService<IBusinessDayCalculator>();
            return new SLAEnforcerService(dbContext, logger, options, metricsCollector, calculator);
        });

        // Register ResilientSLAEnforcerService as the ISLAEnforcer interface
        // This wraps SLAEnforcerService with circuit breaker, retry, and timeout policies
        services.AddScoped<ISLAEnforcer>(sp =>
        {
            var innerService = sp.GetRequiredService<SLAEnforcerService>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ResilientSLAEnforcerService>>();
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SLAResilienceOptions>>();
            return new ResilientSLAEnforcerService(innerService, logger, options);
        });

        services.AddScoped<IUnifiedMetadataStore, EfCoreUnifiedMetadataStore>();
        services.AddScoped<IManualReviewerPanel, ManualReviewerService>();

        // Configure SLA options
        services.Configure<SLAOptions>(options =>
        {
            if (configuration != null)
            {
                var section = configuration.GetSection(SLAOptions.SectionName);
                var criticalThreshold = section["CriticalThreshold"];
                var warningThreshold = section["WarningThreshold"];

                if (!string.IsNullOrEmpty(criticalThreshold) && TimeSpan.TryParse(criticalThreshold, out var critical))
                {
                    options.CriticalThreshold = critical;
                }
                else
                {
                    options.CriticalThreshold = TimeSpan.FromHours(4);
                }

                if (!string.IsNullOrEmpty(warningThreshold) && TimeSpan.TryParse(warningThreshold, out var warning))
                {
                    options.WarningThreshold = warning;
                }
                else
                {
                    options.WarningThreshold = TimeSpan.FromHours(24);
                }
            }
            else
            {
                // Use default options if configuration not provided
                options.CriticalThreshold = TimeSpan.FromHours(4);
                options.WarningThreshold = TimeSpan.FromHours(24);
            }
        });

        // Configure SLA background update options
        services.Configure<SLAUpdateOptions>(options =>
        {
            if (configuration != null)
            {
                var section = configuration.GetSection(SLAUpdateOptions.SectionName);

                if (int.TryParse(section["UpdateIntervalSeconds"], out var interval))
                {
                    options.UpdateIntervalSeconds = interval;
                }

                if (int.TryParse(section["BatchSize"], out var batchSize))
                {
                    options.BatchSize = batchSize;
                }

                if (int.TryParse(section["MaxRetries"], out var maxRetries))
                {
                    options.MaxRetries = maxRetries;
                }

                if (int.TryParse(section["RetryDelaySeconds"], out var retryDelay))
                {
                    options.RetryDelaySeconds = retryDelay;
                }
            }
        });

        // Configure SLA resilience options
        services.Configure<SLAResilienceOptions>(options =>
        {
            if (configuration != null)
            {
                var section = configuration.GetSection(SLAResilienceOptions.SectionName);

                if (int.TryParse(section["CircuitBreakerFailureThreshold"], out var failureThreshold))
                {
                    options.CircuitBreakerFailureThreshold = failureThreshold;
                }

                if (TimeSpan.TryParse(section["CircuitBreakerResetTimeout"], out var resetTimeout))
                {
                    options.CircuitBreakerResetTimeout = resetTimeout;
                }

                if (int.TryParse(section["CircuitBreakerSuccessThreshold"], out var successThreshold))
                {
                    options.CircuitBreakerSuccessThreshold = successThreshold;
                }

                if (int.TryParse(section["MaxRetryAttempts"], out var maxRetries))
                {
                    options.MaxRetryAttempts = maxRetries;
                }

                if (TimeSpan.TryParse(section["RetryBaseDelay"], out var baseDelay))
                {
                    options.RetryBaseDelay = baseDelay;
                }

                if (TimeSpan.TryParse(section["RetryMaxDelay"], out var maxDelay))
                {
                    options.RetryMaxDelay = maxDelay;
                }

                if (TimeSpan.TryParse(section["OperationTimeout"], out var timeout))
                {
                    options.OperationTimeout = timeout;
                }
            }
        });

        // Register background service for automatic SLA updates
        services.AddHostedService<SLAUpdateBackgroundService>();

        // Configure audit options
        services.Configure<AuditOptions>(options =>
        {
            if (configuration != null)
            {
                var section = configuration.GetSection(AuditOptions.SectionName);

                if (int.TryParse(section["RetentionYears"], out var retentionYears))
                {
                    options.RetentionYears = retentionYears;
                }

                if (int.TryParse(section["ArchiveAfterYears"], out var archiveAfterYears))
                {
                    options.ArchiveAfterYears = archiveAfterYears;
                }

                var archiveLocation = section["ArchiveLocation"];
                if (!string.IsNullOrWhiteSpace(archiveLocation))
                {
                    options.ArchiveLocation = archiveLocation;
                }

                if (bool.TryParse(section["AutoDeleteAfterRetention"], out var autoDelete))
                {
                    options.AutoDeleteAfterRetention = autoDelete;
                }
            }
        });

        // Configure audit retention background service options
        services.Configure<AuditRetentionOptions>(options =>
        {
            if (configuration != null)
            {
                var section = configuration.GetSection(AuditRetentionOptions.SectionName);

                if (int.TryParse(section["IntervalHours"], out var intervalHours))
                {
                    options.IntervalHours = intervalHours;
                }

                if (int.TryParse(section["BatchSize"], out var batchSize))
                {
                    options.BatchSize = batchSize;
                }

                if (int.TryParse(section["RetryDelayHours"], out var retryDelayHours))
                {
                    options.RetryDelayHours = retryDelayHours;
                }
            }
        });

        // Register background service for automatic audit retention enforcement
        services.AddHostedService<Services.AuditRetentionBackgroundService>();

        // Register event persistence worker to persist events to database. Skipped for lean processes
        // (e.g. the Orion Downloader) that wire the DB only for audit and have no local IEventPublisher.
        if (registerEventPersistence)
        {
            services.AddHostedService<Services.EventPersistenceWorker>();
        }

        return services;
    }
}