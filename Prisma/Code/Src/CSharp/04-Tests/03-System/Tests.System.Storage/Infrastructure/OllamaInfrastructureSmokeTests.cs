using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ExxerCube.Prisma.Testing.Infrastructure.Fixtures;

namespace ExxerCube.Prisma.Tests.System.Storage.Infrastructure;

/// <summary>
/// Smoke tests for Ollama LLM container infrastructure.
/// Verifies that the Docker container can start, models are loaded correctly,
/// health checks pass, and basic embedding/LLM generation works.
/// These tests validate the testing infrastructure itself, not business logic.
/// </summary>
[Collection("OllamaInfrastructure")]
public sealed class OllamaInfrastructureSmokeTests
{
    private readonly OllamaContainerFixture _fixture;

    public OllamaInfrastructureSmokeTests(OllamaContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private void Log(string message)
    {
        TestContext.Current?.SendDiagnosticMessage(message);
    }

    private void Log(Exception ex, string message)
    {
        var fullMessage = $"{message}\nException: {ex.GetType().Name}: {ex.Message}\nStackTrace: {ex.StackTrace}";
        TestContext.Current?.SendDiagnosticMessage(fullMessage);
    }

    [Fact]
    public void Container_ShouldBeAvailable()
    {
        // Assert - Container should be running and available
        _fixture.IsAvailable.ShouldBeTrue("Ollama container should be running");
        _fixture.ConnectionString.ShouldNotBeNullOrWhiteSpace("Base URL should be configured");
        _fixture.BaseUrl.ShouldStartWith("http://");

        Log($"✅ Ollama container is available at: {_fixture.BaseUrl}");
    }

    [Fact]
    public async Task HealthCheck_ShouldPass()
    {
        // Arrange - Ensure container is available
        _fixture.EnsureAvailable();

        // Act - Perform health check
        var isHealthy = await _fixture.CheckHealthAsync();

        // Assert - Health check should pass
        isHealthy.ShouldBeTrue("Ollama service should be healthy");

        Log("✅ Ollama health check passed");
    }

    [Fact]
    public async Task Models_ShouldBeLoaded()
    {
        // Arrange - Ensure container is available
        _fixture.EnsureAvailable();

        // Act - Verify models are loaded
        var modelsLoaded = await _fixture.VerifyModelsAsync();

        // Assert - Both embedding and LLM models should be available
        modelsLoaded.ShouldBeTrue("Required models should be loaded");

        Log($"✅ Ollama models verified: {OllamaContainerFixture.EmbeddingModel}, {OllamaContainerFixture.LLMModel}");
    }

    [Fact]
    public async Task Models_ShouldLoadIntoMemory()
    {
        // Arrange - Ensure container is available and models downloaded
        _fixture.EnsureAvailable();
        var modelsDownloaded = await _fixture.VerifyModelsAsync();
        modelsDownloaded.ShouldBeTrue("Models should be downloaded before loading into memory");

        // Act - Load models into memory (warmup)
        var modelsLoadedInMemory = await _fixture.EnsureModelsLoadedAsync(TestContext.Current.CancellationToken);

        // Assert - Both models should load into memory successfully
        modelsLoadedInMemory.ShouldBeTrue("Models should load into memory successfully");

        Log($"✅ Ollama models loaded into memory: {OllamaContainerFixture.EmbeddingModel}, {OllamaContainerFixture.LLMModel}");
    }

    [Fact]
    public async Task EmbeddingModel_ShouldGenerateEmbeddings()
    {
        // Arrange - Ensure container is available
        _fixture.EnsureAvailable();
        using var httpClient = _fixture.GetHttpClient();

        var requestPayload = new
        {
            model = OllamaContainerFixture.EmbeddingModel,
            prompt = "This is a test document for embedding generation."
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestPayload),
            Encoding.UTF8,
            new MediaTypeHeaderValue("application/json"));

        // Act - Generate embedding
        var response = await httpClient.PostAsync("/api/embeddings", content, TestContext.Current.CancellationToken);

        // Assert - Embedding generation should succeed
        response.IsSuccessStatusCode.ShouldBeTrue("Embedding generation should succeed");

        var responseContent = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var jsonDoc = JsonDocument.Parse(responseContent);

        jsonDoc.RootElement.TryGetProperty("embedding", out var embeddingArray).ShouldBeTrue();
        var embeddingLength = embeddingArray.GetArrayLength();

        // Verify embedding dimensions match expected
        embeddingLength.ShouldBe(OllamaContainerFixture.EmbeddingDimensions,
            "Embedding dimensions should match expected value");

        Log($"✅ Embedding generated successfully. Dimensions: {embeddingLength}");
    }

