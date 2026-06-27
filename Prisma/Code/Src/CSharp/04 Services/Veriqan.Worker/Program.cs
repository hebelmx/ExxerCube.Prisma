using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Stores;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Adapters;
using ExxerCube.Prisma.Veriqan.Orchestration.Batch;
using ExxerCube.Prisma.Veriqan.Orchestration.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Orchestration.Observability;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using ExxerCube.Prisma.Veriqan.Worker;
using ExxerCube.Prisma.Veriqan.Worker.HealthChecks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ──────────────────────────────────────────────────────────────────
// Console sink is ALWAYS active. Seq sink is added ONLY when a non-empty
// Veriqan:Seq:ServerUrl is configured — an empty/absent URL does NOT crash boot.
var seqUrl = builder.Configuration["Veriqan:Seq:ServerUrl"];
var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .Enrich.WithProperty("Application", "ExxerCube.Prisma.Veriqan.Worker");

if (!string.IsNullOrWhiteSpace(seqUrl))
    loggerConfig = loggerConfig.WriteTo.Seq(seqUrl);

Log.Logger = loggerConfig.CreateLogger();
builder.Host.UseSerilog();

// ── OpenTelemetry ─────────────────────────────────────────────────────────────
var otlpEndpoint = builder.Configuration["Veriqan:OtlpEndpoint"] ?? "http://localhost:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("ExxerCube.Prisma.Veriqan.Worker"))
    .WithMetrics(metrics => metrics
        .AddMeter(VeriqanMetrics.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint)))
    .WithTracing(tracing => tracing
        .AddSource(VeriqanMetrics.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint)));

// Veriqan VEC batch worker — full DI composition via Orchestration layer.
builder.Services.AddVeriqan(builder.Configuration);

// Health-check service — the SqlLegalToleranceProvider is optional (only present when SQL
// persistence is configured via ConnectionStrings:VeriqanDb). We resolve it as the concrete
// type directly so the check can read IsWarm; the interface does not expose that property.
builder.Services.AddSingleton<IHealthCheckService>(sp =>
{
    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
    // Null when running with in-memory persistence (no connection string).
    var toleranceProvider = sp.GetService<SqlLegalToleranceProvider>();
    var csvOptions = sp.GetRequiredService<IOptions<CsvReferenceDataOptions>>();
    var logger = sp.GetRequiredService<ILogger<VeriqanWorkerHealthCheckService>>();
    return new VeriqanWorkerHealthCheckService(scopeFactory, toleranceProvider, csvOptions, logger);
});

// ── CORS (#N3) ────────────────────────────────────────────────────────────────
// Origin list is read from Veriqan:Cors:AllowedOrigins (string array).
// When the array is absent or empty the policy denies every cross-origin request —
// this is the secure default. Populate the array in production to allow your UI origins.
const string VeriqanCorsPolicy = "VeriqanCors";
var allowedOrigins = builder.Configuration
    .GetSection("Veriqan:Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
    options.AddPolicy(VeriqanCorsPolicy, policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            // Deny all cross-origin requests when no origins are configured.
            policy.SetIsOriginAllowed(_ => false);
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    }));

// ── Authentication + Authorization (#17) ─────────────────────────────────────
// Secure-by-default: JWT bearer auth is ENABLED unless Veriqan:Auth:Enabled is
// explicitly set to false. The escape hatch (Enabled=false) installs a permissive
// "allow all" policy so local/demo runs work without a token — NEVER use in production.
//
// When enabled, tokens are validated against the Veriqan:Auth:Jwt section:
//   Issuer     — the expected iss claim; omit to skip issuer validation.
//   Audience   — the expected aud claim; omit to skip audience validation.
//   SigningKey  — UTF-8 bytes of a shared HMAC-SHA256 key (min 32 chars recommended).
//                 Replace with an environment variable or Key Vault reference in production.
//                 A missing/placeholder key is logged loudly at startup.
var authEnabled = builder.Configuration.GetValue("Veriqan:Auth:Enabled", defaultValue: true);

if (!authEnabled)
{
    Log.Warning(
        "SECURITY WARNING: JWT authentication is DISABLED (Veriqan:Auth:Enabled = false). " +
        "All requests to protected endpoints will be permitted without a bearer token. " +
        "NEVER run with authentication disabled in an environment that handles real legal documents.");

    // Register the JWT bearer handler with validation disabled. The permissive
    // DefaultPolicy set below allows every request through regardless of token state,
    // so no token is ever required. UseAuthentication / UseAuthorization remain in
    // the pipeline so the production code path is identical — only the policy differs.
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = false,
            };
        });

    builder.Services.AddAuthorization(opts =>
    {
        // Allow every request — anonymous or otherwise — when auth is disabled.
        var permissive = new AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true)
            .Build();
        opts.DefaultPolicy = permissive;
        opts.FallbackPolicy = permissive;
    });
}
else
{
    var jwtSection = builder.Configuration.GetSection("Veriqan:Auth:Jwt");
    var issuer = jwtSection["Issuer"];
    var audience = jwtSection["Audience"];
    var signingKey = jwtSection["SigningKey"];

    // Loud startup warning when the signing key is absent or still a placeholder.
    // The service will boot but every token will be rejected at runtime.
    if (string.IsNullOrWhiteSpace(signingKey) ||
        signingKey.StartsWith("<REPLACE", StringComparison.Ordinal))
    {
        Log.Warning(
            "SECURITY WARNING: {ConfigKey} is absent or contains a placeholder value. " +
            "JWT bearer validation will reject all tokens until a real key is supplied. " +
            "Set the key via an environment variable (Veriqan__Auth__Jwt__SigningKey) or Key Vault.",
            "Veriqan:Auth:Jwt:SigningKey");
    }

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = !string.IsNullOrWhiteSpace(issuer),
                ValidIssuer = issuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                ValidAudience = audience,
                ValidateIssuerSigningKey = !string.IsNullOrWhiteSpace(signingKey),
                IssuerSigningKey = string.IsNullOrWhiteSpace(signingKey)
                    ? null
                    : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                ValidateLifetime = true,
                // 30-second clock skew tolerates modest server-clock drift.
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        });

    builder.Services.AddAuthorization();
}

