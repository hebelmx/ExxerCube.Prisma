using Microsoft.Extensions.FileProviders;
using Siara.Simulator.Components;
using Siara.Simulator.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register the simulation background service
builder.Services.AddSingleton<CaseService>();

// Register the user's authentication state service
builder.Services.AddScoped<AuthenticationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
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

// Manually start the CaseService by retrieving it from the service provider.
// This ensures its constructor runs and the simulation starts.
app.Services.GetRequiredService<CaseService>();

app.Run();