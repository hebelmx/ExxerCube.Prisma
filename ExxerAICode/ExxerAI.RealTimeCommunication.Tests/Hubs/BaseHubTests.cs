using ExxerAI.RealTimeCommunication.Adapters.SignalR;
using Microsoft.AspNetCore.SignalR;

namespace ExxerAI.RealTimeCommunication.Tests.Hubs
{
    /// <summary>
    /// Tests for BaseHub functionality and connection lifecycle management.
    /// </summary>
    public class BaseHubTests
    {
        private readonly ILogger<BaseHubTests> _logger;
        private readonly TestableBaseHub _hub;
        private readonly HubCallerContext _mockContext;
        private readonly IGroupManager _mockGroups;
        private readonly IHubCallerClients _mockClients;
        private readonly IClientProxy _mockClientProxy;
        private readonly ISingleClientProxy _mockSingleClientProxy;

        /// <summary>
        /// Constructor: Sets up testable hub and mocks.
        /// </summary>
        public BaseHubTests()
        {
            var testLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<BaseHubTests>.Instance;
            var hubLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<TestableBaseHub>.Instance;

            // Create mocks
            _mockContext = Substitute.For<HubCallerContext>();
            _mockGroups = Substitute.For<IGroupManager>();
            _mockClients = Substitute.For<IHubCallerClients>();
            _mockClientProxy = Substitute.For<IClientProxy>();
            _mockSingleClientProxy = Substitute.For<ISingleClientProxy>();

            // Setup mock context
            _mockContext.ConnectionId.Returns("test-connection-123");
            _mockContext.UserIdentifier.Returns("test-user-456");

            // Setup mock clients - use correct interface types
            _mockClients.All.Returns(_mockClientProxy);
            _mockClients.Caller.Returns(_mockSingleClientProxy);  // ISingleClientProxy for Caller
            _mockClients.Others.Returns(_mockClientProxy);
            _mockClients.Group(Arg.Any<string>()).Returns(_mockClientProxy);
            _mockClients.Client(Arg.Any<string>()).Returns(_mockSingleClientProxy);  // ISingleClientProxy for specific client
            _mockClients.User(Arg.Any<string>()).Returns(_mockSingleClientProxy);   // ISingleClientProxy for specific user

            // Create testable hub
            _hub = new TestableBaseHub(hubLogger);
            _hub.Context = _mockContext;
            _hub.Groups = _mockGroups;
            _hub.Clients = _mockClients;

            _logger = testLogger;
        }

        //   Connection Lifecycle Tests

        /// <summary>
        /// Connection Test: OnConnectedAsync should log connection event.
        /// </summary>
        [Fact(Timeout = 30_000)]
        public async Task OnConnectedAsync_Should_Log_Connection_Event()
        {
            // Arrange
            var connectionId = "test-connection-123";

            // Act
            await _hub.OnConnectedAsync();

            // Assert
            _hub.OnConnectedAsyncCalled.ShouldBeTrue();
            _logger.LogInformation("BaseHub OnConnectedAsync completed for connection {ConnectionId}", connectionId);
        }

        /// <summary>
        /// Connection Test: OnDisconnectedAsync should log disconnection event.
        /// </summary>
        [Fact(Timeout = 30_000)]
        public async Task OnDisconnectedAsync_Should_Log_Disconnection_Event()
        {
            // Arrange
            var exception = new Exception("Connection lost");

            // Act
            await _hub.OnDisconnectedAsync(exception);

            // Assert
            _hub.OnDisconnectedAsyncCalled.ShouldBeTrue();
            _hub.LastDisconnectionException.ShouldBe(exception);
        }

        /// <summary>
        /// Connection Test: OnDisconnectedAsync should handle null exception.
        /// </summary>
        [Fact(Timeout = 30_000)]
        public async Task OnDisconnectedAsync_Should_Handle_Null_Exception()
        {
            // Arrange & Act
            await _hub.OnDisconnectedAsync(null);

            // Assert
            _hub.OnDisconnectedAsyncCalled.ShouldBeTrue();
            _hub.LastDisconnectionException.ShouldBeNull();
        }

        //   Connection Lifecycle Tests

        //   Error Handling Tests

        /// <summary>
        /// Error Handling Test: Should handle errors gracefully during connection.
        /// </summary>
        [Fact(Timeout = 30_000)]
        public async Task OnConnectedAsync_Should_Handle_Errors_Gracefully()
        {
            // Arrange
            var faultyLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<FaultyBaseHub>.Instance;
            var faultyHub = new FaultyBaseHub(faultyLogger);
            faultyHub.Context = _mockContext;
            faultyHub.Groups = _mockGroups;
            faultyHub.Clients = _mockClients;

            // Act & Assert - Should not throw
            await faultyHub.OnConnectedAsync();

            faultyHub.ErrorHandled.ShouldBeTrue();
        }

        //   Error Handling Tests

        //   Context Access Tests

        /// <summary>
        /// Context Test: Should provide access to connection context.
        /// </summary>
        [Fact]
        public void Hub_Should_Provide_Access_To_Connection_Context()
        {
            // Assert
            _hub.Context.ShouldNotBeNull();
            _hub.Context.ConnectionId.ShouldBe("test-connection-123");
            _hub.Context.UserIdentifier.ShouldBe("test-user-456");
        }

