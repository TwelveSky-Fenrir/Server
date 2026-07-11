using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.RateLimiting;

namespace Fenrir.Network.Tests.TestSupport;

internal sealed class AlwaysRejectRateLimiter : ISessionRateLimiter
{
    public bool TryConsume(long sessionId, FenrirServer server, byte opcode)
    {
        return false;
    }

    public void Remove(long sessionId)
    {
    }
}
