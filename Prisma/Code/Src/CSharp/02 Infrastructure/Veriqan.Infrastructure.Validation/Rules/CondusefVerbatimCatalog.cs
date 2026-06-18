using System.Collections.Generic;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// Verbatim text blocks mandated by the CONDUSEF <i>Acuerdo relativo al formato de estado de
/// cuenta estandarizado de tarjeta de crédito para personas físicas</i>
/// (DOF 29-Dec-2022, mandatory since 17-Oct-2024).
/// </summary>
/// <remarks>
/// <para>
/// All string constants are transcribed as faithfully as possible from the official DOF PDF.
/// Matching is performed via <see cref="ExxerCube.Prisma.Veriqan.Domain.Extraction.VecTextMatcher"/>
/// (tolerant similarity) so near-exact transcription is acceptable; minor whitespace or
/// accent differences do not affect pass/fail outcome at the configured threshold.
/// </para>
/// <para>
/// Constants are grouped by the DOF <i>Acuerdo</i> section numeral that mandates them.
/// </para>
/// <para>
/// <b>Threshold knob:</b> <see cref="DefaultSimilarityThreshold"/> is the global default.
/// Individual rules read from the tenant profile first; this constant is the fallback.
/// </para>
/// </remarks>
internal static class CondusefVerbatimCatalog
{
    // -----------------------------------------------------------------------
    // Global similarity threshold (the configurable knob — Story 10.4)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Default minimum similarity score (Jaccard / Levenshtein max) required for a verbatim
    /// block to be considered present.  Rules read a per-tenant override first; when none is
    /// configured this constant is used.
    /// </summary>
    /// <remarks>
    /// 0.82 was chosen to tolerate: PDF ligature folding, soft-hyphen removal, minor OCR
    /// character substitutions, and line-wrap whitespace merging — while still rejecting a
    /// block that has been meaningfully altered or is absent.
    /// </remarks>
    public const double DefaultSimilarityThreshold = 0.82;

    // -----------------------------------------------------------------------
    // §11 — "Compara tu tarjeta" — two exact URLs
    // Source: Acuerdo §11 / Formato sección 11 (DOF p. 615)
    // -----------------------------------------------------------------------

    /// <summary>
    /// §11 URL 1: CONDUSEF tarjetas comparador portal.
    /// The format requires the full URL text to appear in section 11 of the statement.
    /// </summary>
    public const string Section11Url1 = "https://tarjetas.condusef.gob.mx/index.php";

    /// <summary>
    /// §11 URL 2: Banxico comparador portal.
    /// </summary>
    public const string Section11Url2 = "https://comparador.banxico.org.mx/";

    // -----------------------------------------------------------------------
    // §17 — "Mensajes adicionales" — art-6-IV mandatory legends
    // Source: Acuerdo §17 / Formato sección 17 (DOF p. 616)
    // The four legends shown in the format are mandatory when the section is present;
    // the guía de llenado instructs that at least the art-6-IV applicable legends must appear.
    // -----------------------------------------------------------------------

    /// <summary>
    /// §17 Leyenda art-6-IV (a): tasa variable warning.
    /// </summary>
    public const string Section17Legend1 =
        "Al ser tu crédito de tasa variable, los intereses pueden aumentar.";

    /// <summary>
    /// §17 Leyenda art-6-IV (b): morosidad warning.
    /// </summary>
    public const string Section17Legend2 =
        "Incumplir tus obligaciones te puede generar comisiones e intereses moratorios.";

    /// <summary>
    /// §17 Leyenda art-6-IV (c): sobreendeudamiento warning.
    /// </summary>
    public const string Section17Legend3 =
        "Contratar créditos que excedan tu capacidad de pago afecta tu historial crediticio.";

    /// <summary>
    /// §17 Leyenda art-6-IV (d): pago mínimo warning.
    /// DOF text ends with a period ("."); corrected from the original transcription.
    /// </summary>
    public const string Section17Legend4 =
        "Realizar sólo el pago mínimo aumenta el tiempo de pago y el costo de la deuda.";

    // -----------------------------------------------------------------------
    // §24 — "Atención de quejas" — UNE legend
    // Source: Acuerdo §24 / Formato sección 24 (DOF p. 618)
    // The template uses [placeholder] brackets for institution-specific data;
    // the invariant portions (CONDUSEF contact info) are what we verify.
    // -----------------------------------------------------------------------

    /// <summary>
    /// §24 — invariant CONDUSEF contact legend fragment.
    /// The issuer fills in its own name/address/phone; the CONDUSEF contact block is fixed.
    /// Cross-check: phones 800-999-8080 and 55-53-40-09-99 (DOF p. 618).
    /// </summary>
    public const string Section24QuejasLegendFragment =
        "podrá acudir a la Comisión Nacional para la Protección y Defensa de los Usuarios de " +
        "Servicios Financieros. Correo electrónico: asesoria@condusef.gob.mx, chat en línea " +
        "www.condusef.gob.mx o Tel: 800 999 8080 y 55 53 40 09 99.";

