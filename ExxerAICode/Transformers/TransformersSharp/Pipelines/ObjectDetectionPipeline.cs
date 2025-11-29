using CSnakes.Runtime.Python;

namespace TransformersSharp.Pipelines;

/// <summary>
/// A pipeline for object detection tasks using transformer models.
/// </summary>
public class ObjectDetectionPipeline: Pipeline
{
    /// <summary>
    /// Represents a detection box with coordinates.
    /// </summary>
    /// <param name="XMin">The minimum X coordinate of the detection box.</param>
    /// <param name="YMin">The minimum Y coordinate of the detection box.</param>
    /// <param name="XMax">The maximum X coordinate of the detection box.</param>
    /// <param name="YMax">The maximum Y coordinate of the detection box.</param>
    public readonly record struct DetectionBox(int XMin, int YMin, int XMax, int YMax);
    
    /// <summary>
    /// Represents the result of an object detection.
    /// </summary>
    /// <param name="Label">The label of the detected object.</param>
    /// <param name="Score">The confidence score of the detection.</param>
    /// <param name="Box">The bounding box of the detected object.</param>
    public readonly record struct DetectionResult(string Label, double Score, DetectionBox Box);

    internal ObjectDetectionPipeline(PyObject pipelineObject) : base(pipelineObject)
    {
    }
    /// <summary>
    /// Creates a new instance of the <see cref="ObjectDetectionPipeline"/> class from a model.
    /// </summary>
    /// <param name="model">The model name or path to use for object detection.</param>
    /// <param name="torchDtype">The torch data type to use for the model.</param>
    /// <param name="device">The device to run the model on.</param>
    /// <param name="trustRemoteCode">Whether to trust remote code when loading the model.</param>
    /// <returns>A new instance of <see cref="ObjectDetectionPipeline"/>.</returns>
    public static ObjectDetectionPipeline FromModel(string model, TorchDtype? torchDtype = null, string? device = null, bool trustRemoteCode = false)
    {
        return new ObjectDetectionPipeline(TransformerEnvironment.TransformersWrapper.Pipeline(
            "object-detection",
            model,
            null,
            torchDtype?.ToString(),
            device,
            trustRemoteCode));
    }
    /// <summary>
    /// Detects objects in an image from a file path.
    /// </summary>
    /// <param name="path">The path to the image file.</param>
    /// <param name="threshold">The confidence threshold for detections.</param>
    /// <param name="timeout">Optional timeout for the detection process.</param>
    /// <returns>An enumerable of detection results.</returns>
    public IEnumerable<DetectionResult> Detect(string path, double threshold = 0.5, double? timeout = null)
    {
        IEnumerable<(string Label, double Score, (long XMin, long YMin, long XMax, long YMax) Box)> results =
            TransformerEnvironment.TransformersWrapper.InvokeObjectDetectionPipeline(PipelineObject, path, threshold, timeout);

        return
            from e in results
            select new DetectionResult
            {
                Label = e.Label,
                Score = e.Score,
                Box = checked(new()
                {
                    XMin = (int)e.Box.XMin,
                    YMin = (int)e.Box.YMin,
                    XMax = (int)e.Box.XMax,
                    YMax = (int)e.Box.YMax,
                })
            };
    }
}
