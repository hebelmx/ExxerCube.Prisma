using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Operations;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="IExpedienteClasifier"/> — the CNBV requirement
/// classifier. Split out of the previously-conflated <c>ExpedienteClasifierServiceContractTests</c>
/// (a real-SUT class merely <em>named</em> "ContractTests") so the interface-generic behaviour is
/// inherited by every implementation and by the mock blueprint (ADR-005).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Behavioural-vs-implementation analysis (owner-requested).</strong> The CNBV semantics
/// are the documented interface contract (types 100-104, Article 4 required fields, the six
/// Article 17 grounds, "the 5 Situations"), so the classification <em>outcome</em> is behavioural —
/// any correct implementation must produce it. What is NOT contract-grade and stays in the deriving
/// implementation class: the exact internal required-field name strings
/// (<c>"InitialBlockedAmount"</c>, <c>"AccountNumber"</c>…), the R29 42-field enumeration, and
/// magic-number confidence cut-offs.
/// </para>
/// <para>
/// <strong>Thresholds as first-class contract clauses.</strong> Where a threshold expresses a real
/// contract clause ("an unambiguous request yields high confidence"), it is materialised as the
/// overridable property <see cref="MinHighClassificationConfidence"/> with a minimal-plausible
/// default — the behavioural test asserts against the property and the real implementation overrides
/// it to its actual value (preserving the exact-threshold kill power). Magic numbers that express no
/// behavioural guarantee are left impl-side.
/// </para>
/// <para>
/// Inputs are supplied through abstract fixture hooks so each implementation (and the blueprint)
/// provides a representative Expediente it considers e.g. "an aseguramiento request"; the base then
/// asserts the behavioural outcome. Cancellation tests (one per method) were added in Phase 6 once the
/// impl was fixed to honor the token (carried from the Phase 4 gate, per the ADR-005 §7 precedent).
/// </para>
/// </remarks>
public abstract class ExpedienteClasifierContract
{
    /// <summary>Creates the implementation under test. Called once per test.</summary>
    /// <returns>The <see cref="IExpedienteClasifier"/> implementation to verify.</returns>
    protected abstract IExpedienteClasifier CreateSut();

    /// <summary>
    /// The minimal confidence an implementation must report for an unambiguous classification.
    /// Default is a minimal-plausible 0.5; a real implementation overrides this with its actual
    /// (higher) bound so the behavioural clause "clear request ⇒ high confidence" is pinned exactly.
    /// </summary>
    protected virtual double MinHighClassificationConfidence => 0.5;

    // Fixture hooks — each implementation supplies a representative input.

    /// <summary>An Expediente that requests information only (→ type 100).</summary>
    protected abstract Expediente CreateInformationRequestExpediente();

    /// <summary>An Expediente that requests documentation (→ RequiereDocumentacion).</summary>
    protected abstract Expediente CreateDocumentationRequestExpediente();

    /// <summary>An Expediente that orders asset seizure (→ type 101).</summary>
    protected abstract Expediente CreateAseguramientoExpediente();

    /// <summary>An Expediente that orders unblocking (→ type 102).</summary>
    protected abstract Expediente CreateDesbloqueoExpediente();

    /// <summary>An Expediente that orders an electronic transfer (→ type 103).</summary>
    protected abstract Expediente CreateTransferenciaExpediente();

    /// <summary>An Expediente that orders physical delivery of funds (→ type 104).</summary>
    protected abstract Expediente CreateSituacionFondosExpediente();

    /// <summary>A complete Expediente that satisfies Article 4 for its type.</summary>
    protected abstract Expediente CreateCompleteExpediente();

    /// <summary>An Expediente missing mandatory Article 4 fields.</summary>
    protected abstract Expediente CreateIncompleteExpediente();

    /// <summary>An Expediente lacking a legal-authority citation (Article 17 ground).</summary>
    protected abstract Expediente CreateExpedienteWithoutLegalCitation();

    /// <summary>An Expediente lacking an authority signature (Article 17 ground).</summary>
    protected abstract Expediente CreateExpedienteWithoutSignature();

    /// <summary>A vague Expediente lacking specificity (Article 17 ground).</summary>
    protected abstract Expediente CreateVagueExpediente();

    /// <summary>An Expediente outside CNBV jurisdiction (Article 17 ground).</summary>
    protected abstract Expediente CreateOutOfJurisdictionExpediente();

    //
    // Contract: ClassifyAsync — requirement types (100-104)
    //

