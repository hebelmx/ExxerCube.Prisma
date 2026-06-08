namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Mutation-killing tests for <see cref="LegalDirectiveClassifierService"/>.
/// </summary>
/// <remarks>
/// The existing <c>LegalDirectiveClassifierServiceTests</c> / <c>...EdgeCaseTests</c> exercise the
/// happy paths with loose assertions (<c>ShouldContain</c>, <c>Confidence &gt; 60</c>) and only ever
/// drive <c>MapToComplianceActionAsync</c> with a Block directive. As a result Stryker left blind spots
/// (Survived/NoCoverage/Timeout) across the whole behaviour surface: the <c>CalculateConfidence</c> ladder
/// and its <c>Math.Min(100, …)</c> cap, the precedence ladder (<c>DetermineActionTypeWithPrecedence</c>),
/// the per-type confidence selection, <c>DetectDocumentRelationType</c> (never tested), the amount
/// pattern-priority / account-number skip heuristic, the product-type detection, the <c>DetectLegalInstruments</c>
/// Circular pattern, and every branch of <c>ApplyEdgeCaseValidation</c> (CLABE / prior-reference /
/// missing-account / low-confidence warnings — none asserted).
///
/// Each test below pins an <b>exact</b> value (confidence number, action type, relation type, extracted
/// amount/account/product, or a specific warning message) so a string/equality/boolean/block/logical
/// mutation is observable.
/// </remarks>
public class LegalDirectiveClassifierServiceMutationKillingTests
{
    private readonly LegalDirectiveClassifierService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="LegalDirectiveClassifierServiceMutationKillingTests"/> class.
    /// </summary>
    public LegalDirectiveClassifierServiceMutationKillingTests()
    {
        var logger = Substitute.For<ILogger<LegalDirectiveClassifierService>>();
        _service = new LegalDirectiveClassifierService(logger);
    }

