var builder = WebApplication.CreateBuilder(args);

// Veriqan VEC batch worker host (Epic 1 skeleton). Pipeline DI composition (rules, adapters,
// reference-data providers, reporting) is wired through Veriqan.Orchestration as feature epics land.
var app = builder.Build();

// Health endpoints (minimal: the Veriqan worker is a background batch host with no readiness gate yet).
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));

await app.RunAsync();

namespace ExxerCube.Prisma.Veriqan.Worker
{
    /// <summary>
    /// Program entry point for the Veriqan VEC worker (made public for WebApplicationFactory testing).
    /// </summary>
    public partial class Program { }
}
