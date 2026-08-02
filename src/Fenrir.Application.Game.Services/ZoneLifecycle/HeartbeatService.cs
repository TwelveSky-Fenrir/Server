using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class HeartbeatService : IHeartbeatService
{
    public HeartbeatOutcome Process(PlayerRuntimeState state, uint lastSend)
    {
        if (state.PrevSentHeartbeat != 0 && state.PrevSentHeartbeat == lastSend)
            return HeartbeatOutcome.Replayed;

        state.PrevSentHeartbeat = lastSend;
        state.LastSentHeartbeat = DateTime.UtcNow;
        return HeartbeatOutcome.Accepted;
    }
}
