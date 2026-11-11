# Lessons Learned: Telemetry Integration & DI Container Testing

**Date:** 2025-01-15  
**Status:** ✅ Completed  
**Related Areas:** Dependency Injection, Telemetry, Testing, Build Configuration

---

## Executive Summary

This document captures key learnings from implementing OpenTelemetry telemetry/tracing integration and fixing DI container testing to validate the actual application configuration. Key achievements include centralized build configuration, proper DbContext factory patterns, and comprehensive telemetry setup.

---

## 🎯 Key Learnings

### 1. Centralized Build Warning Suppression

**Problem:** NU1902 vulnerability warnings appeared across multiple projects, requiring individual project file modifications.

**Solution:** Centralized suppression in `Directory.Build.props`:
- Added `NoWarn` to suppress warnings entirely
- Added `WarningsNotAsErrors` to prevent warnings from breaking builds
- Documented rationale in comments and `SECURITY_SUPPRESSIONS.md`

**Pattern:**
```xml
<!-- In Directory.Build.props -->
<NoWarn>$(NoWarn);NU1902</NoWarn>
<WarningsNotAsErrors>$(WarningsNotAsErrors);NU1902</WarningsNotAsErrors>
```

**Benefits:**
- Single point of maintenance
- Consistent across all projects
- Easy to update when vulnerability is fixed

**Action Items:**
- ✅ Always use `Directory.Build.props` for build-wide settings
- ✅ Document security suppressions with rationale
- ✅ Reference external documentation (SECURITY_SUPPRESSIONS.md)

---

### 2. DbContext Factory Registration Pattern

**Problem:** 
- `IDbContextFactory<T>` registered as Singleton consuming scoped `DbContextOptions<T>` caused DI validation errors
- Tests were not testing the actual application DI configuration

**Solution:**
- Register `IDbContextFactory<T>` using `AddDbContextFactory<T>()` which handles lifetime correctly
- Do NOT register `DbContext` directly when using factory pattern
- Extract `ConfigureServices` method from `Program.cs` for testability

**Pattern:**
```csharp
// ✅ Correct: Factory pattern
services.AddDbContextFactory<ApplicationDbContext>(options => 
    options.UseSqlServer(connectionString));

// ❌ Wrong: Don't register DbContext when using factory
// services.AddDbContext<ApplicationDbContext>(...); // Don't do this
```

**Benefits:**
- Proper lifetime management (factory is singleton, contexts are scoped)
- Testable DI configuration
- Matches repository pattern requirements

**Action Items:**
- ✅ Use factory pattern for DbContext in repository-based architectures
- ✅ Extract `ConfigureServices` from `Program.cs` for testing
- ✅ Test actual application DI configuration, not mock setup

---

### 3. Test-Driven DI Container Validation

**Problem:** Tests were creating their own DI setup instead of validating the actual application configuration.

**Solution:**
- Extracted `Program.ConfigureServices` method
- Tests call the actual application's service configuration
- Use `WebApplicationFactory` for full-stack testing

**Pattern:**
```csharp
// In Program.cs
public static void ConfigureServices(
    IServiceCollection services, 
    IConfiguration configuration, 
    IWebHostEnvironment environment)
{
    // Actual application DI configuration
}

// In Tests
var services = new ServiceCollection();
var config = new ConfigurationBuilder().Build();
var env = new TestHostEnvironment();
Program.ConfigureServices(services, config, env);
var provider = services.BuildServiceProvider();
```

**Benefits:**
- Tests validate actual production configuration
- Catches DI misconfigurations early
- Ensures tests reflect real application behavior

**Action Items:**
- ✅ Extract service configuration for testability
- ✅ Test actual DI setup, not mock setup
- ✅ Use `WebApplicationFactory` for integration testing

---

### 4. OpenTelemetry Integration with Seq

**Problem:** Need comprehensive telemetry (tracing, metrics, logs) with queryable storage.

