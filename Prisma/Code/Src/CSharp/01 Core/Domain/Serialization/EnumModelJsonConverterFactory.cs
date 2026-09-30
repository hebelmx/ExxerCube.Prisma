using System.Text.Json;
using System.Text.Json.Serialization;
using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Domain.Serialization;

/// <summary>
/// <see cref="System.Text.Json.Serialization.JsonConverterFactory"/> that handles any
/// <see cref="EnumModel"/>-derived type in any System.Text.Json round-trip — the handoff JSON
/// (ADR-011, MVP-PATH 1.4) and the SignalR cross-process wire (MVP-PATH 1.3).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why needed:</strong> <see cref="System.Text.Json.JsonSerializer"/> (default options)
/// serializes an <see cref="EnumModel"/> by its public properties (Value, Name, DisplayName), which
/// produces a JSON object. On deserialization it tries to construct the type via a parameterless
/// constructor and set properties — but <c>Value</c> has no setter (it is <c>init</c>-only via the
/// protected constructor), so the round-trip loses the integer value and hands back a zero-value
/// default instance: the wrong singleton. This converter serializes as a stable <c>Name</c> string
/// and deserializes back to the correct singleton via <see cref="EnumModel.FromName{TEnumeration}"/>.
/// </para>
/// <para>
/// <strong>Why it lives in Domain:</strong> it depends only on <see cref="EnumModel"/> (Domain) and the
/// BCL, and every cross-process worker (Orion Downloader, Athena Extractor, Reconciliator) plus the
/// Infrastructure handoff store needs it. Domain is the only project common to them all, and the lean
/// Downloader process must not take a dependency on the heavy Infrastructure assembly (CSnakes/Python,
/// imaging) just for a serializer — that would undermine the security-mandated process split.
/// </para>
/// <para>
/// <strong>Key choice — Name over Value:</strong> Name is a stable human-readable string identifier
/// (e.g., <c>"A_AS"</c>, <c>"CNBV"</c>, <c>"Aseguramiento"</c>) that is also unique within each
/// derived type. Value is stable too, but Name is the safer key because it survives accidental
/// integer re-numbering and is readable in the handoff JSON on disk.
/// </para>
/// </remarks>
public sealed class EnumModelJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsClass
            && !typeToConvert.IsAbstract
            && typeof(EnumModel).IsAssignableFrom(typeToConvert);

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var converterType = typeof(EnumModelJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

/// <summary>
/// Type-safe converter for a specific <typeparamref name="TEnum"/> derived from
/// <see cref="EnumModel"/>.
/// </summary>
/// <typeparam name="TEnum">The concrete <see cref="EnumModel"/> subtype.</typeparam>
internal sealed class EnumModelJsonConverter<TEnum> : JsonConverter<TEnum>
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
    public override void Write(
        Utf8JsonWriter writer,
        TEnum value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Name);
    }
}
