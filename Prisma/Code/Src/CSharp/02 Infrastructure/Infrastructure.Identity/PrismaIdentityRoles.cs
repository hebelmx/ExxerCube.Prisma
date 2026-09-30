namespace ExxerCube.Prisma.Infrastructure.Identity;

/// <summary>
/// Canonical role name constants for ExxerCube.Prisma.
/// All role strings used in <c>[Authorize(Roles = "...")]</c> attributes and the seeder
/// must reference these constants to avoid typo drift.
/// </summary>
/// <remarks>
/// Three roles are seeded per ADR-014 §5:
/// <list type="bullet">
///   <item><c>Reviewer</c> — content reviewers; access to <c>/manual-review</c>.</item>
///   <item><c>Admin</c>    — content administrators; access to <c>/manual-review</c>.</item>
///   <item><c>Administrator</c> — system administrators; access to <c>/admin/*</c> pages.</item>
/// </list>
/// The seed admin user holds both <c>Admin</c> and <c>Administrator</c> so no admin
/// page locks out the seed account.
/// </remarks>
public static class PrismaIdentityRoles
{
    /// <summary>Content reviewer — can access <c>/manual-review</c>.</summary>
    public const string Reviewer = "Reviewer";

    /// <summary>Content administrator — can access <c>/manual-review</c>.</summary>
    public const string Admin = "Admin";

    /// <summary>
    /// System administrator — can access <c>/admin/database-migration</c> and
    /// <c>/admin/connection-string</c>. Assigned together with <see cref="Admin"/>
    /// to the seed admin user.
    /// </summary>
    public const string Administrator = "Administrator";
}
