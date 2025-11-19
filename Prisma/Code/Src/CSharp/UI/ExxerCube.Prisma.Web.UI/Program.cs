using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Logs;
using System.Diagnostics;

namespace ExxerCube.Prisma.Web.UI;

/// <summary>
/// Main entry point for the ExxerCube.Prisma.Web.UI application.
/// </summary>
public class Program
{
    /// <summary>
    /// Application entry point.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure Serilog from configuration
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            //  .Enrich.WithMachineName()
            // .Enrich.WithThreadId()
            .CreateLogger();

        try
        {
            Log.Information("Starting ExxerCube.Prisma.Web.UI application");

            // Use Serilog for logging
            builder.Host.UseSerilog();

            // Configure OpenTelemetry for telemetry and distributed tracing
            ConfigureOpenTelemetry(builder.Services, builder.Configuration, builder.Environment);

            // Configure services using the extracted method
            ConfigureServices(builder.Services, builder.Configuration, builder.Environment);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Error", createScopeForErrors: true);
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseAntiforgery();

            // Map API controllers
            app.MapControllers();

            // Map health checks endpoint
            app.MapHealthChecks("/health");

            // Map SignalR hub
            app.MapHub<ProcessingHub>("/processingHub");

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            // Add additional endpoints required by the Identity /Account Razor components.
            app.MapAdditionalIdentityEndpoints();

            Log.Information("Application started successfully");
            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// Configures all services for dependency injection.
    /// This method is extracted to allow testing of the actual DI configuration.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="environment">The web host environment.</param>
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        // Add MudBlazor services
        services.AddMudServices();

        // Add SignalR for real-time updates
        services.AddSignalR();

        // Register SignalR hub as scoped (SignalR hubs are scoped by default)
        services.AddScoped<ProcessingHub>();

        // Add OCR processing services
        var pythonModulesPath = Path.Combine(environment.ContentRootPath, "..", "..", "Python", "ocr_modules");
        var pythonConfig = new ExxerCube.Prisma.Infrastructure.Python.PythonConfiguration
        {
            ModulesPath = pythonModulesPath,
            PythonExecutablePath = "python",
            MaxConcurrency = 5,
            OperationTimeoutSeconds = 30,
            EnableDebugging = environment.IsDevelopment()
        };
        services.AddOcrProcessingServices(pythonConfig);

        // Add services to the container.
        services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // Add API controllers
        services.AddControllers();

        // Add HttpClient for API calls with proper configuration
        services.AddHttpClient("api", client =>
        {
            client.BaseAddress = new Uri(configuration["ApiBaseUrl"] ?? "https://localhost:7062/");
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        services.AddCascadingAuthenticationState();
        services.AddScoped<IdentityUserAccessor>();
        services.AddScoped<IdentityRedirectManager>();
        services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        //services.AddDbContext<ApplicationDbContext>(options =>
        //    options.UseSqlServer(connectionString));
        services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddDatabaseDeveloperPageExceptionFilter();

        services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

        // Add Story 1.1 services: Browser Automation, File Storage, and Database services
        services.AddDatabaseServices(connectionString, configuration);
        services.AddBrowserAutomationServices(options =>
        {
            configuration.GetSection("BrowserAutomation").Bind(options);
        });
        services.AddFileStorageServices(options =>
        {
            configuration.GetSection("FileStorage").Bind(options);
        });
        services.AddScoped<DocumentIngestionService>();
        services.AddScoped<FileMetadataQueryService>();
        services.AddScoped<FileDownloadService>();

        // Add Story 1.2 services: Extraction, Classification, and Metadata Extraction
        services.AddExtractionServices();
        services.AddClassificationServices(configuration);
        services.AddScoped<MetadataExtractionService>();

        // Add Story 1.3 services: Field Matching and Unified Metadata Generation
        services.AddScoped<FieldMatchingService>();
        // Register FieldMatcherService instances for each source type
        services.AddScoped(typeof(IFieldMatcher<DocxSource>), typeof(ExxerCube.Prisma.Infrastructure.Classification.FieldMatcherService<DocxSource>));
        services.AddScoped(typeof(IFieldMatcher<PdfSource>), typeof(ExxerCube.Prisma.Infrastructure.Classification.FieldMatcherService<PdfSource>));

        // Add Story 1.4 services: Decision Logic (Identity Resolution and Legal Classification)
        services.AddScoped<DecisionLogicService>();

        // Add Story 1.5 services: SLA Tracking and Escalation
        services.AddScoped<SLATrackingService>();

        // Add Story 1.7 & 1.8 services: Export Generation (SIRO XML, Excel, PDF Signing)
        services.AddExportServices(configuration);
        services.AddScoped<ExportService>();

        // Add Story 1.9 services: Audit Reporting
        services.AddScoped<AuditReportingService>();

        // Add SLA health checks
        services.AddHealthChecks()
            .AddCheck<SLAEnforcerHealthCheck>(
                "sla_enforcer",
                tags: new[] { "sla", "database", "ready" })
            .AddCheck<SLABackgroundJobHealthCheck>(
                "sla_background_job",
                tags: new[] { "sla", "background", "ready" });
    }

    /// <summary>
    /// Configures OpenTelemetry for telemetry, distributed tracing, and metrics.
    /// Exports telemetry data to Seq for queryable analysis.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="environment">The web host environment.</param>
    private static void ConfigureOpenTelemetry(IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var otelConfig = configuration.GetSection("OpenTelemetry");
        var serviceName = otelConfig["ServiceName"] ?? "ExxerCube.Prisma.Web.UI";
        var serviceVersion = otelConfig["ServiceVersion"] ?? "1.0.0";
        var seqEndpoint = otelConfig["Seq:Endpoint"] ?? "http://localhost:5341";
        var seqApiKey = otelConfig["Seq:ApiKey"];
        var tracingEnabled = otelConfig.GetValue<bool>("Tracing:Enabled", true);
        var metricsEnabled = otelConfig.GetValue<bool>("Metrics:Enabled", true);
        var samplingRatio = otelConfig.GetValue<double>("Tracing:SamplingRatio", 1.0);

        // Build resource attributes
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName, serviceVersion)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = environment.EnvironmentName,
                ["service.instance.id"] = Environment.MachineName
            });

