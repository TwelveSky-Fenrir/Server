using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.Progression;

public enum AutoHuntToggleOutcome : byte
{
    Applied,

    Ignored,

    Disconnect
}

public readonly record struct AutoHuntToggleResult(AutoHuntToggleOutcome Outcome, bool Enabled);

public interface IAutoHuntToggleService
{
    public ValueTask<AutoHuntToggleResult> ToggleAsync(int characterId, Zone zone, PlayerRuntimeState state,
        AutoHuntToggleRequest packet, CancellationToken cancellationToken);
}
