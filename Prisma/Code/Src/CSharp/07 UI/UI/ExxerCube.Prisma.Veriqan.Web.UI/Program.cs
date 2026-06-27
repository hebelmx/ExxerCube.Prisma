using ExxerCube.Prisma.Veriqan.Web.UI.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Razor components + Interactive Server rendering (mirrors ExxerCube.Prisma.Web.UI) ──
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── MudBlazor ──
builder.Services.AddMudServices();

// ── Demo data service (singleton: data is static, no DB required) ──
builder.Services.AddSingleton<DemoDataService>();

// ── Health (minimal, no DB dependency needed for demo) ──
builder.Services.AddHealthChecks();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<ExxerCube.Prisma.Veriqan.Web.UI.Components.App>()
    .AddInteractiveServerRenderMode();

app.MapHealthChecks("/health");

app.Run();
