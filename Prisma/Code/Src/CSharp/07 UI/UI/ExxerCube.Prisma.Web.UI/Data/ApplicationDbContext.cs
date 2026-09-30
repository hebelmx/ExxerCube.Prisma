using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExxerCube.Prisma.Web.UI.Data;

/// <summary>
/// Application database context for managing user identity and authentication data.
/// </summary>
/// <param name="options">The database context options.</param>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
}
