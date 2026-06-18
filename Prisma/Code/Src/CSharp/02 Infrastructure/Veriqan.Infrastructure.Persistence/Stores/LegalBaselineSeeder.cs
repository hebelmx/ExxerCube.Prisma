using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Stores;

/// <summary>
/// Idempotent seeder that populates <c>veriqan.LegalBaselineTolerances</c> with the
/// legally-mandated tolerance specifications defined by <c>DefaultLegalToleranceProvider</c>.
/// </summary>
/// <remarks>
/// <para>
/// Run at integration-test setup and available for the worker host startup path.
/// The seeder is idempotent — rows already present (matched by <c>CheckId</c>) are skipped.
/// </para>
/// <para>
/// Values MUST match <c>DefaultLegalToleranceProvider</c> exactly so that switching between
/// the in-code and SQL-backed providers produces identical behaviour.
/// </para>
/// </remarks>
public static class LegalBaselineSeeder
{
    /// <summary>
    /// Seeds the legal-baseline tolerance table idempotently. Rows already present are skipped.
    /// </summary>
    /// <param name="context">The Veriqan database context (must already have migrations applied).</param>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    public static async Task SeedAsync(
        VeriqanDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var existing = await context.LegalBaselineTolerances
            .AsNoTracking()
            .Select(r => r.CheckId)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        var toInsert = BuildSeedRecords()
            .Where(r => !existing.Contains(r.CheckId))
            .ToList();

        if (toInsert.Count == 0)
            return;

        await context.LegalBaselineTolerances.AddRangeAsync(toInsert, cancellationToken)
            .ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the canonical set of seed records matching <c>DefaultLegalToleranceProvider</c>.
    /// </summary>
    internal static IEnumerable<LegalBaselineToleranceRecord> BuildSeedRecords()
    {
        // CurrencyMxn: legalDefault=0.50, min=0.00, max=1.00
        // Applies to: CL-10, CL-17, CL-18, CL-19, CL-20, CL-21, CL-22, CL-24, CL-25, CL-44, ITEM-58
        // + Epic 11 recompute rules (§20/§19/§6/§8/§16) — MUST match DefaultLegalToleranceProvider.
        foreach (var id in new[] { "CL-10", "CL-17", "CL-18", "CL-19", "CL-20", "CL-21",
                                    "CL-22", "CL-24", "CL-25", "CL-44", "ITEM-58",
                                    "LAW-§20-WATERFALL", "LAW-§19-INTERES", "LAW-§6-SIMULACION",
                                    "LAW-§8-INDICADORES", "LAW-§16-OTRASLINEAS" })
        {
            yield return new LegalBaselineToleranceRecord
            {
                CheckId = id,
                LegalDefault = 0.50m,
                Min = 0.00m,
                Max = 1.00m,
            };
        }

        // Points: legalDefault=1.00, min=0.00, max=2.00
        yield return new LegalBaselineToleranceRecord
        {
            CheckId = "CL-39",
            LegalDefault = 1.00m,
            Min = 0.00m,
            Max = 2.00m,
        };

        // ExchangeRate: legalDefault=0.10, min=0.05, max=0.20
        yield return new LegalBaselineToleranceRecord
        {
            CheckId = "CL-37",
            LegalDefault = 0.10m,
            Min = 0.05m,
            Max = 0.20m,
        };

        // RewardsPesosMxn: legalDefault=1.00, min=0.00, max=2.00
        yield return new LegalBaselineToleranceRecord
        {
            CheckId = "CL-36",
            LegalDefault = 1.00m,
            Min = 0.00m,
            Max = 2.00m,
        };
    }
}
