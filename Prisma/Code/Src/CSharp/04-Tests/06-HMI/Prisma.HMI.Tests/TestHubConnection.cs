namespace Prisma.HMI.Tests;

/// <summary>
/// Test double for HubConnection that allows full control for testing.
/// </summary>
internal sealed class TestHubConnection : HubConnection
{
    private readonly Dictionary<string, List<Delegate>> _handlers = new();
    private HubConnectionState _state = HubConnectionState.Disconnected;
    private readonly string? _authToken;
    private readonly bool _allowConnection;

    public TestHubConnection(string? authToken = null, bool allowConnection = true)
    {
        _authToken = authToken;
        _allowConnection = allowConnection;
        ConnectionId = "test-conn-" + Guid.NewGuid().ToString("N")[..8];
    }

    public override string? ConnectionId { get; }
    public override HubConnectionState State => _state;

    public override IDisposable On(string methodName, Type[] parameterTypes, Func<object?[], object, Task> handler, object state)
    {
        if (!_handlers.ContainsKey(methodName))
        {
            _handlers[methodName] = new List<Delegate>();
        }

        // Create a wrapper that will be called during simulation
        void TypedWrapper<T>(T arg)
        {
            var args = new object?[] { arg };
            handler(args, state).GetAwaiter().GetResult();
        }

        // Store the typed wrapper
        if (parameterTypes.Length == 1)
        {
            var wrapperType = typeof(Action<>).MakeGenericType(parameterTypes[0]);
            var wrapper = Delegate.CreateDelegate(wrapperType, this, nameof(DynamicInvoke));
            _handlers[methodName].Add(wrapper);

            // Also store the actual handler for invocation
            _handlers[$"{methodName}_actualHandler"] = new List<Delegate> { handler };
            _handlers[$"{methodName}_state"] = new List<Delegate> { (Action)(() => { /* store state */ }) };
        }

        return new DisposableAction(() => { /* cleanup if needed */ });
    }

    public override Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_allowConnection)
        {
            throw new HttpRequestException("Unauthorized", null, System.Net.HttpStatusCode.Unauthorized);
        }

        if (_authToken != null)
        {
            // Check if token is valid
            if (_authToken.StartsWith("valid-") || _authToken.StartsWith("valid-jwt"))
            {
                _state = HubConnectionState.Connected;
                return Task.CompletedTask;
            }
            else
            {
                throw new HttpRequestException("Unauthorized", null, System.Net.HttpStatusCode.Unauthorized);
            }
        }
        else if (_allowConnection)
        {
            _state = HubConnectionState.Connected;
            return Task.CompletedTask;
        }

        throw new HttpRequestException("Unauthorized", null, System.Net.HttpStatusCode.Unauthorized);
    }

    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        _state = HubConnectionState.Disconnected;
        return Task.CompletedTask;
    }

    public Task SimulateBroadcastAsync<T>(string methodName, T data)
    {
        // Find and invoke registered handlers
        if (_handlers.TryGetValue(methodName, out var handlers))
        {
            foreach (var handler in handlers)
            {
                if (handler is Action<T> typedHandler)
                {
                    typedHandler(data);
                }
            }
        }

        return Task.CompletedTask;
    }

    private void DynamicInvoke<T>(T arg)
    {
        // Placeholder for delegate creation
    }

    private sealed class DisposableAction : IDisposable
    {
        private readonly Action _action;

        public DisposableAction(Action action)
        {
            _action = action;
        }

        public void Dispose()
        {
            _action();
        }
    }
}
