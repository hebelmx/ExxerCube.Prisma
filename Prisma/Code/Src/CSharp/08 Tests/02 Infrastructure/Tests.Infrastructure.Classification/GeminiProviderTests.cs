using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using ExxerCube.Prisma.Domain.Llm;
using ExxerCube.Prisma.Infrastructure.Classification.Llm;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Unit tests for <see cref="GeminiProvider"/>.
/// All HTTP calls are intercepted by a <see cref="FakeHttpMessageHandler"/> — no network traffic.
/// </summary>
public sealed class GeminiProviderTests
{
    // -----------------------------------------------------------------------
    // Infrastructure helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Minimal in-process HTTP handler that returns a pre-canned <see cref="HttpResponseMessage"/>.
    /// </summary>
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }

    /// <summary>Canned Gemini generateContent JSON for a single text part.</summary>
    private static string MakeGeminiResponse(string innerJson) =>
        $$$"""
        {
          "candidates": [{
            "content": {
              "parts": [{"text": {{{System.Text.Json.JsonSerializer.Serialize(innerJson)}}} }]
            }
          }]
        }
        """;

    private const string ValidInnerJson =
        """{"expediente":"123/2024","solicitante":"Juan García","monto":null,"cuenta":null,"rfc":null,"curp":null,"partes":[]}""";

    private static IOptionsMonitor<LlmProvidersOptions> MakeOptions(
        string model = "gemini-2.0-flash",
        string apiKeyRef = "Gemini:ApiKey",
        int timeout = 60)
    {
        var opts = new LlmProvidersOptions
        {
            Gemini = new GeminiProviderOptions
            {
                Model = model,
                ApiKeyRef = apiKeyRef,
                TimeoutSeconds = timeout,
            },
        };
        var monitor = Substitute.For<IOptionsMonitor<LlmProvidersOptions>>();
        monitor.CurrentValue.Returns(opts);
        return monitor;
    }

    private static ISecretProvider MakeSecretProvider(string? key = "fake-api-key")
    {
        var sp = Substitute.For<ISecretProvider>();
        if (key is not null)
        {
            sp.GetSecretAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result<string>.WithSuccess(key)));
        }
        else
        {
            sp.GetSecretAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result<string>.WithFailure("Secret not configured.")));
        }
        return sp;
    }

    private static IHttpClientFactory MakeHttpClientFactory(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(GeminiProvider.HttpClientName).Returns(httpClient);
        return factory;
    }

    private GeminiProvider BuildProvider(
        ISecretProvider? secretProvider = null,
        IHttpClientFactory? httpClientFactory = null,
        IOptionsMonitor<LlmProvidersOptions>? options = null,
        ITestOutputHelper? output = null)
    {
        var logger = XUnitLogger.CreateLogger<GeminiProvider>(output!);
        return new GeminiProvider(
            secretProvider ?? MakeSecretProvider(),
            httpClientFactory ?? MakeHttpClientFactory(
                new FakeHttpMessageHandler(_ =>
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            MakeGeminiResponse(ValidInnerJson),
                            Encoding.UTF8,
                            "application/json"),
                    })),
            options ?? MakeOptions(),
            logger);
    }

    // -----------------------------------------------------------------------
    // Happy path — valid key + HTTP 200 → inner JSON returned
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_ValidKeyAndHttp200_ReturnsInnerText()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var response = MakeGeminiResponse(ValidInnerJson);
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            });

        var provider = BuildProvider(httpClientFactory: MakeHttpClientFactory(handler));
        var request = new LlmRequest("You are an extractor.", "Extract fields.");

        // Act
        var result = await provider.GenerateAsync(request, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNullOrWhiteSpace();
        result.Value!.ShouldContain("123/2024"); // inner JSON text should be the returned string
    }

    [Fact]
    public async Task GenerateAsync_WithImages_SendsInlineDataParts()
    {
        // Arrange — capture the request body inside the handler (before it is disposed).
        var ct = TestContext.Current.CancellationToken;
        string? capturedBody = null;
        var handler = new FakeHttpMessageHandler(req =>
        {
            // Read synchronously via GetAwaiter so we can inspect before disposal.
            capturedBody = req.Content?.ReadAsStringAsync(CancellationToken.None).GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    MakeGeminiResponse(ValidInnerJson), Encoding.UTF8, "application/json"),
            };
        });

        var provider = BuildProvider(httpClientFactory: MakeHttpClientFactory(handler));
        var fakeImage = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG magic bytes
        var request = new LlmRequest("system", "user", Images: new[] { fakeImage });

        // Act
        var result = await provider.GenerateAsync(request, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        capturedBody.ShouldNotBeNullOrWhiteSpace();
        capturedBody!.ShouldContain("inline_data"); // image part must be present
        capturedBody.ShouldContain("image/png");
    }

    // -----------------------------------------------------------------------
    // Key missing → failure (no network call attempted)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_KeyMissing_ReturnsFailureWithoutCallingHttp()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var httpCallMade = false;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            httpCallMade = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var provider = BuildProvider(
            secretProvider: MakeSecretProvider(key: null), // no key
            httpClientFactory: MakeHttpClientFactory(handler));

        var request = new LlmRequest("system", "user");

        // Act
        var result = await provider.GenerateAsync(request, ct);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors!.ShouldContain(e => e.Contains("API key", StringComparison.OrdinalIgnoreCase));
        httpCallMade.ShouldBeFalse(); // no HTTP call when key resolution fails
    }

    // -----------------------------------------------------------------------
    // HTTP error → failure result (never throw)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_Http500_ReturnsFailure()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                ReasonPhrase = "Internal Server Error",
                Content = new StringContent("{\"error\":\"boom\"}", Encoding.UTF8, "application/json"),
            });

        var provider = BuildProvider(httpClientFactory: MakeHttpClientFactory(handler));
        var request = new LlmRequest("system", "user");

        // Act
        var result = await provider.GenerateAsync(request, ct);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors!.ShouldContain(e => e.Contains("500", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GenerateAsync_Http403_ReturnsFailure()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                ReasonPhrase = "Forbidden",
                Content = new StringContent("{\"error\":\"unauthorized\"}", Encoding.UTF8, "application/json"),
            });

        var provider = BuildProvider(httpClientFactory: MakeHttpClientFactory(handler));
        var request = new LlmRequest("system", "user");

        // Act
        var result = await provider.GenerateAsync(request, ct);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors!.ShouldContain(e => e.Contains("403", StringComparison.Ordinal));
    }

    // -----------------------------------------------------------------------
    // Null request guard
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_NullRequest_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = BuildProvider();

        var result = await provider.GenerateAsync(null!, ct);

        result.IsSuccess.ShouldBeFalse();
    }

    // -----------------------------------------------------------------------
    // Capabilities / Name
    // -----------------------------------------------------------------------

    [Fact]
    public void Name_IsGemini()
    {
        BuildProvider().Name.ShouldBe("Gemini");
    }

    [Fact]
    public void Capabilities_IncludesTextVisionAndStructuredOutput()
    {
        var caps = BuildProvider().Capabilities;
        caps.HasFlag(LlmCapabilities.TextGenerate).ShouldBeTrue();
        caps.HasFlag(LlmCapabilities.VisionGenerate).ShouldBeTrue();
        caps.HasFlag(LlmCapabilities.StructuredOutput).ShouldBeTrue();
    }
}