    // -----------------------------------------------------------------------
    // §26 — "Notas aclaratorias" — 13 mandatory verbatim notes
    // Source: Acuerdo §26 / Formato sección 26 (DOF p. 619)
    // Notes are labeled a)–m) in the DOF. Each string below is the note body
    // (excluding the "[N o número de nota consecutivo]" superscript bracket).
    // -----------------------------------------------------------------------

    /// <summary>
    /// §26 Nota a) — fecha límite de pago / siguiente día hábil.
    /// </summary>
    public const string Section26NoteA =
        "Tienes como límite esta fecha para realizar tu pago, evitar el cargo de comisiones " +
        "por pago tardío, falta de pago o intereses moratorios y mantener tu crédito al " +
        "corriente. Si esta fecha corresponde a un día inhábil bancario, puedes realizar el " +
        "pago el siguiente día hábil bancario sin que proceda el cobro de comisiones por pago " +
        "tardío, falta de pago o intereses moratorios.";

    /// <summary>
    /// §26 Nota b) — pago para no generar intereses ordinarios.
    /// </summary>
    public const string Section26NoteB =
        "Este es el saldo a pagar para no generar intereses ordinarios (excepto los asociados " +
        "a disposiciones de efectivo o compras diferidas con intereses, cuyos intereses se " +
        "continuarán cobrando de conformidad con la tasa acordada y el plazo de diferimiento " +
        "al que se encuentren sujetos) ni intereses moratorios o comisiones por falta de pago " +
        "o pago tardío (en caso de resultar aplicables) en el siguiente periodo. No considera " +
        "el saldo pendiente de compras y cargos diferidos a meses que no es exigible en el " +
        "periodo actual.";

    /// <summary>
    /// §26 Nota c) — intereses ordinarios por no pagar mensualidad a meses.
    /// </summary>
    public const string Section26NoteC =
        "Si no pagas la mensualidad de tus compras a meses, en adición al pago mínimo, éstas " +
        "generarán intereses ordinarios en el siguiente periodo.";

    /// <summary>
    /// §26 Nota d) — pago mínimo / intereses sobre saldo no pagado.
    /// </summary>
    public const string Section26NoteD =
        "El pago mínimo es el monto mínimo que debes pagar para que tu crédito se considere " +
        "al corriente y no se te cobren comisiones por pago tardío, falta de pago o intereses " +
        "moratorios. El monto incluye los intereses que se hayan generado en el periodo y el " +
        "IVA correspondiente. Este pago no te exime de pagar intereses ordinarios. Si solo " +
        "pagas el mínimo, se generarán intereses sobre el saldo que no fue cubierto con el " +
        "pago, estos intereses aparecerán reflejados en el siguiente periodo.";

    /// <summary>
    /// §26 Nota e) — datos de la tabla pueden modificarse.
    /// </summary>
    public const string Section26NoteE =
        "Los datos de esta tabla pueden modificarse en los estados de cuenta subsecuentes " +
        "debido a que varían en función del uso y pagos realizados a la tarjeta.";

    /// <summary>
    /// §26 Nota f) — intereses calculados a la tasa de interés a la fecha de corte.
    /// </summary>
    public const string Section26NoteF =
        "Estos intereses se calculan considerando la tasa de interés a la fecha de corte. En " +
        "caso de que la tasa de interés se modifique en los siguientes periodos, el monto de " +
        "los intereses calculados será distinto. Además, el monto de intereses calculados no " +
        "considera aquellos derivados de compras y cargos diferidos a meses con intereses.";

    /// <summary>
    /// §26 Nota g) — monto exigible de la mensualidad a meses.
    /// </summary>
    public const string Section26NoteG =
        "Es el monto exigible de la mensualidad destinado a amortizar el capital de: las " +
        "compras a meses sin intereses, las compras o cargos diferidos a meses con intereses " +
        "y las otras líneas de crédito adicionales a la línea de crédito de la tarjeta, en " +
        "su caso.";

    /// <summary>
    /// §26 Nota h) — intereses del periodo calculados en función de la tasa de interés.
    /// </summary>
    public const string Section26NoteH =
        "Los intereses del periodo se calculan en función de la tasa de interés aplicable a " +
        "los distintos saldos. Revisa la sección \"SALDO SOBRE EL QUE SE CALCULARON LOS " +
        "INTERESES DEL PERIODO\" en este estado de cuenta.";

    /// <summary>
    /// §26 Nota i) — incluye intereses ordinarios y moratorios.
    /// </summary>
    public const string Section26NoteI =
        "Incluye los intereses ordinarios y moratorios de compras regulares, así como de " +
        "compras y cargos a meses con intereses.";

