namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Mutation-killing tests for <see cref="SemanticAnalyzerAdapter"/> (an adapter over a mocked
/// <see cref="ISemanticAnalyzer"/>). Pins the guards, exact error strings, the SemanticAnalysis →
/// List&lt;ComplianceAction&gt; conversion (every requirement mapping + the confidence ×100 rounding +
/// the Block extras), and the highest-confidence selection in MapToComplianceActionAsync.
/// </summary>
public class SemanticAnalyzerAdapterMutationTests
{
    private readonly ISemanticAnalyzer _analyzer = Substitute.For<ISemanticAnalyzer>();
    private readonly SemanticAnalyzerAdapter _adapter;

    public SemanticAnalyzerAdapterMutationTests(ITestOutputHelper output) =>
        _adapter = new SemanticAnalyzerAdapter(_analyzer, XUnitLogger.CreateLogger<SemanticAnalyzerAdapter>(output));

    private void AnalyzerReturns(SemanticAnalysis analysis) =>
        _analyzer.AnalyzeDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SemanticAnalysis>.Success(analysis));

    // =================== ClassifyDirectivesAsync ===================

    [Fact]
    public async Task Classify_CancelledBeforeStart_ReturnsExactFailure()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _adapter.ClassifyDirectivesAsync("bloquear", cancellationToken: cts.Token);

        result.Error.ShouldBe("Operation was cancelled.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Classify_BlankText_ReturnsExactFailure(string? text)
    {
        var result = await _adapter.ClassifyDirectivesAsync(text!, cancellationToken: TestContext.Current.CancellationToken);

        result.Error.ShouldBe("Document text cannot be null or empty.");
    }

    [Fact]
    public async Task Classify_AnalyzerFails_PropagatesError()
    {
        _analyzer.AnalyzeDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SemanticAnalysis>.WithFailure("analyzer-broke"));

        var result = await _adapter.ClassifyDirectivesAsync("text", cancellationToken: TestContext.Current.CancellationToken);

        result.Error.ShouldBe("analyzer-broke");
    }

    [Fact]
    public async Task Classify_BloqueoRequired_MapsToBlock_WithOriginsAndConfidence()
    {
        AnalyzerReturns(new SemanticAnalysis
        {
            RequiereBloqueo = new BloqueoRequirement { EsRequerido = true, Confidence = 0.9, Monto = 1234m },
        });
        var expediente = new Expediente { NumeroExpediente = "EXP-7", NumeroOficio = "OF-7" };

        var result = await _adapter.ClassifyDirectivesAsync("x", expediente, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var action = result.Value!.ShouldHaveSingleItem();
        action.ActionType.ShouldBe(ComplianceActionKind.Block);
        action.ExpedienteOrigen.ShouldBe("EXP-7");
        action.OficioOrigen.ShouldBe("OF-7");
        action.Confidence.ShouldBe(90); // ConvertConfidence: round(0.9 * 100)
        action.Amount.ShouldBe(1234m);
    }

    [Fact]
    public async Task Classify_ConvertConfidence_RoundsToNearestInteger()
    {
        AnalyzerReturns(new SemanticAnalysis
        {
            RequiereBloqueo = new BloqueoRequirement { EsRequerido = true, Confidence = 0.5 },
        });

        var result = await _adapter.ClassifyDirectivesAsync("x", cancellationToken: TestContext.Current.CancellationToken);

        // 0.5 * 100 = 50 (kills `* 100` and rounding mutants distinctly from the 90 case).
        result.Value!.Single().Confidence.ShouldBe(50);
    }

    [Fact]
    public async Task Classify_BloqueoNotRequired_ProducesNoAction()
    {
        AnalyzerReturns(new SemanticAnalysis
        {
            RequiereBloqueo = new BloqueoRequirement { EsRequerido = false, Confidence = 0.9 },
        });

        var result = await _adapter.ClassifyDirectivesAsync("x", cancellationToken: TestContext.Current.CancellationToken);

        result.Value!.ShouldBeEmpty();
    }

    [Fact]
    public async Task Classify_BloqueoMultipleAccounts_SetsFirstAndAdditionalData()
    {
        AnalyzerReturns(new SemanticAnalysis
        {
            RequiereBloqueo = new BloqueoRequirement
            {
                EsRequerido = true,
                Confidence = 0.8,
                CuentasEspecificas = new List<string> { "111", "222" },
                ProductosEspecificos = new List<string> { "TARJETA" },
            },
        });

        var result = await _adapter.ClassifyDirectivesAsync("x", cancellationToken: TestContext.Current.CancellationToken);

        var action = result.Value!.Single();
        action.AccountNumber.ShouldBe("111"); // FirstOrDefault of CuentasEspecificas
        action.AdditionalData.ShouldContainKey("CuentasEspecificas"); // Count > 1
        action.AdditionalData.ShouldContainKey("ProductosEspecificos"); // Any()
        action.ProductType.ShouldBe("TARJETA");
    }

    [Fact]
    public async Task Classify_BloqueoSingleAccount_NoCuentasEspecificasKey()
    {
        AnalyzerReturns(new SemanticAnalysis
        {
            RequiereBloqueo = new BloqueoRequirement
            {
                EsRequerido = true,
                Confidence = 0.8,
                CuentasEspecificas = new List<string> { "111" },
            },
        });

        var result = await _adapter.ClassifyDirectivesAsync("x", cancellationToken: TestContext.Current.CancellationToken);

        var action = result.Value!.Single();
        action.AccountNumber.ShouldBe("111");
        // Count > 1 is false → key absent (kills `> 1` → `>= 1`).
        action.AdditionalData.ShouldNotContainKey("CuentasEspecificas");
    }

    [Fact]
    public async Task Classify_DesbloqueoRequired_MapsToUnblock()
    {
        AnalyzerReturns(new SemanticAnalysis
        {
            RequiereDesbloqueo = new DesbloqueoRequirement { EsRequerido = true, Confidence = 0.7 },
        });

        var result = await _adapter.ClassifyDirectivesAsync("x", cancellationToken: TestContext.Current.CancellationToken);

        result.Value!.Single().ActionType.ShouldBe(ComplianceActionKind.Unblock);
    }

    [Fact]
    public async Task Classify_TransferenciaRequired_MapsToTransfer()
    {
        AnalyzerReturns(new SemanticAnalysis
        {
            RequiereTransferencia = new TransferenciaRequirement { EsRequerido = true, Confidence = 0.7 },
        });

        var result = await _adapter.ClassifyDirectivesAsync("x", cancellationToken: TestContext.Current.CancellationToken);

        result.Value!.Single().ActionType.ShouldBe(ComplianceActionKind.Transfer);
    }

    [Fact]
    public async Task Classify_DocumentacionRequired_MapsToDocument()
    {
        AnalyzerReturns(new SemanticAnalysis
        {
            RequiereDocumentacion = new DocumentacionRequirement { EsRequerido = true, Confidence = 0.7 },
        });

        var result = await _adapter.ClassifyDirectivesAsync("x", cancellationToken: TestContext.Current.CancellationToken);

        result.Value!.Single().ActionType.ShouldBe(ComplianceActionKind.Document);
    }

    [Fact]
    public async Task Classify_InformacionGeneralRequired_MapsToInformation()
    {
        AnalyzerReturns(new SemanticAnalysis
        {
            RequiereInformacionGeneral = new InformacionGeneralRequirement { EsRequerido = true, Confidence = 0.7 },
        });

        var result = await _adapter.ClassifyDirectivesAsync("x", cancellationToken: TestContext.Current.CancellationToken);

        result.Value!.Single().ActionType.ShouldBe(ComplianceActionKind.Information);
    }

    [Fact]
    public async Task Classify_MultipleRequirements_ProducesMultipleActions()
    {
        AnalyzerReturns(new SemanticAnalysis
        {
            RequiereBloqueo = new BloqueoRequirement { EsRequerido = true, Confidence = 0.9 },
            RequiereInformacionGeneral = new InformacionGeneralRequirement { EsRequerido = true, Confidence = 0.5 },
        });

        var result = await _adapter.ClassifyDirectivesAsync("x", cancellationToken: TestContext.Current.CancellationToken);

        result.Value!.Count.ShouldBe(2);
        result.Value!.Select(a => a.ActionType).ShouldContain(ComplianceActionKind.Block);
        result.Value!.Select(a => a.ActionType).ShouldContain(ComplianceActionKind.Information);
    }

    // =================== DetectLegalInstrumentsAsync ===================

    [Fact]
    public async Task DetectLegalInstruments_Cancelled_ReturnsExactFailure()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _adapter.DetectLegalInstrumentsAsync("x", cts.Token);

        result.Error.ShouldBe("Operation was cancelled.");
    }

    [Fact]
    public async Task DetectLegalInstruments_NotCancelled_ReturnsEmptyList()
    {
        var result = await _adapter.DetectLegalInstrumentsAsync("x", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Count.ShouldBe(0);
    }

    // =================== MapToComplianceActionAsync ===================

    [Fact]
    public async Task Map_CancelledBeforeStart_ReturnsExactFailure()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _adapter.MapToComplianceActionAsync("bloquear", cancellationToken: cts.Token);

        result.Error.ShouldBe("Operation was cancelled.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Map_BlankText_ReturnsExactFailure(string? text)
    {
        var result = await _adapter.MapToComplianceActionAsync(text!, cancellationToken: TestContext.Current.CancellationToken);

        result.Error.ShouldBe("Directive text cannot be null or empty.");
    }

    [Fact]
    public async Task Map_NoActionsDetected_ReturnsExactFailure()
    {
        AnalyzerReturns(new SemanticAnalysis()); // no requirements → zero actions

        var result = await _adapter.MapToComplianceActionAsync("x", cancellationToken: TestContext.Current.CancellationToken);

        result.Error.ShouldBe("No compliance actions detected in directive text.");
    }

    [Fact]
    public async Task Map_MultipleActions_ReturnsHighestConfidence()
    {
        AnalyzerReturns(new SemanticAnalysis
        {
            RequiereInformacionGeneral = new InformacionGeneralRequirement { EsRequerido = true, Confidence = 0.5 },
            RequiereBloqueo = new BloqueoRequirement { EsRequerido = true, Confidence = 0.9 },
        });

        var result = await _adapter.MapToComplianceActionAsync("x", cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        // OrderByDescending(Confidence).First() → Block (90) over Information (50).
        result.Value!.ActionType.ShouldBe(ComplianceActionKind.Block);
        result.Value!.Confidence.ShouldBe(90);
    }

    [Fact]
    public async Task Map_SingleAction_ReturnsThatAction()
    {
        AnalyzerReturns(new SemanticAnalysis
        {
            RequiereTransferencia = new TransferenciaRequirement { EsRequerido = true, Confidence = 0.7 },
        });

        var result = await _adapter.MapToComplianceActionAsync("x", cancellationToken: TestContext.Current.CancellationToken);

        result.Value!.ActionType.ShouldBe(ComplianceActionKind.Transfer);
    }

    [Fact]
    public async Task Map_InnerClassificationFails_PropagatesError()
    {
        // MapToComplianceActionAsync delegates to ClassifyDirectivesAsync; when the analyzer fails the inner
        // classification fails and Map must surface that failure (covers the !IsSuccess propagation branch).
        _analyzer.AnalyzeDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SemanticAnalysis>.WithFailure("inner-broke"));

        var result = await _adapter.MapToComplianceActionAsync("x", cancellationToken: TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("inner-broke");
    }
}
