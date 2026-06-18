using System.Runtime.CompilerServices;

// Allow the Orchestration test project to access internal types (BatchProcessor, VerificationPipeline)
// for DI-based integration tests.
[assembly: InternalsVisibleTo("ExxerCube.Prisma.Veriqan.Orchestration.Tests")]
