using ExxerCube.Prisma.Veriqan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;

/// <summary>
/// EF Core <see cref="SaveChangesInterceptor"/> that prevents application-layer mutations
/// (UPDATE or DELETE) on the two append-only audit entities:
/// <list type="bullet">
///   <item><see cref="Disposition"/> — human-reviewer audit trail (AR-9, FR-18).</item>
///   <item><see cref="ReprocessAuditLogEntity"/> — reprocess event log (Story 6.1).</item>
/// </list>
/// <para>
/// This interceptor provides a fast, descriptive rejection <b>before</b> any SQL is emitted.
/// The database-level triggers (<c>trg_Dispositions_PreventMutation</c> and
/// <c>trg_ReprocessAuditLog_PreventMutation</c>, added in migration
/// <c>AddImmutabilityTriggers</c>) serve as the enforcement guarantee at the engine level
/// (defense-in-depth, Story 6.2).
/// </para>
/// <para>
/// <see cref="VerificationOutcomeSnapshotEntity"/> is intentionally NOT in the protected set —
/// <c>ReplaceOutcomeAsync</c> legitimately UPDATEs that table on every reprocess.
/// </para>
/// </summary>
/// <remarks>
/// Register as a singleton via
/// <c>services.AddSingleton&lt;ImmutableEntityInterceptor&gt;()</c>
/// and add to the DbContext options with <c>.AddInterceptors(...)</c>.
/// </remarks>
public sealed class ImmutableEntityInterceptor : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ThrowIfImmutableMutationDetected(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ThrowIfImmutableMutationDetected(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ThrowIfImmutableMutationDetected(DbContext? context)
    {
        if (context is null)
            return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted))
                continue;

            if (entry.Entity is Disposition)
            {
                throw new InvalidOperationException(
                    $"{nameof(Disposition)} is an immutable audit record; UPDATE/DELETE is prohibited. " +
                    "Append a new row instead of modifying an existing one.");
            }

            if (entry.Entity is ReprocessAuditLogEntity)
            {
                throw new InvalidOperationException(
                    $"{nameof(ReprocessAuditLogEntity)} is an immutable audit record; UPDATE/DELETE is prohibited. " +
                    "Append a new row instead of modifying an existing one.");
            }
        }
    }
}
