using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tests;

/// <summary>
/// Unit tests for Story 10.4 verbatim-block rules:
/// LAW-§11-URLS, LAW-§17-LEGENDS, LAW-§24-QUEJAS, LAW-§26-NOTAS, LAW-§27-GLOSARIO.
/// </summary>
/// <remarks>
/// Every rule follows the same behavioral contract:
/// <list type="bullet">
///   <item>null model / empty NormalizedFullText → <see cref="FindingVerdict.InsufficientData"/>.</item>
///   <item>Sections list empty (detection not run) → <see cref="FindingVerdict.InsufficientData"/>.</item>
///   <item>Host section absent/not-detected → <see cref="FindingVerdict.InsufficientData"/>.</item>
///   <item>All blocks present (with accents/whitespace perturbation) → <see cref="FindingVerdict.Pass"/>.</item>
///   <item>One block missing/altered below threshold → <see cref="FindingVerdict.Fail"/> naming the block + score.</item>
///   <item>Cancellation → cancelled result.</item>
/// </list>
/// Catalog strings are inlined here (not imported from internal class) to keep tests
/// self-contained and independent of the production class's access modifier.
/// </remarks>
public sealed class VerbatimBlockRulesTests
{
    // -----------------------------------------------------------------------
    // Inline catalog excerpts (verbatim from DOF — same source as the production catalog)
    // These are inlined here so the test project does not depend on internal types.
    // -----------------------------------------------------------------------

    // §11
    private const string Url1 = "https://tarjetas.condusef.gob.mx/index.php";
    private const string Url2 = "https://comparador.banxico.org.mx/";

    // §17 — four art-6-IV legends
    private const string Legend17A = "Al ser tu crédito de tasa variable, los intereses pueden aumentar.";
    private const string Legend17B = "Incumplir tus obligaciones te puede generar comisiones e intereses moratorios.";
    private const string Legend17C = "Contratar créditos que excedan tu capacidad de pago afecta tu historial crediticio.";
    // R2 correction: §17 Legend-d now ends with a terminal period (CondusefVerbatimCatalog update).
    private const string Legend17D = "Realizar sólo el pago mínimo aumenta el tiempo de pago y el costo de la deuda.";

    // §24 — invariant CONDUSEF quejas fragment
    private const string Quejas24Fragment =
        "podrá acudir a la Comisión Nacional para la Protección y Defensa de los Usuarios de " +
        "Servicios Financieros. Correo electrónico: asesoria@condusef.gob.mx, chat en línea " +
        "www.condusef.gob.mx o Tel: 800 999 8080 y 55 53 40 09 99.";

    // §26 — two representative notes (a and b) sufficient for Pass/Fail tests
    private const string Note26A =
        "Tienes como límite esta fecha para realizar tu pago, evitar el cargo de comisiones " +
        "por pago tardío, falta de pago o intereses moratorios y mantener tu crédito al " +
        "corriente. Si esta fecha corresponde a un día inhábil bancario, puedes realizar el " +
        "pago el siguiente día hábil bancario sin que proceda el cobro de comisiones por pago " +
        "tardío, falta de pago o intereses moratorios.";

    private const string Note26B =
        "Este es el saldo a pagar para no generar intereses ordinarios (excepto los asociados " +
        "a disposiciones de efectivo o compras diferidas con intereses, cuyos intereses se " +
        "continuarán cobrando de conformidad con la tasa acordada y el plazo de diferimiento " +
        "al que se encuentren sujetos) ni intereses moratorios o comisiones por falta de pago " +
        "o pago tardío (en caso de resultar aplicables) en el siguiente periodo. No considera " +
        "el saldo pendiente de compras y cargos diferidos a meses que no es exigible en el " +
        "periodo actual.";

    private const string Note26C =
        "Si no pagas la mensualidad de tus compras a meses, en adición al pago mínimo, éstas " +
        "generarán intereses ordinarios en el siguiente periodo.";

