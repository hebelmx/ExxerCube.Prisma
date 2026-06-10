using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Mutation-killing tests for <see cref="ExportService"/>.
/// All collaborators are mocked (<see cref="IResponseExporter"/>, <see cref="ILayoutGenerator"/>,
/// <see cref="ICriterionMapper"/>, <see cref="IPdfRequirementSummarizer"/>, <see cref="IAuditLogger"/>),
/// so every guard, exact error-message string, cancel/failure propagation, and audit side-effect on the
/// pure orchestration surface is pinned. Mirrors the SLA/Audit/DecisionLogic Unit 38–41 pattern.
/// </summary>
public class ExportServiceMutationTests
{
    private readonly IResponseExporter _responseExporter = Substitute.For<IResponseExporter>();
    private readonly ILayoutGenerator _layoutGenerator = Substitute.For<ILayoutGenerator>();
    private readonly ICriterionMapper _criterionMapper = Substitute.For<ICriterionMapper>();
    private readonly IPdfRequirementSummarizer _pdfRequirementSummarizer = Substitute.For<IPdfRequirementSummarizer>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly ExportService _service;

    public ExportServiceMutationTests(ITestOutputHelper output)
    {
        _service = new ExportService(
            _responseExporter,
            _layoutGenerator,
            _criterionMapper,
            _pdfRequirementSummarizer,
            _auditLogger,
            XUnitLogger.CreateLogger<ExportService>(output));

        _auditLogger.LogAuditAsync(
            Arg.Any<AuditActionType>(), Arg.Any<ProcessingStage>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
    }

    private static Expediente ValidExpediente() => new()
    {
        NumeroExpediente = "EXP-1",
        NumeroOficio = "OF-1",
        FundamentoLegal = "Art 115",
        MedioEnvio = "SIARA",
        Subdivision = LegalSubdivisionKind.A_AS,
        FechaRecepcion = new DateTime(2026, 1, 1),
        FechaEstimadaConclusion = new DateTime(2026, 1, 8),
    };

    private static UnifiedMetadataRecord ValidMetadata() => new() { Expediente = ValidExpediente() };

    // =================== ExportSiroXmlAsync ===================

    [Fact]
    public async Task ExportSiroXml_CancelledBeforeStart_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var stream = new MemoryStream();

        var result = await _service.ExportSiroXmlAsync(ValidMetadata(), stream, cancellationToken: cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task ExportSiroXml_NullMetadata_ReturnsExactFailure()
    {
        using var stream = new MemoryStream();

        var result = await _service.ExportSiroXmlAsync(null!, stream, cancellationToken: TestContext.Current.CancellationToken);

        result.Error.ShouldBe("Metadata cannot be null");
    }

    [Fact]
    public async Task ExportSiroXml_NullStream_ReturnsExactFailure()
    {
        var result = await _service.ExportSiroXmlAsync(ValidMetadata(), null!, cancellationToken: TestContext.Current.CancellationToken);

        result.Error.ShouldBe("Output stream cannot be null");
    }

    [Fact]
    public async Task ExportSiroXml_Success_LogsSuccessAudit_AndReturnsSuccess()
    {
        using var stream = new MemoryStream();
        _responseExporter.ExportSiroXmlAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await _service.ExportSiroXmlAsync(ValidMetadata(), stream, cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        // Exact detail JSON pins every literal segment of the interpolated audit string.
        await _auditLogger.Received(1).LogAuditAsync(
            AuditActionType.Export, ProcessingStage.Export, Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Is<string?>(d => d == "{\"Expediente\":\"EXP-1\",\"Oficio\":\"OF-1\",\"Format\":\"SIRO XML\"}"),
            Arg.Is<bool>(b => b), Arg.Is<string?>(e => e == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportSiroXml_ExporterCancelled_ReturnsCancelled()
    {
        using var stream = new MemoryStream();
        _responseExporter.ExportSiroXmlAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled());

        var result = await _service.ExportSiroXmlAsync(ValidMetadata(), stream, cancellationToken: TestContext.Current.CancellationToken);

        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task ExportSiroXml_ExporterFailure_WrapsError_AndLogsFailureAudit()
    {
        using var stream = new MemoryStream();
        _responseExporter.ExportSiroXmlAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result.WithFailure("boom"));

        var result = await _service.ExportSiroXmlAsync(ValidMetadata(), stream, cancellationToken: TestContext.Current.CancellationToken);

        result.Error.ShouldBe("SIRO XML export failed: boom");
        await _auditLogger.Received(1).LogAuditAsync(
            AuditActionType.Export, ProcessingStage.Export, Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Is<string?>(d => d == "{\"Expediente\":\"EXP-1\",\"Oficio\":\"OF-1\",\"Format\":\"SIRO XML\"}"),
            Arg.Is<bool>(b => !b), Arg.Is<string?>(e => e == "boom"), Arg.Any<CancellationToken>());
    }

    // =================== ValidateMetadataCompleteness (via ExportSiroXmlAsync) ===================

    [Fact]
    public async Task Validate_NullExpediente_FailsWithExpedienteMissing()
    {
        using var stream = new MemoryStream();
        var metadata = new UnifiedMetadataRecord { Expediente = null };

        var result = await _service.ExportSiroXmlAsync(metadata, stream, cancellationToken: TestContext.Current.CancellationToken);

        result.Error.ShouldBe("Validation failed: Expediente");
    }

    // Note: blank NumeroExpediente is reported under the field name "Expediente" in the SUT.
    [Theory]
    [InlineData("NumeroExpediente", "Expediente")]
    [InlineData("NumeroOficio", "NumeroOficio")]
    [InlineData("FundamentoLegal", "FundamentoLegal")]
    [InlineData("MedioEnvio", "MedioEnvio")]
    [InlineData("Subdivision", "Subdivision")]
    [InlineData("FechaRecepcion", "FechaRecepcion")]
    [InlineData("FechaEstimadaConclusion", "FechaEstimadaConclusion")]
    public async Task Validate_SingleMissingField_FailsWithThatFieldName(string fieldToOmit, string expectedMissing)
    {
        using var stream = new MemoryStream();
        var exp = ValidExpediente();
        switch (fieldToOmit)
        {
            case "NumeroExpediente": exp.NumeroExpediente = ""; break;
            case "NumeroOficio": exp.NumeroOficio = ""; break;
            case "FundamentoLegal": exp.FundamentoLegal = ""; break;
            case "MedioEnvio": exp.MedioEnvio = ""; break;
            case "Subdivision": exp.Subdivision = LegalSubdivisionKind.Unknown; break;
            case "FechaRecepcion": exp.FechaRecepcion = default; break;
            case "FechaEstimadaConclusion": exp.FechaEstimadaConclusion = default; break;
        }
        var metadata = new UnifiedMetadataRecord { Expediente = exp };

        var result = await _service.ExportSiroXmlAsync(metadata, stream, cancellationToken: TestContext.Current.CancellationToken);

        // Exactly one field missing → the joined Missing string is precisely that field name.
        result.Error.ShouldBe($"Validation failed: {expectedMissing}");
    }

    [Fact]
    public async Task Validate_BlockActionWithoutAccount_FailsWithAccountMissing()
    {
        using var stream = new MemoryStream();
        var metadata = ValidMetadata();
        metadata.ComplianceActions = new List<ComplianceAction>
        {
            new() { ActionType = ComplianceActionKind.Block, AccountNumber = "" },
        };

        var result = await _service.ExportSiroXmlAsync(metadata, stream, cancellationToken: TestContext.Current.CancellationToken);

        result.Error.ShouldBe("Validation failed: ComplianceAction.Account");
    }

    [Fact]
    public async Task Validate_UnknownActionType_FailsWithActionTypeMissing()
    {
        using var stream = new MemoryStream();
        var metadata = ValidMetadata();
        metadata.ComplianceActions = new List<ComplianceAction>
        {
            new() { ActionType = ComplianceActionKind.Unknown },
        };

        var result = await _service.ExportSiroXmlAsync(metadata, stream, cancellationToken: TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull();
        result.Error!.ShouldContain("ComplianceAction.ActionType");
    }

    [Fact]
    public async Task Validate_BlockActionWithAccountNumber_PassesAccountCheck()
    {
        using var stream = new MemoryStream();
        _responseExporter.ExportSiroXmlAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var metadata = ValidMetadata();
        metadata.ComplianceActions = new List<ComplianceAction>
        {
            new() { ActionType = ComplianceActionKind.Block, AccountNumber = "0123456789" },
        };

        var result = await _service.ExportSiroXmlAsync(metadata, stream, cancellationToken: TestContext.Current.CancellationToken);

        // Account satisfied via AccountNumber → validation passes → exporter reached → success.
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_BlockActionWithCuentaNumero_PassesAccountCheck()
    {
        using var stream = new MemoryStream();
        _responseExporter.ExportSiroXmlAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var metadata = ValidMetadata();
        metadata.ComplianceActions = new List<ComplianceAction>
        {
            new() { ActionType = ComplianceActionKind.Block, AccountNumber = "", Cuenta = new Cuenta { Numero = "987654" } },
        };

        var result = await _service.ExportSiroXmlAsync(metadata, stream, cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_DocumentAction_DoesNotRequireAccount()
    {
        using var stream = new MemoryStream();
        _responseExporter.ExportSiroXmlAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var metadata = ValidMetadata();
        metadata.ComplianceActions = new List<ComplianceAction>
        {
            // Document is NOT in the Block/Unblock/Transfer set → no account required.
            new() { ActionType = ComplianceActionKind.Document, AccountNumber = "" },
        };

        var result = await _service.ExportSiroXmlAsync(metadata, stream, cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    // =================== GenerateExcelLayoutAsync ===================

    [Fact]
    public async Task GenerateExcel_CancelledBeforeStart_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var stream = new MemoryStream();

        var result = await _service.GenerateExcelLayoutAsync(ValidMetadata(), stream, cancellationToken: cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateExcel_NullMetadata_ReturnsExactFailure()
    {
        using var stream = new MemoryStream();

        var result = await _service.GenerateExcelLayoutAsync(null!, stream, cancellationToken: TestContext.Current.CancellationToken);

        result.Error.ShouldBe("Metadata cannot be null");
    }

    [Fact]
    public async Task GenerateExcel_NullStream_ReturnsExactFailure()
    {
        var result = await _service.GenerateExcelLayoutAsync(ValidMetadata(), null!, cancellationToken: TestContext.Current.CancellationToken);

        result.Error.ShouldBe("Output stream cannot be null");
    }

    [Fact]
    public async Task GenerateExcel_NullExpediente_ReturnsExactFailure()
    {
        using var stream = new MemoryStream();
        var metadata = new UnifiedMetadataRecord { Expediente = null };

        var result = await _service.GenerateExcelLayoutAsync(metadata, stream, cancellationToken: TestContext.Current.CancellationToken);

        result.Error.ShouldBe("Expediente is required for Excel layout generation");
    }

    [Fact]
    public async Task GenerateExcel_Success_LogsSuccessAudit_AndReturnsSuccess()
    {
        using var stream = new MemoryStream();
        _layoutGenerator.GenerateExcelLayoutAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await _service.GenerateExcelLayoutAsync(ValidMetadata(), stream, cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _auditLogger.Received(1).LogAuditAsync(
            AuditActionType.Export, ProcessingStage.Export, Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Is<string?>(d => d == "{\"Expediente\":\"EXP-1\",\"Format\":\"Excel\"}"),
            Arg.Is<bool>(b => b), Arg.Is<string?>(e => e == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateExcel_GeneratorCancelled_ReturnsCancelled()
    {
        using var stream = new MemoryStream();
        _layoutGenerator.GenerateExcelLayoutAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled());

        var result = await _service.GenerateExcelLayoutAsync(ValidMetadata(), stream, cancellationToken: TestContext.Current.CancellationToken);

        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateExcel_GeneratorFailure_WrapsError_AndLogsFailureAudit()
    {
        using var stream = new MemoryStream();
        _layoutGenerator.GenerateExcelLayoutAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result.WithFailure("xlfail"));

        var result = await _service.GenerateExcelLayoutAsync(ValidMetadata(), stream, cancellationToken: TestContext.Current.CancellationToken);

        result.Error.ShouldBe("Excel layout generation failed: xlfail");
        await _auditLogger.Received(1).LogAuditAsync(
            AuditActionType.Export, ProcessingStage.Export, Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Is<string?>(d => d == "{\"Expediente\":\"EXP-1\",\"Format\":\"Excel\"}"),
            Arg.Is<bool>(b => !b), Arg.Is<string?>(e => e == "xlfail"), Arg.Any<CancellationToken>());
    }

    // =================== MapToSiroCriteriaAsync ===================

    [Fact]
    public async Task MapToSiro_CancelledBeforeStart_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _service.MapToSiroCriteriaAsync(new List<ComplianceRequirement>(), cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task MapToSiro_NullRequirements_ReturnsExactFailure()
    {
        var result = await _service.MapToSiroCriteriaAsync(null!, TestContext.Current.CancellationToken);

        result.Error.ShouldBe("Requirements cannot be null");
    }

    [Fact]
    public async Task MapToSiro_MapperCancelled_ReturnsCancelled()
    {
        _criterionMapper.MapToSiroCriteriaAsync(Arg.Any<List<ComplianceRequirement>>(), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<Dictionary<string, object>>());

        var result = await _service.MapToSiroCriteriaAsync(new List<ComplianceRequirement>(), TestContext.Current.CancellationToken);

        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task MapToSiro_MapperFailure_WrapsError()
    {
        _criterionMapper.MapToSiroCriteriaAsync(Arg.Any<List<ComplianceRequirement>>(), Arg.Any<CancellationToken>())
            .Returns(Result<Dictionary<string, object>>.WithFailure("mapfail"));

        var result = await _service.MapToSiroCriteriaAsync(new List<ComplianceRequirement>(), TestContext.Current.CancellationToken);

        result.Error.ShouldBe("Criterion mapping failed: mapfail");
    }

    [Fact]
    public async Task MapToSiro_Success_ReturnsMapperValue()
    {
        var mapped = new Dictionary<string, object> { ["Criterion_1"] = "v" };
        _criterionMapper.MapToSiroCriteriaAsync(Arg.Any<List<ComplianceRequirement>>(), Arg.Any<CancellationToken>())
            .Returns(Result<Dictionary<string, object>>.Success(mapped));

        var result = await _service.MapToSiroCriteriaAsync(
            new List<ComplianceRequirement> { new() { RequerimientoId = "1" } }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!["Criterion_1"].ShouldBe("v");
    }

    // =================== ExportSignedPdfWithSummarizationAsync (audit flags) ===================

    [Fact]
    public async Task ExportSignedPdf_Success_LogsAuditWithSuccessTrue_AndHasSummaryTrue()
    {
        using var stream = new MemoryStream();
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        _pdfRequirementSummarizer.SummarizeRequirementsAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<RequirementSummary>.Success(new RequirementSummary { SummaryText = "s" }));
        _responseExporter.ExportSignedPdfAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await _service.ExportSignedPdfWithSummarizationAsync(
            ValidMetadata(), pdfContent, stream, cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        // Audit success flag mirrors exportResult.IsSuccess (true); detail carries HasSummary:true.
        await _auditLogger.Received(1).LogAuditAsync(
            AuditActionType.Export, ProcessingStage.Export, Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Is<string?>(d => d == "{\"Expediente\":\"EXP-1\",\"Oficio\":\"OF-1\",\"Format\":\"Signed PDF\",\"HasSummary\":True}"),
            Arg.Is<bool>(b => b), Arg.Is<string?>(e => e == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportSignedPdf_ExporterFailure_LogsAuditWithSuccessFalse_AndWrapsError()
    {
        using var stream = new MemoryStream();
        _responseExporter.ExportSignedPdfAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result.WithFailure("pdffail"));

        var result = await _service.ExportSignedPdfWithSummarizationAsync(
            ValidMetadata(), null, stream, cancellationToken: TestContext.Current.CancellationToken);

        result.Error.ShouldBe("Signed PDF export failed: pdffail");
        await _auditLogger.Received(1).LogAuditAsync(
            AuditActionType.Export, ProcessingStage.Export, Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Is<string?>(d => d == "{\"Expediente\":\"EXP-1\",\"Oficio\":\"OF-1\",\"Format\":\"Signed PDF\",\"HasSummary\":False}"),
            Arg.Is<bool>(b => !b), Arg.Is<string?>(e => e == "pdffail"), Arg.Any<CancellationToken>());
    }

    // =================== Additional-field WarnIf branches (advisory warnings) ===================

    [Fact]
    public async Task Validate_SubdivisionUnknown_AddsSubdivisionWarning()
    {
        using var stream = new MemoryStream();
        _responseExporter.ExportSiroXmlAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var metadata = ValidMetadata();
        // value == "Unknown" → WarnIf condition false → warning added. Kills the "Unknown" literal:
        // mutating it to "" makes the inequality true → condition true → no warning.
        metadata.AdditionalFields["Subdivision"] = "Unknown";

        await _service.ExportSiroXmlAsync(metadata, stream, cancellationToken: TestContext.Current.CancellationToken);

        metadata.Validation.ShouldNotBeNull();
        metadata.Validation!.Warnings.ShouldContain("Subdivision");
    }

    [Fact]
    public async Task Validate_SubdivisionKnown_NoSubdivisionWarning()
    {
        using var stream = new MemoryStream();
        _responseExporter.ExportSiroXmlAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var metadata = ValidMetadata();
        metadata.AdditionalFields["Subdivision"] = "A_AS"; // != "Unknown" → condition true → no warning

        await _service.ExportSiroXmlAsync(metadata, stream, cancellationToken: TestContext.Current.CancellationToken);

        metadata.Validation!.Warnings.ShouldNotContain("Subdivision");
    }

    [Fact]
    public async Task Validate_MeasureHintInformacion_AddsMeasureHintWarning()
    {
        using var stream = new MemoryStream();
        _responseExporter.ExportSiroXmlAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var metadata = ValidMetadata();
        metadata.AdditionalFields["MeasureHint"] = "Informacion"; // == sentinel → condition false → warning

        await _service.ExportSiroXmlAsync(metadata, stream, cancellationToken: TestContext.Current.CancellationToken);

        metadata.Validation!.Warnings.ShouldContain("MeasureHint");
    }

    [Fact]
    public async Task Validate_MeasureHintOther_NoMeasureHintWarning()
    {
        using var stream = new MemoryStream();
        _responseExporter.ExportSiroXmlAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var metadata = ValidMetadata();
        metadata.AdditionalFields["MeasureHint"] = "Bloqueo"; // != "Informacion" → condition true → no warning

        await _service.ExportSiroXmlAsync(metadata, stream, cancellationToken: TestContext.Current.CancellationToken);

        metadata.Validation!.Warnings.ShouldNotContain("MeasureHint");
    }

    [Fact]
    public async Task Validate_AdditionalFieldConflicts_AddWarnings()
    {
        using var stream = new MemoryStream();
        _responseExporter.ExportSiroXmlAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var metadata = ValidMetadata();
        metadata.AdditionalFieldConflicts = new List<string> { "RfcList" };

        await _service.ExportSiroXmlAsync(metadata, stream, cancellationToken: TestContext.Current.CancellationToken);

        // Each conflict yields a "Conflict:{conflict}" warning (kills the prefix string + loop).
        metadata.Validation!.Warnings.ShouldContain("Conflict:RfcList");
    }
}
