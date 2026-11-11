namespace ExxerAI.Domain.Sample.Entities;

using System.Threading;

/// <summary>Represents a simple document ingested for doc quality trials.</summary>
/// <remarks>Used to verify cataloging and lint heuristics.</remarks>
public class SampleEntity
{
    /// <summary>Creates a new SampleEntity capturing identity and text.</summary>
    /// <param name="id">Unique identifier for the sample entity.</param>
    /// <param name="description">Optional descriptive text for analytics.</param>
    public SampleEntity(string id, string description)
    {
        Id = id;
        Description = description;
    }

    /// <summary>Gets the entity identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the sample description.</summary>
    /// <remarks>Returns whatever text was supplied during construction.</remarks>
    public string Description { get; }

    /// <summary>Process entity without adequate detail.</summary>
    public string Process(CancellationToken token)
    {
        return Description;
    }
}
