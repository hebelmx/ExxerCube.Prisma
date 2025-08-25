using CSnakes.Runtime.Python;
using Microsoft.Extensions.AI;
using System.Numerics.Tensors;

namespace TransformersSharp;

/// <summary>
/// A sentence transformer that generates embeddings for text using transformer models.
/// </summary>
public class SentenceTransformer(PyObject transformerObject) : IEmbeddingGenerator<string, Embedding<float>>
{
    /// <summary>
    /// Creates a new instance of the <see cref="SentenceTransformer"/> class from a model.
    /// </summary>
    /// <param name="model">The model name or path to use for sentence transformation.</param>
    /// <param name="device">The device to run the model on.</param>
    /// <param name="cacheDir">The directory to cache the model in.</param>
    /// <param name="revision">The model revision to use.</param>
    /// <param name="trustRemoteCode">Whether to trust remote code when loading the model.</param>
    /// <returns>A new instance of <see cref="SentenceTransformer"/>.</returns>
    public static SentenceTransformer FromModel(string model, string? device = null, string? cacheDir = null, string? revision = null, bool trustRemoteCode = false)
    {
        return new SentenceTransformer(TransformerEnvironment.SentenceTransformersWrapper.SentenceTransformer(
            model,
            device,
            cacheDir,
            revision,
            trustRemoteCode));
    }

    /// <summary>
    /// Disposes the sentence transformer and releases resources.
    /// </summary>
    public void Dispose()
    {
        transformerObject.Dispose();
    }

    /// <summary>
    /// Generates an embedding for a single sentence.
    /// </summary>
    /// <param name="sentence">The sentence to generate an embedding for.</param>
    /// <returns>The embedding as a float array.</returns>
    public float[] Generate(string sentence)
    {
        var result = TransformerEnvironment.SentenceTransformersWrapper.EncodeSentence(transformerObject, sentence);
        return result.AsFloatReadOnlySpan().ToArray();
    }

    /// <summary>
    /// Generates embeddings for multiple sentences asynchronously.
    /// </summary>
    /// <param name="values">The sentences to generate embeddings for.</param>
    /// <param name="options">Optional embedding generation options.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that represents the asynchronous operation and contains the generated embeddings.</returns>
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var embeddings = new GeneratedEmbeddings<Embedding<float>>();
            var results = TransformerEnvironment.SentenceTransformersWrapper.EncodeSentences(transformerObject, values.ToList());
#pragma warning disable SYSLIB5001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            ReadOnlyTensorSpan<float> tensor = results.AsFloatReadOnlyTensorSpan();
#pragma warning restore SYSLIB5001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

            if (tensor.Lengths.Length != 2)
                throw new ArgumentException("The tensor returned is not 2-dimensional.");

            if (tensor.Lengths[0] != values.Count())
                throw new ArgumentException("The number of sentences does not match the number of embeddings returned.");

            for (int i = 0; i < tensor.Lengths[0]; i++) // Tensor for each sentence's embedding
            {
                var vector = new float[tensor.Lengths[1]];
                // TODO : Find a more efficient way to copy the tensor data to the vector
                for (int j = 0; j < tensor.Lengths[1]; j++)
                {
                    vector[j] = tensor[i, j];
                }
                embeddings.Add(new Embedding<float>(vector));
            }

            return embeddings;
        }, cancellationToken);
    }

    /// <summary>
    /// Gets a service of the specified type and optional key.
    /// </summary>
    /// <param name="serviceType">The type of service to retrieve.</param>
    /// <param name="serviceKey">Optional service key.</param>
    /// <returns>The service instance if found; otherwise, null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when serviceType is null.</exception>
    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType is null ? throw new ArgumentNullException(nameof(serviceType)) :
        serviceKey is not null ? null :
        serviceType.IsInstanceOfType(this) ? this :
        serviceType == typeof(SentenceTransformer) ? this :
        null;
}