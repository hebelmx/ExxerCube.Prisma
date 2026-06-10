namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Mutation-killing tests for <see cref="ExpedienteClasifierService"/>, driven through a mocked
/// <see cref="ISemanticAnalyzer"/> so the service's own deterministic logic is isolated: the
/// requirement-type ladder + confidences, the authority switch, the required-field map, the
/// Article-4 reflection validator, the Article-17 rejection grounds, and the semantic enrichment +
/// document-text inference.
/// </summary>
public class ExpedienteClasifierServiceMutationTests
{
    private readonly ISemanticAnalyzer _analyzer = Substitute.For<ISemanticAnalyzer>();
    private readonly ExpedienteClasifierService _service;
    private string? _capturedText;

    public ExpedienteClasifierServiceMutationTests(ITestOutputHelper output)
    {
        _service = new ExpedienteClasifierService(_analyzer, XUnitLogger.CreateLogger<ExpedienteClasifierService>(output));
        _analyzer.AnalyzeDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente?>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                _capturedText = ci.ArgAt<string>(0);
                return Result<SemanticAnalysis>.Success(new SemanticAnalysis());
            });
    }

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    // =================== ClassifyRequirementType ladder (via ClassifyAsync) ===================

    [Theory]
    [InlineData("DESBLOQUEO de cuentas")]
    [InlineData("se ordena LIBERAR fondos")]
    [InlineData("LEVANTAR el aseguramiento")]
    public async Task Classify_DesbloqueoKeywords_ReturnsDesbloqueo_095(string referencia)
    {
        var result = await _service.ClassifyAsync(new Expediente { Referencia = referencia }, Ct);

        result.Value!.RequirementType.ShouldBe(RequirementType.Desbloqueo);
        result.Value!.ClassificationConfidence.ShouldBe(0.95);
    }

    [Fact]
    public async Task Classify_TieneAseguramiento_ReturnsAseguramiento_090()
    {
        var result = await _service.ClassifyAsync(new Expediente { TieneAseguramiento = true }, Ct);

        result.Value!.RequirementType.ShouldBe(RequirementType.Aseguramiento);
        result.Value!.ClassificationConfidence.ShouldBe(0.90);
    }

    [Fact]
    public async Task Classify_AreaAseguramiento_ReturnsAseguramiento()
    {
        var result = await _service.ClassifyAsync(new Expediente { AreaDescripcion = "ASEGURAMIENTO" }, Ct);

        result.Value!.RequirementType.ShouldBe(RequirementType.Aseguramiento);
    }

    [Fact]
    public async Task Classify_ExpedienteAsToken_ReturnsAseguramiento()
    {
        var result = await _service.ClassifyAsync(new Expediente { NumeroExpediente = "A/AS1-0000" }, Ct);

        result.Value!.RequirementType.ShouldBe(RequirementType.Aseguramiento);
    }

    [Fact]
    public async Task Classify_AseguramientoWithTransferKeyword_ReturnsTransferencia_090()
    {
        var result = await _service.ClassifyAsync(
            new Expediente { TieneAseguramiento = true, Referencia = "TRANSFERIR a otra cuenta" }, Ct);

        result.Value!.RequirementType.ShouldBe(RequirementType.Transferencia);
        result.Value!.ClassificationConfidence.ShouldBe(0.90);
    }

    [Fact]
    public async Task Classify_AseguramientoWithChequeKeyword_ReturnsSituacionFondos_090()
    {
        var result = await _service.ClassifyAsync(
            new Expediente { TieneAseguramiento = true, Referencia = "emitir CHEQUE de caja" }, Ct);

        result.Value!.RequirementType.ShouldBe(RequirementType.SituacionFondos);
        result.Value!.ClassificationConfidence.ShouldBe(0.90);
    }

    [Fact]
    public async Task Classify_TransferenciaKeyword_NoAseguramiento_Returns085()
    {
        var result = await _service.ClassifyAsync(new Expediente { Referencia = "ordena CLABE interbancaria" }, Ct);

        result.Value!.RequirementType.ShouldBe(RequirementType.Transferencia);
        result.Value!.ClassificationConfidence.ShouldBe(0.85);
    }

    [Fact]
    public async Task Classify_FondosKeyword_NoAseguramiento_ReturnsSituacionFondos_085()
    {
        var result = await _service.ClassifyAsync(new Expediente { Referencia = "situar los FONDOS" }, Ct);

        result.Value!.RequirementType.ShouldBe(RequirementType.SituacionFondos);
        result.Value!.ClassificationConfidence.ShouldBe(0.85);
    }

    [Fact]
    public async Task Classify_NoKeywords_DefaultsToInformationRequest_080()
    {
        var result = await _service.ClassifyAsync(new Expediente { Referencia = "saludos cordiales" }, Ct);

        result.Value!.RequirementType.ShouldBe(RequirementType.InformationRequest);
        result.Value!.ClassificationConfidence.ShouldBe(0.80);
    }

    // =================== DetermineAuthorityKind (via ClassifyAsync) ===================

    [Theory]
    [InlineData("JUZGADO PRIMERO", "Juzgado")]
    [InlineData("TRIBUNAL COLEGIADO", "Juzgado")]
    [InlineData("MAGISTRADO PONENTE", "Juzgado")]
    [InlineData("SAT ADMINISTRACION", "Hacienda")]
    [InlineData("FGR DELEGACION", "Hacienda")]
    [InlineData("MINISTERIO FISCAL", "Hacienda")]
    [InlineData("HACIENDA PUBLICA", "Hacienda")]
    [InlineData("UIF MEXICO", "UIF")]
    [InlineData("CNBV SUPERVISION", "CNBV")]
    [InlineData("BANCO PRIVADO", "Other")]
    public async Task Classify_AuthorityName_MapsToExactKind(string nombre, string expectedKind)
    {
        var result = await _service.ClassifyAsync(new Expediente { AutoridadNombre = nombre }, Ct);

        result.Value!.AuthorityType.Name.ShouldBe(expectedKind);
    }

    // =================== GetRequiredFields (via ClassifyAsync) ===================

    [Fact]
    public async Task Classify_Aseguramiento_RequiredFieldsExact()
    {
        var result = await _service.ClassifyAsync(new Expediente { TieneAseguramiento = true }, Ct);

        var fields = result.Value!.RequiredFields;
        fields.Count.ShouldBe(7);
        fields.ShouldContain("AccountNumber");
        fields.ShouldContain("InitialBlockedAmount");
        fields.ShouldContain("ProductType");
    }

    [Fact]
    public async Task Classify_InformationRequest_RequiredFieldsExact()
    {
        var result = await _service.ClassifyAsync(new Expediente { Referencia = "nada" }, Ct);

        var fields = result.Value!.RequiredFields;
        fields.Count.ShouldBe(3);
        fields.ShouldContain("InternalCaseId");
        fields.ShouldContain("SourceAuthorityCode");
        fields.ShouldContain("RequirementType");
    }

    // =================== ValidateArticle4Async ===================

    [Fact]
    public async Task Article4_AllRequiredPresent_Passes()
    {
        var expediente = new Expediente
        {
            LawMandatedFields = new LawMandatedFields
            {
                InternalCaseId = Guid.NewGuid(),
                SourceAuthorityCode = "SAT",
                RequirementType = "ASEGURAMIENTO",
                AccountNumber = "123",
                BranchCode = "001",
                ProductType = 101,
                InitialBlockedAmount = 1000m,
            },
        };

        var result = await _service.ValidateArticle4Async(expediente, RequirementType.Aseguramiento, Ct);

        result.Value!.PassesArticle4.ShouldBeTrue();
        result.Value!.MissingRequiredFields.ShouldBeEmpty();
    }

    [Fact]
    public async Task Article4_MissingOneField_FailsWithThatField()
    {
        var expediente = new Expediente
        {
            LawMandatedFields = new LawMandatedFields
            {
                InternalCaseId = Guid.NewGuid(),
                SourceAuthorityCode = "SAT",
                RequirementType = "ASEGURAMIENTO",
                AccountNumber = "123",
                BranchCode = "001",
                ProductType = 101,
                // InitialBlockedAmount omitted
            },
        };

        var result = await _service.ValidateArticle4Async(expediente, RequirementType.Aseguramiento, Ct);

        result.Value!.PassesArticle4.ShouldBeFalse();
        result.Value!.MissingRequiredFields.ShouldContain("InitialBlockedAmount");
    }

    [Fact]
    public async Task Article4_NullLawFields_AllRequiredMissing()
    {
        var result = await _service.ValidateArticle4Async(new Expediente(), RequirementType.Desbloqueo, Ct);

        // Desbloqueo (102) requires exactly InternalCaseId + SourceAuthorityCode.
        result.Value!.PassesArticle4.ShouldBeFalse();
        result.Value!.MissingRequiredFields.Count.ShouldBe(2);
        result.Value!.MissingRequiredFields.ShouldContain("InternalCaseId");
        result.Value!.MissingRequiredFields.ShouldContain("SourceAuthorityCode");
    }

    [Fact]
    public async Task Article4_WhitespaceStringField_CountsAsMissing()
    {
        var expediente = new Expediente
        {
            LawMandatedFields = new LawMandatedFields
            {
                InternalCaseId = Guid.NewGuid(),
                SourceAuthorityCode = "   ", // whitespace → IsFieldPopulated false
            },
        };

        var result = await _service.ValidateArticle4Async(expediente, RequirementType.Desbloqueo, Ct);

        result.Value!.MissingRequiredFields.ShouldContain("SourceAuthorityCode");
        result.Value!.MissingRequiredFields.ShouldNotContain("InternalCaseId");
    }

    // =================== CheckArticle17RejectionAsync ===================

    private static Expediente FullyCompliant() => new()
    {
        FundamentoLegal = "Art 42 CFF",
        EvidenciaFirma = "SHA256:abc",
        AreaDescripcion = "ASEGURAMIENTO",
        LawMandatedFields = new LawMandatedFields { InternalCaseId = Guid.NewGuid(), AccountNumber = "123" },
        SolicitudPartes = new List<SolicitudParte> { new() { Rfc = "XAXX010101000" } },
    };

    [Fact]
    public async Task Article17_FullyCompliant_NoRejections()
    {
        var result = await _service.CheckArticle17RejectionAsync(FullyCompliant(), Ct);

        result.Value!.ShouldBeEmpty();
    }

    [Fact]
    public async Task Article17_BlankFundamentoLegal_FlagsNoLegalAuthorityCitation()
    {
        var e = FullyCompliant();
        e.FundamentoLegal = "";

        var result = await _service.CheckArticle17RejectionAsync(e, Ct);

        result.Value!.ShouldContain(RejectionReason.NoLegalAuthorityCitation);
        result.Value!.ShouldNotContain(RejectionReason.MissingSignature);
    }

    [Fact]
    public async Task Article17_BlankSignature_FlagsMissingSignature()
    {
        var e = FullyCompliant();
        e.EvidenciaFirma = "";

        var result = await _service.CheckArticle17RejectionAsync(e, Ct);

        result.Value!.ShouldContain(RejectionReason.MissingSignature);
    }

    [Fact]
    public async Task Article17_NoAccountAndNoRfcCurp_FlagsLackOfSpecificity()
    {
        var e = FullyCompliant();
        e.LawMandatedFields = new LawMandatedFields { InternalCaseId = Guid.NewGuid() }; // no AccountNumber
        e.SolicitudPartes = new List<SolicitudParte> { new() { Rfc = "", Curp = "" } };

        var result = await _service.CheckArticle17RejectionAsync(e, Ct);

        result.Value!.ShouldContain(RejectionReason.LackOfSpecificity);
    }

    [Fact]
    public async Task Article17_HasRfcButNoAccount_DoesNotFlagSpecificity()
    {
        var e = FullyCompliant();
        e.LawMandatedFields = new LawMandatedFields { InternalCaseId = Guid.NewGuid() }; // no account
        e.SolicitudPartes = new List<SolicitudParte> { new() { Rfc = "XAXX010101000" } };

        var result = await _service.CheckArticle17RejectionAsync(e, Ct);

        // hasRfcOrCurp true → `!hasAccount && !hasRfcOrCurp` false → not flagged (kills the && / negations).
        result.Value!.ShouldNotContain(RejectionReason.LackOfSpecificity);
    }

    [Fact]
    public async Task Article17_InvalidArea_FlagsExceedsJurisdiction()
    {
        var e = FullyCompliant();
        e.AreaDescripcion = "FUERA_DE_AMBITO";

        var result = await _service.CheckArticle17RejectionAsync(e, Ct);

        result.Value!.ShouldContain(RejectionReason.ExceedsJurisdiction);
    }

    [Theory]
    [InlineData("ASEGURAMIENTO")]
    [InlineData("HACENDARIO")]
    [InlineData("PENAL")]
    [InlineData("CIVIL")]
    [InlineData("ADMINISTRATIVO")]
    [InlineData("JUDICIAL")]
    public async Task Article17_ValidArea_NoJurisdictionRejection(string area)
    {
        var e = FullyCompliant();
        e.AreaDescripcion = area;

        var result = await _service.CheckArticle17RejectionAsync(e, Ct);

        result.Value!.ShouldNotContain(RejectionReason.ExceedsJurisdiction);
    }

    [Fact]
    public async Task Article17_NullInternalCaseId_FlagsMissingRequiredData()
    {
        var e = FullyCompliant();
        e.LawMandatedFields = new LawMandatedFields { AccountNumber = "123" }; // InternalCaseId null

        var result = await _service.CheckArticle17RejectionAsync(e, Ct);

        result.Value!.ShouldContain(RejectionReason.MissingRequiredData);
    }

    // =================== AnalyzeSemanticRequirementsAsync ===================

    [Fact]
    public async Task Analyze_AnalyzerFailure_PropagatesError()
    {
        _analyzer.AnalyzeDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SemanticAnalysis>.WithFailure("sem-broke"));

        var result = await _service.AnalyzeSemanticRequirementsAsync(new Expediente { Referencia = "x" }, Ct);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("sem-broke");
    }

    [Fact]
    public async Task Analyze_EnrichesBloqueoFromExpedienteMetadata()
    {
        _analyzer.AnalyzeDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SemanticAnalysis>.Success(new SemanticAnalysis
            {
                RequiereBloqueo = new BloqueoRequirement { EsRequerido = true },
            }));
        var expediente = new Expediente
        {
            Referencia = "aseguramiento",
            LawMandatedFields = new LawMandatedFields { InitialBlockedAmount = 5000m, Currency = "USD" },
        };

        var result = await _service.AnalyzeSemanticRequirementsAsync(expediente, Ct);

        var bloqueo = result.Value!.RequiereBloqueo!;
        bloqueo.EsParcial.ShouldBeTrue();      // InitialBlockedAmount != null
        bloqueo.Monto.ShouldBe(5000m);
        bloqueo.Moneda.ShouldBe("USD");
    }

    [Fact]
    public async Task Analyze_BloqueoEnrich_DefaultsCurrencyToMxn()
    {
        _analyzer.AnalyzeDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SemanticAnalysis>.Success(new SemanticAnalysis
            {
                RequiereBloqueo = new BloqueoRequirement { EsRequerido = true },
            }));
        var expediente = new Expediente
        {
            Referencia = "aseguramiento",
            LawMandatedFields = new LawMandatedFields { InitialBlockedAmount = null, Currency = null },
        };

        var result = await _service.AnalyzeSemanticRequirementsAsync(expediente, Ct);

        result.Value!.RequiereBloqueo!.EsParcial.ShouldBeFalse(); // InitialBlockedAmount == null
        result.Value!.RequiereBloqueo!.Moneda.ShouldBe("MXN");   // ?? "MXN"
    }

    [Fact]
    public async Task Analyze_EnrichesTransferenciaMonto()
    {
        _analyzer.AnalyzeDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SemanticAnalysis>.Success(new SemanticAnalysis
            {
                RequiereTransferencia = new TransferenciaRequirement { EsRequerido = true },
            }));
        var expediente = new Expediente
        {
            Referencia = "transferir",
            LawMandatedFields = new LawMandatedFields { OperationAmount = 7777m },
        };

        var result = await _service.AnalyzeSemanticRequirementsAsync(expediente, Ct);

        result.Value!.RequiereTransferencia!.Monto.ShouldBe(7777m);
    }

    [Fact]
    public async Task Analyze_EnrichesDesbloqueoOriginalExpediente()
    {
        _analyzer.AnalyzeDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SemanticAnalysis>.Success(new SemanticAnalysis
            {
                RequiereDesbloqueo = new DesbloqueoRequirement { EsRequerido = true },
            }));
        var expediente = new Expediente { Referencia = "desbloqueo", OficioOrigen = "OF-ORIG-1" };

        var result = await _service.AnalyzeSemanticRequirementsAsync(expediente, Ct);

        result.Value!.RequiereDesbloqueo!.ExpedienteBloqueoOriginal.ShouldBe("OF-ORIG-1");
    }

    // ----- InferDocumentTextFromMetadata (captured analyzer input) -----

    [Fact]
    public async Task Analyze_EmptyReferencias_TieneAseguramiento_InfersAseguramientoText()
    {
        await _service.AnalyzeSemanticRequirementsAsync(new Expediente { TieneAseguramiento = true }, Ct);

        _capturedText.ShouldBe("aseguramiento de fondos");
    }

    [Fact]
    public async Task Analyze_EmptyReferencias_AreaDesbloqueo_InfersDesbloqueoText()
    {
        await _service.AnalyzeSemanticRequirementsAsync(
            new Expediente { TieneAseguramiento = false, AreaDescripcion = "DESBLOQUEO DE CUENTA" }, Ct);

        _capturedText.ShouldBe("desbloqueo de cuenta");
    }

    [Fact]
    public async Task Analyze_EmptyReferencias_AreaAseguramiento_InfersAseguramientoText()
    {
        await _service.AnalyzeSemanticRequirementsAsync(
            new Expediente { TieneAseguramiento = false, AreaDescripcion = "ASEGURAMIENTO" }, Ct);

        _capturedText.ShouldBe("aseguramiento de fondos");
    }

    [Fact]
    public async Task Analyze_EmptyReferencias_NoMetadata_InfersInformationText()
    {
        await _service.AnalyzeSemanticRequirementsAsync(new Expediente { TieneAseguramiento = false }, Ct);

        _capturedText.ShouldBe("solicitud de información");
    }

    [Fact]
    public async Task Analyze_NonEmptyReferencias_UsesReferenciasNotInference()
    {
        await _service.AnalyzeSemanticRequirementsAsync(new Expediente { Referencia = "TEXTO REAL" }, Ct);

        // documentText is built from the referencias (trimmed), not the metadata inference.
        _capturedText.ShouldBe("TEXTO REAL");
    }
}