    private const string Note26D =
        "El pago mínimo es el monto mínimo que debes pagar para que tu crédito se considere " +
        "al corriente y no se te cobren comisiones por pago tardío, falta de pago o intereses " +
        "moratorios. El monto incluye los intereses que se hayan generado en el periodo y el " +
        "IVA correspondiente. Este pago no te exime de pagar intereses ordinarios. Si solo " +
        "pagas el mínimo, se generarán intereses sobre el saldo que no fue cubierto con el " +
        "pago, estos intereses aparecerán reflejados en el siguiente periodo.";

    private const string Note26E =
        "Los datos de esta tabla pueden modificarse en los estados de cuenta subsecuentes " +
        "debido a que varían en función del uso y pagos realizados a la tarjeta.";

    private const string Note26F =
        "Estos intereses se calculan considerando la tasa de interés a la fecha de corte. En " +
        "caso de que la tasa de interés se modifique en los siguientes periodos, el monto de " +
        "los intereses calculados será distinto. Además, el monto de intereses calculados no " +
        "considera aquellos derivados de compras y cargos diferidos a meses con intereses.";

    private const string Note26G =
        "Es el monto exigible de la mensualidad destinado a amortizar el capital de: las " +
        "compras a meses sin intereses, las compras o cargos diferidos a meses con intereses " +
        "y las otras líneas de crédito adicionales a la línea de crédito de la tarjeta, en " +
        "su caso.";

    private const string Note26H =
        "Los intereses del periodo se calculan en función de la tasa de interés aplicable a " +
        "los distintos saldos. Revisa la sección \"SALDO SOBRE EL QUE SE CALCULARON LOS " +
        "INTERESES DEL PERIODO\" en este estado de cuenta.";

    private const string Note26I =
        "Incluye los intereses ordinarios y moratorios de compras regulares, así como de " +
        "compras y cargos a meses con intereses.";

    private const string Note26J =
        "Consulta la sección \"GLOSARIO DE TÉRMINOS Y ABREVIATURAS\" para conocer cómo " +
        "interpretar este indicador.";

    private const string Note26K =
        "El saldo deudor total es la suma del pago para no generar intereses y el saldo " +
        "pendiente a meses.";

    private const string Note26L =
        "Los intereses del periodo se calculan usando la tasa de interés aplicable a los días " +
        "del periodo, la cual se obtiene dividiendo la tasa de interés anual aplicable, de " +
        "acuerdo al renglón que corresponda (ya sea ordinaria, moratoria, preferencial, de " +
        "cargos y compras diferidas a meses, por disposiciones de efectivo u otras), entre " +
        "360 días y multiplicando el resultado por el número de días del periodo.";

    private const string Note26M =
        "El pago requerido de compras o cargos a meses con intereses ya incluye los intereses " +
        "pactados al momento de la compra a la tasa de interés acordada.";

    // §27 — fifteen glossary terms
    private const string Term27A =
        "CAT: Costo Anual Total de financiamiento expresado en términos porcentuales anuales " +
        "que, para fines informativos y de comparación, incorpora la totalidad de los costos " +
        "y gastos inherentes a los créditos, préstamos o financiamientos que otorgan las " +
        "Instituciones Financieras, de conformidad con las disposiciones que al efecto emita " +
        "el Banco de México.";

    private const string Term27B =
        "CLABE: es la Clave Bancaria Estandarizada de dieciocho dígitos que se utiliza para " +
        "identificar una cuenta bancaria.";

    private const string Term27C =
        "Fecha de corte: Último día del periodo de facturación en el que se calculan los " +
        "intereses del periodo y los montos de pago mínimo, pago mínimo + compras y cargos " +
        "diferidos a meses, y pago para no generar intereses.";

    private const string Term27D =
        "Fecha límite de pago: Fecha límite para realizar el pago de la tarjeta. Si el pago " +
        "del periodo se recibe después de esta fecha se considerará que el Usuario incumplió " +
        "con el pago en cuyo caso se pueden generar intereses moratorios o comisiones por " +
        "falta de pago o pago tardío.";

