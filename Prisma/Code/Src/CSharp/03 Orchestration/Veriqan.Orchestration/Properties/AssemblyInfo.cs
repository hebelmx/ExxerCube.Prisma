using System.Runtime.CompilerServices;

// Allow the Orchestration test project to access internal types (BatchProcessor, VerificationPipeline)
// for DI-based integration tests.
[assembly: InternalsVisibleTo("ExxerCube.Prisma.Veriqan.Orchestration.Tests")]

// Allow the Persistence integration test project to wire EfVerificationResultStore and
// EfReprocessAuditRepository directly for the durable-stores integration tests (Story 6.1).
[assembly: InternalsVisibleTo("ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.IntegrationTests")]
