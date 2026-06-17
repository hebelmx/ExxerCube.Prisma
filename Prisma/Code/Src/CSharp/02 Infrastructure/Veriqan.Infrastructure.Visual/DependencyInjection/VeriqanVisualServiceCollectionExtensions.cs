using System;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Infrastructure.Visual.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Visual.DependencyInjection;

/// <summary>
/// Registers all visual <see cref="IVecValidationRule"/> implementations discovered in
/// the <c>Veriqan.Infrastructure.Visual</c> assembly.
/// </summary>
/// <remarks>
/// <para>
/// Rule discovery uses <b>Scrutor</b> assembly scanning.  Any class in this assembly that
/// implements <see cref="IVecValidationRule"/> (including <c>internal</c> classes) is
/// registered as <c>IVecValidationRule</c> with a <c>Transient</c> lifetime.
/// </para>
/// <para>
/// <b>Cross-assembly discovery (ADR-V7):</b> calling both
/// <c>AddVeriqanValidation()</c> (from <c>Veriqan.Infrastructure.Validation</c>) and
/// <c>AddVeriqanVisual()</c> populates <c>IEnumerable&lt;IVecValidationRule&gt;</c> with
/// rules from both assemblies.  <c>VecValidationEngine</c> resolves the combined set at
/// construction time via DI.
/// </para>
/// <para>
/// Adding a new visual rule requires only:
/// <list type="number">
///   <item>Create a class implementing <see cref="IVecValidationRule"/> in this assembly.</item>
///   <item>No further registration is needed; Scrutor will find it automatically.</item>
/// </list>
/// </para>
/// </remarks>
public static class VeriqanVisualServiceCollectionExtensions
{
    /// <summary>
    /// Adds all <see cref="IVecValidationRule"/> implementations in the
    /// <c>Veriqan.Infrastructure.Visual</c> assembly to the DI container (Transient lifetime).
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddVeriqanVisual(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Scrutor assembly scan: find all IVecValidationRule implementations in this assembly.
        // Cl35FontComplianceRule is a concrete (non-static) type in this assembly that anchors
        // the scan; AssemblyMarker is static and cannot be used as a type argument.
        services.Scan(scan => scan
            .FromAssemblyOf<Cl35FontComplianceRule>()
            .AddClasses(classes => classes.AssignableTo<IVecValidationRule>(), publicOnly: false)
            .AsImplementedInterfaces()
            .WithTransientLifetime());

        return services;
    }
}
