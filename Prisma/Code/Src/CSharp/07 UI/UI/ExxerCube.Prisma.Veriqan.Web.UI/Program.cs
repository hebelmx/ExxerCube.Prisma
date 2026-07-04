using ExxerCube.Prisma.Veriqan.Orchestration.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Web.UI.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Razor components + Interactive Server rendering (mirrors ExxerCube.Prisma.Web.UI) ──
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── MudBlazor ──
builder.Services.AddMudServices();

// ── Real Veriqan pipeline composition root (VLD-S1) ──────────────────────────
// Veriqan:CsvReferenceData:RootDirectory is stored repo-root-relative in appsettings.json
// (e.g. "Prisma/Fixtures/PRP2/demo/reference-bundle") and resolved to an absolute path here —
// the Web.UI's working directory differs between `dotnet run` (assembly under BuildArtifacts,
// a sibling of the repository root) and a published container, so a hardcoded relative path
// would break in at least one of those environments.
var csvReferenceDataRelativePath = builder.Configuration["Veriqan:CsvReferenceData:RootDirectory"];
if (!string.IsNullOrWhiteSpace(csvReferenceDataRelativePath) && !Path.IsPathRooted(csvReferenceDataRelativePath))
{
    var resolvedCsvReferenceDataRoot = DemoCorpusPathResolver.ResolveAbsolutePath(
        AppContext.BaseDirectory, csvReferenceDataRelativePath);

    if (resolvedCsvReferenceDataRoot is not null)
    {
        builder.Configuration["Veriqan:CsvReferenceData:RootDirectory"] = resolvedCsvReferenceDataRoot;
    }
    // else: repository root not found (e.g. running outside the normal build tree) — leave the
    // configured value as-is; AddVeriqanReferenceData/readiness probes report the gap explicitly
    // rather than this host crashing at startup.
}

// Mirrors Veriqan.Worker/Program.cs's composition root.
// PERSISTENCE DECISION (VLD-S1, explicit): ConnectionStrings:VeriqanDb is intentionally OMITTED
// from appsettings.json for this Web.UI demo surface, so AddVeriqan falls back to
// AddVeriqanInMemoryPersistence() (VeriqanOrchestrationExtensions.cs ~L108-134) — no SQL Server
// dependency for `dotnet run`. Rationale: this is a sales-demo surface, not a system of record;
// an in-memory store also guarantees a demo run can never collide with the worker's own data.
// A durable shared VeriqanDb can be wired later (VLD-S7) if an audit trail of live demo runs
// is wanted — that is an open decision, not settled here.
builder.Services.AddVeriqan(builder.Configuration);

// ── Demo data service (singleton: data is static, no DB required) — kept as the fallback ──
builder.Services.AddSingleton<DemoDataService>();

// ── Marked-page PDF→PNG rasterizer (VLD-S3): stateless, singleton is fine ──
builder.Services.AddSingleton<IMarkedPageRenderer, MarkedPageRenderer>();

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
