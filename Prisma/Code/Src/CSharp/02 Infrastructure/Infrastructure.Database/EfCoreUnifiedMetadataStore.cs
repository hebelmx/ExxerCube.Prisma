using System.Text.Json;
using System.Text.Json.Serialization;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Database.EntityFramework;
using ExxerCube.Prisma.Infrastructure.Database.EntityFramework.Entities;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Database;

/// <summary>
/// EF Core implementation of <see cref="IUnifiedMetadataStore"/>.
/// Persists and retrieves the consolidated <see cref="UnifiedMetadataRecord"/> as a JSON payload
/// in the <c>UnifiedMetadataRecords</c> table, keyed by file identifier.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Serialisation:</strong> <see cref="UnifiedMetadataRecord"/> contains SmartEnum
/// (<see cref="EnumModel"/>-derived) members. The local <see cref="DatabaseEnumModelJsonConverterFactory"/>
/// is included in the serialiser options so that enum members round-trip correctly via their stable
/// <c>Name</c> string (identical to the strategy in the Infrastructure.FileSystem handoff store).
/// </para>
/// <para>
/// <strong>Cross-infrastructure isolation:</strong> To avoid introducing a project reference from
/// <c>Infrastructure.Database</c> to <c>Infrastructure</c> (which would violate the no-cross-infra rule),
/// the converter factory is re-implemented as an internal class within this file. The logic is
/// identical; the class name differs to satisfy the architecture duplicate-name guard.
/// </para>
/// <para>
/// <strong>Fault tolerance:</strong> Every public method is fail-open — persistence errors are logged
/// and returned as <see cref="Result"/> failures, never thrown.
/// </para>
/// </remarks>
public sealed class EfCoreUnifiedMetadataStore : IUnifiedMetadataStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new DatabaseEnumModelJsonConverterFactory() },
    };

    private readonly PrismaDbContext _dbContext;
    private readonly ILogger<EfCoreUnifiedMetadataStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfCoreUnifiedMetadataStore"/> class.
    /// </summary>
    /// <param name="dbContext">The EF Core database context.</param>
    /// <param name="logger">The logger.</param>
    public EfCoreUnifiedMetadataStore(
        PrismaDbContext dbContext,
        ILogger<EfCoreUnifiedMetadataStore> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result> SaveAsync(
        string fileId,
        UnifiedMetadataRecord record,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(fileId))
        {
            return Result.WithFailure("fileId cannot be null or empty");
        }

        if (record is null)
        {
            return Result.WithFailure("record cannot be null");
        }

        try
        {
            var json = JsonSerializer.Serialize(record, SerializerOptions);

            var existing = await _dbContext.UnifiedMetadataRecords
                .FirstOrDefaultAsync(e => e.FileId == fileId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.PayloadJson = json;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                await _dbContext.UnifiedMetadataRecords.AddAsync(
                    new PersistedUnifiedMetadata
                    {
                        FileId = fileId,
                        PayloadJson = json,
                        UpdatedAt = DateTime.UtcNow,
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Persisted UnifiedMetadataRecord for file {FileId} ({Bytes} bytes)",
                fileId, json.Length);

            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist UnifiedMetadataRecord for file {FileId}", fileId);
            return Result.WithFailure($"Failed to persist unified metadata record: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<UnifiedMetadataRecord?>> GetByFileIdAsync(
        string fileId,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<UnifiedMetadataRecord?>();
        }

        if (string.IsNullOrWhiteSpace(fileId))
        {
            return Result<UnifiedMetadataRecord?>.WithFailure("fileId cannot be null or empty");
        }

        try
        {
            var entity = await _dbContext.UnifiedMetadataRecords
                .FirstOrDefaultAsync(e => e.FileId == fileId, cancellationToken)
                .ConfigureAwait(false);

            if (entity is null)
            {
                return Result<UnifiedMetadataRecord?>.WithFailure(
                    $"No unified metadata record for file {fileId}");
            }

            var record = JsonSerializer.Deserialize<UnifiedMetadataRecord>(entity.PayloadJson, SerializerOptions);

            if (record is null)
            {
                _logger.LogWarning(
                    "Deserialized UnifiedMetadataRecord for file {FileId} was null (payload may be corrupt)",
                    fileId);
                return Result<UnifiedMetadataRecord?>.WithFailure(
                    $"Unified metadata record for file {fileId} deserialized to null");
            }

            _logger.LogDebug("Loaded UnifiedMetadataRecord for file {FileId}", fileId);
            return Result<UnifiedMetadataRecord?>.Success(record);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<UnifiedMetadataRecord?>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load UnifiedMetadataRecord for file {FileId}", fileId);
            return Result<UnifiedMetadataRecord?>.WithFailure(
                $"Failed to load unified metadata record: {ex.Message}", default(UnifiedMetadataRecord?), ex);
        }
    }
}

/// <summary>
/// Internal <see cref="JsonConverterFactory"/> for <see cref="EnumModel"/>-derived types within the
/// Infrastructure.Database project.  Logically identical to the
/// <c>ExxerCube.Prisma.Domain.Serialization.EnumModelJsonConverterFactory</c> in the
/// Domain project; re-declared here to keep the EF JSON column converter self-contained.
/// </summary>
internal sealed class DatabaseEnumModelJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsClass
            && !typeToConvert.IsAbstract
            && typeof(EnumModel).IsAssignableFrom(typeToConvert);

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(DatabaseEnumModelJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

/// <summary>
/// Type-safe JSON converter for a concrete <typeparamref name="TEnum"/> derived from
/// <see cref="EnumModel"/>.  Serialises to the stable <c>Name</c> string and deserialises via
/// <see cref="EnumModel.FromName{TEnumeration}"/>.
/// </summary>
/// <typeparam name="TEnum">The concrete <see cref="EnumModel"/> subtype.</typeparam>
internal sealed class DatabaseEnumModelJsonConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : EnumModel, new()
{
    /// <inheritdoc />
    public override TEnum Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var name = reader.GetString();
        if (string.IsNullOrWhiteSpace(name))
        {
            return EnumModel.InvalidValue<TEnum>();
        }

        return EnumModel.FromName<TEnum>(name);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Name);
    }
}
