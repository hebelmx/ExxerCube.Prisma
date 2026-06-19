using ExxerCube.Prisma.Veriqan.Orchestration.Batch;
using ExxerCube.Prisma.Veriqan.Orchestration.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using ExxerCube.Prisma.Veriqan.Worker;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Veriqan VEC batch worker — full DI composition via Orchestration layer.
builder.Services.AddVeriqan(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));

// POST /verify — single-statement verification.
// TODO(E4): add authentication/authorisation before production deployment.
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
});

// POST /batch — multi-statement batch verification.
// TODO(E4): add authentication/authorisation before production deployment.
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
});

await app.RunAsync();

namespace ExxerCube.Prisma.Veriqan.Worker
{
    /// <summary>
    /// Program entry point for the Veriqan VEC worker (made public for WebApplicationFactory testing).
    /// </summary>
    public partial class Program { }
}
