using ExxerCube.Prisma.Domain.Llm;
using ExxerCube.Prisma.Infrastructure.Classification.Llm;
using ExxerCube.Prisma.Infrastructure.Extraction.Txt.Llm;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Txt;

/// <summary>
/// Unit tests for <see cref="LlmTxtFieldExtractor"/>.
/// All LLM calls are replaced with mocked <see cref="ILlmProviderFactory"/> / <see cref="ILlmProvider"/>
/// returning canned JSON — no real network calls.
/// </summary>
public sealed class LlmTxtFieldExtractorTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<LlmTxtFieldExtractor> _logger;
    private readonly IOptions<LlmProvidersOptions> _defaultOptions;

    public LlmTxtFieldExtractorTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = XUnitLogger.CreateLogger<LlmTxtFieldExtractor>(output);
        _defaultOptions = Options.Create(new LlmProvidersOptions());
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static ILlmProviderFactory MakeFactory(string json)
    {
        var provider = Substitute.For<ILlmProvider>();
        provider.Name.Returns("MockLlm");
        provider.GenerateAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string>.WithSuccess(json)));

        var factory = Substitute.For<ILlmProviderFactory>();
        factory.GetActive().Returns(provider);
        return factory;
    }

    private static ILlmProviderFactory MakeFailingFactory(string errorMessage)
    {
        var provider = Substitute.For<ILlmProvider>();
        provider.Name.Returns("MockLlm");
        provider.GenerateAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string>.WithFailure(errorMessage)));

        var factory = Substitute.For<ILlmProviderFactory>();
        factory.GetActive().Returns(provider);
        return factory;
    }

    // -----------------------------------------------------------------------
    // Happy path
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFieldsAsync_HappyPath_ReturnsPopulatedExpediente()
    {
        // Arrange — canned JSON with all main fields
        const string json = """
            {
              "expediente": "123/2024",
              "solicitante": "Juan Pérez García",
              "monto": "5000.00",
              "cuenta": "1234567890",
              "rfc": null,
              "curp": null,
              "partes": [
                { "nombre": "María López", "rfc": "LOPM800101AAA", "curp": null, "fechaNacimiento": null, "caracter": "Contribuyente" }
              ]
            }
            """;

        var factory = MakeFactory(json);
        var extractor = new LlmTxtFieldExtractor(factory, _defaultOptions, _logger);
        var source = new TxtSource("Texto OCR de prueba del documento legal.", ocrConfidence: 0.90f);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await extractor.ExtractFieldsAsync(source, []);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Expediente.ShouldBe("123/2024");
        result.Value.AdditionalFields["NombreSolicitante"].ShouldBe("Juan Pérez García");
        result.Value.AdditionalFields["_ExtractionSource"].ShouldBe("llm-text");
        result.Value.AdditionalFields.ContainsKey("_OcrConfidence").ShouldBeTrue();
        result.Value.AdditionalFields.ContainsKey("_OcrText").ShouldBeTrue();
    }

    [Fact]
    public async Task ExtractFieldsAsync_HappyPath_PartesMapped()
    {
        const string json = """
            {
              "expediente": "456/2023",
              "solicitante": null,
              "monto": null,
              "cuenta": null,
              "rfc": null,
              "curp": null,
              "partes": [
                { "nombre": "Carlos Ruiz", "rfc": null, "curp": null, "fechaNacimiento": null, "caracter": "Patrón" }
              ]
            }
            """;

        var factory = MakeFactory(json);
        var extractor = new LlmTxtFieldExtractor(factory, _defaultOptions, _logger);
        var source = new TxtSource("Texto de prueba");

        var result = await extractor.ExtractFieldsAsync(source, []);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Expediente.ShouldBe("456/2023");
    }

    // -----------------------------------------------------------------------
    // Gate rejection
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFieldsAsync_GateRejectsAllNull_ReturnsFailure()
    {
        // All-null DTO — gate should reject
        const string json = """{"expediente":null,"solicitante":null,"monto":null,"cuenta":null,"rfc":null,"curp":null,"partes":null}""";

        var factory = MakeFactory(json);
        var extractor = new LlmTxtFieldExtractor(factory, _defaultOptions, _logger);
        var source = new TxtSource("Texto.");

        var result = await extractor.ExtractFieldsAsync(source, []);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Contains("Gate rejected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExtractFieldsAsync_GateRejectsBadExpediente_ReturnsFailure()
    {
        // Expediente in old format (not ddd/yyyy) — gate should reject
        const string json = """
            {
              "expediente": "A/AS1-2505-088637-PHM",
              "solicitante": "Test",
              "monto": null,
              "cuenta": null,
              "rfc": null,
              "curp": null,
              "partes": null
            }
            """;

        var factory = MakeFactory(json);
        var extractor = new LlmTxtFieldExtractor(factory, _defaultOptions, _logger);
        var source = new TxtSource("Texto.");

        var result = await extractor.ExtractFieldsAsync(source, []);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Contains("Gate rejected", StringComparison.OrdinalIgnoreCase));
    }

    // -----------------------------------------------------------------------
    // Provider failure
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFieldsAsync_ProviderFailure_ReturnsFailure()
    {
        var factory = MakeFailingFactory("Timeout connecting to Ollama");
        var extractor = new LlmTxtFieldExtractor(factory, _defaultOptions, _logger);
        var source = new TxtSource("Texto.");

        var result = await extractor.ExtractFieldsAsync(source, []);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Contains("LLM provider failed", StringComparison.OrdinalIgnoreCase));
    }

    // -----------------------------------------------------------------------
    // Malformed JSON
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFieldsAsync_MalformedJson_ReturnsFailure()
    {
        var factory = MakeFactory("{ this is not valid JSON }}}");
        var extractor = new LlmTxtFieldExtractor(factory, _defaultOptions, _logger);
        var source = new TxtSource("Texto.");

        var result = await extractor.ExtractFieldsAsync(source, []);

        result.IsSuccess.ShouldBeFalse();
    }

    // -----------------------------------------------------------------------
    // Null / empty source guards
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFieldsAsync_NullSource_ReturnsFailure()
    {
        var factory = MakeFactory("{}");
        var extractor = new LlmTxtFieldExtractor(factory, _defaultOptions, _logger);

        var result = await extractor.ExtractFieldsAsync(null!, []);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task ExtractFieldsAsync_EmptyText_ReturnsFailure()
    {
        var factory = MakeFactory("{}");
        var extractor = new LlmTxtFieldExtractor(factory, _defaultOptions, _logger);
        var source = new TxtSource(string.Empty);

        var result = await extractor.ExtractFieldsAsync(source, []);

        result.IsSuccess.ShouldBeFalse();
    }

    // -----------------------------------------------------------------------
    // ExtractFieldAsync — not supported
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFieldAsync_AlwaysReturnsFailure()
    {
        var factory = MakeFactory("{}");
        var extractor = new LlmTxtFieldExtractor(factory, _defaultOptions, _logger);
        var source = new TxtSource("Texto.");

        var result = await extractor.ExtractFieldAsync(source, "Expediente");

        result.IsSuccess.ShouldBeFalse();
    }

    // Adversarial review F2: a misconfigured active provider makes GetActive() THROW —
    // the extractor must convert it to WithFailure, never propagate (IFieldExtractor is no-throw).
    [Fact]
    public async Task ExtractFieldsAsync_ProviderResolutionThrows_ReturnsFailure()
    {
        var factory = Substitute.For<ILlmProviderFactory>();
        factory.GetActive().Returns(_ => throw new InvalidOperationException("misconfigured active provider"));
        var extractor = new LlmTxtFieldExtractor(factory, _defaultOptions, _logger);

        var result = await extractor.ExtractFieldsAsync(new TxtSource("Texto del oficio."), []);

        result.IsSuccess.ShouldBeFalse();
    }
}
