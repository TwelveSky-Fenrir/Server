using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public enum HeartbeatOutcome
{
    Replayed,
    Accepted
}

public interface IHeartbeatService
{
    public HeartbeatOutcome Process(PlayerRuntimeState state, uint lastSend);
}
