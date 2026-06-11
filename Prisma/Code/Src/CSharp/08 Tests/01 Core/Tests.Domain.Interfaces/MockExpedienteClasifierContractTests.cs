namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Blueprint instance of <see cref="ExpedienteClasifierContract"/> — the mock-backed inheritor
/// that encodes the design specification (ADR-005 §6).
/// </summary>
/// <remarks>
/// Created in Phase 4 of the ITDD refactor when the conflated
/// <c>ExpedienteClasifierServiceContractTests</c> was split into a behavioural base + a real-impl
/// deriving class. There was no pre-existing mock blueprint (the class was always real-SUT), so this
/// blueprint is authored fresh: its fixtures carry the documented classification markers
/// (Referencia keywords, <c>TieneAseguramiento</c>, legal-formality fields) that
/// <see cref="ExpedienteClasifierMockFactory"/>'s reference fake reads.
/// </remarks>
public sealed class MockExpedienteClasifierContractTests : ExpedienteClasifierContract
{
    /// <inheritdoc />
    protected override IExpedienteClasifier CreateSut()
        => ExpedienteClasifierMockFactory.CreateContractConformingMock();

    /// <inheritdoc />
    protected override Expediente CreateInformationRequestExpediente() => new()
    {
        NumeroExpediente = "H/IN1-1111-222222-AAA",
        AreaDescripcion = "HACENDARIO",
        TieneAseguramiento = false,
        LawMandatedFields = new LawMandatedFields
        {
            InternalCaseId = Guid.NewGuid(),
            SourceAuthorityCode = "SAT",
            RequirementType = "INFORMACION",
        },
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
        LawMandatedFields = new LawMandatedFields
        {
            InternalCaseId = Guid.NewGuid(),
            SourceAuthorityCode = "SAT",
            AccountNumber = "1234567890",
            InitialBlockedAmount = 100000.00m,
        },
    };

    /// <inheritdoc />
    protected override Expediente CreateDesbloqueoExpediente() => new()
    {
        NumeroExpediente = "A/DS1-1111-222222-AAA",
        AreaDescripcion = "ASEGURAMIENTO",
        TieneAseguramiento = false,
        Referencia = "DESBLOQUEO DE CUENTAS",
        LawMandatedFields = new LawMandatedFields
        {
            InternalCaseId = Guid.NewGuid(),
            SourceAuthorityCode = "JUZGADO",
        },
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
        },
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
        },
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
        LawMandatedFields = new LawMandatedFields
        {
            InternalCaseId = Guid.NewGuid(),
            SourceAuthorityCode = "SAT",
            AccountNumber = "1234567890",
            InitialBlockedAmount = 100000.00m,
        },
    };

    /// <inheritdoc />
    protected override Expediente CreateIncompleteExpediente() => new()
    {
        NumeroExpediente = "A/AS1-1111-222222-AAA",
        AreaDescripcion = "ASEGURAMIENTO",
        // No LawMandatedFields → fails Article 4
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
        AreaDescripcion = "VAGUE",
        FundamentoLegal = "Artículo 42 Código Fiscal de la Federación",
        EvidenciaFirma = "SHA256:abc123def456",
    };

    /// <inheritdoc />
    protected override Expediente CreateOutOfJurisdictionExpediente() => new()
    {
        NumeroExpediente = "X/XX1-1111-222222-AAA",
        AreaDescripcion = "OUTSIDE_CNBV_SCOPE",
        FundamentoLegal = "Artículo 42 Código Fiscal de la Federación",
        EvidenciaFirma = "SHA256:abc123def456",
    };
}
