using CSnakes.Runtime.Python;

namespace TransformersSharp.Pipelines;

/// <summary>
/// A pipeline for text classification tasks using transformer models.
/// </summary>
public class TextClassificationPipeline : Pipeline
{
    internal TextClassificationPipeline(PyObject pipelineObject) : base(pipelineObject)
    {
    }

    /// <summary>
    /// Creates a new instance of the <see cref="TextClassificationPipeline"/> class from a model.
    /// </summary>
    /// <param name="model">The model name or path to use for text classification.</param>
    /// <param name="torchDtype">The torch data type to use for the model.</param>
    /// <param name="device">The device to run the model on.</param>
    /// <returns>A new instance of <see cref="TextClassificationPipeline"/>.</returns>
    public static TextClassificationPipeline FromModel(string model, TorchDtype? torchDtype = null, string? device = null)
    {
        return new TextClassificationPipeline(TransformerEnvironment.TransformersWrapper.Pipeline(
            "text-classification",
            model,
            null,
            torchDtype?.ToString(),
            device));
    }

    /// <summary>
    /// Classifies a single text input.
    /// </summary>
    /// <param name="input">The text to classify.</param>
    /// <returns>A list of classification results with labels and scores.</returns>
    public IReadOnlyList<(string Label, double Score)> Classify(string input)
    {
        return RunPipeline(input).Select(result => (result["label"].As<string>(), result["score"].As<double>())).ToList();
    }

    /// <summary>
    /// Classifies multiple text inputs in batch.
    /// </summary>
    /// <param name="inputs">The list of texts to classify.</param>
    /// <returns>A list of classification results with labels and scores.</returns>
    public IReadOnlyList<(string Label, double Score)> ClassifyBatch(IReadOnlyList<string> inputs)
    {
        return RunPipeline(inputs).Select(result => (result["label"].As<string>(), result["score"].As<double>())).ToList();
    }
}
