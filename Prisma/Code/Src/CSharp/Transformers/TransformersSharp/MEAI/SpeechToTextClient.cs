using Microsoft.Extensions.AI;
using TransformersSharp.Pipelines;

namespace TransformersSharp.MEAI;

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

/// <summary>
/// A client for speech-to-text tasks using an automatic speech recognition pipeline.
/// </summary>
public class SpeechToTextClient : ISpeechToTextClient
{
    /// <summary>
    /// The automatic speech recognition pipeline used for transcribing audio to text.
    /// </summary>
    public AutomaticSpeechRecognitionPipeline? AutomaticSpeechRecognitionPipeline { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpeechToTextClient"/> class from the specified model.
    /// </summary>
    /// <param name="model"></param>
    /// <param name="torchDtype"></param>
    /// <param name="device"></param>
    /// <param name="trustRemoteCode"></param>
    /// <returns></returns>
    public static SpeechToTextClient FromModel(string model, TorchDtype? torchDtype = null, string? device = null, bool trustRemoteCode = false)
    {
        return new SpeechToTextClient
        {
            AutomaticSpeechRecognitionPipeline = AutomaticSpeechRecognitionPipeline.FromModel(model, torchDtype, device, trustRemoteCode: trustRemoteCode)
        };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpeechToTextClient"/> class with the specified automatic speech recognition pipeline.
    /// </summary>
    public void Dispose()
    {
        // Nothing to do right now. ASR pipeline object will be collected via next GC pass anyway.
    }

    /// <summary>
    /// Gets a service of the specified type and optional key.
    /// </summary>
    /// <param name="serviceType"></param>
    /// <param name="serviceKey"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType is null ? throw new ArgumentNullException(nameof(serviceType)) :
        serviceKey is not null ? null :
        serviceType.IsInstanceOfType(this) ? this :
        serviceType == typeof(AutomaticSpeechRecognitionPipeline) ? this.AutomaticSpeechRecognitionPipeline :
        null;

    /// <summary>
    /// Transcribes the provided audio stream into text asynchronously.
    /// </summary>
    /// <param name="audioSpeechStream"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<SpeechToTextResponse> GetTextAsync(Stream audioSpeechStream, SpeechToTextOptions? options = null, CancellationToken cancellationToken = default)
    {
        byte[] audioBytes = new byte[audioSpeechStream.Length];
        await audioSpeechStream.ReadExactlyAsync(audioBytes, 0, (int)audioSpeechStream.Length, cancellationToken);
        if (AutomaticSpeechRecognitionPipeline is not null)
        {
            var result = AutomaticSpeechRecognitionPipeline.Transcribe(audioBytes);
            return new SpeechToTextResponse(result);
        }
        return new SpeechToTextResponse(string.Empty);
    }

    /// <summary>
    /// Transcribes the provided audio stream into text asynchronously, yielding updates as they become available.
    /// </summary>
    /// <param name="audioSpeechStream"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(Stream audioSpeechStream, SpeechToTextOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // None of the models are streaming yet.
        var response = await GetTextAsync(audioSpeechStream, options, cancellationToken).ConfigureAwait(false);
        foreach (var update in response.ToSpeechToTextResponseUpdates())
        {
            yield return update;
        }
    }
}

#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.