    [Fact]
    public async Task LLMModel_ShouldGenerateText()
    {
        // Arrange - Ensure container is available
        _fixture.EnsureAvailable();
        using var httpClient = _fixture.GetHttpClient();

        var requestPayload = new
        {
            model = OllamaContainerFixture.LLMModel,
            prompt = "Say 'Hello, Prisma!' and nothing else.",
            stream = false,
            options = new
            {
                temperature = 0.0,  // Deterministic for testing
                num_predict = 20    // Limit output tokens
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestPayload),
            Encoding.UTF8,
            new MediaTypeHeaderValue("application/json"));

        // Act - Generate text
        var response = await httpClient.PostAsync("/api/generate", content, TestContext.Current.CancellationToken);

        // Assert - Text generation should succeed
        response.IsSuccessStatusCode.ShouldBeTrue("Text generation should succeed");

        var responseContent = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var jsonDoc = JsonDocument.Parse(responseContent);

        jsonDoc.RootElement.TryGetProperty("response", out var responseText).ShouldBeTrue();
        var generatedText = responseText.GetString();

        generatedText.ShouldNotBeNullOrWhiteSpace("Generated text should not be empty");
        generatedText.Length.ShouldBeGreaterThan(0, "Generated text should have content");

        Log($"✅ Text generated successfully: {generatedText?.Substring(0, Math.Min(50, generatedText?.Length ?? 0))}");
    }

    [Fact]
    public async Task ApiTags_ShouldListAvailableModels()
    {
        // Arrange - Ensure container is available
        _fixture.EnsureAvailable();
        using var httpClient = _fixture.GetHttpClient();

        // Act - Get available models
        var response = await httpClient.GetAsync("/api/tags", TestContext.Current.CancellationToken);

        // Assert - Should return model list
        response.IsSuccessStatusCode.ShouldBeTrue("API tags endpoint should succeed");

        var responseContent = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var jsonDoc = JsonDocument.Parse(responseContent);

        jsonDoc.RootElement.TryGetProperty("models", out var modelsArray).ShouldBeTrue();
        var modelCount = modelsArray.GetArrayLength();

        // Should have at least 2 models (embedding + LLM)
        modelCount.ShouldBeGreaterThanOrEqualTo(2, "Should have at least 2 models loaded");

        // Extract model names for logging
        var modelNames = new List<string>();
        foreach (var model in modelsArray.EnumerateArray())
        {
            if (model.TryGetProperty("name", out var nameElement))
            {
                var name = nameElement.GetString();
                if (name != null)
                    modelNames.Add(name);
            }
        }

        Log($"✅ Available models ({modelCount}): {string.Join(", ", modelNames)}");
    }

    [Fact]
    public void OllamaConfiguration_ShouldHaveCorrectSettings()
    {
        // Assert - Verify fixture configuration
        OllamaContainerFixture.EmbeddingModel.ShouldBe("nomic-embed-text");
        OllamaContainerFixture.LLMModel.ShouldBe("llama3.2:3b");
        OllamaContainerFixture.EmbeddingDimensions.ShouldBe(768);

        Log("✅ Ollama configuration verified");
    }
}

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
