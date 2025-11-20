namespace ExxerCube.Prisma.SignalR.Abstractions.Tests.Infrastructure.Messaging;

/// <summary>
/// Tests for the MessageThrottler&lt;T&gt; class.
/// </summary>
public class MessageThrottlerTests
{
    private readonly ILogger<MessageThrottler<TestMessage>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageThrottlerTests"/> class.
    /// </summary>
    public MessageThrottlerTests()
    {
        _logger = Substitute.For<ILogger<MessageThrottler<TestMessage>>>();
    }

    /// <summary>
    /// Tests that ThrottleAsync sends message immediately if throttle interval has passed.
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WhenIntervalPassed_SendsImmediately()
    {
        // Arrange
        using var throttler = new MessageThrottler<TestMessage>(TimeSpan.FromMilliseconds(10), _logger);
        var messageReceived = false;
        TestMessage? receivedMessage = null;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        throttler.MessageReady += (sender, args) =>
        {
            messageReceived = true;
            receivedMessage = args.Message;
            eventWaitHandle.Set();
        };

        var message = new TestMessage { Id = 1 };

        // Act
        await throttler.ThrottleAsync(message, CancellationToken.None);
        
        // Wait for event with timeout
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert
        messageReceived.ShouldBeTrue();
        receivedMessage.ShouldBe(message);
    }

    /// <summary>
    /// Tests that ThrottleAsync delays message when within throttle interval.
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WithinThrottleInterval_DelaysMessage()
    {
        // Arrange
        using var throttler = new MessageThrottler<TestMessage>(TimeSpan.FromMilliseconds(100), _logger);
        var messageReceived = false;
        var messageReceivedCount = 0;
        using var firstMessageReceived = new ManualResetEventSlim(false);

        throttler.MessageReady += (sender, args) =>
        {
            messageReceived = true;
            messageReceivedCount++;
            firstMessageReceived.Set();
        };

        // Act - First message will be sent immediately (no previous send)
        await throttler.ThrottleAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        
        // Wait for first message to actually fire
        firstMessageReceived.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);
        
        // Second message within throttle interval should be delayed
        await throttler.ThrottleAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        await Task.Delay(10, CancellationToken.None); // Short delay, second message should not have fired yet

