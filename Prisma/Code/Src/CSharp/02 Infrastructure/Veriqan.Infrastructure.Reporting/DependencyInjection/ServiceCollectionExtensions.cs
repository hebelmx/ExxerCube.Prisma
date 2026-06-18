using System;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Reporting.DependencyInjection;

/// <summary>
/// Registers Veriqan reporting services (marked-PDF generator, SMTP email sender, and
/// RED-verdict alert service).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Veriqan reporting services to the DI container:
    /// <list type="bullet">
    ///   <item><description><see cref="IMarkedPdfGenerator"/> → <see cref="MarkedPdfGenerator"/></description></item>
    ///   <item><description><see cref="IEmailSender"/> → <see cref="SmtpEmailSender"/> (Story 7.3, FR-17)</description></item>
    ///   <item><description><see cref="IVecAlertService"/> → <see cref="VecAlertService"/> (Story 7.3, CL-54)</description></item>
    /// </list>
    /// Options are bound from <paramref name="configuration"/>:
    /// <list type="bullet">
    ///   <item><description><c>"Veriqan:Smtp"</c> → <see cref="SmtpOptions"/></description></item>
    ///   <item><description><c>"Veriqan:Alerts"</c> → <see cref="AlertOptions"/></description></item>
    /// </list>
    /// </summary>
    /// <param name="services">The service collection to extend.</param>
    /// <param name="configuration">
    /// Application configuration used to bind <see cref="SmtpOptions"/> and
    /// <see cref="AlertOptions"/>.  When <see langword="null"/> default option values are used.
    /// </param>
    /// <returns>The same <paramref name="services"/> for method chaining.</returns>
    public static IServiceCollection AddVeriqanReporting(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Marked-PDF generator (Story 7.2)
        services.AddSingleton<IMarkedPdfGenerator, MarkedPdfGenerator>();

        // SMTP options — bind from config if available, otherwise use defaults.
        if (configuration is not null)
        {
            services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionKey));
            services.Configure<AlertOptions>(configuration.GetSection(AlertOptions.SectionKey));
        }
        else
        {
            services.Configure<SmtpOptions>(_ => { });
            services.Configure<AlertOptions>(_ => { });
        }

        // Email transport (Story 7.3)
        services.AddTransient<IEmailSender, SmtpEmailSender>();

        // RED-verdict alert service (Story 7.3)
        services.AddTransient<IVecAlertService, VecAlertService>();

        return services;
    }
}
