using ExxerCube.Prisma.SignalR.Abstractions.Tests.Infrastructure;
using Microsoft.AspNetCore.SignalR;

namespace ExxerCube.Prisma.SignalR.Abstractions.Tests.Abstractions.Hubs;

/// <summary>
/// Tests for the ExxerHub&lt;T&gt; base class.
/// Note: Full SignalR hub testing requires integration tests due to extension methods.
/// These unit tests focus on validation, cancellation, and error handling logic.
/// </summary>
public class ExxerHubTests
{
    private readonly ILogger<TestHub> _logger;
    private readonly HubCallerContext _mockContext;
    private readonly IHubCallerClients _mockClients;
    private readonly IClientProxy _mockClientProxy;
    private readonly ISingleClientProxy _mockSingleClientProxy;
    private readonly IGroupManager _mockGroups;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExxerHubTests"/> class.
    /// </summary>
    public ExxerHubTests()
    {
        _logger = Substitute.For<ILogger<TestHub>>();
        
        // Setup SignalR mocks
        _mockContext = Substitute.For<HubCallerContext>();
        _mockClients = Substitute.For<IHubCallerClients>();
        _mockClientProxy = Substitute.For<IClientProxy>();
        _mockSingleClientProxy = Substitute.For<ISingleClientProxy>();
        _mockGroups = Substitute.For<IGroupManager>();

        // Configure mock context
        _mockContext.ConnectionId.Returns("test-connection-id");
        
        // Configure mock clients
        _mockClients.All.Returns(_mockClientProxy);
        _mockClients.Client(Arg.Any<string>()).Returns(_mockSingleClientProxy); // Client() returns ISingleClientProxy
        _mockClients.Group(Arg.Any<string>()).Returns(_mockClientProxy);
    }

    /// <summary>
    /// Tests that SendToAllAsync returns cancelled when cancellation is requested.
    /// </summary>
    [Fact]
    public async Task SendToAllAsync_WithCancellationRequested_ReturnsCancelled()
    {
        // Arrange
        var hub = CreateTestHub();
        var testData = new TestData { Id = 1, Name = "Test" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await hub.SendToAllAsync(testData, cts.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Tests that SendToClientAsync returns failure when connection ID is null.
    /// </summary>
    [Fact]
    public async Task SendToClientAsync_WithNullConnectionId_ReturnsFailure()
    {
        // Arrange
        var hub = CreateTestHub();
        var testData = new TestData { Id = 1, Name = "Test" };

        // Act
        var result = await hub.SendToClientAsync(null!, testData, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("Connection ID");
    }

    /// <summary>
    /// Tests that SendToClientAsync returns failure when connection ID is empty.
    /// </summary>
    [Fact]
    public async Task SendToClientAsync_WithEmptyConnectionId_ReturnsFailure()
    {
        // Arrange
        var hub = CreateTestHub();
        var testData = new TestData { Id = 1, Name = "Test" };

        // Act
        var result = await hub.SendToClientAsync(string.Empty, testData, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("Connection ID");
    }

    /// <summary>
    /// Tests that SendToClientAsync returns cancelled when cancellation is requested.
    /// </summary>
    [Fact]
    public async Task SendToClientAsync_WithCancellationRequested_ReturnsCancelled()
    {
        // Arrange
        var hub = CreateTestHub();
        var connectionId = "test-connection-id";
        var testData = new TestData { Id = 1, Name = "Test" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await hub.SendToClientAsync(connectionId, testData, cts.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Tests that SendToGroupAsync returns failure when group name is null.
    /// </summary>
    [Fact]
    public async Task SendToGroupAsync_WithNullGroupName_ReturnsFailure()
    {
        // Arrange
        var hub = CreateTestHub();
        var testData = new TestData { Id = 1, Name = "Test" };

        // Act
        var result = await hub.SendToGroupAsync(null!, testData, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("Group name");
    }

    /// <summary>
    /// Tests that SendToGroupAsync returns failure when group name is empty.
    /// </summary>
    [Fact]
    public async Task SendToGroupAsync_WithEmptyGroupName_ReturnsFailure()
    {
        // Arrange
        var hub = CreateTestHub();
        var testData = new TestData { Id = 1, Name = "Test" };

        // Act
        var result = await hub.SendToGroupAsync(string.Empty, testData, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("Group name");
    }

    /// <summary>
    /// Tests that SendToGroupAsync returns cancelled when cancellation is requested.
    /// </summary>
    [Fact]
    public async Task SendToGroupAsync_WithCancellationRequested_ReturnsCancelled()
    {
        // Arrange
        var hub = CreateTestHub();
        var groupName = "test-group";
        var testData = new TestData { Id = 1, Name = "Test" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await hub.SendToGroupAsync(groupName, testData, cts.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Tests that GetConnectionCountAsync returns failure (not implemented).
    /// </summary>
    [Fact]
    public async Task GetConnectionCountAsync_Always_ReturnsFailure()
    {
        // Arrange
        var hub = CreateTestHub();

        // Act
        var result = await hub.GetConnectionCountAsync(CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("not implemented");
    }

    /// <summary>
    /// Tests that GetConnectionCountAsync returns cancelled when cancellation is requested.
    /// </summary>
    [Fact]
    public async Task GetConnectionCountAsync_WithCancellationRequested_ReturnsCancelled()
    {
        // Arrange
        var hub = CreateTestHub();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await hub.GetConnectionCountAsync(cts.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Creates a test hub instance with mocked dependencies.
    /// </summary>
    private TestHub CreateTestHub()
    {
        var hub = new TestHub(_logger);
        TestHubHelper.SetupHub(hub, _mockContext, _mockClients, _mockGroups);
        return hub;
    }

    /// <summary>
    /// Test hub implementation for testing.
    /// </summary>
    public class TestHub : ExxerHub<TestData>
    {
        public TestHub(ILogger<TestHub> logger)
            : base(logger)
        {
        }
    }

    /// <summary>
    /// Test data class for testing.
    /// </summary>
    public class TestData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
