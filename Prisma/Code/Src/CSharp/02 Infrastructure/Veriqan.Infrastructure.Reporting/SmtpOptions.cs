namespace ExxerCube.Prisma.Veriqan.Infrastructure.Reporting;

/// <summary>
/// SMTP transport configuration for <see cref="SmtpEmailSender"/>.
/// Bind from the <c>"Veriqan:Smtp"</c> configuration section.
/// </summary>
public sealed class SmtpOptions
{
    /// <summary>The configuration section key used when binding from <c>appsettings.json</c>.</summary>
    public const string SectionKey = "Veriqan:Smtp";

    /// <summary>Gets or sets the SMTP server host name or IP address.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Gets or sets the SMTP server port.  Defaults to <c>25</c>.</summary>
    public int Port { get; set; } = 25;

    /// <summary>Gets or sets the "From" address used for outbound messages.</summary>
    public string From { get; set; } = "noreply@veriqan.local";

    /// <summary>
    /// Gets or sets the SMTP user name.
    /// Leave <see langword="null"/> or empty for unauthenticated relay.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Gets or sets the SMTP password.
    /// Leave <see langword="null"/> or empty for unauthenticated relay.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether SSL/TLS should be enabled.
    /// Defaults to <see langword="false"/> (suitable for local/relay servers).
    /// </summary>
    public bool EnableSsl { get; set; }
}
