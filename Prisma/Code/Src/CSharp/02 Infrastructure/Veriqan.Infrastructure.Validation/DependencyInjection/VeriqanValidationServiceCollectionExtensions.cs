using System;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;

/// <summary>
/// Registers the VEC validation engine and all <see cref="IVecValidationRule"/>
/// implementations discovered in the <c>Veriqan.Infrastructure.Validation</c> assembly.
/// </summary>
public static class VeriqanValidationServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="IVecValidationEngine"/> (as <see cref="VecValidationEngine"/>) and
    /// all <see cref="IVecValidationRule"/> implementations in this assembly to the DI container.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rule discovery uses <b>Scrutor 7.0.0</b> assembly scanning. Any class in this
    /// assembly that implements <see cref="IVecValidationRule"/> (including <c>internal</c>
    /// classes) is registered as <c>IVecValidationRule</c> with a <c>Transient</c> lifetime.
    /// </para>
    /// <para>
    /// Adding a new rule for Story 4.2+ requires only:
    /// <list type="number">
    ///   <item>Create a class implementing <see cref="IVecValidationRule"/> in this assembly.</item>
    ///   <item>No further registration is needed; Scrutor will find it automatically.</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddVeriqanValidation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register the engine
        services.AddTransient<IVecValidationEngine, VecValidationEngine>();

        // Scrutor assembly scan: find all IVecValidationRule implementations in this assembly.
        // VecValidationEngine anchors the scan to this assembly (AssemblyMarker is static and
        // cannot be used as a type argument).
        services.Scan(scan => scan
            .FromAssemblyOf<VecValidationEngine>()
            .AddClasses(classes => classes.AssignableTo<IVecValidationRule>(), publicOnly: false)
            .AsImplementedInterfaces()
            .WithTransientLifetime());

        return services;
    }
}