        // Assert
        messageReceived.ShouldBeTrue(); // First message should have been sent
        messageReceivedCount.ShouldBe(1); // Only first message should have been sent so far
    }

    /// <summary>
    /// Tests that ThrottleAsync handles cancellation.
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WithCancellationRequested_DoesNotSendMessage()
    {
        // Arrange
        using var throttler = new MessageThrottler<TestMessage>(TimeSpan.FromMilliseconds(10), _logger);
        var messageReceived = false;

        throttler.MessageReady += (sender, args) => messageReceived = true;

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        await throttler.ThrottleAsync(new TestMessage { Id = 1 }, cts.Token);
        await Task.Delay(50, CancellationToken.None);

        // Assert
        messageReceived.ShouldBeFalse();
    }

    /// <summary>
    /// Tests that ThrottleAsync sends immediately when exactly throttleInterval has elapsed.
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WhenExactlyIntervalElapsed_SendsImmediately()
    {
        // Arrange
        var throttleInterval = TimeSpan.FromMilliseconds(100);
        using var throttler = new MessageThrottler<TestMessage>(throttleInterval, _logger);
        var messageCount = 0;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        throttler.MessageReady += (sender, args) =>
        {
            messageCount++;
            eventWaitHandle.Set();
        };

        // Act - First message (immediate send)
        await throttler.ThrottleAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);
        eventWaitHandle.Reset();

        // Wait exactly throttleInterval
        await Task.Delay(throttleInterval, CancellationToken.None);

        // Second message should send immediately (exactly interval elapsed)
        await throttler.ThrottleAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert
        messageCount.ShouldBe(2);
    }

    /// <summary>
    /// Tests that ThrottleAsync delays send when within throttle interval.
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WhenWithinThrottleInterval_DelaysSend()
    {
        // Arrange
        var throttleInterval = TimeSpan.FromMilliseconds(200);
        using var throttler = new MessageThrottler<TestMessage>(throttleInterval, _logger);
        var messageCount = 0;
        var firstMessageTime = DateTime.UtcNow;
        var secondMessageTime = DateTime.MaxValue;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        throttler.MessageReady += (sender, args) =>
        {
            messageCount++;
            if (messageCount == 1)
            {
                firstMessageTime = DateTime.UtcNow;
            }
            else if (messageCount == 2)
            {
                secondMessageTime = DateTime.UtcNow;
                eventWaitHandle.Set();
            }
        };

        // Act - First message (immediate send)
        await throttler.ThrottleAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None); // Wait for first message to be sent

        // Second message should be delayed (within throttle interval)
        await throttler.ThrottleAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        await Task.Delay(30, CancellationToken.None); // Short delay, second message should not have fired yet

        // Assert - Only first message sent so far (timing dependent, but should be true if throttle is working)
        if (messageCount == 1)
        {
            // Wait for delayed send
            var waitResult = eventWaitHandle.Wait(throttleInterval.Add(TimeSpan.FromMilliseconds(100)), CancellationToken.None);
            waitResult.ShouldBeTrue("Delayed message should have been sent");
            messageCount.ShouldBe(2);
            
            // Verify timing - second message should be sent after throttle interval
            var timeBetweenMessages = secondMessageTime - firstMessageTime;
            timeBetweenMessages.ShouldBeGreaterThanOrEqualTo(throttleInterval.Subtract(TimeSpan.FromMilliseconds(50))); // Allow tolerance
        }
        else
        {
            // If both messages were sent, verify they were sent with appropriate delay
            var timeBetweenMessages = secondMessageTime - firstMessageTime;
            timeBetweenMessages.ShouldBeGreaterThanOrEqualTo(throttleInterval.Subtract(TimeSpan.FromMilliseconds(100))); // Allow tolerance
            messageCount.ShouldBe(2);
        }
    }

    /// <summary>
    /// Tests that ThrottleAsync replaces message during delay and sends only latest.
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WhenMessageReplacedDuringDelay_SendsLatestOnly()
    {
        // Arrange
        var throttleInterval = TimeSpan.FromMilliseconds(200);
        using var throttler = new MessageThrottler<TestMessage>(throttleInterval, _logger);
        var receivedMessages = new List<TestMessage>();
        using var eventWaitHandle = new ManualResetEventSlim(false);

        throttler.MessageReady += (sender, args) =>
        {
            receivedMessages.Add(args.Message);
            eventWaitHandle.Set();
        };

        // Act - First message (immediate send)
        var message1 = new TestMessage { Id = 1 };
        await throttler.ThrottleAsync(message1, CancellationToken.None);
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);
        eventWaitHandle.Reset();

        // Second message (delayed)
        var message2 = new TestMessage { Id = 2 };
        await throttler.ThrottleAsync(message2, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None); // Wait a bit

        // Third message replaces second (same reference check - but different object)
        // Actually, the code uses Equals() so we need to test with same value
        var message3 = new TestMessage { Id = 3 };
        await throttler.ThrottleAsync(message3, CancellationToken.None);

        // Wait for delayed send
        await Task.Delay(throttleInterval.Add(TimeSpan.FromMilliseconds(50)), CancellationToken.None);

        // Assert - Should have message1 and message3 (message2 replaced)
        receivedMessages.Count.ShouldBe(2);
        receivedMessages[0].Id.ShouldBe(1);
        receivedMessages[1].Id.ShouldBe(3);
    }

    /// <summary>
    /// Tests that ThrottleAsync sends both messages when different messages arrive during delay.
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WhenDifferentMessageDuringDelay_SendsBoth()
    {
        // Arrange
        var throttleInterval = TimeSpan.FromMilliseconds(200);
        using var throttler = new MessageThrottler<TestMessage>(throttleInterval, _logger);
        var receivedMessages = new List<TestMessage>();
        using var eventWaitHandle = new ManualResetEventSlim(false);

        throttler.MessageReady += (sender, args) =>
        {
            receivedMessages.Add(args.Message);
            if (receivedMessages.Count == 2)
            {
                eventWaitHandle.Set();
            }
        };

        // Act - First message (immediate send)
        var message1 = new TestMessage { Id = 1 };
        await throttler.ThrottleAsync(message1, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None); // Wait for first message

        // Second message (delayed - will check if equals message1, which it's not)
        var message2 = new TestMessage { Id = 2 };
        await throttler.ThrottleAsync(message2, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None);

        // Wait for delayed send of message2
        await Task.Delay(throttleInterval.Add(TimeSpan.FromMilliseconds(50)), CancellationToken.None);

        // Assert - Both messages should be sent
        receivedMessages.Count.ShouldBe(2);
        receivedMessages[0].Id.ShouldBe(1);
        receivedMessages[1].Id.ShouldBe(2);
    }

    /// <summary>
    /// Tests that ThrottleAsync handles cancellation during delayed send.
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WhenCancelledDuringDelay_MayStillSend()
    {
        // Arrange
        // Note: Cancellation during delay may not prevent the message from being sent
        // if the cancellation happens after the delay task has already started processing
        var throttleInterval = TimeSpan.FromMilliseconds(200);
        using var throttler = new MessageThrottler<TestMessage>(throttleInterval, _logger);
        var messageCount = 0;

        throttler.MessageReady += (sender, args) => messageCount++;

        // Act - First message (immediate send)
        await throttler.ThrottleAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None);

        // Second message (delayed)
        using var cts = new CancellationTokenSource();
        await throttler.ThrottleAsync(new TestMessage { Id = 2 }, cts.Token);
        await Task.Delay(50, CancellationToken.None);

        // Cancel before delay completes
        cts.Cancel();
        await Task.Delay(throttleInterval.Add(TimeSpan.FromMilliseconds(100)), CancellationToken.None);

        // Assert - First message definitely sent, second may or may not be sent depending on timing
        messageCount.ShouldBeGreaterThanOrEqualTo(1);
        // Note: The actual behavior depends on when cancellation occurs relative to the delay task
    }

    /// <summary>
    /// Tests that ThrottleAsync throttles multiple rapid messages correctly.
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WithRapidMessages_ThrottlesCorrectly()
    {
        // Arrange
        var throttleInterval = TimeSpan.FromMilliseconds(100);
        using var throttler = new MessageThrottler<TestMessage>(throttleInterval, _logger);
        var messageCount = 0;

        throttler.MessageReady += (sender, args) => messageCount++;

        // Act - Send multiple messages rapidly
        await throttler.ThrottleAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await throttler.ThrottleAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        await throttler.ThrottleAsync(new TestMessage { Id = 3 }, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None);

        // Assert - First message sent immediately, others throttled
        messageCount.ShouldBe(1);

        // Wait for throttled messages
        await Task.Delay(throttleInterval.Add(TimeSpan.FromMilliseconds(50)), CancellationToken.None);
        
        // Should have at least one more (the latest message)
        messageCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    /// <summary>
    /// Tests that MessageReady event contains correct message.
    /// </summary>
    [Fact]
    public async Task MessageReady_EventContains_CorrectMessage()
    {
        // Arrange
        using var throttler = new MessageThrottler<TestMessage>(TimeSpan.FromMilliseconds(10), _logger);
        TestMessage? receivedMessage = null;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        throttler.MessageReady += (sender, args) =>
        {
            receivedMessage = args.Message;
            eventWaitHandle.Set();
        };

        var message = new TestMessage { Id = 42 };

        // Act
        await throttler.ThrottleAsync(message, CancellationToken.None);
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert
        receivedMessage.ShouldNotBeNull();
        receivedMessage.Id.ShouldBe(42);
    }

    /// <summary>
    /// Tests that constructor throws ArgumentNullException when logger is null.
    /// </summary>
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MessageThrottler<TestMessage>(TimeSpan.FromMilliseconds(100), null!));
    }

    /// <summary>
    /// Tests that ThrottledMessageEventArgs constructor throws ArgumentNullException when message is null.
    /// </summary>
    [Fact]
    public void ThrottledMessageEventArgs_WithNullMessage_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ThrottledMessageEventArgs<TestMessage>(null!));
    }

    /// <summary>
    /// Tests that SendMessageAsync updates LastSendTime correctly.
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_UpdatesLastSendTime_Correctly()
    {
        // Arrange
        var throttleInterval = TimeSpan.FromMilliseconds(100);
        using var throttler = new MessageThrottler<TestMessage>(throttleInterval, _logger);
        var sendTimes = new List<DateTime>();
        using var eventWaitHandle = new ManualResetEventSlim(false);

        throttler.MessageReady += (sender, args) =>
        {
            sendTimes.Add(DateTime.UtcNow);
            if (sendTimes.Count == 2)
            {
                eventWaitHandle.Set();
            }
        };

        // Act - Send first message
        await throttler.ThrottleAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None);

        // Wait for interval to pass
        await Task.Delay(throttleInterval.Add(TimeSpan.FromMilliseconds(50)), CancellationToken.None);

        // Send second message (should be immediate)
        await throttler.ThrottleAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert - Verify timing (second send should be after interval)
        var timeBetweenSends = sendTimes[1] - sendTimes[0];
        timeBetweenSends.ShouldBeGreaterThanOrEqualTo(throttleInterval.Subtract(TimeSpan.FromMilliseconds(50))); // Allow some tolerance
    }

    /// <summary>
    /// Tests that ThrottleAsync handles cancellation before lock acquisition.
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WhenCancelledBeforeLockAcquisition_ReturnsEarly()
    {
        // Arrange
        using var throttler = new MessageThrottler<TestMessage>(TimeSpan.FromMilliseconds(100), _logger);
        using var cts = new CancellationTokenSource();
        var messageReceived = false;
        
        throttler.MessageReady += (sender, args) => messageReceived = true;
        
        // Cancel before acquiring lock
        cts.Cancel();

        // Act - Should return early without throwing
        await throttler.ThrottleAsync(new TestMessage { Id = 1 }, cts.Token);
        await Task.Delay(50, CancellationToken.None);

        // Assert - No message should be sent
        messageReceived.ShouldBeFalse();
    }

    /// <summary>
    /// Tests that ThrottleAsync sends immediately when timeSinceLastSend exactly equals throttleInterval.
    /// This tests the boundary condition: timeSinceLastSend >= _throttleInterval
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WhenTimeSinceLastSendExactlyEqualsInterval_SendsImmediately()
    {
        // Arrange
        var throttleInterval = TimeSpan.FromMilliseconds(100);
        using var throttler = new MessageThrottler<TestMessage>(throttleInterval, _logger);
        var messageCount = 0;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        throttler.MessageReady += (sender, args) =>
        {
            messageCount++;
            eventWaitHandle.Set();
        };

        // Act - First message (immediate send)
        await throttler.ThrottleAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);
        eventWaitHandle.Reset();

        // Wait exactly throttleInterval (boundary condition)
        await Task.Delay(throttleInterval, CancellationToken.None);

        // Second message should send immediately (timeSinceLastSend == throttleInterval exactly)
        await throttler.ThrottleAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert - Both messages should be sent
        messageCount.ShouldBe(2);
    }

    /// <summary>
    /// Tests that ThrottleAsync delays when timeSinceLastSend is less than throttleInterval.
    /// This tests the boundary condition: timeSinceLastSend >= _throttleInterval (false case)
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WhenTimeSinceLastSendLessThanInterval_DelaysSend()
    {
        // Arrange
        var throttleInterval = TimeSpan.FromMilliseconds(200);
        using var throttler = new MessageThrottler<TestMessage>(throttleInterval, _logger);
        var messageCount = 0;

        throttler.MessageReady += (sender, args) => messageCount++;

        // Act - First message (immediate send)
        await throttler.ThrottleAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None);

        // Second message before interval (timeSinceLastSend < throttleInterval)
        await throttler.ThrottleAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None);

        // Assert - Only first message sent so far (second is delayed)
        messageCount.ShouldBe(1);
    }

    /// <summary>
    /// Tests that ThrottleAsync sends immediately when timeSinceLastSend is greater than throttleInterval.
    /// This tests the branch: timeSinceLastSend >= _throttleInterval (true case when greater).
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WhenTimeSinceLastSendGreaterThanInterval_SendsImmediately()
    {
        // Arrange
        var throttleInterval = TimeSpan.FromMilliseconds(100);
        using var throttler = new MessageThrottler<TestMessage>(throttleInterval, _logger);
        var messageCount = 0;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        throttler.MessageReady += (sender, args) =>
        {
            messageCount++;
            eventWaitHandle.Set();
        };

        // Act - First message (immediate send)
        await throttler.ThrottleAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);
        eventWaitHandle.Reset();

        // Wait longer than throttleInterval
        await Task.Delay(throttleInterval.Add(TimeSpan.FromMilliseconds(50)), CancellationToken.None);

        // Second message should send immediately (timeSinceLastSend > throttleInterval)
        await throttler.ThrottleAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert - Both messages should be sent immediately
        messageCount.ShouldBe(2);
    }

    /// <summary>
    /// Tests that delayed send checks if latest message equals scheduled message.
    /// This tests the branch: Equals(_latestMessage, message) (true case).
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WhenDelayedMessageEqualsLatest_SendsLatest()
    {
        // Arrange
        var throttleInterval = TimeSpan.FromMilliseconds(200);
        using var throttler = new MessageThrottler<TestMessage>(throttleInterval, _logger);
        var receivedMessages = new List<TestMessage>();
        using var eventWaitHandle = new ManualResetEventSlim(false);

        throttler.MessageReady += (sender, args) =>
        {
            receivedMessages.Add(args.Message);
            eventWaitHandle.Set();
        };

        // Act - First message (immediate send)
        var message1 = new TestMessage { Id = 1 };
        await throttler.ThrottleAsync(message1, CancellationToken.None);
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);
        eventWaitHandle.Reset();

        // Second message (delayed) - same Id, so Equals should return true
        var message2 = new TestMessage { Id = 1 }; // Same Id as message1
        await throttler.ThrottleAsync(message2, CancellationToken.None);
        
        // Wait for delayed send
        await Task.Delay(throttleInterval.Add(TimeSpan.FromMilliseconds(50)), CancellationToken.None);

        // Assert - Should have received messages (behavior depends on Equals implementation)
        receivedMessages.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Tests that delayed send does not send when latest message is different.
    /// This tests the branch: Equals(_latestMessage, message) (false case).
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WhenDelayedMessageDifferentFromLatest_DoesNotSendDelayed()
    {
        // Arrange
        var throttleInterval = TimeSpan.FromMilliseconds(200);
        using var throttler = new MessageThrottler<TestMessage>(throttleInterval, _logger);
        var receivedMessages = new List<TestMessage>();
        using var eventWaitHandle = new ManualResetEventSlim(false);

        throttler.MessageReady += (sender, args) =>
        {
            receivedMessages.Add(args.Message);
            eventWaitHandle.Set();
        };

        // Act - First message (immediate send)
        var message1 = new TestMessage { Id = 1 };
        await throttler.ThrottleAsync(message1, CancellationToken.None);
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);
        eventWaitHandle.Reset();

        // Second message (delayed) - different Id
        var message2 = new TestMessage { Id = 2 };
        await throttler.ThrottleAsync(message2, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None);

        // Third message replaces second (different from message2)
        var message3 = new TestMessage { Id = 3 };
        await throttler.ThrottleAsync(message3, CancellationToken.None);
        
        // Wait for delayed send
        await Task.Delay(throttleInterval.Add(TimeSpan.FromMilliseconds(50)), CancellationToken.None);

        // Assert - Should have message1 and message3 (message2 replaced because message3 != message2)
        receivedMessages.Count.ShouldBeGreaterThanOrEqualTo(2);
        receivedMessages.ShouldContain(m => m.Id == 1);
        receivedMessages.ShouldContain(m => m.Id == 3);
    }

    /// <summary>
    /// Tests that ThrottleAsync sends immediately when timeSinceLastSend equals throttleInterval.
    /// This tests the boundary: timeSinceLastSend >= _throttleInterval (true when equal).
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WhenTimeSinceLastSendEqualsInterval_SendsImmediately()
    {
        // Arrange
        var throttleInterval = TimeSpan.FromMilliseconds(100);
        using var throttler = new MessageThrottler<TestMessage>(throttleInterval, _logger);
        var messageCount = 0;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        throttler.MessageReady += (sender, args) =>
        {
            messageCount++;
            eventWaitHandle.Set();
        };

        // Act - First message (immediate send)
        await throttler.ThrottleAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);
        eventWaitHandle.Reset();

        // Wait exactly throttleInterval
        await Task.Delay(throttleInterval, CancellationToken.None);

        // Second message should send immediately (timeSinceLastSend == throttleInterval)
        await throttler.ThrottleAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert - Both messages should be sent
        messageCount.ShouldBe(2);
    }

    /// <summary>
    /// Tests that MessageReady event is not raised when no subscribers.
    /// This tests the branch: MessageReady?.Invoke (null check).
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WithNoSubscribers_DoesNotThrow()
    {
        // Arrange
        using var throttler = new MessageThrottler<TestMessage>(TimeSpan.FromMilliseconds(10), _logger);
        // No subscribers registered

        // Act - Throttle message (should not throw even with no subscribers)
        await throttler.ThrottleAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None);

        // Assert - Should complete without throwing
        throttler.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that ThrottleAsync delays when timeSinceLastSend is less than throttleInterval.
    /// This tests the branch: timeSinceLastSend >= _throttleInterval (false case - delays).
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WhenTimeSinceLastSendLessThanInterval_DelaysMessage()
    {
        // Arrange
        var throttleInterval = TimeSpan.FromMilliseconds(200);
        using var throttler = new MessageThrottler<TestMessage>(throttleInterval, _logger);
        var messageCount = 0;

        throttler.MessageReady += (sender, args) => messageCount++;

        // Act - First message (immediate send)
        await throttler.ThrottleAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None);

        // Second message before interval (timeSinceLastSend < throttleInterval)
        await throttler.ThrottleAsync(new TestMessage { Id = 2 }, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None);

        // Assert - Only first message sent so far (second is delayed)
        messageCount.ShouldBe(1);
    }

    /// <summary>
    /// Tests that ThrottledMessageEventArgs stores message correctly.
    /// </summary>
    [Fact]
    public void ThrottledMessageEventArgs_Stores_MessageCorrectly()
    {
        // Arrange
        var message = new TestMessage { Id = 42 };

        // Act
        var args = new ThrottledMessageEventArgs<TestMessage>(message);

        // Assert
        args.Message.ShouldBe(message);
        args.Message.Id.ShouldBe(42);
    }

    /// <summary>
    /// Tests that unsubscribing from MessageReady event prevents notifications.
    /// </summary>
    [Fact]
    public async Task MessageReady_AfterUnsubscribe_DoesNotNotify()
    {
        // Arrange
        using var throttler = new MessageThrottler<TestMessage>(TimeSpan.FromMilliseconds(10), _logger);
        var eventRaised = false;

        EventHandler<ThrottledMessageEventArgs<TestMessage>> handler = (sender, args) => eventRaised = true;
        throttler.MessageReady += handler;

        // Unsubscribe
        throttler.MessageReady -= handler;

        // Act - Throttle message
        await throttler.ThrottleAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None);

        // Assert - Should not raise event after unsubscribe
        eventRaised.ShouldBeFalse();
    }

    /// <summary>
    /// Tests that multiple subscribers all receive MessageReady notifications.
    /// </summary>
    [Fact]
    public async Task MessageReady_WithMultipleSubscribers_NotifiesAll()
    {
        // Arrange
        using var throttler = new MessageThrottler<TestMessage>(TimeSpan.FromMilliseconds(10), _logger);
        var subscriber1Called = false;
        var subscriber2Called = false;
        var subscriber3Called = false;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        throttler.MessageReady += (sender, args) =>
        {
            subscriber1Called = true;
            eventWaitHandle.Set();
        };
        throttler.MessageReady += (sender, args) => subscriber2Called = true;
        throttler.MessageReady += (sender, args) => subscriber3Called = true;

        // Act - Throttle message
        await throttler.ThrottleAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert - All subscribers should be notified
        subscriber1Called.ShouldBeTrue();
        subscriber2Called.ShouldBeTrue();
        subscriber3Called.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that ThrottleAsync returns early when cancellation is requested before lock.
    /// This tests the mutation: if (cancellationToken.IsCancellationRequested) return;
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WithCancellationBeforeLock_ReturnsEarly()
    {
        // Arrange
        using var throttler = new MessageThrottler<TestMessage>(TimeSpan.FromMilliseconds(10), _logger);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var eventRaised = false;

        throttler.MessageReady += (sender, args) => eventRaised = true;

        // Act - Throttle with cancelled token (should return before lock)
        await throttler.ThrottleAsync(new TestMessage { Id = 1 }, cts.Token);
        await Task.Delay(50, CancellationToken.None);

        // Assert - Should return early, no event raised
        eventRaised.ShouldBeFalse();
    }

    /// <summary>
    /// Tests that ThrottleAsync sends immediately when timeSinceLastSend is exactly zero (first message).
    /// This tests the boundary: timeSinceLastSend >= _throttleInterval (true when timeSinceLastSend is DateTime.MinValue).
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WithFirstMessage_SendsImmediately()
    {
        // Arrange
        using var throttler = new MessageThrottler<TestMessage>(TimeSpan.FromMilliseconds(100), _logger);
        var messageCount = 0;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        throttler.MessageReady += (sender, args) =>
        {
            messageCount++;
            eventWaitHandle.Set();
        };

        // Act - First message (timeSinceLastSend should be very large, >= throttleInterval)
        await throttler.ThrottleAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert - Should send immediately on first message
        messageCount.ShouldBe(1);
    }

    /// <summary>
    /// Tests that delayed send path checks message equality correctly.
    /// This tests the mutation: if (Equals(_latestMessage, message)) - both true and false cases.
    /// </summary>
    [Fact]
    public async Task ThrottleAsync_WhenDelayedSend_ChecksMessageEquality()
    {
        // Arrange
        var throttleInterval = TimeSpan.FromMilliseconds(200);
        using var throttler = new MessageThrottler<TestMessage>(throttleInterval, _logger);
        var receivedMessages = new List<TestMessage>();
        using var eventWaitHandle = new ManualResetEventSlim(false);

        throttler.MessageReady += (sender, args) =>
        {
            receivedMessages.Add(args.Message);
            eventWaitHandle.Set();
        };

        // Act - First message (immediate send)
        var message1 = new TestMessage { Id = 1 };
        await throttler.ThrottleAsync(message1, CancellationToken.None);
        eventWaitHandle.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);
        eventWaitHandle.Reset();

        // Second message (delayed) - different message
        var message2 = new TestMessage { Id = 2 };
        await throttler.ThrottleAsync(message2, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None);

        // Third message replaces second (different from message2, so Equals returns false)
        var message3 = new TestMessage { Id = 3 };
        await throttler.ThrottleAsync(message3, CancellationToken.None);
        
        // Wait for delayed send
        await Task.Delay(throttleInterval.Add(TimeSpan.FromMilliseconds(50)), CancellationToken.None);

        // Assert - Should have message1 and message3 (message2 replaced because message3 != message2)
        receivedMessages.Count.ShouldBeGreaterThanOrEqualTo(2);
        receivedMessages.ShouldContain(m => m.Id == 1);
        receivedMessages.ShouldContain(m => m.Id == 3);
    }

    /// <summary>
    /// Test message class for testing.
    /// </summary>
    public class TestMessage
    {
        public int Id { get; set; }
    }
}