    /// <summary>Contract: an information request classifies as type 100 with high confidence.</summary>
    [Fact]
    public async Task ClassifyAsync_InformationRequest_ClassifiesAsInformationRequest()
    {
        var sut = CreateSut();

        var result = await sut.ClassifyAsync(CreateInformationRequestExpediente(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.RequirementType.ShouldBe(RequirementType.InformationRequest);
        result.Value.AuthorityType.ShouldNotBeNull();
        result.Value.ClassificationConfidence.ShouldBeInRange(0.0, 1.0);
        result.Value.ClassificationConfidence.ShouldBeGreaterThanOrEqualTo(MinHighClassificationConfidence);
    }

    /// <summary>Contract: an asset-seizure order classifies as type 101 and yields required fields.</summary>
    [Fact]
    public async Task ClassifyAsync_AseguramientoRequest_ClassifiesAsAseguramiento()
    {
        var sut = CreateSut();

        var result = await sut.ClassifyAsync(CreateAseguramientoExpediente(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.RequirementType.ShouldBe(RequirementType.Aseguramiento);
        result.Value.RequiredFields.ShouldNotBeEmpty();
        result.Value.ClassificationConfidence.ShouldBeInRange(0.0, 1.0);
    }

    /// <summary>Contract: an unblocking order classifies as type 102.</summary>
    [Fact]
    public async Task ClassifyAsync_DesbloqueoRequest_ClassifiesAsDesbloqueo()
    {
        var sut = CreateSut();

        var result = await sut.ClassifyAsync(CreateDesbloqueoExpediente(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.RequirementType.ShouldBe(RequirementType.Desbloqueo);
        result.Value.RequiredFields.ShouldNotBeEmpty();
        result.Value.ClassificationConfidence.ShouldBeInRange(0.0, 1.0);
    }

    /// <summary>Contract: an electronic transfer order classifies as type 103.</summary>
    [Fact]
    public async Task ClassifyAsync_TransferenciaElectronica_ClassifiesAsTransferencia()
    {
        var sut = CreateSut();

        var result = await sut.ClassifyAsync(CreateTransferenciaExpediente(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.RequirementType.ShouldBe(RequirementType.Transferencia);
        result.Value.RequiredFields.ShouldNotBeEmpty();
        result.Value.ClassificationConfidence.ShouldBeInRange(0.0, 1.0);
    }

    /// <summary>Contract: a put-at-disposal order classifies as type 104.</summary>
    [Fact]
    public async Task ClassifyAsync_SituacionFondos_ClassifiesAsSituacionFondos()
    {
        var sut = CreateSut();

        var result = await sut.ClassifyAsync(CreateSituacionFondosExpediente(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.RequirementType.ShouldBe(RequirementType.SituacionFondos);
        result.Value.RequiredFields.ShouldNotBeEmpty();
        result.Value.ClassificationConfidence.ShouldBeInRange(0.0, 1.0);
    }

    //
    // Contract: ValidateArticle4Async
    //

    /// <summary>Contract: a complete Expediente passes Article 4 with no missing fields.</summary>
    [Fact]
    public async Task ValidateArticle4Async_AllMandatoryFieldsPresent_PassesValidation()
    {
        var sut = CreateSut();

        var result = await sut.ValidateArticle4Async(
            CreateCompleteExpediente(),
            RequirementType.Aseguramiento,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.PassesArticle4.ShouldBeTrue();
        result.Value.MissingRequiredFields.ShouldBeEmpty();
    }

    /// <summary>Contract: an Expediente missing mandatory fields fails Article 4 and lists them.</summary>
    [Fact]
    public async Task ValidateArticle4Async_MissingMandatoryFields_FailsValidation()
    {
        var sut = CreateSut();

        var result = await sut.ValidateArticle4Async(
            CreateIncompleteExpediente(),
            RequirementType.Aseguramiento,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.PassesArticle4.ShouldBeFalse();
        result.Value.MissingRequiredFields.ShouldNotBeEmpty();
    }

    //
    // Contract: CheckArticle17RejectionAsync — the documented rejection grounds
    //

    /// <summary>Contract: a missing legal-authority citation is reported.</summary>
    [Fact]
    public async Task CheckArticle17RejectionAsync_MissingLegalAuthorityCitation_ReturnsRejectionReason()
    {
        var sut = CreateSut();

        var result = await sut.CheckArticle17RejectionAsync(CreateExpedienteWithoutLegalCitation(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain(RejectionReason.NoLegalAuthorityCitation);
    }

    /// <summary>Contract: a missing authority signature is reported.</summary>
    [Fact]
    public async Task CheckArticle17RejectionAsync_MissingSignature_ReturnsRejectionReason()
    {
        var sut = CreateSut();

        var result = await sut.CheckArticle17RejectionAsync(CreateExpedienteWithoutSignature(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain(RejectionReason.MissingSignature);
    }

    /// <summary>Contract: a vague request is reported for lack of specificity.</summary>
    [Fact]
    public async Task CheckArticle17RejectionAsync_LackOfSpecificity_ReturnsRejectionReason()
    {
        var sut = CreateSut();

        var result = await sut.CheckArticle17RejectionAsync(CreateVagueExpediente(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain(RejectionReason.LackOfSpecificity);
    }

    /// <summary>Contract: a request outside CNBV competence is reported.</summary>
    [Fact]
    public async Task CheckArticle17RejectionAsync_ExceedsJurisdiction_ReturnsRejectionReason()
    {
        var sut = CreateSut();

        var result = await sut.CheckArticle17RejectionAsync(CreateOutOfJurisdictionExpediente(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain(RejectionReason.ExceedsJurisdiction);
    }

    /// <summary>Contract: a legally compliant Expediente has no rejection grounds.</summary>
    [Fact]
    public async Task CheckArticle17RejectionAsync_ValidExpediente_ReturnsEmptyList()
    {
        var sut = CreateSut();

        var result = await sut.CheckArticle17RejectionAsync(CreateCompleteExpediente(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldBeEmpty();
    }

    //
    // Contract: AnalyzeSemanticRequirementsAsync — "the 5 Situations"
    //

    /// <summary>Contract: a general information request yields RequiereInformacionGeneral.</summary>
    [Fact]
    public async Task AnalyzeSemanticRequirementsAsync_InformationRequest_CreatesGeneralRequirement()
    {
        var sut = CreateSut();

        var result = await sut.AnalyzeSemanticRequirementsAsync(CreateInformationRequestExpediente(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.RequiereInformacionGeneral.ShouldNotBeNull();
        result.Value.RequiereInformacionGeneral.EsRequerido.ShouldBeTrue();
    }

    /// <summary>Contract: a documentation request yields RequiereDocumentacion.</summary>
    [Fact]
    public async Task AnalyzeSemanticRequirementsAsync_DocumentRequest_CreatesDocumentationRequirement()
    {
        var sut = CreateSut();

        var result = await sut.AnalyzeSemanticRequirementsAsync(CreateDocumentationRequestExpediente(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.RequiereDocumentacion.ShouldNotBeNull();
        result.Value.RequiereDocumentacion.EsRequerido.ShouldBeTrue();
    }

    /// <summary>Contract: an asset-seizure order yields RequiereBloqueo.</summary>
    [Fact]
    public async Task AnalyzeSemanticRequirementsAsync_Aseguramiento_CreatesBloqueoRequirement()
    {
        var sut = CreateSut();

        var result = await sut.AnalyzeSemanticRequirementsAsync(CreateAseguramientoExpediente(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.RequiereBloqueo.ShouldNotBeNull();
        result.Value.RequiereBloqueo.EsRequerido.ShouldBeTrue();
    }

    /// <summary>Contract: an unblocking order yields RequiereDesbloqueo.</summary>
    [Fact]
    public async Task AnalyzeSemanticRequirementsAsync_Desbloqueo_CreatesDesbloqueoRequirement()
    {
        var sut = CreateSut();

        var result = await sut.AnalyzeSemanticRequirementsAsync(CreateDesbloqueoExpediente(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.RequiereDesbloqueo.ShouldNotBeNull();
        result.Value.RequiereDesbloqueo.EsRequerido.ShouldBeTrue();
    }

    /// <summary>Contract: a transfer order yields RequiereTransferencia.</summary>
    [Fact]
    public async Task AnalyzeSemanticRequirementsAsync_Transferencia_CreatesTransferenciaRequirement()
    {
        var sut = CreateSut();

        var result = await sut.AnalyzeSemanticRequirementsAsync(CreateTransferenciaExpediente(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.RequiereTransferencia.ShouldNotBeNull();
        result.Value.RequiereTransferencia.EsRequerido.ShouldBeTrue();
    }

    //
    // Cancellation Tests (Phase 6 — repository-wide CancellationToken mandate, ADR-005 §5)
    //

    /// <summary>Contract: a pre-cancelled token short-circuits ClassifyAsync to Cancelled (never a throw).</summary>
    [Fact]
    public async Task ClassifyAsync_WhenCancellationRequested_ReturnsCancelled()
    {
        var sut = CreateSut();

        var result = await sut.ClassifyAsync(CreateCompleteExpediente(), new CancellationToken(canceled: true));

        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>Contract: a pre-cancelled token short-circuits ValidateArticle4Async to Cancelled.</summary>
    [Fact]
    public async Task ValidateArticle4Async_WhenCancellationRequested_ReturnsCancelled()
    {
        var sut = CreateSut();

        var result = await sut.ValidateArticle4Async(
            CreateCompleteExpediente(),
            RequirementType.InformationRequest,
            new CancellationToken(canceled: true));

        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>Contract: a pre-cancelled token short-circuits CheckArticle17RejectionAsync to Cancelled.</summary>
    [Fact]
    public async Task CheckArticle17RejectionAsync_WhenCancellationRequested_ReturnsCancelled()
    {
        var sut = CreateSut();

        var result = await sut.CheckArticle17RejectionAsync(CreateCompleteExpediente(), new CancellationToken(canceled: true));

        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>Contract: a pre-cancelled token short-circuits AnalyzeSemanticRequirementsAsync to Cancelled.</summary>
    [Fact]
    public async Task AnalyzeSemanticRequirementsAsync_WhenCancellationRequested_ReturnsCancelled()
    {
        var sut = CreateSut();

        var result = await sut.AnalyzeSemanticRequirementsAsync(CreateCompleteExpediente(), new CancellationToken(canceled: true));

        result.IsCancelled().ShouldBeTrue();
    }
}
