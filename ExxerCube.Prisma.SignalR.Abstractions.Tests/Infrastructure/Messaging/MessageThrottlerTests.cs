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

        throttler.MessageReady += (sender, args) =>
        {
            messageReceived = true;
            messageReceivedCount++;
        };

        // Act - First message will be sent immediately (no previous send)
        await throttler.ThrottleAsync(new TestMessage { Id = 1 }, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None); // Wait for first message
        
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
    /// Test message class for testing.
    /// </summary>
    public class TestMessage
    {
        public int Id { get; set; }
    }
}

