using System.Collections.Frozen;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;

namespace ExxerCube.Prisma.Veriqan.Domain.Binding;

/// <summary>
/// Immutable snapshot of which reference-data capabilities are available for a specific
/// verification run.  Built by <c>BundleBinder</c> after a <see cref="VecReferenceBundle"/>
/// has been loaded.
/// </summary>
/// <remarks>
/// Each <see cref="ReferenceCapability"/> maps to one section of the bundle.
/// When a section is absent (null) or empty, the capability is marked
/// <see cref="ReferenceCapabilityStatus.InsufficientData"/>; checks that depend on that
/// capability emit <c>INSUFFICIENT_DATA</c> rather than producing a false verdict.
/// Capabilities whose section <em>is</em> present stay <see cref="ReferenceCapabilityStatus.Available"/>
/// and run normally — a missing TASA section does not block Legends checks, for example.
/// </remarks>
public sealed class ReferenceDataAvailability
{
    private readonly FrozenDictionary<ReferenceCapability, ReferenceCapabilityStatus> _map;

    /// <summary>
    /// Initializes a <see cref="ReferenceDataAvailability"/> from a pre-built capability map.
    /// </summary>
    /// <param name="map">Full capability→status mapping for every known <see cref="ReferenceCapability"/>.</param>
    private ReferenceDataAvailability(
        Dictionary<ReferenceCapability, ReferenceCapabilityStatus> map)
    {
        _map = map.ToFrozenDictionary();
    }

    /// <summary>
    /// Derives availability from a loaded <see cref="VecReferenceBundle"/>.
    /// A section is considered present when it is non-null <em>and</em> contains at least one element.
    /// </summary>
    /// <param name="bundle">The loaded reference bundle.</param>
    /// <returns>A fully-populated <see cref="ReferenceDataAvailability"/>.</returns>
    public static ReferenceDataAvailability FromBundle(VecReferenceBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        static ReferenceCapabilityStatus Status(bool present) =>
            present ? ReferenceCapabilityStatus.Available : ReferenceCapabilityStatus.InsufficientData;

        var map = new Dictionary<ReferenceCapability, ReferenceCapabilityStatus>
        {
            [ReferenceCapability.Rate] =
                Status(bundle.InterestRates is { Count: > 0 }),

            [ReferenceCapability.Tolerances] =
                Status(bundle.ToleranceConfig is not null),

            [ReferenceCapability.PriorStatement] =
                Status(bundle.PriorStatements is { Count: > 0 }),

            [ReferenceCapability.ExpectedTransactions] =
                Status(bundle.ExpectedTransactions is { Count: > 0 }),

            [ReferenceCapability.Promotions] =
                Status(bundle.Promotions is { Count: > 0 }),

            [ReferenceCapability.Legends] =
                Status(bundle.MandatoryLegends is { Count: > 0 }),

            [ReferenceCapability.CatalogImages] =
                Status(bundle.SequentialImages is { Count: > 0 }),
        };

        return new ReferenceDataAvailability(map);
    }

    /// <summary>
    /// Returns the status of the specified <paramref name="capability"/> for this run.
    /// </summary>
    /// <param name="capability">The capability to query.</param>
    /// <returns>
    /// <see cref="ReferenceCapabilityStatus.Available"/> if the backing bundle section was present,
    /// otherwise <see cref="ReferenceCapabilityStatus.InsufficientData"/>.
    /// </returns>
    public ReferenceCapabilityStatus StatusOf(ReferenceCapability capability) =>
        _map.TryGetValue(capability, out var status) ? status : ReferenceCapabilityStatus.InsufficientData;

    /// <summary>
    /// Returns <see langword="true"/> when the specified <paramref name="capability"/> is
    /// <see cref="ReferenceCapabilityStatus.InsufficientData"/> and the check that needs it
    /// must therefore emit <c>INSUFFICIENT_DATA</c> instead of running.
    /// </summary>
    /// <param name="capability">The capability the check depends on.</param>
    public bool IsInsufficientData(ReferenceCapability capability) =>
        StatusOf(capability) == ReferenceCapabilityStatus.InsufficientData;

    /// <summary>
    /// Read-only view of the full capability map, for diagnostics and serialization.
    /// </summary>
    public IReadOnlyDictionary<ReferenceCapability, ReferenceCapabilityStatus> All => _map;
}
