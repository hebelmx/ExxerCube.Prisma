using CSnakes.Runtime.Python;

namespace TransformersSharp.Pipelines;

/// <summary>
/// A pipeline for text generation tasks using transformer models.
/// </summary>
public class TextGenerationPipeline : Pipeline
{
    internal TextGenerationPipeline(PyObject pipelineObject) : base(pipelineObject)
    {
    }

    /// <summary>
    /// Creates a new instance of the <see cref="TextGenerationPipeline"/> class from a model.
    /// </summary>
    /// <param name="model">The model name or path to use for text generation.</param>
    /// <param name="torchDtype">The torch data type to use for the model.</param>
    /// <param name="device">The device to run the model on.</param>
    /// <param name="trustRemoteCode">Whether to trust remote code when loading the model.</param>
    /// <returns>A new instance of <see cref="TextGenerationPipeline"/>.</returns>
    public static TextGenerationPipeline FromModel(string model, TorchDtype? torchDtype = null, string? device = null, bool trustRemoteCode = false)
    {
        return new TextGenerationPipeline(TransformerEnvironment.TransformersWrapper.Pipeline(
            "text-generation",
            model,
            null,
            torchDtype?.ToString(),
            device,
            trustRemoteCode));
    }

    /// <summary>
    /// Generates text based on the input prompt.
    /// </summary>
    /// <param name="input">The input text to generate from.</param>
    /// <returns>An enumerable of generated text strings.</returns>
    public IEnumerable<string> Generate(string input)
    {
        var results = RunPipeline(input);
        return results.Select(result => result["generated_text"].As<string>());
    }

    /// <summary>
    /// Generates text based on a list of messages with various generation parameters.
    /// </summary>
    /// <param name="messages">The list of messages to generate from.</param>
    /// <param name="maxLength">Maximum length of the generated text.</param>
    /// <param name="maxNewTokens">Maximum number of new tokens to generate.</param>
    /// <param name="minLength">Minimum length of the generated text.</param>
    /// <param name="minNewTokens">Minimum number of new tokens to generate.</param>
    /// <param name="stopStrings">List of strings that stop generation when encountered.</param>
    /// <param name="temperature">Controls randomness in generation (0.0 to 2.0).</param>
    /// <param name="topk">Number of highest probability tokens to consider.</param>
    /// <param name="topp">Cumulative probability threshold for token selection.</param>
    /// <param name="minp">Minimum probability threshold for token selection.</param>
    /// <returns>An enumerable of generated message dictionaries.</returns>
    public IEnumerable<IReadOnlyDictionary<string, string>> Generate(IReadOnlyList<IReadOnlyDictionary<string, string>> messages, long? maxLength = null, long? maxNewTokens = null, long? minLength = null, long? minNewTokens = null, IReadOnlyList<string>? stopStrings = null, double? temperature = 1, long? topk = 50, double? topp = 1, double? minp = null)
    {
        var results = TransformerEnvironment.TransformersWrapper.InvokeTextGenerationPipelineWithTemplate(PipelineObject, messages, maxLength, maxNewTokens, minLength, minNewTokens, stopStrings, temperature, topk, topp, minp);
        return results;
    }

    /// <summary>
    /// Streams text generation based on a list of messages with various generation parameters.
    /// </summary>
    /// <param name="messages">The list of messages to generate from.</param>
    /// <param name="maxLength">Maximum length of the generated text.</param>
    /// <param name="maxNewTokens">Maximum number of new tokens to generate.</param>
    /// <param name="minLength">Minimum length of the generated text.</param>
    /// <param name="minNewTokens">Minimum number of new tokens to generate.</param>
    /// <param name="stopStrings">List of strings that stop generation when encountered.</param>
    /// <param name="temperature">Controls randomness in generation (0.0 to 2.0).</param>
    /// <param name="topk">Number of highest probability tokens to consider.</param>
    /// <param name="topp">Cumulative probability threshold for token selection.</param>
    /// <param name="minp">Minimum probability threshold for token selection.</param>
    /// <returns>An enumerator that yields generated message dictionaries.</returns>
    public IEnumerator<IReadOnlyDictionary<string, string>> Stream(IReadOnlyList<IReadOnlyDictionary<string, string>> messages, long? maxLength = null, long? maxNewTokens = null, long? minLength = null, long? minNewTokens = null, IReadOnlyList<string>? stopStrings = null, double? temperature = 1, long? topk = 50, double? topp = 1, double? minp = null)
    {
        var results = TransformerEnvironment.TransformersWrapper.StreamTextGenerationPipelineWithTemplate(PipelineObject, messages, maxLength, maxNewTokens, minLength, minNewTokens, stopStrings, temperature, topk, topp, minp);
        return results.GetEnumerator();
    }
}
