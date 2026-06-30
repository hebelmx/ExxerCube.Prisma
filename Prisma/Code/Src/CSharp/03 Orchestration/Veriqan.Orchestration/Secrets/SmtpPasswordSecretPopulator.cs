using System;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Infrastructure.Reporting;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Secrets;

/// <summary>
/// <see cref="IPostConfigureOptions{TOptions}"/> that populates <see cref="SmtpOptions.Password"/>
/// from <see cref="ISecretProvider"/> when the password was not already set by configuration binding.
/// </summary>
/// <remarks>
/// <para>
/// This populator runs after all <c>Configure&lt;SmtpOptions&gt;</c> calls (which bind from
/// <c>appsettings.json</c> and environment variables). If the password was already supplied via
/// configuration or a prior post-configure step, it is left unchanged — the secret provider is
/// only consulted when the property is null or whitespace.
/// </para>
/// <para>
/// The logical name used to resolve the SMTP password is
/// <c>"Veriqan:Smtp:Password"</c> — the same path that configuration binding would use
/// (i.e. environment variable <c>Veriqan__Smtp__Password</c>). This means both paths (direct
/// config and secret-provider-routed) ultimately read the same underlying value in the default
/// <c>ConfigurationSecretProvider</c>, making the seam transparent.
/// </para>
/// <para>
/// Registered unconditionally from <c>VeriqanOrchestrationExtensions.AddVeriqan</c> so that
/// the <see cref="ISecretProvider"/> singleton is always resolved from DI (not from
/// <c>AddVeriqanReporting</c>, which is called without provider context). This keeps
/// <c>AddVeriqanReporting</c> tests unaffected.
/// </para>
/// </remarks>
internal sealed class SmtpPasswordSecretPopulator : IPostConfigureOptions<SmtpOptions>
{
    /// <summary>The logical name used to look up the SMTP password in the secret provider.</summary>
    public const string SecretLogicalName = "Veriqan:Smtp:Password";

    private readonly ISecretProvider _secretProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="SmtpPasswordSecretPopulator"/>.
    /// </summary>
    /// <param name="secretProvider">Secret provider to consult for the SMTP password.</param>
    public SmtpPasswordSecretPopulator(ISecretProvider secretProvider)
    {
        _secretProvider = secretProvider ?? throw new ArgumentNullException(nameof(secretProvider));
    }

    /// <inheritdoc />
    public void PostConfigure(string? name, SmtpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Only override when the password was not already set by configuration binding
        // (direct env var / appsettings.json). This ensures config always wins if supplied.
        if (!string.IsNullOrWhiteSpace(options.Password))
            return;

        // Call the async method synchronously — this runs exactly once at first options
        // resolution (lazy singleton). Blocking is acceptable in this startup path.
        var result = _secretProvider.GetSecretAsync(SecretLogicalName)
            .GetAwaiter().GetResult();

        if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value?.Value))
            options.Password = result.Value.Value;

        // If the secret is absent/empty we leave the password null — the SmtpEmailSender
        // already handles unauthenticated relay gracefully (credentials are only set when
        // UserName is non-empty; Password being null is the standard relay scenario).
    }
}
