using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Interfaces;
using IndQuestResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Infrastructure.Classification;

/// <summary>
/// Ollama HTTP adapter: <c>POST {endpoint}/api/generate</c> with JSON body
/// <c>{model, prompt, stream:false, options:{temperature:0}}</c>, parses the <c>response</c> field.
/// All HTTP, timeout, and JSON-parse errors return a failure result (fail-open).
/// </summary>
/// <remarks>
/// Registered as a typed <c>HttpClient</c> via <c>services.AddHttpClient&lt;OllamaHttpClient&gt;()</c>.
/// The injected <see cref="HttpClient"/> base address and timeout are overridden per call from
/// <see cref="OllamaOptions"/> so config changes take effect without restarting the host.
/// </remarks>
public sealed class OllamaHttpClient : IOllamaClient
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaHttpClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = false };

    /// <summary>
    /// Initializes the client with a typed <see cref="HttpClient"/> from DI and the current options.
    /// </summary>
    /// <param name="http">Typed HttpClient provided by the AddHttpClient registration.</param>
    /// <param name="options">Bound Ollama options (endpoint, model, timeout).</param>
    /// <param name="logger">Logger for structured diagnostics.</param>
    public OllamaHttpClient(
        HttpClient http,
        IOptions<OllamaOptions> options,
        ILogger<OllamaHttpClient> logger)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<string>> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<string>();
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Result<string>.WithFailure("Prompt cannot be empty.");
        }

        try
        {
            var requestBody = new
            {
                model = _options.Model,
                prompt,
                stream = false,
                options = new { temperature = 0 }
            };

            var json = JsonSerializer.Serialize(requestBody, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

            // Override base address from options so config changes are honoured at call time.
            var endpoint = _options.Endpoint.TrimEnd('/');
            var requestUri = new Uri($"{endpoint}/api/generate");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            _logger.LogDebug(
                "Sending Ollama generate request to {Endpoint} with model {Model}",
                requestUri, _options.Model);

            using var response = await _http.PostAsync(requestUri, content, cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(CancellationToken.None).ConfigureAwait(false);
                _logger.LogWarning(
                    "Ollama returned HTTP {Status}: {Body}",
                    (int)response.StatusCode, body);
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
                _logger.LogWarning("Ollama response JSON missing 'response' field or empty. Raw: {Raw}", responseJson);
                return Result<string>.WithFailure("Ollama response was empty.");
            }

            var trimmed = text.Trim();
            _logger.LogDebug("Ollama generate succeeded ({Length} chars).", trimmed.Length);
            return Result<string>.WithSuccess(trimmed);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Ollama generate request timed out or was cancelled.");
            return ResultExtensions.Cancelled<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama generate request failed: {Message}", ex.Message);
            return Result<string>.WithFailure($"Ollama generate failed: {ex.Message}");
        }
    }
}
