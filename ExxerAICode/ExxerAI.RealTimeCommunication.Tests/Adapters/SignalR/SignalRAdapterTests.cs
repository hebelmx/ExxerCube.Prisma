using ExxerAI.RealTimeCommunication.Abstractions;
using ExxerAI.RealTimeCommunication.Adapters.SignalR;
using ExxerAI.RealTimeCommunication.Models;
using Microsoft.AspNetCore.SignalR;

namespace ExxerAI.RealTimeCommunication.Tests.Adapters.SignalR
{
    /// <summary>
    /// Comprehensive tests for SignalRAdapter implementation following ExxerAI patterns.
    /// Tests IndTrace patterns, Result&lt;T&gt; integration, and thread-safe connection management.
    /// </summary>
    public class SignalRAdapterTests : IAsyncDisposable, IDisposable
    {
        private readonly SignalRAdapter _adapter;
        private readonly IHubContext<SystemHub> _mockSystemHub;
        private readonly IHubContext<AgentHub> _mockAgentHub;
        private readonly IHubContext<TaskHub> _mockTaskHub;
        private readonly IHubContext<DocumentHub> _mockDocumentHub;
        private readonly IHubContext<EconomicHub> _mockEconomicHub;
        private readonly IHubClients _mockClients;
        private readonly IClientProxy _mockClientProxy;
        private readonly IGroupManager _mockGroupManager;
        private readonly ILogger<SignalRAdapter> _logger;

        /// <summary>
        /// Constructor: Initializes test dependencies and the SignalRAdapter instance.
        /// </summary>
        public SignalRAdapterTests()
        {
            // Clear any leftover argument specifications from previous tests

            // Create mock hub contexts
            _mockSystemHub = Substitute.For<IHubContext<SystemHub>>();
            _mockAgentHub = Substitute.For<IHubContext<AgentHub>>();
            _mockTaskHub = Substitute.For<IHubContext<TaskHub>>();
            _mockDocumentHub = Substitute.For<IHubContext<DocumentHub>>();
            _mockEconomicHub = Substitute.For<IHubContext<EconomicHub>>();

            // Create mock clients and proxies
            _mockClients = Substitute.For<IHubClients>();
            _mockClientProxy = Substitute.For<IClientProxy>();
            _mockGroupManager = Substitute.For<IGroupManager>();

            // Setup hub contexts to return mock clients
            _mockSystemHub.Clients.Returns(_mockClients);
            _mockAgentHub.Clients.Returns(_mockClients);
            _mockTaskHub.Clients.Returns(_mockClients);
            _mockDocumentHub.Clients.Returns(_mockClients);
            _mockEconomicHub.Clients.Returns(_mockClients);

            // Setup group managers
            _mockSystemHub.Groups.Returns(_mockGroupManager);
            _mockAgentHub.Groups.Returns(_mockGroupManager);
            _mockTaskHub.Groups.Returns(_mockGroupManager);
            _mockDocumentHub.Groups.Returns(_mockGroupManager);
            _mockEconomicHub.Groups.Returns(_mockGroupManager);

            // Setup clients to return mock proxy
            _mockClients.All.Returns(_mockClientProxy);
            _mockClients.Group(Arg.Any<string>()).Returns(_mockClientProxy);
            _mockClients.User(Arg.Any<string>()).Returns(_mockClientProxy);

            // Setup logger (using NullLogger for unit tests)
            _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<SignalRAdapter>.Instance;

            // Create adapter instance
            _adapter = new SignalRAdapter(
                _mockSystemHub,
                _mockAgentHub,
                _mockTaskHub,
                _mockDocumentHub,
                _mockEconomicHub,
                _logger);
        }

        //   Constructor Tests

        /// <summary>
        /// Constructor Test: Should initialize with all required dependencies.
        /// </summary>
        [Fact]
        public void Constructor_Should_Initialize_With_All_Dependencies()
        {
            // Arrange & Act done in constructor

            // Assert
            _adapter.ShouldNotBeNull();
            _logger.LogInformation("SignalRAdapter successfully initialized with all dependencies");
        }

