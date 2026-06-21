using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExxerCube.Prisma.Infrastructure.Identity;

/// <summary>
/// EF Core database context for ASP.NET Core Identity tables.
/// Hosts user, role, claim, and token tables for the Prisma application.
/// </summary>
/// <remarks>
/// Intentionally separate from <c>PrismaDbContext</c> (application tables).
/// The identity schema targets the <c>DefaultConnection</c> SQL Server database
/// (PrismaID), while application data lives in <c>ApplicationConnection</c> (Prisma).
/// See ADR-014 §4 for the connection string strategy.
/// </remarks>
/// <param name="options">EF Core context options.</param>
public class PrismaIdentityDbContext(DbContextOptions<PrismaIdentityDbContext> options)
    : IdentityDbContext<PrismaApplicationUser>(options)
{
}
