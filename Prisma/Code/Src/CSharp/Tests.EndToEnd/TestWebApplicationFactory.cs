using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using ExxerCube.Prisma.Infrastructure.Database.EntityFramework;
using ExxerCube.Prisma.Infrastructure.Database.Services;
using ExxerCube.Prisma.Web.UI;

namespace ExxerCube.Prisma.Tests.EndToEnd;

/// <summary>
/// Custom WebApplicationFactory for testing the ExxerCube.Prisma.Web.UI application.
/// Configures the application for testing with in-memory database and test-specific settings.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<ExxerCube.Prisma.Web.UI.Program>
{
    /// <summary>
    /// Disposes the factory and ensures all resources are cleaned up.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Ensure all services are properly disposed
            // WebApplicationFactory base class handles disposal of the host and services
            // This override ensures we don't leave any SQL Server connections open
        }
        
        base.Dispose(disposing);
    }
    /// <summary>
    /// Configures the web host builder for testing.
    /// </summary>
    /// <param name="builder">The web host builder.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Configure minimal Serilog for testing (avoid file/seq sinks in tests)
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Console()
            .CreateLogger();

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Configure test database connection string
            var testConnectionString = "Server=(localdb)\\mssqllocaldb;Database=TestDb_" + Guid.NewGuid() + ";Trusted_Connection=True;MultipleActiveResultSets=true";
            
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", testConnectionString },
                { "ApiBaseUrl", "https://localhost:7062/" }
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace SQL Server DbContext registrations with in-memory database for tests
            // This prevents sqlsrv.exe processes from being spawned and left orphaned
            // Note: ConfigureServices runs AFTER Program.cs service registration, so we can remove existing registrations
            
            // Remove ApplicationDbContext (IDbContextFactory) registrations
            var appDbContextDescriptors = services
                .Where(d => d.ServiceType == typeof(IDbContextFactory<ApplicationDbContext>))
                .ToList();
            
            foreach (var descriptor in appDbContextDescriptors)
            {
                services.Remove(descriptor);
            }
            
            // Remove PrismaDbContext registrations (both direct and DbContextOptions)
            var prismaDbContextDescriptors = services
                .Where(d => 
                    d.ServiceType == typeof(PrismaDbContext) ||
                    (d.ServiceType.IsGenericType && 
                     d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>) &&
                     d.ServiceType.GetGenericArguments()[0] == typeof(PrismaDbContext)))
                .ToList();
            
            foreach (var descriptor in prismaDbContextDescriptors)
            {
                services.Remove(descriptor);
            }
            
            // Also remove IPrismaDbContext if registered separately
            var iprismaDbContextDescriptors = services
                .Where(d => d.ServiceType == typeof(IPrismaDbContext))
                .ToList();
            
            foreach (var descriptor in iprismaDbContextDescriptors)
            {
                services.Remove(descriptor);
            }
            
            // Register in-memory databases instead of SQL Server
            // Use unique database names to avoid conflicts between tests
            var appDbName = "TestAppDb_" + Guid.NewGuid();
            var prismaDbName = "TestPrismaDb_" + Guid.NewGuid();
            
            services.AddDbContextFactory<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(appDbName);
                options.EnableSensitiveDataLogging();
            });
            
            services.AddDbContext<PrismaDbContext>(options =>
            {
                options.UseInMemoryDatabase(prismaDbName);
                options.EnableSensitiveDataLogging();
            });
            
            // Register IPrismaDbContext to use PrismaDbContext
            services.AddScoped<IPrismaDbContext>(sp => sp.GetRequiredService<PrismaDbContext>());
            
            // Ensure database schemas are created when services are built
            // This is necessary for in-memory databases to work properly
            var serviceProvider = services.BuildServiceProvider();
            try
            {
                // Create ApplicationDbContext schema
                using (var scope = serviceProvider.CreateScope())
                {
                    var appDbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
                    using var appDbContext = appDbContextFactory.CreateDbContext();
                    appDbContext.Database.EnsureCreated();
                }
                
                // Create PrismaDbContext schema
                using (var scope = serviceProvider.CreateScope())
                {
                    var prismaDbContext = scope.ServiceProvider.GetRequiredService<PrismaDbContext>();
                    prismaDbContext.Database.EnsureCreated();
                }
            }
            finally
            {
                // Dispose the temporary service provider
                if (serviceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            
            // Disable hosted services that might create SQL connections
            // Remove background services that process audit logs and SLA updates
            // Remove QueuedAuditProcessorService singleton (it's also registered as IHostedService)
            var queuedAuditProcessorDescriptors = services
                .Where(d => 
                    d.ServiceType == typeof(QueuedAuditProcessorService) ||
                    (d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(QueuedAuditProcessorService)))
                .ToList();
            
            foreach (var descriptor in queuedAuditProcessorDescriptors)
            {
                services.Remove(descriptor);
            }
            
            // Remove other hosted services that might create connections
            var hostedServiceDescriptors = services
                .Where(d => 
                    d.ServiceType == typeof(IHostedService) &&
                    (d.ImplementationType == typeof(AuditRetentionBackgroundService) ||
                     d.ImplementationType == typeof(SLAUpdateBackgroundService)))
                .ToList();
            
            foreach (var descriptor in hostedServiceDescriptors)
            {
                services.Remove(descriptor);
            }
            
            // Configure test-specific services if needed
            // For example, you could replace real services with mocks here
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddSerilog();
        });

        builder.UseEnvironment("Testing");
    }
}

