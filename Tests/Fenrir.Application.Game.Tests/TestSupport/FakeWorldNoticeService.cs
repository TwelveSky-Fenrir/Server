using Fenrir.Application.Game.Abstractions.Chat;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeWorldNoticeService : IWorldNoticeService
{
    public List<string> Broadcasts { get; } = [];

    public void Broadcast(string content)
    {
        Broadcasts.Add(content);
    }
}
