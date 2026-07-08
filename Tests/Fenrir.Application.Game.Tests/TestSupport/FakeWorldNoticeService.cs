using Fenrir.Application.Game.Abstractions.Chat;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for IWorldNoticeService: records every broadcast content string (append-only) so
///     service test suites can assert a system notice was (or was not) raised, without a real ZoneRegistry
///     fan-out.
/// </summary>
internal sealed class FakeWorldNoticeService : IWorldNoticeService
{
    public List<string> Broadcasts { get; } = [];

    public void Broadcast(string content)
    {
        Broadcasts.Add(content);
    }
}