var app = builder.Build();

// ── Middleware pipeline — order is mandated by ASP.NET Core ──────────────────
// 1. HTTPS redirect — upgrades plain-HTTP requests before any business logic runs.
app.UseHttpsRedirection();

// 2. HSTS — tells browsers to only use HTTPS for future visits. Suppress in Development
//    so a self-signed dev-cert does not permanently poison the browser HSTS store.
if (!app.Environment.IsDevelopment())
    app.UseHsts();

// 3. CORS — must precede authentication so that pre-flight OPTIONS requests are answered
//    before the auth middleware short-circuits them.
app.UseCors(VeriqanCorsPolicy);

// 4. Authentication → 5. Authorization
app.UseAuthentication();
app.UseAuthorization();

// ── Health endpoints — always anonymous ───────────────────────────────────────
// /health/live  → liveness only: process-up, no external deps.
// /health/ready → all three readiness checks; 503 when any check fails.
// /health       → combined (same readiness set); kept for backward compatibility.
app.MapGet("/health/live", async (IHealthCheckService healthCheck, CancellationToken ct) =>
{
    var result = await healthCheck.GetLivenessAsync(ct);
    return Results.Ok(new { status = result.Status.ToString(), description = result.Description, data = result.Data });
}).AllowAnonymous();

app.MapGet("/health/ready", async (IHealthCheckService healthCheck, CancellationToken ct) =>
{
    var result = await healthCheck.GetReadinessAsync(ct);
    return result.Status == OrchestratorHealthState.Healthy
        ? Results.Ok(new { status = result.Status.ToString(), description = result.Description, data = result.Data })
        : Results.Json(new { status = result.Status.ToString(), description = result.Description, data = result.Data }, statusCode: 503);
}).AllowAnonymous();

app.MapGet("/health", async (IHealthCheckService healthCheck, CancellationToken ct) =>
{
    var result = await healthCheck.GetHealthAsync(ct);
    return result.Status == OrchestratorHealthState.Healthy
        ? Results.Ok(new { status = result.Status.ToString(), description = result.Description, data = result.Data })
        : Results.Json(new { status = result.Status.ToString(), description = result.Description, data = result.Data }, statusCode: 503);
}).AllowAnonymous();

// ── Protected endpoints — JWT bearer required when auth is enabled ─────────────
// When Veriqan:Auth:Enabled = false the DefaultPolicy is permissive (RequireAssertion
// always returns true), so .RequireAuthorization() is a no-op in practice. This lets the
// endpoint declarations stay uniform regardless of the auth mode.

// POST /verify — single-statement verification.
app.MapPost("/verify", async (
    [FromBody] StatementSubmission submission,
    [FromServices] IVerificationPipeline pipeline,
    HttpContext httpContext) =>
{
    var ct = httpContext.RequestAborted;

    if (ct.IsCancellationRequested)
        return Results.StatusCode(499); // client closed request

    var result = await pipeline.ProcessAsync(submission, ct);

    if (!result.IsSuccess)
        return Results.UnprocessableEntity(new { error = result.Error });

    var jobId = result.Value!.Job.Id;
    return Results.Accepted(value: new { jobId });
}).RequireAuthorization();

// POST /batch — multi-statement batch verification.
app.MapPost("/batch", async (
    [FromBody] BatchSubmissionRequest request,
    [FromServices] IBatchProcessor batchProcessor,
    HttpContext httpContext) =>
{
    var ct = httpContext.RequestAborted;

    if (ct.IsCancellationRequested)
        return Results.StatusCode(499); // client closed request

    var options = request.Options ?? new BatchOptions();
    var result = await batchProcessor.ProcessBatchAsync(request.Items, options, progress: null, ct);

    if (!result.IsSuccess)
        return Results.UnprocessableEntity(new { error = result.Error });

    var report = result.Value!;
    return Results.Accepted(value: new
    {
        totalSubmitted = report.TotalSubmitted,
        completedCount = report.CompletedCount,
        failedCount = report.FailedCount,
    });
}).RequireAuthorization();

await app.RunAsync();

namespace ExxerCube.Prisma.Veriqan.Worker
{
    /// <summary>
    /// Program entry point for the Veriqan VEC worker (made public for WebApplicationFactory testing).
    /// </summary>
    public partial class Program { }
}
