using ExxerCube.Prisma.Infrastructure.Identity;

namespace ExxerCube.Prisma.Web.UI.Components.Account;

internal sealed class IdentityUserAccessor(UserManager<PrismaApplicationUser> userManager, IdentityRedirectManager redirectManager)
{
    public async Task<PrismaApplicationUser> GetRequiredUserAsync(HttpContext context)
    {
        var user = await userManager.GetUserAsync(context.User);

        if (user is null)
        {
            redirectManager.RedirectToWithStatus("Account/InvalidUser", $"Error: Unable to load user with ID '{userManager.GetUserId(context.User)}'.", context);
        }

        return user;
    }
}
