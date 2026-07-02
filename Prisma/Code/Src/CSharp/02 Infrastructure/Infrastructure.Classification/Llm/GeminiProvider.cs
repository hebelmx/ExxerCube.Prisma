using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExxerCube.Prisma.Domain.Llm;

namespace ExxerCube.Prisma.Infrastructure.Classification.Llm;

/// <summary>
/// LLM provider adapter for the Google Gemini REST API.
/// Supports both text and vision requests (images encoded as <c>inline_data</c> parts).
/// Ships DARK — registered as a second <see cref="ILlmProvider"/> so the factory sees it,
/// but the active provider remains Ollama until configuration is changed.
/// </summary>
public sealed class GeminiProvider : ILlmProvider
{
    /// <summary>Named HttpClient key used when registering via <c>AddHttpClient</c>.</summary>
    public const string HttpClientName = "GeminiProvider";

    private const string GenerateContentBaseUrl =
        "https://generativelanguage.googleapis.com/v1beta/models/";

    private readonly ISecretProvider _secretProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<LlmProvidersOptions> _options;
    private readonly ILogger<GeminiProvider> _logger;

    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web) { WriteIndented = false };

    /// <summary>
    /// Initializes a new instance of <see cref="GeminiProvider"/>.
    /// </summary>
    /// <param name="secretProvider">Resolves the API key from configuration at call time.</param>
    /// <param name="httpClientFactory">Factory for named <see cref="HttpClient"/> instances.</param>
    /// <param name="options">Live-reloadable provider options.</param>
    /// <param name="logger">Structured logger.  The API key is NEVER logged.</param>
    public GeminiProvider(
        ISecretProvider secretProvider,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<LlmProvidersOptions> options,
        ILogger<GeminiProvider> logger)
    {
        _secretProvider = secretProvider ?? throw new ArgumentNullException(nameof(secretProvider));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string Name => "Gemini";

    /// <inheritdoc />
    public LlmCapabilities Capabilities =>
        LlmCapabilities.TextGenerate | LlmCapabilities.VisionGenerate | LlmCapabilities.StructuredOutput;

    /// <inheritdoc />
    public async Task<Result<string>> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<string>();

        if (request is null)
            return Result<string>.WithFailure("LlmRequest cannot be null.");

        var opts = _options.CurrentValue.Gemini;

        // Resolve the API key — never log it.
        var keyResult = await _secretProvider
            .GetSecretAsync(opts.ApiKeyRef, cancellationToken)
            .ConfigureAwait(false);

        if (!keyResult.IsSuccess)
        {
            _logger.LogWarning(
                "GeminiProvider: API key could not be resolved from key reference '{KeyRef}'.",
                opts.ApiKeyRef);
            return Result<string>.WithFailure("Gemini API key not configured.");
        }

        var model = request.ModelOverride ?? opts.Model;
        var hasImages = request.Images is { Count: > 0 };

        try
        {
            // Build the parts array: [text, (optional images…)]
            var parts = new List<object>
            {
                new { text = $"{request.SystemPrompt}\n\n{request.UserPrompt}" },
            };

            if (hasImages)
            {
                foreach (var img in request.Images!)
                {
                    parts.Add(new
                    {
                        inline_data = new
                        {
                            mime_type = "image/png",
                            data = Convert.ToBase64String(img),
                        },
                    });
                }
            }

            var body = new
            {
                contents = new[]
                {
                    new { parts = parts.ToArray() },
                },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    temperature = 0,
                    // TODO: add responseSchema when structured-output spec is finalized (S3).
                },
            };

            var json = JsonSerializer.Serialize(body, JsonOpts);
            using var content = new StringContent(json, Encoding.UTF8,
                new MediaTypeHeaderValue("application/json"));

            var requestUri = new Uri(
                $"{GenerateContentBaseUrl}{Uri.EscapeDataString(model)}:generateContent");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(opts.TimeoutSeconds));

            _logger.LogDebug(
                "GeminiProvider: POST {Uri} model={Model} hasImages={HasImages}",
                requestUri, model, hasImages);

            using var http = _httpClientFactory.CreateClient(HttpClientName);

            // Set the API key per-request (NOT on http.DefaultRequestHeaders): the named client is
            // pooled/shared, so mutating DefaultRequestHeaders races across concurrent requests.
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri) { Content = content };
            httpRequest.Headers.Add("x-goog-api-key", keyResult.Value!);

            using var response = await http
                .SendAsync(httpRequest, cts.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content
                    .ReadAsStringAsync(CancellationToken.None)
                    .ConfigureAwait(false);

                _logger.LogWarning(
                    "GeminiProvider: HTTP {Status} from {Uri}. Body: {Body}",
                    (int)response.StatusCode, requestUri, errorBody);

                return Result<string>.WithFailure(
                    $"Gemini returned HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            }

            var responseJson = await response.Content
                .ReadAsStringAsync(CancellationToken.None)
                .ConfigureAwait(false);

            // Parse: candidates[0].content.parts[0].text
            var node = JsonNode.Parse(responseJson);
            var text = node?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning(
                    "GeminiProvider: could not parse text from response. Raw: {Raw}", responseJson);
                return Result<string>.WithFailure("Gemini response text was empty or missing.");
            }

            var trimmed = text.Trim();
            _logger.LogDebug("GeminiProvider: response received ({Length} chars).", trimmed.Length);
            return Result<string>.WithSuccess(trimmed);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "GeminiProvider: request timed out or was cancelled.");
            return ResultExtensions.Cancelled<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GeminiProvider: unexpected error — {Message}", ex.Message);
            return Result<string>.WithFailure($"Gemini generate failed: {ex.Message}");
        }
    }
}
