using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prisma.Athena.Processing;
using Prisma.Athena.Worker;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<ProcessingOrchestrator>();
builder.Services.AddHostedService<AthenaWorkerService>();

var app = builder.Build();
await app.RunAsync();