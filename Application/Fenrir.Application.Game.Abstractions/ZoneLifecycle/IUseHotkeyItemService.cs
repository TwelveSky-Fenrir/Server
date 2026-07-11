using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public enum UseHotkeyItemOutcome
{

        Disconnect,

        RejectedClean,

        Success
}

public interface IUseHotkeyItemService
{

        public ValueTask<UseHotkeyItemOutcome> UseAsync(Zone zone, PlayerRuntimeState state, int characterId, int page,
        int index, CancellationToken cancellationToken);
}
