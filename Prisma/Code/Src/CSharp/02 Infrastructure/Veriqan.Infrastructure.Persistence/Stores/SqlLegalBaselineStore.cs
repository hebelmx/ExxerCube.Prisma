using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Tolerances;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Stores;

/// <summary>
/// SQL-backed implementation of <see cref="ILegalBaselineStore"/> that reads legal-baseline
/// tolerance records from the encrypted <c>veriqan.LegalBaselineTolerances</c> table.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read-only design:</b> this store exposes no write methods. The production SQL login
/// should be granted <c>db_datareader</c> only on the <c>LegalBaselineTolerances</c> table.
/// TDE at the database level is an additional OPS provisioning step (not enforced here).
/// </para>
/// <para>
/// Decryption is transparent — the AES value converter on <see cref="VeriqanDbContext"/>
/// decrypts each column as EF Core materialises the entity.
/// </para>
/// </remarks>
internal sealed class SqlLegalBaselineStore : ILegalBaselineStore
{
    private readonly VeriqanDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="SqlLegalBaselineStore"/>.
    /// </summary>
    /// <param name="context">The Veriqan database context.</param>
    public SqlLegalBaselineStore(VeriqanDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, Tolerance>> LoadTolerancesAsync(
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return new Dictionary<string, Tolerance>(StringComparer.OrdinalIgnoreCase);

        var records = await _context.LegalBaselineTolerances
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var dict = new Dictionary<string, Tolerance>(
            records.Count,
            StringComparer.OrdinalIgnoreCase);

        foreach (var r in records)
        {
            dict[r.CheckId] = new Tolerance(r.LegalDefault, r.Min, r.Max);
        }

        return dict;
    }
}