        /// <summary>
        /// Constructor Test: Should throw ArgumentNullException for null system hub.
        /// </summary>
        [Fact]
        public void Constructor_Should_Throw_When_SystemHub_Is_Null()
        {
            // Arrange & Act & Assert
            Should.Throw<ArgumentNullException>(() => new SignalRAdapter(
                null!,
                _mockAgentHub,
                _mockTaskHub,
                _mockDocumentHub,
                _mockEconomicHub,
                _logger));
        }

        /// <summary>
        /// Constructor Test: Should throw ArgumentNullException for null logger.
        /// </summary>
        [Fact]
        public void Constructor_Should_Throw_When_Logger_Is_Null()
        {
            // Arrange & Act & Assert
            Should.Throw<ArgumentNullException>(() => new SignalRAdapter(
                _mockSystemHub,
                _mockAgentHub,
                _mockTaskHub,
                _mockDocumentHub,
                _mockEconomicHub,
                null!));
        }

        //   Constructor Tests

        //   BroadcastMessageAsync Tests

        /// <summary>
        /// Business Logic Test: BroadcastMessageAsync should succeed with valid request.
        /// </summary>
        /// <returns>A task that completes after broadcasting the message and asserting success.</returns>
        [Fact(Timeout = 30_000)]
        public async Task BroadcastMessageAsync_Should_Succeed_When_ValidRequest()
        {
            // Arrange
            var request = new BroadcastMessageRequest(
                CommunicationTarget.System,
                "TestMessage",
                new { Message = "Hello World" });

            // Act
            var result = await _adapter.BroadcastMessageAsync(request, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            await _mockClientProxy.Received(1).SendAsync("TestMessage", Arg.Is<object>(o => o != null), Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Validation Test: BroadcastMessageAsync should return failure for null request.
        /// </summary>
        /// <returns>A task that completes after validating the null request path.</returns>
        [Fact(Timeout = 30_000)]
        public async Task BroadcastMessageAsync_Should_Return_Failure_When_Request_Is_Null()
        {
            // Arrange
            BroadcastMessageRequest? request = null;

            // Act
            var result = await _adapter.BroadcastMessageAsync(request!, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Error.ShouldNotBeNull();
            result.Error.ShouldContain("cannot be null");
        }

        /// <summary>
        /// Business Logic Test: BroadcastMessageAsync should handle all communication targets.
        /// </summary>
        /// <param name="targetName">Communication target name that maps to a specific hub.</param>
        /// <returns>A task that completes after broadcasting to the requested hub target.</returns>
        [Theory(Timeout = 30_000)]
        [InlineData(nameof(CommunicationTarget.System))]
        [InlineData(nameof(CommunicationTarget.Agent))]
        [InlineData(nameof(CommunicationTarget.Task))]
        [InlineData(nameof(CommunicationTarget.Document))]
        [InlineData(nameof(CommunicationTarget.Economic))]
        public async Task BroadcastMessageAsync_Should_Handle_All_Communication_Targets(string targetName)
        {
            // Arrange
            var target = Enum.Parse<CommunicationTarget>(targetName);
            var request = new BroadcastMessageRequest(
                target,
                "TestMessage",
                new { Message = "Test" });

            // Act
            var result = await _adapter.BroadcastMessageAsync(request, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            await _mockClientProxy.Received(1).SendAsync("TestMessage", Arg.Is<object>(o => o != null), Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Error Handling Test: BroadcastMessageAsync should handle SignalR exceptions.
        /// </summary>
        /// <returns>A task that completes after ensuring SignalR exceptions are surfaced as failures.</returns>
        [Fact(Timeout = 30_000)]
        public async Task BroadcastMessageAsync_Should_Handle_SignalR_Exceptions()
        {
            // Arrange
            var request = new BroadcastMessageRequest(
                CommunicationTarget.System,
                "TestMessage",
                new { Message = "Test" });

            _mockClientProxy.SendAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException(new InvalidOperationException("SignalR connection failed")));

            // Act
            var result = await _adapter.BroadcastMessageAsync(request, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Error.ShouldNotBeNull();
            result.Error.ShouldContain("SignalR connection failed");
        }

        /// <summary>
        /// Cancellation Test: BroadcastMessageAsync should respect cancellation tokens.
        /// </summary>
        /// <returns>A task that completes after asserting cancellation is honored.</returns>
        [Fact(Timeout = 30_000)]
        public async Task BroadcastMessageAsync_Should_Respect_Cancellation_Token()
        {
            // Arrange
            var request = new BroadcastMessageRequest(
                CommunicationTarget.System,
                "TestMessage",
                new { Message = "Test" });

            using var cts = new CancellationTokenSource();

            _mockClientProxy.SendAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromCanceled(cts.Token));

            // Act
            var result = await _adapter.BroadcastMessageAsync(request, cts.Token);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Error.ShouldNotBeNull();
            result.Error.ShouldContain("canceled", Case.Insensitive);
        }

        //   BroadcastMessageAsync Tests

        //   SendToGroupAsync Tests

        /// <summary>
        /// Business Logic Test: SendToGroupAsync should succeed with valid request.
        /// </summary>
        /// <returns>A task that completes after sending the group message and asserting delivery.</returns>
        [Fact(Timeout = 30_000)]
        public async Task SendToGroupAsync_Should_Succeed_When_ValidRequest()
        {
            // Arrange
            var request = new GroupMessageRequest(
                CommunicationTarget.Agent,
                "test-group",
                "GroupMessage",
                new { Message = "Hello Group" });

            // Act
            var result = await _adapter.SendToGroupAsync(request, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            await _mockClientProxy.Received(1).SendAsync("GroupMessage", Arg.Is<object>(o => o != null), Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Validation Test: SendToGroupAsync should return failure for null group name.
        /// </summary>
        /// <returns>A task that completes after verifying the invalid group path returns failure.</returns>
        [Fact(Timeout = 30_000)]
        public async Task SendToGroupAsync_Should_Return_Failure_When_GroupName_Is_Null()
        {
            // Arrange
            var request = new GroupMessageRequest(
                CommunicationTarget.Agent,
                null!,
                "GroupMessage",
                new { Message = "Test" });

            // Act
            var result = await _adapter.SendToGroupAsync(request, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Error.ShouldNotBeNull();
            result.Error.ShouldContain("Group name cannot be null");
        }

        //   SendToGroupAsync Tests

        //   SendToUserAsync Tests

        /// <summary>
        /// Business Logic Test: SendToUserAsync should succeed with valid request.
        /// </summary>
        /// <returns>A task that completes after sending a user message and asserting delivery.</returns>
        [Fact(Timeout = 30_000)]
        public async Task SendToUserAsync_Should_Succeed_When_ValidRequest()
        {
            // Arrange
            var request = new UserMessageRequest(
                CommunicationTarget.Task,
                "user123",
                "UserMessage",
                new { Message = "Hello User" });

            // Act
            var result = await _adapter.SendToUserAsync(request, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            await _mockClientProxy.Received(1).SendAsync("UserMessage", Arg.Is<object>(o => o != null), Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Validation Test: SendToUserAsync should return failure for null user ID.
        /// </summary>
        /// <returns>A task that completes after verifying null user identifiers are rejected.</returns>
        [Fact(Timeout = 30_000)]
        public async Task SendToUserAsync_Should_Return_Failure_When_UserId_Is_Null()
        {
            // Arrange
            var request = new UserMessageRequest(
                CommunicationTarget.Task,
                null!,
                "UserMessage",
                new { Message = "Test" });

            // Act
            var result = await _adapter.SendToUserAsync(request, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Error.ShouldNotBeNull();
            result.Error.ShouldContain("User ID cannot be null");
        }

        //   SendToUserAsync Tests

        //   AddToGroupAsync Tests

        /// <summary>
        /// Business Logic Test: AddToGroupAsync should succeed with valid request.
        /// </summary>
        /// <returns>A task that completes after adding the connection to the group and asserting success.</returns>
        [Fact(Timeout = 30_000)]
        public async Task AddToGroupAsync_Should_Succeed_When_ValidRequest()
        {
            // Arrange
            var request = new AddToGroupRequest(
                CommunicationTarget.Document,
                "connection123",
                "document-group");

            // Act
            var result = await _adapter.AddToGroupAsync(request, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            await _mockGroupManager.Received(1).AddToGroupAsync("connection123", "document-group", TestContext.Current.CancellationToken);
        }

        /// <summary>
        /// Validation Test: AddToGroupAsync should return failure for null connection ID.
        /// </summary>
        /// <returns>A task that completes after verifying null connection identifiers are rejected.</returns>
        [Fact(Timeout = 30_000)]
        public async Task AddToGroupAsync_Should_Return_Failure_When_ConnectionId_Is_Null()
        {
            // Arrange
            var request = new AddToGroupRequest(
                CommunicationTarget.Document,
                null!,
                "document-group");

            // Act
            var result = await _adapter.AddToGroupAsync(request, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Error.ShouldNotBeNull();
            result.Error.ShouldContain("Connection ID cannot be null");
        }

        //   AddToGroupAsync Tests

        //   Event Broadcasting Tests

        /// <summary>
        /// Business Logic Test: BroadcastSystemEventAsync should succeed with valid event.
        /// </summary>
        /// <returns>A task that completes after broadcasting the system event.</returns>
        [Fact(Timeout = 30_000)]
        public async Task BroadcastSystemEventAsync_Should_Succeed_When_ValidEvent()
        {
            // Arrange
            var systemEvent = new SystemEvent("SystemStarted", DateTime.UtcNow, new { Status = "Online" });

            // Act
            var result = await _adapter.BroadcastSystemEventAsync(systemEvent, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            await _mockClientProxy.Received(1).SendAsync("SystemEvent", Arg.Is<object>(o => o != null), Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Business Logic Test: BroadcastAgentEventAsync should succeed with valid event.
        /// </summary>
        /// <returns>A task that completes after broadcasting the agent event.</returns>
        [Fact(Timeout = 30_000)]
        public async Task BroadcastAgentEventAsync_Should_Succeed_When_ValidEvent()
        {
            // Arrange
            var agentEvent = new AgentEvent("AgentStarted", DateTime.UtcNow, new { Status = "Active" }, "agent123");

            // Act
            var result = await _adapter.BroadcastAgentEventAsync(agentEvent, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            await _mockClientProxy.Received(1).SendAsync("AgentEvent", Arg.Is<object>(o => o != null), Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Business Logic Test: BroadcastTaskEventAsync should succeed with valid event.
        /// </summary>
        /// <returns>A task that completes after broadcasting the task event.</returns>
        [Fact(Timeout = 30_000)]
        public async Task BroadcastTaskEventAsync_Should_Succeed_When_ValidEvent()
        {
            // Arrange
            var taskEvent = new TaskEvent("TaskStarted", DateTime.UtcNow, new { Status = "Running" }, "task123");

            // Act
            var result = await _adapter.BroadcastTaskEventAsync(taskEvent, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            await _mockClientProxy.Received(1).SendAsync("TaskEvent", Arg.Is<object>(o => o != null), Arg.Any<CancellationToken>());
        }

        //   Event Broadcasting Tests

        //   Notification Tests

        /// <summary>
        /// Business Logic Test: SendNotificationAsync should succeed with valid request.
        /// </summary>
        /// <returns>A task that completes after sending notifications and verifying delivery.</returns>
        [Fact(Timeout = 30_000)]
        public async Task SendNotificationAsync_Should_Succeed_When_ValidRequest()
        {
            // Arrange
            var request = new NotificationRequest(
                CommunicationTarget.System,
                "InfoNotification",
                "Test Notification",
                "This is a test notification");

            // Act
            var result = await _adapter.SendNotificationAsync(request, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            await _mockClientProxy.Received(1).SendAsync("Notification", Arg.Is<object>(o => o != null), Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Business Logic Test: SendAlertAsync should succeed with valid request.
        /// </summary>
        /// <returns>A task that completes after sending alerts and verifying delivery.</returns>
        [Fact(Timeout = 30_000)]
        public async Task SendAlertAsync_Should_Succeed_When_ValidRequest()
        {
            // Arrange
            var request = new AlertRequest(
                CommunicationTarget.System,
                "SystemAlert",
                CommunicationAlertSeverity.Warning,
                "System warning message");

            // Act
            var result = await _adapter.SendAlertAsync(request, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            await _mockClientProxy.Received(1).SendAsync("Alert", Arg.Is<object>(o => o != null), Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Business Logic Test: SendProgressUpdateAsync should validate progress range.
        /// </summary>
        /// <returns>A task that completes after ensuring invalid progress values are rejected.</returns>
        [Fact(Timeout = 30_000)]
        public async Task SendProgressUpdateAsync_Should_Validate_Progress_Range()
        {
            // Arrange
            var request = new ProgressUpdateRequest(
                CommunicationTarget.Task,
                "Task",
                "task123",
                150); // Invalid: > 100

            // Act
            var result = await _adapter.SendProgressUpdateAsync(request, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Error.ShouldNotBeNull();
            result.Error.ShouldContain("Progress must be between 0 and 100");
        }

        //   Notification Tests

        //   Disposal Tests

        /// <summary>
        /// Resource Management Test: Should handle disposal gracefully.
        /// </summary>
        /// <returns>A task that completes after disposing the adapter and validating the disposed state.</returns>
        [Fact(Timeout = 30_000)]
        public async Task DisposeAsync_Should_Handle_Disposal_Gracefully()
        {
            // Arrange
            var request = new BroadcastMessageRequest(
                CommunicationTarget.System,
                "TestMessage",
                new { Message = "Test" });

            // Act
            await _adapter.DisposeAsync();
            var result = await _adapter.BroadcastMessageAsync(request, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Error.ShouldNotBeNull();
            result.Error.ShouldContain("disposed");
        }

        //   Disposal Tests

        //   Performance and Thread Safety Tests

        /// <summary>
        /// Performance Test: Should handle concurrent operations efficiently.
        /// </summary>
        /// <returns>A task that completes after executing concurrent operations and asserting success.</returns>
        [Fact(Timeout = 30_000)]
        public async Task Concurrent_Operations_Should_Be_Thread_Safe()
        {
            // Arrange
            const int operationCount = 10;
            var tasks = new List<Task<Result>>();

            // Act
            for (int i = 0; i < operationCount; i++)
            {
                var index = i; // Capture for closure
                var task = _adapter.BroadcastMessageAsync(new BroadcastMessageRequest(
                    CommunicationTarget.System,
                    $"Message{index}",
                    new { Index = index }), TestContext.Current.CancellationToken);

                tasks.Add(task);
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Length.ShouldBe(operationCount);
            results.ShouldAllBe(r => r.IsSuccess);
        }

        //   Performance and Thread Safety Tests

        //   IDisposable and IAsyncDisposable Implementation

        /// <summary>
        /// Disposes test resources synchronously.
        /// </summary>
        public void Dispose()
        {
            // Clear any leftover argument specifications

            _adapter?.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes test resources asynchronously.
        /// </summary>
        /// <returns>A task that completes when the adapter has been disposed.</returns>
        public async ValueTask DisposeAsync()
        {
            // Clear any leftover argument specifications

            if (_adapter != null)
            {
                await _adapter.DisposeAsync();
            }

            GC.SuppressFinalize(this);
        }

        //   IDisposable and IAsyncDisposable Implementation
    }
}
