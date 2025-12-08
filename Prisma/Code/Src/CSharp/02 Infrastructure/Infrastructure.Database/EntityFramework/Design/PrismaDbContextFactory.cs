// <copyright file="PrismaDbContextFactory.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExxerCube.Prisma.Infrastructure.Database.EntityFramework.Design;

/// <summary>
/// Design-time factory for PrismaDbContext to support EF Core migrations and tooling.
/// This is ONLY used at design time (dotnet ef commands), not at runtime.
/// </summary>
public class PrismaDbContextFactory : IDesignTimeDbContextFactory<PrismaDbContext>
{
    /// <summary>
    /// Creates a new instance of PrismaDbContext for design-time operations.
    /// Uses the ApplicationConnection from appsettings.json.
    /// </summary>
    public PrismaDbContext CreateDbContext(string[] args)
    {
        // Default connection string for design-time operations
        // This matches the ApplicationConnection in appsettings.json
        var connectionString = "Server=DESKTOP-FB2ES22\\SQL2022;Database=Prisma;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        var optionsBuilder = new DbContextOptionsBuilder<PrismaDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new PrismaDbContext(optionsBuilder.Options);
    }
}
