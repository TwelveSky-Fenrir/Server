using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.ZoneLifecycle.Services;

public enum HeartbeatOutcome
{
    Replayed,
    Accepted
}

/// <summary>Business logic for CZ_HEARTBEAT_SEND (op151) -- see <c>HeartbeatHandler</c>'s remarks.</summary>
public interface IHeartbeatService
{
    HeartbeatOutcome Process(PlayerRuntimeState state, uint lastSend);
}

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
