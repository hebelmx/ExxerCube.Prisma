namespace ExxerCube.Prisma.Tests.System.Storage.Infrastructure;

/// <summary>
/// xUnit collection definition for Ollama infrastructure tests.
/// Ensures all tests in this collection share the same OllamaContainerFixture instance,
/// which means the container is started once and reused across all tests.
/// Model pulling happens once during fixture initialization, speeding up test execution.
/// </summary>
[CollectionDefinition("OllamaInfrastructure")]
public sealed class OllamaInfrastructureCollection : ICollectionFixture<OllamaContainerFixture>
{
    // This class is just a marker for xUnit collection fixture
    // The actual fixture implementation is OllamaContainerFixture
}