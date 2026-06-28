using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// LAW-DUC-ART27-GAT: GAT-legend presence rule — Disposición Única de la CONDUSEF,
/// Artículo 27.  The article mandates that the siglas "GAT" (Ganancia Anual Total) are
/// displayed in statements for <em>operaciones pasivas</em> (savings/deposit products).
/// </summary>
/// <remarks>
/// <para>
/// <b>Abstain-safe design (never Fail on credit-card statements):</b>
/// The current system has no product-type discriminator in <see cref="StatementModel"/>.
/// GAT is NOT mandated for credit cards.  Therefore this rule emits
/// <c>InsufficientData</c> — not <c>Fail</c> — when neither "GAT" nor
/// "GANANCIA ANUAL TOTAL" is found in the document text.
/// This prevents false REDs on every credit-card statement while still surfacing a
/// <c>Pass</c> when the legend is genuinely present.
/// </para>
/// <para>
/// <b>Matching strategy:</b> searches <see cref="StatementModel.NormalizedFullText"/>
/// (already upper-case and accent-stripped by the extraction stage) for either of the
/// substrings "GAT" or "GANANCIA ANUAL TOTAL" after normalizing both tokens through
/// <see cref="TextNormalizer.Normalize"/>.  The check is therefore accent- and
/// case-insensitive by construction.
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item>No <see cref="StatementModel"/> or <see cref="StatementModel.NormalizedFullText"/>
///         is empty (extraction has not run or the PDF has no text layer).</item>
///   <item>Neither "GAT" nor "GANANCIA ANUAL TOTAL" is found in the normalized text —
///         product type cannot be determined, so the rule abstains rather than failing.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Section27GatLegendRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    // Normalized at class-init time; consistent with the legend-rule pattern and guards
    // future normalizer changes (these tokens are ASCII-clean so normalization is a no-op today).
    private static readonly string NormalizedGat = TextNormalizer.Normalize("GAT");
    private static readonly string NormalizedLongForm = TextNormalizer.Normalize("GANANCIA ANUAL TOTAL");

    /// <inheritdoc />
    public string CheckId => "LAW-DUC-ART27-GAT";

    /// <inheritdoc />
    public string DofNumeral => "Disposición Única CONDUSEF Art. 27";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.TenantTightenableOnly;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // Gate: statement text must be present.
        var model = ctx.StatementModel;
        if (model is null || string.IsNullOrEmpty(model.NormalizedFullText))
            return InsufficientData(
                "StatementModel or NormalizedFullText is not populated — extraction has not run.");

        var text = model.NormalizedFullText;

        var gatPresent = text.Contains(NormalizedGat, System.StringComparison.Ordinal);
        var longFormPresent = text.Contains(NormalizedLongForm, System.StringComparison.Ordinal);

        if (gatPresent || longFormPresent)
        {
            var which = longFormPresent ? "GANANCIA ANUAL TOTAL" : "GAT";
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"GAT legend found in statement text (\"{which}\" present).",
                    toleranceApplied: null,
                    locator: FieldLocator.PageHint(1)));
        }

        // Neither form found — product type is unknown, so we abstain instead of failing.
        // GAT applies only to operaciones pasivas; emitting Fail here would falsely RED every
        // credit-card statement this system currently processes.
        return InsufficientData(
            "GAT legend not present; GAT applies only to operaciones pasivas " +
            "(Disposición Única Art. 27) and this system cannot yet determine product type " +
            "— abstaining rather than failing.");
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}