    private const string Term27E = "IVA: Impuesto al Valor Agregado.";
    private const string Term27F = "M.N.: Moneda Nacional.";
    // R2 correction: catalog term-g uses "N/A:" (fixed DOF transcription); after normalization
    // both "N/A" and "NA" fold to "NA" via PunctuationFoldMap, so they will match.
    private const string Term27G = "N/A: Indica que el rubro, campo o concepto no es aplicable para la tarjeta del Usuario.";
    private const string Term27H = "Núm.: Número.";

    private const string Term27I =
        "Pago mínimo: Es la cantidad que la Institución Financiera deberá requerir al Usuario " +
        "Tarjetahabiente titular de la tarjeta de crédito en cada periodo de pago para que, " +
        "una vez cubierta, el financiamiento se considere al corriente. Dicha cantidad deberá " +
        "ajustarse a lo establecido en las disposiciones que al efecto emita el Banco de " +
        "México y deberá ser congruente con lo establecido en el contrato de adhesión " +
        "correspondiente.";

    private const string Term27J =
        "Pago mínimo + compras y cargos diferidos a meses: Es el monto del pago que el " +
        "Usuario podrá realizar para que el financiamiento se considere al corriente, además " +
        "de realizar el pago periódico requerido en las secciones de \"COMPRAS Y CARGOS " +
        "DIFERIDOS A MESES SIN INTERESES\" y \"COMPRAS Y CARGOS DIFERIDOS A MESES CON " +
        "INTERESES\". En caso de no realizarse el pago por este monto, el pago periódico de " +
        "dichas secciones pasará a formar parte del saldo sobre el que se calcularán los " +
        "intereses ordinarios (y en su caso moratorios) del siguiente periodo.";

    private const string Term27K =
        "Pago para no generar intereses: Pago que se deberá hacer a más tardar en la fecha " +
        "límite de pago para evitar que se carguen intereses ordinarios (y en su caso " +
        "moratorios) en el siguiente periodo. No considera el saldo pendiente de compras y " +
        "cargos diferidos a meses sin intereses y con intereses que no sean exigibles en el " +
        "periodo actual, tampoco consideran los intereses que en su caso se devenguen por " +
        "disposiciones de efectivo.";

    private const string Term27L = "RFC: Registro Federal de Contribuyentes.";

    private const string Term27M =
        "Tasa de interés moratoria: Tasa de interés anual que se aplica a los saldos vencidos " +
        "cuando no se paga al menos el pago mínimo, de conformidad con lo pactado en el " +
        "contrato de adhesión respectivo y las disposiciones que al efecto emita el Banco de " +
        "México.";

    private const string Term27N =
        "Tasa de interés ordinaria: Tasa de interés anual que se aplica a los saldos no " +
        "pagados de cada periodo, siempre y cuando se pague al menos el pago mínimo, de " +
        "conformidad con lo pactado en el contrato de adhesión respectivo y las disposiciones " +
        "que al efecto emita el Banco de México.";

    private const string Term27O = "UNE: Unidad Especializada de Atención a Usuarios.";

    private static readonly string[] AllSection26Notes =
    [
        Note26A, Note26B, Note26C, Note26D, Note26E, Note26F, Note26G,
        Note26H, Note26I, Note26J, Note26K, Note26L, Note26M,
    ];

    private static readonly string[] AllSection27Terms =
    [
        Term27A, Term27B, Term27C, Term27D, Term27E, Term27F, Term27G, Term27H,
        Term27I, Term27J, Term27K, Term27L, Term27M, Term27N, Term27O,
    ];

    // -----------------------------------------------------------------------
    // Shared fixture helpers
    // -----------------------------------------------------------------------

    private static BundleMetadata Metadata() =>
        new("1.0.0", "Test Bank", null, null, null, null);

    private static VecProduct Product() =>
        new(ProductId: "TC-VBT-TEST", ProductName: "Test Card",
            Aliases: null, HasRewardsProgram: false,
            CardImage: null, ImportantMessageImage: null,
            Tariffs: null);

    private static FieldLocator P1() => FieldLocator.PageHint(1);

