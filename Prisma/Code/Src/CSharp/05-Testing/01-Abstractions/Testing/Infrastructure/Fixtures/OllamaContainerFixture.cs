using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using ExxerCube.Prisma.Testing.Infrastructure.Fixtures.Base;

namespace ExxerCube.Prisma.Testing.Infrastructure.Fixtures;

/// <summary>
/// Ollama LLM service container fixture for language model integration and system tests.
/// Implements xUnit v3 IAsyncLifetime pattern with all Docker defensive programming patterns:
/// - Verified image tag (ollama/ollama:latest)
/// - Defensive wait strategy with HTTP port availability check
/// - Console logging via TestContext for collection fixtures
/// - Proper lifecycle management (StartAsync, StopAsync, DisposeAsync)
/// - Fail-hard when Docker unavailable (no graceful degradation)
/// - Model caching support via host volume mount
/// </summary>
public sealed class OllamaContainerFixture : ContainerFixtureBase<IContainer>
{
    private const string OllamaImage = "ollama/ollama:latest";  // ✅ Verified image tag
    private const int OllamaHttpPort = 11434;

    /// <summary>
    /// The embedding model name used for text vectorization.
    /// </summary>
    public const string EmbeddingModel = "nomic-embed-text";

    /// <summary>
    /// The large language model name used for chat completions.
    /// </summary>
    public const string LLMModel = "llama3.2:3b";

    /// <summary>
    /// The dimensionality of embeddings produced by the embedding model.
    /// </summary>
    public const int EmbeddingDimensions = 768;

    private readonly string _hostModelsPath;

    /// <summary>
    /// Gets the Ollama container hostname.
    /// </summary>
    public override string Hostname => Container?.Hostname ?? "localhost";

    /// <summary>
    /// Gets the mapped HTTP port for Ollama API.
    /// </summary>
    public override int Port => Container?.GetMappedPublicPort(OllamaHttpPort) ?? OllamaHttpPort;

    /// <summary>
    /// Gets the Ollama base URL.
    /// </summary>
    public override string ConnectionString { get; protected set; } = string.Empty;

    /// <summary>
    /// Gets the base URL for the running Ollama container.
    /// </summary>
    public string BaseUrl => ConnectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaContainerFixture"/> class.
    /// Uses TestContext.Current.SendMessage() for logging container lifecycle events.
    /// Resolves host model cache path for performance.
    /// </summary>
    public OllamaContainerFixture()
    {
        _hostModelsPath = ResolveOrCreateHostModelPath();
        LogMessage($"📁 Using host Ollama model cache at: {_hostModelsPath}");
    }

