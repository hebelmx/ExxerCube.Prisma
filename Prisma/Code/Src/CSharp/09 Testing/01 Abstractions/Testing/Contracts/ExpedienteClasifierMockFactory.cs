using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Operations;
using NSubstitute;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Builds the contract-conforming NSubstitute mock that backs the blueprint instance
/// of <see cref="ExpedienteClasifierContract"/>.
/// </summary>
/// <remarks>
/// A small <em>reference fake</em> of the documented CNBV classification methodology: it reads the
/// keyword markers (Referencia) and the <c>TieneAseguramiento</c> flag to pick the requirement type
/// and the matching "5 Situations", and the legal-formality fields (FundamentoLegal, EvidenciaFirma,
/// AreaDescripcion) to derive the Article 17 grounds — exactly the signals the interface documents.
/// The blueprint supplies inputs carrying those markers via the fixture hooks, so the same behavioural
/// contract bodies pass against both this fake and the real <c>ExpedienteClasifierService</c>.
/// </remarks>
public static class ExpedienteClasifierMockFactory
{
    /// <summary>
    /// Creates an <see cref="IExpedienteClasifier"/> mock that satisfies every test in
    /// <see cref="ExpedienteClasifierContract"/>.
    /// </summary>
    /// <returns>The configured mock.</returns>
    public static IExpedienteClasifier CreateContractConformingMock()
    {
        var mock = Substitute.For<IExpedienteClasifier>();

        mock.ClassifyAsync(Arg.Any<Expediente>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CancellationToken>(1).IsCancellationRequested
                ? ResultExtensions.Cancelled<ExpedienteClassificationResult>()
                : Result<ExpedienteClassificationResult>.WithSuccess(Classify(call.ArgAt<Expediente>(0))));

        mock.ValidateArticle4Async(Arg.Any<Expediente>(), Arg.Any<RequirementType>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CancellationToken>(2).IsCancellationRequested
                ? ResultExtensions.Cancelled<ArticleValidationResult>()
                : Result<ArticleValidationResult>.WithSuccess(ValidateArticle4(call.ArgAt<Expediente>(0))));

        mock.CheckArticle17RejectionAsync(Arg.Any<Expediente>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CancellationToken>(1).IsCancellationRequested
                ? ResultExtensions.Cancelled<List<RejectionReason>>()
                : Result<List<RejectionReason>>.WithSuccess(CheckArticle17(call.ArgAt<Expediente>(0))));

        mock.AnalyzeSemanticRequirementsAsync(Arg.Any<Expediente>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CancellationToken>(1).IsCancellationRequested
                ? ResultExtensions.Cancelled<SemanticAnalysis>()
                : Result<SemanticAnalysis>.WithSuccess(AnalyzeSemantic(call.ArgAt<Expediente>(0))));

        return mock;
    }

    // Documented methodology: keyword markers + the TieneAseguramiento flag select the type.
    private static RequirementType ClassifyType(Expediente expediente)
    {
        var reference = (expediente.Referencia ?? string.Empty).ToUpperInvariant();

        if (reference.Contains("DESBLOQUEO"))
        {
            return RequirementType.Desbloqueo;
        }

        if (reference.Contains("TRANSFER"))
        {
            return RequirementType.Transferencia;
        }

        if (reference.Contains("SITUAR") || reference.Contains("CHEQUE"))
        {
            return RequirementType.SituacionFondos;
        }

        if (expediente.TieneAseguramiento)
        {
            return RequirementType.Aseguramiento;
        }

        return RequirementType.InformationRequest;
    }

    private static ExpedienteClassificationResult Classify(Expediente expediente)
    {
        var type = ClassifyType(expediente);

        return new ExpedienteClassificationResult
        {
            RequirementType = type,
            ClassificationConfidence = 0.9,
            AuthorityType = AuthorityKind.CNBV,
            RequiredFields = RequiredFieldsFor(type),
            ArticleValidation = ValidateArticle4(expediente),
            SemanticAnalysis = AnalyzeSemantic(expediente),
            RejectionReasons = CheckArticle17(expediente),
        };
    }

    private static List<string> RequiredFieldsFor(RequirementType type)
    {
        // The exact field-name strings are an implementation detail and are pinned in the deriving
        // implementation class; the contract only requires the list to be non-empty per type.
        if (type == RequirementType.InformationRequest)
        {
            return new List<string> { "InternalCaseId", "SourceAuthorityCode", "RequirementType" };
        }

        if (type == RequirementType.Aseguramiento)
        {
            return new List<string> { "InternalCaseId", "AccountNumber", "InitialBlockedAmount" };
        }

        if (type == RequirementType.Desbloqueo)
        {
            return new List<string> { "InternalCaseId", "SourceAuthorityCode" };
        }

        // Transferencia / SituacionFondos
        return new List<string> { "InternalCaseId", "AccountNumber", "OperationAmount" };
    }

    private static ArticleValidationResult ValidateArticle4(Expediente expediente)
    {
        var missing = new List<string>();

        if (expediente.LawMandatedFields?.InternalCaseId is null || expediente.LawMandatedFields.InternalCaseId == Guid.Empty)
        {
            missing.Add("InternalCaseId");
        }

        if (string.IsNullOrWhiteSpace(expediente.LawMandatedFields?.AccountNumber))
        {
            missing.Add("AccountNumber");
        }

        if (string.IsNullOrWhiteSpace(expediente.LawMandatedFields?.SourceAuthorityCode))
        {
            missing.Add("SourceAuthorityCode");
        }

        return new ArticleValidationResult
        {
            PassesArticle4 = missing.Count == 0,
            MissingRequiredFields = missing,
        };
    }

    private static List<RejectionReason> CheckArticle17(Expediente expediente)
    {
        var reasons = new List<RejectionReason>();

        if (string.IsNullOrWhiteSpace(expediente.FundamentoLegal))
        {
            reasons.Add(RejectionReason.NoLegalAuthorityCitation);
        }

        if (string.IsNullOrWhiteSpace(expediente.EvidenciaFirma))
        {
            reasons.Add(RejectionReason.MissingSignature);
        }

        var area = (expediente.AreaDescripcion ?? string.Empty).ToUpperInvariant();

        if (area.Contains("VAGUE"))
        {
            reasons.Add(RejectionReason.LackOfSpecificity);
        }

        if (area.Contains("OUTSIDE") || area.Contains("OUT_OF"))
        {
            reasons.Add(RejectionReason.ExceedsJurisdiction);
        }

        return reasons;
    }

    private static SemanticAnalysis AnalyzeSemantic(Expediente expediente)
    {
        var analysis = new SemanticAnalysis();
        var reference = (expediente.Referencia ?? string.Empty).ToUpperInvariant();

        if (reference.Contains("ESTADOS DE CUENTA") || reference.Contains("DOCUMENT"))
        {
            analysis.RequiereDocumentacion = new DocumentacionRequirement { EsRequerido = true };
        }
        else if (reference.Contains("DESBLOQUEO"))
        {
            analysis.RequiereDesbloqueo = new DesbloqueoRequirement { EsRequerido = true };
        }
        else if (reference.Contains("TRANSFER"))
        {
            analysis.RequiereTransferencia = new TransferenciaRequirement { EsRequerido = true };
        }
        else if (expediente.TieneAseguramiento)
        {
            analysis.RequiereBloqueo = new BloqueoRequirement { EsRequerido = true };
        }
        else
        {
            analysis.RequiereInformacionGeneral = new InformacionGeneralRequirement { EsRequerido = true };
        }

        return analysis;
    }
}
