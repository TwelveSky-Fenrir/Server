using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Tests.TestSupport;

internal sealed class CapturingLogger(LogLevel minimumLevel = LogLevel.Trace) : ILogger
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return NoopScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= minimumLevel;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new();

        public void Dispose()
        {
        }
    }
}