    private async Task<ComplianceAction> MapAsync(string directive)
    {
        var result = await _service.MapToComplianceActionAsync(directive, null, TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        return result.Value!;
    }

    // ── CalculateConfidence ladder: 60 + matches*10, capped at 100 ──────────────────────────────

    /// <summary>One keyword yields exactly 60 + 1*10 = 70.</summary>
    [Fact]
    public async Task MapTo_SingleBlockKeyword_ConfidenceIs70()
    {
        var action = await MapAsync("Se ordena el BLOQUEO de la cuenta 1234567890");
        action.ActionType.ShouldBe(ComplianceActionKind.Block);
        action.Confidence.ShouldBe(70);
    }

    /// <summary>Two distinct keywords yield 60 + 2*10 = 80.</summary>
    [Fact]
    public async Task MapTo_TwoBlockKeywords_ConfidenceIs80()
    {
        var action = await MapAsync("Se ordena el ASEGURAR y EMBARGO de la cuenta 1234567890");
        action.ActionType.ShouldBe(ComplianceActionKind.Block);
        action.Confidence.ShouldBe(80);
    }

    /// <summary>Three distinct keywords yield 60 + 3*10 = 90.</summary>
    [Fact]
    public async Task MapTo_ThreeBlockKeywords_ConfidenceIs90()
    {
        var action = await MapAsync("ASEGURAR, EMBARGO e INMOVILIZAR la cuenta 1234567890");
        action.ActionType.ShouldBe(ComplianceActionKind.Block);
        action.Confidence.ShouldBe(90);
    }

    /// <summary>All six block keywords would be 120 but the Math.Min cap clamps to 100.</summary>
    [Fact]
    public async Task MapTo_AllSixBlockKeywords_ConfidenceCappedAt100()
    {
        var action = await MapAsync("BLOQUEO EMBARGO ASEGURAR CONGELAR RETENER INMOVILIZAR cuenta 1234567890");
        action.ActionType.ShouldBe(ComplianceActionKind.Block);
        action.Confidence.ShouldBe(100);
    }

    // ── Precedence ladder + per-type confidence selection ───────────────────────────────────────

    /// <summary>Unblock is detected and scored against the Unblock keyword set (70).</summary>
    [Fact]
    public async Task MapTo_UnblockDirective_ReturnsUnblockWithConfidence70()
    {
        var action = await MapAsync("Se ordena el DESBLOQUEO de la cuenta segun OFICIO 555");
        action.ActionType.ShouldBe(ComplianceActionKind.Unblock);
        action.Confidence.ShouldBe(70);
    }

    /// <summary>A document-only directive resolves to Document scored at 70.</summary>
    [Fact]
    public async Task MapTo_DocumentDirective_ReturnsDocumentWithConfidence70()
    {
        var action = await MapAsync("Se solicitan los DOCUMENTOS del expediente");
        action.ActionType.ShouldBe(ComplianceActionKind.Document);
        action.Confidence.ShouldBe(70);
    }

    /// <summary>A transfer-only directive resolves to Transfer scored at 70.</summary>
    [Fact]
    public async Task MapTo_TransferDirective_ReturnsTransferWithConfidence70()
    {
        var action = await MapAsync("Se ordena la TRANSFERENCIA a la cuenta 123456789012345678");
        action.ActionType.ShouldBe(ComplianceActionKind.Transfer);
        action.Confidence.ShouldBe(70);
    }

    /// <summary>An information-only directive resolves to Information scored at 70 (pins L261 branch).</summary>
    [Fact]
    public async Task MapTo_InformationDirective_ReturnsInformationWithConfidence70()
    {
        var action = await MapAsync("Se solicita INFORMACIÓN de las operaciones");
        action.ActionType.ShouldBe(ComplianceActionKind.Information);
        action.Confidence.ShouldBe(70);
    }

    /// <summary>No recognised keyword resolves to Unknown with the fixed 30 confidence (pins L265 branch).</summary>
    [Fact]
    public async Task MapTo_NoKeyword_ReturnsUnknownWithConfidence30()
    {
        var action = await MapAsync("Texto generico sin terminos clasificables");
        action.ActionType.ShouldBe(ComplianceActionKind.Unknown);
        action.Confidence.ShouldBe(30);
    }

    /// <summary>Unblock takes priority over Block when both keyword sets are present.</summary>
    [Fact]
    public async Task MapTo_UnblockAndBlockPresent_UnblockWins()
    {
        // "DESBLOQUEO" also substring-contains "BLOQUEO", and ASEGURAR is a block keyword.
        var action = await MapAsync("Se ordena DESBLOQUEO para liberar el ASEGURAR previo, OFICIO 1");
        action.ActionType.ShouldBe(ComplianceActionKind.Unblock);
    }

    /// <summary>Block takes priority over Transfer (priority-2 order).</summary>
    [Fact]
    public async Task MapTo_BlockAndTransferPresent_BlockWins()
    {
        var action = await MapAsync("Se ordena el BLOQUEO mediante TRANSFERENCIA de fondos");
        action.ActionType.ShouldBe(ComplianceActionKind.Block);
    }

    /// <summary>Transfer takes priority over Document.</summary>
    [Fact]
    public async Task MapTo_TransferAndDocumentPresent_TransferWins()
    {
        var action = await MapAsync("Realice la TRANSFERENCIA y adjunte los DOCUMENTOS");
        action.ActionType.ShouldBe(ComplianceActionKind.Transfer);
    }

    /// <summary>Document takes priority over Information.</summary>
    [Fact]
    public async Task MapTo_DocumentAndInformationPresent_DocumentWins()
    {
        var action = await MapAsync("Entregue los DOCUMENTOS y la INFORMACIÓN solicitada");
        action.ActionType.ShouldBe(ComplianceActionKind.Document);
    }

    /// <summary>
    /// The <c>ClassifyDirectivesAsync</c> Block branch also runs <c>ExtractActionDetails</c>: the produced
    /// Block action carries the parsed account and amount (pins the L76 extraction call, which the MapTo
    /// path's coverage does not reach).
    /// </summary>
    [Fact]
    public async Task Classify_BlockDirective_ExtractsAccountAndAmountOntoBlockAction()
    {
        var block = await ClassifyBlockAsync("Se ordena el BLOQUEO de la cuenta 9876543210 por $250,000.00");
        block.AccountNumber.ShouldBe("9876543210");
        block.Amount.ShouldBe(250000.00m);
    }

    /// <summary>
    /// The Block branch of <c>ClassifyDirectivesAsync</c> also runs <c>ApplyEdgeCaseValidation</c>: a Block
    /// with no account/amount is flagged on the Block action (pins the L77 validation call).
    /// </summary>
    [Fact]
    public async Task Classify_BlockWithoutTarget_FlagsBlockActionForReview()
    {
        var block = await ClassifyBlockAsync("Se procede a ASEGURAR, EMBARGO e INMOVILIZAR de inmediato");
        block.Warnings.ShouldContain(w => w.Contains("Missing account or amount"));
        block.RequiresManualReview.ShouldBeTrue();
    }

    /// <summary>
    /// The Unblock branch of <c>ClassifyDirectivesAsync</c> runs both <c>ExtractActionDetails</c> and
    /// <c>ApplyEdgeCaseValidation</c> on the Unblock action (pins the L92/L93 calls): account extracted and
    /// the missing-prior-reference warning present.
    /// </summary>
    [Fact]
    public async Task Classify_UnblockDirective_ExtractsAndValidatesUnblockAction()
    {
        var result = await _service.ClassifyDirectivesAsync(
            "Se procede al DESBLOQUEO de la cuenta 5551234567", null, TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        var unblock = result.Value!.First(a => a.ActionType == ComplianceActionKind.Unblock);
        unblock.AccountNumber.ShouldBe("5551234567");
        unblock.Warnings.ShouldContain(w => w.Contains("Missing prior order reference"));
        unblock.RequiresManualReview.ShouldBeTrue();
    }

    /// <summary>
    /// The Transfer branch of <c>ClassifyDirectivesAsync</c> runs <c>ApplyEdgeCaseValidation</c> on the
    /// Transfer action (pins the L122 call): a transfer with no 18-digit CLABE is flagged.
    /// </summary>
    [Fact]
    public async Task Classify_TransferWithoutClabe_FlagsTransferAction()
    {
        var result = await _service.ClassifyDirectivesAsync(
            "Se ordena la TRANSFERENCIA de fondos por $10,000.00", null, TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        var transfer = result.Value!.First(a => a.ActionType == ComplianceActionKind.Transfer);
        transfer.Warnings.ShouldContain(w => w.Contains("Missing CLABE"));
        transfer.RequiresManualReview.ShouldBeTrue();
    }

    // ── DetectDocumentRelationType (never previously tested) ─────────────────────────────────────

    private async Task<ComplianceAction> ClassifyBlockAsync(string text)
    {
        var result = await _service.ClassifyDirectivesAsync(text, null, TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        return result.Value!.First(a => a.ActionType == ComplianceActionKind.Block);
    }

    /// <summary>"Recordatorio del oficio" wording maps to the Recordatorio relation.</summary>
    [Fact]
    public async Task Classify_RecordatorioWording_SetsRecordatorioRelation()
    {
        var block = await ClassifyBlockAsync("BLOQUEO de cuenta, RECORDATORIO DEL OFICIO 123/2024");
        block.DocumentRelationType.ShouldBe(DocumentRelationType.Recordatorio);
    }

    /// <summary>"Alcance al oficio" wording maps to the Alcance relation.</summary>
    [Fact]
    public async Task Classify_AlcanceWording_SetsAlcanceRelation()
    {
        var block = await ClassifyBlockAsync("BLOQUEO de cuenta, en ALCANCE AL OFICIO 123/2024");
        block.DocumentRelationType.ShouldBe(DocumentRelationType.Alcance);
    }

    /// <summary>The "amplía/ampliación" synonym also maps to Alcance.</summary>
    [Fact]
    public async Task Classify_AmpliacionWording_SetsAlcanceRelation()
    {
        var block = await ClassifyBlockAsync("BLOQUEO de cuenta, se solicita la AMPLIACIÓN del requerimiento");
        block.DocumentRelationType.ShouldBe(DocumentRelationType.Alcance);
    }

    /// <summary>"Precisión" wording maps to the Precision relation.</summary>
    [Fact]
    public async Task Classify_PrecisionWording_SetsPrecisionRelation()
    {
        var block = await ClassifyBlockAsync("BLOQUEO de cuenta, PRECISIÓN sobre el monto indicado");
        block.DocumentRelationType.ShouldBe(DocumentRelationType.Precision);
    }

    /// <summary>The "aclara" synonym also maps to Precision.</summary>
    [Fact]
    public async Task Classify_AclaraWording_SetsPrecisionRelation()
    {
        var block = await ClassifyBlockAsync("BLOQUEO de cuenta, se ACLARA el contenido previo");
        block.DocumentRelationType.ShouldBe(DocumentRelationType.Precision);
    }

    /// <summary>Plain wording with no relation keyword defaults to NewRequirement.</summary>
    [Fact]
    public async Task Classify_NoRelationWording_DefaultsToNewRequirement()
    {
        var block = await ClassifyBlockAsync("Se ordena el BLOQUEO de la cuenta 1234567890");
        block.DocumentRelationType.ShouldBe(DocumentRelationType.NewRequirement);
    }

    // ── ExtractActionDetails: amount pattern priority + account-number skip heuristic ────────────

    /// <summary>
    /// A large (≥10 digit) formatted number with no monetary context is treated as an account number and
    /// skipped; one carrying a currency marker ($, monto, cantidad, importe, pesos, dolares) is extracted.
    /// Pins the whole account-skip guard chain (the six <c>!Contains(...)</c> terms + the <c>continue</c>).
    /// </summary>
    [Theory]
    [InlineData("se identifica el folio 1,234,567,890 en el registro", null)]
    [InlineData("por la cantidad de $1,234,567,890.00", 1234567890.00)]
    [InlineData("por un monto de 1,234,567,890.00", 1234567890.00)]
    [InlineData("la cantidad 1,234,567,890.00 indicada", 1234567890.00)]
    [InlineData("el importe 1,234,567,890.00 senalado", 1234567890.00)]
    [InlineData("saldo de 1,234,567,890.00 pesos", 1234567890.00)]
    [InlineData("equivalente a 1,234,567,890.00 dolares", 1234567890.00)]
    public async Task MapTo_LargeFormattedNumber_AppliesAccountSkipHeuristic(string text, double? expected)
    {
        var action = await MapAsync(text);
        if (expected is null)
        {
            action.Amount.ShouldBeNull();
        }
        else
        {
            action.Amount.ShouldBe((decimal)expected.Value);
        }
    }

    /// <summary>A dollar-sign amount (priority pattern 1) is extracted exactly.</summary>
    [Fact]
    public async Task MapTo_DollarAmount_ExtractsExactValue()
    {
        var action = await MapAsync("BLOQUEO por la suma de $1,000,000.00");
        action.Amount.ShouldBe(1000000.00m);
    }

    /// <summary>An amount expressed with a currency word (pattern 3) is extracted exactly.</summary>
    [Fact]
    public async Task MapTo_PesosAmount_ExtractsExactValue()
    {
        var action = await MapAsync("BLOQUEO por 750,000.50 pesos");
        action.Amount.ShouldBe(750000.50m);
    }

    /// <summary>
    /// With two same-priority ($) matches of different lengths, the longer (more complete) amount wins —
    /// pins the <c>match.Value.Length &gt; bestMatch.Value.Length</c> tie-break (a <c>&lt;</c> mutation
    /// would keep the shorter $5.00).
    /// </summary>
    [Fact]
    public async Task MapTo_TwoDollarAmountsDifferentLengths_LongerWins()
    {
        var action = await MapAsync("BLOQUEO por $5.00 y tambien $1,000,000.00");
        action.Amount.ShouldBe(1000000.00m);
    }

    /// <summary>
    /// With two same-priority ($) matches of equal length, the first is kept — pins the strict <c>&gt;</c>
    /// (a <c>&gt;=</c> mutation would replace it with the second $200.00).
    /// </summary>
    [Fact]
    public async Task MapTo_TwoDollarAmountsEqualLength_FirstKept()
    {
        var action = await MapAsync("BLOQUEO por $100.00 y luego $200.00");
        action.Amount.ShouldBe(100.00m);
    }

    /// <summary>
    /// A formatted number under 10 digits with no currency context is NOT treated as an account number and
    /// IS extracted. Pins the <c>digitsOnly.Length &gt;= 10</c> boundary of the skip guard (the <c>&amp;&amp;</c>
    /// chain at L460) and BOTH <c>digitsOnly</c> <c>Replace</c> calls at L457 — the decimal value (9 digits)
    /// sits just under the threshold, so inflating either the comma- or the dot-removal pushes the length
    /// past 10 and wrongly skips this amount.
    /// </summary>
    [Fact]
    public async Task MapTo_FormattedNumberUnderTenDigits_NoContext_StillExtracted()
    {
        var action = await MapAsync("se registra el folio 1,234,567.89 en el sistema");
        action.Amount.ShouldBe(1234567.89m);
    }

    /// <summary>
    /// A dollar amount (pattern 1) beats a later plain-formatted amount (pattern 4) by priority, regardless
    /// of size. Pins the <c>i &lt; bestPatternPriority</c> comparison (an <c>i &gt; …</c> mutation would let
    /// the lower-priority 5,000,000 override the $100).
    /// </summary>
    [Fact]
    public async Task MapTo_DollarBeatsLaterLargerFormatted_ByPriority()
    {
        var action = await MapAsync("BLOQUEO por $100.00 mas la referencia 5,000,000.00");
        action.Amount.ShouldBe(100.00m);
    }

    /// <summary>The "cuenta NNNN" pattern populates the account number (≥4 digits).</summary>
    [Fact]
    public async Task MapTo_CuentaPattern_ExtractsAccountNumber()
    {
        var action = await MapAsync("BLOQUEO de la cuenta 1234567890");
        action.AccountNumber.ShouldBe("1234567890");
    }

    // ── ExtractActionDetails: product type ──────────────────────────────────────────────────────

    /// <summary>A document mentioning TARJETA sets the product type to TARJETA.</summary>
    [Fact]
    public async Task MapTo_TarjetaMention_SetsProductTypeTarjeta()
    {
        var action = await MapAsync("BLOQUEO de la TARJETA del cliente");
        action.ProductType.ShouldBe("TARJETA");
    }

    /// <summary>A document mentioning CUENTA (without TARJETA) sets the product type to CUENTA.</summary>
    [Fact]
    public async Task MapTo_CuentaMention_SetsProductTypeCuenta()
    {
        var action = await MapAsync("BLOQUEO de la CUENTA bancaria");
        action.ProductType.ShouldBe("CUENTA");
    }

    /// <summary>TARJETA takes priority over CUENTA when both are present (else-if order).</summary>
    [Fact]
    public async Task MapTo_TarjetaAndCuentaMention_PrefersTarjeta()
    {
        var action = await MapAsync("BLOQUEO de la TARJETA asociada a la CUENTA");
        action.ProductType.ShouldBe("TARJETA");
    }

    /// <summary>
    /// When neither TARJETA nor CUENTA is mentioned, the product type stays null — pins both
    /// <c>Contains("TARJETA"/"CUENTA")</c> guards (a <c>Contains("")</c> mutation is always true and would
    /// wrongly assign a product type to any text).
    /// </summary>
    [Fact]
    public async Task MapTo_NoProductMention_LeavesProductTypeNull()
    {
        var action = await MapAsync("Se ordena ASEGURAR los bienes inmuebles del titular");
        action.ProductType.ShouldBeNull();
    }

    // ── ApplyEdgeCaseValidation: Transfer requires a CLABE ───────────────────────────────────────

    /// <summary>A Transfer without an 18-digit CLABE is flagged for manual review.</summary>
    [Fact]
    public async Task MapTo_TransferWithoutClabe_AddsMissingClabeWarning()
    {
        var action = await MapAsync("Se ordena la TRANSFERENCIA de fondos");
        action.ActionType.ShouldBe(ComplianceActionKind.Transfer);
        action.Warnings.ShouldContain(w => w.Contains("Missing CLABE"));
        action.RequiresManualReview.ShouldBeTrue();
    }

    /// <summary>A Transfer that carries an 18-digit CLABE is not flagged for the missing-CLABE case.</summary>
    [Fact]
    public async Task MapTo_TransferWithClabe_NoMissingClabeWarning()
    {
        var action = await MapAsync("Se ordena la TRANSFERENCIA a la CLABE 123456789012345678");
        action.ActionType.ShouldBe(ComplianceActionKind.Transfer);
        action.Warnings.ShouldNotContain(w => w.Contains("Missing CLABE"));
    }

    // ── ApplyEdgeCaseValidation: Unblock requires a prior-order reference ────────────────────────

    /// <summary>An Unblock with no prior-order reference is flagged for manual review.</summary>
    [Fact]
    public async Task MapTo_UnblockWithoutReference_AddsMissingReferenceWarning()
    {
        var action = await MapAsync("Se efectua el DESBLOQUEO de la cuenta bancaria");
        action.ActionType.ShouldBe(ComplianceActionKind.Unblock);
        action.Warnings.ShouldContain(w => w.Contains("Missing prior order reference"));
        action.RequiresManualReview.ShouldBeTrue();
    }

    /// <summary>
    /// An Unblock that mentions <b>any single</b> one of the four reference terms is not flagged. Each term
    /// in isolation pins a distinct <c>||</c> in the reference chain (a <c>&amp;&amp;</c> mutation on that
    /// operator would drop the lone true term and wrongly re-flag). The sentences deliberately avoid
    /// "ordena" (which embeds the substring "ORDEN") so exactly one term is present.
    /// </summary>
    [Theory]
    [InlineData("Se solicita el DESBLOQUEO conforme al OFICIO 555")]
    [InlineData("Se solicita el DESBLOQUEO ligado al EXPEDIENTE 123")]
    [InlineData("Se solicita el DESBLOQUEO por ORDEN judicial")]
    [InlineData("Se solicita el DESBLOQUEO del aseguramiento ANTERIOR")]
    public async Task MapTo_UnblockWithSingleReference_NoMissingReferenceWarning(string directive)
    {
        var action = await MapAsync(directive);
        action.ActionType.ShouldBe(ComplianceActionKind.Unblock);
        action.Warnings.ShouldNotContain(w => w.Contains("Missing prior order reference"));
    }

    // ── ApplyEdgeCaseValidation: Block requires an account or amount ─────────────────────────────

    /// <summary>A high-confidence Block lacking both account and amount is flagged.</summary>
    [Fact]
    public async Task MapTo_BlockWithoutAccountOrAmount_AddsMissingTargetWarning()
    {
        var action = await MapAsync("Se ordena ASEGURAR, EMBARGO e INMOVILIZAR de inmediato");
        action.ActionType.ShouldBe(ComplianceActionKind.Block);
        action.Confidence.ShouldBe(90); // ≥70 so the low-confidence warning is not in play
        action.Warnings.ShouldContain(w => w.Contains("Missing account or amount"));
        action.RequiresManualReview.ShouldBeTrue();
    }

    /// <summary>
    /// A Block with an account present is not flagged for the missing-target case — pins the
    /// <c>&amp;&amp;</c> guard (an <c>||</c> mutation would still flag because no amount is set).
    /// </summary>
    [Fact]
    public async Task MapTo_BlockWithAccountOnly_NoMissingTargetWarning()
    {
        var action = await MapAsync("Se ordena ASEGURAR y EMBARGO de la cuenta 1234567890");
        action.ActionType.ShouldBe(ComplianceActionKind.Block);
        action.AccountNumber.ShouldBe("1234567890");
        action.Amount.ShouldBeNull();
        action.Warnings.ShouldNotContain(w => w.Contains("Missing account or amount"));
    }

    /// <summary>
    /// A Block with an amount but no account is not flagged — pins the <c>!action.Amount.HasValue</c>
    /// term (a <c>HasValue</c> mutation would flag despite the amount being present).
    /// </summary>
    [Fact]
    public async Task MapTo_BlockWithAmountOnly_NoMissingTargetWarning()
    {
        var action = await MapAsync("Se ordena ASEGURAR y EMBARGO por la suma de $5,000.00");
        action.ActionType.ShouldBe(ComplianceActionKind.Block);
        action.Amount.ShouldBe(5000.00m);
        action.AccountNumber.ShouldBeNull();
        action.Warnings.ShouldNotContain(w => w.Contains("Missing account or amount"));
    }

    // ── ApplyEdgeCaseValidation: low-confidence threshold (< 70) ─────────────────────────────────

    /// <summary>Confidence below 70 (an Unknown at 30) adds the low-confidence warning.</summary>
    [Fact]
    public async Task MapTo_LowConfidence_AddsLowConfidenceWarning()
    {
        var action = await MapAsync("Texto generico sin terminos clasificables");
        action.Confidence.ShouldBe(30);
        action.Warnings.ShouldContain(w => w.Contains("Low classification confidence") && w.Contains("30%"));
        action.RequiresManualReview.ShouldBeTrue();
    }

    /// <summary>
    /// Confidence of exactly 70 is not low (70 &lt; 70 is false) — pins the boundary so a
    /// <c>&lt;=</c> mutation is killed. The Block also carries an account so no other warning fires.
    /// </summary>
    [Fact]
    public async Task MapTo_ConfidenceExactly70_NoLowConfidenceWarning()
    {
        var action = await MapAsync("Se ordena el BLOQUEO de la cuenta 1234567890");
        action.Confidence.ShouldBe(70);
        action.Warnings.ShouldNotContain(w => w.Contains("Low classification confidence"));
    }

    // ── DetectLegalInstruments: Circular pattern + counts ────────────────────────────────────────

    /// <summary>The Circular pattern is detected (the previously-uncovered third instrument branch).</summary>
    [Fact]
    public async Task DetectInstruments_Circular_ReturnsCircular()
    {
        var result = await _service.DetectLegalInstrumentsAsync(
            "De conformidad con la Circular 456/2022", TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.ShouldContain("Circular 456/2022");
    }

    /// <summary>All three instrument types are detected together with the exact formatted strings.</summary>
    [Fact]
    public async Task DetectInstruments_AllThreeTypes_ReturnsEachExactly()
    {
        var result = await _service.DetectLegalInstrumentsAsync(
            "Acuerdo 105/2021, Ley 123/2020 y Circular 456/2022", TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Count.ShouldBe(3);
        result.Value.ShouldContain("Acuerdo 105/2021");
        result.Value.ShouldContain("Ley 123/2020");
        result.Value.ShouldContain("Circular 456/2022");
    }

    // ── ClassifyDirectivesAsync: Ignore default confidence ──────────────────────────────────────

    /// <summary>When no directive matches, a single Ignore action with the fixed 50 confidence is returned.</summary>
    [Fact]
    public async Task Classify_NoDirective_ReturnsIgnoreWithConfidence50()
    {
        var result = await _service.ClassifyDirectivesAsync(
            "Texto generico sin terminos clasificables", null, TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        var ignore = result.Value!.Single();
        ignore.ActionType.ShouldBe(ComplianceActionKind.Ignore);
        ignore.Confidence.ShouldBe(50);
    }
}
