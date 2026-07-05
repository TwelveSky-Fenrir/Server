using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public enum HeartbeatOutcome
{
    Replayed,
    Accepted
}

/// <summary>Business logic for CZ_HEARTBEAT_SEND (op151) -- see <c>HeartbeatHandler</c>'s remarks.</summary>
public interface IHeartbeatService
{
    public HeartbeatOutcome Process(PlayerRuntimeState state, uint lastSend);
}
