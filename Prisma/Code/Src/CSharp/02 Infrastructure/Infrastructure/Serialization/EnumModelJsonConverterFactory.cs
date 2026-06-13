using System.Text.Json;
using System.Text.Json.Serialization;
using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Infrastructure.Serialization;

/// <summary>
/// <see cref="System.Text.Json.Serialization.JsonConverterFactory"/> that handles any
/// <see cref="EnumModel"/>-derived type in the handoff JSON round-trip (ADR-011, MVP-PATH 1.4).
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
/// <strong>Key choice — Name over Value:</strong> Name is a stable human-readable string identifier
/// (e.g., <c>"A_AS"</c>, <c>"CNBV"</c>, <c>"Aseguramiento"</c>) that is also unique within each
/// derived type. Value is stable too, but Name is the safer key because it survives accidental
/// integer re-numbering and is readable in the handoff JSON on disk.
/// </para>
/// <para>
/// <strong>Ambiguity note:</strong> the Infrastructure project has global usings for both
/// <c>System.Text.Json</c> and <c>Newtonsoft.Json</c>. All System.Text.Json types are therefore
/// fully qualified in this file to avoid CS0104.
/// </para>
/// </remarks>
public sealed class EnumModelJsonConverterFactory : System.Text.Json.Serialization.JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsClass
            && !typeToConvert.IsAbstract
            && typeof(EnumModel).IsAssignableFrom(typeToConvert);

    /// <inheritdoc />
    public override System.Text.Json.Serialization.JsonConverter? CreateConverter(
        Type typeToConvert,
        System.Text.Json.JsonSerializerOptions options)
    {
        var converterType = typeof(EnumModelJsonConverter<>).MakeGenericType(typeToConvert);
        return (System.Text.Json.Serialization.JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

/// <summary>
/// Type-safe converter for a specific <typeparamref name="TEnum"/> derived from
/// <see cref="EnumModel"/>.
/// </summary>
/// <typeparam name="TEnum">The concrete <see cref="EnumModel"/> subtype.</typeparam>
internal sealed class EnumModelJsonConverter<TEnum> : System.Text.Json.Serialization.JsonConverter<TEnum>
    where TEnum : EnumModel, new()
{
    /// <inheritdoc />
    public override TEnum Read(
        ref System.Text.Json.Utf8JsonReader reader,
        Type typeToConvert,
        System.Text.Json.JsonSerializerOptions options)
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
        System.Text.Json.Utf8JsonWriter writer,
        TEnum value,
        System.Text.Json.JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Name);
    }
}
