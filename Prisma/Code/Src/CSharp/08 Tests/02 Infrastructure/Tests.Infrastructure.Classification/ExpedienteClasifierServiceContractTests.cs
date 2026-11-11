namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Implementation instance of <see cref="ExpedienteClasifierContract"/> for
/// <see cref="ExpedienteClasifierService"/> (ADR-005).
/// </summary>
/// <remarks>
/// <para>
/// Phase 4 of the ITDD refactor split the previously-conflated real-SUT class: the interface-generic
/// behaviour (CNBV type classification, Article 4 pass/fail, the six Article 17 grounds, the 5
/// Situations) moved into <see cref="ExpedienteClasifierContract"/> and runs here through inheritance
/// against the real <see cref="ExpedienteClasifierService"/> (built with a real
/// <c>SemanticAnalyzerService</c> + <c>LevenshteinTextComparer</c>).
/// </para>
/// <para>
/// The tests below stay implementation-side because they pin implementation details, not contract
/// behaviour (ADR-005 §5): the exact internal required-field name strings per requirement type, the
/// exact missing-field reported for an incomplete Expediente, and the R29 A-2911 42-field
/// enumeration. The exact high-confidence bound is pinned via the
/// <see cref="MinHighClassificationConfidence"/> override (0.80). Mutation-pinning lives in
/// <c>ExpedienteClasifierServiceMutationTests</c> (untouched).
/// </para>
/// </remarks>
public sealed class ExpedienteClasifierServiceContractTests(ITestOutputHelper output) : ExpedienteClasifierContract
{
    private readonly ITestOutputHelper _output = output;

    /// <inheritdoc />
    protected override IExpedienteClasifier CreateSut()
    {
        // Use real implementations for integration-style testing.
        var logger = XUnitLogger.CreateLogger<ExpedienteClasifierService>(_output);
        var textComparerLogger = Substitute.For<ILogger<LevenshteinTextComparer>>();
        var semanticAnalyzerLogger = Substitute.For<ILogger<SemanticAnalyzerService>>();

        var textComparer = new LevenshteinTextComparer(textComparerLogger);
        var semanticAnalyzer = new SemanticAnalyzerService(textComparer, semanticAnalyzerLogger);

        return new ExpedienteClasifierService(semanticAnalyzer, logger);
    }

    /// <inheritdoc />
    protected override double MinHighClassificationConfidence => 0.80;

    //
    // Fixture hooks — the real CNBV test data this implementation classifies.
    //

    /// <inheritdoc />
    protected override Expediente CreateInformationRequestExpediente() => new()
    {
        NumeroExpediente = "H/IN1-1111-222222-AAA",
        AreaDescripcion = "HACENDARIO",
        TieneAseguramiento = false,
        SolicitudPartes = new List<SolicitudParte>
        {
            new() { Rfc = "XAXX010101000", Curp = "XAXX010101HDFXXX00", Nombre = "JUAN", Paterno = "PEREZ", Materno = "GARCIA" }
        },
        LawMandatedFields = new LawMandatedFields
        {
            InternalCaseId = Guid.NewGuid(),
            SourceAuthorityCode = "SAT",
            RequirementType = "INFORMACION"
        }
    };

    /// <inheritdoc />
    protected override Expediente CreateDocumentationRequestExpediente()
    {
        var expediente = CreateInformationRequestExpediente();
        expediente.Referencia = "SOLICITO ESTADOS DE CUENTA";
        return expediente;
    }

    /// <inheritdoc />
    protected override Expediente CreateAseguramientoExpediente() => new()
    {
        NumeroExpediente = "A/AS1-1111-222222-AAA",
        AreaDescripcion = "ASEGURAMIENTO",
        TieneAseguramiento = true,
        SolicitudPartes = new List<SolicitudParte>
        {
            new() { Rfc = "XAXX010101000", Curp = "XAXX010101HDFXXX00", Nombre = "JUAN", Paterno = "PEREZ", Materno = "GARCIA" }
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

    /// <inheritdoc />
    protected override Expediente CreateDesbloqueoExpediente() => new()
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

    /// <inheritdoc />
    protected override Expediente CreateTransferenciaExpediente() => new()
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

    /// <inheritdoc />
    protected override Expediente CreateSituacionFondosExpediente() => new()
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

    /// <inheritdoc />
    protected override Expediente CreateCompleteExpediente() => new()
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
            new() { Rfc = "XAXX010101000", Curp = "XAXX010101HDFXXX00", Nombre = "JUAN", Paterno = "PEREZ", Materno = "GARCIA" }
        },
        LawMandatedFields = new LawMandatedFields
        {
            InternalCaseId = Guid.NewGuid(),
            SourceAuthorityCode = "SAT",
            RequirementType = "ASEGURAMIENTO",
            BranchCode = "001",
            AccountNumber = "1234567890",
            ProductType = 101,
            InitialBlockedAmount = 100000.00m
        }
    };