    /// <summary>
    /// §26 Nota j) — consultar glosario para interpretar el indicador.
    /// </summary>
    public const string Section26NoteJ =
        "Consulta la sección \"GLOSARIO DE TÉRMINOS Y ABREVIATURAS\" para conocer cómo " +
        "interpretar este indicador.";

    /// <summary>
    /// §26 Nota k) — saldo deudor total = pago para no generar intereses + saldo pendiente.
    /// </summary>
    public const string Section26NoteK =
        "El saldo deudor total es la suma del pago para no generar intereses y el saldo " +
        "pendiente a meses.";

    /// <summary>
    /// §26 Nota l) — intereses del periodo calculados usando la tasa de interés anual aplicable.
    /// </summary>
    public const string Section26NoteL =
        "Los intereses del periodo se calculan usando la tasa de interés aplicable a los días " +
        "del periodo, la cual se obtiene dividiendo la tasa de interés anual aplicable, de " +
        "acuerdo al renglón que corresponda (ya sea ordinaria, moratoria, preferencial, de " +
        "cargos y compras diferidas a meses, por disposiciones de efectivo u otras), entre " +
        "360 días y multiplicando el resultado por el número de días del periodo.";

    /// <summary>
    /// §26 Nota m) — pago requerido de compras a meses con intereses ya incluye intereses.
    /// </summary>
    public const string Section26NoteM =
        "El pago requerido de compras o cargos a meses con intereses ya incluye los intereses " +
        "pactados al momento de la compra a la tasa de interés acordada.";

    /// <summary>
    /// All thirteen §26 notes in document order (a–m).
    /// </summary>
    public static readonly IReadOnlyList<(string Id, string Text)> Section26Notes =
    [
        ("§26-a", Section26NoteA),
        ("§26-b", Section26NoteB),
        ("§26-c", Section26NoteC),
        ("§26-d", Section26NoteD),
        ("§26-e", Section26NoteE),
        ("§26-f", Section26NoteF),
        ("§26-g", Section26NoteG),
        ("§26-h", Section26NoteH),
        ("§26-i", Section26NoteI),
        ("§26-j", Section26NoteJ),
        ("§26-k", Section26NoteK),
        ("§26-l", Section26NoteL),
        ("§26-m", Section26NoteM),
    ];

    // -----------------------------------------------------------------------
    // §27 — "Glosario de términos y abreviaturas" — 15 verbatim definitions
    // Source: Acuerdo §27 / Formato sección 27 (DOF p. 620)
    // Each entry is "Term": "Definition." as printed in the DOF.
    // -----------------------------------------------------------------------

    /// <summary>
    /// §27 Term a) — CAT definition.
    /// </summary>
    public const string Section27TermA =
        "CAT: Costo Anual Total de financiamiento expresado en términos porcentuales anuales " +
        "que, para fines informativos y de comparación, incorpora la totalidad de los costos " +
        "y gastos inherentes a los créditos, préstamos o financiamientos que otorgan las " +
        "Instituciones Financieras, de conformidad con las disposiciones que al efecto emita " +
        "el Banco de México.";

    /// <summary>
    /// §27 Term b) — CLABE definition.
    /// </summary>
    public const string Section27TermB =
        "CLABE: es la Clave Bancaria Estandarizada de dieciocho dígitos que se utiliza para " +
        "identificar una cuenta bancaria.";

    /// <summary>
    /// §27 Term c) — Fecha de corte definition.
    /// </summary>
    public const string Section27TermC =
        "Fecha de corte: Último día del periodo de facturación en el que se calculan los " +
        "intereses del periodo y los montos de pago mínimo, pago mínimo + compras y cargos " +
        "diferidos a meses, y pago para no generar intereses.";

    /// <summary>
    /// §27 Term d) — Fecha límite de pago definition.
    /// </summary>
    public const string Section27TermD =
        "Fecha límite de pago: Fecha límite para realizar el pago de la tarjeta. Si el pago " +
        "del periodo se recibe después de esta fecha se considerará que el Usuario incumplió " +
        "con el pago en cuyo caso se pueden generar intereses moratorios o comisiones por " +
        "falta de pago o pago tardío.";

    /// <summary>
    /// §27 Term e) — IVA definition.
    /// </summary>
    public const string Section27TermE =
        "IVA: Impuesto al Valor Agregado.";

    /// <summary>
    /// §27 Term f) — M.N. definition.
    /// </summary>
    public const string Section27TermF =
        "M.N.: Moneda Nacional.";

    /// <summary>
    /// §27 Term g) — N/A definition.
    /// DOF text uses "N/A:" (with the slash); corrected from the original "NA:" transcription.
    /// </summary>
    public const string Section27TermG =
        "N/A: Indica que el rubro, campo o concepto no es aplicable para la tarjeta del Usuario.";

