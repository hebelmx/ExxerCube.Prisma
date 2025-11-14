using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using ExxerCube.Prisma.Web.UI.Components;
using ExxerCube.Prisma.Web.UI.Components.Account;
using ExxerCube.Prisma.Web.UI.Data;
using ExxerCube.Prisma.Infrastructure.DependencyInjection;
using ExxerCube.Prisma.Web.UI.Hubs;
using ExxerCube.Prisma.Infrastructure.Database.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.FileStorage;
using ExxerCube.Prisma.Infrastructure.FileStorage.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.Extraction;
using ExxerCube.Prisma.Infrastructure.Classification;
using ExxerCube.Prisma.Application.Services;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

// Add SignalR for real-time updates
builder.Services.AddSignalR();

// Register SignalR hub as scoped (SignalR hubs are scoped by default)
builder.Services.AddScoped<ProcessingHub>();

// Add OCR processing services
var pythonModulesPath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "Python", "ocr_modules");
var pythonConfig = new ExxerCube.Prisma.Infrastructure.Python.PythonConfiguration
{
    ModulesPath = pythonModulesPath,
    PythonExecutablePath = "python",
    MaxConcurrency = 5,
    OperationTimeoutSeconds = 30,
    EnableDebugging = builder.Environment.IsDevelopment()
};
builder.Services.AddOcrProcessingServices(pythonConfig);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add API controllers
builder.Services.AddControllers();

// Add HttpClient for API calls with proper configuration
builder.Services.AddHttpClient("api", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7062/");
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// Add Story 1.1 services: Browser Automation, File Storage, and Database services
builder.Services.AddDatabaseServices(connectionString);
builder.Services.AddBrowserAutomationServices(options =>
{
    builder.Configuration.GetSection("BrowserAutomation").Bind(options);
});
builder.Services.AddFileStorageServices(options =>
{
    builder.Configuration.GetSection("FileStorage").Bind(options);
});

// Add Story 1.2 services: Extraction, Classification, and Metadata Extraction
builder.Services.AddExtractionServices();
builder.Services.AddClassificationServices(builder.Configuration);
builder.Services.AddScoped<MetadataExtractionService>();

// Add Story 1.3 services: Field Matching and Unified Metadata Generation
builder.Services.AddScoped<FieldMatchingService>();
// Register FieldMatcherService instances for each source type
builder.Services.AddScoped(typeof(IFieldMatcher<Domain.Entities.DocxSource>), typeof(Infrastructure.Classification.FieldMatcherService<Domain.Entities.DocxSource>));
builder.Services.AddScoped(typeof(IFieldMatcher<Domain.Entities.PdfSource>), typeof(Infrastructure.Classification.FieldMatcherService<Domain.Entities.PdfSource>));

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

// Map SignalR hub
app.MapHub<ProcessingHub>("/processingHub");

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
