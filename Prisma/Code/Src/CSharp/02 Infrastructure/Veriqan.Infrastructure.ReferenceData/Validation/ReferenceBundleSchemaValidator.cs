using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using IndQuestResults;
using IndQuestResults.Operations;
using Json.Schema;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Validation;

/// <summary>
/// Validates a serialized <c>VecReferenceBundle</c> JSON document against the embedded
/// <c>vec-reference-bundle.schema.json</c> (JSON Schema draft 2020-12).
/// Lives outside the CSV adapter so any future adapter (DB, API) can reuse it without
/// duplicating the schema engine wiring.
/// </summary>
public sealed class ReferenceBundleSchemaValidator
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null   // records use explicit [JsonPropertyName] attributes
    };

    private readonly JsonSchema _schema;

    /// <summary>
    /// Initializes the validator by loading the embedded schema resource.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the embedded schema resource cannot be found.</exception>
    public ReferenceBundleSchemaValidator()
    {
        _schema = LoadEmbeddedSchema();
    }

    /// <summary>
    /// Validates <paramref name="bundle"/> against the JSON Schema contract.
    /// Serializes the bundle to JSON, then evaluates it with <c>draft 2020-12</c> semantics.
    /// </summary>
    /// <param name="bundle">The bundle to validate.</param>
    /// <returns>
    /// <see cref="Result{T}"/> success with the original bundle when the document is valid;
    /// a failure carrying concatenated validation error messages when it is not.
    /// </returns>
    public Result<ExxerCube.Prisma.Veriqan.Domain.ReferenceData.VecReferenceBundle> Validate(
        ExxerCube.Prisma.Veriqan.Domain.ReferenceData.VecReferenceBundle bundle)
    {
        string json = JsonSerializer.Serialize(bundle, SerializerOptions);

        var node = JsonNode.Parse(json);
        if (node is null)
            return Result<ExxerCube.Prisma.Veriqan.Domain.ReferenceData.VecReferenceBundle>
                .WithFailure("Failed to parse serialized bundle as JSON node.");

        var options = new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
            RequireFormatValidation = false
        };

        EvaluationResults results = _schema.Evaluate(node, options);

        if (results.IsValid)
            return Result<ExxerCube.Prisma.Veriqan.Domain.ReferenceData.VecReferenceBundle>.WithSuccess(bundle);

        var errors = CollectErrors(results);
        string message = $"VecReferenceBundle failed schema validation ({errors.Count} error(s)): {string.Join("; ", errors)}";
        return Result<ExxerCube.Prisma.Veriqan.Domain.ReferenceData.VecReferenceBundle>.WithFailure(message);
    }

    // ── private helpers ──────────────────────────────────────────────────────

    private static JsonSchema LoadEmbeddedSchema()
    {
        var assembly = typeof(ReferenceBundleSchemaValidator).Assembly;
        const string resourceName =
            "ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Schema.vec-reference-bundle.schema.json";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded schema resource '{resourceName}' not found in assembly '{assembly.FullName}'. " +
                "Verify the file is marked as EmbeddedResource in the .csproj.");

        return JsonSchema.FromStream(stream).GetAwaiter().GetResult();
    }

    private static List<string> CollectErrors(EvaluationResults results)
    {
        var errors = new List<string>();
        if (results.Details is null) return errors;

        foreach (var detail in results.Details)
        {
            if (detail.IsValid) continue;
            if (detail.Errors is not null)
            {
                foreach (var kvp in detail.Errors)
                    errors.Add($"{detail.InstanceLocation} [{kvp.Key}]: {kvp.Value}");
            }
        }

        return errors;
    }
}
