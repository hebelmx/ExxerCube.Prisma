using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ExxerCube.Prisma.Domain.Interfaces;

namespace ExxerCube.Prisma.Infrastructure.FileStorage.DependencyInjection;

/// <summary>
/// Extension methods for configuring file storage dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds file storage services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure file storage options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFileStorageServices(
        this IServiceCollection services,
        Action<FileStorageOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddScoped<IDownloadStorage, FileSystemDownloadStorageAdapter>();
        services.AddScoped<ISafeFileNamer, SafeFileNamerService>();
        services.AddScoped<IFileMover, FileMoverService>();

        return services;
    }
}

