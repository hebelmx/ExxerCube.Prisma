using CSnakes.Runtime.Python;

namespace TransformersSharp.Pipelines;

/// <summary>
/// A pipeline for automatic speech recognition tasks using transformer models.
/// </summary>
public class AutomaticSpeechRecognitionPipeline : Pipeline
{
    internal AutomaticSpeechRecognitionPipeline(PyObject pipelineObject) : base(pipelineObject)
    {
    }
    /// <summary>
    /// Creates a new instance of the <see cref="AutomaticSpeechRecognitionPipeline"/> class from a model.
    /// </summary>
    /// <param name="model">The model name or path to use for speech recognition.</param>
    /// <param name="torchDtype">The torch data type to use for the model.</param>
    /// <param name="device">The device to run the model on.</param>
    /// <param name="trustRemoteCode">Whether to trust remote code when loading the model.</param>
    /// <returns>A new instance of <see cref="AutomaticSpeechRecognitionPipeline"/>.</returns>
    public static AutomaticSpeechRecognitionPipeline FromModel(string model, TorchDtype? torchDtype = null, string? device = null, bool trustRemoteCode = false)
    {
        return new AutomaticSpeechRecognitionPipeline(TransformerEnvironment.TransformersWrapper.Pipeline(
            "automatic-speech-recognition",
            model,
            null,
            torchDtype?.ToString(),
            device,
            trustRemoteCode));
    }

    /// <summary>
    /// Transcribes audio from a file path or URL.
    /// </summary>
    /// <param name="audioPath">Local file path or URL to the audio file.</param>
    /// <returns>The transcribed text.</returns>
    public string Transcribe(string audioPath)
    {
        return TransformerEnvironment.TransformersWrapper.InvokeAutomaticSpeechRecognitionPipeline(PipelineObject, audioPath);
    }

    /// <summary>
    /// Transcribes audio from byte array data.
    /// </summary>
    /// <param name="audio">The audio data as a byte array.</param>
    /// <returns>The transcribed text.</returns>
    public string Transcribe(byte[] audio)
    {
        return TransformerEnvironment.TransformersWrapper.InvokeAutomaticSpeechRecognitionPipelineFromBytes(PipelineObject, audio);
    }
}
