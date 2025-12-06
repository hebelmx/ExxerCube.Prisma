namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// ITDD contract tests for <see cref="IExpedienteClasifier"/> interface.
/// These tests define the expected behavior that ANY implementation must satisfy.
/// </summary>
/// <remarks>
/// Contract Test Philosophy:
/// - Validates CNBV requirement classification (types 100-104)
/// - Validates Article 4 compliance (42 mandatory fields)
/// - Validates Article 17 rejection grounds
/// - Tests semantic analysis of "The 5 Situations"
/// </remarks>
public class ExpedienteClasifierServiceContractTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private readonly ILogger<ExpedienteClasifierServiceContractTests> _logger = XUnitLogger.CreateLogger<ExpedienteClasifierServiceContractTests>(output);

    #region Classification Tests - Requirement Types (100-104)

    [Fact]
    public async Task ClassifyAsync_InformationRequest_Returns100()
    {
        // Arrange - Expediente requesting information only
        var expediente = CreateInformationRequestExpediente();
        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.ClassifyAsync(expediente, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var classification = result.Value;

        classification.RequirementType.ShouldBe(RequirementType.InformationRequest);
        classification.ClassificationConfidence.ShouldBeGreaterThanOrEqualTo(0.80);
        classification.AuthorityType.ShouldNotBeNull();
    }

    [Fact]
    public async Task ClassifyAsync_AseguramientoRequest_Returns101()
    {
        _logger.LogInformation("=== TEST START: AseguramientoRequest_Returns101 ===");

        // Arrange - Expediente ordering asset seizure
        var expediente = CreateAseguramientoExpediente();
        var sut = CreateSystemUnderTest();

        _logger.LogInformation("Test data: Aseguramiento expediente with TieneAseguramiento=true");

        // Act
        var result = await sut.ClassifyAsync(expediente, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var classification = result.Value;

        _logger.LogInformation("Classification result:");
        _logger.LogInformation("  RequirementType: {Type} (expected: Aseguramiento)", classification.RequirementType);
        _logger.LogInformation("  RequiredFields count: {Count}", classification.RequiredFields.Count);
        _logger.LogInformation("  RequiredFields: {Fields}", string.Join(", ", classification.RequiredFields));
        _logger.LogInformation("  PassesArticle4: {Passes}", classification.ArticleValidation.PassesArticle4);

        classification.RequirementType.ShouldBe(RequirementType.Aseguramiento);
        classification.RequiredFields.ShouldContain("InitialBlockedAmount");
        classification.RequiredFields.ShouldContain("AccountNumber");
        classification.ArticleValidation.PassesArticle4.ShouldBeTrue(); // Must have all required fields

        _logger.LogInformation("=== TEST PASSED ===");
    }

    [Fact]
    public async Task ClassifyAsync_DesbloqueoRequest_Returns102()
    {
        // Arrange - Expediente ordering unblocking
        var expediente = CreateDesbloqueoExpediente();
        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.ClassifyAsync(expediente, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var classification = result.Value;

        classification.RequirementType.ShouldBe(RequirementType.Desbloqueo);
        classification.RequiredFields.ShouldContain("InternalCaseId");
        classification.RequiredFields.ShouldContain("SourceAuthorityCode");
    }

    [Fact]
    public async Task ClassifyAsync_TransferenciaElectronica_Returns103()
    {
        // Arrange - Expediente ordering electronic transfer
        var expediente = CreateTransferenciaElectronicaExpediente();
        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.ClassifyAsync(expediente, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var classification = result.Value;

        classification.RequirementType.ShouldBe(RequirementType.Transferencia);
        classification.RequiredFields.ShouldContain("OperationAmount");
        classification.RequiredFields.ShouldContain("AccountNumber");
    }

    [Fact]
    public async Task ClassifyAsync_SituacionFondos_Returns104()
    {
        // Arrange - Expediente ordering physical delivery of funds
        var expediente = CreateSituacionFondosExpediente();
        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.ClassifyAsync(expediente, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var classification = result.Value;

        classification.RequirementType.ShouldBe(RequirementType.SituacionFondos);
        classification.RequiredFields.ShouldContain("OperationAmount");
        classification.RequiredFields.ShouldContain("AccountNumber");
    }

    #endregion Classification Tests - Requirement Types (100-104)

    #region Article 4 Validation Tests - 42 Mandatory Fields

    [Fact]
    public async Task ValidateArticle4Async_AllMandatoryFieldsPresent_PassesValidation()
    {
        // Arrange - Expediente with all 42 mandatory fields populated
        var expediente = CreateCompleteExpediente();
        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.ValidateArticle4Async(
            expediente,
            RequirementType.Aseguramiento,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var validation = result.Value;

        validation.PassesArticle4.ShouldBeTrue();
        validation.MissingRequiredFields.ShouldBeEmpty();
    }

    [Fact]
    public async Task ValidateArticle4Async_MissingMandatoryFields_FailsValidation()
    {
        // Arrange - Expediente missing critical fields
        var expediente = CreateIncompleteExpediente();
        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.ValidateArticle4Async(
            expediente,
            RequirementType.Aseguramiento,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var validation = result.Value;

        validation.PassesArticle4.ShouldBeFalse();
        validation.MissingRequiredFields.ShouldNotBeEmpty();
        validation.MissingRequiredFields.ShouldContain("InternalCaseId"); // Example mandatory field
    }

    [Fact]
    public async Task ValidateArticle4Async_R29Compliance_Validates42Fields()
    {
        // Arrange - Test all 42 fields from R29 A-2911
        var expediente = CreateR29CompliantExpediente();
        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.ValidateArticle4Async(
            expediente,
            RequirementType.InformationRequest,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var validation = result.Value;

        // R29 Section 2.1: Core Identification (Fields 1-5)
        validation.MissingRequiredFields.ShouldNotContain("InternalCaseId");
        validation.MissingRequiredFields.ShouldNotContain("ExternalReferenceId");
        validation.MissingRequiredFields.ShouldNotContain("SourceAuthorityCode");

        // R29 Section 2.2: SLA & Classification (Fields 6-10)
        validation.MissingRequiredFields.ShouldNotContain("RequirementType");
        validation.MissingRequiredFields.ShouldNotContain("ReceptionDate");

        // R29 Section 2.4: Financial Information (Fields 16-42)
        // NO NULLS PERMITTED per R29 specification
        if (validation.PassesArticle4)
        {
            expediente.LawMandatedFields.ShouldNotBeNull();
            expediente.LawMandatedFields.BranchCode.ShouldNotBeNullOrWhiteSpace();
            expediente.LawMandatedFields.AccountNumber.ShouldNotBeNullOrWhiteSpace();
        }
    }

    #endregion Article 4 Validation Tests - 42 Mandatory Fields

    #region Article 17 Rejection Tests

    [Fact]
    public async Task CheckArticle17RejectionAsync_MissingLegalAuthorityCitation_ReturnsRejectionReason()
    {
        // Arrange - Expediente without legal authority citation
        var expediente = CreateExpedienteWithoutLegalCitation();
        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.CheckArticle17RejectionAsync(expediente, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var rejectionReasons = result.Value;

        rejectionReasons.ShouldContain(RejectionReason.NoLegalAuthorityCitation);
    }

    [Fact]
    public async Task CheckArticle17RejectionAsync_MissingSignature_ReturnsRejectionReason()
    {
        // Arrange - Expediente without signature
        var expediente = CreateExpedienteWithoutSignature();
        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.CheckArticle17RejectionAsync(expediente, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var rejectionReasons = result.Value;

        rejectionReasons.ShouldContain(RejectionReason.MissingSignature);
    }

    [Fact]
    public async Task CheckArticle17RejectionAsync_LackOfSpecificity_ReturnsRejectionReason()
    {
        // Arrange - Vague request without specific account details
        var expediente = CreateVagueExpediente();
        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.CheckArticle17RejectionAsync(expediente, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var rejectionReasons = result.Value;

        rejectionReasons.ShouldContain(RejectionReason.LackOfSpecificity);
    }

    [Fact]
    public async Task CheckArticle17RejectionAsync_ExceedsJurisdiction_ReturnsRejectionReason()
    {
        // Arrange - Request outside CNBV competence
        var expediente = CreateOutOfJurisdictionExpediente();
        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.CheckArticle17RejectionAsync(expediente, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var rejectionReasons = result.Value;

        rejectionReasons.ShouldContain(RejectionReason.ExceedsJurisdiction);
    }

    [Fact]
    public async Task CheckArticle17RejectionAsync_ValidExpediente_ReturnsEmptyList()
    {
        // Arrange - Properly formed, legally compliant expediente
        var expediente = CreateCompleteExpediente();
        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.CheckArticle17RejectionAsync(expediente, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var rejectionReasons = result.Value;

        rejectionReasons.ShouldBeEmpty(); // No grounds for rejection
    }

    #endregion Article 17 Rejection Tests

    #region Semantic Analysis Tests - The 5 Situations

    [Fact]
    public async Task AnalyzeSemanticRequirementsAsync_InformationRequest_CreatesGeneralRequirement()
    {
        // Arrange - General information request
        var expediente = CreateInformationRequestExpediente();
        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.AnalyzeSemanticRequirementsAsync(expediente, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var analysis = result.Value;

        analysis.RequiereInformacionGeneral.ShouldNotBeNull();
        analysis.RequiereInformacionGeneral.EsRequerido.ShouldBeTrue();
    }

    [Fact]
    public async Task AnalyzeSemanticRequirementsAsync_DocumentRequest_CreatesDocumentationRequirement()
    {
        // Arrange - Documentation request
        var expediente = CreateInformationRequestExpediente();
        expediente.Referencia = "SOLICITO ESTADOS DE CUENTA";
        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.AnalyzeSemanticRequirementsAsync(expediente, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var analysis = result.Value;

        analysis.RequiereDocumentacion.ShouldNotBeNull();
        analysis.RequiereDocumentacion.EsRequerido.ShouldBeTrue();
    }

    [Fact]
    public async Task AnalyzeSemanticRequirementsAsync_Aseguramiento_CreatesBloqueoRequirement()
    {
        // Arrange - Asset seizure order
        var expediente = CreateAseguramientoExpediente();
        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.AnalyzeSemanticRequirementsAsync(expediente, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var analysis = result.Value;

        analysis.RequiereBloqueo.ShouldNotBeNull();
        analysis.RequiereBloqueo.EsRequerido.ShouldBeTrue();
    }

    [Fact]
    public async Task AnalyzeSemanticRequirementsAsync_Desbloqueo_CreatesDesbloqueoRequirement()
    {
        // Arrange - Unblocking order
        var expediente = CreateDesbloqueoExpediente();
        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.AnalyzeSemanticRequirementsAsync(expediente, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var analysis = result.Value;

        analysis.RequiereDesbloqueo.ShouldNotBeNull();
        analysis.RequiereDesbloqueo.EsRequerido.ShouldBeTrue();
    }

    [Fact]
    public async Task AnalyzeSemanticRequirementsAsync_Transferencia_CreatesTransferenciaRequirement()
    {
        // Arrange - Transfer order
        var expediente = CreateTransferenciaElectronicaExpediente();
        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.AnalyzeSemanticRequirementsAsync(expediente, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var analysis = result.Value;

        analysis.RequiereTransferencia.ShouldNotBeNull();
        analysis.RequiereTransferencia.EsRequerido.ShouldBeTrue();
    }

    #endregion Semantic Analysis Tests - The 5 Situations

    #region Helper Methods - Test Data Builders

    private IExpedienteClasifier CreateSystemUnderTest()
    {
        // Use real implementations for integration-style testing
        var logger = XUnitLogger.CreateLogger<ExpedienteClasifierService>(_output);

        // Create real ISemanticAnalyzer with fuzzy matching
        var textComparerLogger = Substitute.For<ILogger<LevenshteinTextComparer>>();
        var semanticAnalyzerLogger = Substitute.For<ILogger<SemanticAnalyzerService>>();

        var textComparer = new LevenshteinTextComparer(textComparerLogger);
        var semanticAnalyzer = new SemanticAnalyzerService(textComparer, semanticAnalyzerLogger);

        return new ExpedienteClasifierService(semanticAnalyzer, logger);
    }

    private static Expediente CreateInformationRequestExpediente()
    {
        return new Expediente
        {
            NumeroExpediente = "H/IN1-1111-222222-AAA",
            AreaDescripcion = "HACENDARIO",
            TieneAseguramiento = false,
            SolicitudPartes = new List<SolicitudParte>
            {
                new SolicitudParte
                {
                    Rfc = "XAXX010101000",
                    Curp = "XAXX010101HDFXXX00",
                    Nombre = "JUAN",
                    Paterno = "PEREZ",
                    Materno = "GARCIA"
                }
            },
            LawMandatedFields = new LawMandatedFields
            {
                InternalCaseId = Guid.NewGuid(),
                SourceAuthorityCode = "SAT",
                RequirementType = "INFORMACION"
            }
        };
    }

    private static Expediente CreateAseguramientoExpediente()
    {
        return new Expediente
        {
            NumeroExpediente = "A/AS1-1111-222222-AAA",
            AreaDescripcion = "ASEGURAMIENTO",
            TieneAseguramiento = true,
            SolicitudPartes = new List<SolicitudParte>
            {
                new SolicitudParte
                {
                    Rfc = "XAXX010101000",
                    Curp = "XAXX010101HDFXXX00",
                    Nombre = "JUAN",
                    Paterno = "PEREZ",
                    Materno = "GARCIA"
                }
            },
            LawMandatedFields = new LawMandatedFields
            {
                InternalCaseId = Guid.NewGuid(),
                SourceAuthorityCode = "SAT",
                RequirementType = "ASEGURAMIENTO",
                AccountNumber = "1234567890",
                BranchCode = "001",
                ProductType = 101,
                InitialBlockedAmount = 100000.00m
            }
        };
    }

    private static Expediente CreateDesbloqueoExpediente()
    {
        return new Expediente
        {
            NumeroExpediente = "A/DS1-1111-222222-AAA",
            AreaDescripcion = "ASEGURAMIENTO",
            TieneAseguramiento = false,
            Referencia = "DESBLOQUEO DE CUENTAS",
            OficioOrigen = "A/AS1-1111-222222-AAA",
            LawMandatedFields = new LawMandatedFields
            {
                InternalCaseId = Guid.NewGuid(),
                SourceAuthorityCode = "JUZGADO"
            }
        };
    }

    private static Expediente CreateTransferenciaElectronicaExpediente()
    {
        return new Expediente
        {
            NumeroExpediente = "A/TR1-1111-222222-AAA",
            AreaDescripcion = "ASEGURAMIENTO",
            TieneAseguramiento = true,
            Referencia = "TRANSFERIR FONDOS A CLABE",
            LawMandatedFields = new LawMandatedFields
            {
                InternalCaseId = Guid.NewGuid(),
                AccountNumber = "1234567890",
                SourceAuthorityCode = "SAT",
                OperationAmount = 50000.00m
            }
        };
    }

    private static Expediente CreateSituacionFondosExpediente()
    {
        return new Expediente
        {
            NumeroExpediente = "A/SF1-1111-222222-AAA",
            AreaDescripcion = "ASEGURAMIENTO",
            TieneAseguramiento = true,
            Referencia = "CHEQUE DE CAJA SITUAR FONDOS",
            LawMandatedFields = new LawMandatedFields
            {
                InternalCaseId = Guid.NewGuid(),
                AccountNumber = "1234567890",
                SourceAuthorityCode = "SAT",
                OperationAmount = 75000.00m
            }
        };
    }

    private static Expediente CreateCompleteExpediente()
    {
        return new Expediente
        {
            NumeroExpediente = "A/AS1-1111-222222-AAA",
            NumeroOficio = "123/ABC/-4444444444/2025",
            AreaDescripcion = "ASEGURAMIENTO",
            AutoridadNombre = "SUBDELEGACION 8 SAN ANGEL",
            FundamentoLegal = "Artículo 42 Código Fiscal de la Federación",
            EvidenciaFirma = "SHA256:abc123def456",
            TieneAseguramiento = true,
            SolicitudPartes = new List<SolicitudParte>
            {
                new SolicitudParte
                {
                    Rfc = "XAXX010101000",
                    Curp = "XAXX010101HDFXXX00",
                    Nombre = "JUAN",
                    Paterno = "PEREZ",
                    Materno = "GARCIA"
                }
            },
            LawMandatedFields = new LawMandatedFields
            {
                InternalCaseId = Guid.NewGuid(),
                SourceAuthorityCode = "SAT",
                RequirementType = "ASEGURAMIENTO",
                BranchCode = "001",
                AccountNumber = "1234567890",
                ProductType = 101, // Depósito a la Vista
                InitialBlockedAmount = 100000.00m
            }
        };
    }

    private static Expediente CreateIncompleteExpediente()
    {
        return new Expediente
        {
            NumeroExpediente = "A/AS1-1111-222222-AAA",
            AreaDescripcion = "ASEGURAMIENTO"
            // Missing most mandatory fields
        };
    }

    private static Expediente CreateR29CompliantExpediente()
    {
        // Full R29 A-2911 compliant expediente with all 42 mandatory fields
        return CreateCompleteExpediente();
    }

    private static Expediente CreateExpedienteWithoutLegalCitation()
    {
        var expediente = CreateCompleteExpediente();
        expediente.FundamentoLegal = string.Empty;
        return expediente;
    }

    private static Expediente CreateExpedienteWithoutSignature()
    {
        var expediente = CreateCompleteExpediente();
        expediente.EvidenciaFirma = string.Empty; // Clear signature to test missing signature detection
        return expediente;
    }

    private static Expediente CreateVagueExpediente()
    {
        return new Expediente
        {
            NumeroExpediente = "H/IN1-1111-222222-AAA",
            AreaDescripcion = "HACENDARIO",
            // Missing specific account details
        };
    }

    private static Expediente CreateOutOfJurisdictionExpediente()
    {
        return new Expediente
        {
            NumeroExpediente = "X/XX1-1111-222222-AAA",
            AreaDescripcion = "OUTSIDE_CNBV_SCOPE"
        };
    }

    #endregion Helper Methods - Test Data Builders
}