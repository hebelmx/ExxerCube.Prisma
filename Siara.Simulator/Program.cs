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
// SIARA AUTHENTICATION FLOW - SOURCE OF TRUTH
// ============================================================================
// This is a DEMO/SIMULATOR - Authentication is intentionally simplified.
//
// USER STORY:
// 1. User opens the application
// 2. App starts in NOT AUTHENTICATED state (CaseService created but NOT started)
// 3. Routes.razor detects unauthenticated user and redirects to /login
// 4. User sees login form (username and password fields for appearance only)
// 5. User enters ANY credentials (validation is cosmetic only)
// 6. User clicks "Ingresar" (submit button)
// 7. Login ALWAYS succeeds (AuthenticationService.Login() always returns true)
// 8. User is redirected to Dashboard (/)
// 9. Dashboard.OnInitialized() calls CaseService.Start() - simulation begins
// 10. User sees case arrivals with documents (PDF, DOCX, XML) in real-time
//
// TECHNICAL IMPLEMENTATION:
// - AuthenticationService is registered as SINGLETON (not Scoped)
// - Singleton ensures authentication state survives Blazor circuit resets
// - Login() method accepts any password and always sets IsAuthenticated = true
// - Routes.razor checks IsAuthenticated on every navigation
// - forceLoad: true on navigation ensures clean page reload
//
// WHY SINGLETON (normally anti-pattern for auth):
// - This is a single-user demo simulator, not a multi-user application
// - Avoids Blazor Server circuit lifecycle issues (Scoped gets recreated)
// - State persists across page reloads and reconnections
//
// WARNING: DO NOT USE THIS PATTERN IN PRODUCTION
// Real applications need:
// - Proper authentication (ASP.NET Core Identity, JWT, OAuth)
// - Per-user state management (Scoped services with session storage)
// - Secure password hashing and validation
// - CSRF protection, rate limiting, etc.
// ============================================================================

// Register authentication service (always - this is a simulator/demo)
builder.Services.AddSingleton<AuthenticationService>();

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

// ============================================================================
// FORCE LOGOUT AT STARTUP - Ensure app always starts in logged-out state
// ============================================================================
// This allows tests to demonstrate the complete login flow from the beginning.
// Without this, the AuthenticationService singleton might retain logged-in state
// from previous runs (especially during development/hot reload scenarios).
// ============================================================================
using (var scope = app.Services.CreateScope())
{
    var authService = scope.ServiceProvider.GetRequiredService<AuthenticationService>();
    authService.Logout();
    Log.Information("Application startup: User logged out - ready for login demonstration");
}

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

// Serve static files from wwwroot (like css, js)
app.UseStaticFiles();

// Serve static files from the document store
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

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// CaseService will be started when Dashboard page loads (deferred initialization)

app.Run();