namespace ExxerAI.Application.Sample.Services;

using ExxerAI.Domain.Sample.Entities;

/// <summary>Coordinates SampleEntity usage.</summary>
public class SampleConsumer : ISampleProcessor
{
    public SampleConsumer(SampleEntity entity)
    {
        Entity = entity;
    }

    public SampleEntity Entity { get; }

    public void Execute()
    {
        var local = new SampleEntity("generated", "runtime");
        _ = local.Process(default);
    }
}