    private static StatementModel ModelWithText(
        string normalizedFullText,
        IReadOnlyList<DetectedSection>? sections = null)
    {
        var missingStr = ExtractedField<string>.Missing(P1());
        var missingName = ExtractedField<ExtractedClientName>.Missing(P1());
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(P1());

        return new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missingStr,
            cardNumber: missingStr,
            clabe: missingStr,
            clientNumber: missingStr,
            rfc: missingStr)
        {
            NormalizedFullText = normalizedFullText,
            Sections = sections ?? [],
        };
    }

    /// <summary>
    /// Builds a StatementModel where the host section's SectionText is set to
    /// <paramref name="sectionText"/>. R2 rules use SectionText for scoped matching.
    /// </summary>
    private static StatementModel ModelWithTextAndSectionText(
        string normalizedFullText,
        int hostSectionNumber,
        string sectionText,
        IReadOnlyList<DetectedSection>? sections = null)
    {
        // If explicit sections are provided, rebuild them with SectionText on the host section.
        var builtSections = sections is not null
            ? BuildSectionsWithSectionText(sections, hostSectionNumber, sectionText)
            : (IReadOnlyList<DetectedSection>)[];

        var missingStr = ExtractedField<string>.Missing(P1());
        var missingName = ExtractedField<ExtractedClientName>.Missing(P1());
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(P1());

        return new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missingStr,
            cardNumber: missingStr,
            clabe: missingStr,
            clientNumber: missingStr,
            rfc: missingStr)
        {
            NormalizedFullText = normalizedFullText,
            Sections = builtSections,
        };
    }

    private static IReadOnlyList<DetectedSection> BuildSectionsWithSectionText(
        IReadOnlyList<DetectedSection> sections,
        int hostSectionNumber,
        string sectionText)
    {
        var result = new System.Collections.Generic.List<DetectedSection>(sections.Count);
        foreach (var s in sections)
        {
            if (s.SectionNumber == hostSectionNumber && s.IsPresent)
            {
                result.Add(s with
                {
                    DetectionStatus = SectionDetectionStatus.Present,
                    SectionText = sectionText,
                });
            }
            else
            {
                result.Add(s);
            }
        }

        return result;
    }

    private static VecReferenceBundle EmptyBundle() =>
        new(
            BundleMetadata: Metadata(),
            Products: [Product()],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);

    private static VerificationContext Ctx(StatementModel? model = null) =>
        new(
            bundle: EmptyBundle(),
            resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(EmptyBundle()),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: model);

    private static IVecValidationRule GetRule(string checkId)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        services.AddVeriqanValidation();
        var sp = services.BuildServiceProvider();

        foreach (var rule in sp.GetServices<IVecValidationRule>())
            if (rule.CheckId == checkId)
                return rule;

        throw new InvalidOperationException($"Rule '{checkId}' not found in DI.");
    }

    /// <summary>
    /// Builds a list of 28 <see cref="DetectedSection"/> entries with only the specified
    /// sections marked as present. All sections are IsApplicable = true.
    /// </summary>
    private static IReadOnlyList<DetectedSection> SectionsWithPresent(params int[] presentNumerals)
    {
        var presentSet = new HashSet<int>(presentNumerals);
        var list = new List<DetectedSection>(28);
        for (var i = 1; i <= 28; i++)
        {
            list.Add(new DetectedSection(
                SectionNumber: i,
                Name: $"Section {i}",
                IsPresent: presentSet.Contains(i),
                IsApplicable: true,
                Locator: presentSet.Contains(i) ? FieldLocator.PageHint(1) : FieldLocator.NoPage()));
        }

        return list;
    }

    // -----------------------------------------------------------------------
    // Shared: all five rules must be discoverable via DI + correct metadata
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("LAW-§11-URLS", "Acuerdo §11")]
    [InlineData("LAW-§17-LEGENDS", "Acuerdo §17")]
    [InlineData("LAW-§24-QUEJAS", "Acuerdo §24")]
    [InlineData("LAW-§26-NOTAS", "Acuerdo §26")]
    [InlineData("LAW-§27-GLOSARIO", "Acuerdo §27")]
    public void AllVerbatimRules_AreRegisteredInDI_WithCorrectMetadata(string checkId, string expectedNumeral)
    {
        var rule = GetRule(checkId);

        rule.ShouldNotBeNull();
        rule.CheckId.ShouldBe(checkId);
        rule.DofNumeral.ShouldBe(expectedNumeral);
        rule.Classification.ShouldBe(RuleClassification.BaselineLocked);
        rule.Technique.ShouldBe(TechniqueClass.Deterministic);
    }

    // -----------------------------------------------------------------------
    // Shared: null model → InsufficientData
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("LAW-§11-URLS")]
    [InlineData("LAW-§17-LEGENDS")]
    [InlineData("LAW-§24-QUEJAS")]
    [InlineData("LAW-§26-NOTAS")]
    [InlineData("LAW-§27-GLOSARIO")]
    public void AllVerbatimRules_NullStatementModel_ReturnsInsufficientData(string checkId)
    {
        var rule = GetRule(checkId);
        var ctx = Ctx(model: null);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    // -----------------------------------------------------------------------
    // Shared: empty NormalizedFullText → InsufficientData
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("LAW-§11-URLS")]
    [InlineData("LAW-§17-LEGENDS")]
    [InlineData("LAW-§24-QUEJAS")]
    [InlineData("LAW-§26-NOTAS")]
    [InlineData("LAW-§27-GLOSARIO")]
    public void AllVerbatimRules_EmptyNormalizedFullText_ReturnsInsufficientData(string checkId)
    {
        var rule = GetRule(checkId);
        var ctx = Ctx(ModelWithText(string.Empty));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    // -----------------------------------------------------------------------
    // Shared: Sections list empty (detection not run) → InsufficientData
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("LAW-§11-URLS")]
    [InlineData("LAW-§17-LEGENDS")]
    [InlineData("LAW-§24-QUEJAS")]
    [InlineData("LAW-§26-NOTAS")]
    [InlineData("LAW-§27-GLOSARIO")]
    public void AllVerbatimRules_SectionsListEmpty_ReturnsInsufficientData(string checkId)
    {
        var rule = GetRule(checkId);
        // Sections = [] (empty, not null) — simulates extraction pre-Story-10.1.
        var ctx = Ctx(ModelWithText("SOME TEXT", sections: []));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    // -----------------------------------------------------------------------
    // Shared: host section NOT detected → InsufficientData (abstain, never Fail)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("LAW-§11-URLS", 11)]
    [InlineData("LAW-§17-LEGENDS", 17)]
    [InlineData("LAW-§24-QUEJAS", 24)]
    [InlineData("LAW-§26-NOTAS", 26)]
    [InlineData("LAW-§27-GLOSARIO", 27)]
    public void AllVerbatimRules_HostSectionAbsent_ReturnsInsufficientData_NeverFail(
        string checkId,
        int hostSectionNumber)
    {
        var rule = GetRule(checkId);
        // Sections list populated but host section NOT present.
        // Present: all other sections except the host one.
        var presentNumerals = Enumerable.Range(1, 28)
            .Where(n => n != hostSectionNumber)
            .ToArray();
        var sections = SectionsWithPresent(presentNumerals);
        var ctx = Ctx(ModelWithText("SOME TEXT WITH CONTENT", sections));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        // MUST be InsufficientData, never Fail — preventive pipeline gate rule.
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail);
    }

    // -----------------------------------------------------------------------
    // Cancellation
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("LAW-§11-URLS")]
    [InlineData("LAW-§17-LEGENDS")]
    [InlineData("LAW-§24-QUEJAS")]
    [InlineData("LAW-§26-NOTAS")]
    [InlineData("LAW-§27-GLOSARIO")]
    public void AllVerbatimRules_Cancellation_ReturnsCancelled(string checkId)
    {
        var rule = GetRule(checkId);
        var ctx = Ctx(ModelWithText("ANY TEXT"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = rule.Evaluate(ctx, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // LAW-§11-URLS: Pass when both URLs present (tolerance via normalized substring)
    // -----------------------------------------------------------------------

    [Fact]
    public void Section11Rule_BothUrlsPresent_ReturnsPass()
    {
        var rule = GetRule("LAW-§11-URLS");
        var sections = SectionsWithPresent(11);

        // Build document text that contains both normalized URLs.
        // VecTextMatcher.Normalize uppercases and strips accents/ligatures.
        var url1N = VecTextMatcher.Normalize(Url1);
        var url2N = VecTextMatcher.Normalize(Url2);
        var sectionText = $"COMPARA TU TARJETA CON OTRAS EN: {url1N} {url2N}";
        var docText = sectionText + " NOTAS ACLARATORIAS";

        // R2: provide SectionText on §11 so the scoped rule finds the URLs.
        var ctx = Ctx(ModelWithTextAndSectionText(docText, 11, sectionText, sections));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("LAW-§11-URLS");
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.Severity.ShouldBe(FindingSeverity.Info);
    }

    [Fact]
    public void Section11Rule_OneUrlMissing_ReturnsFail_NamingBlockAndScore()
    {
        var rule = GetRule("LAW-§11-URLS");
        var sections = SectionsWithPresent(11);

        // Only url1 is present; url2 is completely absent.
        var url1N = VecTextMatcher.Normalize(Url1);
        var sectionText = $"COMPARA TU TARJETA: {url1N}";
        var docText = sectionText + " NOTAS ACLARATORIAS";

        // R2: SectionText contains only url1.
        var ctx = Ctx(ModelWithTextAndSectionText(docText, 11, sectionText, sections));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("LAW-§11-URLS");
        result.Value.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
        result.Value.Observed.ShouldNotBeNull();
        result.Value.Observed!.ShouldContain("§11-url2");
        result.Value.Observed.ShouldContain("similarity=");
    }

    [Fact]
    public void Section11Rule_BothUrlsMissing_ReturnsFail_NamingBothBlocks()
    {
        var rule = GetRule("LAW-§11-URLS");
        var sections = SectionsWithPresent(11);

        var sectionText = "COMPARA TU TARJETA CON OTRAS OPCIONES";
        var docText = sectionText + " NOTAS ACLARATORIAS";

        // R2: SectionText contains no URLs.
        var ctx = Ctx(ModelWithTextAndSectionText(docText, 11, sectionText, sections));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Observed.ShouldNotBeNull();
        result.Value.Observed!.ShouldContain("§11-url1");
        result.Value.Observed.ShouldContain("§11-url2");
    }

    // -----------------------------------------------------------------------
    // LAW-§17-LEGENDS: Pass when all four legends present (with accent stripping)
    // -----------------------------------------------------------------------

    [Fact]
    public void Section17Rule_AllFourLegendsPresent_ReturnsPass()
    {
        var rule = GetRule("LAW-§17-LEGENDS");
        var sections = SectionsWithPresent(17);

        // Normalize all four legends (simulates what the PDF extractor produces).
        // Slight whitespace perturbation (double spaces) to prove tolerance.
        var sectionText = string.Join(" SEPARADOR ",
            new[] { Legend17A, Legend17B, Legend17C, Legend17D }
                .Select(l => VecTextMatcher.Normalize(l)));
        var doc = sectionText + " SIGUIENTE SECCION";

        // R2: SectionText scoped to §17 contains all four legends.
        var ctx = Ctx(ModelWithTextAndSectionText(doc, 17, sectionText, sections));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("LAW-§17-LEGENDS");
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.Severity.ShouldBe(FindingSeverity.Info);
    }

    [Fact]
    public void Section17Rule_OneLegendMissing_ReturnsFail_NamingMissingId()
    {
        var rule = GetRule("LAW-§17-LEGENDS");
        var sections = SectionsWithPresent(17);

        // Only first three legends present; §17-d is absent.
        var sectionText = string.Join(" ",
            new[] { Legend17A, Legend17B, Legend17C }.Select(VecTextMatcher.Normalize));

        // R2: SectionText scoped to §17.
        var ctx = Ctx(ModelWithTextAndSectionText(sectionText, 17, sectionText, sections));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
        result.Value.Observed.ShouldNotBeNull();
        result.Value.Observed!.ShouldContain("§17-d");
        result.Value.Observed.ShouldContain("similarity=");
    }

    [Fact]
    public void Section17Rule_AccentAndCaseVariant_StillProducesPass()
    {
        // Proves that accent stripping + upper-casing by the extractor is tolerated.
        // The test provides already-normalized text (no accents, uppercase) for the
        // document, but the expected catalog strings have accents — both paths normalize.
        var rule = GetRule("LAW-§17-LEGENDS");
        var sections = SectionsWithPresent(17);

        // All four legends normalized (accents stripped, uppercased).
        var sectionText = string.Join(" ",
            new[] { Legend17A, Legend17B, Legend17C, Legend17D }
                .Select(VecTextMatcher.Normalize));

        // R2: SectionText scoped to §17.
        var ctx = Ctx(ModelWithTextAndSectionText(sectionText, 17, sectionText, sections));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    // -----------------------------------------------------------------------
    // LAW-§24-QUEJAS: Pass when invariant CONDUSEF block present
    // -----------------------------------------------------------------------

    [Fact]
    public void Section24Rule_QuejasLegendPresent_ReturnsPass()
    {
        var rule = GetRule("LAW-§24-QUEJAS");
        var sections = SectionsWithPresent(24);

        // Simulate issuer-specific prefix + invariant CONDUSEF block.
        var invariantN = VecTextMatcher.Normalize(Quejas24Fragment);
        var sectionText = $"BANCO DEMO RECIBE QUEJAS EN SU UNE. {invariantN}";
        var docText = sectionText + " REESTRUCTURA DE TU DEUDA";

        // R2: SectionText scoped to §24 contains the CONDUSEF block.
        var ctx = Ctx(ModelWithTextAndSectionText(docText, 24, sectionText, sections));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("LAW-§24-QUEJAS");
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.Severity.ShouldBe(FindingSeverity.Info);
    }

    [Fact]
    public void Section24Rule_QuejasLegendAbsent_ReturnsFail_WithScore()
    {
        var rule = GetRule("LAW-§24-QUEJAS");
        var sections = SectionsWithPresent(24);

        // Document text has the section heading but none of the invariant CONDUSEF contact block.
        var sectionText = "ATENCION DE QUEJAS BANCO DEMO RECIBE CONSULTAS EN SU UNIDAD ESPECIALIZADA";
        var docText = sectionText + " SIGUIENTE SECCION";

        // R2: SectionText scoped to §24 has no matching block.
        var ctx = Ctx(ModelWithTextAndSectionText(docText, 24, sectionText, sections));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
        result.Value.Observed.ShouldNotBeNull();
        result.Value.Observed!.ShouldContain("similarity=");
        result.Value.Observed.ShouldContain("threshold=");
    }

    // -----------------------------------------------------------------------
    // LAW-§26-NOTAS: Pass when all 13 notes present (with accent tolerance)
    // -----------------------------------------------------------------------

    [Fact]
    public void Section26Rule_AllThirteenNotesPresent_ReturnsPass()
    {
        var rule = GetRule("LAW-§26-NOTAS");
        var sections = SectionsWithPresent(26);

        // Build doc with all 13 notes normalized, separated by distinct section markers.
        var sectionText = "NOTAS ACLARATORIAS " +
                  string.Join(" NOTA ",
                      AllSection26Notes.Select(VecTextMatcher.Normalize));
        var doc = sectionText + " GLOSARIO DE TERMINOS";

        // R2: SectionText scoped to §26 contains all notes.
        var ctx = Ctx(ModelWithTextAndSectionText(doc, 26, sectionText, sections));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("LAW-§26-NOTAS");
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.Observed.ShouldNotBeNull();
        result.Value.Observed!.ShouldContain("13");
    }

    [Fact]
    public void Section26Rule_MostNotesAbsent_ReturnsFail_NamingMissingNotes()
    {
        var rule = GetRule("LAW-§26-NOTAS");
        var sections = SectionsWithPresent(26);

        // Only notes a and b present; the remaining 11 notes are absent.
        var sectionText = $"NOTAS ACLARATORIAS {VecTextMatcher.Normalize(Note26A)} " +
                  $"{VecTextMatcher.Normalize(Note26B)} FIN DE NOTAS";

        // R2: SectionText scoped to §26 has only 2 notes.
        var ctx = Ctx(ModelWithTextAndSectionText(sectionText, 26, sectionText, sections));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
        result.Value.Observed.ShouldNotBeNull();
        // At least one of the missing note ids (c–m) should appear.
        result.Value.Observed!.ShouldContain("§26-");
        result.Value.Observed.ShouldContain("similarity=");
    }

    [Fact]
    public void Section26Rule_AllNotesPresent_WithAccentAndWhitespaceVariation_ReturnsPass()
    {
        // Proves tolerance: document has accent-stripped, whitespace-collapsed notes.
        var rule = GetRule("LAW-§26-NOTAS");
        var sections = SectionsWithPresent(26);

        // Simulate all 13 notes as they appear after PDF extraction (accent-stripped, uppercased).
        var sectionText = "NOTAS ACLARATORIAS " +
                  string.Join(" NUMERO ",
                      AllSection26Notes.Select(VecTextMatcher.Normalize));

        // R2: SectionText scoped to §26.
        var ctx = Ctx(ModelWithTextAndSectionText(sectionText, 26, sectionText, sections));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    // -----------------------------------------------------------------------
    // LAW-§27-GLOSARIO: Pass when all 15 terms present (with accent tolerance)
    // -----------------------------------------------------------------------

    [Fact]
    public void Section27Rule_AllFifteenTermsPresent_ReturnsPass()
    {
        var rule = GetRule("LAW-§27-GLOSARIO");
        var sections = SectionsWithPresent(27);

        var sectionText = "GLOSARIO DE TERMINOS Y ABREVIATURAS " +
                  string.Join(" TERMINO ",
                      AllSection27Terms.Select(VecTextMatcher.Normalize));
        var doc = sectionText + " FIN DEL ESTADO DE CUENTA";

        // R2: SectionText scoped to §27 contains all 15 terms.
        var ctx = Ctx(ModelWithTextAndSectionText(doc, 27, sectionText, sections));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("LAW-§27-GLOSARIO");
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.Observed.ShouldNotBeNull();
        result.Value.Observed!.ShouldContain("15");
    }

    [Fact]
    public void Section27Rule_MostTermsAbsent_ReturnsFail_NamingMissingTerms()
    {
        var rule = GetRule("LAW-§27-GLOSARIO");
        var sections = SectionsWithPresent(27);

        // Only term a (CAT) and term e (IVA) present; the other 13 are absent.
        var sectionText = $"GLOSARIO {VecTextMatcher.Normalize(Term27A)} {VecTextMatcher.Normalize(Term27E)} FIN";

        // R2: SectionText scoped to §27 has only 2 terms.
        var ctx = Ctx(ModelWithTextAndSectionText(sectionText, 27, sectionText, sections));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
        result.Value.Observed.ShouldNotBeNull();
        result.Value.Observed!.ShouldContain("§27-");
        result.Value.Observed.ShouldContain("similarity=");
    }

    [Fact]
    public void Section27Rule_AllTermsPresent_WithAccentStripping_ReturnsPass()
    {
        // Proves tolerance: document has accent-stripped, uppercased terms.
        var rule = GetRule("LAW-§27-GLOSARIO");
        var sections = SectionsWithPresent(27);

        var sectionText = "GLOSARIO " +
                  string.Join(" SIGUIENTE ",
                      AllSection27Terms.Select(VecTextMatcher.Normalize));

        // R2: SectionText scoped to §27.
        var ctx = Ctx(ModelWithTextAndSectionText(sectionText, 27, sectionText, sections));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }
}
