using Fenrir.Network.Abstractions;

namespace Fenrir.Network.Dispatch.RateLimiting;

public interface ISessionRateLimiter
{
    public bool TryConsume(long sessionId, FenrirServer server, byte opcode);

    public void Remove(long sessionId);
}
