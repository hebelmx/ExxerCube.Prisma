using System;
using ExxerCube.Prisma.Veriqan.Domain.Enums;

namespace ExxerCube.Prisma.Veriqan.Application.Binding;

/// <summary>
/// Represents a BLOCKED outcome from <see cref="Ports.IBundleBinder"/>: the binding
/// could not complete because a pre-check condition was not met.
/// </summary>
/// <remarks>
/// <para>
/// When binding is blocked the binder returns a <c>Result</c> failure whose
/// <see cref="IndQuestResults.Result{T}.Error"/> message is derived from
/// <see cref="Reason"/> (machine-readable) and <see cref="Detail"/> (human-readable).
/// Callers that need to branch on the reason should use <see cref="TryParse"/>
/// to recover the typed <see cref="BlockReason"/> from the error string rather than
/// comparing free text.
/// </para>
/// <para>
/// Formatting contract: <c>"BLOCKED:{Reason}:{Detail}"</c>
/// e.g. <c>"BLOCKED:UnknownProduct:No product matched token 'NL'"</c>.
/// This allows both human-readable surfacing and machine-readable routing.
/// </para>
/// </remarks>
/// <param name="Reason">Machine-readable block reason.</param>
/// <param name="Detail">Human-readable explanation for logs and UI.</param>
public sealed record BlockedOutcome(BlockReason Reason, string Detail)
{
    // -----------------------------------------------------------------------
    // Serialisation helpers
    // -----------------------------------------------------------------------

    private const string Prefix = "BLOCKED:";

    /// <summary>
    /// Formats this outcome into the canonical error string embedded in a
    /// <c>Result</c> failure so that <see cref="TryParse"/> can recover it.
    /// </summary>
    public string ToErrorString() => $"{Prefix}{Reason}:{Detail}";

    /// <summary>
    /// Attempts to parse a <see cref="BlockedOutcome"/> from a <c>Result</c>
    /// error string produced by <see cref="ToErrorString"/>.
    /// </summary>
    /// <param name="error">The <c>Result.Error</c> string to parse.</param>
    /// <param name="outcome">The parsed outcome, if successful.</param>
    /// <returns><see langword="true"/> when the string matches the BLOCKED prefix and
    /// the reason can be parsed as a <see cref="BlockReason"/> value.</returns>
    public static bool TryParse(string? error, out BlockedOutcome? outcome)
    {
        outcome = null;

        if (string.IsNullOrEmpty(error) || !error.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        // Format: BLOCKED:{Reason}:{Detail}
        var remainder = error[Prefix.Length..];
        var colonIndex = remainder.IndexOf(':', StringComparison.Ordinal);
        if (colonIndex < 0)
            return false;

        var reasonPart = remainder[..colonIndex];
        var detailPart = remainder[(colonIndex + 1)..];

        if (!Enum.TryParse<BlockReason>(reasonPart, ignoreCase: false, out var reason))
            return false;

        outcome = new BlockedOutcome(reason, detailPart);
        return true;
    }
}
