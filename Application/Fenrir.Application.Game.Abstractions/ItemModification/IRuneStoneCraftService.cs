using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.ItemModification;

public interface IRuneStoneCraftService
{

        public ValueTask<RuneStoneCraftResult> CraftAsync(
        int sourcePage, int sourceSlot,
        int destinationPage, int destinationSlot,
        int statSlotSelector, int destinationPackedStat,
        bool secondInventoryPageAccessible,
        Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken);
}