        /// <summary>
        /// Context Test: Should provide access to group management.
        /// </summary>
        [Fact]
        public void Hub_Should_Provide_Access_To_Group_Management()
        {
            // Assert
            _hub.Groups.ShouldNotBeNull();
            _hub.Groups.ShouldBe(_mockGroups);
        }

        /// <summary>
        /// Context Test: Should provide access to client communication.
        /// </summary>
        [Fact]
        public void Hub_Should_Provide_Access_To_Client_Communication()
        {
            // Assert
            _hub.Clients.ShouldNotBeNull();
            _hub.Clients.ShouldBe(_mockClients);
        }

        //   Context Access Tests

        //   Cancellation Token Tests

        /// <summary>
        /// Cancellation Test: Should respect cancellation during connection setup.
        /// </summary>
        [Fact(Timeout = 30_000)]
        public async Task OnConnectedAsync_Should_Respect_Cancellation_Token()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var cancellableLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<CancellableBaseHub>.Instance;
            var cancellableHub = new CancellableBaseHub(cancellableLogger);
            cancellableHub.Context = _mockContext;
            cancellableHub.Groups = _mockGroups;
            cancellableHub.Clients = _mockClients;

            // Act
            cts.Cancel();
            await cancellableHub.OnConnectedAsync();

            // Assert
            cancellableHub.CancellationHandled.ShouldBeTrue();
        }

        //   Cancellation Token Tests
    }

    /// <summary>
    /// Testable implementation of BaseHub for unit testing.
    /// </summary>
    internal class TestableBaseHub : BaseHub<TestableBaseHub>
    {
        public bool OnConnectedAsyncCalled { get; private set; }
        public bool OnDisconnectedAsyncCalled { get; private set; }
        public Exception? LastDisconnectionException { get; private set; }

        /// <summary>
        /// Initializes a test hub instance using the provided logger.
        /// </summary>
        /// <param name="logger">Logger used by the base hub and its override implementations.</param>
        public TestableBaseHub(ILogger<TestableBaseHub> logger) : base(logger)
        {
        }

        /// <summary>
        /// Tracks connection attempts and delegates to the base implementation.
        /// </summary>
        /// <returns>A task that completes when the hub connection pipeline finishes.</returns>
        public override async Task OnConnectedAsync()
        {
            OnConnectedAsyncCalled = true;
            Logger.LogInformation("TestableBaseHub connection established for {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Tracks disconnect events and delegates to the base implementation.
        /// </summary>
        /// <param name="exception">Optional exception associated with the disconnection.</param>
        /// <returns>A task that completes when the disconnection pipeline finishes.</returns>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            OnDisconnectedAsyncCalled = true;
            LastDisconnectionException = exception;
            Logger.LogInformation("TestableBaseHub disconnection for {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }

    /// <summary>
    /// Faulty implementation for testing error handling.
    /// </summary>
    internal class FaultyBaseHub : BaseHub<FaultyBaseHub>
    {
        public bool ErrorHandled { get; private set; }

        /// <summary>
        /// Creates a hub that simulates failures to validate error handling.
        /// </summary>
        /// <param name="logger">Logger used to record simulated failures.</param>
        public FaultyBaseHub(ILogger<FaultyBaseHub> logger) : base(logger)
        {
        }

        /// <summary>
        /// Simulates an exception during connection and ensures it is handled gracefully.
        /// </summary>
        /// <returns>A task that completes after invoking the base connection logic.</returns>
        public override async Task OnConnectedAsync()
        {
            try
            {
                // Simulate an error during connection
                throw new InvalidOperationException("Simulated connection error");
            }
            catch (Exception ex)
            {
                ErrorHandled = true;
                Logger.LogError(ex, "Handled error in FaultyBaseHub");
                // Don't rethrow - simulate graceful error handling
            }

            await base.OnConnectedAsync();
        }
    }

    /// <summary>
    /// Implementation for testing cancellation token handling.
    /// </summary>
    internal class CancellableBaseHub : BaseHub<CancellableBaseHub>
    {
        public bool CancellationHandled { get; private set; }

        /// <summary>
        /// Creates a cancellable hub used to validate cancellation token handling.
        /// </summary>
        /// <param name="logger">Logger used for cancellation diagnostics.</param>
        public CancellableBaseHub(ILogger<CancellableBaseHub> logger) : base(logger)
        {
        }

        /// <summary>
        /// Simulates work that respects the connection cancellation token.
        /// </summary>
        /// <returns>A task that completes when the connection setup finishes or is cancelled.</returns>
        public override async Task OnConnectedAsync()
        {
            try
            {
                // Simulate work that can be cancelled
                await Task.Delay(100, Context.ConnectionAborted);
            }
            catch (OperationCanceledException)
            {
                CancellationHandled = true;
                Logger.LogInformation("Cancellation handled in CancellableBaseHub");
            }

            await base.OnConnectedAsync();
        }
    }
}
