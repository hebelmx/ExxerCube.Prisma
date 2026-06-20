// <copyright file="QaHarnessServiceCollectionExtensions.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.DependencyInjection;

/// <summary>
/// Extension methods for registering QA Harness services with an <see cref="IServiceCollection"/>.
/// </summary>
public static class QaHarnessServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core QA Harness services into the dependency injection container.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance so calls can be chained.</returns>
    public static IServiceCollection AddQaHarness(this IServiceCollection services)
    {
        // Stub — implementations registered here in subsequent chunks.
        return services;
    }
}
