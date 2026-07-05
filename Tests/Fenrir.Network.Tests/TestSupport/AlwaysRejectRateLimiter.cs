using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.RateLimiting;

namespace Fenrir.Network.Tests.TestSupport;

/// <summary>
///     Rejects every opcode unconditionally, to exercise SessionLoop's RateLimited disconnect path without a real
///     token-bucket implementation.
/// </summary>
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
