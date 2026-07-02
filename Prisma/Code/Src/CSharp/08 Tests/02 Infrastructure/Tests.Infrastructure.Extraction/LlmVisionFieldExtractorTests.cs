using ExxerCube.Prisma.Domain.Llm;
using ExxerCube.Prisma.Domain.Sources;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Llm;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Unit tests for <see cref="LlmVisionFieldExtractor"/>.
/// All LLM calls are mocked via <see cref="ILlmProviderFactory"/> — no real network calls.
/// </summary>
public sealed class LlmVisionFieldExtractorTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>Fake PNG bytes (just the PNG magic number — content irrelevant for mocked providers).</summary>
    private static readonly byte[] FakePng = { 0x89, 0x50, 0x4E, 0x47 };

    private static readonly ImageSource OnePageSource =
        new("test-doc-001", new[] { FakePng });

    private static ILlmProviderFactory MakeVisionFactory(
        string jsonResponse,
        LlmCapabilities caps = LlmCapabilities.TextGenerate | LlmCapabilities.VisionGenerate)
    {
        var provider = Substitute.For<ILlmProvider>();
        provider.Name.Returns("MockVision");
        provider.Capabilities.Returns(caps);
        provider.GenerateAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string>.WithSuccess(jsonResponse)));

        var factory = Substitute.For<ILlmProviderFactory>();
        factory.GetActive().Returns(provider);
        return factory;
    }

    private static ILlmProviderFactory MakeTextOnlyFactory()
    {
        var provider = Substitute.For<ILlmProvider>();
        provider.Name.Returns("MockTextOnly");
        provider.Capabilities.Returns(LlmCapabilities.TextGenerate); // no VisionGenerate
        var factory = Substitute.For<ILlmProviderFactory>();
        factory.GetActive().Returns(provider);
        return factory;
    }

    private static ILlmProviderFactory MakeFailingFactory(string error)
    {
        var provider = Substitute.For<ILlmProvider>();
        provider.Name.Returns("MockFailing");
        provider.Capabilities.Returns(LlmCapabilities.TextGenerate | LlmCapabilities.VisionGenerate);
        provider.GenerateAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string>.WithFailure(error)));
        var factory = Substitute.For<ILlmProviderFactory>();
        factory.GetActive().Returns(provider);
        return factory;
    }

    private LlmVisionFieldExtractor BuildExtractor(
        ILlmProviderFactory factory,
        ITestOutputHelper? output = null)
    {
        var logger = XUnitLogger.CreateLogger<LlmVisionFieldExtractor>(output!);
        return new LlmVisionFieldExtractor(factory, logger);
    }

    // -----------------------------------------------------------------------
    // Provider capability check
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFieldsAsync_ProviderNoVision_ReturnsFailure()
    {
        // Arrange
        var extractor = BuildExtractor(MakeTextOnlyFactory());

        // Act
        var result = await extractor.ExtractFieldsAsync(OnePageSource, []);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors!.ShouldContain(e => e.Contains("VisionGenerate", StringComparison.Ordinal));
    }

    // -----------------------------------------------------------------------
    // Happy path
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFieldsAsync_HappyPath_ReturnsPopulatedFieldsWithVisionProvenance()
    {
        // Arrange
        const string json =
            """{"expediente":"456/2024","solicitante":"María López","monto":null,"cuenta":null,"rfc":null,"curp":null,"partes":[]}""";
        var ct = TestContext.Current.CancellationToken;
        var extractor = BuildExtractor(MakeVisionFactory(json));

        // Act
        var result = await extractor.ExtractFieldsAsync(OnePageSource, []);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Expediente.ShouldBe("456/2024");
        result.Value.AdditionalFields["NombreSolicitante"].ShouldBe("María López");
        result.Value.AdditionalFields["_ExtractionSource"].ShouldBe("llm-vision");
    }

    [Fact]
    public async Task ExtractFieldsAsync_HappyPath_ImagesPassedToProvider()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        LlmRequest? capturedRequest = null;
        var provider = Substitute.For<ILlmProvider>();
        provider.Name.Returns("MockVision");
        provider.Capabilities.Returns(LlmCapabilities.TextGenerate | LlmCapabilities.VisionGenerate);
        provider.GenerateAsync(Arg.Do<LlmRequest>(r => capturedRequest = r), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string>.WithSuccess(
                """{"expediente":"789/2023","solicitante":"Test","monto":null,"cuenta":null,"rfc":null,"curp":null,"partes":[]}""")));

        var factory = Substitute.For<ILlmProviderFactory>();
        factory.GetActive().Returns(provider);

        var extractor = BuildExtractor(factory);
        var source = new ImageSource("doc-xyz", new[] { FakePng, FakePng }); // 2 pages

        // Act
        await extractor.ExtractFieldsAsync(source, []);

        // Assert — provider received the images
        capturedRequest.ShouldNotBeNull();
        capturedRequest!.Images.ShouldNotBeNull();
        capturedRequest.Images!.Count.ShouldBe(2);
    }

    // -----------------------------------------------------------------------
    // Gate rejection
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFieldsAsync_GateRejectsAllNullDto_ReturnsFailure()
    {
        // All-null DTO → gate should reject
        const string json =
            """{"expediente":null,"solicitante":null,"monto":null,"cuenta":null,"rfc":null,"curp":null,"partes":null}""";
        var extractor = BuildExtractor(MakeVisionFactory(json));

        var result = await extractor.ExtractFieldsAsync(OnePageSource, []);

        result.IsSuccess.ShouldBeFalse();
        result.Errors!.ShouldContain(e => e.Contains("Gate rejected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExtractFieldsAsync_MalformedJson_ReturnsFailure()
    {
        var extractor = BuildExtractor(MakeVisionFactory("{ not valid JSON }}}"));

        var result = await extractor.ExtractFieldsAsync(OnePageSource, []);

        result.IsSuccess.ShouldBeFalse();
        result.Errors!.ShouldContain(e => e.Contains("JSON", StringComparison.OrdinalIgnoreCase));
    }

    // -----------------------------------------------------------------------
    // Provider failure
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFieldsAsync_ProviderFailure_ReturnsFailure()
    {
        var extractor = BuildExtractor(MakeFailingFactory("Vision model unavailable"));

        var result = await extractor.ExtractFieldsAsync(OnePageSource, []);

        result.IsSuccess.ShouldBeFalse();
        result.Errors!.ShouldContain(e => e.Contains("LLM provider failed", StringComparison.OrdinalIgnoreCase));
    }

    // -----------------------------------------------------------------------
    // Guard rails
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFieldsAsync_NullSource_ReturnsFailure()
    {
        var extractor = BuildExtractor(MakeVisionFactory("{}"));

        var result = await extractor.ExtractFieldsAsync(null!, []);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task ExtractFieldsAsync_EmptyPageList_ReturnsFailure()
    {
        var extractor = BuildExtractor(MakeVisionFactory("{}"));
        var emptySource = new ImageSource("doc-empty", Array.Empty<byte[]>());

        var result = await extractor.ExtractFieldsAsync(emptySource, []);

        result.IsSuccess.ShouldBeFalse();
        result.Errors!.ShouldContain(e => e.Contains("no page images", StringComparison.OrdinalIgnoreCase));
    }

    // -----------------------------------------------------------------------
    // ExtractFieldAsync — not supported
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFieldAsync_AlwaysReturnsFailure()
    {
        var extractor = BuildExtractor(MakeVisionFactory("{}"));

        var result = await extractor.ExtractFieldAsync(OnePageSource, "NumeroExpediente");

        result.IsSuccess.ShouldBeFalse();
    }

    // Adversarial review F2: a misconfigured active provider makes GetActive() THROW —
    // the extractor must convert it to WithFailure, never propagate (IFieldExtractor is no-throw).
    [Fact]
    public async Task ExtractFieldsAsync_ProviderResolutionThrows_ReturnsFailure()
    {
        var factory = Substitute.For<ILlmProviderFactory>();
        factory.GetActive().Returns(_ => throw new InvalidOperationException("misconfigured active provider"));
        var extractor = BuildExtractor(factory);

        var result = await extractor.ExtractFieldsAsync(OnePageSource, []);

        result.IsSuccess.ShouldBeFalse();
    }

    // -----------------------------------------------------------------------
    // ILlmExpedienteExtractor<ImageSource> — ExtractExpedienteAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractExpedienteAsync_HappyPath_ReturnsExpedienteWithPartes()
    {
        // Arrange — canned JSON with two partes
        const string json = """
            {
              "expediente": "456/2025",
              "solicitante": "Claudia Reyes",
              "monto": null,
              "cuenta": null,
              "rfc": null,
              "curp": null,
              "partes": [
                {
                  "nombre": "Roberto Sánchez Díaz",
                  "rfc": "SADR750101BBB",
                  "curp": null,
                  "fechaNacimiento": "1975-01-01",
                  "caracter": "Contribuyente"
                },
                {
                  "nombre": "Empresa XYZ S.A. de C.V.",
                  "rfc": "EXY200101CCC",
                  "curp": null,
                  "fechaNacimiento": null,
                  "caracter": "Patrón Determinado"
                }
              ]
            }
            """;

        var ct = TestContext.Current.CancellationToken;
        var extractor = BuildExtractor(MakeVisionFactory(json));

        // Act — call the new port directly
        var result = await extractor.ExtractExpedienteAsync(OnePageSource, ct);

        // Assert — full Expediente with SolicitudPartes populated
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();

        var expediente = result.Value!;
        expediente.NumeroExpediente.ShouldBe("456/2025");
        expediente.NombreSolicitante.ShouldBe("Claudia Reyes");
        // Provenance marker for vision track
        expediente.AdditionalFields["_ExtractionSource"].ShouldBe("llm-vision");

        expediente.SolicitudPartes.Count.ShouldBe(2);

        var parte0 = expediente.SolicitudPartes[0];
        parte0.Nombre.ShouldBe("Roberto Sánchez Díaz");
        parte0.Rfc.ShouldBe("SADR750101BBB");
        parte0.Caracter.ShouldBe("Contribuyente");
        parte0.FechaNacimiento.ShouldBe(new DateOnly(1975, 1, 1));

        var parte1 = expediente.SolicitudPartes[1];
        parte1.Nombre.ShouldBe("Empresa XYZ S.A. de C.V.");
        parte1.Rfc.ShouldBe("EXY200101CCC");
        parte1.Caracter.ShouldBe("Patrón Determinado");
    }
}
