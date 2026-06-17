namespace ExxerCube.Prisma.Veriqan.Application.Ports;

/// <summary>
/// Immutable key that identifies which reference-data bundle to load for a statement verification run.
/// All fields except <see cref="Institution"/> are optional; adapters use the most specific match available.
/// </summary>
/// <param name="Institution">Bank / issuer name exactly as it appears in the bundle metadata (e.g. <c>"Demo Bank (Iqubica)"</c>).</param>
/// <param name="PeriodLabel">Human-readable period label, e.g. <c>"Sep-Oct 2025"</c>. Used to select time-bounded data (TASA, promotions).</param>
/// <param name="AccountRef">Optional account reference; narrows selection when multiple accounts exist in the source.</param>
/// <param name="ProductId">Optional canonical product id; narrows selection when loading product-specific data.</param>
public sealed record StatementContextKey(
    string Institution,
    string? PeriodLabel = null,
    string? AccountRef = null,
    string? ProductId = null
);
