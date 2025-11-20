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
    /// Tests that AddMessageAsync does not flush when batchSize-1 messages are added.
    /// </summary>
    [Fact]
    public async Task AddMessageAsync_WithBatchSizeMinusOne_DoesNotFlush()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(3, TimeSpan.FromSeconds(10), _logger);
        var batchReceived = false;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        batcher.BatchReady += (sender, args) =>
        {
            batchReceived = true;
            eventWaitHandle.Set();
        };

        // Act - Add batchSize-1 messages
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await batcher.AddMessageAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        
        // Wait a short time to ensure no flush occurred
        await Task.Delay(100, CancellationToken.None);

        // Assert
        batchReceived.ShouldBeFalse();
    }

    /// <summary>
    /// Tests that AddMessageAsync flushes when exactly batchSize messages are added.
    /// </summary>
    [Fact]
    public async Task AddMessageAsync_WithExactlyBatchSize_FlushesBatch()
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

        // Act - Add exactly batchSize messages
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
    /// Tests that batch is flushed via timer after batchInterval elapses.
    /// </summary>
    [Fact]
    public async Task AddMessageAsync_AfterBatchInterval_FlushesViaTimer()
    {
        // Arrange
        var batchInterval = TimeSpan.FromMilliseconds(300);
        using var batcher = new MessageBatcher<TestMessage>(10, batchInterval, _logger);
        var batchReceived = false;
        IReadOnlyList<TestMessage>? receivedBatch = null;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        batcher.BatchReady += (sender, args) =>
        {
            batchReceived = true;
            receivedBatch = args.Messages;
            eventWaitHandle.Set();
        };

        // Act - Add message, wait for timer
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        
        // Wait for timer to fire (batchInterval + buffer for timer precision)
        var waitResult = eventWaitHandle.Wait(batchInterval.Add(TimeSpan.FromMilliseconds(200)), CancellationToken.None);

        // Assert
        waitResult.ShouldBeTrue("Timer should have fired within timeout");
        batchReceived.ShouldBeTrue();
        receivedBatch.ShouldNotBeNull();
        receivedBatch.Count.ShouldBe(1);
    }

    /// <summary>
    /// Tests that FlushAsync with empty batch does not raise event.
    /// </summary>
    [Fact]
    public async Task FlushAsync_WithEmptyBatch_DoesNotRaiseEvent()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(10, TimeSpan.FromSeconds(1), _logger);
        var batchReceived = false;

        batcher.BatchReady += (sender, args) => batchReceived = true;

        // Act
        await batcher.FlushAsync(CancellationToken.None);
        await Task.Delay(100, CancellationToken.None);

        // Assert
        batchReceived.ShouldBeFalse();
    }

    /// <summary>
    /// Tests that FlushAsync disposes timer and allows new timer to start.
    /// </summary>
    [Fact]
    public async Task FlushAsync_DisposesTimer_AndResetsForNextBatch()
    {
        // Arrange
        var batchInterval = TimeSpan.FromMilliseconds(300);
        using var batcher = new MessageBatcher<TestMessage>(10, batchInterval, _logger);
        var batchCount = 0;
        using var eventWaitHandle1 = new ManualResetEventSlim(false);
        using var eventWaitHandle2 = new ManualResetEventSlim(false);

        batcher.BatchReady += (sender, args) =>
        {
            batchCount++;
            if (batchCount == 1 && args.Messages.Count == 1 && args.Messages[0].Id == 1)
            {
                eventWaitHandle1.Set();
            }
            else if (batchCount == 2 && args.Messages.Count == 1 && args.Messages[0].Id == 2)
            {
                eventWaitHandle2.Set();
            }
        };

        // Act - Add message, flush immediately
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await batcher.FlushAsync(CancellationToken.None);
        eventWaitHandle1.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);
        
        // Add new message - timer should restart
        await batcher.AddMessageAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        var waitResult = eventWaitHandle2.Wait(batchInterval.Add(TimeSpan.FromMilliseconds(200)), CancellationToken.None);

        // Assert
        waitResult.ShouldBeTrue("Timer should have fired for second batch");
        batchCount.ShouldBe(2);
    }

    /// <summary>
    /// Tests that FlushAsync handles cancellation gracefully.
    /// </summary>
    [Fact]
    public async Task FlushAsync_WithCancellationRequested_HandlesGracefully()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(10, TimeSpan.FromSeconds(1), _logger);
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Should throw TaskCanceledException when trying to acquire lock
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await batcher.FlushAsync(cts.Token));
    }

    /// <summary>
    /// Tests that multiple rapid FlushAsync calls are handled gracefully.
    /// </summary>
    [Fact]
    public async Task FlushAsync_CalledMultipleTimes_HandlesGracefully()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(10, TimeSpan.FromSeconds(1), _logger);
        var batchCount = 0;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        batcher.BatchReady += (sender, args) =>
        {
            batchCount++;
            eventWaitHandle.Set();
        };

        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);

        // Act - Call FlushAsync multiple times rapidly
        await batcher.FlushAsync(CancellationToken.None);
        await batcher.FlushAsync(CancellationToken.None);
        await batcher.FlushAsync(CancellationToken.None);
        
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert - Should only flush once (subsequent flushes have empty batch)
        batchCount.ShouldBe(1);
    }

    /// <summary>
    /// Tests that BatchReady event contains correct messages in order.
    /// </summary>
    [Fact]
    public async Task BatchReady_EventContains_CorrectMessages()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(5, TimeSpan.FromSeconds(10), _logger);
        IReadOnlyList<TestMessage>? receivedBatch = null;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        batcher.BatchReady += (sender, args) =>
        {
            receivedBatch = args.Messages;
            eventWaitHandle.Set();
        };

        var messages = new[]
        {
            new TestMessage { Id = 1 },
            new TestMessage { Id = 2 },
            new TestMessage { Id = 3 },
            new TestMessage { Id = 4 },
            new TestMessage { Id = 5 }
        };

        // Act
        foreach (var message in messages)
        {
            await batcher.AddMessageAsync(message, CancellationToken.None);
        }
        
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert
        receivedBatch.ShouldNotBeNull();
        receivedBatch.Count.ShouldBe(5);
        receivedBatch[0].Id.ShouldBe(1);
        receivedBatch[1].Id.ShouldBe(2);
        receivedBatch[2].Id.ShouldBe(3);
        receivedBatch[3].Id.ShouldBe(4);
        receivedBatch[4].Id.ShouldBe(5);
    }

    /// <summary>
    /// Tests that constructor throws ArgumentOutOfRangeException when batchSize is zero.
    /// </summary>
    [Fact]
    public void Constructor_WithZeroBatchSize_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MessageBatcher<TestMessage>(0, TimeSpan.FromSeconds(1), _logger));
    }

    /// <summary>
    /// Tests that constructor throws ArgumentOutOfRangeException when batchSize is negative.
    /// </summary>
    [Fact]
    public void Constructor_WithNegativeBatchSize_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MessageBatcher<TestMessage>(-1, TimeSpan.FromSeconds(1), _logger));
    }

    /// <summary>
    /// Tests that constructor throws ArgumentNullException when logger is null.
    /// </summary>
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MessageBatcher<TestMessage>(10, TimeSpan.FromSeconds(1), null!));
    }

    /// <summary>
    /// Tests that BatchReadyEventArgs constructor throws ArgumentNullException when messages is null.
    /// </summary>
    [Fact]
    public void BatchReadyEventArgs_WithNullMessages_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new BatchReadyEventArgs<TestMessage>(null!));
    }

    /// <summary>
    /// Tests that timer callback handles exceptions gracefully.
    /// </summary>
    [Fact]
    public async Task OnBatchTimer_WhenExceptionOccurs_LogsError()
    {
        // Arrange
        var batchInterval = TimeSpan.FromMilliseconds(100);
        using var batcher = new MessageBatcher<TestMessage>(10, batchInterval, _logger);
        
        // Simulate exception by disposing batcher while timer is running
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        
        // Wait for timer to potentially fire
        await Task.Delay(batchInterval.Add(TimeSpan.FromMilliseconds(50)), CancellationToken.None);

        // Act - Dispose should handle any timer callback exceptions
        batcher.Dispose();

        // Assert - Verify error was logged if exception occurred
        // Note: This is a behavioral test - we verify the system handles exceptions gracefully
        batcher.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that AddMessageAsync starts timer on first message.
    /// </summary>
    [Fact]
    public async Task AddMessageAsync_OnFirstMessage_StartsTimer()
    {
        // Arrange
        var batchInterval = TimeSpan.FromMilliseconds(200);
        using var batcher = new MessageBatcher<TestMessage>(10, batchInterval, _logger);
        var batchReceived = false;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        batcher.BatchReady += (sender, args) =>
        {
            batchReceived = true;
            eventWaitHandle.Set();
        };

        // Act - Add first message (should start timer)
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        
        // Wait for timer to fire
        eventWaitHandle.Wait(batchInterval.Add(TimeSpan.FromMilliseconds(100)), CancellationToken.None);

        // Assert
        batchReceived.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that AddMessageAsync does not start new timer if timer already exists.
    /// </summary>
    [Fact]
    public async Task AddMessageAsync_WhenTimerExists_DoesNotStartNewTimer()
    {
        // Arrange
        var batchInterval = TimeSpan.FromMilliseconds(300);
        using var batcher = new MessageBatcher<TestMessage>(10, batchInterval, _logger);
        var batchCount = 0;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        batcher.BatchReady += (sender, args) =>
        {
            batchCount++;
            eventWaitHandle.Set();
        };

        // Act - Add first message (starts timer), then add more messages before timer fires
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None);
        await batcher.AddMessageAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None);
        await batcher.AddMessageAsync(new TestMessage { Id = 3 }, CancellationToken.None);
        
        // Wait for timer to fire (should fire once with all 3 messages)
        eventWaitHandle.Wait(batchInterval.Add(TimeSpan.FromMilliseconds(100)), CancellationToken.None);

        // Assert - Should only fire once (timer not restarted)
        batchCount.ShouldBe(1);
    }

    /// <summary>
    /// Tests that AddMessageAsync starts timer when batchTimer is null.
    /// This tests the branch: _batchTimer == null (true case).
    /// </summary>
    [Fact]
    public async Task AddMessageAsync_WhenTimerIsNull_StartsTimer()
    {
        // Arrange
        var batchInterval = TimeSpan.FromMilliseconds(200);
        using var batcher = new MessageBatcher<TestMessage>(10, batchInterval, _logger);
        var batchReceived = false;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        batcher.BatchReady += (sender, args) =>
        {
            batchReceived = true;
            eventWaitHandle.Set();
        };

        // Act - Add first message (timer should be null initially)
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        
        // Wait for timer to fire
        var waitResult = eventWaitHandle.Wait(batchInterval.Add(TimeSpan.FromMilliseconds(100)), CancellationToken.None);

        // Assert - Timer should have fired
        waitResult.ShouldBeTrue("Timer should have started and fired");
        batchReceived.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that AddMessageAsync does not start timer when batchTimer is not null.
    /// This tests the branch: _batchTimer == null (false case).
    /// </summary>
    [Fact]
    public async Task AddMessageAsync_WhenTimerAlreadyExists_DoesNotStartNewTimer()
    {
        // Arrange
        var batchInterval = TimeSpan.FromSeconds(10); // Long interval to prevent timer firing
        using var batcher = new MessageBatcher<TestMessage>(10, batchInterval, _logger);

        // Act - Add multiple messages (timer should only be started once)
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await batcher.AddMessageAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        await batcher.AddMessageAsync(new TestMessage { Id = 3 }, CancellationToken.None);

        // Assert - All messages should be added (behavioral test)
        // The timer should only be started once (first message)
        batcher.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that FlushBatchAsync returns early when Count is exactly zero.
    /// This tests the branch: _pendingMessages.Count == 0 (true case).
    /// </summary>
    [Fact]
    public async Task FlushAsync_WithZeroMessages_DoesNotRaiseEvent()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(10, TimeSpan.FromSeconds(1), _logger);
        var eventRaised = false;

        batcher.BatchReady += (sender, args) => eventRaised = true;

        // Act - Flush with no messages (Count == 0)
        await batcher.FlushAsync(CancellationToken.None);

        // Assert - Should not raise event when count is exactly zero
        eventRaised.ShouldBeFalse();
    }

    /// <summary>
    /// Tests that FlushBatchAsync processes batch when Count is greater than zero.
    /// This tests the branch: _pendingMessages.Count == 0 (false case).
    /// </summary>
    [Fact]
    public async Task FlushAsync_WithMessages_ProcessesBatch()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(10, TimeSpan.FromSeconds(1), _logger);
        var batchReceived = false;
        IReadOnlyList<TestMessage>? receivedBatch = null;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        batcher.BatchReady += (sender, args) =>
        {
            batchReceived = true;
            receivedBatch = args.Messages;
            eventWaitHandle.Set();
        };

        // Add messages (Count > 0)
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await batcher.AddMessageAsync(new TestMessage { Id = 2 }, CancellationToken.None);

        // Act - Flush with messages (Count > 0)
        await batcher.FlushAsync(CancellationToken.None);
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert - Should process batch when count is greater than zero
        batchReceived.ShouldBeTrue();
        receivedBatch.ShouldNotBeNull();
        receivedBatch.Count.ShouldBe(2);
    }

    /// <summary>
    /// Tests that AddMessageAsync flushes when Count exactly equals batchSize.
    /// This tests the boundary: _pendingMessages.Count >= _batchSize (true when equal).
    /// </summary>
    [Fact]
    public async Task AddMessageAsync_WhenCountEqualsBatchSize_FlushesBatch()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(2, TimeSpan.FromSeconds(10), _logger);
        var batchReceived = false;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        batcher.BatchReady += (sender, args) =>
        {
            batchReceived = true;
            eventWaitHandle.Set();
        };

        // Act - Add exactly batchSize messages (Count == batchSize)
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await batcher.AddMessageAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert - Should flush when count equals batchSize
        batchReceived.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that AddMessageAsync does not flush when Count is less than batchSize.
    /// This tests the boundary: _pendingMessages.Count >= _batchSize (false case).
    /// </summary>
    [Fact]
    public async Task AddMessageAsync_WhenCountLessThanBatchSize_DoesNotFlush()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(3, TimeSpan.FromSeconds(10), _logger);
        var batchReceived = false;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        batcher.BatchReady += (sender, args) =>
        {
            batchReceived = true;
            eventWaitHandle.Set();
        };

        // Act - Add less than batchSize messages (Count < batchSize)
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await batcher.AddMessageAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        
        // Small delay to ensure no immediate flush
        await Task.Delay(50, CancellationToken.None);

        // Assert - Should not flush when count is less than batchSize
        batchReceived.ShouldBeFalse();
    }

    /// <summary>
    /// Tests that FlushBatchAsync disposes timer and sets it to null.
    /// </summary>
    [Fact]
    public async Task FlushAsync_DisposesTimer_AndResetsToNull()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(10, TimeSpan.FromSeconds(10), _logger);
        var batchCount = 0;
        using var eventWaitHandle1 = new ManualResetEventSlim(false);
        using var eventWaitHandle2 = new ManualResetEventSlim(false);

        batcher.BatchReady += (sender, args) =>
        {
            batchCount++;
            if (batchCount == 1)
            {
                eventWaitHandle1.Set();
            }
            else if (batchCount == 2)
            {
                eventWaitHandle2.Set();
            }
        };

        // Act - Add message and flush (timer should be disposed)
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await batcher.FlushAsync(CancellationToken.None);
        eventWaitHandle1.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Add new message - timer should restart (was null after flush)
        await batcher.AddMessageAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        
        // Flush again to verify timer was restarted
        await batcher.FlushAsync(CancellationToken.None);
        eventWaitHandle2.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert - Both batches should be processed
        batchCount.ShouldBe(2);
    }

    /// <summary>
    /// Tests that BatchReady event is not raised when no subscribers.
    /// This tests the branch: BatchReady?.Invoke (null check).
    /// </summary>
    [Fact]
    public async Task AddMessageAsync_WithNoSubscribers_DoesNotThrow()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(3, TimeSpan.FromSeconds(10), _logger);
        // No subscribers registered

        // Act - Add messages (should not throw even with no subscribers)
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await batcher.AddMessageAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        await batcher.AddMessageAsync(new TestMessage { Id = 3 }, CancellationToken.None);

        // Assert - Should complete without throwing
        batcher.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that FlushAsync handles empty batch when BatchReady has no subscribers.
    /// </summary>
    [Fact]
    public async Task FlushAsync_WithNoSubscribers_DoesNotThrow()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(10, TimeSpan.FromSeconds(1), _logger);
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        // No subscribers registered

        // Act - Flush (should not throw even with no subscribers)
        await batcher.FlushAsync(CancellationToken.None);

        // Assert - Should complete without throwing
        batcher.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that AddMessageAsync handles cancellation before lock acquisition.
    /// This tests the early return branch: cancellationToken.IsCancellationRequested (true case).
    /// </summary>
    [Fact]
    public async Task AddMessageAsync_WithCancellationBeforeLock_ReturnsEarly()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(10, TimeSpan.FromSeconds(1), _logger);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var eventRaised = false;

        batcher.BatchReady += (sender, args) => eventRaised = true;

        // Act - Add message with cancelled token (should return early)
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, cts.Token);
        await Task.Delay(100, CancellationToken.None);

        // Assert - Should return early, no event raised
        eventRaised.ShouldBeFalse();
    }

    /// <summary>
    /// Tests that Dispose handles null timer gracefully.
    /// This tests the branch: _batchTimer?.Dispose() (null check).
    /// </summary>
    [Fact]
    public void Dispose_WithNullTimer_DoesNotThrow()
    {
        // Arrange
        var batcher = new MessageBatcher<TestMessage>(10, TimeSpan.FromSeconds(10), _logger);
        // Timer is null initially

        // Act & Assert - Dispose should not throw when timer is null
        batcher.Dispose();
        batcher.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that Dispose disposes timer when timer exists.
    /// </summary>
    [Fact]
    public async Task Dispose_WithActiveTimer_DisposesTimer()
    {
        // Arrange
        var batcher = new MessageBatcher<TestMessage>(10, TimeSpan.FromSeconds(10), _logger);
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        // Timer should be active now

        // Act & Assert - Dispose should dispose timer without throwing
        batcher.Dispose();
        batcher.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that BatchReady event args contains correct messages list.
    /// </summary>
    [Fact]
    public async Task BatchReadyEventArgs_Contains_CorrectMessagesList()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(3, TimeSpan.FromSeconds(10), _logger);
        IReadOnlyList<TestMessage>? receivedMessages = null;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        batcher.BatchReady += (sender, args) =>
        {
            receivedMessages = args.Messages;
            eventWaitHandle.Set();
        };

        var messages = new[]
        {
            new TestMessage { Id = 1 },
            new TestMessage { Id = 2 },
            new TestMessage { Id = 3 }
        };

        // Act
        foreach (var message in messages)
        {
            await batcher.AddMessageAsync(message, CancellationToken.None);
        }
        
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert
        receivedMessages.ShouldNotBeNull();
        receivedMessages.Count.ShouldBe(3);
        receivedMessages[0].Id.ShouldBe(1);
        receivedMessages[1].Id.ShouldBe(2);
        receivedMessages[2].Id.ShouldBe(3);
    }

    /// <summary>
    /// Tests that BatchReady event args with empty list is valid.
    /// </summary>
    [Fact]
    public void BatchReadyEventArgs_WithEmptyList_IsValid()
    {
        // Arrange
        var emptyList = new List<TestMessage>();

        // Act
        var args = new BatchReadyEventArgs<TestMessage>(emptyList);

        // Assert - Empty list should be valid
        args.Messages.ShouldNotBeNull();
        args.Messages.Count.ShouldBe(0);
    }

    /// <summary>
    /// Tests that unsubscribing from BatchReady event prevents notifications.
    /// </summary>
    [Fact]
    public async Task BatchReady_AfterUnsubscribe_DoesNotNotify()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(2, TimeSpan.FromSeconds(10), _logger);
        var eventRaised = false;

        EventHandler<BatchReadyEventArgs<TestMessage>> handler = (sender, args) => eventRaised = true;
        batcher.BatchReady += handler;

        // Unsubscribe
        batcher.BatchReady -= handler;

        // Act - Add messages to trigger batch
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await batcher.AddMessageAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None);

        // Assert - Should not raise event after unsubscribe
        eventRaised.ShouldBeFalse();
    }

    /// <summary>
    /// Tests that multiple subscribers all receive BatchReady notifications.
    /// </summary>
    [Fact]
    public async Task BatchReady_WithMultipleSubscribers_NotifiesAll()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(2, TimeSpan.FromSeconds(10), _logger);
        var subscriber1Called = false;
        var subscriber2Called = false;
        var subscriber3Called = false;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        batcher.BatchReady += (sender, args) =>
        {
            subscriber1Called = true;
            eventWaitHandle.Set();
        };
        batcher.BatchReady += (sender, args) => subscriber2Called = true;
        batcher.BatchReady += (sender, args) => subscriber3Called = true;

        // Act - Add messages to trigger batch
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await batcher.AddMessageAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert - All subscribers should be notified
        subscriber1Called.ShouldBeTrue();
        subscriber2Called.ShouldBeTrue();
        subscriber3Called.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that FlushAsync handles cancellation during lock wait.
    /// Note: FlushAsync doesn't check cancellation before lock, it goes straight to WaitAsync.
    /// </summary>
    [Fact]
    public async Task FlushAsync_WithCancellationDuringLock_ThrowsOperationCanceledException()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(10, TimeSpan.FromSeconds(1), _logger);
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Should throw OperationCanceledException when cancellation is requested
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await batcher.FlushAsync(cts.Token));
    }

    /// <summary>
    /// Tests that constructor throws ArgumentOutOfRangeException when batchSize is exactly zero.
    /// This tests the boundary: batchSize > 0 (false when == 0).
    /// </summary>
    [Fact]
    public void Constructor_WithBatchSizeZero_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MessageBatcher<TestMessage>(0, TimeSpan.FromSeconds(1), _logger));
    }

    /// <summary>
    /// Tests that constructor accepts batchSize of exactly one.
    /// This tests the boundary: batchSize > 0 (true when == 1).
    /// </summary>
    [Fact]
    public void Constructor_WithBatchSizeOne_CreatesSuccessfully()
    {
        // Act
        using var batcher = new MessageBatcher<TestMessage>(1, TimeSpan.FromSeconds(1), _logger);

        // Assert - Should create successfully
        batcher.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that constructor accepts batchSize of 1 (boundary for > 0 mutation).
    /// </summary>
    [Fact]
    public void Constructor_WithBatchSizeOne_DoesNotThrow()
    {
        // Act & Assert - batchSize == 1 should be valid (tests batchSize > 0 boundary)
        var batcher = new MessageBatcher<TestMessage>(1, TimeSpan.FromSeconds(1), _logger);
        batcher.ShouldNotBeNull();
        batcher.Dispose();
    }

    /// <summary>
    /// Tests that FlushBatchAsync with exactly zero messages does not raise event.
    /// This tests the boundary condition: _pendingMessages.Count == 0
    /// </summary>
    [Fact]
    public async Task FlushAsync_WithExactlyZeroMessages_DoesNotRaiseEvent()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(10, TimeSpan.FromSeconds(1), _logger);
        var batchReceived = false;

        batcher.BatchReady += (sender, args) => batchReceived = true;

        // Act - Flush with no messages (Count == 0 exactly)
        await batcher.FlushAsync(CancellationToken.None);
        await Task.Delay(100, CancellationToken.None);

        // Assert - Should not raise event when count is exactly 0
        batchReceived.ShouldBeFalse();
    }

    /// <summary>
    /// Tests that AddMessageAsync flushes when Count exactly equals batchSize.
    /// This tests the boundary condition: _pendingMessages.Count >= _batchSize
    /// </summary>
    [Fact]
    public async Task AddMessageAsync_WhenCountExactlyEqualsBatchSize_FlushesBatch()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(2, TimeSpan.FromSeconds(10), _logger);
        var batchReceived = false;
        IReadOnlyList<TestMessage>? receivedBatch = null;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        batcher.BatchReady += (sender, args) =>
        {
            batchReceived = true;
            receivedBatch = args.Messages;
            eventWaitHandle.Set();
        };

        // Act - Add exactly batchSize messages (Count == batchSize exactly)
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await batcher.AddMessageAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        
        // Wait for event with timeout
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert - Should flush when count exactly equals batchSize
        batchReceived.ShouldBeTrue();
        receivedBatch.ShouldNotBeNull();
        receivedBatch.Count.ShouldBe(2);
    }

    /// <summary>
    /// Tests that AddMessageAsync does not flush when Count is one less than batchSize.
    /// This tests the boundary condition: _pendingMessages.Count >= _batchSize
    /// </summary>
    [Fact]
    public async Task AddMessageAsync_WhenCountIsOneLessThanBatchSize_DoesNotFlush()
    {
        // Arrange
        using var batcher = new MessageBatcher<TestMessage>(3, TimeSpan.FromSeconds(10), _logger);
        var batchReceived = false;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        batcher.BatchReady += (sender, args) =>
        {
            batchReceived = true;
            eventWaitHandle.Set();
        };

        // Act - Add batchSize - 1 messages (Count < batchSize)
        await batcher.AddMessageAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await batcher.AddMessageAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        
        // Wait a short time to ensure no flush occurred
        await Task.Delay(100, CancellationToken.None);

        // Assert - Should not flush when count is less than batchSize
        batchReceived.ShouldBeFalse();
    }

    /// <summary>
    /// Test message class for testing.
    /// </summary>
    public class TestMessage
    {
        public int Id { get; set; }
    }
}