    /// <summary>
    /// Builds the Ollama container with defensive programming patterns.
    /// </summary>
    /// <returns>A configured Ollama container ready to start.</returns>
    protected override Task<IContainer> BuildContainerAsync()
    {
        var container = new ContainerBuilder()
            .WithImage(OllamaImage)  // ✅ Verified image tag
            .WithPortBinding(OllamaHttpPort, true)
            .WithEnvironment("OLLAMA_HOST", "0.0.0.0:11434")
            .WithEnvironment("OLLAMA_ORIGINS", "*")
            .WithBindMount(_hostModelsPath, "/root/.ollama/models")  // Model caching
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPort(OllamaHttpPort)
                    .ForPath("/api/tags")
                    .ForStatusCode(System.Net.HttpStatusCode.OK)))  // ✅ Defensive wait strategy
            .WithAutoRemove(true)
            .WithCleanUp(true)
            .Build();

        return Task.FromResult(container);
    }

    /// <summary>
    /// Configures connection string after successful container start.
    /// Pulls required models (embedding and LLM) into the container.
    /// </summary>
    /// <returns>A Task representing the asynchronous configuration operation.</returns>
    protected override async Task ConfigureConnectionAsync()
    {
        if (Container == null)
        {
            throw new InvalidOperationException("Container is not initialized");
        }

        ConnectionString = $"http://{Hostname}:{Port}";

        LogMessage($"✅ Ollama service available at: {BaseUrl}");

        // Pull required models into the container
        await PullRequiredModelsAsync(Xunit.TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Gets the container type name for logging.
    /// </summary>
    /// <returns>The container type name.</returns>
    protected override string GetContainerTypeName() => "Ollama";

    /// <summary>
    /// Resolves or creates the host model cache path for Ollama models.
    /// Uses the local application data folder to store cached models for performance.
    /// </summary>
    /// <returns>The absolute path to the host model cache directory.</returns>
    private static string ResolveOrCreateHostModelPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var modelsPath = Path.Combine(localAppData, "Ollama", "models");
        Directory.CreateDirectory(modelsPath);
        return modelsPath;
    }

    /// <summary>
    /// Pulls the required models (embedding and LLM) into the Ollama container.
    /// This method executes ollama pull commands inside the container for both models.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the model pulling operation.</param>
    /// <returns>A task representing the asynchronous model pulling operation.</returns>
    public async Task PullRequiredModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            LogMessage("📥 Pulling required models...");

            // Pull embedding model
            LogMessage($"  Pulling embedding model: {EmbeddingModel}");
            var pullEmbeddingResult = await Container!.ExecAsync(
                new[] { "ollama", "pull", EmbeddingModel },
                cancellationToken);

            // Fail-fast on model pull failure - tests cannot proceed without models
            if (pullEmbeddingResult.ExitCode != 0)
            {
                LogMessage($"❌ FATAL: Failed to pull embedding model: {pullEmbeddingResult.Stderr}");
                throw new InvalidOperationException(
                    $"Failed to pull embedding model '{EmbeddingModel}'. Exit code: {pullEmbeddingResult.ExitCode}. " +
                    $"Error: {pullEmbeddingResult.Stderr}. " +
                    $"Stdout: {pullEmbeddingResult.Stdout}");
            }
            LogMessage("  ✅ Embedding model pulled successfully");

            // Pull LLM model
            LogMessage($"  Pulling LLM model: {LLMModel}");
            var pullLLMResult = await Container.ExecAsync(
                new[] { "ollama", "pull", LLMModel },
                cancellationToken);

            // Fail-fast on model pull failure
            if (pullLLMResult.ExitCode != 0)
            {
                LogMessage($"❌ FATAL: Failed to pull LLM model: {pullLLMResult.Stderr}");
                throw new InvalidOperationException(
                    $"Failed to pull LLM model '{LLMModel}'. Exit code: {pullLLMResult.ExitCode}. " +
                    $"Error: {pullLLMResult.Stderr}. " +
                    $"Stdout: {pullLLMResult.Stdout}");
            }
            LogMessage("  ✅ LLM model pulled successfully");

            LogMessage("✅ Model pulling completed - All models available");
        }
        catch (OperationCanceledException)
        {
            LogMessage("❌ FATAL: Model pulling cancelled");
            throw;
        }
        catch (Exception ex)
        {
            LogMessage(ex, "❌ FATAL: Error during model pulling - Container fixture cannot proceed");
            throw;
        }
    }

    /// <summary>
    /// Checks if the Ollama service is healthy and responsive by calling the /api/tags endpoint.
    /// </summary>
    /// <returns>A task containing true if the service is healthy; otherwise, false.</returns>
    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromMilliseconds(120_000)
            };

            var response = await httpClient.GetAsync("/api/tags",
                cancellationToken: Xunit.TestContext.Current.CancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            LogMessage($"Health check failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Verifies that the required models (embedding and LLM) are available in the Ollama instance.
    /// Calls the /api/tags endpoint and checks for the presence of both required models.
    /// This checks if models are downloaded to disk, not if they're loaded into memory.
    /// </summary>
    /// <returns>A task containing true if both required models are available; otherwise, false.</returns>
    public async Task<bool> VerifyModelsAsync()
    {
        try
        {
            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromMilliseconds(120_000)
            };

            var response = await httpClient.GetAsync("/api/tags",
                cancellationToken: Xunit.TestContext.Current.CancellationToken);
            if (!response.IsSuccessStatusCode)
                return false;

            var content = await response.Content.ReadAsStringAsync();
            var modelsResponse = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(content);

            if (modelsResponse == null)
                return false;

            var models = modelsResponse["models"]?.AsArray();
            if (models == null)
                return false;

            var availableModels = models
                .Select(m => m?["name"]?.ToString())
                .Where(m => m != null)
                .ToList();

            // Match models with flexible version tag handling
            var hasEmbeddingModel = availableModels.Any(m =>
                m!.Split(':')[0].Equals(EmbeddingModel.Split(':')[0], StringComparison.OrdinalIgnoreCase));
            var hasLLMModel = availableModels.Any(m =>
                m!.Split(':')[0].Equals(LLMModel.Split(':')[0], StringComparison.OrdinalIgnoreCase));

            LogMessage($"Available models: {string.Join(", ", availableModels)}");

            return hasEmbeddingModel && hasLLMModel;
        }
        catch (Exception ex)
        {
            LogMessage($"Model verification failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Ensures models are loaded into memory by making a lightweight API call to each model.
    /// This warms up the models so subsequent operations don't timeout.
    /// Should be called after VerifyModelsAsync() confirms models are downloaded.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing true if models loaded successfully; otherwise, false.</returns>
    public async Task<bool> EnsureModelsLoadedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            LogMessage("🔥 Warming up models - loading into memory...");

            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromMilliseconds(600_000) // 10 minutes for loading models into memory
            };

            // Load embedding model into memory with a simple embedding request
            LogMessage($"  Loading {EmbeddingModel} into memory...");
            var embeddingRequest = new
            {
                model = EmbeddingModel,
                prompt = "warmup"
            };

            var embeddingContent = new System.Net.Http.StringContent(
                System.Text.Json.JsonSerializer.Serialize(embeddingRequest),
                System.Text.Encoding.UTF8,
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));

            var embeddingResponse = await httpClient.PostAsync("/api/embeddings", embeddingContent, cancellationToken);
            if (!embeddingResponse.IsSuccessStatusCode)
            {
                LogMessage($"  ❌ Failed to load {EmbeddingModel}");
                return false;
            }
            LogMessage($"  ✅ {EmbeddingModel} loaded into memory");

            // Load LLM model into memory with a simple generation request
            LogMessage($"  Loading {LLMModel} into memory...");
            var llmRequest = new
            {
                model = LLMModel,
                prompt = "Hi",
                stream = false,
                options = new
                {
                    num_predict = 1 // Just 1 token to warm up
                }
            };

            var llmContent = new System.Net.Http.StringContent(
                System.Text.Json.JsonSerializer.Serialize(llmRequest),
                System.Text.Encoding.UTF8,
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));

            var llmResponse = await httpClient.PostAsync("/api/generate", llmContent, cancellationToken);
            if (!llmResponse.IsSuccessStatusCode)
            {
                LogMessage($"  ❌ Failed to load {LLMModel}");
                return false;
            }
            LogMessage($"  ✅ {LLMModel} loaded into memory");

            LogMessage("✅ All models loaded into memory and ready");
            return true;
        }
        catch (Exception ex)
        {
            LogMessage($"Model loading failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets an HTTP client configured for the Ollama API.
    /// Returns a NEW HttpClient instance for each call to avoid "already started" errors.
    /// Each client is pre-configured with the correct base URL and timeout settings.
    /// </summary>
    /// <returns>A new <see cref="HttpClient"/> instance configured for Ollama API calls.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the Ollama container is not available.</exception>
    public HttpClient GetHttpClient()
    {
        EnsureAvailable();

        // Return a new HttpClient instance for each call to avoid HttpClient lifecycle issues
        // 5 minute timeout for Ollama operations (model loading into memory can be slow)
        return new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromMilliseconds(300_000)
        };
    }
}
