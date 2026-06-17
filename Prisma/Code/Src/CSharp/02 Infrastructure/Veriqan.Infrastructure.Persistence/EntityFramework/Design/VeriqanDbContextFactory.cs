using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework.Design;

/// <summary>
/// Design-time factory for <see cref="VeriqanDbContext"/> used exclusively by EF Core tooling
/// (<c>dotnet ef migrations add</c>, <c>dotnet ef database update</c>, etc.).
/// This factory is NEVER invoked at runtime — DI uses <see cref="DependencyInjection.VeriqanPersistenceExtensions.AddVeriqanPersistence"/>.
/// </summary>
public sealed class VeriqanDbContextFactory : IDesignTimeDbContextFactory<VeriqanDbContext>
{
    /// <summary>
    /// Creates a <see cref="VeriqanDbContext"/> configured for EF Core design-time operations.
    /// </summary>
    /// <param name="args">Command-line arguments passed by EF tooling (not used).</param>
    /// <returns>A fully configured <see cref="VeriqanDbContext"/> instance.</returns>
    public VeriqanDbContext CreateDbContext(string[] args)
    {
        // Design-time default connection string — mirrors the PrismaDbContextFactory convention.
        // Override by passing a connection string as args[0] or by setting the environment variable
        // VERIQAN_CONNECTION_STRING before running dotnet ef commands.
        var connectionString = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? args[0]
            : Environment.GetEnvironmentVariable("VERIQAN_CONNECTION_STRING")
              ?? "Server=DESKTOP-FB2ES22\\SQL2022;Database=Prisma;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        var optionsBuilder = new DbContextOptionsBuilder<VeriqanDbContext>();
        optionsBuilder.UseSqlServer(
            connectionString,
            b => b.MigrationsHistoryTable("__EFMigrationsHistory", "veriqan"));

        return new VeriqanDbContext(optionsBuilder.Options);
    }
}
