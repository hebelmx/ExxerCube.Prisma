using System;
using System.Threading;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Document-scoped cost guard for the progressive fallback-extraction chain (see
/// <c>docs/planning-artifacts/veriqan-fallback-chain-design-2026-07-05.md</c>, "Cost/latency
/// discipline"). One instance is created per document extraction call and threaded through every
/// field's <see cref="FieldResolutionContext{TValue}"/> so that the total number of LLM calls for
/// that document — however many fields escalate — never exceeds the configured ceiling.
/// </summary>
/// <remarks>
/// As of E1 no stage consumes the LLM budget (there is no LLM stage yet); the type exists so the
/// orchestrator and later stages (E5) have a ready-made, already-wired guard instead of adding one
/// under time pressure once the LLM stage lands. Thread-safe: <see cref="TryConsumeLlmCall"/> uses
/// an atomic decrement so concurrent field resolutions cannot overspend the budget.
/// </remarks>
public sealed class StageBudget
{
    /// <summary>
    /// Default document-level ceiling on LLM calls when no explicit budget is configured.
    /// Deliberately small — the LLM stage is a last resort for a small, heavily-validated tail
    /// of fields (design doc, "Cost/latency discipline" §2).
    /// </summary>
    public const int DefaultMaxLlmCallsPerDocument = 5;

    private int _remainingLlmCalls;

    /// <summary>
    /// Initializes a <see cref="StageBudget"/> with the given LLM-call ceiling for one document.
    /// </summary>
    /// <param name="maxLlmCalls">
    /// Maximum number of LLM calls allowed for the document being processed. Defaults to
    /// <see cref="DefaultMaxLlmCallsPerDocument"/>.
    /// </param>
    public StageBudget(int maxLlmCalls = DefaultMaxLlmCallsPerDocument)
    {
        if (maxLlmCalls < 0)
            throw new ArgumentOutOfRangeException(nameof(maxLlmCalls), maxLlmCalls, "maxLlmCalls cannot be negative.");

        _remainingLlmCalls = maxLlmCalls;
    }

    /// <summary>
    /// Number of LLM calls still available for the current document. Never negative.
    /// </summary>
    public int RemainingLlmCalls => Volatile.Read(ref _remainingLlmCalls);

    /// <summary>
    /// Atomically consumes one LLM call from the remaining budget.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> and decrements the remaining count when budget was available;
    /// <see langword="false"/> (budget unchanged) when the document-level ceiling has been reached —
    /// callers must treat this as a distinguishable abstain, never a silent degradation
    /// (design doc, "Cost/latency discipline" §2).
    /// </returns>
    public bool TryConsumeLlmCall()
    {
        while (true)
        {
            var current = Volatile.Read(ref _remainingLlmCalls);
            if (current <= 0)
                return false;

            if (Interlocked.CompareExchange(ref _remainingLlmCalls, current - 1, current) == current)
                return true;
        }
    }
}
