using TransformersSharp.MEAI;
using Microsoft.Extensions.AI;
using Shouldly;
using Xunit;

namespace TransformersSharp.Tests;

/// <summary>
/// Tests for the TransformerSharp MEAI integration functionality.
/// </summary>
public class TransformerSharpMEAITests
{
    /// <summary>
    /// Tests that the chat client can generate responses correctly.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task TestChatClient()
    {
        var chatClient = TextGenerationPipelineChatClient.FromModel("Qwen/Qwen2.5-0.5B", TorchDtype.BFloat16, trustRemoteCode: true);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful little robot."),
            new(ChatRole.User, "how many helicopters can a human eat in one sitting?!")
        };
        var response = await chatClient.GetResponseAsync(messages, new() { Temperature = 0.7f });
        
        response.ShouldNotBeNull();
        response.Text.ToLowerInvariant().ShouldContain("helicopter");
    }

    /// <summary>
    /// Tests that the chat client can generate streaming responses correctly.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task TestChatClientStreaming()
    {
        var chatClient = TextGenerationPipelineChatClient.FromModel("Qwen/Qwen2.5-0.5B", TorchDtype.BFloat16, trustRemoteCode: true);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful little robot."),
            new(ChatRole.User, "how many helicopters can a human eat in one sitting?!")
        };
        var response = chatClient.GetStreamingResponseAsync(messages, new() { Temperature = 0.7f });
        await foreach (var update in response)
        {
            update.ShouldNotBeNull();
            update.Text.ShouldNotBeEmpty();
        }
    }

    /// <summary>
    /// Tests that the speech to text client can transcribe audio correctly.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task TestSpeechToTextClient()
    {
        var speechClient = SpeechToTextClient.FromModel("openai/whisper-tiny");
        using var audioStream = new MemoryStream(File.ReadAllBytes("sample.flac"));
        var response = await speechClient.GetTextAsync(audioStream);
        
        response.ShouldNotBeNull();
        response.Text.ShouldNotBeEmpty();
        response.Text.ToLowerInvariant().ShouldContain("stew for dinner");
    }

    /// <summary>
    /// Tests that the speech to text client can generate streaming transcriptions correctly.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task TestSpeechToTextClientStreaming()
    {
        var speechClient = SpeechToTextClient.FromModel("openai/whisper-tiny");
        using var audioStream = new MemoryStream(File.ReadAllBytes("sample.flac"));
        var response = speechClient.GetStreamingTextAsync(audioStream);
        await foreach (var update in response)
        {
            update.ShouldNotBeNull();
            update.Text.ShouldNotBeEmpty();
            update.Text.ToLowerInvariant().ShouldContain("stew for dinner");
        }
    }
}
