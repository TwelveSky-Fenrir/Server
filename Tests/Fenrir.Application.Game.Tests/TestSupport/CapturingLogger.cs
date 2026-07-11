using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<object> Scopes { get; } = [];
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        Scopes.Add(state);
        return NoopScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception)));
    }

        public static IReadOnlyList<KeyValuePair<string, object>> PropertiesOf(object scope)
    {
        return (IReadOnlyList<KeyValuePair<string, object>>)scope;
    }

    private sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new();

        public void Dispose()
        {
        }
    }
}
