using Fenrir.Core.Wire;

namespace Fenrir.Core.Abstractions;

public interface ISessionRateLimiter
{
    public bool TryConsume(long sessionId, FenrirServer server, byte opcode);

    public bool TryConsumeGmCommand(long sessionId);

    public void Remove(long sessionId);
}