    /// <summary>
    /// §27 Term h) — Núm. definition.
    /// </summary>
    public const string Section27TermH =
        "Núm.: Número.";

    /// <summary>
    /// §27 Term i) — Pago mínimo definition.
    /// </summary>
    public const string Section27TermI =
        "Pago mínimo: Es la cantidad que la Institución Financiera deberá requerir al Usuario " +
        "Tarjetahabiente titular de la tarjeta de crédito en cada periodo de pago para que, " +
        "una vez cubierta, el financiamiento se considere al corriente. Dicha cantidad deberá " +
        "ajustarse a lo establecido en las disposiciones que al efecto emita el Banco de " +
        "México y deberá ser congruente con lo establecido en el contrato de adhesión " +
        "correspondiente.";

    /// <summary>
    /// §27 Term j) — Pago mínimo + compras y cargos diferidos a meses definition.
    /// </summary>
    public const string Section27TermJ =
        "Pago mínimo + compras y cargos diferidos a meses: Es el monto del pago que el " +
        "Usuario podrá realizar para que el financiamiento se considere al corriente, además " +
        "de realizar el pago periódico requerido en las secciones de \"COMPRAS Y CARGOS " +
        "DIFERIDOS A MESES SIN INTERESES\" y \"COMPRAS Y CARGOS DIFERIDOS A MESES CON " +
        "INTERESES\". En caso de no realizarse el pago por este monto, el pago periódico de " +
        "dichas secciones pasará a formar parte del saldo sobre el que se calcularán los " +
        "intereses ordinarios (y en su caso moratorios) del siguiente periodo.";

    /// <summary>
    /// §27 Term k) — Pago para no generar intereses definition.
    /// </summary>
    public const string Section27TermK =
        "Pago para no generar intereses: Pago que se deberá hacer a más tardar en la fecha " +
        "límite de pago para evitar que se carguen intereses ordinarios (y en su caso " +
        "moratorios) en el siguiente periodo. No considera el saldo pendiente de compras y " +
        "cargos diferidos a meses sin intereses y con intereses que no sean exigibles en el " +
        "periodo actual, tampoco consideran los intereses que en su caso se devenguen por " +
        "disposiciones de efectivo.";

    /// <summary>
    /// §27 Term l) — RFC definition.
    /// </summary>
    public const string Section27TermL =
        "RFC: Registro Federal de Contribuyentes.";

    /// <summary>
    /// §27 Term m) — Tasa de interés moratoria definition.
    /// </summary>
    public const string Section27TermM =
        "Tasa de interés moratoria: Tasa de interés anual que se aplica a los saldos vencidos " +
        "cuando no se paga al menos el pago mínimo, de conformidad con lo pactado en el " +
        "contrato de adhesión respectivo y las disposiciones que al efecto emita el Banco de " +
        "México.";

    /// <summary>
    /// §27 Term n) — Tasa de interés ordinaria definition.
    /// </summary>
    public const string Section27TermN =
        "Tasa de interés ordinaria: Tasa de interés anual que se aplica a los saldos no " +
        "pagados de cada periodo, siempre y cuando se pague al menos el pago mínimo, de " +
        "conformidad con lo pactado en el contrato de adhesión respectivo y las disposiciones " +
        "que al efecto emita el Banco de México.";

    /// <summary>
    /// §27 Term o) — UNE definition.
    /// </summary>
    public const string Section27TermO =
        "UNE: Unidad Especializada de Atención a Usuarios.";

    /// <summary>
    /// All fifteen §27 glossary terms in document order (a–o).
    /// </summary>
    public static readonly IReadOnlyList<(string Id, string Text)> Section27Terms =
    [
        ("§27-a", Section27TermA),
        ("§27-b", Section27TermB),
        ("§27-c", Section27TermC),
        ("§27-d", Section27TermD),
        ("§27-e", Section27TermE),
        ("§27-f", Section27TermF),
        ("§27-g", Section27TermG),
        ("§27-h", Section27TermH),
        ("§27-i", Section27TermI),
        ("§27-j", Section27TermJ),
        ("§27-k", Section27TermK),
        ("§27-l", Section27TermL),
        ("§27-m", Section27TermM),
        ("§27-n", Section27TermN),
        ("§27-o", Section27TermO),
    ];

    /// <summary>
    /// §17 mandatory legends in order (art-6-IV, a–d).
    /// </summary>
    public static readonly IReadOnlyList<(string Id, string Text)> Section17Legends =
    [
        ("§17-a", Section17Legend1),
        ("§17-b", Section17Legend2),
        ("§17-c", Section17Legend3),
        ("§17-d", Section17Legend4),
    ];
}
