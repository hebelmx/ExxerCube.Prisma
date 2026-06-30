using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Serialization;

/// <summary>
/// Custom STJ converter for <see cref="VerdictSummary"/>.
/// <see cref="VerdictSummary"/> has a private constructor that STJ cannot invoke natively;
/// this converter uses reflection to call it after reading each property from the JSON object.
/// </summary>
/// <remarks>
/// The computed property <c>LegalBaselineSignal</c> is intentionally omitted — it is derived
/// from other persisted properties and is never stored separately.
/// </remarks>
internal sealed class VerdictSummaryJsonConverter : JsonConverter<VerdictSummary>
{
    /// <inheritdoc />
    public override VerdictSummary Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        T Get<T>(string propName) =>
            root.TryGetProperty(propName, out var el)
                ? JsonSerializer.Deserialize<T>(el.GetRawText(), options)!
                : default(T)!;

        IReadOnlyList<T>? GetList<T>(string propName) =>
            root.TryGetProperty(propName, out var el) && el.ValueKind != JsonValueKind.Null
                ? JsonSerializer.Deserialize<List<T>>(el.GetRawText(), options)
                : null;

        BlockedOutcome? blockedOutcome = null;
        if (root.TryGetProperty("BlockedOutcome", out var bo) && bo.ValueKind != JsonValueKind.Null)
            blockedOutcome = JsonSerializer.Deserialize<BlockedOutcome>(bo.GetRawText(), options);

        double confidence = 1.0;
        if (root.TryGetProperty("Confidence", out var conf))
            confidence = conf.GetDouble();

        string? transientDetail = null;
        if (root.TryGetProperty("TransientDetail", out var td) && td.ValueKind != JsonValueKind.Null)
            transientDetail = td.GetString();

        var ctor = typeof(VerdictSummary)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0];

        return (VerdictSummary)ctor.Invoke(
        [
            Get<VerdictSignal>("Signal"),
            Get<int>("FailCount"),
            Get<int>("PassCount"),
            Get<int>("InsufficientDataCount"),
            Get<int>("Total"),
            (object?)(GetList<string>("FailCheckIds") ?? (IReadOnlyList<string>)[]),
            (object?)(GetList<string>("InsufficientDataCheckIds") ?? (IReadOnlyList<string>)[]),
            blockedOutcome,
            GetList<TenantDeviation>("TenantDeviations"),
            GetList<string>("LegalBreachCheckIds"),
            GetList<string>("TenantOnlyFailCheckIds"),
            Get<VerdictSignal>("BankTierVerdict"),
            Get<VerdictSignal>("CondusefTierVerdict"),
            GetList<string>("BankFailCheckIds"),
            GetList<string>("CondusefFailCheckIds"),
            confidence,
            transientDetail,
        ]);
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        VerdictSummary value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteNumber("Signal", (int)value.Signal);
        writer.WriteNumber("FailCount", value.FailCount);
        writer.WriteNumber("PassCount", value.PassCount);
        writer.WriteNumber("InsufficientDataCount", value.InsufficientDataCount);
        writer.WriteNumber("Total", value.Total);

        writer.WritePropertyName("FailCheckIds");
        JsonSerializer.Serialize(writer, value.FailCheckIds, options);

        writer.WritePropertyName("InsufficientDataCheckIds");
        JsonSerializer.Serialize(writer, value.InsufficientDataCheckIds, options);

        writer.WritePropertyName("BlockedOutcome");
        JsonSerializer.Serialize(writer, value.BlockedOutcome, options);

        writer.WritePropertyName("TenantDeviations");
        JsonSerializer.Serialize(writer, value.TenantDeviations, options);

        writer.WritePropertyName("LegalBreachCheckIds");
        JsonSerializer.Serialize(writer, value.LegalBreachCheckIds, options);

        writer.WritePropertyName("TenantOnlyFailCheckIds");
        JsonSerializer.Serialize(writer, value.TenantOnlyFailCheckIds, options);

        writer.WriteNumber("BankTierVerdict", (int)value.BankTierVerdict);
        writer.WriteNumber("CondusefTierVerdict", (int)value.CondusefTierVerdict);

        writer.WritePropertyName("BankFailCheckIds");
        JsonSerializer.Serialize(writer, value.BankFailCheckIds, options);

        writer.WritePropertyName("CondusefFailCheckIds");
        JsonSerializer.Serialize(writer, value.CondusefFailCheckIds, options);

        writer.WriteNumber("Confidence", value.Confidence);

        if (value.TransientDetail is null)
            writer.WriteNull("TransientDetail");
        else
            writer.WriteString("TransientDetail", value.TransientDetail);

        writer.WriteEndObject();
    }
}
