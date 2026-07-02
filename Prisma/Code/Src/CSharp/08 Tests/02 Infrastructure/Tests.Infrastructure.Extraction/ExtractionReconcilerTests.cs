using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Llm;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Llm;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Unit tests for <see cref="ExtractionReconciler"/>.
/// All Expediente instances are hand-built — no LLM calls, no I/O.
/// Verifies the three-rule per-field merge policy and the ReviewFlags contract.
/// </summary>
public sealed class ExtractionReconcilerTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private ExtractionReconciler BuildReconciler(ITestOutputHelper? output = null)
    {
        var logger = XUnitLogger.CreateLogger<ExtractionReconciler>(output!);
        return new ExtractionReconciler(logger);
    }

    private static LabelledExtraction Det(Expediente? fields, TrackStatus status = TrackStatus.Available)
        => new("deterministic", fields, status);

    private static LabelledExtraction Llm(string source, Expediente? fields, TrackStatus status = TrackStatus.Available)
        => new(source, fields, status);

    private static Expediente Expediente(string expediente = "", string? solicitante = null)
    {
        var e = new Domain.Entities.Expediente
        {
            NumeroExpediente = expediente,
            NombreSolicitante = solicitante,
        };
        return e;
    }

    // -----------------------------------------------------------------------
    // Rule 1: deterministic wins
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ReconcileAsync_DeterministicNonEmpty_WinsOverLlm()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var detExpediente = new Domain.Entities.Expediente { NumeroExpediente = "123/2024" };
        var llmExpediente = new Domain.Entities.Expediente { NumeroExpediente = "999/9999" };

        var candidates = new[]
        {
            Det(detExpediente),
            Llm("llm-text", llmExpediente),
        };

        var reconciler = BuildReconciler();

        // Act
        var result = await reconciler.ReconcileAsync(candidates, ct);

        // Assert — deterministic value must win
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Best.NumeroExpediente.ShouldBe("123/2024");
        result.Value.ReviewFlags.ShouldBeEmpty();
    }

    // -----------------------------------------------------------------------
    // Rule 2: LLM fills the gap when deterministic is absent/empty
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ReconcileAsync_DetAbsent_LlmFillsNumeroExpediente()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var detExpediente = new Domain.Entities.Expediente { NumeroExpediente = string.Empty }; // empty
        var llmExpediente = new Domain.Entities.Expediente { NumeroExpediente = "456/2023" };

        var candidates = new[]
        {
            Det(detExpediente),
            Llm("llm-text", llmExpediente),
        };

        var reconciler = BuildReconciler();

        // Act
        var result = await reconciler.ReconcileAsync(candidates, ct);

        // Assert — LLM fills the empty field
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Best.NumeroExpediente.ShouldBe("456/2023");
        result.Value.ReviewFlags.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReconcileAsync_NoDeterministicCandidate_LlmFillsFields()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var llmExpediente = new Domain.Entities.Expediente
        {
            NumeroExpediente = "789/2022",
            NombreSolicitante = "Carlos Ruiz",
        };

        var candidates = new[] { Llm("llm-vision", llmExpediente) };
        var reconciler = BuildReconciler();

        // Act
        var result = await reconciler.ReconcileAsync(candidates, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Best.NumeroExpediente.ShouldBe("789/2022");
        result.Value.Best.NombreSolicitante.ShouldBe("Carlos Ruiz");
    }

    // -----------------------------------------------------------------------
    // Rule 3: two LLMs disagree → null field + ReviewFlag
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ReconcileAsync_TwoLlmsDisagree_FieldNullAndReviewFlagAdded()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var detExpediente = new Domain.Entities.Expediente { NumeroExpediente = string.Empty };
        var llmText = new Domain.Entities.Expediente { NumeroExpediente = "111/2024" };
        var llmVision = new Domain.Entities.Expediente { NumeroExpediente = "222/2024" };

        var candidates = new[]
        {
            Det(detExpediente),
            Llm("llm-text", llmText),
            Llm("llm-vision", llmVision),
        };

        var reconciler = BuildReconciler();

        // Act
        var result = await reconciler.ReconcileAsync(candidates, ct);

        // Assert — conflict: neither LLM value should be chosen; a flag must appear
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Best.NumeroExpediente.ShouldBe(string.Empty); // left at copied-from-det value
        result.Value.ReviewFlags.Count.ShouldBe(1);
        result.Value.ReviewFlags[0].ShouldContain("NumeroExpediente");
        result.Value.ReviewFlags[0].ShouldContain("disagree");
    }

    // -----------------------------------------------------------------------
    // Two LLMs agree → fill (no flag)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ReconcileAsync_TwoLlmsAgree_FillsFieldNoFlag()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var detExpediente = new Domain.Entities.Expediente { NumeroExpediente = string.Empty };
        var llmText = new Domain.Entities.Expediente { NumeroExpediente = "333/2024" };
        var llmVision = new Domain.Entities.Expediente { NumeroExpediente = "333/2024" };

        var candidates = new[]
        {
            Det(detExpediente),
            Llm("llm-text", llmText),
            Llm("llm-vision", llmVision),
        };

        var reconciler = BuildReconciler();

        // Act
        var result = await reconciler.ReconcileAsync(candidates, ct);

        // Assert — both agree → fill
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Best.NumeroExpediente.ShouldBe("333/2024");
        result.Value.ReviewFlags.ShouldBeEmpty();
    }

    // -----------------------------------------------------------------------
    // Best Expediente assembled correctly (header fields from deterministic)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ReconcileAsync_BestExpediente_CopiesHeaderFieldsFromDeterministic()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var det = new Domain.Entities.Expediente
        {
            NumeroExpediente = "100/2024",
            NumeroOficio = "214-1-12345678/2024",
            AreaDescripcion = "ASEGURAMIENTO",
            AutoridadNombre = "SAT",
            Folio = 42,
        };
        var llm = new Domain.Entities.Expediente { NumeroExpediente = "999/9999" };

        var candidates = new[]
        {
            Det(det),
            Llm("llm-text", llm),
        };

        var reconciler = BuildReconciler();

        // Act
        var result = await reconciler.ReconcileAsync(candidates, ct);

        // Assert — header fields come from deterministic; high-value field also from deterministic
        result.IsSuccess.ShouldBeTrue();
        var best = result.Value!.Best;
        best.NumeroExpediente.ShouldBe("100/2024"); // det wins
        best.NumeroOficio.ShouldBe("214-1-12345678/2024");
        best.AreaDescripcion.ShouldBe("ASEGURAMIENTO");
        best.AutoridadNombre.ShouldBe("SAT");
        best.Folio.ShouldBe(42);
    }

    // -----------------------------------------------------------------------
    // SolicitudPartes: deterministic wins; LLM fills if det has none
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ReconcileAsync_DetHasPartes_UsesDetPartes()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var parte = new SolicitudParte { ParteId = 1, Nombre = "Juan Pérez", Curp = string.Empty, Caracter = "Contribuyente" };
        var det = new Domain.Entities.Expediente { NumeroExpediente = "1/2024" };
        det.SolicitudPartes.Add(parte);

        var llmExp = new Domain.Entities.Expediente { NumeroExpediente = "2/2024" };
        var llmParte = new SolicitudParte { ParteId = 1, Nombre = "Persona Distinta", Curp = string.Empty, Caracter = "Patrón" };
        llmExp.SolicitudPartes.Add(llmParte);

        var candidates = new[] { Det(det), Llm("llm-text", llmExp) };
        var reconciler = BuildReconciler();

        // Act
        var result = await reconciler.ReconcileAsync(candidates, ct);

        // Assert — deterministic partes must win
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Best.SolicitudPartes.Count.ShouldBe(1);
        result.Value.Best.SolicitudPartes[0].Nombre.ShouldBe("Juan Pérez");
    }

    [Fact]
    public async Task ReconcileAsync_DetNoPartes_LlmPartesUsed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var det = new Domain.Entities.Expediente { NumeroExpediente = "1/2024" }; // no partes
        var llmExp = new Domain.Entities.Expediente { NumeroExpediente = "1/2024" };
        llmExp.SolicitudPartes.Add(new SolicitudParte { ParteId = 1, Nombre = "Persona LLM", Curp = string.Empty, Caracter = "Solicitante" });

        var candidates = new[] { Det(det), Llm("llm-vision", llmExp) };
        var reconciler = BuildReconciler();

        // Act
        var result = await reconciler.ReconcileAsync(candidates, ct);

        // Assert — LLM partes fill the gap
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Best.SolicitudPartes.Count.ShouldBe(1);
        result.Value.Best.SolicitudPartes[0].Nombre.ShouldBe("Persona LLM");
    }

    // -----------------------------------------------------------------------
    // AdditionalFields merge (Monto from LLM when det has none)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ReconcileAsync_MontoAbsentInDet_LlmMontoUsed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var det = new Domain.Entities.Expediente { NumeroExpediente = "50/2024" };
        // det has no Monto in AdditionalFields

        var llmExp = new Domain.Entities.Expediente { NumeroExpediente = "50/2024" };
        llmExp.AdditionalFields["Monto"] = "9500.00";

        var candidates = new[] { Det(det), Llm("llm-text", llmExp) };
        var reconciler = BuildReconciler();

        // Act
        var result = await reconciler.ReconcileAsync(candidates, ct);

        // Assert — LLM fills Monto
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Best.AdditionalFields.TryGetValue("Monto", out var monto).ShouldBeTrue();
        monto.ShouldBe("9500.00");
    }

    // -----------------------------------------------------------------------
    // Skipped / failed candidates are ignored
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ReconcileAsync_SkippedLlmCandidate_IsIgnoredInMerge()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var det = new Domain.Entities.Expediente { NumeroExpediente = string.Empty };
        // LLM candidate skipped (no capability) — Fields is null, Status is SkippedNoCapability
        var skipped = new LabelledExtraction("llm-vision", null, TrackStatus.SkippedNoCapability);

        var candidates = new[] { Det(det), skipped };
        var reconciler = BuildReconciler();

        // Act
        var result = await reconciler.ReconcileAsync(candidates, ct);

        // Assert — skipped candidate provides no value; no flags
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Best.NumeroExpediente.ShouldBe(string.Empty);
        result.Value.ReviewFlags.ShouldBeEmpty();
    }

    // -----------------------------------------------------------------------
    // All candidates passed through in result
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ReconcileAsync_CandidatesRetainedInResult()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var det = new Domain.Entities.Expediente { NumeroExpediente = "1/2024" };
        var skipped = new LabelledExtraction("llm-vision", null, TrackStatus.SkippedByFlag, "Flag off");
        var candidates = new[] { Det(det), skipped };

        var reconciler = BuildReconciler();

        // Act
        var result = await reconciler.ReconcileAsync(candidates, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Candidates.Count.ShouldBe(2);
    }

    // -----------------------------------------------------------------------
    // Empty candidate list → failure
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ReconcileAsync_EmptyCandidates_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var reconciler = BuildReconciler();

        var result = await reconciler.ReconcileAsync([], ct);

        result.IsSuccess.ShouldBeFalse();
    }

    // -----------------------------------------------------------------------
    // Provenance marker set on Best
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ReconcileAsync_BestAlwaysHasReconciliationSourceMarker()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var det = new Domain.Entities.Expediente { NumeroExpediente = "1/2024" };
        var candidates = new[] { Det(det) };
        var reconciler = BuildReconciler();

        // Act
        var result = await reconciler.ReconcileAsync(candidates, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Best.AdditionalFields["_ReconciliationSource"].ShouldBe("ExtractionReconciler");
    }
}
