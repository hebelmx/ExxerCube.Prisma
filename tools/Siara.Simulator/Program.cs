using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.FileProviders;
using Serilog;
using Siara.Simulator.Components;
using Siara.Simulator.Configuration;
using Siara.Simulator.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Configure SimulatorSettings from appsettings.json
builder.Services.Configure<SimulatorSettings>(
    builder.Configuration.GetSection("SimulatorSettings"));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register the simulation background service
builder.Services.AddSingleton<CaseService>();

// ============================================================================
// SIARA AUTHENTICATION — cookie-based session (models the real SIARA)
// ============================================================================
// The real siara.cnbv.gob.mx is a username/password web form that issues a
// SESSION COOKIE and locks the account after too many failed attempts. The
// simulator now mirrors that with ASP.NET Core cookie authentication so that a
// browser-automation scraper's exported storage-state (which captures cookies)
// actually carries the authenticated session — the behaviour the Prisma SIARA
// auth providers (ADR-010) depend on. The previous demo auto-login used Blazor
// *circuit* state, which storage-state cannot capture.
//
// Credentials are PUBLIC FAKE values for the simulator only (Auth section /
// SiaraAuthOptions defaults). This is NOT real authentication — no hashing,
// CSRF-on-login only, in-memory lockout. Do not use this pattern in production.
// ============================================================================
builder.Services.Configure<SiaraAuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.AddSingleton<SiaraCredentialValidator>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "siara_session";
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Log warning if not in development mode
if (!builder.Environment.IsDevelopment())
{
    Console.WriteLine("==============================================");
    Console.WriteLine("⚠️  WARNING: SIARA SIMULATOR RUNNING");
    Console.WriteLine("==============================================");
    Console.WriteLine("This is a SIMULATOR/DEMO environment.");
    Console.WriteLine("It is NOT the real SIARA system.");
    Console.WriteLine("Do NOT use for production purposes.");
    Console.WriteLine("Do NOT enter real confidential data.");
    Console.WriteLine("==============================================");
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// Add Serilog request logging
app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

// Serve static files from wwwroot (like css, js) — anonymous.
app.UseStaticFiles();

// Authentication must run before any gated resource so HttpContext.User is populated from the cookie.
app.UseAuthentication();
app.UseAuthorization();

// Gate the document store on an authenticated SIARA session — a scraper must hold the session cookie
// to download documents, exactly as it would against the real SIARA.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/document_store") &&
        context.User.Identity?.IsAuthenticated != true)
    {
        context.Response.Redirect("/login");
        return;
    }

    await next();
});

// Serve static files from the document store (now gated by the middleware above).
var documentStorePath = Path.Combine(builder.Environment.ContentRootPath, "..", "bulk_generated_documents_all_formats");
if (Directory.Exists(documentStorePath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(documentStorePath),
        RequestPath = "/document_store"
    });
}

app.UseAntiforgery();

// Logout endpoint — clears the session cookie and returns to the login page.
app.MapPost("/auth/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).DisableAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// CaseService is started when the (authenticated) Dashboard page loads.

app.Run();
