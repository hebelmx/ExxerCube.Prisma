using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation;

/// <summary>
/// Production implementation of <see cref="IVecValidationEngine"/>.
/// Iterates every registered <see cref="IVecValidationRule"/>, collects findings,
/// and returns them in deterministic <see cref="RuleFinding.CheckId"/> order.
/// </summary>
/// <remarks>
/// <para>
/// <b>Batch isolation (NFR-6):</b> if a rule returns a failure <c>Result</c> or throws
/// an unexpected exception, the engine captures an
/// <see cref="FindingVerdict.InsufficientData"/> finding for that rule's
/// <see cref="IVecValidationRule.CheckId"/> and continues with the remaining rules.
/// The overall result is still successful (a non-empty or empty findings list) unless
/// cancellation was requested.
/// </para>
/// <para>
/// <b>Determinism (NFR-5):</b> findings are sorted by <see cref="RuleFinding.CheckId"/>
/// using ordinal comparison before being returned, so output order is stable regardless
/// of DI registration order.
/// </para>
/// </remarks>
internal sealed class VecValidationEngine : IVecValidationEngine
{
    private const string EngineFallbackVersion = "engine-error";

    private readonly IReadOnlyList<IVecValidationRule> _rules;
    private readonly ILogger<VecValidationEngine> _logger;

    /// <summary>
    /// Initializes a new <see cref="VecValidationEngine"/>.
    /// </summary>
    /// <param name="rules">All registered validation rules (injected via DI scan).</param>
    /// <param name="logger">Logger for engine-level diagnostics.</param>
    public VecValidationEngine(
        IEnumerable<IVecValidationRule> rules,
        ILogger<VecValidationEngine> logger)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(logger);
        _rules = new List<IVecValidationRule>(rules).AsReadOnly();
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<RuleFinding>>> RunAsync(
        VerificationContext ctx,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (ct.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<IReadOnlyList<RuleFinding>>());

        var findings = new List<RuleFinding>(_rules.Count);

        foreach (var rule in _rules)
        {
            // Mid-run cancellation check between rules
            if (ct.IsCancellationRequested)
                return Task.FromResult(ResultExtensions.Cancelled<IReadOnlyList<RuleFinding>>());

            RuleFinding finding;
            try
            {
                var ruleResult = rule.Evaluate(ctx, ct);

                if (ruleResult.IsCancelled())
                    return Task.FromResult(ResultExtensions.Cancelled<IReadOnlyList<RuleFinding>>());

                if (ruleResult.IsSuccess && ruleResult.Value is not null)
                {
                    finding = ruleResult.Value;
                }
                else
                {
                    // Rule returned a failure Result; capture as InsufficientData and continue (NFR-6)
                    _logger.LogWarning(
                        "Rule {CheckId} returned a failure result: {Error}. Captured as InsufficientData.",
                        rule.CheckId,
                        ruleResult.Error);

                    finding = RuleFinding.InsufficientData(
                        checkId: rule.CheckId,
                        technique: rule.Technique,
                        engineVersion: EngineFallbackVersion,
                        reason: $"Rule error: {ruleResult.Error}");
                }
            }
            catch (Exception ex)
            {
                // Unexpected exception; isolate the rule and continue batch (NFR-6)
                _logger.LogError(
                    ex,
                    "Rule {CheckId} threw an unexpected exception. Captured as InsufficientData.",
                    rule.CheckId);

                finding = RuleFinding.InsufficientData(
                    checkId: rule.CheckId,
                    technique: rule.Technique,
                    engineVersion: EngineFallbackVersion,
                    reason: $"Unexpected exception: {ex.GetType().Name}");
            }

            // Stamp the DOF numeral onto the finding (NFR-7 — single central point, Story 9.2).
            // Applied here so rule authors never need to pass the numeral through their factory calls.
            finding = finding with { DofNumeral = rule.DofNumeral };

            findings.Add(finding);
        }

        // Deterministic ordering by CheckId (NFR-5)
        findings.Sort(static (a, b) =>
            string.Compare(a.CheckId, b.CheckId, StringComparison.Ordinal));

        IReadOnlyList<RuleFinding> result = findings.AsReadOnly();
        return Task.FromResult(Result<IReadOnlyList<RuleFinding>>.WithSuccess(result));
    }

    /// <inheritdoc />
    public IReadOnlyList<(string CheckId, string DofNumeral)> GetCoverageMap()
    {
        var map = new List<(string CheckId, string DofNumeral)>(_rules.Count);

        foreach (var rule in _rules)
            map.Add((rule.CheckId, rule.DofNumeral));

        // Deterministic ordering matches RunAsync sort (NFR-5)
        map.Sort(static (a, b) =>
            string.Compare(a.CheckId, b.CheckId, StringComparison.Ordinal));

        return map.AsReadOnly();
    }
}
