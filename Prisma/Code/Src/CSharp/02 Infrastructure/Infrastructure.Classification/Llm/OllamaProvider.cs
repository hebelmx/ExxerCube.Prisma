using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Llm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Infrastructure.Classification.Llm;

/// <summary>
/// LLM provider adapter for the local Ollama server.
/// Uses the <c>POST /api/generate</c> endpoint; supports both text and vision (same endpoint,
/// images encoded as base-64 strings in the request body).
/// </summary>
public sealed class OllamaProvider : ILlmProvider
{
    /// <summary>Named HttpClient key used when registering via <c>AddHttpClient</c>.</summary>
    public const string HttpClientName = "OllamaProvider";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<LlmProvidersOptions> _options;
    private readonly ILogger<OllamaProvider> _logger;

    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web) { WriteIndented = false };

    /// <summary>
    /// Initializes a new instance of <see cref="OllamaProvider"/>.
    /// </summary>
    /// <param name="httpClientFactory">Factory for creating named <see cref="HttpClient"/> instances.</param>
    /// <param name="options">Live-reloadable provider options.</param>
    /// <param name="logger">Structured logger.</param>
    public OllamaProvider(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<LlmProvidersOptions> options,
        ILogger<OllamaProvider> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string Name => "Ollama";

    /// <inheritdoc />
    public LlmCapabilities Capabilities => LlmCapabilities.TextGenerate | LlmCapabilities.VisionGenerate;

    /// <inheritdoc />
    public async Task<Result<string>> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<string>();
        }

        if (request is null)
        {
            return Result<string>.WithFailure("LlmRequest cannot be null.");
        }

        var opts = _options.CurrentValue.Ollama;
        var hasImages = request.Images is { Count: > 0 };
        var model = request.ModelOverride
            ?? (hasImages ? opts.VisionModel : opts.Model);

        try
        {
            var fullPrompt = $"{request.SystemPrompt}\n\n{request.UserPrompt}";

            object body;
            if (hasImages)
            {
                var encodedImages = request.Images!
                    .Select(img => Convert.ToBase64String(img))
                    .ToList();

                body = new
                {
                    model,
                    prompt = fullPrompt,
                    format = "json",
                    stream = false,
                    options = new { temperature = 0 },
                    images = encodedImages,
                };
            }
            else
            {
                body = new
                {
                    model,
                    prompt = fullPrompt,
                    format = "json",
                    stream = false,
                    options = new { temperature = 0 },
                };
            }

            var json = JsonSerializer.Serialize(body, JsonOpts);
            using var content = new StringContent(json, Encoding.UTF8,
                new MediaTypeHeaderValue("application/json"));

            var baseUrl = opts.BaseUrl.TrimEnd('/');
            var requestUri = new Uri($"{baseUrl}/api/generate");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(opts.TimeoutSeconds));

            _logger.LogDebug(
                "OllamaProvider: POST {Uri} model={Model} hasImages={HasImages}",
                requestUri, model, hasImages);

            using var http = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await http.PostAsync(requestUri, content, cts.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content
                    .ReadAsStringAsync(CancellationToken.None)
                    .ConfigureAwait(false);

                _logger.LogWarning(
                    "OllamaProvider: HTTP {Status} from {Uri}. Body: {Body}",
                    (int)response.StatusCode, requestUri, errorBody);

                return Result<string>.WithFailure(
                    $"Ollama returned HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            }

            var responseJson = await response.Content
                .ReadAsStringAsync(CancellationToken.None)
                .ConfigureAwait(false);

            var node = JsonNode.Parse(responseJson);
            var text = node?["response"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning(
                    "OllamaProvider: 'response' field missing or empty. Raw: {Raw}", responseJson);
                return Result<string>.WithFailure("Ollama response was empty.");
            }

            var trimmed = text.Trim();
            _logger.LogDebug("OllamaProvider: response received ({Length} chars).", trimmed.Length);
            return Result<string>.WithSuccess(trimmed);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "OllamaProvider: request timed out or was cancelled.");
            return ResultExtensions.Cancelled<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OllamaProvider: unexpected error — {Message}", ex.Message);
            return Result<string>.WithFailure($"Ollama generate failed: {ex.Message}");
        }
    }
}
