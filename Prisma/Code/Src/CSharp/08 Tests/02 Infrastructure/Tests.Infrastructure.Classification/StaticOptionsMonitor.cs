namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

internal sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T> where T : class, new()
{
    private readonly T _value;

    public StaticOptionsMonitor(T value)
    {
        _value = value;
    }

    public T CurrentValue => _value;

    public T Get(string? name) => _value;

    public IDisposable OnChange(Action<T, string?> listener) => NullDisposable.Instance;

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose()
        {
        }
    }
}