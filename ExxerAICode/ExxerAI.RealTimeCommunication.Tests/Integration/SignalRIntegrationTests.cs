using ExxerAI.RealTimeCommunication.Abstractions;
using ExxerAI.RealTimeCommunication.Extensions;
using ExxerAI.RealTimeCommunication.Adapters.SignalR;
using ExxerAI.RealTimeCommunication.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExxerAI.RealTimeCommunication.Tests.Integration
{
    /// <summary>
    /// Integration tests for SignalR communication flows with real connections.
    /// Tests end-to-end communication patterns and hub functionality.
    /// </summary>
    public class SignalRIntegrationTests : IAsyncDisposable
    {
        private readonly ILogger<SignalRIntegrationTests> _logger;
        private readonly TestServer _testServer;
        private readonly IHost _host;
        private readonly ICompleteCommunicationPort _communicationPort;
        private readonly List<HubConnection> _hubConnections;

        /// <summary>
        /// Initializes a new instance of the <see cref="SignalRIntegrationTests"/> class.
        /// Builds an in-memory test host with SignalR hubs and resolves the communication port.
        /// </summary>
        public SignalRIntegrationTests()
        {
            _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<SignalRIntegrationTests>.Instance;
            _hubConnections = new List<HubConnection>();

            // Create test host with SignalR configuration
            var hostBuilder = new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.ConfigureServices(services =>
                    {
                        services.AddLogging(builder => builder.AddProvider(new TestLoggerProvider()));
                        services.AddRealTimeCommunication(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
                    });
                    webHost.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapHub<SystemHub>("/hubs/system");
                            endpoints.MapHub<AgentHub>("/hubs/agent");
                            endpoints.MapHub<TaskHub>("/hubs/task");
                            endpoints.MapHub<DocumentHub>("/hubs/document");
                            endpoints.MapHub<EconomicHub>("/hubs/economic");
                        });
                    });
                });

            _host = hostBuilder.Build();
            _host.StartAsync().Wait();

            _testServer = _host.GetTestServer();
            _communicationPort = _host.Services.GetRequiredService<ICompleteCommunicationPort>();
        }

        //   Hub Connection Tests

        /// <summary>
        /// Integration Test: Should establish connection to SystemHub successfully.
        /// </summary>
        /// <returns>A task that completes when the connection attempt and assertions finish.</returns>
        [Fact(Timeout = 30_000)]
        public async Task Should_Establish_Connection_To_SystemHub_Successfully()
        {
            // Arrange
            var connection = CreateHubConnection("/hubs/system");

            // Act
            await connection.StartAsync(TestContext.Current.CancellationToken);

            // Assert
            connection.State.ShouldBe(HubConnectionState.Connected);

            _logger.LogInformation("Successfully connected to SystemHub");
        }

        /// <summary>
        /// Integration Test: Should establish connections to all hub types.
        /// </summary>
        [Theory(Timeout = 30_000)]
        [InlineData("/hubs/system")]
        [InlineData("/hubs/agent")]
        [InlineData("/hubs/task")]
        [InlineData("/hubs/document")]
        [InlineData("/hubs/economic")]
        /// <param name="hubPath">Relative hub path that should accept a connection.</param>
        /// <returns>A task that completes when the connection attempt and assertions finish.</returns>
        public async Task Should_Establish_Connection_To_All_Hub_Types(string hubPath)
        {
            // Arrange
            var connection = CreateHubConnection(hubPath);

            // Act
            await connection.StartAsync(TestContext.Current.CancellationToken);

            // Assert
            connection.State.ShouldBe(HubConnectionState.Connected);

            _logger.LogInformation("Successfully connected to hub: {HubPath}", hubPath);
        }

        //   Hub Connection Tests

        //   Message Broadcasting Tests

        /// <summary>
        /// Integration Test: Should broadcast message to all connected clients.
        /// </summary>
        /// <returns>A task that completes after broadcasting and verifying the received messages.</returns>
        [Fact(Timeout = 30_000)]
        public async Task Should_Broadcast_Message_To_All_Connected_Clients()
        {
            // Arrange
            var connection1 = CreateHubConnection("/hubs/system");
            var connection2 = CreateHubConnection("/hubs/system");

            var receivedMessages = new List<object>();
            var messageReceived = new TaskCompletionSource<bool>();

            connection1.On<object>("TestMessage", (data) =>
            {
                receivedMessages.Add(data);
                if (receivedMessages.Count >= 2) messageReceived.SetResult(true);
            });

            connection2.On<object>("TestMessage", (data) =>
            {
                receivedMessages.Add(data);
                if (receivedMessages.Count >= 2) messageReceived.SetResult(true);
            });

            await connection1.StartAsync(TestContext.Current.CancellationToken);
            await connection2.StartAsync(TestContext.Current.CancellationToken);

            var broadcastRequest = new BroadcastMessageRequest(
                CommunicationTarget.System,
                "TestMessage",
                new { Message = "Hello World", Timestamp = DateTime.UtcNow });

            // Act
            var result = await _communicationPort.BroadcastMessageAsync(broadcastRequest, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();

            // Wait for messages to be received
            await messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

            receivedMessages.Count.ShouldBe(2);

            _logger.LogInformation("Successfully broadcast message to {ClientCount} clients", receivedMessages.Count);
        }

        /// <summary>
        /// Integration Test: Should send message to specific group.
        /// </summary>
        /// <returns>A task that completes after group message delivery and assertions.</returns>
        [Fact(Timeout = 30_000)]
        public async Task Should_Send_Message_To_Specific_Group()
        {
            // Arrange
            var connection1 = CreateHubConnection("/hubs/agent");
            var connection2 = CreateHubConnection("/hubs/agent");
            var connection3 = CreateHubConnection("/hubs/agent");

            var groupMessages = new List<object>();
            var messageReceived = new TaskCompletionSource<bool>();

            connection1.On<object>("GroupMessage", (data) =>
            {
                groupMessages.Add(data);
                if (groupMessages.Count >= 2) messageReceived.SetResult(true);
            });

            connection2.On<object>("GroupMessage", (data) =>
            {
                groupMessages.Add(data);
                if (groupMessages.Count >= 2) messageReceived.SetResult(true);
            });

            connection3.On<object>("GroupMessage", (data) =>
            {
                groupMessages.Add(data);
                // Connection3 should not receive group messages
            });

            await connection1.StartAsync(TestContext.Current.CancellationToken);
            await connection2.StartAsync(TestContext.Current.CancellationToken);
            await connection3.StartAsync(TestContext.Current.CancellationToken);

            // Add connections to group
            await _communicationPort.AddToGroupAsync(new AddToGroupRequest(
                CommunicationTarget.Agent,
                connection1.ConnectionId!,
                "test-group"), TestContext.Current.CancellationToken);

            await _communicationPort.AddToGroupAsync(new AddToGroupRequest(
                CommunicationTarget.Agent,
                connection2.ConnectionId!,
                "test-group"), TestContext.Current.CancellationToken);

            // Connection3 is not added to the group

            var groupRequest = new GroupMessageRequest(
                CommunicationTarget.Agent,
                "test-group",
                "GroupMessage",
                new { Message = "Group Message", Timestamp = DateTime.UtcNow });

            // Act
            var result = await _communicationPort.SendToGroupAsync(groupRequest, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();

            // Wait for messages to be received
            await messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

            groupMessages.Count.ShouldBe(2); // Only connections in the group should receive

            _logger.LogInformation("Successfully sent group message to {GroupMembers} group members", groupMessages.Count);
        }

        //   Message Broadcasting Tests

        //   Event Broadcasting Tests

        /// <summary>
        /// Integration Test: Should broadcast system events to connected clients.
        /// </summary>
        /// <returns>A task that completes after broadcasting and validating system events.</returns>
        [Fact(Timeout = 30_000)]
        public async Task Should_Broadcast_System_Events_To_Connected_Clients()
        {
            // Arrange
            var connection = CreateHubConnection("/hubs/system");

            SystemEvent? receivedEvent = null;
            var eventReceived = new TaskCompletionSource<bool>();

            connection.On<SystemEvent>("SystemEvent", (systemEvent) =>
            {
                receivedEvent = systemEvent;
                eventReceived.SetResult(true);
            });

            await connection.StartAsync(TestContext.Current.CancellationToken);

            var systemEvent = new SystemEvent("SystemStarted", DateTime.UtcNow, new { Status = "Online", Version = "1.0.0" });

            // Act
            var result = await _communicationPort.BroadcastSystemEventAsync(systemEvent, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();

            await eventReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

            receivedEvent.ShouldNotBeNull();
            receivedEvent.EventType.ShouldBe("SystemStarted");

            _logger.LogInformation("Successfully broadcast system event: {EventType}", receivedEvent.EventType);
        }

        /// <summary>
        /// Integration Test: Should broadcast agent events to connected clients.
        /// </summary>
        /// <returns>A task that completes after broadcasting and validating agent events.</returns>
        [Fact(Timeout = 30_000)]
        public async Task Should_Broadcast_Agent_Events_To_Connected_Clients()
        {
            // Arrange
            var connection = CreateHubConnection("/hubs/agent");

            AgentEvent? receivedEvent = null;
            var eventReceived = new TaskCompletionSource<bool>();

            connection.On<AgentEvent>("AgentEvent", (agentEvent) =>
            {
                receivedEvent = agentEvent;
                eventReceived.SetResult(true);
            });

            await connection.StartAsync(TestContext.Current.CancellationToken);

            var agentEvent = new AgentEvent("AgentStarted", DateTime.UtcNow, new { Status = "Active", Capabilities = new[] { "Processing", "Analysis" } }, "agent-123");

            // Act
            var result = await _communicationPort.BroadcastAgentEventAsync(agentEvent, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();

            await eventReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

            receivedEvent.ShouldNotBeNull();
            receivedEvent.EventType.ShouldBe("AgentStarted");
            receivedEvent.AgentId.ShouldBe("agent-123");

            _logger.LogInformation("Successfully broadcast agent event: {EventType} for agent {AgentId}",
                receivedEvent.EventType, receivedEvent.AgentId);
        }

        //   Event Broadcasting Tests

        //   Notification Tests

        /// <summary>
        /// Integration Test: Should send notifications to connected clients.
        /// </summary>
        /// <returns>A task that completes after sending notifications and validating receipt.</returns>
        [Fact(Timeout = 30_000)]
        public async Task Should_Send_Notifications_To_Connected_Clients()
        {
            // Arrange
            var connection = CreateHubConnection("/hubs/system");

            NotificationRequest? receivedNotification = null;
            var notificationReceived = new TaskCompletionSource<bool>();

            connection.On<NotificationRequest>("Notification", (notification) =>
            {
                receivedNotification = notification;
                notificationReceived.SetResult(true);
            });

            await connection.StartAsync(TestContext.Current.CancellationToken);

            var notificationRequest = new NotificationRequest(
                CommunicationTarget.System,
                "InfoNotification",
                "System Update",
                "System has been updated to version 2.0");

            // Act
            var result = await _communicationPort.SendNotificationAsync(notificationRequest, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();

            await notificationReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

            receivedNotification.ShouldNotBeNull();
            receivedNotification.Title.ShouldBe("System Update");
            receivedNotification.Message.ShouldBe("System has been updated to version 2.0");

            _logger.LogInformation("Successfully sent notification: {Title}", receivedNotification.Title);
        }

        /// <summary>
        /// Integration Test: Should send alerts with different severity levels.
        /// </summary>
        /// <returns>A task that completes after sending alerts and validating severity handling.</returns>
        [Fact(Timeout = 30_000)]
        public async Task Should_Send_Alerts_With_Different_Severity_Levels()
        {
            // Arrange
            var connection = CreateHubConnection("/hubs/system");

            var receivedAlerts = new List<AlertRequest>();
            var alertsReceived = new TaskCompletionSource<bool>();

            connection.On<AlertRequest>("Alert", (alert) =>
            {
                receivedAlerts.Add(alert);
                if (receivedAlerts.Count >= 3) alertsReceived.SetResult(true);
            });

            await connection.StartAsync(TestContext.Current.CancellationToken);

            var alerts = new[]
            {
                new AlertRequest(
                    CommunicationTarget.System,
                    "InfoAlert",
                    CommunicationAlertSeverity.Informational,
                    "Information alert message"),
                new AlertRequest(
                    CommunicationTarget.System,
                    "WarningAlert",
                    CommunicationAlertSeverity.Warning,
                    "Warning alert message"),
                new AlertRequest(
                    CommunicationTarget.System,
                    "ErrorAlert",
                    CommunicationAlertSeverity.Error,
                    "Error alert message")
            };

            // Act
            foreach (var alert in alerts)
            {
                var result = await _communicationPort.SendAlertAsync(alert, TestContext.Current.CancellationToken);
                result.IsSuccess.ShouldBeTrue();
            }

            // Assert
            await alertsReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

            receivedAlerts.Count.ShouldBe(3);
            receivedAlerts.ShouldContain(a => a.Severity == CommunicationAlertSeverity.Informational);
            receivedAlerts.ShouldContain(a => a.Severity == CommunicationAlertSeverity.Warning);
            receivedAlerts.ShouldContain(a => a.Severity == CommunicationAlertSeverity.Error);

            _logger.LogInformation("Successfully sent {AlertCount} alerts with different severity levels", receivedAlerts.Count);
        }

        //   Notification Tests

        //   Connection Management Tests

        /// <summary>
        /// Integration Test: Should handle connection disconnection gracefully.
        /// </summary>
        /// <returns>A task that completes after disconnecting and verifying the result.</returns>
        [Fact(Timeout = 30_000)]
        public async Task Should_Handle_Connection_Disconnection_Gracefully()
        {
            // Arrange
            var connection = CreateHubConnection("/hubs/system");
            await connection.StartAsync(TestContext.Current.CancellationToken);

            connection.State.ShouldBe(HubConnectionState.Connected);

            // Act
            await connection.StopAsync(TestContext.Current.CancellationToken);

            // Assert
            connection.State.ShouldBe(HubConnectionState.Disconnected);

            _logger.LogInformation("Connection disconnected gracefully");
        }

        /// <summary>
        /// Integration Test: Should handle multiple concurrent connections.
        /// </summary>
        /// <returns>A task that completes after connecting multiple clients and verifying their state.</returns>
        [Fact(Timeout = 30_000)]
        public async Task Should_Handle_Multiple_Concurrent_Connections()
        {
            // Arrange
            const int connectionCount = 5;
            var connections = new List<HubConnection>();

            // Act
            for (int i = 0; i < connectionCount; i++)
            {
                var connection = CreateHubConnection("/hubs/system");
                await connection.StartAsync(TestContext.Current.CancellationToken);
                connections.Add(connection);
            }

            // Assert
            connections.Count.ShouldBe(connectionCount);
            connections.ShouldAllBe(c => c.State == HubConnectionState.Connected);

            _logger.LogInformation("Successfully established {ConnectionCount} concurrent connections", connectionCount);

            // Cleanup
            foreach (var connection in connections)
            {
                await connection.StopAsync(TestContext.Current.CancellationToken);
                await connection.DisposeAsync();
            }
        }

        //   Connection Management Tests

        //   Performance Tests

        /// <summary>
        /// Performance Test: Should handle high message throughput.
        /// </summary>
        /// <returns>A task that completes after sending high-volume messages and asserting throughput.</returns>
        [Fact(Timeout = 30_000)]
        public async Task Should_Handle_High_Message_Throughput()
        {
            // Arrange
            const int messageCount = 50;
            var connection = CreateHubConnection("/hubs/system");

            var messagesReceived = 0;
            var allMessagesReceived = new TaskCompletionSource<bool>();

            connection.On<object>("PerformanceTest", (data) =>
            {
                Interlocked.Increment(ref messagesReceived);
                if (messagesReceived >= messageCount)
                {
                    allMessagesReceived.SetResult(true);
                }
            });

            await connection.StartAsync(TestContext.Current.CancellationToken);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var tasks = Enumerable.Range(0, messageCount)
                .Select(i => _communicationPort.BroadcastMessageAsync(new BroadcastMessageRequest(
                    CommunicationTarget.System,
                    "PerformanceTest",
                    new { MessageId = i, Timestamp = DateTime.UtcNow }), TestContext.Current.CancellationToken))
                .ToArray();

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            tasks.ShouldAllBe(t => t.Result.IsSuccess);

            await allMessagesReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

            messagesReceived.ShouldBe(messageCount);

            _logger.LogInformation("Successfully processed {MessageCount} messages in {ElapsedMs}ms (avg: {AvgMs}ms/msg)",
                messageCount, stopwatch.ElapsedMilliseconds, stopwatch.ElapsedMilliseconds / (double)messageCount);
        }

        //   Performance Tests

        //   Helper Methods

        /// <summary>
        /// Creates a new hub connection for testing.
        /// </summary>
        /// <param name="hubPath">Relative hub endpoint to connect to within the test server.</param>
        /// <returns>A configured <see cref="HubConnection"/> ready to start.</returns>
        private HubConnection CreateHubConnection(string hubPath)
        {
            var connection = new HubConnectionBuilder()
                //.WithUrl(_testServer.BaseAddress + hubPath.TrimStart('/'), options =>
                //{
                //    options.HttpMessageHandlerFactory = _ => _testServer.CreateHandler();
                //})
                .WithAutomaticReconnect()
                .Build();

            _hubConnections.Add(connection);
            return connection;
        }

        //   Helper Methods

        //   IAsyncDisposable Implementation

        /// <summary>
        /// Disposes test resources asynchronously.
        /// </summary>
        /// <returns>A task that completes when all connections and host resources are released.</returns>
        public async ValueTask DisposeAsync()
        {
            foreach (var connection in _hubConnections)
            {
                if (connection.State == HubConnectionState.Connected)
                {
                    await connection.StopAsync();
                }
                await connection.DisposeAsync();
            }

            _hubConnections.Clear();

            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }

            _testServer?.Dispose();

            GC.SuppressFinalize(this);
        }

        //   IAsyncDisposable Implementation

        /// <summary>
        /// Test logger provider for integration tests.
        /// </summary>
        internal class TestLoggerProvider : ILoggerProvider
        {
            /// <summary>
            /// Creates a logger with the specified category name for test output.
            /// </summary>
            /// <param name="categoryName">The logger category name.</param>
            /// <returns>A logger that writes to the console to aid diagnostics.</returns>
            public ILogger CreateLogger(string categoryName)
            {
                return new TestIntegrationLogger(categoryName);
            }

            /// <summary>
            /// Disposes any resources held by the provider.
            /// </summary>
            public void Dispose()
            {
                // No resources to dispose
            }
        }

        /// <summary>
        /// Test logger implementation that writes log messages to the console for debugging tests.
        /// </summary>
        internal class TestIntegrationLogger : ILogger
        {
            private readonly string _categoryName;

            /// <summary>
            /// Initializes a new instance of the <see cref="TestIntegrationLogger"/> class.
            /// </summary>
            /// <param name="categoryName">Category name used in log output.</param>
            public TestIntegrationLogger(string categoryName)
            {
                _categoryName = categoryName;
            }

            /// <summary>
            /// Begins a logical operation scope. Not required for test logger, returns null.
            /// </summary>
            /// <typeparam name="TState">Type of the scoped state object.</typeparam>
            /// <param name="state">State value provided by the caller to contextualize logs.</param>
            /// <returns>Always null because scope tracking is not implemented for the test logger.</returns>
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            /// <summary>
            /// Determines whether logging is enabled for the specified level.
            /// </summary>
            /// <param name="logLevel">Log level to check.</param>
            /// <returns>true when the level is Information or higher so test output is visible.</returns>
            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel >= LogLevel.Information;
            }

            /// <summary>
            /// Writes a log entry using the provided formatter.
            /// </summary>
            /// <typeparam name="TState">Type of the state object.</typeparam>
            /// <param name="logLevel">Log level for this entry.</param>
            /// <param name="eventId">Event identifier.</param>
            /// <param name="state">State object to format.</param>
            /// <param name="exception">Optional exception associated with the entry.</param>
            /// <param name="formatter">Function that formats the state and exception into a message string.</param>
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                try
                {
                    var message = formatter(state, exception);
                    Console.WriteLine($"[{logLevel}] {_categoryName}: {message}");

                    if (exception != null)
                    {
                        Console.WriteLine($"Exception: {exception}");
                    }
                }
                catch
                {
                    // Ignore logging errors in tests
                }
            }
        }
    }
}
