using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace ExxerCube.Prisma.Infrastructure.Identity;

/// <summary>
/// No-operation <see cref="IEmailSender{TUser}"/> for <see cref="PrismaApplicationUser"/>.
/// Logs the action instead of sending a real email.
/// </summary>
/// <remarks>
/// Replace with a real SMTP / SendGrid implementation before enabling e-mail confirmation
/// workflows. The ADR-014 MVP scope does not require e-mail confirmation
/// (<c>RequireConfirmedAccount = false</c>).
/// </remarks>
public sealed class PrismaNoOpEmailSender : IEmailSender<PrismaApplicationUser>
{
    private readonly IEmailSender _inner = new NoOpEmailSender();

    /// <inheritdoc />
    public Task SendConfirmationLinkAsync(PrismaApplicationUser user, string email, string confirmationLink) =>
        _inner.SendEmailAsync(
            email,
            "Confirm your email",
            $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

    /// <inheritdoc />
    public Task SendPasswordResetLinkAsync(PrismaApplicationUser user, string email, string resetLink) =>
        _inner.SendEmailAsync(
            email,
            "Reset your password",
            $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");

    /// <inheritdoc />
    public Task SendPasswordResetCodeAsync(PrismaApplicationUser user, string email, string resetCode) =>
        _inner.SendEmailAsync(
            email,
            "Reset your password",
            $"Please reset your password using the following code: {resetCode}");
}
