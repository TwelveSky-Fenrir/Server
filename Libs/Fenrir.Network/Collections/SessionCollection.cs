using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Fenrir.Network.Options;
using Fenrir.Network.Transport;
using Microsoft.Extensions.Options;

namespace Fenrir.Network.Collections;

/// <inheritdoc />
/// TODO: Why does session collection need to know about TMessage?
///  TODO Session should be interface?
public class SessionCollection(IOptions<FenrirServerOptions> options)
    : ISessionCollection<ISession>
{
    private FenrirServerOptions Options { get; } = options.Value;

    private ConcurrentDictionary<string, ISession> Sessions { get; } =
        new(StringComparer.InvariantCultureIgnoreCase);

    /// <inheritdoc />
    public int Count => Sessions.Count;

    /// <inheritdoc />
    public bool Any => !Sessions.IsEmpty;

    /// <inheritdoc />
    public bool IsFull => Options.MaxConnections > 0 && Count >= Options.MaxConnections;

    /// <inheritdoc />
    public void AddSession(ISession session)
    {
        Sessions.TryAdd(session.SessionId, session);
    }

    /// <inheritdoc />
    public ISession? GetSession(string sessionId)
    {
        return Sessions.GetValueOrDefault(sessionId);
    }

    /// <inheritdoc />
    public void RemoveSession(string sessionId)
    {
        Sessions.TryRemove(sessionId, out _);
    }

    /// <inheritdoc />
    public ISession? GetSession(Func<ISession, bool> predicate)
    {
        return Sessions.Values.FirstOrDefault(predicate);
    }

    /// <inheritdoc />
    public void RemoveSession(Func<ISession, bool> predicate)
    {
        var session = Sessions.Values.FirstOrDefault(predicate);

        if (session is null)
            return;

        Sessions.TryRemove(session.SessionId, out _);
    }

    /// <inheritdoc />
    public bool TryGetSession(string sessionId, [NotNullWhen(true)] out ISession? session)
    {
        return Sessions.TryGetValue(sessionId, out session);
    }

    /// <inheritdoc />
    public bool TryRemoveSession(string sessionId, [NotNullWhen(true)] out ISession? session)
    {
        return Sessions.TryRemove(sessionId, out session);
    }

    /// <inheritdoc />
    public bool TryGetSession(Func<ISession, bool> predicate,
        [NotNullWhen(true)] out ISession? session)
    {
        return (session = Sessions.Values.FirstOrDefault(predicate)) is not null;
    }

    /// <inheritdoc />
    public bool TryRemoveSession(Func<ISession, bool> predicate,
        [NotNullWhen(true)] out ISession? session)
    {
        return (session = Sessions.Values.FirstOrDefault(predicate)) is not null &&
               Sessions.TryRemove(session.SessionId, out _);
    }

    /// <inheritdoc />
    public IEnumerable<ISession> GetSessions(
        Func<ISession, bool>? predicate = null)
    {
        return predicate is null
            ? Sessions.Values
            : Sessions.Values.Where(predicate);
    }

    /// <inheritdoc />
    public void RemoveSessions(Func<ISession, bool>? predicate = null)
    {
        if (predicate is null)
        {
            Sessions.Clear();
            return;
        }

        foreach (var session in Sessions.Values.Where(predicate))
            Sessions.TryRemove(session.SessionId, out _);
    }

    /// <inheritdoc />
    public bool AnySession(Func<ISession, bool> predicate)
    {
        return Sessions.Values.Any(predicate);
    }

    /// <inheritdoc />
    public int CountSessions(Func<ISession, bool> predicate)
    {
        return Sessions.Values.Count(predicate);
    }

    /// <inheritdoc />
    public Task ExecuteAsync(Action<ISession> action,
        Func<ISession, bool>? predicate = null)
    {
        var sessions = predicate is null
            ? Sessions.Values
            : Sessions.Values.Where(predicate);

        return Task.WhenAll(sessions.Select(session => Task.Run(() => action(session))));
    }

    /// <inheritdoc />
    public Task ExecuteAsync(Func<ISession, ValueTask> action,
        Func<ISession, bool>? predicate = null)
    {
        var sessions = predicate is null
            ? Sessions.Values
            : Sessions.Values.Where(predicate);

        return Task.WhenAll(sessions.Select(session => action(session).AsTask()));
    }

    /// <inheritdoc />
    public Task ExecuteAsync(Func<ISession, Task> action,
        Func<ISession, bool>? predicate = null)
    {
        var sessions = predicate is null
            ? Sessions.Values
            : Sessions.Values.Where(predicate);

        return Task.WhenAll(sessions.Select(action));
    }
}