        // Configure OpenTelemetry services
        services.AddOpenTelemetry()
            .WithTracing(tracerProviderBuilder =>
            {
                if (tracingEnabled)
                {
                    tracerProviderBuilder
                        .SetResourceBuilder(resourceBuilder)
                        .SetSampler(new TraceIdRatioBasedSampler(samplingRatio))
                        // Add ASP.NET Core instrumentation
                        .AddAspNetCoreInstrumentation(options =>
                        {
                            options.RecordException = true;
                            options.EnrichWithHttpRequest = (activity, request) =>
                            {
                                activity.SetTag("http.request.method", request.Method);
                                activity.SetTag("http.request.path", request.Path);
                            };
                            options.EnrichWithHttpResponse = (activity, response) =>
                            {
                                activity.SetTag("http.response.status_code", response.StatusCode);
                            };
                        })
                        // Add HTTP client instrumentation
                        .AddHttpClientInstrumentation(options =>
                        {
                            options.RecordException = true;
                            options.EnrichWithHttpRequestMessage = (activity, request) =>
                            {
                                activity.SetTag("http.client.request.method", request.Method?.Method);
                                activity.SetTag("http.client.request.uri", request.RequestUri?.ToString());
                            };
                        })
                        // Add Entity Framework Core instrumentation
                        .AddEntityFrameworkCoreInstrumentation(options =>
                        {
                            options.SetDbStatementForText = true;
                            options.EnrichWithIDbCommand = (activity, command) =>
                            {
                                activity.SetTag("db.command.text", command.CommandText);
                            };
                        })
                        // Note: SignalR instrumentation is automatically included in ASP.NET Core instrumentation
                        // No separate SignalR instrumentation package exists
                        // Export to Seq via OTLP (OpenTelemetry Protocol)
                        // Seq supports OTLP endpoint at http://localhost:5341/ingest/otlp/v1
                        .AddOtlpExporter(options =>
                        {
                            options.Endpoint = new Uri($"{seqEndpoint.TrimEnd('/')}/ingest/otlp/v1/traces");
                            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                            if (!string.IsNullOrEmpty(seqApiKey))
                            {
                                options.Headers = $"X-Seq-ApiKey={seqApiKey}";
                            }
                        })
                        // Also export to console for development
                        .AddConsoleExporter();
                }
            })
            .WithMetrics(metricsProviderBuilder =>
            {
                if (metricsEnabled)
                {
                    metricsProviderBuilder
                        .SetResourceBuilder(resourceBuilder)
                        // Add ASP.NET Core metrics
                        .AddAspNetCoreInstrumentation()
                        // Add HTTP client metrics
                        .AddHttpClientInstrumentation()
                        // Add runtime metrics (GC, memory, etc.)
                        .AddRuntimeInstrumentation()
                        // Add process metrics
                        .AddProcessInstrumentation()
                        // Export to Seq via OTLP (OpenTelemetry Protocol)
                        // Seq supports OTLP endpoint at http://localhost:5341/ingest/otlp/v1
                        .AddOtlpExporter(options =>
                        {
                            options.Endpoint = new Uri($"{seqEndpoint.TrimEnd('/')}/ingest/otlp/v1/metrics");
                            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                            if (!string.IsNullOrEmpty(seqApiKey))
                            {
                                options.Headers = $"X-Seq-ApiKey={seqApiKey}";
                            }
                        })
                        // Also export to console for development
                        .AddConsoleExporter();
                }
            });

        // Configure logging to use OpenTelemetry (integrates with Serilog)
        // Note: Serilog already exports to Seq, so OpenTelemetry logs will also go there
        // This adds structured OpenTelemetry log attributes for better querying
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(resourceBuilder);
                // Export logs to Seq via OTLP (OpenTelemetry Protocol)
                options.AddOtlpExporter(otlpOptions =>
                {
                    otlpOptions.Endpoint = new Uri($"{seqEndpoint.TrimEnd('/')}/ingest/otlp/v1/logs");
                    otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    if (!string.IsNullOrEmpty(seqApiKey))
                    {
                        otlpOptions.Headers = $"X-Seq-ApiKey={seqApiKey}";
                    }
                });
                // Also export to console for development
                options.AddConsoleExporter();
            });
        });
    }
}