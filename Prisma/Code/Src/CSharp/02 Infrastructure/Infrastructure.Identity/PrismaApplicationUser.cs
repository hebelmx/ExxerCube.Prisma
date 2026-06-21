using Microsoft.AspNetCore.Identity;

namespace ExxerCube.Prisma.Infrastructure.Identity;

/// <summary>
/// Application user entity for ExxerCube.Prisma Identity.
/// Extends <see cref="IdentityUser"/> with an optional display name.
/// </summary>
/// <remarks>
/// Named <c>PrismaApplicationUser</c> (not <c>ApplicationUser</c>) to avoid a naming
/// collision with the legacy <c>ApplicationUser</c> type that resided in Web.UI prior to
/// ADR-014 and may still exist in Account scaffolding during the transition period.
/// </remarks>
public class PrismaApplicationUser : IdentityUser
{
    /// <summary>
    /// Gets or sets the user's display / full name (optional).
    /// Used for audit trail presentation and management UI.
    /// </summary>
    public string? FullName { get; set; }
}
