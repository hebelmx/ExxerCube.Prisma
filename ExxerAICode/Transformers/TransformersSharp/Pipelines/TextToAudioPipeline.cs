using CommunityToolkit.HighPerformance;
using CSnakes.Runtime.Python;

namespace TransformersSharp.Pipelines;

/// <summary>
/// A pipeline for text-to-audio generation tasks using transformer models.
/// </summary>
public class TextToAudioPipeline : Pipeline
{
    /// <summary>
    /// Represents the result of a text-to-audio generation.
    /// </summary>
    /// <summary>
    /// Represents the result of a text-to-audio generation.
    /// </summary>
    public ref struct AudioResult
    {
        /// <summary>
        /// Gets or sets the generated audio data as a 2D span of float values.
        /// </summary>
        public ReadOnlySpan2D<float> Audio { get; set; }
        
        /// <summary>
        /// Gets or sets the sampling rate of the generated audio.
        /// </summary>
        public int SamplingRate { get; set; }
    }
    internal TextToAudioPipeline(PyObject pipelineObject) : base(pipelineObject)
    {
    }

    /// <summary>
    /// Creates a new instance of the <see cref="TextToAudioPipeline"/> class from a model.
    /// </summary>
    /// <param name="model">The model name or path to use for text-to-audio generation.</param>
    /// <param name="torchDtype">The torch data type to use for the model.</param>
    /// <param name="device">The device to run the model on.</param>
    /// <param name="trustRemoteCode">Whether to trust remote code when loading the model.</param>
    /// <returns>A new instance of <see cref="TextToAudioPipeline"/>.</returns>
    public static TextToAudioPipeline FromModel(string model, TorchDtype? torchDtype = null, string? device = null, bool trustRemoteCode = false)
    {
        return new TextToAudioPipeline(TransformerEnvironment.TransformersWrapper.Pipeline(
            "text-to-audio",
            model,
            null,
            torchDtype?.ToString(),
            device,
            trustRemoteCode));
    }

    /// <summary>
    /// Generates audio from text input.
    /// </summary>
    /// <param name="text">The text to convert to audio.</param>
    /// <returns>The generated audio result.</returns>
    public AudioResult Generate(string text)
    {
        var (audio, sampleRate) = TransformerEnvironment.TransformersWrapper.InvokeTextToAudioPipeline(PipelineObject, text);
        return new AudioResult { 
            Audio = audio.AsFloatReadOnlySpan2D(),
            SamplingRate = (int)sampleRate
        };
    }
}
