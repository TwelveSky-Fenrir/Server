using Fenrir.Core.Wire;

namespace Fenrir.Security.Abstractions;

public interface ISessionRateLimiter
{

        bool TryConsume(long sessionId, FenrirServer server, byte opcode);

        void Remove(long sessionId);
}
