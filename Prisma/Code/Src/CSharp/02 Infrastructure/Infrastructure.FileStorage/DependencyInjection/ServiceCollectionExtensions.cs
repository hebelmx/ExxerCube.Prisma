using System;
using Microsoft.Extensions.DependencyInjection;
using ExxerCube.Prisma.Domain.Interfaces;

namespace ExxerCube.Prisma.Infrastructure.FileStorage.DependencyInjection;

/// <summary>
/// Extension methods for configuring file storage dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds file storage services to the service collection, including AES-256-GCM
    /// encryption at rest via <see cref="AesGcmStorageEncryptor"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure file storage options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Required configuration:</strong> Set <c>Storage:EncryptionKey</c> (or
    /// environment variable <c>PRISMA_STORAGE_KEY</c>) to a Base64-encoded 32-byte AES-256
    /// master key. Never commit the key to source control.
    /// Generate with: <c>openssl rand -base64 32</c>
    /// </para>
    /// <para>
    /// The <see cref="IStorageEncryptor"/> seam is registered as Singleton — the master key
    /// is read once at startup and held in memory. Cloud adapters can replace
    /// <see cref="AesGcmStorageEncryptor"/> with a KMS-backed implementation by swapping
    /// the registration here.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddFileStorageServices(
        this IServiceCollection services,
        Action<FileStorageOptions> configureOptions)
    {
        services.Configure(configureOptions);

        // Register the encryption seam. Cloud adapters swap this for a KMS-backed impl.
        services.AddSingleton<IStorageEncryptor, AesGcmStorageEncryptor>();

        services.AddScoped<IDownloadStorage, FileSystemDownloadStorageAdapter>();
        services.AddScoped<ISafeFileNamer, SafeFileNamerService>();
        services.AddScoped<IFileMover, FileMoverService>();

        return services;
    }
}
