using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Serialization;

/// <summary>
/// Serialises <see cref="TimeSpan"/> as an <see cref="long"/> tick count for STJ round-trip safety.
/// The default STJ behaviour for <see cref="TimeSpan"/> is to write an ISO-8601 string that STJ
/// cannot deserialise back without a custom converter.
/// </summary>
internal sealed class TimeSpanTicksJsonConverter : JsonConverter<TimeSpan>
{
    /// <inheritdoc />
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => TimeSpan.FromTicks(reader.GetInt64());

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.Ticks);
}
