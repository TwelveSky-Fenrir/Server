using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class HeartbeatService : IHeartbeatService
{
    public HeartbeatOutcome Process(PlayerRuntimeState state, uint lastSend)
    {
        // Anti-replay: a captured-and-resent heartbeat frame carries the exact same LastSend counter twice in
        // a row. A genuine client always advances it.
        if (state.PrevSentHeartbeat == lastSend)
            return HeartbeatOutcome.Replayed;

        state.PrevSentHeartbeat = lastSend;
        state.LastSentHeartbeat = DateTime.UtcNow;
        return HeartbeatOutcome.Accepted;
    }
}
