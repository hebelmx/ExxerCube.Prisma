using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prisma.Orion.Ingestion;
using Prisma.Orion.Worker;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IngestionOrchestrator>();
builder.Services.AddHostedService<OrionWorkerService>();

var app = builder.Build();
await app.RunAsync();