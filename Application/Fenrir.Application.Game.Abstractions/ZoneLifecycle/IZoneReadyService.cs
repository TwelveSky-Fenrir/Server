using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public enum ZoneReadyOutcome
{
    Admitted,
    Rejected
}

public interface IZoneReadyService
{
    public ZoneReadyOutcome Validate(PlayerRuntimeState state, int tribe, int autoState);
}
