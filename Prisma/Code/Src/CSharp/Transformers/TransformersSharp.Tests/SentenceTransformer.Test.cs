using Microsoft.Extensions.AI;
using Shouldly;
using Xunit;

namespace TransformersSharp.Tests;

/// <summary>
/// Tests for the SentenceTransformer functionality.
/// </summary>
public class SentenceTransformerTests
{
    /// <summary>
    /// Tests that the sentence transformer can generate embeddings for multiple sentences.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    async public Task SentenceTransformer_ShouldGenerateEmbeddings()
    {
        var transformer = SentenceTransformer.FromModel("nomic-ai/nomic-embed-text-v1.5", trustRemoteCode: true);
        transformer.ShouldNotBeNull();
        transformer.ShouldBeOfType<SentenceTransformer>();

        var sentences = new List<string>
        {
            "The quick brown fox jumps over the lazy dog.",
            "Transformers are amazing for natural language processing."
        };

        var embeddings = await transformer.GenerateAsync(sentences);

        embeddings.ShouldNotBeNull();
        embeddings.Count.ShouldBe(sentences.Count);
        foreach (var embedding in embeddings)
        {
            embedding.ShouldNotBeNull();
            embedding.ShouldBeOfType<Embedding<float>>();
            embedding.Vector.Length.ShouldBe(768); // Assuming the model produces 768-dimensional embeddings
        }
    }

    /// <summary>
    /// Tests that the sentence transformer can generate a single embedding.
    /// </summary>
    [Fact]
    public void SentenceTransformer_ShouldGenerateSingleEmbedding()
    {
        var transformer = SentenceTransformer.FromModel("nomic-ai/nomic-embed-text-v1.5", trustRemoteCode: true);
        transformer.ShouldNotBeNull();
        transformer.ShouldBeOfType<SentenceTransformer>();
        var sentence = "The quick brown fox jumps over the lazy dog.";
        var embedding = transformer.Generate(sentence);
        embedding.ShouldNotBeNull();
        embedding.ShouldBeOfType<float[]>();
        embedding.Length.ShouldBe(768); // Assuming the model produces 768-dimensional embeddings
    }
}
