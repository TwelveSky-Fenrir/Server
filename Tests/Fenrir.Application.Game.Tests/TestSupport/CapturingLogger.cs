using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     Minimal <see cref="ILogger{T}" /> test double that records every <see cref="BeginScope{TState}" /> state
///     and <see cref="Log{TState}" /> call, so tests can assert on structured logging (scope key/value pairs,
///     message templates) without standing up a real OTEL/console logging pipeline.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<object> Scopes { get; } = [];
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        Scopes.Add(state);
        return NoopScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception)));
    }

    /// <summary>
    ///     Reads the named scope properties off a captured <see cref="BeginScope{TState}" /> state -- every scope
    ///     opened via the <c>logger.BeginScope("template {Prop}", value)</c> convenience overload implements this
    ///     interface with one entry per placeholder plus a trailing "{OriginalFormat}".
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, object>> PropertiesOf(object scope) =>
        (IReadOnlyList<KeyValuePair<string, object>>)scope;

    private sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new();
        public void Dispose() { }
    }
}