**Solution:**
- Use OpenTelemetry for instrumentation
- Export to Seq via OTLP (OpenTelemetry Protocol)
- Configure in `Program.cs` with environment-based settings

**Pattern:**
```csharp
services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder => 
    {
        tracerProviderBuilder
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter(options => 
            {
                options.Endpoint = new Uri($"{seqEndpoint}/ingest/otlp/v1/traces");
                options.Protocol = OtlpExportProtocol.HttpProtobuf;
            });
    })
    .WithMetrics(metricsProviderBuilder => 
    {
        metricsProviderBuilder
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation()
            .AddOtlpExporter(options => 
            {
                options.Endpoint = new Uri($"{seqEndpoint}/ingest/otlp/v1/metrics");
            });
    });
```

**Configuration (`appsettings.json`):**
```json
{
  "OpenTelemetry": {
    "ServiceName": "ExxerCube.Prisma.Web.UI",
    "ServiceVersion": "1.0.0",
    "Seq": {
      "Endpoint": "http://localhost:5341",
      "ApiKey": null
    },
    "Tracing": {
      "Enabled": true,
      "SamplingRatio": 1.0
    },
    "Metrics": {
      "Enabled": true
    }
  }
}
```

**Benefits:**
- Standardized telemetry (OpenTelemetry)
- Queryable logs/metrics/traces in Seq
- Native .NET 10 support
- Environment-configurable

**Action Items:**
- ✅ Use OpenTelemetry for all telemetry needs
- ✅ Export to Seq via OTLP for queryability
- ✅ Make telemetry configurable via appsettings
- ✅ Disable telemetry in test environments

---

### 5. Resource Disposal in Database Tests

**Problem:** SQL Server connections not properly disposed in tests, causing resource leaks.

**Solution:**
- Ensure proper disposal of `DbContext` instances
- Use `using` statements or `await using` for async disposal
- Configure in-memory database for tests

**Pattern:**
```csharp
// In TestWebApplicationFactory
services.AddDbContextFactory<ApplicationDbContext>(options =>
{
    options.UseInMemoryDatabase("InMemoryDbForTesting");
});

// In tests
await using var scope = serviceProvider.CreateAsyncScope();
var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
await using var context = await factory.CreateDbContextAsync(cancellationToken);
// Context automatically disposed
```

**Benefits:**
- No resource leaks
- Proper async disposal
- Isolated test databases

**Action Items:**
- ✅ Always dispose DbContext instances
- ✅ Use `await using` for async disposal
- ✅ Use in-memory databases for unit tests
- ✅ Verify no connection leaks in test cleanup

---

## 📋 Checklist for Future Implementations

### Before Starting:
- [ ] Check if build-wide settings belong in `Directory.Build.props`
- [ ] Verify DbContext registration pattern matches architecture
- [ ] Ensure DI configuration is testable

### During Implementation:
- [ ] Extract service configuration for testability
- [ ] Test actual application DI, not mock setup
- [ ] Configure telemetry with environment-based settings
- [ ] Document security suppressions with rationale

### After Implementation:
- [ ] Verify no resource leaks in tests
- [ ] Validate DI container configuration
- [ ] Test telemetry export functionality
- [ ] Update documentation with patterns used

---

## 🔗 Related Documentation

- [SECURITY_SUPPRESSIONS.md](../../Prisma/Code/Src/CSharp/UI/ExxerCube.Prisma.Web.UI/SECURITY_SUPPRESSIONS.md) - Security warning suppressions
- [Directory.Build.props](../../Prisma/Code/Src/CSharp/Directory.Build.props) - Build-wide configuration
- [Program.cs](../../Prisma/Code/Src/CSharp/UI/ExxerCube.Prisma.Web.UI/Program.cs) - Application entry point with DI configuration

---

## 📝 Notes

- OpenTelemetry packages may have transitive vulnerabilities (NU1902) - monitor for updates
- Seq supports OTLP natively - no need for custom exporters
- SignalR instrumentation is included in ASP.NET Core instrumentation
- Always use factory pattern for DbContext in repository-based architectures

