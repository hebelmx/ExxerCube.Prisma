// ADR-014: IdentityNoOpEmailSender is superseded by PrismaNoOpEmailSender in Infrastructure.Identity.
// This file is retained as a stub so that any Account scaffolding that references
// IdentityNoOpEmailSender by name (e.g. RegisterConfirmation.razor) continues to compile.
// The DI registration in Program.cs now uses PrismaNoOpEmailSender; this class is no longer
// registered. It will be removed in a follow-up cleanup pass.
using ExxerCube.Prisma.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace ExxerCube.Prisma.Web.UI.Components.Account;

// Remove the "else if (EmailSender is IdentityNoOpEmailSender)" block from RegisterConfirmation.razor after updating with a real implementation.
internal sealed class IdentityNoOpEmailSender : IEmailSender<PrismaApplicationUser>
{
    private readonly IEmailSender emailSender = new NoOpEmailSender();

    public Task SendConfirmationLinkAsync(PrismaApplicationUser user, string email, string confirmationLink) =>
        emailSender.SendEmailAsync(email, "Confirm your email", $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

    public Task SendPasswordResetLinkAsync(PrismaApplicationUser user, string email, string resetLink) =>
        emailSender.SendEmailAsync(email, "Reset your password", $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");

    public Task SendPasswordResetCodeAsync(PrismaApplicationUser user, string email, string resetCode) =>
        emailSender.SendEmailAsync(email, "Reset your password", $"Please reset your password using the following code: {resetCode}");
}
