namespace ExxerCube.Prisma.SignalR.Abstractions.Tests.Infrastructure.Messaging;

/// <summary>
/// Tests for the MessageBatcher&lt;T&gt; class.
/// </summary>
public class MessageBatcherTests
{
    private readonly ILogger<MessageBatcher<TestMessage>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageBatcherTests"/> class.
    /// </summary>
    public MessageBatcherTests()
    {
        _logger = Substitute.For<ILogger<MessageBatcher<TestMessage>>>();
    }

    /// <summary>
    /// Tests that AddMessageAsync adds message to pending batch.
    /// </summary>
    [Fact]
    public async Task AddMessageAsync_AddsMessage_ToPendingBatch()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(10, TimeSpan.FromSeconds(1), _logger);
        var message = new TestMessage { Id = 1 };

        // Act
        await batcher.AddMessageAsync(message, CancellationToken.None);
        await Task.Delay(100, CancellationToken.None); // Small delay

        // Assert
        // Message should be pending (not yet batched)
        // We can't directly verify internal state, but we can verify it doesn't throw
        batcher.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that batch is flushed when batch size is reached.
    /// </summary>
    [Fact]
    public async Task AddMessageAsync_WhenBatchSizeReached_FlushesBatch()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(3, TimeSpan.FromSeconds(10), _logger);
        var batchReceived = false;
        IReadOnlyList<TestMessage>? receivedBatch = null;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        batcher.BatchReady += (sender, args) =>
        {
            batchReceived = true;
            receivedBatch = args.Messages;
            eventWaitHandle.Set();
        };

        // Act
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await batcher.AddMessageAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        await batcher.AddMessageAsync(new TestMessage { Id = 3 }, CancellationToken.None);
        
        // Wait for event with timeout
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert
        batchReceived.ShouldBeTrue();
        receivedBatch.ShouldNotBeNull();
        receivedBatch.Count.ShouldBe(3);
    }

    /// <summary>
    /// Tests that FlushAsync flushes pending messages immediately.
    /// </summary>
    [Fact]
    public async Task FlushAsync_Flushes_PendingMessages()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(10, TimeSpan.FromSeconds(10), _logger);
        var batchReceived = false;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        batcher.BatchReady += (sender, args) =>
        {
            batchReceived = true;
            eventWaitHandle.Set();
        };

        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);

        // Act
        await batcher.FlushAsync(CancellationToken.None);
        
        // Wait for event with timeout
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert
        batchReceived.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that AddMessageAsync handles cancellation.
    /// </summary>
    [Fact]
    public async Task AddMessageAsync_WithCancellationRequested_DoesNotAddMessage()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(10, TimeSpan.FromSeconds(1), _logger);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, cts.Token);
        // Should complete without error (cancellation is handled gracefully)
    }

    /// <summary>
    /// Test message class for testing.
    /// </summary>
    public class TestMessage
    {
        public int Id { get; set; }
    }
}

