using ExxerCube.Prisma.Infrastructure.Classification;
using ExxerCube.Prisma.Infrastructure.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ExxerCube.Prisma.Tests.System.Storage.Infrastructure;

/// <summary>
/// Docker-gated integration test: verifies that the Ollama LLM <c>llama3.2:3b</c> can produce
/// a grounded, non-empty answer for a sample oficio text when called via <see cref="OllamaHttpClient"/>.
///
/// This test requires Docker + an Ollama container (pulled via <see cref="OllamaContainerFixture"/>).
/// It is in the "OllamaInfrastructure" collection, which is excluded from the normal unit suite by
/// virtue of needing a real container — all other tests in this module do NOT need Docker.
/// </summary>
[Collection("OllamaInfrastructure")]
public sealed class OllamaSemanticAnalyzerIntegrationTests
{
    private readonly OllamaContainerFixture _fixture;

    public OllamaSemanticAnalyzerIntegrationTests(OllamaContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private void Log(string message) =>
        TestContext.Current?.SendDiagnosticMessage(message);

    /// <summary>
    /// Real <see cref="OllamaHttpClient"/> against llama3.2:3b in Docker:
    /// a grounded oficio text asking for bank movements should yield a non-empty answer.
    /// </summary>
    [Fact]
    public async Task OllamaHttpClient_RealLlm_GroundedOficioPrompt_ReturnsNonEmptyAnswer()
    {
        // Arrange
        _fixture.EnsureAvailable();

        var options = Options.Create(new OllamaOptions
        {
            Enabled = true,
            Endpoint = _fixture.BaseUrl,
            Model = OllamaContainerFixture.LLMModel,
            TimeoutSeconds = 300 // generous for Docker model inference
        });

        using var httpClient = _fixture.GetHttpClient();
        var ollamaClient = new OllamaHttpClient(
            httpClient,
            options,
            NullLogger<OllamaHttpClient>.Instance);

        const string OficioText =
            "En atención al oficio número 123/2024 de la CNBV, se solicita a la institución " +
            "proporcionar información detallada sobre los movimientos de las cuentas bancarias " +
            "del señor Juan Pérez durante el período del 01 de enero al 31 de diciembre de 2023.";

        const string Question =
            "\n\n¿Qué información solicita la autoridad en este oficio? " +
            "Responde solo con la información solicitada, en una o dos oraciones breves.";

        var prompt = OficioText + Question;

        Log($"Sending grounded oficio prompt ({prompt.Length} chars) to {_fixture.BaseUrl}...");

        // Act
        var result = await ollamaClient.GenerateAsync(prompt, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue($"LLM call must succeed. Error: {(result.IsFailure ? result.Error : "n/a")}");
        result.Value.ShouldNotBeNullOrWhiteSpace("LLM must return a non-empty grounded answer");
        result.Value!.Length.ShouldBeGreaterThan(10, "Answer should be substantive, not just whitespace or a single char");

        Log($"LLM answered ({result.Value.Length} chars): {result.Value[..Math.Min(200, result.Value.Length)]}");
    }

    /// <summary>
    /// Full round-trip: <see cref="SemanticAnalyzerService"/> with a real <see cref="OllamaHttpClient"/>
    /// enriches <c>InformacionSolicitada</c> from the LLM when E1 left it empty.
    /// </summary>
    [Fact]
    public async Task SemanticAnalyzerService_WithRealOllama_EnrichesInformacionSolicitada()
    {
        // Arrange
        _fixture.EnsureAvailable();

        var options = Options.Create(new OllamaOptions
        {
            Enabled = true,
            Endpoint = _fixture.BaseUrl,
            Model = OllamaContainerFixture.LLMModel,
            TimeoutSeconds = 300
        });

        using var httpClient = _fixture.GetHttpClient();
        var ollamaClient = new OllamaHttpClient(
            httpClient,
            options,
            NullLogger<OllamaHttpClient>.Instance);

        // Use the real Levenshtein text comparer to detect the Information requirement
        var levenshteinComparer = new LevenshteinTextComparer(NullLogger<LevenshteinTextComparer>.Instance);
        var analyzerLogger = NullLogger<SemanticAnalyzerService>.Instance;

        var service = new SemanticAnalyzerService(
            levenshteinComparer,
            analyzerLogger,
            ollamaClient,
            options);

        // This text triggers the Information detector via the "solicita información" phrase
        const string OficioText =
            "La Comisión Nacional Bancaria y de Valores, mediante el presente oficio, " +
            "solicita información sobre los movimientos y transacciones de las cuentas " +
            "bancarias del contribuyente durante el ejercicio fiscal 2023.";

        Log($"Running SemanticAnalyzerService with real Ollama on oficio ({OficioText.Length} chars)...");

        // Act
        var result = await service.AnalyzeDirectivesAsync(
            OficioText,
            expediente: null,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue($"Analyzer must succeed. Error: {(result.IsFailure ? result.Error : "n/a")}");
        var analysis = result.Value!;

        analysis.RequiereInformacionGeneral.ShouldNotBeNull(
            "Información requirement should be detected from oficio text");

        var informacion = analysis.RequiereInformacionGeneral!;
        informacion.EsRequerido.ShouldBeTrue();

        // The LLM should have enriched InformacionSolicitada (E1 may leave it empty for this text)
        Log($"InformacionSolicitada after enrichment: '{informacion.InformacionSolicitada}'");

        // We assert a non-empty value — either E1 or LLM must have produced something
        informacion.InformacionSolicitada.ShouldNotBeNullOrWhiteSpace(
            "InformacionSolicitada must be non-empty: either E1 regex or LLM enrichment must fill it");
        informacion.InformacionSolicitada!.Length.ShouldBeGreaterThan(5,
            "Answer should be substantive");

        Log($"Full analysis: Bloqueo={analysis.RequiereBloqueo != null}, " +
            $"Información={analysis.RequiereInformacionGeneral != null} " +
            $"('{informacion.InformacionSolicitada[..Math.Min(100, informacion.InformacionSolicitada.Length)]}')");
    }
}
