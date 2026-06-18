using ExxerCube.Prisma.Veriqan.Orchestration.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Veriqan VEC batch worker — full DI composition via Orchestration layer.
builder.Services.AddVeriqan(builder.Configuration);

var app = builder.Build();

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