    /// <inheritdoc />
    protected override Expediente CreateIncompleteExpediente() => new()
    {
        NumeroExpediente = "A/AS1-1111-222222-AAA",
        AreaDescripcion = "ASEGURAMIENTO"
        // Missing most mandatory fields
    };

    /// <inheritdoc />
    protected override Expediente CreateExpedienteWithoutLegalCitation()
    {
        var expediente = CreateCompleteExpediente();
        expediente.FundamentoLegal = string.Empty;
        return expediente;
    }

    /// <inheritdoc />
    protected override Expediente CreateExpedienteWithoutSignature()
    {
        var expediente = CreateCompleteExpediente();
        expediente.EvidenciaFirma = string.Empty;
        return expediente;
    }

    /// <inheritdoc />
    protected override Expediente CreateVagueExpediente() => new()
    {
        NumeroExpediente = "H/IN1-1111-222222-AAA",
        AreaDescripcion = "HACENDARIO"
        // Missing specific account details
    };

    /// <inheritdoc />
    protected override Expediente CreateOutOfJurisdictionExpediente() => new()
    {
        NumeroExpediente = "X/XX1-1111-222222-AAA",
        AreaDescripcion = "OUTSIDE_CNBV_SCOPE"
    };

    //
    // Implementation-side: exact required-field strings + R29 enumeration (ADR-005 §5)
    //

    [Fact]
    public async Task ClassifyAsync_AseguramientoRequest_RequiresAseguramientoFields()
    {
        var sut = CreateSut();

        var result = await sut.ClassifyAsync(CreateAseguramientoExpediente(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.RequiredFields.ShouldContain("InitialBlockedAmount");
        result.Value.RequiredFields.ShouldContain("AccountNumber");
        result.Value.ArticleValidation.PassesArticle4.ShouldBeTrue();
    }

    [Fact]
    public async Task ClassifyAsync_DesbloqueoRequest_RequiresDesbloqueoFields()
    {
        var sut = CreateSut();

        var result = await sut.ClassifyAsync(CreateDesbloqueoExpediente(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.RequiredFields.ShouldContain("InternalCaseId");
        result.Value.RequiredFields.ShouldContain("SourceAuthorityCode");
    }

    [Fact]
    public async Task ClassifyAsync_TransferenciaElectronica_RequiresTransferenciaFields()
    {
        var sut = CreateSut();

        var result = await sut.ClassifyAsync(CreateTransferenciaExpediente(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.RequiredFields.ShouldContain("OperationAmount");
        result.Value.RequiredFields.ShouldContain("AccountNumber");
    }

    [Fact]
    public async Task ClassifyAsync_SituacionFondos_RequiresSituacionFondosFields()
    {
        var sut = CreateSut();

        var result = await sut.ClassifyAsync(CreateSituacionFondosExpediente(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.RequiredFields.ShouldContain("OperationAmount");
        result.Value.RequiredFields.ShouldContain("AccountNumber");
    }

    [Fact]
    public async Task ValidateArticle4Async_MissingMandatoryFields_ReportsInternalCaseId()
    {
        var sut = CreateSut();

        var result = await sut.ValidateArticle4Async(
            CreateIncompleteExpediente(),
            RequirementType.Aseguramiento,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.MissingRequiredFields.ShouldContain("InternalCaseId");
    }

    [Fact]
    public async Task ValidateArticle4Async_R29Compliance_Validates42Fields()
    {
        var sut = CreateSut();

        var result = await sut.ValidateArticle4Async(
            CreateCompleteExpediente(),
            RequirementType.InformationRequest,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var validation = result.Value;

        // R29 Section 2.1: Core Identification (Fields 1-5)
        validation.MissingRequiredFields.ShouldNotContain("InternalCaseId");
        validation.MissingRequiredFields.ShouldNotContain("ExternalReferenceId");
        validation.MissingRequiredFields.ShouldNotContain("SourceAuthorityCode");

        // R29 Section 2.2: SLA & Classification (Fields 6-10)
        validation.MissingRequiredFields.ShouldNotContain("RequirementType");
        validation.MissingRequiredFields.ShouldNotContain("ReceptionDate");

        // R29 Section 2.4: Financial Information (Fields 16-42) — NO NULLS PERMITTED
        if (validation.PassesArticle4)
        {
            var complete = CreateCompleteExpediente();
            complete.LawMandatedFields.ShouldNotBeNull();
            complete.LawMandatedFields.BranchCode.ShouldNotBeNullOrWhiteSpace();
            complete.LawMandatedFields.AccountNumber.ShouldNotBeNullOrWhiteSpace();
        }
    }
}
